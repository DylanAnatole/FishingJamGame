# Draws the prototype's sprites into Assets/Resources/Art. Run with Pillow:
#   D:/luyentayunityxgame/.artvenv/Scripts/python.exe ArtSource/make_art.py
# Fish are drawn in greys and tinted in Unity by Species.color, so colours stay editable in the Inspector.
# Tile icons are drawn in full colour and sit on the tile's own colour.
import math
import os
from PIL import Image, ImageDraw

OUT = os.path.join(os.path.dirname(__file__), "..", "Assets", "Resources", "Art")
S = 4  # draw 4x larger, then shrink for smooth edges

INK = (16, 38, 46, 255)
LIGHT, MID, SHADE, LINE = (255, 255, 255, 255), (215, 215, 215, 255), (165, 165, 165, 255), (70, 70, 70, 255)


def canvas(w, h):
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def save(im, name, w, h):
    os.makedirs(OUT, exist_ok=True)
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))


def P(*pts, W=1, H=1):
    return [(x * W, y * H) for x, y in pts]


# ---------- fish (head on the left) ----------

def fish(name, w, h, kind):
    im, d = canvas(w, h)
    W, H = w * S, h * S
    lw = max(3, int(H * 0.035))

    if kind == "squid":
        # mantle pointing right, tentacles trailing left like a swimming squid seen from the side
        for i in range(5):
            y = 0.3 + i * 0.1
            d.line(P((0.42, y), (0.2, y + 0.05), (0.03, y - 0.02 + 0.04 * (i % 2)), W=W, H=H), fill=SHADE, width=int(H * 0.07), joint="curve")
        d.polygon(P((0.36, 0.2), (0.88, 0.32), (1.0, 0.5), (0.88, 0.68), (0.36, 0.8), W=W, H=H), fill=MID, outline=LINE, width=lw)
        d.polygon(P((0.85, 0.3), (0.99, 0.12), (0.97, 0.5), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
        d.polygon(P((0.85, 0.7), (0.99, 0.88), (0.97, 0.5), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
        for x in (0.55, 0.68, 0.8):
            d.ellipse([x * W, 0.42 * H, x * W + H * 0.08, 0.5 * H], fill=SHADE)
        d.ellipse([0.36 * W, 0.36 * H, 0.36 * W + H * 0.2, 0.56 * H], fill=LIGHT, outline=LINE, width=lw)
        d.ellipse([0.39 * W, 0.41 * H, 0.39 * W + H * 0.1, 0.51 * H], fill=INK)
        save(im, name, w * 2, h * 2)
        return

    top, bot = (0.12, 0.9) if kind != "puffer" else (0.1, 0.92)
    right = 0.8 if kind != "puffer" else 0.84
    # tail
    if kind == "shark":
        d.polygon(P((0.74, 0.5), (0.99, 0.05), (0.9, 0.5), (0.97, 0.85), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
    else:
        d.polygon(P((0.72, 0.5), (0.99, 0.15), (0.92, 0.5), (0.99, 0.85), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
    # dorsal and belly fins
    if kind == "shark":
        d.polygon(P((0.36, 0.3), (0.5, 0.0), (0.58, 0.3), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
        d.polygon(P((0.3, 0.75), (0.4, 0.99), (0.46, 0.75), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
    elif kind == "puffer":
        for i in range(14):
            a = math.pi * 2 * i / 14
            cx, cy, rx, ry = 0.43, 0.51, 0.41, 0.41
            base = (cx + rx * math.cos(a), cy + ry * math.sin(a))
            tip = (cx + (rx + 0.08) * math.cos(a), cy + (ry + 0.08) * math.sin(a))
            d.line(P(base, tip, W=W, H=H), fill=LINE, width=lw)
    else:
        d.polygon(P((0.25, 0.2), (0.4, 0.01), (0.6, 0.06), (0.62, 0.22), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
        d.polygon(P((0.35, 0.8), (0.45, 0.99), (0.55, 0.8), W=W, H=H), fill=SHADE, outline=LINE, width=lw)
    # body with a light belly
    body = [0.02 * W, top * H, right * W, bot * H]
    d.ellipse(body, fill=MID)
    belly = Image.new("RGBA", im.size, (0, 0, 0, 0))
    bd = ImageDraw.Draw(belly)
    bd.ellipse([0.06 * W, 0.5 * H, 0.74 * W, 0.98 * H], fill=LIGHT)
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).ellipse(body, fill=255)
    im.paste(belly, (0, 0), Image.composite(belly, Image.new("RGBA", im.size), mask).split()[3])
    d = ImageDraw.Draw(im)

    # patterns
    if kind == "carp":
        for r in range(2):
            for c in range(5):
                x = 0.3 + c * 0.09 + r * 0.045
                y = 0.32 + r * 0.14
                d.arc([x * W, y * H, x * W + 0.08 * W, y * H + 0.14 * H], 200, 340, fill=SHADE, width=lw)
    elif kind in ("tilapia", "mackerel"):
        n = 4 if kind == "tilapia" else 6
        for i in range(n):
            x = 0.3 + i * (0.42 / n)
            if kind == "tilapia":
                d.line(P((x, 0.2), (x - 0.03, 0.75), W=W, H=H), fill=SHADE, width=lw * 2)
            else:
                d.arc([x * W, 0.15 * H, x * W + 0.1 * W, 0.5 * H], 180, 360, fill=LINE, width=lw)
    elif kind == "tuna":
        for i in range(5):
            x = 0.6 + i * 0.035
            d.polygon(P((x, 0.17), (x + 0.02, 0.07), (x + 0.03, 0.18), W=W, H=H), fill=LINE)
            d.polygon(P((x, 0.83), (x + 0.02, 0.93), (x + 0.03, 0.82), W=W, H=H), fill=LINE)
        d.line(P((0.2, 0.5), (0.72, 0.5), W=W, H=H), fill=LIGHT, width=lw)
    elif kind == "shark":
        for i in range(3):
            x = 0.24 + i * 0.03
            d.line(P((x, 0.38), (x - 0.01, 0.62), W=W, H=H), fill=LINE, width=lw)
        d.polygon(P((0.03, 0.6), (0.16, 0.56), (0.16, 0.66), W=W, H=H), fill=LIGHT, outline=LINE, width=lw)
    elif kind == "puffer":
        for i in range(6):
            x, y = 0.3 + (i % 3) * 0.14, 0.28 + (i // 3) * 0.2
            d.ellipse([x * W, y * H, x * W + 0.05 * W, y * H + 0.05 * H], fill=SHADE)

    d.ellipse(body, outline=LINE, width=lw)
    # eye and mouth
    ex, ey, er = 0.13, 0.42, 0.13 if kind != "puffer" else 0.16
    d.ellipse([ex * W - er * H / 2, ey * H - er * H / 2, ex * W + er * H / 2, ey * H + er * H / 2], fill=LIGHT, outline=LINE, width=lw)
    pr = er * 0.55
    d.ellipse([ex * W - pr * H / 2, ey * H - pr * H / 2, ex * W + pr * H / 2, ey * H + pr * H / 2], fill=INK)
    d.line(P((0.03, 0.6), (0.09, 0.62), W=W, H=H), fill=LINE, width=lw)
    save(im, name, w * 2, h * 2)


# ---------- tile icons (128x128, full colour) ----------

def icon(name, draw):
    im, d = canvas(128, 128)
    draw(d, 128 * S)
    save(im, "tile_" + name, 128, 128)


def worm(d, N):
    pts = [(N * (0.18 + 0.64 * t), N * (0.5 + 0.17 * math.sin(t * math.pi * 3))) for t in [i / 30 for i in range(31)]]
    d.line(pts, fill=(205, 92, 120, 255), width=int(N * 0.16), joint="curve")
    d.line(pts, fill=(240, 140, 160, 255), width=int(N * 0.1), joint="curve")
    for t in (0.35, 0.55, 0.75):
        x, y = N * (0.18 + 0.64 * t), N * (0.5 + 0.17 * math.sin(t * math.pi * 3))
        d.line([(x - N * 0.03, y - N * 0.05), (x + N * 0.03, y + N * 0.05)], fill=(190, 80, 110, 255), width=int(N * 0.02))
    d.ellipse([N * 0.12, N * 0.43, N * 0.24, N * 0.55], fill=(240, 140, 160, 255))
    d.ellipse([N * 0.15, N * 0.46, N * 0.18, N * 0.49], fill=INK)


def shrimp(d, N):
    c, dk = (255, 140, 90, 255), (210, 90, 50, 255)
    for i in range(6):
        a = math.radians(200 + i * 26)
        x, y, r = N * (0.5 + 0.26 * math.cos(a)), N * (0.52 + 0.26 * math.sin(a)), N * (0.13 - i * 0.012)
        d.ellipse([x - r, y - r, x + r, y + r], fill=c, outline=dk, width=int(N * 0.015))
    d.polygon([(N * 0.72, N * 0.38), (N * 0.9, N * 0.3), (N * 0.86, N * 0.5)], fill=dk)
    d.ellipse([N * 0.22, N * 0.42, N * 0.28, N * 0.48], fill=INK)
    d.line([(N * 0.2, N * 0.4), (N * 0.1, N * 0.16)], fill=dk, width=int(N * 0.015))
    d.line([(N * 0.24, N * 0.38), (N * 0.2, N * 0.12)], fill=dk, width=int(N * 0.015))


def corn(d, N):
    d.polygon([(N * 0.3, N * 0.78), (N * 0.15, N * 0.4), (N * 0.42, N * 0.7)], fill=(120, 170, 70, 255))
    d.polygon([(N * 0.7, N * 0.78), (N * 0.85, N * 0.4), (N * 0.58, N * 0.7)], fill=(120, 170, 70, 255))
    d.ellipse([N * 0.32, N * 0.12, N * 0.68, N * 0.82], fill=(250, 200, 60, 255), outline=(200, 150, 30, 255), width=int(N * 0.02))
    for r in range(6):
        for c in range(3):
            x, y = N * (0.38 + c * 0.09), N * (0.2 + r * 0.095)
            d.ellipse([x, y, x + N * 0.07, y + N * 0.07], fill=(255, 225, 110, 255))
    d.polygon([(N * 0.5, N * 0.95), (N * 0.38, N * 0.72), (N * 0.62, N * 0.72)], fill=(100, 150, 60, 255))


def bread(d, N):
    d.rounded_rectangle([N * 0.16, N * 0.3, N * 0.84, N * 0.86], radius=N * 0.08, fill=(200, 140, 70, 255))
    d.ellipse([N * 0.12, N * 0.12, N * 0.52, N * 0.5], fill=(200, 140, 70, 255))
    d.ellipse([N * 0.48, N * 0.12, N * 0.88, N * 0.5], fill=(200, 140, 70, 255))
    d.rounded_rectangle([N * 0.22, N * 0.34, N * 0.78, N * 0.8], radius=N * 0.06, fill=(250, 230, 180, 255))
    d.ellipse([N * 0.18, N * 0.18, N * 0.5, N * 0.48], fill=(250, 230, 180, 255))
    d.ellipse([N * 0.5, N * 0.18, N * 0.82, N * 0.48], fill=(250, 230, 180, 255))
    for x, y in ((0.35, 0.5), (0.6, 0.62), (0.45, 0.7), (0.62, 0.38)):
        d.ellipse([N * x, N * y, N * (x + 0.04), N * (y + 0.04)], fill=(220, 190, 130, 255))


def minnow(d, N):
    c, dk = (90, 160, 220, 255), (40, 100, 160, 255)
    d.polygon([(N * 0.68, N * 0.5), (N * 0.9, N * 0.3), (N * 0.86, N * 0.5), (N * 0.9, N * 0.7)], fill=dk)
    d.ellipse([N * 0.1, N * 0.32, N * 0.74, N * 0.68], fill=c, outline=dk, width=int(N * 0.02))
    d.ellipse([N * 0.14, N * 0.5, N * 0.68, N * 0.66], fill=(200, 230, 250, 255))
    d.ellipse([N * 0.2, N * 0.4, N * 0.28, N * 0.48], fill=(255, 255, 255, 255))
    d.ellipse([N * 0.22, N * 0.42, N * 0.26, N * 0.46], fill=INK)


def lure(d, N):
    d.line([(N * 0.5, N * 0.08), (N * 0.5, N * 0.2)], fill=(120, 120, 130, 255), width=int(N * 0.025))
    d.ellipse([N * 0.45, N * 0.06, N * 0.55, N * 0.14], outline=(120, 120, 130, 255), width=int(N * 0.02))
    d.ellipse([N * 0.32, N * 0.18, N * 0.68, N * 0.7], fill=(230, 90, 200, 255), outline=(150, 40, 130, 255), width=int(N * 0.02))
    d.ellipse([N * 0.38, N * 0.24, N * 0.5, N * 0.44], fill=(255, 200, 245, 255))
    d.arc([N * 0.38, N * 0.6, N * 0.62, N * 0.92], 0, 200, fill=(120, 120, 130, 255), width=int(N * 0.03))
    d.polygon([(N * 0.38, N * 0.76), (N * 0.33, N * 0.68), (N * 0.42, N * 0.7)], fill=(120, 120, 130, 255))


def reel(d, N):
    d.ellipse([N * 0.14, N * 0.14, N * 0.86, N * 0.86], fill=(120, 90, 200, 255), outline=(70, 50, 140, 255), width=int(N * 0.025))
    d.ellipse([N * 0.26, N * 0.26, N * 0.74, N * 0.74], fill=(235, 235, 245, 255))
    for r in (0.2, 0.15, 0.1):
        d.ellipse([N * (0.5 - r), N * (0.5 - r), N * (0.5 + r), N * (0.5 + r)], outline=(180, 180, 200, 255), width=int(N * 0.015))
    d.ellipse([N * 0.44, N * 0.44, N * 0.56, N * 0.56], fill=(70, 50, 140, 255))
    d.line([(N * 0.5, N * 0.5), (N * 0.86, N * 0.2)], fill=(70, 50, 140, 255), width=int(N * 0.04))
    d.ellipse([N * 0.8, N * 0.12, N * 0.92, N * 0.24], fill=(70, 50, 140, 255))




def weed(d, N):
    for x, sway, c in ((0.3, -0.08, (60, 120, 70, 255)), (0.5, 0.1, (80, 150, 80, 255)), (0.7, -0.05, (60, 120, 70, 255))):
        pts = [(N * (x + sway * math.sin(t * math.pi * 2)), N * (0.92 - 0.8 * t)) for t in [i / 20 for i in range(21)]]
        d.line(pts, fill=c, width=int(N * 0.08), joint="curve")
        for t in (0.3, 0.55, 0.8):
            px, py = N * (x + sway * math.sin(t * math.pi * 2)), N * (0.92 - 0.8 * t)
            d.ellipse([px, py - N * 0.04, px + N * 0.12, py + N * 0.02], fill=c)


# ---------- fight action tiles: what the player does to the line ----------

ROD, ROD_DK = (150, 95, 50, 255), (95, 60, 30, 255)
GREEN, RED = (46, 139, 87, 255), (220, 70, 50, 255)


def arrow(d, N, x, y, up, c):
    s = -1 if up else 1
    d.line([(N * x, N * (y - 0.12 * s)), (N * x, N * (y + 0.12 * s))], fill=c, width=int(N * 0.05))
    d.polygon([(N * (x - 0.09), N * (y + 0.08 * s)), (N * (x + 0.09), N * (y + 0.08 * s)), (N * x, N * (y + 0.2 * s))], fill=c)


def yank(d, N):  # giật cần: rod bent hard upward, big up arrow
    d.arc([N * 0.1, N * 0.1, N * 1.1, N * 1.3], 180, 265, fill=ROD, width=int(N * 0.06))
    d.rounded_rectangle([N * 0.06, N * 0.7, N * 0.2, N * 0.92], radius=N * 0.03, fill=ROD_DK)
    d.line([(N * 0.56, N * 0.12), (N * 0.62, N * 0.6)], fill=(90, 90, 90, 255), width=int(N * 0.015))
    arrow(d, N, 0.8, 0.45, True, RED)


def slack(d, N):  # nới dây: loose wavy line, down arrow
    pts = [(N * (0.12 + 0.5 * t), N * (0.2 + 0.6 * t + 0.08 * math.sin(t * math.pi * 4))) for t in [i / 30 for i in range(31)]]
    d.line(pts, fill=(80, 80, 90, 255), width=int(N * 0.03), joint="curve")
    d.rounded_rectangle([N * 0.06, N * 0.1, N * 0.24, N * 0.24], radius=N * 0.04, fill=ROD_DK)
    arrow(d, N, 0.8, 0.5, False, GREEN)


def brace(d, N):  # ghìm cần: rod held steady against a shield
    d.line([(N * 0.15, N * 0.85), (N * 0.85, N * 0.15)], fill=ROD, width=int(N * 0.06))
    d.rounded_rectangle([N * 0.08, N * 0.74, N * 0.3, N * 0.94], radius=N * 0.04, fill=ROD_DK)
    d.polygon([(N * 0.5, N * 0.38), (N * 0.78, N * 0.48), (N * 0.74, N * 0.78), (N * 0.5, N * 0.92), (N * 0.26, N * 0.78), (N * 0.22, N * 0.48)],
              fill=(70, 130, 200, 255), outline=(40, 80, 140, 255))
    d.line([(N * 0.38, N * 0.64), (N * 0.48, N * 0.74), (N * 0.64, N * 0.54)], fill=(255, 255, 255, 255), width=int(N * 0.05))




def net(d, N):  # vợt: landing net
    d.line([(N * 0.12, N * 0.9), (N * 0.42, N * 0.5)], fill=ROD, width=int(N * 0.06))
    d.ellipse([N * 0.36, N * 0.12, N * 0.9, N * 0.56], outline=(90, 90, 100, 255), width=int(N * 0.04))
    for i in range(1, 5):
        x = 0.36 + i * 0.108
        d.line([(N * x, N * 0.16), (N * (0.63 + (x - 0.63) * 0.5), N * 0.82)], fill=(150, 150, 160, 255), width=int(N * 0.012))
    d.line([(N * 0.4, N * 0.42), (N * 0.86, N * 0.42)], fill=(150, 150, 160, 255), width=int(N * 0.012))
    d.line([(N * 0.45, N * 0.56), (N * 0.63, N * 0.84), (N * 0.82, N * 0.56)], fill=(150, 150, 160, 255), width=int(N * 0.015))


# ---------- scene pieces ----------

def boat():
    im, d = canvas(260, 110)
    N = S
    d.polygon([(10 * N, 60 * N), (250 * N, 60 * N), (220 * N, 105 * N), (40 * N, 105 * N)], fill=(140, 90, 55, 255), outline=(80, 50, 30, 255), width=3 * N)
    d.rectangle([20 * N, 60 * N, 240 * N, 70 * N], fill=(225, 80, 60, 255))
    for x in range(40, 230, 30):
        d.line([(x * N, 74 * N), (x * N + 20 * N, 74 * N)], fill=(110, 70, 40, 255), width=2 * N)
    # fisher
    d.rounded_rectangle([90 * N, 22 * N, 128 * N, 62 * N], radius=8 * N, fill=(60, 110, 160, 255))
    d.ellipse([94 * N, 0, 124 * N, 30 * N], fill=(250, 210, 170, 255))
    d.pieslice([90 * N, -6 * N, 128 * N, 22 * N], 180, 360, fill=(240, 190, 40, 255))
    save(im, "boat", 260, 110)


def hook():
    im, d = canvas(40, 56)
    N = S
    c = (230, 235, 240, 255)
    d.ellipse([16 * N, 0, 24 * N, 8 * N], outline=c, width=2 * N)
    d.line([(20 * N, 8 * N), (20 * N, 36 * N)], fill=c, width=3 * N)
    d.arc([6 * N, 24 * N, 26 * N, 50 * N], 0, 180, fill=c, width=3 * N)
    d.polygon([(6 * N, 37 * N), (2 * N, 28 * N), (10 * N, 32 * N)], fill=c)
    # bait wriggling on the hook
    d.line([(20 * N, 40 * N), (28 * N, 46 * N), (34 * N, 42 * N)], fill=(240, 140, 160, 255), width=4 * N, joint="curve")
    save(im, "hook", 40, 56)


def branch():
    im, d = canvas(260, 40)
    N = S
    d.rounded_rectangle([0, 12 * N, 260 * N, 30 * N], radius=9 * N, fill=(115, 85, 55, 255), outline=(70, 50, 30, 255), width=2 * N)
    for x in range(20, 250, 40):
        d.line([(x * N, 17 * N), (x * N + 18 * N, 17 * N)], fill=(90, 65, 40, 255), width=2 * N)
    d.line([(70 * N, 14 * N), (90 * N, 0)], fill=(115, 85, 55, 255), width=5 * N)
    d.line([(180 * N, 28 * N), (200 * N, 40 * N)], fill=(115, 85, 55, 255), width=5 * N)
    d.ellipse([86 * N, 0, 100 * N, 8 * N], fill=(90, 150, 70, 255))
    save(im, "branch", 260, 40)


def seabed():
    im, d = canvas(540, 160)
    N = S
    for x, h, c in ((40, 140, (40, 110, 80, 255)), (90, 100, (55, 130, 85, 255)), (400, 150, (40, 110, 80, 255)), (460, 90, (55, 130, 85, 255)), (250, 70, (55, 130, 85, 255))):
        pts = [((x + 14 * math.sin(t * 6)) * N, (160 - h * t) * N) for t in [i / 20 for i in range(21)]]
        d.line(pts, fill=c, width=12 * N, joint="curve")
    for x, r in ((150, 30), (200, 20), (330, 36), (520, 24)):
        d.ellipse([(x - r) * N, (160 - r) * N, (x + r) * N, (160 + r) * N], fill=(120, 130, 120, 255))
    d.rectangle([0, 150 * N, 540 * N, 160 * N], fill=(170, 150, 110, 255))
    save(im, "seabed", 540, 160)


# ---------- backgrounds and UI ----------

def vgrad(d, box, top, bottom):
    x0, y0, x1, y1 = box
    for y in range(int(y0), int(y1)):
        t = (y - y0) / max(1, y1 - y0)
        d.line([(x0, y), (x1, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))


def sky():
    im, d = canvas(540, 80)
    N = S
    vgrad(d, (0, 0, 540 * N, 80 * N), (150, 205, 230, 255), (220, 238, 242, 255))
    d.ellipse([440 * N, 8 * N, 480 * N, 48 * N], fill=(255, 220, 120, 255))
    for x, y, r in ((60, 30, 14), (78, 24, 18), (98, 32, 13), (300, 20, 12), (316, 14, 16), (334, 22, 12)):
        d.ellipse([(x - r) * N, (y - r) * N, (x + r) * N, (y + r) * N], fill=(255, 255, 255, 230))
    # far shore
    d.polygon([(0, 80 * N), (0, 62 * N), (90 * N, 50 * N), (200 * N, 64 * N), (260 * N, 56 * N), (380 * N, 66 * N), (540 * N, 54 * N), (540 * N, 80 * N)], fill=(110, 160, 120, 255))
    save(im, "sky", 1080, 160)


def wave():
    im, d = canvas(540, 20)
    N = S
    pts = [(x * N, (8 + 4 * math.sin(x / 540 * math.pi * 12)) * N) for x in range(0, 541, 4)]
    d.polygon(pts + [(540 * N, 20 * N), (0, 20 * N)], fill=(29, 106, 128, 255))
    d.line(pts, fill=(200, 235, 240, 255), width=2 * N)
    save(im, "wave", 1080, 40)


def panel_water():
    im, d = canvas(500, 70)
    N = S
    d.rounded_rectangle([0, 0, 500 * N, 70 * N], radius=14 * N, fill=(29, 106, 128, 255))
    vgrad(d, (6 * N, 40 * N, 494 * N, 64 * N), (29, 106, 128, 255), (20, 80, 100, 255))
    for x in (40, 90, 420, 460):
        d.line([((x + 6 * math.sin(t * 6)) * N, (70 - 30 * t) * N) for t in [i / 10 for i in range(11)]], fill=(55, 130, 85, 255), width=4 * N)
    for x, y, r in ((70, 20, 3), (440, 30, 4), (260, 12, 2)):
        d.ellipse([(x - r) * N, (y - r) * N, (x + r) * N, (y + r) * N], outline=(200, 235, 240, 255), width=N)
    save(im, "panel_water", 1000, 140)


def wood(name, w, h, plank):
    im, d = canvas(w, h)
    N = S
    d.rounded_rectangle([0, 0, w * N, h * N], radius=12 * N, fill=(160, 115, 70, 255), outline=(110, 75, 45, 255), width=3 * N)
    for y in range(plank, h - 4, plank):
        d.line([(6 * N, y * N), ((w - 6) * N, y * N)], fill=(130, 90, 55, 255), width=2 * N)
    for x, y in ((40, plank // 2), (w - 50, plank + plank // 2), (w // 2, plank // 3)):
        if y < h:
            d.ellipse([x * N, y * N, (x + 10) * N, (y + 5) * N], fill=(130, 90, 55, 255))
    save(im, name, w * 2, h * 2)


def button():
    # white rounded rectangle with a darker bottom lip, tinted per button and drawn 9-sliced in Unity
    im, d = canvas(48, 48)
    N = S
    # the body is a little grey so the tint shows brighter on the shine; shine and lip stay inside the 24 px caps
    d.rounded_rectangle([0, 3 * N, 48 * N, 48 * N], radius=12 * N, fill=(150, 150, 150, 255))
    d.rounded_rectangle([0, 0, 48 * N, 44 * N], radius=12 * N, fill=(232, 232, 232, 255))
    d.rounded_rectangle([4 * N, 2 * N, 44 * N, 10 * N], radius=4 * N, fill=(255, 255, 255, 255))
    save(im, "button", 96, 96)


def hand():
    # a white glove pointing up; the fingertip sits at the top centre (the tutorial places that point)
    im, d = canvas(128, 128)
    N, ink = S, (40, 52, 60, 255)
    w = 5 * N
    d.ellipse([18 * N, 66 * N, 52 * N, 92 * N], fill=(255, 255, 255, 255), outline=ink, width=w)  # thumb
    d.rounded_rectangle([34 * N, 58 * N, 108 * N, 124 * N], radius=24 * N, fill=(255, 255, 255, 255), outline=ink, width=w)
    d.rounded_rectangle([50 * N, 4 * N, 78 * N, 80 * N], radius=14 * N, fill=(255, 255, 255, 255), outline=ink, width=w)
    d.rectangle([55 * N, 60 * N, 73 * N, 84 * N], fill=(255, 255, 255, 255))  # finger joins the palm cleanly
    for x in (74, 92):
        d.line([(x * N, 62 * N), (x * N, 84 * N)], fill=ink, width=4 * N)
    save(im, "hand", 128, 128)


def sound_icons():
    # speaker with sound waves (on) or a cross (off), drawn dark for light buttons
    for name, on in (("icon_sound_on", True), ("icon_sound_off", False)):
        im, d = canvas(64, 64)
        N, c = S, INK
        d.polygon([(8 * N, 24 * N), (20 * N, 24 * N), (34 * N, 10 * N), (34 * N, 54 * N), (20 * N, 40 * N), (8 * N, 40 * N)], fill=c)
        if on:
            for r in (10, 18):
                d.arc([(34 - r) * N, (32 - r) * N, (40 + r) * N, (32 + r) * N], -50, 50, fill=c, width=4 * N)
        else:
            d.line([(42 * N, 22 * N), (58 * N, 42 * N)], fill=c, width=5 * N)
            d.line([(58 * N, 22 * N), (42 * N, 42 * N)], fill=c, width=5 * N)
        save(im, name, 64, 64)


# ---------- reel fight: the rings, the reel body and the spool (sizes match ReelPhase) ----------

REEL_RINGS = ((330, 440), (220, 325), (115, 215))  # (inner, outer) radius of each ring, outside in


def reel_art():
    for i, (r0, r1) in enumerate(REEL_RINGS):
        # a white band (tinted per ring in Unity) with a soft inner shadow and 8 notches marking the steps
        size = r1 * 2
        im, d = canvas(size, size)
        N, c = S, size / 2
        d.ellipse([(c - r1) * N, (c - r1) * N, (c + r1) * N, (c + r1) * N], fill=(255, 255, 255, 255))
        d.ellipse([(c - r1 + 6) * N, (c - r1 + 6) * N, (c + r1 - 6) * N, (c + r1 - 6) * N], outline=(225, 225, 225, 255), width=4 * N)
        d.ellipse([(c - r0) * N, (c - r0) * N, (c + r0) * N, (c + r0) * N], fill=(0, 0, 0, 0))
        d.ellipse([(c - r0 - 4) * N, (c - r0 - 4) * N, (c + r0 + 4) * N, (c + r0 + 4) * N], outline=(190, 190, 190, 255), width=5 * N)
        for k in range(8):
            a = math.pi * 2 * k / 8
            x, y = c + math.sin(a) * (r1 - 16), c - math.cos(a) * (r1 - 16)
            d.ellipse([(x - 7) * N, (y - 7) * N, (x + 7) * N, (y + 7) * N], fill=(170, 170, 170, 255))
        save(im, f"reel_ring_{i}", size, size)
    # the dark reel body behind the rings
    size = 940
    im, d = canvas(size, size)
    N = S
    d.ellipse([0, 0, size * N, size * N], fill=(140, 205, 230, 255))
    d.ellipse([8 * N, 8 * N, (size - 8) * N, (size - 8) * N], fill=(22, 56, 71, 255))
    save(im, "reel_body", size, size)
    # the spool: a white disc (tinted grey or orange), and its crank knob
    size = 220
    im, d = canvas(size, size)
    d.ellipse([0, 0, size * N, size * N], fill=(255, 255, 255, 255))
    d.ellipse([14 * N, 14 * N, (size - 14) * N, (size - 14) * N], outline=(215, 215, 215, 255), width=6 * N)
    d.ellipse([90 * N, 90 * N, 130 * N, 130 * N], fill=(16, 38, 46, 255))
    save(im, "reel_spool", size, size)


def bg_soft():
    # a pale sea-glass gradient behind the HUD, the cast strip and the fight
    im, d = canvas(8, 480)
    vgrad(d, (0, 0, 8 * S, 480 * S), (236, 247, 250, 255), (196, 224, 233, 255))
    save(im, "bg_soft", 8, 480)


# ---------- fight screen: glossy tiles, a checkered board, tube bars ----------

def tile_gloss():
    # white-to-grey so Unity's tint (tile colour) shows full on the shine and a little darker on the body
    im, d = canvas(128, 128)
    N = S
    d.rounded_rectangle([2 * N, 8 * N, 126 * N, 126 * N], radius=28 * N, fill=(120, 120, 120, 255))  # bottom lip
    body = Image.new("RGBA", im.size)
    vgrad(ImageDraw.Draw(body), (0, 0, 128 * N, 128 * N), (235, 235, 235, 255), (185, 185, 185, 255))
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([2 * N, 2 * N, 126 * N, 118 * N], radius=28 * N, fill=255)
    im.paste(body, (0, 0), mask)
    d = ImageDraw.Draw(im)
    d.rounded_rectangle([12 * N, 8 * N, 116 * N, 52 * N], radius=20 * N, fill=(255, 255, 255, 255))  # shine
    d.rounded_rectangle([2 * N, 2 * N, 126 * N, 118 * N], radius=28 * N, outline=(255, 255, 255, 255), width=3 * N)
    save(im, "tile_gloss", 128, 128)


def board_bg():
    # drawn at the exact size the fight screen gives it: 7x7 cells of 740/7 px with a 24/20 px rim
    w, h, ox, oy, n = 788, 780, 24, 20, 7
    u = 740 / n
    im, d = canvas(w, h)
    N = S
    d.rounded_rectangle([0, 0, w * N, h * N], radius=34 * N, fill=(22, 58, 92, 255), outline=(140, 205, 230, 255), width=6 * N)
    for x in range(n):
        for y in range(n):
            c = (44, 92, 134, 255) if (x + y) % 2 == 0 else (35, 78, 118, 255)
            x0, y0 = (ox + x * u + 3) * N, (oy + y * u + 3) * N
            d.rounded_rectangle([x0, y0, x0 + (u - 6) * N, y0 + (u - 6) * N], radius=12 * N, fill=c)
    save(im, "board_bg", w, h)


def bars():
    # 52 px wide tubes, 9-sliced vertically in Unity (24 px caps)
    w, h = 52, 120
    im, d = canvas(w, h)
    N = S
    d.rounded_rectangle([0, 0, w * N, h * N], radius=24 * N, fill=(28, 52, 66, 255))
    d.rounded_rectangle([5 * N, 5 * N, (w - 5) * N, (h - 5) * N], radius=20 * N, fill=(46, 76, 92, 255))
    save(im, "bar_bg", w, h)
    im, d = canvas(w, h)
    for x in range(w * N):  # brighter left of centre, darker edges: a round tube
        t = x / (w * N)
        v = int(185 + 70 * max(0.0, 1 - abs(t - 0.38) / 0.5))
        d.line([(x, 0), (x, h * N)], fill=(v, v, v, 255))
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([5 * N, 5 * N, (w - 5) * N, (h - 5) * N], radius=20 * N, fill=255)
    out = Image.new("RGBA", im.size)
    out.paste(im, (0, 0), mask)
    ImageDraw.Draw(out).line([(15 * N, 26 * N), (15 * N, (h - 26) * N)], fill=(255, 255, 255, 255), width=5 * N)
    save(out, "bar_fill", w, h)


def star():
    im, d = canvas(64, 64)
    N = S
    pts = []
    for i in range(10):
        a = math.radians(-90 + i * 36)
        r = 30 if i % 2 == 0 else 13
        pts.append(((32 + r * math.cos(a)) * N, (34 + r * math.sin(a)) * N))
    d.polygon(pts, fill=(255, 196, 40, 255), outline=(170, 110, 20, 255), width=3 * N)
    save(im, "star", 64, 64)


def coin():
    im, d = canvas(64, 64)
    N = S
    d.ellipse([2 * N, 2 * N, 62 * N, 62 * N], fill=(230, 160, 30, 255))
    d.ellipse([7 * N, 7 * N, 57 * N, 57 * N], fill=(255, 205, 60, 255))
    d.rectangle([28 * N, 18 * N, 36 * N, 46 * N], fill=(230, 160, 30, 255))
    d.ellipse([14 * N, 12 * N, 24 * N, 22 * N], fill=(255, 240, 170, 255))
    save(im, "coin", 64, 64)


def booster_icons():
    c, N = INK, S
    im, d = canvas(64, 64)  # undo: curved arrow
    d.arc([12 * N, 14 * N, 54 * N, 56 * N], 180, 450, fill=c, width=6 * N)
    d.polygon([(4 * N, 34 * N), (20 * N, 34 * N), (12 * N, 22 * N)], fill=c)
    save(im, "icon_undo", 64, 64)
    im, d = canvas(64, 64)  # shuffle: two crossing arrows
    d.line([(6 * N, 16 * N), (24 * N, 16 * N), (40 * N, 48 * N), (52 * N, 48 * N)], fill=c, width=5 * N, joint="curve")
    d.line([(6 * N, 48 * N), (24 * N, 48 * N), (40 * N, 16 * N), (52 * N, 16 * N)], fill=c, width=5 * N, joint="curve")
    d.polygon([(50 * N, 8 * N), (60 * N, 16 * N), (50 * N, 24 * N)], fill=c)
    d.polygon([(50 * N, 40 * N), (60 * N, 48 * N), (50 * N, 56 * N)], fill=c)
    save(im, "icon_shuffle", 64, 64)
    im, d = canvas(64, 64)  # pull back: three tiles with an up arrow
    for i in range(3):
        d.rounded_rectangle([(4 + i * 20) * N, 40 * N, (20 + i * 20) * N, 58 * N], radius=3 * N, outline=c, width=4 * N)
    d.line([(32 * N, 34 * N), (32 * N, 8 * N)], fill=c, width=5 * N)
    d.polygon([(22 * N, 14 * N), (42 * N, 14 * N), (32 * N, 2 * N)], fill=c)
    save(im, "icon_pull", 64, 64)


if __name__ == "__main__":
    sky()
    wave()
    panel_water()
    board_bg()
    tile_gloss()
    bars()
    wood("tray_bg", 506, 80, 80)
    button()
    bg_soft()
    hand()
    sound_icons()
    reel_art()
    coin()
    star()
    booster_icons()
    fish("fish_chep", 150, 66, "carp")
    fish("fish_noc", 88, 78, "puffer")
    fish("fish_rophi", 160, 70, "tilapia")
    fish("fish_cathu", 180, 60, "mackerel")
    fish("fish_ngu", 230, 88, "tuna")
    fish("fish_muc", 140, 90, "squid")
    fish("fish_camap", 300, 110, "shark")
    for n, f in (("giun", worm), ("tom", shrimp), ("ngo", corn), ("banhmi", bread), ("cacon", minnow),
                 ("moigia", lure), ("cuon", reel), ("rong", weed),
                 ("giat", yank), ("thu", reel), ("noi", slack), ("ghim", brace), ("vot", net)):
        icon(n, f)
    boat()
    hook()
    branch()
    seabed()
    print("done:", sorted(os.listdir(OUT)))
