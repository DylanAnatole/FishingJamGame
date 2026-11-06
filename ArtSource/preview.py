# Contact sheet of the generated art, with fish tinted like in game, to check by eye.
import os
from PIL import Image, ImageChops
A = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art")
def hexc(h): v = int(h.lstrip('#'), 16); return ((v >> 16) & 255, (v >> 8) & 255, v & 255, 255)
sheet = Image.new("RGBA", (1100, 1100), (29, 106, 128, 255))
fish = [("chep", "#E8A33D"), ("noc", "#CDB64E"), ("rophi", "#8FA67A"), ("cathu", "#6FA3B5"), ("ngu", "#3E78C2"), ("muc", "#B07AA1"), ("camap", "#6E7F8A")]
x, y = 10, 10
for n, c in fish:
    im = Image.open(os.path.join(A, f"fish_{n}.png")).convert("RGBA")
    im = ImageChops.multiply(im, Image.new("RGBA", im.size, hexc(c)))
    if x + im.width > 1090: x, y = 10, y + 240
    sheet.alpha_composite(im, (x, y)); x += im.width + 20
tiles = [("giun", "#F6C1C7"), ("tom", "#FFD3B0"), ("ngo", "#FFE9A3"), ("banhmi", "#E8D2B0"), ("cacon", "#B9DCF5"), ("moigia", "#F2B6E6"), ("cuon", "#D9CCF2"), ("dao", "#C9D3D6"), ("rong", "#9FB5A0")]
y = 760
for i, (n, c) in enumerate(tiles):
    sheet.paste(Image.new("RGBA", (112, 112), hexc(c)), (10 + i * 120, y))
    sheet.alpha_composite(Image.open(os.path.join(A, f"tile_{n}.png")).convert("RGBA").resize((104, 104)), (14 + i * 120, y + 4))
y = 900
for n in ("boat", "hook", "branch", "seabed"):
    im = Image.open(os.path.join(A, f"{n}.png")).convert("RGBA")
    sheet.alpha_composite(im, (x if False else [10, 290, 350, 540][["boat", "hook", "branch", "seabed"].index(n)], y))
sheet.save(os.path.join(os.path.dirname(__file__), "preview.png"))
