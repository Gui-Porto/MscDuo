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
