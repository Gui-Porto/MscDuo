import math
import os
import random

from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "galpao_tex_jogo")
rng = random.Random(21)


def noise(img, amount):
    px = img.load()
    for y in range(img.size[1]):
        for x in range(img.size[0]):
            r, g, b = px[x, y]
            n = rng.randint(-amount, amount)
            px[x, y] = (max(0, min(255, r + n)), max(0, min(255, g + n)), max(0, min(255, b + n)))
    return img


def lambri():
    size = 512
    img = Image.new("RGB", (size, size))
    d = ImageDraw.Draw(img)
    planks = 5
    w = size // planks
    for i in range(planks):
        base = (118 + rng.randint(-10, 10), 82 + rng.randint(-8, 8), 50 + rng.randint(-6, 6))
        d.rectangle([i * w, 0, (i + 1) * w, size], fill=base)
        for k in range(40):
            x = rng.randint(i * w + 4, (i + 1) * w - 4)
            shade = rng.randint(-18, 8)
            col = tuple(max(0, c + shade) for c in base)
            amp = rng.uniform(0.5, 2.5)
            period = rng.uniform(20, 60)
            pts = [(x + amp * math.sin(t / period), t) for t in range(0, size, 4)]
            d.line(pts, fill=col, width=1)
        d.line([(i * w, 0), (i * w, size)], fill=(60, 40, 24), width=4)
    img = noise(img, 6).filter(ImageFilter.GaussianBlur(0.6))
    img.save(os.path.join(OUT, "lambri.png"))


def rug(name, field, border, accent, pattern):
    size = 512
    img = Image.new("RGB", (size, size), field)
    d = ImageDraw.Draw(img)
    d.rectangle([14, 14, size - 15, size - 15], outline=border, width=26)
    d.rectangle([52, 52, size - 53, size - 53], outline=accent, width=6)
    if pattern == "medalhao":
        c = size // 2
        for r, col in ((150, border), (118, accent), (84, field), (56, border), (26, accent)):
            d.polygon([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], fill=col)
        for x in range(80, size - 80, 44):
            d.rectangle([x, 70, x + 14, 84], fill=accent)
            d.rectangle([x, size - 85, x + 14, size - 71], fill=accent)
    elif pattern == "listras":
        for i, x in enumerate(range(70, size - 70, 36)):
            d.rectangle([x, 70, x + 18, size - 71], fill=accent if i % 2 == 0 else border)
    else:
        step = 46
        for i, x in enumerate(range(72, size - 72 - step + 1, step)):
            for j, y in enumerate(range(72, size - 72 - step + 1, step)):
                if (i + j) % 2 == 0:
                    d.rectangle([x, y, x + step - 1, y + step - 1], fill=accent)
    for y in range(0, size, 3):
        d.line([(0, y), (size, y)], fill=tuple(max(0, c - 6) for c in field), width=1)
    img = noise(img, 10).filter(ImageFilter.GaussianBlur(0.8))
    img.save(os.path.join(OUT, name + ".png"))


def tiles(name, n, color, grout, jitter):
    size = 512
    img = Image.new("RGB", (size, size), grout)
    d = ImageDraw.Draw(img)
    step = size / n
    for i in range(n):
        for j in range(n):
            c = tuple(max(0, min(255, v + rng.randint(-jitter, jitter))) for v in color)
            d.rectangle([int(i * step) + 3, int(j * step) + 3, int((i + 1) * step) - 3, int((j + 1) * step) - 3], fill=c)
    img = noise(img, 4).filter(ImageFilter.GaussianBlur(0.5))
    img.save(os.path.join(OUT, name + ".png"))


if __name__ == "__main__":
    tiles("azulejo", 6, (236, 238, 236), (175, 178, 176), 4)
    tiles("piso_banheiro", 3, (120, 124, 130), (70, 72, 76), 8)
    lambri()
    rug("tapete_sala", (128, 36, 30), (60, 26, 22), (200, 150, 80), "medalhao")
    rug("tapete_jogos", (34, 40, 66), (18, 20, 34), (90, 110, 170), "listras")
    rug("tapete_escritorio", (70, 84, 62), (40, 48, 36), (120, 132, 100), "xadrez")
    rug("tapete_lounge", (150, 118, 82), (82, 58, 38), (205, 180, 140), "listras")
    print("ok")
