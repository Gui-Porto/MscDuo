import os

import numpy as np
from PIL import Image

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "galpao_tex")
SIZE = 1024
rng = np.random.default_rng(7)


def noise(scale, octaves=4, persistence=0.5):
    total = np.zeros((SIZE, SIZE))
    amplitude = 1.0
    norm = 0.0
    fy = np.fft.fftfreq(SIZE)[:, None]
    fx = np.fft.fftfreq(SIZE)[None, :]
    radius = np.sqrt(fx ** 2 + fy ** 2)
    for octave in range(octaves):
        cutoff = (2 ** octave) / scale
        spectrum = np.fft.fft2(rng.standard_normal((SIZE, SIZE)))
        layer = np.real(np.fft.ifft2(spectrum * np.exp(-(radius / cutoff) ** 2)))
        layer = (layer - layer.mean()) / (layer.std() + 1e-9)
        total += layer * amplitude
        norm += amplitude
        amplitude *= persistence
    return total / norm


def save(name, rgb):
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray(np.clip(rgb, 0, 255).astype(np.uint8)).save(os.path.join(OUT, name + ".png"))
    print("textura", name)


def tint(gray, color):
    return gray[..., None] / 255.0 * np.array(color, dtype=float)[None, None, :]


def grid():
    v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    return u, v


def concreto():
    base = 174 + noise(180) * 6 + noise(40, 3) * 3 + noise(6, 2) * 2.5
    speck = rng.random((SIZE, SIZE))
    base -= (speck > 0.996) * 35
    base += (speck < 0.003) * 20
    u, v = grid()
    joint = (np.minimum(u, 1 - u) < 0.0015) | (np.minimum(v, 1 - v) < 0.0015)
    base -= joint * 45
    stain = np.clip(noise(260, 2) - 0.8, 0, None) * 18
    base -= stain
    save("concreto", tint(base, (1.0 * 255, 0.99 * 255, 0.97 * 255)))


def parede():
    u, v = grid()
    base = 232 + noise(200, 3) * 3 + noise(10, 2) * 1.5
    ribs = np.cos(u * 2 * np.pi * 10) * 1.6
    seam_dist = np.minimum(np.abs(u - 0.5), np.minimum(u, 1 - u))
    base -= np.exp(-(seam_dist / 0.0018) ** 2) * 70 + np.exp(-(seam_dist / 0.006) ** 2) * 10
    base += ribs
    dirt = np.clip(noise(150, 3) * (1 - v) * 0.5, 0, None) * 6
    base -= dirt
    save("parede", tint(base, (0.99 * 255, 0.99 * 255, 0.98 * 255)))


def chapa():
    u, v = grid()
    ribs = 12
    phase = (u * ribs) % 1.0
    wave = np.sin(phase * 2 * np.pi)
    shade = 50 + wave * 16 + np.clip(wave, 0, None) ** 8 * 22
    streaks = noise(120, 3) * 4
    vertical = np.repeat(noise(400, 2).mean(axis=1, keepdims=True), SIZE, axis=1)
    shade += streaks + vertical * 3 + noise(8, 2) * 2
    save("chapa", tint(shade, (0.9 * 255, 0.93 * 255, 0.97 * 255)))


def aco():
    brushed = np.repeat(rng.standard_normal((1, SIZE)), SIZE, axis=0)
    brushed = np.real(np.fft.ifft2(np.fft.fft2(brushed) * np.exp(-(np.fft.fftfreq(SIZE)[None, :] / 0.05) ** 2)))
    brushed = (brushed - brushed.mean()) / (brushed.std() + 1e-9)
    base = 26 + brushed * 3 + noise(90, 3) * 4 + noise(5, 2) * 1.5
    save("aco", tint(base, (1.0 * 255, 1.0 * 255, 1.05 * 255)))


def portao():
    u, v = grid()
    sections = 4
    phase = (v * sections) % 1.0
    groove = np.exp(-((phase - 0.5) / 0.012) ** 2) * 55 + np.exp(-((phase - 0.5 - 0.02) / 0.02) ** 2) * -14
    ribs = np.cos(phase * 2 * np.pi * 4) * 4
    base = 205 + ribs - groove + noise(120, 3) * 4 + noise(6, 2) * 1.5
    save("portao", tint(base, (0.97 * 255, 0.98 * 255, 1.0 * 255)))


def porta():
    base = 40 + noise(80, 3) * 4 + noise(5, 2) * 2
    save("porta", tint(base, (1.0 * 255, 1.0 * 255, 1.02 * 255)))


def faixa():
    wear = noise(30, 4)
    base = np.stack([242 + wear * 6, 190 + wear * 10, 22 + wear * 8], axis=-1)
    chips = (noise(12, 3) > 1.6)[..., None]
    base = np.where(chips, np.array([150.0, 150.0, 150.0]), base)
    save("faixa", base)


def luz():
    save("luz", np.full((SIZE, SIZE, 3), 255.0))


def laje():
    base = 128 + noise(200, 3) * 6 + noise(12, 2) * 3
    save("laje", tint(base, (1.0 * 255, 1.0 * 255, 1.0 * 255)))


def degrau():
    u, v = grid()
    count = 16
    a = ((u + v) * count) % 1.0
    b = ((u - v) * count) % 1.0
    lozenge = (np.abs(a - 0.5) < 0.09) & (np.abs(((u * count * 2) % 1.0) - 0.5) < 0.45)
    lozenge2 = (np.abs(b - 0.5) < 0.09) & (np.abs(((v * count * 2) % 1.0) - 0.5) < 0.45)
    pattern = np.where(((np.floor(u * count) + np.floor(v * count)) % 2) == 0, lozenge, lozenge2)
    base = 68 + pattern * 40 + noise(70, 3) * 6 + noise(4, 2) * 2
    save("degrau", tint(base, (1.0 * 255, 1.0 * 255, 1.04 * 255)))


def vidro():
    u, v = grid()
    base = 170 + v * 25 + noise(300, 2) * 3
    save("vidro", np.stack([base * 0.82, base * 0.93, base], axis=-1))


for make in (concreto, parede, chapa, aco, portao, porta, faixa, luz, laje, degrau, vidro):
    make()
