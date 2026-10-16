import os
import sys

import numpy as np
from PIL import Image, ImageFilter

DUMPS = os.path.join(os.path.expanduser("~"), "Documents", "MySummerCar", "Mods", "Config", "Mod Settings", "MscDuo", "dumps")
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "extensao.png")

SIZE = 1024
U_MIN, U_MAX = 0.40, 0.90
V_MIN, V_MAX = -0.36, 0.20
APRON = (0.5137, 0.8271, 0.0143, 0.3970)
EDGE_V = 0.0143
JOINT_U0, JOINT_V0 = 592 / SIZE, 1 - 915 / SIZE
PERIOD_U, PERIOD_V = 225 / SIZE, 150 / SIZE
GRASS_BAND = 0.03
INSET = 28
GAIN = (1.30, 1.21, 1.19)


def blur(a, radius):
    return np.asarray(Image.fromarray(np.clip(a, 0, 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(radius))).astype(float)


def main(path):
    atlas = np.asarray(Image.open(path).convert("RGB")).astype(float)
    x0, y0 = int(JOINT_U0 * SIZE), int((1 - JOINT_V0) * SIZE) - int(PERIOD_V * SIZE)
    w, h = int(PERIOD_U * SIZE), int(PERIOD_V * SIZE)
    crop = atlas[y0:y0 + h, x0:x0 + w]
    tiled = np.tile(crop, (3, 3, 1))
    detail = (crop - blur(tiled, 9)[h:2 * h, w:2 * w])

    ax0, ax1 = int(APRON[0] * SIZE) + INSET, int(APRON[1] * SIZE) - INSET
    ay0, ay1 = int((1 - APRON[3]) * SIZE) + INSET, int((1 - APRON[2]) * SIZE) - 3
    inner = atlas[ay0:ay1 + 1, ax0:ax1 + 1]
    pad = 40
    padded = np.pad(inner, ((pad, pad), (pad, pad), (0, 0)), mode="reflect")
    low = np.zeros_like(atlas)
    low[ay0:ay1 + 1, ax0:ax1 + 1] = blur(padded, 9)[pad:-pad, pad:-pad]

    width = int(round((U_MAX - U_MIN) * SIZE))
    height = int(round((V_MAX - V_MIN) * SIZE))
    cols = U_MIN + (np.arange(width) + 0.5) / SIZE
    rows = V_MAX - (np.arange(height) + 0.5) / SIZE
    u, v = np.meshgrid(cols, rows)

    tu = ((u - JOINT_U0) / PERIOD_U) % 1.0
    tv = ((v - JOINT_V0) / PERIOD_V) % 1.0
    high = detail[((1 - tv) * h).astype(int) % h, (tu * w).astype(int) % w]

    mv = np.where(v < EDGE_V, 2 * EDGE_V - v, v)
    px = np.clip((u * SIZE).astype(int), ax0, ax1)
    py = np.clip(((1 - mv) * SIZE).astype(int), ay0, ay1)
    base = low[py, px] + high

    gx = np.clip((u * SIZE).astype(int), 0, SIZE - 1)
    raw = atlas[py, gx]
    green = raw[..., 1] - (raw[..., 0] + raw[..., 2]) / 2
    near = (np.abs(u - APRON[1]) < GRASS_BAND) & (u <= APRON[1] + 0.002)
    mask = np.clip((green - 4) / 10, 0, 1) * near
    out = (base * (1 - mask[..., None]) + raw * mask[..., None]) * np.array(GAIN)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8)).save(OUT)
    print("textura da extensao:", OUT, out.shape)


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else os.path.join(DUMPS, "laje.png"))
