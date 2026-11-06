# Demo only: copies fish sprites from the extracted asset folder D:/assetripper/fishjam into Assets/Resources/Art
# under our species names (to be replaced by our own art later). Run AFTER import_pixel.py, which it overrides:
#   D:/luyentayunityxgame/.artvenv/Scripts/python.exe -I ArtSource/import_fishjam.py
import os
from PIL import Image

SRC = "D:/assetripper/fishjam/Assets/FishSort/Image/Fish/fish_%d.png"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Art")

# our species id -> source fish number (all face left, as the game expects)
PICK = {"chep": 382, "noc": 224, "rophi": 94, "cathu": 151, "ngu": 213, "muc": 66, "camap": 289}

if __name__ == "__main__":
    for name, n in PICK.items():
        im = Image.open(SRC % n).convert("RGBA")
        im = im.crop(im.getbbox())
        im.thumbnail((320, 320), Image.LANCZOS)
        im.save(os.path.join(OUT, "fish_" + name + ".png"))
        print(name, im.size, round(im.width / im.height, 2))
