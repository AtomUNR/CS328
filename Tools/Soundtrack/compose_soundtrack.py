"""
Battle Tower soundtrack composer.

Renders three seamless music loops for the game with acoustic-style instruments
(no synth leads or pads):

  Music_TowerInterior.wav  - the wizard's tower interior (harp, gamba, recorder, bells)
  Music_GuardBattle.wav    - battle against the Tower Guardian (lute, frame drums, recorder, fiddle)
  Music_BossBattle.wav     - battle against the boss (harpsichord, timpani, gamba, oboe, bells)

The exterior uses the existing Music_TowerEntrance_v3.ogg, so it is not generated here.

Pure Python (standard library only). Run:
    py Tools/Soundtrack/compose_soundtrack.py
Output goes to Assets/Audio/Music/. Each file loops seamlessly: the reverb and ringing
notes at the end are folded back onto the start.
"""

import array
import math
import os
import random
import sys
import time
import wave

SR = 44100


def mtof(m):
    return 440.0 * 2.0 ** ((m - 69) / 12.0)


# ----------------------------------------------------------------------------- mixer

class Mix:
    def __init__(self, seconds, tail=5.0):
        self.loop_n = int(round(seconds * SR))
        self.n = self.loop_n + int(tail * SR)
        self.L = array.array('f', bytes(4 * self.n))
        self.R = array.array('f', bytes(4 * self.n))
        self.send = array.array('f', bytes(4 * self.n))

    def add(self, t, samples, gain=1.0, pan=0.0, rev=0.25):
        start = int(t * SR)
        if start < 0:
            # Humanised timing nudged the note before 0:00 - wrap it to the end of the loop.
            start += self.loop_n
        a = (pan + 1.0) * math.pi / 4.0
        gl = math.cos(a) * gain * 1.414
        gr = math.sin(a) * gain * 1.414
        gs = gain * rev
        L, R, S = self.L, self.R, self.send
        end = min(self.n, start + len(samples))
        j = 0
        for i in range(start, end):
            s = samples[j]
            j += 1
            L[i] += s * gl
            R[i] += s * gr
            S[i] += s * gs

    def render(self, room=0.86, damp=0.3, wet=0.11):
        n = self.n
        wet_mono = reverb(self.send, n, room, damp)
        # Slightly delayed copy on the right gives a wide stone-hall image.
        offset = int(0.017 * SR)
        L, R = self.L, self.R
        for i in range(n):
            w = wet_mono[i] * wet
            L[i] += w
            k = i - offset
            R[i] += (wet_mono[k] * wet) if k >= 0 else 0.0
        # Fold the tail onto the start so the loop is seamless.
        loop_n = self.loop_n
        for i in range(n - loop_n):
            L[i] += L[loop_n + i]
            R[i] += R[loop_n + i]
        L = L[:loop_n]
        R = R[:loop_n]
        # Soft-limit then normalise.
        peak = max(max(L), -min(L), max(R), -min(R)) or 1.0
        drive = 1.35 / peak
        th = math.tanh
        L = [th(x * drive) for x in L]
        R = [th(x * drive) for x in R]
        peak = max(max(L), -min(L), max(R), -min(R)) or 1.0
        g = 0.89 / peak
        return [x * g for x in L], [x * g for x in R]


def reverb(send, n, room, damp):
    """Small Schroeder/Freeverb-style hall: 4 damped combs + 2 allpasses."""
    out = array.array('f', bytes(4 * n))
    for d in (1557, 1617, 1491, 1422):
        buf = [0.0] * d
        idx = 0
        filt = 0.0
        a = 1.0 - damp
        for i in range(n):
            y = buf[idx]
            filt = y * a + filt * damp
            buf[idx] = send[i] + filt * room
            out[i] += y
            idx += 1
            if idx == d:
                idx = 0
    for d in (556, 441):
        buf = [0.0] * d
        idx = 0
        for i in range(n):
            x = out[i]
            b = buf[idx]
            out[i] = b - x
            buf[idx] = x + b * 0.5
            idx += 1
            if idx == d:
                idx = 0
    return out


def write_wav(path, L, R):
    data = array.array('h', bytes(2 * 2 * len(L)))
    k = 0
    for l, r in zip(L, R):
        data[k] = int(max(-1.0, min(1.0, l)) * 32767)
        data[k + 1] = int(max(-1.0, min(1.0, r)) * 32767)
        k += 2
    with wave.open(path, 'wb') as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())


# ----------------------------------------------------------------------------- instruments

def fade_out(out, seconds=0.02):
    n = len(out)
    f = min(n, int(seconds * SR))
    for i in range(f):
        out[n - f + i] *= (f - i) / f
    return out


def pluck(freq, dur, rng, bright=0.5, damp=0.996):
    """Karplus-Strong plucked string: harp, lute or harpsichord depending on brightness."""
    n = int(dur * SR)
    N = max(2, int(SR / freq))
    buf = [rng.uniform(-1.0, 1.0) for _ in range(N)]
    a = 1.0 - bright
    prev = 0.0
    for i in range(N):
        prev = prev * a + buf[i] * (1.0 - a)
        buf[i] = prev
    mean = sum(buf) / N
    peak = max(abs(x - mean) for x in buf) or 1.0
    buf = [(x - mean) / peak for x in buf]
    out = [0.0] * n
    idx = 0
    h = damp * 0.5
    for i in range(n):
        j = idx + 1
        if j == N:
            j = 0
        v = buf[idx]
        buf[idx] = h * (v + buf[j])
        out[i] = v
        idx = j
    return fade_out(out)


def make_table(harmonics, size=2048):
    t = [0.0] * size
    for k, amp in enumerate(harmonics, 1):
        w = 2.0 * math.pi * k / size
        for i in range(size):
            t[i] += amp * math.sin(w * i)
    peak = max(abs(x) for x in t)
    return [x / peak for x in t]


GAMBA = make_table([1, 0.62, 0.45, 0.33, 0.25, 0.18, 0.13, 0.09, 0.06, 0.04])   # bowed bass / viol
FIDDLE = make_table([1, 0.55, 0.42, 0.36, 0.26, 0.2, 0.14, 0.1, 0.07])          # bowed fiddle / viola
RECORDER = make_table([1, 0.3, 0.12, 0.05, 0.02])                                 # soft wooden flute
OBOE = make_table([1, 0.75, 0.62, 0.55, 0.38, 0.27, 0.18, 0.12, 0.08, 0.05])     # nasal double reed


def sustained(freq, dur, rng, table, attack=0.08, release=0.15, vib_rate=5.0,
              vib_depth=0.004, vib_delay=0.3, noise=0.0):
    """Bowed or blown note from a harmonic wavetable, with vibrato and bow/breath noise."""
    n = int(dur * SR)
    size = len(table)
    out = [0.0] * n
    phase = 0.0
    step = freq * size / SR
    att_n = max(1, int(attack * SR))
    rel_n = max(1, int(release * SR))
    vib_w = 2.0 * math.pi * vib_rate / SR
    vib_start = int(vib_delay * SR)
    ramp = int(0.35 * SR)
    nz = 0.0
    sin = math.sin
    uni = rng.uniform
    for i in range(n):
        if i > vib_start:
            depth = vib_depth * min(1.0, (i - vib_start) / ramp)
            phase += step * (1.0 + depth * sin(vib_w * i))
        else:
            phase += step
        if phase >= size:
            phase -= size
        s = table[int(phase)]
        if noise:
            nz = nz * 0.6 + uni(-1.0, 1.0) * 0.4
            s += nz * noise
        e = 1.0
        if i < att_n:
            e = i / att_n
        r = n - i
        if r < rel_n:
            e *= r / rel_n
        out[i] = s * e
    return out


def bell(freq, dur, rng):
    """Church bell: inharmonic partials with long, staggered decays."""
    n = int(dur * SR)
    out = [0.0] * n
    partials = ((0.5, 0.55, 1.0), (1.0, 1.0, 0.8), (1.19, 0.55, 0.55), (1.56, 0.35, 0.45),
                (2.0, 0.42, 0.4), (2.51, 0.25, 0.28), (3.01, 0.2, 0.22), (4.17, 0.12, 0.15))
    for ratio, amp, frac in partials:
        w = 2.0 * math.pi * freq * ratio / SR
        if w >= math.pi:
            continue
        c2 = 2.0 * math.cos(w)
        s0 = 0.0
        s1 = -math.sin(w)
        decay = math.exp(-1.0 / (dur * frac * 0.32 * SR))
        env = amp
        for i in range(n):
            out[i] += s0 * env
            s0, s1 = c2 * s0 - s1, s0
            env *= decay
    # Clapper strike.
    nz = 0.0
    for i in range(int(0.02 * SR)):
        nz = nz * 0.5 + rng.uniform(-1, 1) * 0.5
        out[i] += nz * 0.5 * (1 - i / (0.02 * SR))
    peak = max(abs(x) for x in out) or 1.0
    return fade_out([x / peak for x in out], 0.3)


def tom(f0, rng, dur=0.45, decay=0.18, drop=0.55, noise=0.35):
    """Frame drum / tom: pitch-dropping skin plus a stick click."""
    n = int(dur * SR)
    out = [0.0] * n
    phase = 0.0
    nz = 0.0
    exp = math.exp
    sin = math.sin
    for i in range(n):
        t = i / SR
        f = f0 * (drop + (1.0 - drop) * exp(-t / 0.04))
        phase += 2.0 * math.pi * f / SR
        s = sin(phase) * exp(-t / decay)
        if t < 0.03:
            nz = nz * 0.55 + rng.uniform(-1, 1) * 0.45
            s += nz * noise * exp(-t / 0.006)
        out[i] = s
    return fade_out(out)


def timpani(freq, rng, dur=1.6):
    n = int(dur * SR)
    out = [0.0] * n
    modes = ((1.0, 1.0, 0.75), (1.5, 0.5, 0.45), (1.98, 0.35, 0.35), (2.44, 0.2, 0.25))
    for ratio, amp, decay in modes:
        phase = 0.0
        f = freq * ratio
        for i in range(n):
            t = i / SR
            phase += 2.0 * math.pi * f * (1.0 + 0.012 * math.exp(-t / 0.05)) / SR
            out[i] += amp * math.sin(phase) * math.exp(-t / decay)
    nz = 0.0
    for i in range(int(0.04 * SR)):
        nz = nz * 0.8 + rng.uniform(-1, 1) * 0.2
        out[i] += nz * 1.2 * math.exp(-i / (0.01 * SR))
    peak = max(abs(x) for x in out) or 1.0
    return fade_out([x / peak for x in out], 0.1)


def boom(rng):
    """Big low drum for the boss downbeats."""
    return tom(58.0, rng, dur=1.4, decay=0.55, drop=0.72, noise=0.5)


def tambourine(rng, dur=0.16):
    n = int(dur * SR)
    out = [0.0] * n
    prev = 0.0
    for i in range(n):
        x = rng.uniform(-1, 1)
        hp = x - prev          # crude high-pass for the jingles
        prev = x
        t = i / SR
        env = math.exp(-t / 0.035) + 0.5 * math.exp(-max(0.0, t - 0.018) / 0.03) * (t > 0.018)
        out[i] = hp * env * 0.5
    return fade_out(out)


# ----------------------------------------------------------------------------- song helpers

class Song:
    def __init__(self, bpm, bars, seed, beats_per_bar=4):
        self.beat = 60.0 / bpm
        self.bpb = beats_per_bar
        self.bars = bars
        self.seconds = bars * beats_per_bar * self.beat
        self.mix = Mix(self.seconds)
        self.rng = random.Random(seed)

    def t(self, bar, beat, human=0.006):
        return (bar * self.bpb + beat) * self.beat + self.rng.uniform(-human, human)

    def vel(self, base, spread=0.08):
        return base * (1.0 + self.rng.uniform(-spread, spread))


def play_melody(song, notes, bar_offset, make_note, gain, pan, rev, transpose=0, legato=0.04):
    for bar, beat, length, midi in notes:
        dur = length * song.beat + legato
        s = make_note(mtof(midi + transpose), dur)
        song.mix.add(song.t(bar + bar_offset, beat, 0.004), s, song.vel(gain, 0.05), pan, rev)


# ----------------------------------------------------------------------------- tracks

def tower_interior():
    """Mysterious and spacious: harp arpeggios, viol drone, recorder melody, tower bells. D minor, 72 BPM."""
    song = Song(bpm=72, bars=16, seed=7)
    rng = song.rng
    chords = ['Dm', 'Dm', 'Bb', 'Bb', 'Gm', 'Gm', 'A', 'A', 'Dm', 'Dm', 'F', 'C', 'Bb', 'Gm', 'A', 'A']
    arps = {
        'Dm': [50, 57, 62, 65, 69, 65, 62, 57], 'Bb': [46, 53, 58, 62, 65, 62, 58, 53],
        'Gm': [43, 50, 55, 58, 62, 58, 55, 50], 'A': [45, 52, 57, 61, 64, 61, 57, 52],
        'F': [41, 48, 53, 57, 60, 57, 53, 48], 'C': [48, 55, 60, 64, 67, 64, 60, 55],
    }
    bass = {'Dm': 38, 'Bb': 34, 'Gm': 43, 'A': 45, 'F': 41, 'C': 36}

    for bar, chord in enumerate(chords):
        for k, m in enumerate(arps[chord]):
            accent = 1.0 if k == 0 else 0.72
            s = pluck(mtof(m), 2.4, rng, bright=0.5, damp=0.9988)
            song.mix.add(song.t(bar, k * 0.5), s, song.vel(0.30 * accent), -0.35, 0.45)

    # Bowed viol holds the root under each chord change.
    bar = 0
    while bar < len(chords):
        span = 2 if bar + 1 < len(chords) and chords[bar + 1] == chords[bar] else 1
        dur = span * 4 * song.beat + 0.4
        s = sustained(mtof(bass[chords[bar]]), dur, rng, GAMBA, attack=0.45, release=0.7,
                      vib_rate=4.5, vib_depth=0.003, noise=0.03)
        song.mix.add(song.t(bar, 0), s, 0.20, 0.05, 0.35)
        bar += span

    for bar, midi in ((0, 62), (4, 67), (8, 62), (12, 58)):
        song.mix.add(song.t(bar, 0), bell(mtof(midi), 7.0, rng), 0.13, 0.2, 0.6)

    # Soft heartbeat drum once the melody arrives.
    for bar in range(8, 16):
        song.mix.add(song.t(bar, 0), tom(70, rng, decay=0.25, noise=0.15), 0.16, 0.0, 0.3)

    melody = [
        (4, 0, 1.5, 74), (4, 1.5, 0.5, 72), (4, 2, 1, 70), (4, 3, 1, 69),
        (5, 0, 2, 67), (5, 2, 1, 69), (5, 3, 1, 70),
        (6, 0, 3, 69), (6, 3, 0.5, 67), (6, 3.5, 0.5, 64),
        (7, 0, 2, 73), (7, 2, 2, 69),
        (8, 0, 1.5, 74), (8, 1.5, 0.5, 76), (8, 2, 1, 77), (8, 3, 1, 74),
        (9, 0, 3, 81), (9, 3, 1, 77),
        (10, 0, 1, 77), (10, 1, 1, 76), (10, 2, 2, 72),
        (11, 0, 1.5, 76), (11, 1.5, 0.5, 74), (11, 2, 2, 72),
        (12, 0, 1, 74), (12, 1, 1, 72), (12, 2, 1, 70), (12, 3, 1, 74),
        (13, 0, 2, 79), (13, 2, 1, 77), (13, 3, 1, 74),
        (14, 0, 2, 76), (14, 2, 1, 73), (14, 3, 1, 76),
        (15, 0, 4, 69),
    ]
    play_melody(song, melody, 0,
                lambda f, d: sustained(f, d, rng, RECORDER, attack=0.07, release=0.14,
                                       vib_rate=5.2, vib_depth=0.006, vib_delay=0.25, noise=0.05),
                gain=0.26, pan=0.25, rev=0.5)
    return song.mix.render(room=0.88, damp=0.28, wet=0.13)


def guard_battle():
    """Driving medieval skirmish: galloping lute, frame drums, recorder and fiddle. E minor, 144 BPM."""
    song = Song(bpm=144, bars=32, seed=21)
    rng = song.rng
    cycle = ['Em', 'Em', 'C', 'D', 'Em', 'Em', 'C', 'B']
    root = {'Em': 52, 'C': 48, 'D': 50, 'B': 47}
    third = {'Em': 3, 'C': 4, 'D': 4, 'B': 4}
    bass = {'Em': 40, 'C': 36, 'D': 38, 'B': 35}

    for bar in range(32):
        chord = cycle[bar % 8]
        r = root[chord]
        pattern = [0, 0, 7, 0, 12, 0, 7, third[chord] + 12]
        for k, iv in enumerate(pattern):
            accent = 1.0 if k in (0, 4) else 0.7
            s = pluck(mtof(r + iv), 0.5, rng, bright=0.68, damp=0.995)
            song.mix.add(song.t(bar, k * 0.5, 0.004), s, song.vel(0.24 * accent), -0.3, 0.22)
        # Marcato bowed bass on every beat.
        for beat in range(4):
            s = sustained(mtof(bass[chord]), song.beat * 0.85, rng, GAMBA, attack=0.02, release=0.08,
                          vib_depth=0.0, noise=0.04)
            song.mix.add(song.t(bar, beat, 0.003), s, song.vel(0.2 if beat == 0 else 0.15), 0.1, 0.18)
        # Frame drums and tambourine.
        if bar % 8 == 7:
            song.mix.add(song.t(bar, 0), tom(85, rng), 0.42, -0.1, 0.2)
            song.mix.add(song.t(bar, 1), tom(150, rng), 0.3, 0.15, 0.2)
            for k in range(8):   # sixteenth-note fill, high to low
                f = 190 - k * 14
                song.mix.add(song.t(bar, 2 + k * 0.25, 0.003), tom(f, rng, decay=0.12), song.vel(0.22 + k * 0.02), 0.2 - k * 0.05, 0.2)
        else:
            for beat in (0, 1.5, 2):
                song.mix.add(song.t(bar, beat, 0.004), tom(85, rng), song.vel(0.42), -0.1, 0.2)
            for beat in (1, 3):
                song.mix.add(song.t(bar, beat, 0.004), tom(150, rng, noise=0.5), song.vel(0.3), 0.15, 0.2)
        for beat in (0.5, 1.5, 2.5, 3.5):
            song.mix.add(song.t(bar, beat, 0.005), tambourine(rng), song.vel(0.11, 0.2), 0.35, 0.15)

    theme_a = [
        (0, 0, 1, 76), (0, 1, 1, 71), (0, 2, 1.5, 74), (0, 3.5, 0.5, 72),
        (1, 0, 1, 71), (1, 1, 1, 67), (1, 2, 2, 71),
        (2, 0, 1, 72), (2, 1, 1, 76), (2, 2, 1, 79), (2, 3, 1, 76),
        (3, 0, 1.5, 78), (3, 1.5, 0.5, 76), (3, 2, 2, 74),
        (4, 0, 1, 76), (4, 1, 0.5, 79), (4, 1.5, 0.5, 78), (4, 2, 1, 76), (4, 3, 1, 71),
        (5, 0, 2, 74), (5, 2, 1, 72), (5, 3, 1, 71),
        (6, 0, 1, 72), (6, 1, 1, 71), (6, 2, 1, 69), (6, 3, 1, 72),
        (7, 0, 2, 71), (7, 2, 1, 75), (7, 3, 1, 78),
    ]
    theme_b = [
        (0, 0, 2, 83), (0, 2, 1, 81), (0, 3, 1, 79),
        (1, 0, 1, 78), (1, 1, 1, 79), (1, 2, 2, 76),
        (2, 0, 1.5, 79), (2, 1.5, 0.5, 81), (2, 2, 1, 79), (2, 3, 1, 76),
        (3, 0, 2, 78), (3, 2, 2, 74),
        (4, 0, 1, 76), (4, 1, 1, 79), (4, 2, 1, 83), (4, 3, 1, 81),
        (5, 0, 1, 79), (5, 1, 1, 78), (5, 2, 2, 76),
        (6, 0, 1, 76), (6, 1, 1, 79), (6, 2, 1, 72), (6, 3, 1, 76),
        (7, 0, 3, 75), (7, 3, 1, 78),
    ]
    recorder = lambda f, d: sustained(f, d, rng, RECORDER, attack=0.03, release=0.08,
                                      vib_rate=5.5, vib_depth=0.004, vib_delay=0.18, noise=0.05)
    fiddle = lambda f, d: sustained(f, d, rng, FIDDLE, attack=0.05, release=0.1,
                                    vib_rate=5.8, vib_depth=0.006, vib_delay=0.15, noise=0.03)
    play_melody(song, theme_a, 8, recorder, 0.27, 0.2, 0.3)
    play_melody(song, theme_b, 16, recorder, 0.25, 0.2, 0.3)
    play_melody(song, theme_b, 16, fiddle, 0.13, -0.2, 0.3, transpose=-12)
    play_melody(song, theme_a, 24, recorder, 0.27, 0.2, 0.3)
    play_melody(song, theme_a, 24, fiddle, 0.16, -0.2, 0.3, transpose=-12)
    return song.mix.render(room=0.8, damp=0.35, wet=0.09)


def boss_battle():
    """Dark and relentless: harpsichord, timpani, gamba ostinato, oboe and tolling bells. C harmonic minor, 126 BPM."""
    song = Song(bpm=126, bars=32, seed=66)
    rng = song.rng
    cycle = ['Cm', 'Cm', 'Ab', 'G', 'Cm', 'Fm', 'Ab', 'G']
    bass = {'Cm': 36, 'Ab': 32, 'G': 31, 'Fm': 41}
    bass_pattern = {'Cm': [0, 0, 7, 0, 8, 7, 0, 7], 'G': [0, 0, 7, 0, 8, 7, 0, 7],
                    'Fm': [0, 0, 7, 0, 8, 7, 0, 7], 'Ab': [0, 0, 7, 0, 5, 7, 0, 7]}
    arps = {'Cm': [60, 67, 72, 75, 72, 67, 63, 67], 'Ab': [56, 63, 68, 72, 68, 63, 60, 63],
            'G': [55, 62, 67, 71, 67, 62, 59, 62], 'Fm': [53, 60, 65, 68, 65, 60, 56, 60]}
    timp_root = {'Cm': 48, 'Ab': 44, 'G': 43, 'Fm': 41}

    for bar in range(32):
        chord = cycle[bar % 8]
        for k, iv in enumerate(bass_pattern[chord]):
            s = sustained(mtof(bass[chord] + iv), song.beat * 0.45, rng, GAMBA, attack=0.012,
                          release=0.05, vib_depth=0.0, noise=0.05)
            song.mix.add(song.t(bar, k * 0.5, 0.003), s, song.vel(0.22 if k % 2 == 0 else 0.17), 0.0, 0.18)
        for k, m in enumerate(arps[chord]):
            s = pluck(mtof(m), 0.6, rng, bright=0.92, damp=0.9965)
            song.mix.add(song.t(bar, k * 0.5, 0.003), s, song.vel(0.15 if k else 0.19), -0.35, 0.28)
        # Timpani on 1 and 3, roll into every eighth bar.
        tr = timp_root[chord]
        if bar % 8 == 7:
            song.mix.add(song.t(bar, 0), timpani(mtof(tr), rng), 0.34, 0.1, 0.3)
            for k in range(12):
                song.mix.add(song.t(bar, 1 + k * 0.25, 0.002), timpani(mtof(tr), rng, dur=0.6),
                             0.1 + k * 0.018, 0.1, 0.3)
        else:
            song.mix.add(song.t(bar, 0), timpani(mtof(tr), rng), 0.34, 0.1, 0.3)
            song.mix.add(song.t(bar, 2), timpani(mtof(tr + 7), rng), 0.26, 0.1, 0.3)
            for beat in (1.5, 3.5):
                song.mix.add(song.t(bar, beat, 0.004), tom(95, rng, decay=0.14), song.vel(0.2), -0.15, 0.2)
            if bar >= 16:   # intensity up in the second half
                for beat in (1, 3):
                    song.mix.add(song.t(bar, beat, 0.004), tom(190, rng, decay=0.1, noise=0.8), song.vel(0.2), 0.2, 0.2)
                for beat in (0.5, 1.5, 2.5, 3.5):
                    song.mix.add(song.t(bar, beat, 0.005), tambourine(rng), song.vel(0.08, 0.2), 0.35, 0.15)
        if bar % 4 == 0:
            song.mix.add(song.t(bar, 0), boom(rng), 0.42, 0.0, 0.35)
            song.mix.add(song.t(bar, 0), bell(mtof(60 if bar % 8 == 0 else 59), 6.0, rng), 0.12, 0.25, 0.55)

    theme = [
        (0, 0, 2, 72), (0, 2, 1, 75), (0, 3, 1, 74),
        (1, 0, 1, 72), (1, 1, 1, 71), (1, 2, 2, 67),
        (2, 0, 1, 68), (2, 1, 1, 72), (2, 2, 1, 75), (2, 3, 1, 80),
        (3, 0, 3, 79), (3, 3, 1, 77),
        (4, 0, 1, 75), (4, 1, 1, 74), (4, 2, 1, 75), (4, 3, 1, 79),
        (5, 0, 2, 80), (5, 2, 1, 77), (5, 3, 1, 72),
        (6, 0, 1, 75), (6, 1, 1, 74), (6, 2, 1, 72), (6, 3, 1, 71),
        (7, 0, 2, 71), (7, 2, 2, 74),
    ]
    oboe = lambda f, d: sustained(f, d, rng, OBOE, attack=0.04, release=0.08,
                                  vib_rate=5.5, vib_depth=0.005, vib_delay=0.2, noise=0.02)
    viola = lambda f, d: sustained(f, d, rng, FIDDLE, attack=0.08, release=0.15,
                                   vib_rate=5.0, vib_depth=0.006, vib_delay=0.2, noise=0.04)
    play_melody(song, theme, 8, oboe, 0.22, 0.2, 0.32)
    play_melody(song, theme, 16, viola, 0.22, -0.1, 0.35, transpose=-12)
    play_melody(song, theme, 24, oboe, 0.22, 0.2, 0.32)
    play_melody(song, theme, 24, viola, 0.13, -0.15, 0.35, transpose=-12)
    return song.mix.render(room=0.85, damp=0.3, wet=0.11)


TRACKS = {
    'Music_TowerInterior.wav': tower_interior,
    'Music_GuardBattle.wav': guard_battle,
    'Music_BossBattle.wav': boss_battle,
}


def main():
    here = os.path.dirname(os.path.abspath(__file__))
    out_dir = sys.argv[1] if len(sys.argv) > 1 else os.path.join(here, '..', '..', 'Assets', 'Audio', 'Music')
    os.makedirs(out_dir, exist_ok=True)
    only = set(sys.argv[2:])
    for name, build in TRACKS.items():
        if only and name not in only:
            continue
        start = time.time()
        L, R = build()
        path = os.path.join(out_dir, name)
        write_wav(path, L, R)
        rms = math.sqrt(sum(x * x for x in L) / len(L))
        print(f'{name}: {len(L) / SR:.1f}s, rms {rms:.3f}, rendered in {time.time() - start:.0f}s', flush=True)


if __name__ == '__main__':
    main()
