"""
Generador de efectos de sonido y música de "Sombras de la Frontera" (Nivel 1).
Todo se sintetiza por código (sin samples externos), excepto los gritos del coyote y las quejas de Mateo,
que parten de voces de texto a voz de Windows (es-MX) y aquí se procesan para sonar a exterior.

Uso:  python generar_sonidos.py <carpeta_voces_tts> <carpeta Assets/Audio>
Salida: Assets/Audio/SFX/*.wav y Assets/Audio/BGM/*.wav
"""
import os
import sys
import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 44100
rng = np.random.default_rng(31)


# ------------------------------------------------------------------ utilidades
def t_axis(dur):
    return np.arange(int(SR * dur)) / SR


def env_ad(n, attack, decay_pow=3.0):
    a = max(1, int(SR * attack))
    e = np.ones(n)
    e[:a] = np.linspace(0, 1, a)
    if n > a:
        e[a:] = np.linspace(1, 0, n - a) ** decay_pow
    return e


def bandpass(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, btype="low", fs=SR, output="sos"), x)


def highpass(x, f, order=2):
    return signal.sosfilt(signal.butter(order, f, btype="high", fs=SR, output="sos"), x)


def noise(n):
    return rng.uniform(-1, 1, n)


def normalize(x, peak=0.89):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def fade(x, fin=0.003, fout=0.01):
    a, b = int(SR * fin), int(SR * fout)
    if a: x[:a] *= np.linspace(0, 1, a)
    if b: x[-b:] *= np.linspace(1, 0, b)
    return x


def reverb(x, room=0.25, wet=0.18):
    """Reverb sencilla de exterior (ecos cortos difusos)."""
    n = int(SR * (0.35 + room))
    ir = noise(n) * np.exp(-np.linspace(0, 7, n))
    ir = lowpass(ir, 3500)
    ir /= np.sum(np.abs(ir)) + 1e-9
    w = signal.fftconvolve(x, ir)[: len(x) + n // 2]
    dry = np.concatenate([x, np.zeros(len(w) - len(x))])
    return dry * (1 - wet) + normalize(w, np.max(np.abs(x))) * wet


def save(folder, name, x, peak=0.89):
    x = normalize(fade(np.asarray(x, float)), peak)
    os.makedirs(folder, exist_ok=True)
    wavfile.write(os.path.join(folder, name + ".wav"), SR, (x * 32767).astype(np.int16))


def karplus(freq, dur, decay=0.996, bright=0.5):
    """Cuerda pulsada (guitarra) Karplus-Strong."""
    n = int(SR * dur)
    p = max(2, int(SR / freq))
    buf = lowpass(noise(p), 2000 + 6000 * bright) if p > 20 else noise(p)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % p]
        buf[i % p] = decay * 0.5 * (buf[i % p] + buf[(i + 1) % p])
    return out


def midi(m):
    return 440.0 * 2 ** ((m - 69) / 12)


# ------------------------------------------------------------------ pasos
def footstep(heavy=False):
    dur = 0.16 if not heavy else 0.2
    n = int(SR * dur)
    crunch = noise(n) * (rng.uniform(0, 1, n) > (0.55 if not heavy else 0.45))
    body = bandpass(noise(n), 250 if heavy else 400, 1800 if heavy else 3200)
    grains = bandpass(crunch, 1500, 6000) * 0.5
    thump = np.sin(2 * np.pi * (70 if heavy else 110) * t_axis(dur)) * env_ad(n, 0.002, 6) * (0.9 if heavy else 0.4)
    x = (body + grains) * env_ad(n, 0.006, 2.5) + thump
    return lowpass(x, 2600 if heavy else 4200)


# ------------------------------------------------------------------ perros
def resonator(x, freqs, bws, gains):
    """Filtro de formantes variable en el tiempo (resonadores de 2 polos en paralelo).
    freqs/bws son arreglos (uno por formante) con un valor por muestra."""
    out = np.zeros(len(x))
    for f, bw, g in zip(freqs, bws, gains):
        r = np.exp(-np.pi * bw / SR)
        a1 = -2 * r * np.cos(2 * np.pi * f / SR)
        a2 = r * r
        y1 = y2 = 0.0
        y = np.zeros(len(x))
        for i in range(len(x)):
            y0 = (1 - r) * x[i] - a1[i] * y1 - a2 * y2
            y[i] = y0
            y2, y1 = y1, y0
        out += y * g
    return out


def bark(f0=300, dur=0.17, size=1.0):
    """Ladrido "¡guau!": pulsos glotales ásperos con un tono que sube y cae rápido,
    formantes de vocal abierta que se abren y cierran (wu-AU-f) y un golpe de aire al inicio."""
    t = t_axis(dur)
    n = len(t)
    u = t / dur
    # Tono: sube de golpe (~35 ms) y cae al final, con jitter de una garganta real
    contour = 0.8 + 0.75 * np.exp(-((u - 0.22) / 0.2) ** 2) - 0.3 * u
    f = f0 * contour * (1 + 0.04 * lowpass(noise(n), 60) * 8)
    ph = np.cumsum(f) / SR
    pulses = np.exp(-((ph % 1.0) * 9)) - 0.12          # pulsos glotales con caída rápida
    sub = 1 + 0.55 * np.sign(np.sin(np.pi * ph))       # subarmónico = aspereza/gruñido
    src = pulses * sub + 0.35 * noise(n)
    # Formantes: la boca se abre (F1 sube) y se cierra al final
    openness = np.sin(np.pi * np.clip(u * 1.15, 0, 1)) ** 0.7
    F1 = (420 + 420 * openness) / size
    F2 = (1000 + 380 * openness) / size
    F3 = np.full(n, 2500.0 / size)
    x = resonator(src, [F1, F2, F3], [130, 180, 320], [1.0, 0.7, 0.3])
    env = env_ad(n, 0.006, 1.4) * (0.6 + 0.4 * openness)
    x = x * env
    # Golpe de aire inicial (la "b"/"w" que explota) y pecho
    m = int(SR * 0.03)
    puff = bandpass(noise(m), 300, 2200) * env_ad(m, 0.001, 3) * 0.5
    x[:m] += puff * np.max(np.abs(x))
    chest = np.sin(2 * np.pi * (95 / size) * t) * env_ad(n, 0.004, 5) * 0.35 * np.max(np.abs(x))
    x = np.tanh((x + chest) * 2.5 / (np.max(np.abs(x)) + 1e-9))
    return highpass(lowpass(x, 5000), 120)


def dog_bark(variant):
    if variant == 0:
        x = bark(300, 0.17, 1.0)                      # "¡guau!" de perro mediano
    elif variant == 1:
        x = np.concatenate([bark(320, 0.13, 1.0), np.zeros(int(SR * 0.09)), bark(290, 0.16, 1.0)])  # "¡guau-guau!"
    else:
        x = bark(230, 0.2, 1.25)                      # perro grande, más grave
    return reverb(x, 0.25, 0.14)


def growl(dur=0.9):
    t = t_axis(dur)
    n = len(t)
    f = 95 + 15 * np.sin(2 * np.pi * 3 * t)
    ph = 2 * np.pi * np.cumsum(f) / SR
    am = 0.6 + 0.4 * np.abs(np.sin(2 * np.pi * 21 * t + noise(n) * 0.3))
    src = (signal.sawtooth(ph) + 0.6 * noise(n)) * am
    x = bandpass(src, 80, 900) + 0.3 * bandpass(src, 900, 2200)
    e = np.minimum(1, t / 0.08) * np.minimum(1, (dur - t) / 0.2)
    return x * e


def dog_bite():
    snap_n = int(SR * 0.05)
    snap = highpass(noise(snap_n), 1500) * env_ad(snap_n, 0.0005, 5)
    clack = np.sin(2 * np.pi * 900 * t_axis(0.04)) * env_ad(int(SR * 0.04), 0.0005, 6)
    g = growl(0.55) * 0.7
    x = np.zeros(int(SR * 0.65))
    x[: len(g)] += g
    x[: snap_n] += snap * 1.2
    x[int(SR * 0.01): int(SR * 0.01) + len(clack)] += clack
    return x


def dog_snore():
    dur = 1.8
    t = t_axis(dur)
    n = len(t)
    breath_in = np.clip(np.sin(np.pi * t / 0.9), 0, None) * (t < 0.9)
    breath_out = np.clip(np.sin(np.pi * (t - 0.95) / 0.8), 0, None) * (t >= 0.95)
    rumble = bandpass(noise(n) * (0.6 + 0.4 * np.sin(2 * np.pi * 28 * t)), 70, 500)
    air = bandpass(noise(n), 600, 2500) * 0.35
    return rumble * breath_in + air * breath_out


# ------------------------------------------------------------------ fauna
def rattle(dur=1.1):
    t = t_axis(dur)
    n = len(t)
    rate = 48 + 6 * np.sin(2 * np.pi * 1.3 * t)
    phase = np.cumsum(rate) / SR
    clicks = (np.diff(np.floor(phase), prepend=0) > 0).astype(float)
    burst = signal.lfilter([1], [1, -0.93], clicks)  # cada clic se alarga un poco
    x = bandpass(noise(n), 3500, 9000) * np.clip(burst, 0, 1) * 1.5 + bandpass(noise(n), 5000, 11000) * 0.12
    e = np.minimum(1, t / 0.08) * np.minimum(1, (dur - t) / 0.15)
    return x * e


def hiss(dur=0.7, lo=2800):
    n = int(SR * dur)
    x = highpass(noise(n), lo, 3) + 0.3 * bandpass(noise(n), 1500, 3000)
    t = t_axis(dur)
    return x * np.minimum(1, t / 0.05) * np.exp(-np.maximum(0, t - 0.15) * 4)


def clicks(count, dur, lo=3000, hi=9000, jitter=0.4):
    n = int(SR * dur)
    x = np.zeros(n)
    times = np.sort(rng.uniform(0, dur * 0.92, count))
    for tt in times:
        i = int(tt * SR)
        cl = int(SR * rng.uniform(0.003, 0.008))
        if i + cl < n:
            x[i:i + cl] += bandpass(noise(cl), lo, hi) * env_ad(cl, 0.0003, 4) * rng.uniform(1 - jitter, 1)
    return x


def scorpion_sting():
    whoosh_n = int(SR * 0.22)
    t = t_axis(0.22)
    sweep = bandpass(noise(whoosh_n), 700, 4000) * np.sin(np.pi * t / 0.22) ** 2
    x = np.zeros(int(SR * 0.35))
    x[:whoosh_n] += sweep * 0.6
    x[int(SR * 0.2): int(SR * 0.2) + 400] += clicks(1, 400 / SR, 2500, 8000, 0)[:400] * 3
    x += clicks(4, 0.35, 4000, 10000) * 0.6
    return x


def spider_hiss():
    x = hiss(0.45, 3500) * 0.8
    c = clicks(9, 0.45, 2500, 7000)
    return x + c * 1.2


# ------------------------------------------------------------------ jugador / mundo
def quicksand():
    dur = 1.0
    t = t_axis(dur)
    n = len(t)
    glug = np.zeros(n)
    for start in (0.05, 0.32, 0.6):
        i = int(start * SR)
        m = int(0.22 * SR)
        f = np.linspace(260, 120, m)
        glug[i:i + m] += np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(m, 0.01, 2)
    mud = lowpass(noise(n), 500) * (0.5 + 0.5 * np.sin(2 * np.pi * 3 * t))
    return mud * 0.9 + glug * 0.5


def drink():
    """Un solo trago suave: burbuja corta de agua + el "glup" de la garganta."""
    dur = 0.32
    t = t_axis(dur)
    n = len(t)
    x = np.zeros(n)
    # Burbuja/chorrito al entrar el agua
    m = int(0.06 * SR)
    f = np.linspace(380, 760, m)
    x[:m] += np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(m, 0.004, 2) * 0.5
    # "Glup": golpe grave y redondo de la garganta
    i = int(0.07 * SR)
    m = int(0.16 * SR)
    f = np.linspace(210, 120, m)
    x[i:i + m] += np.sin(2 * np.pi * np.cumsum(f) / SR) * env_ad(m, 0.008, 3)
    x[i:i + m] += lowpass(noise(m), 700) * env_ad(m, 0.003, 5) * 0.25
    return lowpass(x, 1800)


def alert_sting():
    dur = 1.1
    t = t_axis(dur)
    n = len(t)
    chord = [midi(57), midi(58), midi(64), midi(69)]  # La, Si bemol, Mi, La (disonante)
    x = sum(signal.sawtooth(2 * np.pi * f * t * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))) for f in chord)
    x = lowpass(x, 1800)
    hit = lowpass(noise(n), 200) * env_ad(n, 0.002, 8) * 3
    e = np.minimum(1, t / 0.02) * np.exp(-t * 2.2)
    return reverb(x * e + hit, 0.4, 0.3)


def melody(notes, step, dur_note, decay=0.996, strum=0.0):
    total = len(notes) * step + dur_note + 0.5
    out = np.zeros(int(SR * total))
    for i, m in enumerate(notes):
        if m is None: continue
        for j, mm in enumerate(m if isinstance(m, (list, tuple)) else [m]):
            s = karplus(midi(mm), dur_note, decay)
            k = int(SR * (i * step + j * strum))
            out[k:k + len(s)] += s
    return out


def game_over():
    x = melody([69, 67, 64, 60, 57, [45, 52, 57]], 0.28, 1.6, 0.997, 0.03)
    t = t_axis(len(x) / SR)
    drone = np.sin(2 * np.pi * midi(33) * t) * np.minimum(1, t / 0.5) * np.exp(-t * 0.6) * 0.3
    return reverb(x + drone, 0.5, 0.3)


def victory():
    x = melody([57, 61, 64, 69, 64, 69, 73, [57, 64, 69, 73, 76]], 0.16, 1.6, 0.997, 0.025)
    return reverb(x, 0.5, 0.25)


# ------------------------------------------------------------------ música
def loop_seamless(x, xfade=2.0):
    n = int(SR * xfade)
    head, tail = x[:n], x[-n:]
    ramp = np.linspace(0, 1, n)
    body = x[n:-n].copy()
    mixed = tail * (1 - ramp) + head * ramp
    return np.concatenate([mixed, body])


def desert_night(dur=48.0):
    t = t_axis(dur)
    n = len(t)
    wind_amp = 0.5 + 0.3 * np.sin(2 * np.pi * t / 11) + 0.2 * np.sin(2 * np.pi * t / 4.3 + 1)
    wind = bandpass(noise(n), 150, 900) * wind_amp * 0.6 + bandpass(noise(n), 900, 2500) * wind_amp ** 2 * 0.12
    # Grillos: pulsos a 4.6 kHz en grupos
    crick = np.zeros(n)
    for c in range(3):
        f = 4400 + 350 * c
        period = 0.9 + 0.35 * c
        gate = ((t + c * 0.37) % period) < 0.18
        trill = (np.sin(2 * np.pi * 32 * t) > 0)
        crick += np.sin(2 * np.pi * f * t) * gate * trill * (0.05 + 0.03 * np.sin(2 * np.pi * t / (7 + c)))
    # Colchón grave en La menor que respira lentamente
    pad = np.zeros(n)
    for m, w in ((45, 0.5), (52, 0.3), (57, 0.25), (60, 0.15)):
        f = midi(m)
        pad += w * np.sin(2 * np.pi * f * t + 0.3 * np.sin(2 * np.pi * 0.07 * t))
    pad = lowpass(pad, 700) * (0.55 + 0.45 * np.sin(2 * np.pi * t / 16) ** 2) * 0.35
    # Guitarra lejana ocasional
    guitar = np.zeros(n)
    phrase = [57, 60, 64, 62, 60, 57]
    for start in (6.0, 22.0, 36.0):
        g = melody(phrase, 0.55, 2.5, 0.995)
        k = int(start * SR)
        seg = g[: max(0, min(len(g), n - k))]
        guitar[k:k + len(seg)] += seg * 0.35
    guitar = reverb(lowpass(guitar, 2500), 0.8, 0.5)[:n]
    mix = wind + crick + pad + guitar
    return loop_seamless(mix)


def menu_theme(bars=8):
    bpm = 84
    beat = 60 / bpm
    # Am - G - F - E (cadencia andaluza), arpegio de guitarra
    chords = [[45, 52, 57, 60, 64], [43, 50, 55, 59, 62], [41, 48, 53, 57, 60], [40, 47, 52, 56, 59]]
    pattern = [0, 2, 3, 4, 3, 2, 1, 3]
    total = bars * 4 * beat + 3
    out = np.zeros(int(SR * total))
    for b in range(bars):
        ch = chords[b % 4]
        for i, p in enumerate(pattern):
            s = karplus(midi(ch[p]), 1.8, 0.996, 0.45)
            k = int(SR * (b * 4 * beat + i * beat / 2))
            out[k:k + len(s)] += s * (1.0 if i == 0 else 0.7)
    t = t_axis(total)
    wind = bandpass(noise(len(t)), 150, 800) * (0.5 + 0.3 * np.sin(2 * np.pi * t / 9)) * 0.25
    mix = reverb(out, 0.7, 0.35)[: len(t)] + wind
    return loop_seamless(mix[: int(SR * bars * 4 * beat) + int(SR * 2.0)], 2.0)


# ------------------------------------------------------------------ voces
def load_voice(path):
    sr, x = wavfile.read(path)
    x = x.astype(float)
    if x.ndim > 1: x = x.mean(axis=1)
    x /= 32768.0
    # Recortar silencios
    idx = np.nonzero(np.abs(x) > 0.02)[0]
    if len(idx): x = x[max(0, idx[0] - int(0.02 * sr)): idx[-1] + int(0.05 * sr)]
    return signal.resample_poly(x, SR, sr)


def shout(x, pitch=0.94, drive=2.2, distance=0.25):
    """Voz de exterior: un poco más grave, saturada (gritando) y con eco del desierto."""
    if pitch != 1.0:
        x = signal.resample(x, int(len(x) / pitch))  # baja el tono y alarga un poco
    x = highpass(x, 140)
    x = bandpass(x, 180, 4200) * 0.8 + x * 0.4  # presencia de grito
    x = np.tanh(x * drive) / np.tanh(drive)
    return reverb(x, distance, 0.22)


def main():
    tts_dir, audio_dir = sys.argv[1], sys.argv[2]
    sfx = os.path.join(audio_dir, "SFX")
    bgm = os.path.join(audio_dir, "BGM")

    for i in range(4): save(sfx, f"Footstep_Sand_{i + 1}", footstep(False), 0.6)
    for i in range(3): save(sfx, f"Coyote_Step_{i + 1}", footstep(True), 0.65)
    for i in range(3): save(sfx, f"Dog_Bark_{i + 1}", dog_bark(i))
    save(sfx, "Dog_Bite", dog_bite())
    save(sfx, "Dog_Snore", dog_snore(), 0.5)
    save(sfx, "Snake_Rattle", rattle(), 0.7)
    save(sfx, "Snake_Hiss", hiss(), 0.75)
    save(sfx, "Scorpion_Click", clicks(9, 0.45), 0.6)
    save(sfx, "Scorpion_Sting", scorpion_sting(), 0.75)
    save(sfx, "Spider_Skitter", clicks(14, 0.5, 2000, 6000), 0.45)
    save(sfx, "Spider_Hiss", spider_hiss(), 0.75)
    save(sfx, "Quicksand_Sink", quicksand(), 0.75)
    save(sfx, "Drink_Water", drink(), 0.75)
    save(sfx, "Alert_Sting", alert_sting(), 0.8)
    save(sfx, "Game_Over", game_over(), 0.8)
    save(sfx, "Victory", victory(), 0.8)

    # Gritos del coyote (voz Raúl es-MX) y quejas de Mateo (voz más aguda)
    for name, pitch, drive in (("suspect_1", 0.95, 1.4), ("suspect_2", 0.95, 1.4),
                               ("spot_1", 0.93, 2.6), ("spot_2", 0.93, 2.6), ("spot_3", 0.93, 2.6),
                               ("catch_1", 0.92, 3.0), ("catch_2", 0.92, 3.0)):
        p = os.path.join(tts_dir, name + ".wav")
        if os.path.exists(p):
            save(sfx, "Coyote_" + name.capitalize(), shout(load_voice(p), pitch, drive), 0.9)
    for name in ("hurt_1", "hurt_2"):
        p = os.path.join(tts_dir, name + ".wav")
        if os.path.exists(p):
            save(sfx, "Mateo_" + name.capitalize(), shout(load_voice(p), 1.12, 1.6, 0.1), 0.85)

    save(bgm, "Desert_Night_Loop", desert_night(), 0.6)
    save(bgm, "Menu_Theme_Loop", menu_theme(), 0.65)
    print("Sonidos generados en", audio_dir)


if __name__ == "__main__":
    main()
