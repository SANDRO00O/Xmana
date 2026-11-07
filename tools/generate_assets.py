#!/usr/bin/env python3
"""Generates Xmana raster branding assets from the wordmark spec:
solid black background + centered near-white 'Xmana' wordmark.
Run: python3 tools/generate_assets.py  (requires Pillow and the Inter font)."""
import os
from PIL import Image, ImageDraw, ImageFont

FONT = "/usr/share/fonts/opentype/inter/Inter-Bold.otf"
BG = (0, 0, 0, 255)
FG = (245, 245, 245, 255)
OUT = os.path.join(os.path.dirname(__file__), "..", "src", "Xmana.App", "Assets")
BRAND = os.path.join(os.path.dirname(__file__), "..", "assets", "branding")
os.makedirs(OUT, exist_ok=True)
os.makedirs(BRAND, exist_ok=True)

def wordmark(w, h, width_ratio, name, text="Xmana", tracking=-0.02):
    """Fits the wordmark inside width_ratio of w (keeps it inside the safe area)."""
    img = Image.new("RGBA", (w, h), BG)
    d = ImageDraw.Draw(img)
    size = h
    while size > 4:
        f = ImageFont.truetype(FONT, size)
        l, t, r, b = d.textbbox((0, 0), text, font=f)
        if (r - l) <= w * width_ratio and (b - t) <= h * 0.5:
            break
        size -= 2
    f = ImageFont.truetype(FONT, size)
    l, t, r, b = d.textbbox((0, 0), text, font=f)
    x = (w - (r - l)) / 2 - l
    y = (h - (b - t)) / 2 - t
    d.text((x, y), text, font=f, fill=FG)
    img.convert("RGB").save(os.path.join(OUT, name))

def icon_x(w, h, name):
    """Small/icon variant: a single bold X on black (same typeface)."""
    img = Image.new("RGBA", (w, h), BG)
    d = ImageDraw.Draw(img)
    size = int(min(w, h) * 0.78)
    f = ImageFont.truetype(FONT, size)
    l, t, r, b = d.textbbox((0, 0), "X", font=f)
    d.text(((w - (r - l)) / 2 - l, (h - (b - t)) / 2 - t), "X", font=f, fill=FG)
    img.convert("RGB").save(os.path.join(OUT, name))

# Splash 620x300 @ scale-200 = 1240x600
wordmark(1240, 600, 0.42, "SplashScreen.scale-200.png")
wordmark(620, 300, 0.42, "SplashScreen.scale-100.png")
wordmark(1860, 900, 0.42, "SplashScreen.scale-300.png")
# Square150x150 (wordmark inside tile, generous safe margin)
wordmark(300, 300, 0.78, "Square150x150Logo.scale-200.png")
wordmark(150, 150, 0.78, "Square150x150Logo.scale-100.png")
# Wide310x150
wordmark(620, 300, 0.5, "Wide310x150Logo.scale-200.png")
wordmark(310, 150, 0.5, "Wide310x150Logo.scale-100.png")
# Square310x310
wordmark(620, 620, 0.78, "LargeTile.scale-200.png")
# Square44x44 app icon (icon variant)
icon_x(88, 88, "Square44x44Logo.scale-200.png")
icon_x(44, 44, "Square44x44Logo.scale-100.png")
for s in (16, 24, 32, 48, 256):
    icon_x(s, s, f"Square44x44Logo.targetsize-{s}.png")
    icon_x(s, s, f"Square44x44Logo.targetsize-{s}_altform-unplated.png")
# Store logo 50x50 (@200 = 100)
icon_x(100, 100, "StoreLogo.scale-200.png")
icon_x(50, 50, "StoreLogo.scale-100.png")
# Badge-less preview
wordmark(1920, 1080, 0.28, os.path.join("..", "..", "..", "assets", "branding", "wordmark-1080p.png"))
print("assets generated")
