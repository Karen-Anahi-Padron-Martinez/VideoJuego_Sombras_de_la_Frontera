using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

/// <summary>
/// Nivel 2 "Río Bravo" (Issue: sonido del nivel).
/// 1. Genera los sonidos del río (brazadas, chapuzón, salir a respirar, burbujas, corriente, objeto, ahogamiento y sirena)
///    y la música de ambiente en bucle sin cortes (la cola del final se suma al inicio).
/// 2. Crea el mezclador MezcladorPrincipal.mixer: Master (con filtro pasa-bajos expuesto como "CorteSubmarino")
///    y sus grupos Musica y Efectos (volúmenes expuestos como "VolumenMusica" y "VolumenEfectos").
/// 3. Asigna mezclador, grupos y clips al prefab del AudioManager (persistente en todas las escenas).
/// Los sonidos que ya existen se reutilizan: alerta, mordida, siseo, daño, Game Over y Victoria.
/// </summary>
public static partial class SetupNivel2Editor
{
    private const string AUDIO_MANAGER_PREFAB = "Assets/Prefabs/Audio/AudioManager.prefab";
    private const string MIXER_PATH = "Assets/Audio/MezcladorPrincipal.mixer";
    private const string RIVER_SFX = "Assets/Audio/SFX/River_";
    private const string RIVER_BGM = "Assets/Audio/BGM/River_Ambient_Loop.wav";
    private const string CUTOFF_PARAM = "CorteSubmarino";
    private const int RATE = 22050;

    [MenuItem("Sombras de la Frontera/Nivel 2/5. Configurar Sonido del Río (Mezclador, Efectos y Música)")]
    public static void SetupRiverAudioMenu()
    {
        SetupRiverAudio(regenerate: true);
        EditorUtility.DisplayDialog("Nivel 2", "Sonido del río configurado: mezclador, efectos y música en bucle.", "OK");
    }

    public static void SetupRiverAudio(bool regenerate = false)
    {
        GenerateRiverSounds(regenerate);
        ImportUserSounds();   // Sonidos reales de la carpeta Audio (cocodrilo y patrulla), si están
        AudioMixer mixer = EnsureMixer(out AudioMixerGroup music, out AudioMixerGroup sfx);
        AssignAudioManager(mixer, music, sfx);
    }

    // =========================================================================
    // SONIDOS GENERADOS
    // =========================================================================

    private static void GenerateRiverSounds(bool regenerate)
    {
        void Make(string path, Func<float[]> build)
        {
            if (!regenerate && File.Exists(path)) return;
            WriteWav(path, build());
        }

        for (int i = 1; i <= 3; i++)
        {
            int seed = i;
            Make($"{RIVER_SFX}Swim_{i}.wav", () => SwimStroke(seed));
        }
        Make($"{RIVER_SFX}Dive.wav", Dive);
        Make($"{RIVER_SFX}Surface.wav", Surface);
        Make($"{RIVER_SFX}Bubbles.wav", () => Bubbles(0.6f, 4, 11));
        Make($"{RIVER_SFX}Current_Loop.wav", CurrentLoop);
        Make($"{RIVER_SFX}Pickup.wav", Pickup);
        Make($"{RIVER_SFX}Drown.wav", Drown);
        Make($"{RIVER_SFX}Boat_Siren.wav", BoatSiren);
        Make($"{RIVER_SFX}Boat_Engine_Loop.wav", BoatEngine);
        Make($"{RIVER_SFX}Gar_Bite.wav", () => Bite(1900f, 0.12f, 0.5f, 91));
        Make($"{RIVER_SFX}Gar_Splash.wav", TailSplash);
        Make($"{RIVER_SFX}Croc_Bite.wav", () => Bite(950f, 0.26f, 1f, 92));
        Make($"{RIVER_SFX}Croc_Growl.wav", CrocGrowl);
        Make($"{RIVER_SFX}WaterSnake_Hiss.wav", () => Hiss(0.75f, 93));
        Make($"{RIVER_SFX}WaterSnake_Strike.wav", SnakeStrike);
        Make($"{RIVER_SFX}Fauna_Swim.wav", FaunaSwim);
        Make(RIVER_BGM, AmbientMusic);
        AssetDatabase.Refresh();

        // Todos sin compresión y cargados en memoria: el bucle de la música y la corriente cierra exacto (sin silencios)
        foreach (string path in Directory.GetFiles("Assets/Audio", "River_*.wav", SearchOption.AllDirectories))
        {
            var importer = AssetImporter.GetAtPath(path.Replace('\\', '/')) as AudioImporter;
            if (importer == null) continue;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = s;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
    }

    private static float[] SwimStroke(int seed)
    {
        var rnd = new System.Random(100 + seed);
        float[] s = new float[(int)(RATE * 0.38f)];
        float lp = 0f, hp = 0f, prev = 0f;
        float cutoff = 1500f + seed * 250f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            float n = Noise(rnd);
            lp += (n - lp) * Coef(cutoff);            // Agua que se desplaza
            hp = 0.97f * (hp + lp - prev); prev = lp; // Sin retumbo grave
            float env = Mathf.Clamp01(t / 0.04f) * Mathf.Exp(-t / 0.09f);
            float plip = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 150f, t / 0.08f) * t) * Mathf.Exp(-t / 0.03f) * 0.3f;
            s[i] = hp * env * 1.6f + plip;
        }
        return Normalize(s, 0.8f);
    }

    private static float[] Dive()
    {
        var rnd = new System.Random(21);
        float[] s = new float[(int)(RATE * 0.95f)];
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            lp += (Noise(rnd) - lp) * Coef(Mathf.Lerp(4200f, 500f, Mathf.Clamp01(t / 0.5f)));
            float splash = lp * Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t / 0.16f) * 1.8f;
            float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * Mathf.Exp(-t / 0.07f) * 0.6f;
            s[i] = splash + thump;
        }
        Mix(s, Bubbles(0.65f, 6, 22), (int)(RATE * 0.28f), 0.5f);
        return Normalize(s, 0.85f);
    }

    private static float[] Surface()
    {
        var rnd = new System.Random(31);
        float[] s = new float[(int)(RATE * 0.85f)];
        float lp = 0f, bp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            float n = Noise(rnd);
            lp += (n - lp) * Coef(2600f);
            float splash = lp * Mathf.Clamp01(t / 0.008f) * Mathf.Exp(-t / 0.08f) * 1.4f;
            // Bocanada de aire: ruido suave que sube y baja (inhalación)
            bp += (n - bp) * Coef(1400f);
            float gaspT = Mathf.Clamp01((t - 0.12f) / 0.55f);
            float gasp = (bp - lp * 0.5f) * Mathf.Sin(gaspT * Mathf.PI) * (t > 0.12f ? 0.9f : 0f);
            s[i] = splash + gasp;
        }
        return Normalize(s, 0.8f);
    }

    private static float[] Bubbles(float duration, int count, int seed)
    {
        var rnd = new System.Random(seed);
        float[] s = new float[(int)(RATE * duration)];
        for (int b = 0; b < count; b++)
        {
            int start = (int)(rnd.NextDouble() * (s.Length - RATE * 0.08f));
            float f0 = 300f + (float)rnd.NextDouble() * 250f;
            float len = 0.04f + (float)rnd.NextDouble() * 0.04f;
            for (int i = 0; i < len * RATE && start + i < s.Length; i++)
            {
                float t = i / (float)RATE;
                float f = f0 * (1f + 1.8f * t / len);          // La burbuja sube de tono
                s[start + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Sin(Mathf.PI * t / len) * 0.6f;
            }
        }
        return Normalize(s, 0.7f);
    }

    /// <summary>Rumor de agua corriendo: 4 s que cierran el bucle sin costura.</summary>
    private static float[] CurrentLoop()
    {
        var rnd = new System.Random(41);
        int length = RATE * 4, tail = RATE / 2;
        float[] raw = new float[length + tail];
        float low = 0f, high = 0f, prev = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)RATE;
            float n = Noise(rnd);
            low += (n - low) * Coef(650f);
            float lp2 = high + (n - high) * Coef(5000f);
            float fizz = lp2 - prev; prev = lp2; high = lp2;
            float swell = 1f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);  // 2 oleadas exactas en 4 s
            raw[i] = (low * 2.2f + fizz * 0.5f) * swell;
        }
        return Normalize(CrossfadeLoop(raw, length, tail), 0.75f);
    }

    private static float[] Pickup()
    {
        float[] s = new float[(int)(RATE * 0.6f)];
        void Note(float freq, float at)
        {
            for (int i = (int)(at * RATE); i < s.Length; i++)
            {
                float t = i / (float)RATE - at;
                float env = Mathf.Clamp01(t / 0.005f) * Mathf.Exp(-t / 0.16f);
                s[i] += (Mathf.Sin(2f * Mathf.PI * freq * t) + 0.3f * Mathf.Sin(4f * Mathf.PI * freq * t)) * env * 0.5f;
            }
        }
        Note(1318.5f, 0f);     // Mi
        Note(1975.5f, 0.08f);  // Si
        Mix(s, Bubbles(0.2f, 2, 51), 0, 0.25f);
        return Normalize(s, 0.75f);
    }

    private static float[] Drown()
    {
        var rnd = new System.Random(61);
        float[] s = new float[(int)(RATE * 1.9f)];
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            lp += (Noise(rnd) - lp) * Coef(400f);
            s[i] = lp * 1.4f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / 1.9f));
        }
        // Burbujas cada vez más graves y espaciadas (se escapa el último aire)
        float at = 0f; int k = 0;
        while (at < 1.5f)
        {
            float[] b = Bubbles(0.12f, 1, 70 + k);
            float pitch = Mathf.Lerp(1f, 0.55f, at / 1.5f);
            int start = (int)(at * RATE);
            for (int i = 0; i < b.Length; i++)
            {
                int j = start + (int)(i / pitch);
                if (j < s.Length) s[j] += b[i] * 0.7f;
            }
            at += 0.07f + at * 0.12f; k++;
        }
        return Normalize(s, 0.85f);
    }

    private static float[] BoatSiren()
    {
        float[] s = new float[(int)(RATE * 1.7f)];
        float phase = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            float f = 650f + 380f * (0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / 0.85f));  // Dos aullidos
            phase += 2f * Mathf.PI * f / RATE;
            float wave = Mathf.Sin(phase) + 0.35f * Mathf.Sin(2f * phase) + 0.15f * Mathf.Sin(3f * phase);
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((1.7f - t) / 0.25f);
            s[i] = wave * env;
        }
        return Normalize(s, 0.75f);
    }

    /// <summary>
    /// Motor fuera de borda de dos tiempos (3 s en bucle): cada explosión es un pulso que resuena en el escape,
    /// con pequeñas variaciones de fuerza y ritmo (como un motor real, no un tono perfecto), el silbido del engranaje,
    /// el agua que corta el casco y golpes de las olas contra la lancha.
    /// </summary>
    private static float[] BoatEngine()
    {
        var rnd = new System.Random(101);
        int length = RATE * 3, fade = (int)(RATE * 0.4f);
        float[] raw = new float[length + fade];

        // Explosiones del motor (~88 por segundo, unas más fuertes que otras: el "brrrap" de dos tiempos)
        float pos = 0f; int cycle = 0;
        while (pos < raw.Length)
        {
            float period = RATE / 88f * (1f + (float)(rnd.NextDouble() - 0.5) * 0.06f);
            float strength = (cycle % 2 == 0 ? 1f : 0.78f) * (0.85f + (float)rnd.NextDouble() * 0.3f);
            int start = (int)pos;
            for (int i = 0; i < RATE * 0.012f && start + i < raw.Length; i++)
            {
                float t = i / (float)RATE;
                float pulse = Mathf.Exp(-t / 0.0028f) * (Mathf.Sin(2f * Mathf.PI * 340f * t) + 0.5f * Mathf.Sin(2f * Mathf.PI * 760f * t));
                raw[start + i] += pulse * strength;
            }
            pos += period; cycle++;
        }

        float wash = 0f, washPrev = 0f, washHp = 0f, swell = 0f, body = 0f;
        float nextSlap = 0.2f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)RATE;
            float n = Noise(rnd);
            // Cuerpo del motor: suaviza los pulsos (suena a carcasa, no a chicharra)
            body += (raw[i] - body) * Coef(1800f);
            // Silbido del engranaje con un leve vaivén
            float whine = Mathf.Sin(2f * Mathf.PI * (365f + 6f * Mathf.Sin(2f * Mathf.PI * 0.7f * t)) * t) * 0.05f;
            // Agua cortada por el casco (ruido de banda media que respira despacio)
            wash += (n - wash) * Coef(1600f);
            washHp = 0.96f * (washHp + wash - washPrev); washPrev = wash;
            swell += ((float)rnd.NextDouble() - swell) * Coef(2f);
            raw[i] = Tanh(body * 1.6f) * 0.75f + whine + washHp * (0.35f + 0.35f * swell);

            // Golpe de ola contra el casco
            if (t >= nextSlap)
            {
                int len = (int)(RATE * 0.09f);
                float slapLp = 0f;
                for (int k = 0; k < len && i + k < raw.Length; k++)
                {
                    slapLp += (Noise(rnd) - slapLp) * Coef(700f);
                    raw[i + k] += slapLp * Mathf.Exp(-k / (RATE * 0.03f)) * 0.5f;
                }
                nextSlap = t + 0.35f + (float)rnd.NextDouble() * 0.5f;
            }
        }
        return Normalize(CrossfadeLoop(raw, length, fade), 0.8f);
    }

    /// <summary>Mordida en el agua: chasquido de las mandíbulas, rebote y chapoteo (más grave y pesado para el cocodrilo).</summary>
    private static float[] Bite(float jawTone, float splashLength, float thump, int seed)
    {
        var rnd = new System.Random(seed);
        float[] s = new float[(int)(RATE * (0.35f + splashLength))];
        void Snap(float at, float gain)
        {
            int from = (int)(at * RATE);
            for (int i = 0; i < RATE * 0.05f && from + i < s.Length; i++)
            {
                float t = i / (float)RATE;
                float click = Noise(rnd) * Mathf.Exp(-t / 0.0015f);
                float clack = Mathf.Sin(2f * Mathf.PI * jawTone * t) * Mathf.Exp(-t / 0.012f);
                s[from + i] += (click * 0.9f + clack * 0.7f) * gain;
            }
        }
        Snap(0f, 1f);
        Snap(0.045f, 0.45f);  // Rebote de las mandíbulas

        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            lp += (Noise(rnd) - lp) * Coef(Mathf.Lerp(3500f, 700f, Mathf.Clamp01(t / splashLength)));
            s[i] += lp * Mathf.Clamp01((t - 0.01f) / 0.01f) * Mathf.Exp(-t / (splashLength * 0.45f)) * 1.3f;
            s[i] += Mathf.Sin(2f * Mathf.PI * 70f * t) * Mathf.Exp(-t / 0.06f) * thump * 0.8f;  // Golpe del cuerpo
        }
        return Normalize(s, 0.95f);
    }

    /// <summary>Coletazo del pejelagarto al detectar a Mateo.</summary>
    private static float[] TailSplash()
    {
        var rnd = new System.Random(94);
        float[] s = new float[(int)(RATE * 0.5f)];
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            lp += (Noise(rnd) - lp) * Coef(Mathf.Lerp(3000f, 600f, t / 0.5f));
            float slap = lp * Mathf.Clamp01(t / 0.004f) * Mathf.Exp(-t / 0.1f) * 1.5f;
            float plop = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(180f, 90f, t / 0.12f) * t) * Mathf.Exp(-t / 0.05f) * 0.6f;
            s[i] = slap + plop;
        }
        Mix(s, Bubbles(0.3f, 3, 95), (int)(RATE * 0.12f), 0.3f);
        return Normalize(s, 0.85f);
    }

    /// <summary>Gruñido grave del cocodrilo: retumbo con vibración y aire que sale por la nariz.</summary>
    private static float[] CrocGrowl()
    {
        var rnd = new System.Random(96);
        float[] s = new float[(int)(RATE * 1.3f)];
        float phase = 0f, rumble = 0f, breath = 0f, breathPrev = 0f, breathHp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            float f = 52f + 8f * Mathf.Sin(2f * Mathf.PI * 1.3f * t);
            phase += f / RATE;
            float saw = 2f * (phase % 1f) - 1f;
            float flutter = 0.6f + 0.4f * Mathf.Sin(2f * Mathf.PI * 23f * t);   // Vibración de la garganta
            rumble += (saw * flutter - rumble) * Coef(380f);
            float n = Noise(rnd);
            breath += (n - breath) * Coef(4500f);
            breathHp = 0.9f * (breathHp + breath - breathPrev); breathPrev = breath;
            float env = Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((1.3f - t) / 0.35f);
            s[i] = (rumble * 1.6f + breathHp * 0.35f) * env;
        }
        return Normalize(s, 0.9f);
    }

    /// <summary>Siseo de la serpiente de agua.</summary>
    private static float[] Hiss(float duration, int seed)
    {
        var rnd = new System.Random(seed);
        float[] s = new float[(int)(RATE * duration)];
        float lp = 0f, prev = 0f, hp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            lp += (Noise(rnd) - lp) * Coef(8000f);
            hp = 0.85f * (hp + lp - prev); prev = lp;     // Solo agudos: "ssss"
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((duration - t) / 0.2f) * (0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 9f * t));
            s[i] = hp * env;
        }
        return Normalize(s, 0.85f);
    }

    /// <summary>Ataque de la serpiente: siseo corto y golpe en el agua.</summary>
    private static float[] SnakeStrike()
    {
        float[] s = Hiss(0.35f, 97);
        float[] strike = Bite(2400f, 0.1f, 0.2f, 98);
        float[] result = new float[Mathf.Max(s.Length, strike.Length + RATE / 10)];
        Mix(result, s, 0, 0.7f);
        Mix(result, strike, RATE / 10, 1f);
        return Normalize(result, 0.95f);
    }

    /// <summary>Chapoteo suave de un animal nadando cerca.</summary>
    private static float[] FaunaSwim()
    {
        var rnd = new System.Random(99);
        float[] s = new float[(int)(RATE * 0.45f)];
        float lp = 0f;
        for (int i = 0; i < s.Length; i++)
        {
            float t = i / (float)RATE;
            lp += (Noise(rnd) - lp) * Coef(900f);
            s[i] = lp * Mathf.Sin(Mathf.PI * t / 0.45f) * 1.4f;
        }
        Mix(s, Bubbles(0.25f, 2, 100), (int)(RATE * 0.1f), 0.2f);
        return Normalize(s, 0.75f);
    }

    private static float Tanh(float x)
    {
        float e = Mathf.Exp(2f * x);
        return (e - 1f) / (e + 1f);
    }

    /// <summary>
    /// Música de ambiente del río (32 s en bucle): La menor - Fa - Do - Sol con un colchón suave, bajo,
    /// arpegio de guitarra punteada y el rumor del río. La cola de las notas del final se suma al inicio
    /// y el rumor se cruza en el punto de bucle, así que la vuelta no tiene cortes.
    /// </summary>
    private static float[] AmbientMusic()
    {
        const float chordLength = 8f;
        int length = (int)(RATE * chordLength * 4), tail = RATE * 4;
        float[] music = new float[length + tail];
        float[][] chords =
        {
            new[] { 110.00f, 220.00f, 261.63f, 329.63f },  // La menor
            new[] { 87.31f, 174.61f, 220.00f, 261.63f },   // Fa
            new[] { 130.81f, 164.81f, 196.00f, 261.63f },  // Do
            new[] { 98.00f, 146.83f, 196.00f, 246.94f },   // Sol
        };
        int[] arpeggio = { 1, 2, 3, 2, 1, 3, 2, 3 };
        var rnd = new System.Random(81);

        for (int c = 0; c < chords.Length; c++)
        {
            float start = c * chordLength;
            float[] notes = chords[c];

            // Colchón: notas del acorde con dos osciladores levemente desafinados (entra y sale suave)
            for (int n = 1; n < notes.Length; n++)
                AddTone(music, notes[n], start, chordLength + 2.5f, 1.5f, 2f, 0.07f, detune: 0.004f);

            // Bajo: la fundamental al inicio del acorde y a la mitad
            AddTone(music, notes[0], start, 4.5f, 0.02f, 3f, 0.22f);
            AddTone(music, notes[0], start + 4f, 4.5f, 0.02f, 3f, 0.16f);

            // Arpegio de guitarra (una nota por segundo a contratiempo, la octava de arriba)
            for (int k = 0; k < 8; k++)
                Pluck(music, notes[arpeggio[k]] * 2f, start + k + 0.5f, 2.5f, 0.28f, rnd);
        }

        // Las notas que se salen del final continúan al principio del bucle
        float[] loop = new float[length];
        for (int i = 0; i < length; i++) loop[i] = music[i] + (i < tail ? music[length + i] : 0f);

        // Rumor del río bajo la música (cruzado en el punto de bucle)
        float[] river = new float[length + RATE];
        float low = 0f;
        for (int i = 0; i < river.Length; i++)
        {
            float t = i / (float)RATE;
            low += (Noise(rnd) - low) * Coef(500f);
            river[i] = low * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * t / 8f));  // 4 oleadas exactas en 32 s
        }
        float[] riverLoop = CrossfadeLoop(river, length, RATE);
        for (int i = 0; i < length; i++) loop[i] += riverLoop[i] * 0.35f;

        return Normalize(loop, 0.8f);
    }

    private static void AddTone(float[] buffer, float freq, float start, float duration, float attack, float release, float gain, float detune = 0f)
    {
        int from = Mathf.Max(0, (int)(start * RATE));
        int to = Mathf.Min(buffer.Length, (int)((start + duration) * RATE));
        for (int i = from; i < to; i++)
        {
            float t = i / (float)RATE - start;
            float env = Mathf.Clamp01(t / attack) * Mathf.Clamp01((duration - t) / release);
            float w = Mathf.Sin(2f * Mathf.PI * freq * t);
            if (detune > 0f) w = 0.5f * (w + Mathf.Sin(2f * Mathf.PI * freq * (1f + detune) * t));
            buffer[i] += w * env * gain;
        }
    }

    /// <summary>Cuerda punteada (Karplus-Strong): suena a guitarra.</summary>
    private static void Pluck(float[] buffer, float freq, float start, float duration, float gain, System.Random rnd)
    {
        int period = Mathf.Max(2, Mathf.RoundToInt(RATE / freq));
        float[] line = new float[period];
        for (int i = 0; i < period; i++) line[i] = Noise(rnd);
        int from = (int)(start * RATE);
        int samples = (int)(duration * RATE);
        for (int i = 0; i < samples && from + i < buffer.Length; i++)
        {
            int idx = i % period;
            float next = line[(idx + 1) % period];
            float v = line[idx];
            line[idx] = 0.996f * 0.5f * (v + next);
            float fade = Mathf.Clamp01((samples - i) / (RATE * 0.3f));
            if (from + i >= 0) buffer[from + i] += v * gain * fade;
        }
    }

    // =========================================================================
    // SONIDOS REALES (carpeta Assets/Audio)
    // =========================================================================

    private const string USER_CROC = "Assets/Audio/Sonido cocodrilo.mp3";
    private const string USER_BOAT = "Assets/Audio/sonidosKMD.mp3";

    /// <summary>
    /// Convierte los sonidos reales que se agregaron a Assets/Audio (los originales no se modifican):
    /// - Cocodrilo (33 s): se toma su parte más fuerte (~2.5 s, el gruñido) para la fauna del río.
    /// - Patrulla (41 s): se toma el tramo de 6 s más parejo del motor y se convierte en bucle sin cortes
    ///   (y su parte más fuerte, ~3 s, suena al atrapar a Mateo);
    ///   el volumen según la distancia y el efecto Doppler los sigue poniendo PatrolBoat.
    /// </summary>
    private static void ImportUserSounds()
    {
        if (File.Exists(USER_CROC))
        {
            float[] croc = LoadClipMono(USER_CROC, out int rate);
            if (croc != null) WriteWav($"{RIVER_SFX}Croc_Real.wav", LoudestEvent(croc, rate, 2.5f), rate);
        }
        if (File.Exists(USER_BOAT))
        {
            float[] boat = LoadClipMono(USER_BOAT, out int rate);
            if (boat != null)
            {
                WriteWav($"{RIVER_SFX}Boat_Engine_Real_Loop.wav", SteadyLoop(boat, rate, 6f, 0.5f), rate);
                WriteWav($"{RIVER_SFX}Boat_Capture_Real.wav", LoudestEvent(boat, rate, 3f), rate);   // Al atrapar a Mateo
            }
        }
        AssetDatabase.Refresh();

        foreach (string path in new[] { $"{RIVER_SFX}Croc_Real.wav", $"{RIVER_SFX}Boat_Engine_Real_Loop.wav", $"{RIVER_SFX}Boat_Capture_Real.wav" })
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;
            AudioImporterSampleSettings s = importer.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.PCM;
            importer.defaultSampleSettings = s;
            importer.forceToMono = true;
            importer.loadInBackground = false;
            importer.SaveAndReimport();
        }
    }

    /// <summary>Lee un audio del proyecto (mp3, wav...) como muestras en mono.</summary>
    private static float[] LoadClipMono(string path, out int rate)
    {
        rate = RATE;
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(path) as AudioImporter;
        if (importer == null) { Debug.LogWarning($"[Nivel2] No se pudo importar {path}."); return null; }

        AudioImporterSampleSettings s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = s;
        importer.forceToMono = true;
        importer.loadInBackground = false;
        importer.SaveAndReimport();

        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null || !clip.LoadAudioData()) { Debug.LogWarning($"[Nivel2] No se pudo leer {path}."); return null; }
        float[] data = new float[clip.samples * clip.channels];
        if (!clip.GetData(data, 0)) return null;
        rate = clip.frequency;
        if (clip.channels == 1) return data;

        float[] mono = new float[clip.samples];
        for (int i = 0; i < mono.Length; i++)
        {
            float sum = 0f;
            for (int c = 0; c < clip.channels; c++) sum += data[i * clip.channels + c];
            mono[i] = sum / clip.channels;
        }
        return mono;
    }

    /// <summary>Volumen (RMS) por ventanas.</summary>
    private static float[] Energy(float[] s, int window)
    {
        float[] e = new float[s.Length / window];
        for (int w = 0; w < e.Length; w++)
        {
            double sum = 0;
            for (int i = 0; i < window; i++) { float v = s[w * window + i]; sum += v * v; }
            e[w] = (float)Math.Sqrt(sum / window);
        }
        return e;
    }

    /// <summary>
    /// La parte más fuerte del audio (máximo maxSeconds): empieza un poco antes del pico y sigue mientras el sonido
    /// se mantenga sobre el 15% del pico, con entrada y salida suaves.
    /// </summary>
    private static float[] LoudestEvent(float[] s, int rate, float maxSeconds)
    {
        int window = rate / 20;  // 50 ms
        float[] e = Energy(s, window);
        int peak = Array.IndexOf(e, e.Max());
        float threshold = e[peak] * 0.15f;
        int maxWindows = (int)(maxSeconds * 20);

        int from = peak, to = peak;
        while (from > 0 && e[from - 1] > threshold && peak - from < 8) from--;   // Hasta 0.4 s antes del pico
        while (to < e.Length - 1 && e[to + 1] > threshold && to - from < maxWindows) to++;

        int start = from * window, end = Mathf.Min(s.Length, (to + 1) * window + rate / 10);
        float[] cut = new float[end - start];
        Array.Copy(s, start, cut, 0, cut.Length);
        int fadeIn = rate / 50, fadeOut = Mathf.Min(cut.Length / 3, rate / 6);
        for (int i = 0; i < fadeIn && i < cut.Length; i++) cut[i] *= i / (float)fadeIn;
        for (int i = 0; i < fadeOut; i++) cut[cut.Length - 1 - i] *= i / (float)fadeOut;
        return Normalize(cut, 0.95f);
    }

    /// <summary>
    /// El tramo más parejo del audio (volumen estable y fuerte) convertido en bucle sin cortes:
    /// se buscan seconds + fade segundos con la menor variación de volumen y se cruza el final con el inicio.
    /// </summary>
    private static float[] SteadyLoop(float[] s, int rate, float seconds, float fadeSeconds)
    {
        int length = Mathf.Min((int)(seconds * rate), s.Length - (int)(fadeSeconds * rate) - 1);
        int fade = (int)(fadeSeconds * rate);
        int window = rate / 10;  // 100 ms
        float[] e = Energy(s, window);
        int span = (length + fade) / window;
        float globalMax = e.Max();

        int best = 0; float bestScore = float.MaxValue;
        for (int w = 0; w + span <= e.Length; w++)
        {
            float mean = 0f;
            for (int k = 0; k < span; k++) mean += e[w + k];
            mean /= span;
            if (mean < globalMax * 0.35f) continue;   // Que se oiga el motor, no un silencio
            float variance = 0f;
            for (int k = 0; k < span; k++) variance += (e[w + k] - mean) * (e[w + k] - mean);
            float score = Mathf.Sqrt(variance / span) / mean;
            if (score < bestScore) { bestScore = score; best = w; }
        }

        float[] raw = new float[length + fade];
        Array.Copy(s, best * window, raw, 0, raw.Length);
        Debug.Log($"[Nivel2] Motor de la patrulla: tramo desde {best * window / (float)rate:F1} s (variación de volumen {bestScore:P0}).");
        return Normalize(CrossfadeLoop(raw, length, fade), 0.85f);
    }

    // =========================================================================
    // UTILIDADES DE AUDIO
    // =========================================================================

    private static float Noise(System.Random rnd) => (float)(rnd.NextDouble() * 2.0 - 1.0);

    /// <summary>Coeficiente de un filtro pasa-bajos de un polo para la frecuencia de corte indicada.</summary>
    private static float Coef(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / RATE);

    private static void Mix(float[] target, float[] source, int offset, float gain)
    {
        for (int i = 0; i < source.Length && offset + i < target.Length; i++) target[offset + i] += source[i] * gain;
    }

    /// <summary>Recorta a "length" muestras cruzando la cola extra con el inicio: el final empalma con el principio.</summary>
    private static float[] CrossfadeLoop(float[] raw, int length, int fade)
    {
        float[] loop = new float[length];
        for (int i = 0; i < length; i++)
        {
            if (i < fade)
            {
                float k = i / (float)fade;
                loop[i] = raw[i] * k + raw[length + i] * (1f - k);
            }
            else loop[i] = raw[i];
        }
        return loop;
    }

    private static float[] Normalize(float[] s, float peak)
    {
        float max = s.Max(v => Mathf.Abs(v));
        if (max < 1e-5f) return s;
        float k = peak / max;
        for (int i = 0; i < s.Length; i++) s[i] *= k;
        return s;
    }

    private static void WriteWav(string path, float[] samples, int rate = RATE)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataSize = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataSize);
            foreach (float v in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }

    // =========================================================================
    // MEZCLADOR
    // =========================================================================

    /// <summary>
    /// Crea (una sola vez) el mezclador: Master con un filtro pasa-bajos expuesto ("CorteSubmarino") y los grupos
    /// Musica y Efectos. Unity no ofrece una API pública para armar mezcladores desde código, así que se usa la del editor.
    /// </summary>
    private static AudioMixer EnsureMixer(out AudioMixerGroup music, out AudioMixerGroup sfx)
    {
        music = sfx = null;
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);
        if (mixer == null)
        {
            try { BuildMixerAsset(); }
            catch (Exception e) { Debug.LogWarning($"[Nivel2] No se pudo crear el mezclador ({e.GetBaseException().Message}); el filtro bajo el agua usará el oyente."); }
            AssetDatabase.Refresh();
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);
        }
        if (mixer == null) return null;

        music = mixer.FindMatchingGroups("Master/Musica").FirstOrDefault();
        sfx = mixer.FindMatchingGroups("Master/Efectos").FirstOrDefault();
        return mixer;
    }

    private static void BuildMixerAsset()
    {
        const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        Type FindType(string name) => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null)
                                      ?? throw new Exception($"no existe {name}");
        Type ctrlT = FindType("UnityEditor.Audio.AudioMixerController");
        Type groupT = FindType("UnityEditor.Audio.AudioMixerGroupController");
        Type effectT = FindType("UnityEditor.Audio.AudioMixerEffectController");
        Type effectPathT = FindType("UnityEditor.Audio.AudioEffectParameterPath");
        Type groupPathT = FindType("UnityEditor.Audio.AudioGroupParameterPath");

        object ctrl = ctrlT.GetMethod("CreateMixerControllerAtPath", any).Invoke(null, new object[] { MIXER_PATH });
        object master = ctrlT.GetProperty("masterGroup", any).GetValue(ctrl);

        object NewGroup(string name)
        {
            object g = ctrlT.GetMethod("CreateNewGroup", any).Invoke(ctrl, new object[] { name, false });
            ctrlT.GetMethod("AddChildToParent", any).Invoke(ctrl, new[] { g, master });
            return g;
        }
        object musicGroup = NewGroup("Musica");
        object sfxGroup = NewGroup("Efectos");

        // Filtro pasa-bajos en el Master (después de la atenuación): sin efecto a 22000 Hz, apagado bajo el agua
        object lowpass = Activator.CreateInstance(effectT, any, null, new object[] { "Lowpass Simple" }, null);
        ctrlT.GetMethod("AddNewSubAsset", any).Invoke(ctrl, new[] { lowpass, (object)false });
        effectT.GetMethod("PreallocateGUIDs", any).Invoke(lowpass, null);
        groupT.GetMethod("InsertEffect", any).Invoke(master, new[] { lowpass, (object)1 });
        object snapshot = ctrlT.GetProperty("TargetSnapshot", any).GetValue(ctrl);
        effectT.GetMethod("SetValueForParameter", any).Invoke(lowpass, new[] { ctrl, snapshot, "Cutoff freq", (object)22000f });

        MethodInfo expose = ctrlT.GetMethod("AddExposedParameter", any);
        GUID cutoffGuid = (GUID)effectT.GetMethod("GetGUIDForParameter", any).Invoke(lowpass, new object[] { "Cutoff freq" });
        expose.Invoke(ctrl, new[] { Activator.CreateInstance(effectPathT, any, null, new[] { master, lowpass, (object)cutoffGuid }, null) });
        GUID musicGuid = (GUID)groupT.GetMethod("GetGUIDForVolume", any).Invoke(musicGroup, null);
        GUID sfxGuid = (GUID)groupT.GetMethod("GetGUIDForVolume", any).Invoke(sfxGroup, null);
        expose.Invoke(ctrl, new[] { Activator.CreateInstance(groupPathT, any, null, new[] { musicGroup, (object)musicGuid }, null) });
        expose.Invoke(ctrl, new[] { Activator.CreateInstance(groupPathT, any, null, new[] { sfxGroup, (object)sfxGuid }, null) });

        // Nombres de los parámetros expuestos
        PropertyInfo exposedProp = ctrlT.GetProperty("exposedParameters", any);
        Array exposed = (Array)exposedProp.GetValue(ctrl);
        for (int i = 0; i < exposed.Length; i++)
        {
            object p = exposed.GetValue(i);
            FieldInfo guidField = p.GetType().GetField("guid", any), nameField = p.GetType().GetField("name", any);
            GUID g = (GUID)guidField.GetValue(p);
            nameField.SetValue(p, g == cutoffGuid ? CUTOFF_PARAM : g == musicGuid ? "VolumenMusica" : "VolumenEfectos");
            exposed.SetValue(p, i);
        }
        exposedProp.SetValue(ctrl, exposed);

        EditorUtility.SetDirty((Object)ctrl);
        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[Nivel2]</b> Mezclador creado: Master (pasa-bajos 'CorteSubmarino') > Musica, Efectos.</color>");
    }

    // =========================================================================
    // AUDIOMANAGER
    // =========================================================================

    private static void AssignAudioManager(AudioMixer mixer, AudioMixerGroup music, AudioMixerGroup sfx)
    {
        AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        AudioClip ClipOr(string preferred, string fallback) => Clip(preferred) != null ? Clip(preferred) : Clip(fallback);

        void Fill(AudioManager manager)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("audioMixer").objectReferenceValue = mixer;
            so.FindProperty("musicGroup").objectReferenceValue = music;
            so.FindProperty("sfxGroup").objectReferenceValue = sfx;
            so.FindProperty("riverBGM").objectReferenceValue = Clip(RIVER_BGM);
            SerializedProperty strokes = so.FindProperty("swimStrokeClips");
            strokes.arraySize = 3;
            for (int i = 0; i < 3; i++) strokes.GetArrayElementAtIndex(i).objectReferenceValue = Clip($"{RIVER_SFX}Swim_{i + 1}.wav");
            so.FindProperty("diveClip").objectReferenceValue = Clip($"{RIVER_SFX}Dive.wav");
            so.FindProperty("surfaceClip").objectReferenceValue = Clip($"{RIVER_SFX}Surface.wav");
            so.FindProperty("bubblesClip").objectReferenceValue = Clip($"{RIVER_SFX}Bubbles.wav");
            so.FindProperty("currentLoopClip").objectReferenceValue = Clip($"{RIVER_SFX}Current_Loop.wav");
            so.FindProperty("pickupClip").objectReferenceValue = Clip($"{RIVER_SFX}Pickup.wav");
            so.FindProperty("drownClip").objectReferenceValue = Clip($"{RIVER_SFX}Drown.wav");
            so.FindProperty("boatSirenClip").objectReferenceValue = ClipOr($"{RIVER_SFX}Boat_Capture_Real.wav", $"{RIVER_SFX}Boat_Siren.wav");
            so.FindProperty("boatEngineClip").objectReferenceValue = ClipOr($"{RIVER_SFX}Boat_Engine_Real_Loop.wav", $"{RIVER_SFX}Boat_Engine_Loop.wav");
            so.FindProperty("garBiteClip").objectReferenceValue = Clip($"{RIVER_SFX}Gar_Bite.wav");
            so.FindProperty("garSplashClip").objectReferenceValue = ClipOr($"{RIVER_SFX}Croc_Real.wav", $"{RIVER_SFX}Gar_Splash.wav");
            so.FindProperty("crocBiteClip").objectReferenceValue = Clip($"{RIVER_SFX}Croc_Bite.wav");
            so.FindProperty("crocGrowlClip").objectReferenceValue = ClipOr($"{RIVER_SFX}Croc_Real.wav", $"{RIVER_SFX}Croc_Growl.wav");
            so.FindProperty("waterSnakeHissClip").objectReferenceValue = Clip($"{RIVER_SFX}WaterSnake_Hiss.wav");
            so.FindProperty("waterSnakeStrikeClip").objectReferenceValue = Clip($"{RIVER_SFX}WaterSnake_Strike.wav");
            so.FindProperty("faunaSwimClip").objectReferenceValue = Clip($"{RIVER_SFX}Fauna_Swim.wav");
            so.FindProperty("underwaterCutoffParam").stringValue = CUTOFF_PARAM;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // Prefab persistente (llega desde el Splash a todas las escenas)
        GameObject root = PrefabUtility.LoadPrefabContents(AUDIO_MANAGER_PREFAB);
        AudioManager prefabManager = root.GetComponent<AudioManager>();
        if (prefabManager != null)
        {
            Fill(prefabManager);
            PrefabUtility.SaveAsPrefabAsset(root, AUDIO_MANAGER_PREFAB);
        }
        PrefabUtility.UnloadPrefabContents(root);
    }

    // =========================================================================
    // VERIFICACIÓN
    // =========================================================================

    private static int VerifyAudio()
    {
        int problems = 0;
        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel2][OK] {message}");
            else { Debug.LogWarning($"[Nivel2][FALLA] {message}"); problems++; }
        }

        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(MIXER_PATH);
        Expect(mixer != null && mixer.GetFloat(CUTOFF_PARAM, out _), "El mezclador tiene el filtro bajo el agua expuesto (CorteSubmarino)");
        Expect(mixer != null && mixer.FindMatchingGroups("Master/Musica").Length == 1 && mixer.FindMatchingGroups("Master/Efectos").Length == 1,
            "El mezclador separa Música y Efectos");

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AUDIO_MANAGER_PREFAB);
        AudioManager manager = prefab != null ? prefab.GetComponent<AudioManager>() : null;
        Expect(manager != null, "Existe el AudioManager persistente");
        if (manager == null) return problems;

        var so = new SerializedObject(manager);
        Expect(so.FindProperty("audioMixer").objectReferenceValue == mixer && so.FindProperty("musicGroup").objectReferenceValue != null &&
               so.FindProperty("sfxGroup").objectReferenceValue != null, "El AudioManager envía la música y los efectos por el mezclador");

        string[] clips = { "riverBGM", "diveClip", "surfaceClip", "bubblesClip", "currentLoopClip", "pickupClip", "drownClip", "boatSirenClip",
                           "boatEngineClip", "garBiteClip", "garSplashClip", "crocBiteClip", "crocGrowlClip", "waterSnakeHissClip", "waterSnakeStrikeClip", "faunaSwimClip",
                           "alertStingClip", "dogBiteClip", "snakeHissClip", "damageHurtClip", "victoryClip", "gameOverClip", "drinkWaterClip" };
        string[] missing = clips.Where(c => so.FindProperty(c).objectReferenceValue == null).ToArray();
        Expect(missing.Length == 0 && so.FindProperty("swimStrokeClips").arraySize == 3,
            missing.Length == 0 ? "Cada acción del río tiene su sonido (nado, buceo, corriente, objeto, alerta, captura, mordida, ahogamiento y victoria)"
                                : $"Faltan sonidos: {string.Join(", ", missing)}");

        // Bucles sin corte: el final empalma con el principio (sin salto de onda)
        foreach (string field in new[] { "riverBGM", "currentLoopClip", "boatEngineClip" })
        {
            var clip = so.FindProperty(field).objectReferenceValue as AudioClip;
            if (clip == null) continue;
            float[] data = new float[clip.samples];
            bool read = clip.LoadAudioData() && clip.GetData(data, 0);
            float jump = read ? Mathf.Abs(data[data.Length - 1] - data[0]) : 1f;
            // Comparado con los saltos normales entre muestras del mismo clip (el ruido del agua cambia rápido)
            float typical = 0f;
            if (read)
            {
                var diffs = new float[data.Length - 1];
                for (int i = 0; i < diffs.Length; i++) diffs[i] = Mathf.Abs(data[i + 1] - data[i]);
                System.Array.Sort(diffs);
                typical = diffs[(int)(diffs.Length * 0.995f)];
            }
            Expect(read && jump <= Mathf.Max(0.01f, typical), $"{clip.name} hace bucle sin cortes (salto de {jump:F3} en el empalme; normal hasta {typical:F3}; {clip.length:F0} s)");
        }

        return problems;
    }
}
