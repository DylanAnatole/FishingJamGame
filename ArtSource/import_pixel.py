# Copies the pixel-art sprites picked from the itch.io packs in D:/luyentayunityxgame/asset/protyotype into
# Assets/Resources/Art under the names the game loads. Run AFTER make_art.py (it overwrites some of its files):
#   D:/luyentayunityxgame/.artvenv/Scripts/python.exe -I ArtSource/import_pixel.py
# Internal prototype only: the CraftPix free packs are non-commercial (see their License.txt).
import os
from PIL import Image

SRC = "D:/luyentayunityxgame/asset/protyotype"
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Art")
CATCH = SRC + "/cha/3 Objects/Catch/"
ICONS = SRC + "/cha/4 Icons/"
COAST = SRC + "/Tiny Coast Pack/"


def load(path, box=None):
    im = Image.open(path).convert("RGBA")
    return im.crop(box) if box else im


def save(im, name, scale):
    # nearest-neighbour upscale keeps the pixels crisp once Unity filters the texture
    im.resize((im.width * scale, im.height * scale), Image.NEAREST).save(os.path.join(OUT, name + ".png"))


def first_frame(path):
    # the Catch sprites hold 2 swimming frames side by side
    im = load(path)
    return im.crop((0, 0, im.width // 2, im.height))


def fish(name, file):
    # the game draws fish head-left; these swim to the right
    im = first_frame(CATCH + file).transpose(Image.FLIP_LEFT_RIGHT)
    save(im.crop(im.getbbox()), "fish_" + name, 10)


def tile(name, im):
    # a 1-pixel dark outline so thin icons (rods, lines) still read on the pale tile colours
    im = im.crop(im.getbbox())
    out = Image.new("RGBA", (im.width + 2, im.height + 2))
    dark = Image.new("RGBA", im.size, (40, 28, 30, 255))
    for dx, dy in ((0, 1), (2, 1), (1, 0), (1, 2)):
        out.paste(dark, (dx, dy), im)
    out.alpha_composite(im, (1, 1))
    save(out, "tile_" + name, 8)


def boat_frames():
    # 4-frame loops for the menus: rowing (loading screen) and idling with the rod (home)
    hull = load(SRC + "/cha/3 Objects/Boat.png")
    for kind, file, dy in (("row", "Fisherman_row.png", 15), ("idle", "Fisherman_idle.png", 0)):
        sheet = load(SRC + "/cha/1 Fisherman/" + file)
        for i in range(4):
            c = Image.new("RGBA", (80, 60))
            c.alpha_composite(sheet.crop((i * 48, 0, i * 48 + 48, 48)), (14, dy))
            c.alpha_composite(hull, (3, 42))
            save(c, f"boat_{kind}_{i}", 5)


def boat():
    # fisherman (rod held out to the right) standing in the rowing boat
    man = load(SRC + "/cha/1 Fisherman/Fisherman_fish.png", (0, 0, 48, 48))
    hull = load(SRC + "/cha/3 Objects/Boat.png")
    c = Image.new("RGBA", (80, 60))
    c.alpha_composite(man, (14, 0))
    c.alpha_composite(hull, (3, 42))
    save(c, "boat", 5)


if __name__ == "__main__":
    for n, f in (("chep", "3.png"), ("noc", "2.png"), ("rophi", "1.png"), ("cathu", "4.png"),
                 ("ngu", "7.png"), ("muc", "5.png"), ("camap", "6.png")):
        fish(n, f)
    tile("giat", load(ICONS + "Icons_03.png"))                              # rod bent with the bobber up
    tile("noi", load(ICONS + "Icons_01.png"))                               # rod with a loose, curling line
    tile("thu", load(COAST + "Other/fishing_rod_16x16.png", (0, 0, 16, 16)))  # rod with the reel loop
    tile("ghim", load(SRC + "/cha/3 Objects/Fish-rod.png"))                # straight rod held steady
    tile("rong", load(SRC + "/cha/3 Objects/Grass1.png"))                  # weed
    boat()
    boat_frames()
    save(load(SRC + "/cha/3 Objects/Fishing_hut.png"), "hut", 4)
    save(load(COAST + "Tiny Adventure Pack/Other/Coin.png"), "coin", 12)
    print("done")
