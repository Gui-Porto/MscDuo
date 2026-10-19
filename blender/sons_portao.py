import os
import wave

import numpy as np

SR = 32000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sons_portao")
rng = np.random.default_rng(13)


def biquad(x, kind, f, q):
    w = 2 * np.pi * f / SR
    alpha = np.sin(w) / (2 * q)
    c = np.cos(w)
    if kind == "bp":
        b = [alpha, 0.0, -alpha]
    elif kind == "lp":
        b = [(1 - c) / 2, 1 - c, (1 - c) / 2]
    else:
        b = [(1 + c) / 2, -(1 + c), (1 + c) / 2]
    a = [1 + alpha, -2 * c, 1 - alpha]
    b = [v / a[0] for v in b]
    a1, a2 = a[1] / a[0], a[2] / a[0]
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.0
    for i in range(len(x)):
        v = b[0] * x[i] + b[1] * x1 + b[2] * x2 - a1 * y1 - a2 * y2
        x2, x1 = x1, x[i]
        y2, y1 = y1, v
        y[i] = v
    return y


def loop_filter(x, kind, f, q):
    return biquad(np.tile(x, 2), kind, f, q)[len(x):]


def noise(n):
    return rng.standard_normal(n)


def modes(n, start, freqs, decays, amps, jitter=0.0):
    t = np.arange(n - start) / SR
    out = np.zeros(n)
    for f, d, a in zip(freqs, decays, amps):
        f = f * (1 + jitter * rng.uniform(-1, 1))
        out[start:] += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi)) * np.exp(-t / d)
    return out


def burst(n, start, length, amp, kind="hp", f=2500.0):
    out = np.zeros(n)
    m = max(2, int(length * SR))
    if start >= n:
        return out
    m = min(m, n - start)
    env = np.exp(-np.arange(m) / (m / 4))
    b = noise(m) * env
    if kind:
        b = biquad(b, kind, f, 0.7)
    out[start:start + m] = amp * b / (np.abs(b).max() + 1e-9)
    return out


def click(n, at, amp, low=1800, high=5200, decay=(0.006, 0.02), wrap=False):
    length = int(0.08 * SR)
    piece = np.zeros(length)
    piece += burst(length, 0, 0.0015, 0.6)
    freqs = rng.uniform(low, high, 3)
    decays = rng.uniform(decay[0], decay[1], 3)
    amps = rng.uniform(0.3, 1.0, 3)
    piece += modes(length, 0, freqs, decays, amps)
    piece *= amp / (np.abs(piece).max() + 1e-9)
    out = np.zeros(n)
    idx = (at + np.arange(length)) % n if wrap else at + np.arange(length)
    keep = idx < n
    np.add.at(out, idx[keep], piece[keep])
    return out


def loop_noise(n, fade):
    raw = noise(n + fade)
    head = raw[:fade]
    tail = raw[n:n + fade]
    ramp = np.linspace(0, 1, fade)
    out = raw[:n].copy()
    out[:fade] = head * ramp + tail * (1 - ramp)
    return out


def motor():
    n = SR * 3
    t = np.arange(n) / SR
    fade = SR // 2
    hum = 0.10 * np.sin(2 * np.pi * 120 * t) + 0.045 * np.sin(2 * np.pi * 240 * t + 0.4) + 0.03 * np.sin(2 * np.pi * 360 * t + 1.1) + 0.015 * np.sin(2 * np.pi * 480 * t + 2.0)
    rotor = 1 + 0.18 * np.sin(2 * np.pi * 29 * t)
    slot = 0.03 * np.sin(2 * np.pi * 812 * t) * rotor + 0.012 * np.sin(2 * np.pi * 1624 * t)
    gear = (0.05 * np.sin(2 * np.pi * 340 * t) + 0.022 * np.sin(2 * np.pi * 680 * t + 0.7) + 0.01 * np.sin(2 * np.pi * 1020 * t)) * (1 + 0.25 * np.sin(2 * np.pi * (2 / 3) * t))
    base = loop_noise(n, fade)
    mech = loop_filter(base, "bp", 520, 0.8) * 0.9 + loop_filter(base, "bp", 1900, 1.4) * 0.5
    mech = mech / (np.abs(mech).max() + 1e-9) * 0.09 * rotor * (1 + 0.15 * np.sin(2 * np.pi * (2 / 3) * t + 1.3))
    rumble = loop_filter(loop_noise(n, fade), "lp", 140, 0.7)
    rumble = rumble / (np.abs(rumble).max() + 1e-9) * 0.12
    out = hum + slot + gear + mech + rumble
    return out


def laminas():
    n = SR * 3
    out = np.zeros(n)
    rate = 6.0
    for k in range(int(3 * rate)):
        at = int((k / rate + rng.uniform(-0.015, 0.015)) * SR) % n
        amp = rng.uniform(0.55, 1.0)
        out += click(n, at, amp, wrap=True)
        out += click(n, (at + int(rng.uniform(0.003, 0.009) * SR)) % n, amp * rng.uniform(0.5, 0.9), wrap=True)
        if k % 4 == 1:
            out += click(n, (at + int(0.02 * SR)) % n, 0.5, low=650, high=1200, decay=(0.025, 0.05), wrap=True)
    for _ in range(int(3 * 26)):
        out += click(n, int(rng.uniform(0, n)), rng.uniform(0.12, 0.32), wrap=True)
    scrape = loop_filter(loop_noise(n, SR // 2), "bp", 2600, 0.8)
    scrape = scrape / (np.abs(scrape).max() + 1e-9) * 0.05
    flex = loop_filter(loop_noise(n, SR // 2), "lp", 260, 0.7)
    flex = flex / (np.abs(flex).max() + 1e-9) * 0.08
    return out / (np.abs(out).max() + 1e-9) * 0.85 + scrape + flex


def partida():
    n = int(0.55 * SR)
    t = np.arange(n) / SR
    out = burst(n, 0, 0.0009, 0.9)
    out += modes(n, 0, [2900, 5100, 1300], [0.006, 0.003, 0.012], [0.5, 0.3, 0.25])
    out += modes(n, 0, [160], [0.025], [0.55])
    bounce = int(0.006 * SR)
    out += burst(n, bounce, 0.0007, 0.4)
    out += modes(n, bounce, [3100, 4700], [0.004, 0.002], [0.25, 0.15])
    coil = (np.sign(np.sin(2 * np.pi * 120 * t)) * 0.5 + np.sin(2 * np.pi * 120 * t) * 0.5) * 0.06 * np.exp(-t / 0.12)
    out += biquad(coil, "lp", 900, 0.7)
    start = int(0.03 * SR)
    out += modes(n, start, [58, 116], [0.09, 0.05], [0.35, 0.12])
    return out


def freio():
    n = int(1.1 * SR)
    out = burst(n, 0, 0.0008, 0.5)
    out += modes(n, 0, [3300, 5600], [0.004, 0.002], [0.3, 0.15])
    clunk = int(0.016 * SR)
    out += modes(n, clunk, [95, 190], [0.06, 0.03], [0.85, 0.25])
    out += burst(n, clunk, 0.012, 0.45, "lp", 600)
    out += modes(n, clunk, [420, 980, 1630, 2480, 3710], [0.35, 0.25, 0.18, 0.12, 0.08], [0.22, 0.18, 0.13, 0.09, 0.05], 0.03)
    for k in range(9):
        at = clunk + int(rng.uniform(0.06, 0.45) * SR)
        out += click(n, at, 0.35 * (1 - k / 11), decay=(0.004, 0.015))
    return out


def batida():
    n = int(1.4 * SR)
    out = modes(n, 0, [70, 140], [0.09, 0.04], [0.95, 0.3])
    out += burst(n, 0, 0.02, 0.6, "lp", 500)
    out += modes(n, int(0.004 * SR), [310, 760, 1390, 2210], [0.3, 0.2, 0.14, 0.1], [0.16, 0.12, 0.08, 0.05], 0.03)
    time = 0.025
    for k in range(14):
        at = int(time * SR)
        out += click(n, at, 0.62 * (1 - k / 16), low=900, high=2500, decay=(0.01, 0.03))
        time += 0.018 + 0.006 * k + rng.uniform(-0.004, 0.004)
    settle = biquad(noise(n), "bp", 420, 0.9) * np.exp(-np.arange(n) / SR / 0.35)
    out += settle / (np.abs(settle).max() + 1e-9) * 0.1
    return out


def botao():
    n = int(0.25 * SR)
    out = burst(n, 0, 0.0005, 0.7)
    out += modes(n, 0, [3800, 6200, 1100], [0.002, 0.0012, 0.008], [0.6, 0.35, 0.3])
    release = int(0.09 * SR)
    out += burst(n, release, 0.0005, 0.4)
    out += modes(n, release, [4200, 6900, 1200], [0.0015, 0.001, 0.006], [0.35, 0.2, 0.18])
    return out


def write(name, data, peak):
    data = data / (np.abs(data).max() + 1e-9) * peak
    pcm = np.clip(data * 32767, -32768, 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(pcm.tobytes())
    print(name, len(data) / SR, "s")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    write("motor", motor(), 0.8)
    write("laminas", laminas(), 0.8)
    write("partida", partida(), 0.9)
    write("freio", freio(), 0.9)
    write("batida", batida(), 0.95)
    write("botao", botao(), 0.8)
