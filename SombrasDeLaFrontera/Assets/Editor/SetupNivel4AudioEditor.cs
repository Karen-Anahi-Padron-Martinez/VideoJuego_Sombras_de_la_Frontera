using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Nivel 4 "El Refugio Seguro" (Issue 4: sonido del nivel).
/// 1. Genera por código (igual que el Nivel 2) la música de la última noche en el desierto (bucle sin cortes),
///    el zumbido de los drones, sus pitidos al fijar a Mateo, el crepitar de la fogata y la melodía final de victoria.
/// 2. Los asigna al prefab del AudioManager (persistente en todas las escenas), que respeta los volúmenes de Opciones.
/// Se reutilizan los pasos en arena, las voces de la patrulla, los ladridos y el Game Over que ya existen.
/// </summary>
public static class SetupNivel4AudioEditor
{
    private const string AUDIO_MANAGER_PREFAB = "Assets/Prefabs/Audio/AudioManager.prefab";
    public const string BGM_PATH = "Assets/Audio/BGM/SafeHaven_Night_Loop.wav";
    public const string DRONE_HUM = "Assets/Audio/SFX/Drone_Hum_Loop.wav";
    public const string DRONE_LOCK = "Assets/Audio/SFX/Drone_Lock.wav";
    public const string CAMPFIRE = "Assets/Audio/SFX/Campfire_Loop.wav";
    public const string FINAL_VICTORY = "Assets/Audio/SFX/Victory_Final.wav";
    private const int RATE = 22050;

    [MenuItem("Sombras de la Frontera/Nivel 4/5. Configurar Sonido del Nivel 4")]
    public static void SetupAudioMenu()
    {
        SetupAudio(regenerate: true);
        EditorUtility.DisplayDialog("Nivel 4", "Música, drones, fogata y melodía final asignados al AudioManager.", "Aceptar");
    }

    /// <summary>Genera los sonidos que falten (o todos si regenerate) y los asigna al AudioManager.</summary>
    public static void SetupAudio(bool regenerate = false)
    {
        Generate(BGM_PATH, regenerate, NightMusic);
        Generate(DRONE_HUM, regenerate, DroneHum);
        Generate(DRONE_LOCK, regenerate, DroneLock);
        Generate(CAMPFIRE, regenerate, CampfireLoop);
        Generate(FINAL_VICTORY, regenerate, FinalVictory);
        AssetDatabase.Refresh();

        GameObject root = PrefabUtility.LoadPrefabContents(AUDIO_MANAGER_PREFAB);
        AudioManager manager = root.GetComponent<AudioManager>();
        if (manager != null)
        {
            var so = new SerializedObject(manager);
            so.FindProperty("safeHavenBGM").objectReferenceValue = Clip(BGM_PATH);
            so.FindProperty("droneHumClip").objectReferenceValue = Clip(DRONE_HUM);
            so.FindProperty("droneLockClip").objectReferenceValue = Clip(DRONE_LOCK);
            so.FindProperty("campfireLoopClip").objectReferenceValue = Clip(CAMPFIRE);
            so.FindProperty("finalVictoryClip").objectReferenceValue = Clip(FINAL_VICTORY);
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, AUDIO_MANAGER_PREFAB);
        }
        PrefabUtility.UnloadPrefabContents(root);
        Debug.Log("<color=green><b>[Nivel4]</b> Sonido del Nivel 4 asignado al AudioManager.</color>");
    }

    /// <summary>Verdadero si el prefab del AudioManager tiene todos los sonidos del Nivel 4.</summary>
    public static bool IsAssigned()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AUDIO_MANAGER_PREFAB);
        AudioManager manager = prefab != null ? prefab.GetComponent<AudioManager>() : null;
        if (manager == null) return false;
        var so = new SerializedObject(manager);
        return new[] { "safeHavenBGM", "droneHumClip", "droneLockClip", "campfireLoopClip", "finalVictoryClip" }
            .All(p => so.FindProperty(p).objectReferenceValue != null);
    }

    private static AudioClip Clip(string path) => AssetDatabase.LoadAssetAtPath<AudioClip>(path);

    private static void Generate(string path, bool regenerate, System.Func<float[]> make)
    {
        if (!regenerate && File.Exists(path)) return;
        WriteWav(path, make());
        AssetDatabase.ImportAsset(path);
    }

    // =========================================================================
    // SONIDOS
    // =========================================================================

    /// <summary>
    /// Música de la última noche (32 s en bucle): Re menor - Si bemol - Fa - Do, que termina abierta hacia la esperanza.
    /// Colchón suave, bajo, guitarra punteada escasa, un latido grave de tensión cada 2 s y viento del desierto.
    /// La cola de las notas del final se suma al inicio y el viento se cruza en el punto de bucle (sin cortes).
    /// </summary>
    private static float[] NightMusic()
    {
        const float chordLength = 8f;
        int length = (int)(RATE * chordLength * 4), tail = RATE * 4;
        float[] music = new float[length + tail];
        float[][] chords =
        {
            new[] { 73.42f, 146.83f, 174.61f, 220.00f },   // Re menor
            new[] { 58.27f, 116.54f, 146.83f, 174.61f },   // Si bemol
            new[] { 87.31f, 130.81f, 174.61f, 220.00f },   // Fa
            new[] { 65.41f, 130.81f, 164.81f, 196.00f },   // Do
        };
        int[] pattern = { 1, 3, 2, 3, 1, 2, 3, 2 };
        var rnd = new System.Random(404);

        for (int c = 0; c < chords.Length; c++)
        {
            float start = c * chordLength;
            float[] notes = chords[c];
            for (int n = 1; n < notes.Length; n++)
                AddTone(music, notes[n], start, chordLength + 2.5f, 1.8f, 2.2f, 0.06f, detune: 0.005f);
            AddTone(music, notes[0], start, 4.5f, 0.03f, 3f, 0.2f);
            AddTone(music, notes[0], start + 4f, 4.5f, 0.03f, 3f, 0.14f);

            // Guitarra escasa: una nota cada segundo, alternando octavas, con silencios
            for (int k = 0; k < 8; k++)
            {
                if (k == 3 || k == 7) continue;
                float octave = k % 2 == 0 ? 2f : 4f;
                Pluck(music, notes[pattern[k]] * octave, start + k + 0.5f, 2.2f, 0.22f, rnd);
            }

            // Latido grave (dos golpes) cada 2 s: la tensión de la persecución
            for (float b = 0; b < chordLength; b += 2f)
            {
                Thump(music, start + b, 0.18f);
                Thump(music, start + b + 0.32f, 0.11f);
            }
        }

        float[] loop = new float[length];
        for (int i = 0; i < length; i++) loop[i] = music[i] + (i < tail ? music[length + i] : 0f);

        // Viento del desierto (ruido filtrado con ráfagas lentas, 4 exactas en 32 s)
        float[] wind = new float[length + RATE];
        float low = 0f, band = 0f;
        for (int i = 0; i < wind.Length; i++)
        {
            float t = i / (float)RATE;
            low += (Noise(rnd) - low) * Coef(900f);
            band += (low - band) * Coef(250f);
            wind[i] = (low - band) * (0.55f + 0.45f * Mathf.Pow(Mathf.Sin(Mathf.PI * t / 8f), 2f));
        }
        float[] windLoop = CrossfadeLoop(wind, length, RATE);
        for (int i = 0; i < length; i++) loop[i] += windLoop[i] * 0.5f;

        return Normalize(loop, 0.8f);
    }

    /// <summary>Hélices del dron (2 s en bucle): zumbido con el golpeteo de las aspas y aire filtrado.</summary>
    private static float[] DroneHum()
    {
        int length = RATE * 2;
        float[] raw = new float[length + RATE / 4];
        var rnd = new System.Random(77);
        float low = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            float t = i / (float)RATE;
            float blade = 0.65f + 0.35f * Mathf.Sin(2f * Mathf.PI * 40f * t);
            float buzz = 0f;
            for (int h = 1; h <= 6; h++) buzz += Mathf.Sin(2f * Mathf.PI * 150f * h * t + h) / h;
            buzz += 0.5f * Mathf.Sin(2f * Mathf.PI * 157f * t); // Segundo motor apenas desafinado
            low += (Noise(rnd) - low) * Coef(1800f);
            raw[i] = buzz * 0.25f * blade + low * 0.35f;
        }
        return Normalize(CrossfadeLoop(raw, length, RATE / 4), 0.7f);
    }

    /// <summary>Tres pitidos y un tono que sube: el dron fijó a Mateo.</summary>
    private static float[] DroneLock()
    {
        float[] s = new float[(int)(RATE * 0.75f)];
        for (int b = 0; b < 3; b++) AddTone(s, 1320f, b * 0.13f, 0.08f, 0.005f, 0.02f, 0.5f);
        int from = (int)(0.42f * RATE), to = s.Length;
        for (int i = from; i < to; i++)
        {
            float t = (i - from) / (float)RATE;
            float f = 900f + 900f * t / 0.3f;
            float env = Mathf.Clamp01(t / 0.01f) * Mathf.Clamp01((to - i) / (RATE * 0.05f));
            s[i] += Mathf.Sin(2f * Mathf.PI * f * t) * 0.35f * env;
        }
        return Normalize(s, 0.75f);
    }

    /// <summary>Fogata (6 s en bucle): rumor grave del fuego y chasquidos de leña al azar.</summary>
    private static float[] CampfireLoop()
    {
        int length = RATE * 6;
        float[] raw = new float[length + RATE / 2];
        var rnd = new System.Random(12);
        float low = 0f;
        for (int i = 0; i < raw.Length; i++)
        {
            low += (Noise(rnd) - low) * Coef(350f);
            raw[i] = low * 0.6f;
        }
        // Chasquidos: ráfagas cortísimas de ruido que se apagan rápido
        int pops = 6 * 7;
        for (int p = 0; p < pops; p++)
        {
            int at = rnd.Next(0, raw.Length - RATE / 10);
            float gain = 0.25f + (float)rnd.NextDouble() * 0.6f;
            int dur = (int)(RATE * (0.004f + rnd.NextDouble() * 0.025f));
            float hp = 0f, prev = 0f;
            for (int i = 0; i < dur; i++)
            {
                float n = Noise(rnd);
                hp = 0.9f * (hp + n - prev); prev = n;   // Agudo: chasquido seco
                raw[at + i] += hp * gain * Mathf.Exp(-6f * i / dur);
            }
        }
        return Normalize(CrossfadeLoop(raw, length, RATE / 2), 0.7f);
    }

    /// <summary>Melodía final (6 s): arpegio en Re mayor que se abre en un acorde cálido. Mateo está a salvo.</summary>
    private static float[] FinalVictory()
    {
        float[] s = new float[RATE * 6];
        var rnd = new System.Random(5);
        float[] arpeggio = { 293.66f, 369.99f, 440.00f, 587.33f, 739.99f };
        for (int i = 0; i < arpeggio.Length; i++) Pluck(s, arpeggio[i], i * 0.22f, 3.5f, 0.35f, rnd);
        foreach (float f in new[] { 146.83f, 220.00f, 293.66f, 369.99f, 440.00f })
            AddTone(s, f, 1.1f, 4.8f, 0.8f, 2.5f, 0.09f, detune: 0.004f);
        AddTone(s, 73.42f, 1.1f, 4.8f, 0.05f, 3f, 0.22f);
        Pluck(s, 587.33f, 1.3f, 4f, 0.25f, rnd);
        Pluck(s, 880.00f, 1.55f, 4f, 0.18f, rnd);
        return Normalize(s, 0.85f);
    }

    // =========================================================================
    // UTILIDADES (mismas técnicas que el sonido del Nivel 2)
    // =========================================================================

    private static void Thump(float[] buffer, float start, float gain)
    {
        int from = (int)(start * RATE), dur = (int)(0.35f * RATE);
        for (int i = 0; i < dur && from + i < buffer.Length; i++)
        {
            float t = i / (float)RATE;
            float f = 55f + 35f * Mathf.Exp(-t * 25f);
            buffer[from + i] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 9f) * gain;
        }
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

    private static float Noise(System.Random rnd) => (float)(rnd.NextDouble() * 2.0 - 1.0);

    /// <summary>Coeficiente de un filtro pasa-bajos de un polo para la frecuencia de corte indicada.</summary>
    private static float Coef(float cutoff) => 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / RATE);

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

    private static void WriteWav(string path, float[] samples)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int dataSize = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + dataSize);
            w.Write(new[] { 'W', 'A', 'V', 'E' }); w.Write(new[] { 'f', 'm', 't', ' ' });
            w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(RATE); w.Write(RATE * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(dataSize);
            foreach (float v in samples) w.Write((short)Mathf.RoundToInt(Mathf.Clamp(v, -1f, 1f) * 32767f));
        }
    }
}
