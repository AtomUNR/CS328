"""
Boss attack sound effects (pure Python, standard library only).

Hellfire attack (fire):
  Boss_Hellfire_Cast.wav      wind-up swell, ignites right when the fireballs start
  Boss_Hellfire_Launch_1-3    soft whoosh each volley (three variants so it doesn't repeat)
  Boss_Fireball_Hit.wav       fireball hits the player

Star attack (glass / chimes):
  Boss_Star_Cast.wav          rising shimmer during the wind-up
  Boss_Star_Spawn_1-3.wav     quiet glass chime for each wave of stars
  Boss_Star_Burst.wav         a star bursts at the bottom of the arena
  Boss_Star_Hit.wav           star hits the player

Run:  py Tools/BossAudio/generate_boss_sounds.py
Output goes to Assets/Audio/SFX/Boss/.
"""

import math
import os
import random
import sys
import wave
import array

SR = 44100


# ----------------------------------------------------------------------------- building blocks

def mtof(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


class Buf:
    def __init__(self, seconds):
        self.n = int(seconds * SR)
        self.d = [0.0] * self.n

    def add(self, samples, t=0.0, gain=1.0):
        s = int(t * SR)
        for i, v in enumerate(samples):
            j = s + i
            if 0 <= j < self.n:
                self.d[j] += v * gain


def env_ad(n, attack, decay_tau):
    """Attack/exponential-decay envelope as a list of n values."""
    a = max(1, int(attack * SR))
    out = [0.0] * n
    for i in range(n):
        up = min(1.0, i / a)
        out[i] = up * math.exp(-i / (decay_tau * SR))
    return out


def white(n, rng):
    return [rng.uniform(-1.0, 1.0) for _ in range(n)]


def lowpass(x, fc):
    a = 1.0 - math.exp(-2.0 * math.pi * fc / SR)
    y = 0.0
    out = []
    for v in x:
        y += a * (v - y)
        out.append(y)
    return out


def highpass(x, fc):
    lp = lowpass(x, fc)
    return [v - l for v, l in zip(x, lp)]


def svf_band(x, fc_func, q=0.25):
    """State-variable bandpass with a moving centre frequency. fc_func(i) -> Hz. q small = resonant."""
    low = band = 0.0
    out = []
    for i, v in enumerate(x):
        f = 2.0 * math.sin(math.pi * min(fc_func(i), SR / 6.0) / SR)
        low += f * band
        high = v - low - q * band
        band += f * high
        out.append(band)
    return out


def sine(freq_func, n, phase0=0.0):
    ph = phase0
    out = []
    for i in range(n):
        ph += 2.0 * math.pi * freq_func(i) / SR
        out.append(math.sin(ph))
    return out


def crackle(n, rng, density_func, strength=1.0):
    """Sparse clicks like burning wood. density_func(i) = clicks per second."""
    out = [0.0] * n
    i = 0
    while i < n:
        d = max(1.0, density_func(i))
        if rng.random() < d / SR:
            amp = rng.uniform(0.25, 1.0) * strength
            ln = rng.randint(8, 60)
            for k in range(ln):
                if i + k < n:
                    out[i + k] += rng.uniform(-1, 1) * amp * math.exp(-k / (ln * 0.25))
        i += 1
    return out


def bell_partials(freq, dur, ratios, rng):
    """Chime / glass bell from inharmonic partials. ratios = ((ratio, amp, decay_fraction), ...)."""
    n = int(dur * SR)
    out = [0.0] * n
    for ratio, amp, frac in ratios:
        w = 2.0 * math.pi * freq * ratio / SR
        if w >= math.pi:
            continue
        c2 = 2.0 * math.cos(w)
        s0, s1 = 0.0, -math.sin(w)
        decay = math.exp(-1.0 / (dur * frac * 0.3 * SR))
        e = amp
        for i in range(n):
            out[i] += s0 * e
            s0, s1 = c2 * s0 - s1, s0
            e *= decay
    # tiny click at the start
    for i in range(min(n, int(0.004 * SR))):
        out[i] += rng.uniform(-1, 1) * 0.3
    return out


GLASS = ((1.0, 1.0, 1.0), (2.76, 0.6, 0.6), (5.4, 0.4, 0.4), (8.93, 0.25, 0.25))
HIGH_BELL = ((1.0, 1.0, 1.0), (2.0, 0.4, 0.7), (3.01, 0.3, 0.5), (4.17, 0.18, 0.35))


def finish(x, peak=0.88, fade=0.015):
    p = max(abs(v) for v in x) or 1.0
    g = peak / p
    x = [math.tanh(v * g * 1.15) for v in x]
    p = max(abs(v) for v in x) or 1.0
    x = [v * peak / p for v in x]
    f = int(fade * SR)
    n = len(x)
    for i in range(min(f, n)):
        x[i] *= i / f              # soft start (no click)
        x[n - 1 - i] *= i / f      # soft end
    return x


def write_wav(path, x):
    data = array.array('h', [int(max(-1.0, min(1.0, v)) * 32767) for v in x])
    with wave.open(path, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ----------------------------------------------------------------------------- hellfire (fire)

def hellfire_cast(seed=1):
    """1.2 s. Rumble and crackles swell for 0.75 s (the game's wind-up), then ignite on the beat."""
    rng = random.Random(seed)
    dur, ignite = 1.25, 0.75
    n = int(dur * SR)
    b = Buf(dur)

    swell = [min(1.0, (i / SR) / ignite) ** 1.6 for i in range(n)]
    swell = [s if i / SR < ignite else math.exp(-(i / SR - ignite) / 0.35) for i, s in enumerate(swell)]

    rumble = lowpass(lowpass(white(n, rng), 260), 380)
    rumble = [v * 6.0 * swell[i] for i, v in enumerate(rumble)]
    b.add(rumble, 0, 0.9)

    drone = sine(lambda i: 52.0 + 36.0 * min(1.0, (i / SR) / ignite), n)
    b.add([v * swell[i] for i, v in enumerate(drone)], 0, 0.5)

    fire = svf_band(white(n, rng), lambda i: 500 + 1800 * min(1.0, (i / SR) / ignite), 0.5)
    b.add([v * swell[i] * 0.22 for i, v in enumerate(fire)], 0, 0.8)

    clicks = crackle(n, rng, lambda i: 15 + 260 * min(1.0, (i / SR) / ignite), 0.5)
    b.add([v * (0.25 + swell[i]) for i, v in enumerate(clicks)], 0, 0.6)

    # Ignition: whoosh + thump exactly at the end of the wind-up.
    m = int(0.7 * SR)
    burst = svf_band(white(m, rng), lambda i: 2600 - 1700 * (i / m), 0.4)
    e = env_ad(m, 0.012, 0.18)
    b.add([v * e[i] * 0.55 for i, v in enumerate(burst)], ignite - 0.01, 1.0)
    thump = sine(lambda i: 110 * math.exp(-i / (0.09 * SR)) + 40, int(0.5 * SR))
    e = env_ad(len(thump), 0.003, 0.16)
    b.add([v * e[i] for i, v in enumerate(thump)], ignite - 0.005, 0.9)
    return finish(b.d)


def hellfire_launch(variant):
    """~0.4 s fire whoosh; three variants with different pitch and noise."""
    rng = random.Random(100 + variant)
    dur = 0.42
    n = int(dur * SR)
    start, end = (2200, 520) if variant == 1 else (1700, 380) if variant == 2 else (2800, 700)
    noise = white(n, rng)
    band = svf_band(noise, lambda i: start + (end - start) * (i / n) ** 0.7, 0.45)
    e = env_ad(n, 0.025, 0.11)
    body = [v * e[i] for i, v in enumerate(band)]
    low = lowpass(white(n, rng), 300)
    el = env_ad(n, 0.04, 0.1)
    body = [body[i] + low[i] * el[i] * 1.8 for i in range(n)]
    clk = crackle(n, rng, lambda i: 120, 0.3)
    body = [body[i] + clk[i] * e[i] * 0.4 for i in range(n)]
    return finish(body, 0.8)


def fireball_hit():
    """~0.65 s impact: low thump, fire burst, crackling aftermath."""
    rng = random.Random(7)
    dur = 0.65
    n = int(dur * SR)
    b = Buf(dur)
    thump = sine(lambda i: 140 * math.exp(-i / (0.07 * SR)) + 42, n)
    e = env_ad(n, 0.002, 0.14)
    b.add([v * e[i] for i, v in enumerate(thump)], 0, 1.0)
    burst = lowpass(white(n, rng), 3200)
    e = env_ad(n, 0.003, 0.09)
    b.add([v * e[i] for i, v in enumerate(burst)], 0, 0.9)
    mid = svf_band(white(n, rng), lambda i: 1400 - 900 * (i / n), 0.5)
    e = env_ad(n, 0.01, 0.2)
    b.add([v * e[i] * 0.4 for i, v in enumerate(mid)], 0, 0.8)
    clicks = crackle(n, rng, lambda i: 320 * math.exp(-i / (0.25 * SR)) + 20, 0.6)
    e = env_ad(n, 0.0, 0.3)
    b.add([v * e[i] for i, v in enumerate(clicks)], 0.02, 0.7)
    return finish(b.d)


# ----------------------------------------------------------------------------- stars (glass / chimes)

PENTA = [76, 79, 81, 84, 88, 91, 93]   # E G A C E G A: bright pentatonic chimes


def star_cast(seed=3):
    """1.25 s. Rising shimmer of chimes that accelerates, then one bright strike on the beat."""
    rng = random.Random(seed)
    dur, strike = 1.25, 0.75
    n = int(dur * SR)
    b = Buf(dur)

    # Airy rising noise (high band), cut off at the strike.
    air = svf_band(white(n, rng), lambda i: 2500 + 4500 * min(1.0, (i / SR) / strike), 0.6)
    sw = [min(1.0, (i / SR) / strike) ** 2.0 if i / SR < strike else math.exp(-(i / SR - strike) / 0.15)
          for i in range(n)]
    b.add([v * sw[i] * 0.25 for i, v in enumerate(air)], 0, 0.7)

    # Chimes on an accelerating rhythm, climbing the scale.
    t, gap, k = 0.02, 0.17, 0
    while t < strike - 0.03:
        note = PENTA[min(k // 1, len(PENTA) - 1)] - 12 + (12 if k > 3 else 0)
        c = bell_partials(mtof(note), 0.55, GLASS, rng)
        b.add(c, t, 0.16 + 0.12 * (t / strike))
        t += gap
        gap *= 0.82
        k += 1
    # The strike: a full chord of glass bells.
    for note, g in ((88, 0.5), (91, 0.4), (95, 0.3), (100, 0.2)):
        b.add(bell_partials(mtof(note), 0.5, GLASS, rng), strike - 0.005, g)
    # Soft low tone beneath, so it has weight.
    low = sine(lambda i: 196.0, int(1.0 * SR))
    el = env_ad(len(low), 0.5, 0.35)
    b.add([v * el[i] for i, v in enumerate(low)], 0.1, 0.18)
    return finish(b.d)


def star_spawn(variant):
    """~0.55 s soft glass chime for each wave."""
    rng = random.Random(40 + variant)
    note = (84, 88, 91)[variant - 1]
    c = bell_partials(mtof(note), 0.55, HIGH_BELL, rng)
    return finish(c, 0.7, 0.01)


def star_burst():
    """~0.75 s: pop, then a quick falling run of sparkle chimes."""
    rng = random.Random(11)
    dur = 0.75
    n = int(dur * SR)
    b = Buf(dur)
    pop = highpass(white(n, rng), 900)
    e = env_ad(n, 0.001, 0.05)
    b.add([v * e[i] for i, v in enumerate(pop)], 0, 0.8)
    thump = sine(lambda i: 170 * math.exp(-i / (0.05 * SR)) + 70, int(0.3 * SR))
    e = env_ad(len(thump), 0.002, 0.08)
    b.add([v * e[i] for i, v in enumerate(thump)], 0, 0.55)
    for k, note in enumerate((100, 96, 93, 91, 88, 84)):
        b.add(bell_partials(mtof(note), 0.45, GLASS, rng), 0.02 + k * 0.045, 0.28 - k * 0.025)
    return finish(b.d)


def star_hit():
    """~0.5 s: sharp glass crack with a dissonant chime."""
    rng = random.Random(13)
    dur = 0.55
    n = int(dur * SR)
    b = Buf(dur)
    crack = highpass(white(n, rng), 1600)
    e = env_ad(n, 0.001, 0.045)
    b.add([v * e[i] for i, v in enumerate(crack)], 0, 1.0)
    for note, g in ((84, 0.55), (90, 0.45), (97, 0.3)):   # tritone-ish cluster for a sting
        b.add(bell_partials(mtof(note), 0.5, GLASS, rng), 0, g)
    thump = sine(lambda i: 120 * math.exp(-i / (0.05 * SR)) + 55, int(0.25 * SR))
    e = env_ad(len(thump), 0.002, 0.07)
    b.add([v * e[i] for i, v in enumerate(thump)], 0, 0.6)
    return finish(b.d)


SOUNDS = {
    'Boss_Hellfire_Cast.wav': hellfire_cast,
    'Boss_Hellfire_Launch_1.wav': lambda: hellfire_launch(1),
    'Boss_Hellfire_Launch_2.wav': lambda: hellfire_launch(2),
    'Boss_Hellfire_Launch_3.wav': lambda: hellfire_launch(3),
    'Boss_Fireball_Hit.wav': fireball_hit,
    'Boss_Star_Cast.wav': star_cast,
    'Boss_Star_Spawn_1.wav': lambda: star_spawn(1),
    'Boss_Star_Spawn_2.wav': lambda: star_spawn(2),
    'Boss_Star_Spawn_3.wav': lambda: star_spawn(3),
    'Boss_Star_Burst.wav': star_burst,
    'Boss_Star_Hit.wav': star_hit,
}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, '..', '..', 'Assets', 'Audio', 'SFX', 'Boss')
    os.makedirs(out_dir, exist_ok=True)
    for name, build in SOUNDS.items():
        x = build()
        write_wav(os.path.join(out_dir, name), x)
        rms = math.sqrt(sum(v * v for v in x) / len(x))
        print(f'{name}: {len(x) / SR:.2f}s rms {rms:.3f}', flush=True)


if __name__ == '__main__':
    main()
