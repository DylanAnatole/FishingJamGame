# Synthesises the prototype's sound effects into Assets/Resources/Audio (plain Python, no packages needed):
#   python ArtSource/make_sfx.py
# Short, soft, casual-game sounds; all ours, so no licensing to worry about.
import math
import os
import random
import struct
import wave

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Assets", "Resources", "Audio")
RATE = 44100


def save(name, samples):
    os.makedirs(OUT, exist_ok=True)
    peak = max(1e-6, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name + ".wav"), "w") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * 0.8 * 32767)) for s in samples))


def tone(f0, f1, dur, shape="sine", attack=0.005, decay=6.0):
    # a pitch glide from f0 to f1 with a quick attack and an exponential fade
    out, phase = [], 0.0
    n = int(RATE * dur)
    for i in range(n):
        t = i / RATE
        f = f0 + (f1 - f0) * i / n
        phase += 2 * math.pi * f / RATE
        if shape == "sine":
            v = math.sin(phase)
        elif shape == "tri":
            v = 2 / math.pi * math.asin(math.sin(phase))
        else:  # soft square
            v = math.tanh(3 * math.sin(phase))
        env = min(1, t / attack) * math.exp(-decay * t / dur)
        out.append(v * env)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    return [sum(p[i] for p in parts if i < len(p)) for i in range(n)]


def seq(notes, step, dur, shape="tri"):
    # notes played one after another, each ringing on
    out = []
    for k, f in enumerate(notes):
        start = int(RATE * step * k)
        t = tone(f, f, dur, shape, decay=5)
        out += [0.0] * (start + len(t) - len(out))
        for i, v in enumerate(t):
            out[start + i] += v
    return out


def noise(dur, decay=8.0, smooth=0.85):
    out, last = [], 0.0
    for i in range(int(RATE * dur)):
        last = smooth * last + (1 - smooth) * random.uniform(-1, 1)  # low-passed: a soft whoosh, not hiss
        out.append(last * math.exp(-decay * i / (RATE * dur)))
    return out


if __name__ == "__main__":
    random.seed(7)
    C5, E5, G5, C6, E6, G6 = 523.3, 659.3, 784.0, 1046.5, 1318.5, 1568.0
    save("tap", tone(900, 700, 0.05, "sine", decay=8))
    save("swap", mix(noise(0.12, 6, 0.9), tone(300, 520, 0.12, "sine", decay=5)))
    save("bad", tone(220, 160, 0.16, "square", decay=4))
    save("pop", mix(tone(500, 980, 0.11, "sine", decay=5), tone(1000, 1900, 0.06, "sine", decay=7)))
    save("special", seq([E6, G6, C6 * 2], 0.05, 0.18, "sine"))
    save("splash", mix(noise(0.35, 5, 0.75), tone(420, 180, 0.2, "sine", decay=6)))
    save("bite", mix(tone(260, 520, 0.09, "tri", decay=4), tone(700, 700, 0.06, "sine", decay=6)))
    save("catch", seq([C5, E5, G5, C6], 0.07, 0.3))
    save("win", seq([C5, E5, G5, C6, E6, G6, C6 * 2], 0.09, 0.45))
    save("snap", mix(tone(500, 110, 0.32, "square", decay=3), noise(0.1, 10, 0.5)))
    save("lose", seq([G5, E5, C5, C5 / 2], 0.14, 0.4, "sine"))
    print("done:", sorted(os.listdir(OUT)))
