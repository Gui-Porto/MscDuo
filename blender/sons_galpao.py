import os
import wave

import numpy as np

SR = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "sons_galpao")
rng = np.random.default_rng(5)


def note(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def square(f, length, duty=0.5, vol=0.3):
    t = np.arange(int(length * SR)) / SR
    wave_ = np.where((t * f) % 1.0 < duty, 1.0, -1.0) * vol
    env = np.minimum(1.0, np.arange(len(t)) / 60.0) * np.minimum(1.0, (len(t) - np.arange(len(t))) / 200.0)
    return wave_ * env


def triangle(f, length, vol=0.3):
    t = np.arange(int(length * SR)) / SR
    return (2 * np.abs(2 * ((t * f) % 1.0) - 1) - 1) * vol


def seq(notes, step, kind="square", duty=0.5, vol=0.25):
    out = []
    for n in notes:
        if n is None:
            out.append(np.zeros(int(step * SR)))
        elif kind == "square":
            out.append(square(note(n), step, duty, vol))
        else:
            out.append(triangle(note(n), step, vol))
    return np.concatenate(out)


def mix(*tracks):
    n = max(len(t) for t in tracks)
    out = np.zeros(n)
    for t in tracks:
        out[:len(t)] += t
    return out


def elben():
    step = 0.15
    melody = [69, 72, 76, 74, 72, 69, 67, 69, 72, 71, 69, 67, 64, 67, 69, None] * 2
    bass = [45, None, 45, None, 43, None, 43, None, 41, None, 41, None, 43, None, 43, None] * 2
    return mix(seq(melody, step, duty=0.25, vol=0.18), seq(bass, step, "tri", vol=0.28))


def defender():
    step = 0.12
    bass = [40, 40, 52, 40, 40, 52, 43, 55, 38, 38, 50, 38, 43, 43, 55, 47] * 2
    out = seq(bass, step, "tri", vol=0.3)
    for k in range(6):
        at = int(rng.uniform(0, len(out) - SR * 0.3))
        length = int(0.22 * SR)
        t = np.arange(length) / SR
        f = 1800 * np.exp(-t * 9) + 200
        zap = np.where((np.cumsum(f) / SR) % 1.0 < 0.5, 1.0, -1.0) * 0.14 * np.exp(-t * 6)
        out[at:at + length] += zap
    return out


def invaders():
    step = 0.42
    beat = []
    for n in [41, 39, 37, 36] * 3:
        tone = square(note(n), 0.11, 0.5, 0.32)
        beat.append(np.concatenate([tone, np.zeros(int(step * SR) - len(tone))]))
    out = np.concatenate(beat)
    t = np.arange(len(out)) / SR
    ufo = np.sin(2 * np.pi * (900 + 250 * np.sin(2 * np.pi * 7 * t)) * t) * 0.05
    gate = ((t > 1.6) & (t < 3.4)).astype(float)
    return out + ufo * gate


def estatica():
    n = SR * 2
    x = rng.standard_normal(n) * 0.25
    return x - np.convolve(x, np.ones(3) / 3, mode="same") * 0.6


def tom():
    t = np.arange(SR) / SR
    return np.sin(2 * np.pi * 1000 * t) * 0.18


def programa():
    step = 0.2
    melody = [72, 76, 79, 76, 77, 74, 71, 74, 72, 76, 79, 84, 83, 79, 74, None]
    chords = [48, 48, 48, 48, 53, 53, 55, 55, 48, 48, 52, 52, 55, 55, 55, 55]
    return mix(seq(melody, step, duty=0.5, vol=0.14), seq(chords, step, "tri", vol=0.22))


def write(name, data):
    data = data / (np.abs(data).max() + 1e-9) * 0.8
    pcm = np.clip(data * 32767, -32768, 32767).astype("<i2")
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as f:
        f.setnchannels(1)
        f.setsampwidth(2)
        f.setframerate(SR)
        f.writeframes(pcm.tobytes())
    print(name, round(len(data) / SR, 2), "s")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    write("fliperama", elben())
    write("fliperama2", defender())
    write("fliperama3", invaders())
    write("tv_estatica", estatica())
    write("tv_tom", tom())
    write("tv_programa", programa())
