# Synthesizes every horror sound into Assets/MAIN GAME/Sounds/Horror (free, no external services).
# blender -b --python Tools/horror_sfx.py   (Blender's Python ships with numpy)
import os, wave
import numpy as np

SR = 22050
OUT = r"D:\Goutam\UnityProjects\Horror project\Assets\MAIN GAME\Sounds\Horror"
os.makedirs(OUT, exist_ok=True)
rng = np.random.default_rng(13)


def T(d): return np.arange(int(d * SR)) / SR
def noise(d): return rng.standard_normal(int(d * SR))


def curve(d, rate, lo=0.0, hi=1.0):
    """Smooth random control signal."""
    n = int(d * rate) + 2
    v = rng.uniform(lo, hi, n)
    return np.interp(T(d), np.arange(n) / rate, v)


def band(x, lo, hi):
    f = np.fft.rfftfreq(len(x), 1 / SR)
    g = np.clip((f - lo * .7) / (lo * .3 + 1), 0, 1) * np.clip((hi * 1.3 - f) / (hi * .3), 0, 1)
    return np.fft.irfft(np.fft.rfft(x) * g, len(x))


def shape(x, gain):
    """Time-varying EQ: overlap-add with gain(seconds, freqs) -> per-bin gain."""
    N, hop = 1024, 256
    win = np.hanning(N)
    f = np.fft.rfftfreq(N, 1 / SR)
    pad = np.concatenate([x, np.zeros(N)])
    out = np.zeros(len(pad))
    for i in range(0, len(x), hop):
        out[i:i + N] += np.fft.irfft(np.fft.rfft(pad[i:i + N] * win) * gain(i / SR, f), N) * win
    return out[:len(x)] / 1.5


def peaks(f, formants, bw=120.0):
    return sum(a * np.exp(-((f - c) / bw) ** 2) for c, a in formants)


def reverb(x, secs, wet, tail=True):
    ir = noise(secs) * np.exp(-6.9 * T(secs) / secs)
    ir = band(ir, 120, 5000)
    ir /= np.sqrt((ir ** 2).sum()) + 1e-9
    y = np.concatenate([x, np.zeros(len(ir))])
    w = np.fft.irfft(np.fft.rfft(y) * np.fft.rfft(ir, len(y)), len(y))
    y = y * (1 - wet) + w * wet
    return y if tail else y[:len(x)]


def loop(x, xf=2.0):
    """Crossfade the tail into the head so the clip repeats without a click."""
    n = int(xf * SR)
    r = np.linspace(0, 1, n)
    x = x.copy()
    x[:n] = x[:n] * np.sqrt(r) + x[-n:] * np.sqrt(1 - r)
    return x[:-n]


def env(d, attack, decay):
    t = T(d)
    return np.clip(t / max(attack, 1e-4), 0, 1) * np.exp(-t / decay)


def save(name, x, peak=.9, fade=True):
    x = np.nan_to_num(x)
    if fade:
        n = min(int(.01 * SR), len(x) // 4)
        x = x.copy()
        x[-n:] *= np.linspace(1, 0, n)
    p = np.abs(x).max()
    assert p > 1e-6, name + " is silent"
    x = x / p * peak
    with wave.open(os.path.join(OUT, name + ".wav"), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())
    print("SFX", name, round(len(x) / SR, 1), "s rms", round(float(np.sqrt((x ** 2).mean())), 3))


def glottal(f0, rough=0.0):
    """Saw-like voice source from a pitch curve (Hz per sample)."""
    f0 = f0 * (1 + .006 * rng.standard_normal(len(f0)))
    s = 2 * (np.cumsum(f0 / SR) % 1) - 1
    if rough:
        s = s * (1 - rough + rough * np.sign(np.sin(2 * np.pi * np.cumsum(f0 * .5 / SR))))
    return s


A, E, O, OO = [(800, 1), (1200, .7), (2500, .35)], [(500, 1), (1900, .7), (2600, .3)], [(500, 1), (900, .7), (2500, .25)], [(350, 1), (800, .5), (2500, .15)]


def voice(f0, vowel, breath=.15, rough=0.0, bw=130.0, scale=1.0):
    src = glottal(f0, rough) + breath * rng.standard_normal(len(f0))
    return shape(src, lambda t, f: peaks(f, [(c * scale, a) for c, a in vowel], bw) + .02)


def pitch(d, *knots):
    return np.interp(T(d), np.linspace(0, d, len(knots)), knots)


# ---------------------------------------------------------------- ambience
def wind(d=32.0):
    b = noise(d)
    c1, c2, a = curve(d, .25, 250, 700), curve(d, .15, 700, 1400), curve(d, .2, .25, 1)
    gust = curve(d, .08, .3, 1)
    def g(t, f):
        i = min(int(t * SR), len(c1) - 1)
        return peaks(f, [(c1[i], 1), (c2[i], .55 * a[i])], 170) + .06 * np.exp(-f / 300)
    return loop(shape(b, g) * gust)


def drone(d=32.0):
    t = T(d)
    x = sum(np.sin(2 * np.pi * h * t + rng.uniform(0, 6)) * w for h, w in ((55, 1), (55.7, .8), (82.4, .4), (116.5, .22), (117.3, .2)))
    x = x * curve(d, .12, .6, 1) + band(noise(d), 60, 300) * .25 * curve(d, .1, .2, 1)
    return loop(x)


# ---------------------------------------------------------------- world
def whisper(d, distant):
    b = noise(d)
    n, syl, i = int(d * SR), [], 0.0
    while i < d:
        l = rng.uniform(.14, .42)
        syl.append((i, l, [E, A, O, OO][rng.integers(4)], rng.random() < .3))
        i += l + (rng.uniform(.5, 1.1) if rng.random() < .2 else rng.uniform(.02, .1))
    def g(t, f):
        for s, l, v, hiss in syl:
            if s <= t < s + l:
                a = np.sin(np.pi * (t - s) / l)
                return a * (peaks(f, [(c * 1.25, w) for c, w in v] + [(3500, .3)], 300) + (.9 * np.exp(-((f - 6000) / 1500) ** 2) if hiss else 0))
        return np.zeros_like(f)
    x = shape(b, g)
    return reverb(band(x, 300, 3500), 2.5, .7) if distant else reverb(x, .35, .15)


def wail():
    d = 6.0
    f0 = pitch(d, 260, 430, 390, 300, 230) * (1 + .018 * np.sin(2 * np.pi * 5 * T(d)))
    x = voice(f0, OO, .25, bw=110) * np.sin(np.pi * T(d) / d) ** 1.5
    return reverb(band(x, 150, 2200), 3.5, .8)


def creak(d, rate, res):
    r = np.interp(T(d), [0, d * .5, d], rate) * (1 + curve(d, 6, -.45, .45))
    gate = curve(d, 3, 0, 1) > .22
    pulses = np.diff(np.floor(np.cumsum(r / SR)), prepend=0) * gate * rng.uniform(.3, 1, int(d * SR))
    x = shape(pulses + .01 * noise(d), lambda t, f: peaks(f, res, 90))
    return reverb(x, .5, .3)


def slam():
    d = 1.0
    t = T(d)
    x = np.sin(2 * np.pi * 75 * t) * env(d, .002, .13) + band(noise(d), 100, 1800) * env(d, .001, .05) * .8
    for o in (.11, .19):  # frame rattling back into place
        k = int(o * SR)
        x[k:] += (band(noise(d), 600, 2500) * env(d, .001, .02))[:len(x) - k] * .35
    return reverb(band(x, 40, 2400), 2.2, .7)


def debris():
    d = 3.2
    x = np.zeros(int(d * SR))
    at = .05
    while at < d - .4:
        l = int(rng.uniform(.04, .16) * SR)
        h = np.sin(2 * np.pi * rng.uniform(250, 1400) * np.arange(l) / SR) + rng.standard_normal(l) * .9
        k = int(at * SR)
        x[k:k + l] += h * np.exp(-np.arange(l) / (l * .25)) * rng.uniform(.25, 1) * np.exp(-at * .5)
        at += rng.exponential(.11) + .02
    return reverb(band(x, 150, 5000), 1.4, .45)


# ---------------------------------------------------------------- suspense
def swell():
    d = 12.0
    t = T(d)
    r = (t / d) ** 2.2
    x = sum(np.sin(2 * np.pi * h * t + rng.uniform(0, 6)) * w for h, w in ((110, 1), (116.5, .9), (164.8, .6), (233, .4)))
    screech = glottal(pitch(d, 880, 932, 990) * (1 + .012 * np.sin(2 * np.pi * 6.5 * t)))
    x = x * (.15 + r) + band(screech, 900, 4000) * r ** 2 * .22
    fall = np.clip((d - t) / .9, 0, 1)
    return reverb(x * fall, 1.8, .3, tail=False)


def stinger(notes, drop):
    d = 2.2
    t = T(d)
    bend = 1 - drop * np.clip(t / d, 0, 1)
    x = sum(glottal(np.full_like(t, h) * bend) for h in notes) / len(notes)
    x = band(x, 300, 6000) * (1 + .5 * np.sin(2 * np.pi * 17 * t)) * env(d, .004, .6)
    x += np.sin(2 * np.pi * 48 * t) * env(d, .002, .45) * 1.2 + band(noise(d), 200, 6000) * env(d, .001, .06)
    return reverb(x, 1.2, .35)


# ---------------------------------------------------------------- footsteps
def grains(d, count, lo, hi, amp=1.0):
    x = np.zeros(int(d * SR))
    for _ in range(count):
        k = int(abs(rng.normal(.03, .05)) * SR)
        l = int(rng.uniform(.003, .012) * SR)
        if k + l < len(x):
            x[k:k + l] += rng.standard_normal(l) * np.hanning(l) * rng.uniform(.2, 1)
    return band(x, lo, hi) * amp


def thump(d, hz, decay, amp=1.0):
    return np.sin(2 * np.pi * hz * T(d)) * env(d, .002, decay) * amp


def burst(d, start, lo, hi, decay, amp=1.0):
    """Filtered noise hit: a real impact has no steady pitch, unlike a sine thump."""
    x = band(noise(d), lo, hi) * env(d, .0015, decay) * amp
    k = int(start * SR)
    return np.concatenate([np.zeros(k), x[:len(x) - k]])


def crunch(d, start, length, count, lo, hi, amp=1.0):
    """Grains packed tight at contact and thinning out as the weight settles."""
    x = np.zeros(int(d * SR))
    for _ in range(count):
        k = int((start + min(rng.exponential(length * .4), length)) * SR)
        l = int(rng.uniform(.002, .009) * SR)
        if k + l < len(x):
            x[k:k + l] += rng.standard_normal(l) * np.hanning(l) * rng.uniform(.15, 1)
    return band(x, lo, hi) * amp


def shoe(kind):
    """One step = heel strike, then toe rolling down 80-130 ms later."""
    d = .5
    toe = rng.uniform(.08, .13)
    weight = rng.uniform(.85, 1.15)
    if kind == "sand":
        x = burst(d, 0, 60, 320, .035, 1.3 * weight) + crunch(d, 0, .11, 75, 1100, 7000, 1.0)
        x += burst(d, toe, 70, 380, .03, .7) + crunch(d, toe, .15, 60, 1600, 8000, .75)
        return x
    if kind == "stone":
        x = burst(d, 0, 80, 320, .018, 1.1 * weight) + burst(d, 0, 700, 3800, .006, 1.0)
        x += burst(d, toe, 900, 4200, .005, .5) + crunch(d, .005, .1, 14, 2500, 8000, .35)
        return reverb(x, .4, .18, tail=False)  # ruin walls throw the sound back
    x = burst(d, 0, 70, 260, .07, 1.4 * weight) + burst(d, 0, 400, 2200, .012, .55)
    x += burst(d, toe, 90, 320, .045, .6) + burst(d, toe, 500, 2400, .008, .25)
    if rng.random() < .35:  # the odd board gives under the weight
        c = creak(.3, (60, 90, 40), [(420, 1), (1050, .7)]) * .1
        x[:len(c)] += c[:len(x)]
    return reverb(x, .3, .14, tail=False)


def step(kind):
    d = rng.uniform(.28, .36)
    j = rng.uniform(.9, 1.1)
    if kind == "bare_walk": x = thump(d, 78 * j, .04, 1.0) + band(noise(d), 350, 1800) * env(d, .001, .018) * .5 + grains(d, 14, 900, 4000, .25)
    else: x = thump(d, 72 * j, .05, 1.0) + band(noise(d), 300, 2600) * env(d, .001, .03) * .9 + grains(d, 30, 900, 5000, .5)
    return reverb(x, .18, .1, tail=False)


# ---------------------------------------------------------------- chudail
def breathing():
    d, out = 10.0, []
    t = 0.0
    x = np.zeros(int(d * SR))
    for inhale, l, gap in ((True, 1.5, .25), (False, 2.0, .5), (True, 1.3, .2), (False, 2.3, .4), (True, 1.1, .4)):
        n = int(l * SR)
        k = int(t * SR)
        if inhale:
            s = shape(rng.standard_normal(n), lambda tt, f: peaks(f, [(1400 + 900 * tt / l, 1), (2800, .5)], 500))
        else:
            s = shape(rng.standard_normal(n) + 1.2 * glottal(np.full(n, 58.0) * (1 + .1 * np.sin(np.arange(n) / SR * 22)), .6), lambda tt, f: peaks(f, [(450, 1), (1100, .7)], 320))
        x[k:k + n] += s[:len(x) - k] * np.sin(np.pi * np.arange(n) / n) ** 1.4 * (1.1 if inhale else .9)
        t += l + gap
    return loop(reverb(x, .3, .12, tail=False), .3)


def laugh(count, f_hi, f_lo, gap):
    ha = int(gap * SR)
    d = count * gap + .2
    x = np.zeros(int(d * SR))
    for i in range(count):
        l = rng.uniform(.13, .19)
        h = voice(pitch(l, f_hi - (f_hi - f_lo) * i / count, f_lo * (1 - .1 * i / count)), A, .5, .25, scale=1.1)
        a = np.sin(np.pi * np.arange(len(h)) / len(h)) ** .7 * (1 - .05 * i)
        k = i * ha
        x[k:k + len(h)] += h * a
    return reverb(x, .9, .35)


def scream(d, knots, rough, vibrato=.03):
    t = T(d)
    f0 = pitch(d, *knots) * (1 + vibrato * np.sin(2 * np.pi * 7.5 * t) + .008 * curve(d, 30, -1, 1))
    x = voice(f0, A, .35, rough, bw=260, scale=1.5)
    a = np.clip(t / .06, 0, 1) * np.clip((d - t) / (d * .35), 0, 1)
    return reverb(x * a, 1.3, .3)


def chase_layer(d=14.0):
    t = T(d)
    beat = np.zeros(len(t))
    for s in np.arange(0, d, .78):
        for off, a in ((0, 1), (.2, .7)):
            k = int((s + off) * SR)
            b = np.sin(2 * np.pi * 52 * np.arange(int(.18 * SR)) / SR) * np.exp(-np.arange(int(.18 * SR)) / (.05 * SR)) * a
            beat[k:k + len(b)] += b[:max(0, len(beat) - k)]
    tones = sum(glottal(np.full_like(t, h)) for h in (98, 103.8, 146.8, 155.6)) / 4
    return loop(beat + band(tones, 150, 1400) * (1 + .6 * np.sin(2 * np.pi * 11 * t)) * .45, 2.0)


save("amb_wind_loop", wind(), .7, False)
save("amb_drone_loop", drone(), .6, False)
for i in (1, 2): save("world_whisper_%d" % i, whisper(6, True), .6)
save("world_wail", wail(), .6)
save("world_creak_1", creak(4, (18, 55, 25), [(380, 1), (950, .8), (1900, .35)]))
save("world_creak_2", creak(4, (70, 30, 14), [(300, 1), (760, .9), (1500, .4)]))
save("world_door_creak", creak(4, (10, 90, 130), [(620, 1), (1320, .9), (2500, .55)]))
save("world_door_slam", slam())
save("world_debris", debris())
save("sus_swell", swell(), .8)
save("sting_1", stinger((440, 466, 622, 932, 1244), .05))
save("sting_2", stinger((740, 784, 1109, 1568), .35))
for k in ("sand", "stone", "wood"):
    for i in range(1, 7): save("step_%s_%d" % (k, i), shoe(k))
save("chud_breath_loop", breathing(), .7, False)
for i in (1, 2): save("chud_whisper_%d" % i, whisper(6, False), .75)
save("chud_laugh_1", laugh(9, 520, 400, .24))
save("chud_laugh_2", laugh(6, 230, 160, .3))
save("chud_scream", scream(4, (620, 1050, 1250, 980, 700), .3))
save("chud_detect", scream(3, (800, 1350, 1100, 560), .45))
save("chud_chase_loop", chase_layer(), .7, False)
for i in (1, 2, 3): save("chud_walk_%d" % i, step("bare_walk"))
for i in (1, 2, 3): save("chud_run_%d" % i, step("bare_run"))
save("chud_attack", scream(2.6, (420, 1300, 1500, 1050), .6, .05), .98)
