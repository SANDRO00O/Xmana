#!/usr/bin/env python3
"""
Static validation for Xmana (milestones 1-2).

This does NOT replace compiling. It checks everything that can be verified mechanically without the
Windows/.NET toolchain so most wiring mistakes are caught before a Visual Studio build:
  XML well-formedness, csproj<->disk, XAML<->code-behind wiring, resource key parity (en/ar),
  hard-coded UI strings, theme-resource references, manifest assets, image sizes, simple C# sanity,
  platform-neutral Core, and secret/URL guards.

Usage: python3 tools/validate.py      (exit code 1 on any failure)
"""
import glob, os, re, sys
import xml.etree.ElementTree as ET

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
APP = os.path.join(ROOT, "src", "Xmana.App")
CORE = os.path.join(ROOT, "src", "Xmana.Core")
TESTS = os.path.join(ROOT, "tests", "Xmana.Tests")
failures, passes = [], 0


def check(ok, message):
    global passes
    if ok:
        passes += 1
    else:
        failures.append(message)
        print("FAIL:", message)


def read(path):
    with open(path, encoding="utf-8") as fh:
        return fh.read()


def rel(path):
    return os.path.relpath(path, ROOT).replace("\\", "/")


def files(base, pattern):
    return sorted(p for p in glob.glob(os.path.join(base, "**", pattern), recursive=True)
                  if "/bin/" not in p.replace("\\", "/") and "/obj/" not in p.replace("\\", "/"))


# ---- 1. XML well-formedness ---------------------------------------------------------------------
xml_files = files(APP, "*.xaml") + files(APP, "*.resw") + files(APP, "*.appxmanifest") + files(APP, "*.csproj") \
    + files(APP, "*.xml") + files(CORE, "*.csproj") + files(TESTS, "*.csproj")
for p in xml_files:
    try:
        ET.parse(p)
        check(True, "")
    except ET.ParseError as e:
        check(False, f"{rel(p)} is not well-formed XML: {e}")

# ---- 2. csproj <-> disk -------------------------------------------------------------------------
NS = "{http://schemas.microsoft.com/developer/msbuild/2003}"
proj = ET.parse(os.path.join(APP, "Xmana.App.csproj")).getroot()
listed = {}
for item in proj.iter():
    tag = item.tag.replace(NS, "")
    inc = item.attrib.get("Include")
    if inc and tag in ("Compile", "Page", "Content", "PRIResource", "AppxManifest", "ApplicationDefinition", "EmbeddedResource"):
        path = inc.replace("\\", "/")
        listed[path] = tag
        check(os.path.exists(os.path.join(APP, path)), f"csproj lists missing file: {path}")
on_disk = set()
for pattern in ("*.cs", "*.xaml", "*.png", "*.resw", "*.appxmanifest", "*.rd.xml"):
    for p in files(APP, pattern):
        on_disk.add(os.path.relpath(p, APP).replace("\\", "/"))
for p in sorted(on_disk):
    check(p in listed, f"file on disk not in csproj: {p}")
check("Microsoft.NETCore.UniversalWindowsPlatform" in read(os.path.join(APP, "Xmana.App.csproj")), "UWP package reference present")
check("Windows.Universal" in read(os.path.join(APP, "Package.appxmanifest")), "manifest targets Windows.Universal")
check("WinUI" not in read(os.path.join(APP, "Xmana.App.csproj")), "no WinUI 3 (unsupported on Xbox)")

# ---- 3. XAML <-> code-behind wiring -------------------------------------------------------------
XNS = "{http://schemas.microsoft.com/winfx/2006/xaml}"
REQUIRED_NAMES = {
    "Shell/ShellPage": ["LayoutRoot", "ShellContent", "Header", "RailItems", "ContentFrame", "SplashOverlay"],
    "Pages/PlaceholderPage": ["TitleText", "BodyText"],
    "Pages/SettingsPage": ["TitleText", "Host", "Confirm"],
    "Pages/HomePage": ["Scroller", "OfflineNotice", "Hero", "Rails", "Skeleton", "MessagePanel", "MessageTitle", "MessageBody", "RetryButton", "ShimmerStoryboard"],
    "Controls/RailView": ["TitleText", "Scroller", "Cards"],
    "Controls/ResultsView": ["Scroller", "OfflineNotice", "Rows", "LoadMoreRetryButton", "MoreRing", "Skeleton", "MessagePanel", "MessageTitle", "MessageBody", "RetryButton"],
    "Pages/BrowsePage": ["TitleText", "FilterBar", "KindPanel", "MoviesButton", "SeriesButton", "SortBox", "LanguageBox", "Results"],
    "Pages/CategoriesPage": ["TitleText", "Scroller", "MainHeader", "MainRows", "MoreHeader", "MoreRows", "Skeleton", "MessagePanel", "MessageTitle", "MessageBody", "RetryButton"],
    "Pages/DetailsPage": ["Scroller", "OfflineNotice", "PosterBrush", "TitleText", "EpisodeLineText", "OtherTitleText", "MetaText", "CategoriesText", "LanguageText", "DescriptionText", "PlayButton", "LibraryPanel", "FavoriteButton", "WatchlistButton", "ActionsPanel", "PreviousButton", "NextButton", "SeriesButton", "SeriesSection", "EpisodesHeader", "SeasonBox", "EpisodesPanel", "SeriesMessage", "SeriesRetryButton", "SeriesRing", "CastHost", "RecommendedRail", "RelatedRail", "Skeleton", "MessagePanel", "MessageTitle", "MessageBody", "RetryButton"],
    "Pages/StaffPage": ["Header", "PersonCard", "TitlesHeader", "Results"],
    "Controls/ConfirmPanel": ["TitleText", "BodyText", "CancelButton", "ConfirmButton"],
    "Pages/LibraryListPage": ["TitleText", "Results"],
    "Pages/HistoryPage": ["TitleText", "ClearButton", "Results", "Confirm"],
    "Pages/FaqPage": ["TitleText", "Scroller", "ItemsHost", "Ring", "MessagePanel", "MessageTitle", "MessageBody", "RetryButton"],
    "Pages/PlayerPage": ["Root", "Video", "InputSink", "Scrims", "Overlay", "TopBar", "TitleText", "EpisodeText", "StatusPanel", "StatusRing", "StatusText", "NoticeText", "SkipButton", "NextCard", "NextHeader", "NextTitleText", "NextCountdownText", "PlayNowButton", "CancelNextButton", "BottomBar", "SeekBar", "PositionText", "RemainingText", "ButtonsRow", "PreviousButton", "BackSeekButton", "PlayPauseButton", "ForwardSeekButton", "NextButton", "OptionsButton", "ResumePanel", "ResumeTitle", "ResumeText", "ResumeButton", "StartOverButton", "FailurePanel", "FailureTitle", "FailureBody", "RetryButton", "ExitButton", "OptionsPanel", "OptionsHost"],
    "Pages/SearchPage": ["TitleText", "QueryBox", "ClearButton", "AdvancedButton", "IdleHost", "RecentPanel", "RecentHeader", "RecentList", "ClearRecentButton", "IdleHint", "Results"],
    "Pages/AdvancedSearchPage": ["TitleText", "EditButton", "FormScroller", "TitleBox", "StaffBox", "StaffNote", "YearFromBox", "YearToBox", "RatingBox", "TypeBox", "CategoryBox", "SortBox", "ValidationText", "ApplyButton", "ResetButton", "Results"],
    "Controls/HeroView": ["Backdrop", "HorizontalShadeBrush", "Info", "TitleText", "MetaText", "DescriptionText", "PreviousButton", "DetailsButton", "NextButton", "Dots"],
}
for stem in ("App", "Shell/ShellPage", "Pages/PlaceholderPage", "Pages/SettingsPage", "Pages/HomePage", "Controls/RailView", "Controls/HeroView", "Controls/ResultsView", "Pages/BrowsePage", "Pages/CategoriesPage", "Pages/SearchPage", "Pages/AdvancedSearchPage", "Pages/DetailsPage", "Pages/StaffPage", "Pages/PlayerPage", "Controls/ConfirmPanel", "Pages/LibraryListPage", "Pages/HistoryPage", "Pages/FaqPage"):
    xaml_path, cs_path = os.path.join(APP, stem + ".xaml"), os.path.join(APP, stem + ".xaml.cs")
    root = ET.parse(xaml_path).getroot()
    cls = root.attrib.get(XNS + "Class", "")
    ns, _, name = cls.rpartition(".")
    cs = read(cs_path)
    check(re.search(r"namespace\s+" + re.escape(ns) + r"\b", cs) is not None, f"{stem}: namespace {ns} matches x:Class")
    check(re.search(r"partial\s+class\s+" + name + r"\b", cs) is not None, f"{stem}: partial class {name} declared")
    check("InitializeComponent()" in cs, f"{stem}: InitializeComponent called")
    declared = {e.attrib[XNS + "Name"] for e in root.iter() if XNS + "Name" in e.attrib}
    for needed in REQUIRED_NAMES.get(stem, []):
        check(needed in declared, f"{stem}.xaml declares x:Name {needed}")
    for e in root.iter():
        for attr, value in e.attrib.items():
            if attr in ("Loaded", "Unloaded", "Click", "Navigated") and not value.startswith("{"):
                check(re.search(r"\b" + re.escape(value) + r"\s*\(", cs) is not None, f"{stem}: handler {value} exists in code-behind")

# ---- 3b. XAML compiler rules we have been bitten by ---------------------------------------------
NON_CONTROLS = {"Grid", "StackPanel", "Border", "Canvas", "TextBlock", "Image", "Viewbox", "Rectangle", "Path", "Polygon"}
CONTROL_ONLY_ATTRS = {"IsEnabled", "IsTabStop", "Padding" if False else "IsTabStop", "UseSystemFocusVisuals", "TabIndex"}
for p in files(APP, "*.xaml"):
    for e in ET.parse(p).getroot().iter():
        tag = e.tag.split("}")[-1]
        if tag in NON_CONTROLS:
            for attr in e.attrib:
                if attr in CONTROL_ONLY_ATTRS and attr != "TabIndex":
                    check(False, f"{rel(p)}: <{tag}> has no {attr} (not a Control) - XAML compiler error WMC0011")
            check(True, "")

# ---- 4. Hard-coded user-visible text ------------------------------------------------------------
TEXT_ATTRS = ("Text", "Content", "Header", "PlaceholderText", "Title", "Label", "ToolTip")
for p in [q for q in files(APP, "*.xaml") if not q.endswith("Wordmarks.xaml")]:
    root = ET.parse(p).getroot()
    for e in root.iter():
        for attr, value in e.attrib.items():
            local = attr.split("}")[-1]
            if local in TEXT_ATTRS or attr.endswith("AutomationProperties.Name"):
                check(value.startswith("{") or value.strip() == "", f"{rel(p)}: hard-coded UI text {local}=\"{value}\"")
        if e.text and e.text.strip() and not e.tag.endswith(("Setter", "Color", "FontFamily", "Double", "Thickness", "FontWeight")):
            check(False, f"{rel(p)}: literal text node in <{e.tag.split('}')[-1]}>: {e.text.strip()[:40]}")
for p in files(APP, "*.cs"):
    src = read(p)
    for m in re.finditer(r"\b(Text|Label|Content)\s*=\s*\"([^\"]+)\"", src):
        check(False, f"{rel(p)}: hard-coded UI string assigned to {m.group(1)}: {m.group(2)[:40]}")

# ---- 5. Resources: key parity + usage -----------------------------------------------------------
def resw(path):
    root = ET.parse(path).getroot()
    return {d.attrib["name"]: (d.findtext("value") or "") for d in root.findall("data")}
en = resw(os.path.join(APP, "Strings", "en-US", "Resources.resw"))
ar = resw(os.path.join(APP, "Strings", "ar", "Resources.resw"))
check(set(en) == set(ar), f"en/ar key sets identical (only en: {sorted(set(en)-set(ar))}, only ar: {sorted(set(ar)-set(en))})")
for lang, table in (("en", en), ("ar", ar)):
    for k, v in table.items():
        check(v.strip() != "", f"{lang}: empty value for {k}")
        check("." not in k, f"{lang}: key {k} contains '.' (reserved by resw property syntax)")
brand_keys = {"AppDisplayName"}
for k, v in ar.items():
    if k not in brand_keys:
        check(re.search(r"[\u0600-\u06FF]", v) is not None, f"ar: {k} contains no Arabic script: {v[:30]}")
for k, v in en.items():
    if k not in brand_keys and k != "AppDescription":
        check(re.search(r"[\u0600-\u06FF]", v) is None, f"en: {k} contains Arabic script")
destinations = re.findall(r"^\s{8}(\w+),\s*$", read(os.path.join(CORE, "Navigation", "NavigationDestination.cs")).split("enum NavigationDestination")[1].split("}")[0], re.M)
check(len(destinations) == 14 and destinations[-4:] == ["Details", "Staff", "Player", "Faq"], f"10 rail destinations + Details, Staff, Player, Faq parsed ({destinations})")
for d in destinations:
    check("Nav_" + d in en, f"resource Nav_{d} exists")
# dynamic keys: "Home_Section_" + HomeSectionKind
kinds = re.findall(r"^\s{8}(\w+),\s*$", read(os.path.join(CORE, "Home", "HomeModels.cs")).split("enum HomeSectionKind")[1].split("}")[0], re.M)
check(len(kinds) >= 6 and "Group" in kinds, f"HomeSectionKind parsed ({kinds})")
for kind in kinds:
    if kind != "Group":
        check("Home_Section_" + kind in en and "Home_Section_" + kind in ar, f"resource Home_Section_{kind} exists in both languages")
# dynamic keys: SortChoice resource keys
sort_keys = set(re.findall(r'"(Sort_\w+)"', read(os.path.join(CORE, "Discovery", "BrowseModels.cs"))))
check(len(sort_keys) >= 5, f"sort resource keys found ({sorted(sort_keys)})")
for key in sorted(sort_keys):
    check(key in en and key in ar, f"resource {key} exists in both languages")
used_keys = set()
for p in files(APP, "*.cs"):
    used_keys |= set(re.findall(r"\.Get\(\"([A-Za-z0-9_]+)\"\)", read(p)))
for k in sorted(used_keys):
    check(k in en, f"resource key used in code exists: {k}")
manifest = read(os.path.join(APP, "Package.appxmanifest"))
for k in re.findall(r"ms-resource:(\w+)", manifest):
    check(k in en and k in ar, f"manifest resource {k} exists in both languages")
check("Settings_UnofficialNotice" in en, "unofficial-client notice string present")
for name in ("Cinemana", "Shabakaty", "Microsoft", "Netflix"):
    check(name in en["Settings_UnofficialNotice"] and name in ar["Settings_UnofficialNotice"], f"unofficial notice names {name} in both languages")

# ---- 6. Theme-resource references resolve -------------------------------------------------------
keys = set()
for p in files(APP, "*.xaml"):
    keys |= {e.attrib[XNS + "Key"] for e in ET.parse(p).getroot().iter() if XNS + "Key" in e.attrib}
for p in files(APP, "*.xaml"):
    for ref in re.findall(r"\{(?:ThemeResource|StaticResource)\s+(\w+)\}", read(p)):
        check(ref in keys, f"{rel(p)}: resource reference {ref} is defined")
ctl = read(os.path.join(APP, "Theme", "Controls.xaml"))
for prop in re.findall(r"\{TemplateBinding\s+(\w+)\}", ctl):
    check(re.search(r"public\s+\w+\s+" + prop + r"\b", read(os.path.join(APP, "Controls", "ChoiceButton.cs"))) is not None, f"TemplateBinding {prop} exists on ChoiceButton")
app_xaml = read(os.path.join(APP, "App.xaml"))
order = [m for m in re.findall(r'Source="([^"]+)"', app_xaml)]
check(order[0] == "Theme/Colors.xaml", "Colors.xaml merged first")
for s in order:
    check(os.path.exists(os.path.join(APP, s)), f"merged dictionary exists: {s}")
for tpl in ("XmanaWordmarkFull", "XmanaWordmarkCompact", "XmanaWordmarkIcon"):
    check(tpl in keys, f"wordmark template {tpl} defined")
check("RequiresPointerMode" in read(os.path.join(APP, "App.xaml.cs")), "XY focus navigation enabled (RequiresPointerMode)")
check("BackRequested" in read(os.path.join(APP, "Shell", "ShellPage.xaml.cs")), "B button handled via BackRequested")
check("RightToLeft" in read(os.path.join(APP, "Services", "LocalizationService.cs")), "RTL flow direction supported")

# ---- 7. Manifest assets / identity --------------------------------------------------------------
for asset in re.findall(r'(?:Logo|Image)="(Assets\\[^"]+)"', manifest) + re.findall(r"<Logo>(Assets\\[^<]+)</Logo>", manifest):
    base, ext = os.path.splitext(asset.replace("\\", "/"))
    for scale in (100, 200, 400):
        check(os.path.exists(os.path.join(APP, f"{base}.scale-{scale}{ext}")), f"manifest asset {asset} has scale-{scale} file")
m = re.search(r'EntryPoint="([\w.]+)"', manifest)
check(m is not None and m.group(1) == "Xmana.App.App", "manifest EntryPoint = Xmana.App.App")
langs = re.findall(r'<Resource Language="([^"]+)"', manifest)
check(sorted(langs) == sorted(os.listdir(os.path.join(APP, "Strings"))), f"manifest languages {langs} match Strings folders")
capabilities = re.findall(r'<Capability\s+Name="([^"]+)"', manifest)
check(capabilities == ["internetClient"], f"manifest requests only internetClient (found {capabilities})")

# ---- 8. Image sizes -----------------------------------------------------------------------------
try:
    from PIL import Image
    EXPECT = {"Square44x44Logo": (44, 44), "Square71x71Logo": (71, 71), "Square150x150Logo": (150, 150),
              "Wide310x150Logo": (310, 150), "Square310x310Logo": (310, 310), "SplashScreen": (620, 300), "StoreLogo": (50, 50)}
    for base, (w, h) in EXPECT.items():
        for sc in (100, 200, 400):
            path = os.path.join(APP, "Assets", f"{base}.scale-{sc}.png")
            if os.path.exists(path):
                img = Image.open(path).convert("RGB")
                check(img.size == (w * sc // 100, h * sc // 100), f"{base}@{sc} size {img.size}")
                check(img.getpixel((0, 0)) == (0, 0, 0), f"{base}@{sc} background is solid black")
                cx, cy = img.size[0] // 2, img.size[1] // 2
                lit = [img.getpixel((x, y)) for x in range(img.size[0] // 4, 3 * img.size[0] // 4) for y in range(img.size[1] // 4, 3 * img.size[1] // 4)]
                check(max(p[0] for p in lit) >= 200, f"{base}@{sc} has a near-white mark")
except ImportError:
    print("note: Pillow missing, image checks skipped")

# ---- 9. C# sanity -------------------------------------------------------------------------------
for p in files(APP, "*.cs") + files(CORE, "*.cs") + files(TESTS, "*.cs"):
    src = re.sub(r'(?<!")@"(?:[^"]|"")*"', '""', read(p))              # verbatim string literals first
    src = re.sub(r'"(?:\\.|[^"\\])*"', '""', src)               # then ordinary string literals
    src = re.sub(r"'(?:\\.|[^'\\])'", "''", src)                # char literals
    src = re.sub(r"//.*", "", src)
    src = re.sub(r"/\*.*?\*/", "", src, flags=re.S)
    for o, c in ("{}", "()", "[]"):
        check(src.count(o) == src.count(c), f"{rel(p)}: balanced {o}{c} ({src.count(o)} vs {src.count(c)})")
    if p.startswith(APP) or p.startswith(CORE):
        for pattern, why in ((r"\bnew\(\)", "C# 9 target-typed new"), (r"\brecord\b\s+\w", "C# 9 record"), (r"\bis not\b", "C# 9 'is not'"), (r"\binit;", "C# 9 init")):
            check(re.search(pattern, src) is None, f"{rel(p)}: avoids {why} (app/core pin LangVersion 8)")
for p in files(CORE, "*.cs"):
    check("Windows." not in read(p) and "using System.Windows" not in read(p), f"{rel(p)}: Core stays platform-neutral")
check("netstandard2.0" in read(os.path.join(CORE, "Xmana.Core.csproj")), "Core targets netstandard2.0")

# ---- 9b. Missing "using" directives for common UWP types (a class of mistake only a compiler normally finds) -----
UWP_TYPES = {
    "Windows.UI.Xaml.Controls": ["Page", "UserControl", "Button", "ComboBox", "StackPanel", "TextBlock", "TextBox", "ScrollViewer",
                                 "ProgressRing", "Control", "Orientation", "ScrollMode", "ScrollBarVisibility", "ZoomMode",
                                 "TextChangedEventArgs", "SelectionChangedEventArgs", "Image", "ImageBrush_"],
    "Windows.UI.Xaml": ["Visibility", "Thickness", "FrameworkElement", "RoutedEventArgs", "DependencyProperty", "HorizontalAlignment",
                        "FocusState", "SizeChangedEventArgs", "Application", "Window"],
    "Windows.UI.Xaml.Navigation": ["NavigationEventArgs"],
    "Windows.UI.Xaml.Media": ["ImageBrush", "Brush", "SolidColorBrush"],
    "Windows.UI.Xaml.Input": ["KeyRoutedEventArgs"],
    "Windows.UI.Core": ["CoreDispatcher", "CoreDispatcherPriority"],
}
for p in files(APP, "*.cs"):
    raw = read(p)
    code = re.sub(r'@"(?:[^"]|"")*"', '""', raw)
    code = re.sub(r'"(?:\\.|[^"\\])*"', '""', code)
    code = re.sub(r"//.*", "", code)
    code = re.sub(r"/\*.*?\*/", "", code, flags=re.S)
    declared_namespace = re.search(r"^namespace\s+([\w.]+)", code, re.M)
    for ns, names in UWP_TYPES.items():
        if re.search(r"^using\s+" + re.escape(ns) + r"\s*;", code, re.M):
            continue
        for name in names:
            # the bare name used as a type (not qualified by a namespace, not a member access like x.Image)
            if re.search(r"(?<![\w.])" + re.escape(name) + r"(?![\w])\s*(?:[\w<(,>)\]{]|:)", code) and re.search(r"(?<![\w.])(?:class\s+\w+\s*:\s*|new\s+|\(\s*|:\s*|<\s*|\s)" + re.escape(name) + r"(?![\w.])", code):
                if name in ("Image", "Window", "Application", "Control", "Page", "Brush", "Button") and re.search(r"\b" + name + r"\b", code) is None:
                    continue
                check(False, f"{rel(p)}: uses {name} but is missing 'using {ns};'")
                break

# ---- 9c. Static UWP classes used as Type.Member (the pattern above only sees type names followed by an identifier) --------
STATIC_UWP = {
    "Windows.UI.Xaml.Automation": ["AutomationProperties"],
    "Windows.UI.Xaml.Input": ["FocusManager"],
    "Windows.UI.Xaml.Media.Animation": ["Storyboard"],
    "Windows.UI.Xaml": ["Application", "Window", "VisualStateManager"],
    "Windows.Storage": ["ApplicationData"],
    "Windows.ApplicationModel": ["Package"],
    "Windows.System.Profile": ["AnalyticsInfo"],
    "Windows.System.UserProfile": ["GlobalizationPreferences"],
}
for p in files(APP, "*.cs"):
    code = re.sub(r'@"(?:[^"]|"")*"', '""', read(p))
    code = re.sub(r'"(?:\\.|[^"\\])*"', '""', code)
    code = re.sub(r"//.*", "", code)
    code = re.sub(r"/\*.*?\*/", "", code, flags=re.S)
    for ns, names in STATIC_UWP.items():
        if re.search(r"^using\s+" + re.escape(ns) + r"\s*;", code, re.M):
            continue
        for name in names:
            if re.search(r"(?<![\w.])" + re.escape(name) + r"\.\w", code):
                check(False, f"{rel(p)}: uses {name}.<member> but is missing 'using {ns};'")

# ---- 10. Security / scope guards ----------------------------------------------------------------
ENDPOINT_FILE = os.path.join(CORE, "Api", "CinemanaEndpoints.cs")
for p in files(APP, "*.cs") + files(CORE, "*.cs") + files(APP, "*.xaml"):
    src = read(p)
    if p != ENDPOINT_FILE:
        urls = [u for u in re.findall(r"https?://[^\s\"'<>]+", src) if "schemas.microsoft.com" not in u and "www.w3.org" not in u]
        check(not urls, f"{rel(p)}: URLs belong only in CinemanaEndpoints.cs ({urls[:1]})")
    check(re.search(r"(?i)bearer|password|oauth|client_secret|api[_-]?key|authorization", src) is None, f"{rel(p)}: no credential/auth vocabulary")
check(not glob.glob(os.path.join(ROOT, "**", "*.pfx"), recursive=True), "no signing keys committed")

# every URL in CinemanaEndpoints.cs must be on an allow-listed host
endpoint_src = read(ENDPOINT_FILE)
hosts = set(re.findall(r"https://([a-z0-9.\-]+)/", endpoint_src))
check(hosts == {"cinemana.shabakaty.cc", "recommend.shabakaty.cc", "rating.shabakaty.cc"}, f"endpoint hosts are exactly the documented ones ({sorted(hosts)})")
validator_src = read(os.path.join(CORE, "Http", "UrlValidator.cs"))
for h in hosts:
    check(f'"{h}"' in validator_src, f"UrlValidator allow-lists {h}")

# ---- 11. Milestone 2: endpoint coverage vs API.md -----------------------------------------------
api_md = read(os.path.join(ROOT, "API.md"))
DOCUMENTED = ["allVideoInfo", "relatedVideos", "checkVideoParentalLevel", "videoComment", "transcoddedFiles", "translationFiles",
              "skippingDurations", "video_thumbnails", "videoSeasonNumber", "videoSeason", "banner", "latestMovies", "latestSeries",
              "featuredMovies", "newlyVideosItems", "latestVideosItems", "popularFilms", "mainCategories", "subCategories",
              "categoryLangFreqVideo", "videosByCategory", "videosByCategoryAndLanguage", "video/V/2", "AdvancedSearch", "videoGroups",
              "videoListPagination", "collectionsId", "collectionVideos", "staff", "faqQuestion"]
for name in DOCUMENTED:
    check(name in api_md, f"API.md documents {name}")
    check('"' + name in endpoint_src, f"CinemanaEndpoints implements {name}")
iface = read(os.path.join(CORE, "Api", "ICinemanaApi.cs"))
impl = read(os.path.join(CORE, "Api", "CinemanaApi.cs"))
for m in re.findall(r"Task<[^>]*(?:<[^>]*>)*[^>]*>\s+(\w+Async)\(", iface):
    check(re.search(r"public\s+(?:async\s+)?Task<.*>\s+" + m + r"\(", impl) is not None, f"CinemanaApi implements {m}")
tests_text = "".join(read(p) for p in files(TESTS, "*.cs"))
for m in sorted(set(re.findall(r"(\w+Async)\(", iface))):
    check(m in tests_text, f"a test exercises ICinemanaApi.{m}")
check(re.search(r"(?i)\b(login|register|signin|signup|logout)\w*\(", iface) is None, "ICinemanaApi has no authentication methods")

# every JSON field the mappers read must be documented in API.md (nothing invented)
field_names = set()
for p in files(os.path.join(CORE, "Api", "Mapping"), "*.cs"):
    for m in re.finditer(r'JsonRead\.Get\(\s*\w+,\s*"([^"]+)"\)', read(p)):
        field_names.add(m.group(1))
check(len(field_names) > 50, f"mapper field names discovered ({len(field_names)})")
for name in sorted(field_names):
    check(name in api_md, f"mapper reads field '{name}' which API.md does not mention")

# ---- 12. Milestone 10: final cleanup gate -------------------------------------------------------
import re as _re
TODO_PATTERN = _re.compile(r"(?://|/\*)\s*(TODO|FIXME|HACK)\b", _re.IGNORECASE)
for p in files(APP, "*.cs") + files(CORE, "*.cs") + files(TESTS, "*.cs"):
    for line in read(p).splitlines():
        check(not TODO_PATTERN.search(line), f"{rel(p)}: leftover marker comment: {line.strip()[:80]}")
for p in files(APP, "*.cs") + files(CORE, "*.cs"):
    src = read(p)
    check("Console.WriteLine" not in src, f"{rel(p)}: Console.WriteLine is not the logging channel (use IXmanaLogger)")
    check("Debug.Assert(" not in src, f"{rel(p)}: Debug.Assert must not ship (compiled out in Release; never a runtime guard)")

# version policy: assembly/package versions must match and look like a real release, not the 0.1.0.0 placeholder
asm_versions = set(re.findall(r'AssemblyVersion\("([\d.]+)"\)', read(os.path.join(APP, "Properties", "AssemblyInfo.cs"))))
asm_versions |= set(re.findall(r'AssemblyFileVersion\("([\d.]+)"\)', read(os.path.join(APP, "Properties", "AssemblyInfo.cs"))))
pkg_version = re.search(r'Identity[^>]*Version="([\d.]+)"', read(os.path.join(APP, "Package.appxmanifest")))
check(len(asm_versions) == 1, f"AssemblyVersion and AssemblyFileVersion agree ({asm_versions})")
check(pkg_version is not None and pkg_version.group(1) in asm_versions, "Package.appxmanifest Identity Version matches the assembly version")
check("0.1.0.0" not in asm_versions, "version has been bumped past the development placeholder 0.1.0.0")

print(f"\n{passes} checks passed, {len(failures)} failed")
sys.exit(1 if failures else 0)
