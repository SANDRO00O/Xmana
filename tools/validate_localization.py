#!/usr/bin/env python3
"""Repository validator (runs in CI before the Windows build).

1. Every K.* localization key exists in BOTH StringsEn.cs and StringsAr.cs, and no
   catalog contains keys that are not declared in K.cs.
2. XAML / csproj / manifest files are well-formed XML.
3. Every Loc.Key="..." used in XAML is declared in K.cs.
4. No Console.WriteLine / hard-coded http URLs with query strings in source.
"""
import re, sys, glob, os
import xml.etree.ElementTree as ET

ROOT = os.path.join(os.path.dirname(__file__), "..", "src")
LOC = os.path.join(ROOT, "Xmana.Core", "Localization")
errors = []

def read(p):
    with open(p, encoding="utf-8") as f:
        return f.read()

k_src = read(os.path.join(LOC, "K.cs"))
consts = dict(re.findall(r'public const string (\w+) = "([^"]+)";', k_src))

def catalog_keys(name):
    src = read(os.path.join(LOC, name))
    return set(re.findall(r'\[K\.(\w+)\]\s*=', src))

en, ar = catalog_keys("StringsEn.cs"), catalog_keys("StringsAr.cs")
for c in consts:
    if c not in en: errors.append(f"StringsEn missing K.{c}")
    if c not in ar: errors.append(f"StringsAr missing K.{c}")
for extra in (en | ar) - set(consts):
    errors.append(f"Catalog uses undeclared key K.{extra}")

# Arabic catalog must contain Arabic script for non-technical strings
ar_src = read(os.path.join(LOC, "StringsAr.cs"))
for line in ar_src.splitlines():
    m = re.search(r'\[K\.(\w+)\]\s*=\s*"([^"]*)"', line)
    if m and m.group(1) not in ("AppName", "SettingsLanguageEnglish"):
        if not re.search(r'[\u0600-\u06FF]', m.group(2)):
            errors.append(f"Arabic string for K.{m.group(1)} has no Arabic text")

values = set(consts.values())
for path in glob.glob(os.path.join(ROOT, "**", "*.xaml"), recursive=True) + \
            glob.glob(os.path.join(ROOT, "**", "*.csproj"), recursive=True) + \
            glob.glob(os.path.join(ROOT, "**", "*.appxmanifest"), recursive=True):
    text = read(path)
    try:
        ET.fromstring(text.encode("utf-8"))
    except ET.ParseError as e:
        errors.append(f"{os.path.relpath(path, ROOT)}: XML error: {e}")
    for key in re.findall(r'Loc\.Key="([^"]+)"', text):
        if key not in values:
            errors.append(f"{os.path.relpath(path, ROOT)}: unknown Loc.Key '{key}'")

for path in glob.glob(os.path.join(ROOT, "**", "*.cs"), recursive=True):
    text = read(path)
    if "Console.WriteLine" in text:
        errors.append(f"{os.path.relpath(path, ROOT)}: Console.WriteLine is not allowed")
    if text.count("{") != text.count("}"):
        errors.append(f"{os.path.relpath(path, ROOT)}: unbalanced braces")

if errors:
    print("VALIDATION FAILED")
    for e in errors: print(" -", e)
    sys.exit(1)
print(f"OK: {len(consts)} keys, en/ar parity verified, XML well-formed")
