using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestor centralizado y persistente de sonido para Sombras de la Frontera.
/// - Música (BGM) en bucle según la escena: tema del menú en Splash/Menú y noche del desierto en el Nivel 1.
/// - Efectos (SFX) del jugador y de la interfaz.
/// - Sonidos de criaturas (coyotes, perros, serpientes, alacranes, arañas) con volumen según la distancia
///   a la cámara y paneo izquierda/derecha, para ubicar al enemigo solo con el oído.
/// - Volúmenes (General, Música y Efectos) guardados en PlayerPrefs desde el menú de Opciones.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer y Canales (opcional)")]
    [Tooltip("AudioMixer general del juego")]
    [SerializeField] private AudioMixer audioMixer;

    [Tooltip("Grupo de salida para la música")]
    [SerializeField] private AudioMixerGroup musicGroup;

    [Tooltip("Grupo de salida para los efectos de sonido")]
    [SerializeField] private AudioMixerGroup sfxGroup;

    [Header("Fuentes de Audio (AudioSources)")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource footstepSource;

    [Header("Música de Fondo (BGM)")]
    [Tooltip("Música nocturna y tensa del desierto (Nivel 1)")]
    [SerializeField] private AudioClip desertNightBGM;

    [Tooltip("Tema de guitarra del Splash y el Menú Principal")]
    [SerializeField] private AudioClip menuBGM;

    [Header("Efectos del Jugador y del Mundo")]
    [Tooltip("Variaciones de pasos sobre arena seca")]
    [SerializeField] private AudioClip[] footstepSandClips;

    [Tooltip("Succión o hundimiento en arena movediza")]
    [SerializeField] private AudioClip quicksandSinkClip;

    [Tooltip("Trago de agua / cantimplora")]
    [SerializeField] private AudioClip drinkWaterClip;

    [Tooltip("Alerta / Linterna del coyote al detectar")]
    [SerializeField] private AudioClip alertStingClip;

    [Tooltip("Ladrido del perro guardián (variaciones)")]
    [SerializeField] private AudioClip dogBarkClip;

    [Tooltip("Daño o impacto recibido por Mateo (quejidos)")]
    [SerializeField] private AudioClip damageHurtClip;
    [SerializeField] private AudioClip[] damageHurtClips;

    [Tooltip("Game Over / Derrota")]
    [SerializeField] private AudioClip gameOverClip;

    [Tooltip("Victoria / Meta alcanzada")]
    [SerializeField] private AudioClip victoryClip;

    [Header("Coyotes")]
    [SerializeField] private AudioClip[] coyoteStepClips;
    [Tooltip("Murmullo cuando sospecha (¿Eh? ¿Quién anda ahí?)")]
    [SerializeField] private AudioClip[] coyoteSuspectClips;
    [Tooltip("Grito al descubrir a Mateo (¡Ahí está! / ¡Alto ahí!)")]
    [SerializeField] private AudioClip[] coyoteSpotClips;
    [Tooltip("Grito al atraparlo (¡Te tengo!)")]
    [SerializeField] private AudioClip[] coyoteCatchClips;

    [Header("Perros")]
    [SerializeField] private AudioClip[] dogBarkClips;
    [SerializeField] private AudioClip dogBiteClip;
    [SerializeField] private AudioClip dogSnoreClip;

    [Header("Fauna Nociva")]
    [SerializeField] private AudioClip snakeRattleClip;
    [SerializeField] private AudioClip snakeHissClip;
    [SerializeField] private AudioClip scorpionClickClip;
    [SerializeField] private AudioClip scorpionStingClip;
    [SerializeField] private AudioClip spiderSkitterClip;
    [SerializeField] private AudioClip spiderHissClip;

    [Header("Sonido según la distancia")]
    [Tooltip("Hasta esta distancia (unidades) el sonido se escucha a volumen completo")]
    [SerializeField] private float fullVolumeDistance = 1.2f;

    [Tooltip("A partir de esta distancia ya no se escucha")]
    [SerializeField] private float maxHearingDistance = 5f;

    [Tooltip("Cantidad de fuentes simultáneas para sonidos ubicados en el mundo")]
    [SerializeField] private int worldVoices = 10;

    // Claves compartidas con MainMenuController
    public const string PREF_MASTER_VOL = "Volume_Master";
    public const string PREF_MUSIC_VOL = "Volume_Music";
    public const string PREF_SFX_VOL = "Volume_SFX";

    private float musicVolume = 0.8f;
    private float sfxVolume = 0.9f;
    private const float MusicBaseVolume = 0.55f;

    private readonly List<AudioSource> worldSources = new List<AudioSource>();
    private AudioSource voiceSource;
    private int nextWorldSource;
    private readonly Dictionary<string, float> lastPlayTimes = new Dictionary<string, float>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureAudioSources();
        RefreshVolumes();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }

    private void Start()
    {
        PlayMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        PlayMusicForScene(scene.name);
    }

    private void PlayMusicForScene(string sceneName)
    {
        bool isLevel = sceneName.Contains("Level");
        AudioClip clip = isLevel ? desertNightBGM : menuBGM;
        if (clip != null) PlayBGM(clip);
        else StopBGM();
    }

    private void EnsureAudioSources()
    {
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            if (musicGroup != null) musicSource.outputAudioMixerGroup = musicGroup;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
            if (sfxGroup != null) sfxSource.outputAudioMixerGroup = sfxGroup;
        }

        if (footstepSource == null)
        {
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.loop = false;
            footstepSource.playOnAwake = false;
            if (sfxGroup != null) footstepSource.outputAudioMixerGroup = sfxGroup;
        }

        foreach (AudioSource s in new[] { musicSource, sfxSource, footstepSource })
        {
            s.spatialBlend = 0f;
            s.ignoreListenerPause = true;
        }

        // Canal exclusivo para las voces de los coyotes (un grito nuevo corta al anterior)
        voiceSource = gameObject.AddComponent<AudioSource>();
        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;
        if (sfxGroup != null) voiceSource.outputAudioMixerGroup = sfxGroup;

        for (int i = 0; i < worldVoices; i++)
        {
            AudioSource src = gameObject.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            if (sfxGroup != null) src.outputAudioMixerGroup = sfxGroup;
            worldSources.Add(src);
        }
    }

    // =========================================================================
    // VOLUMEN
    // =========================================================================

    /// <summary>Lee los volúmenes guardados en Opciones y los aplica.</summary>
    public void RefreshVolumes()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.85f);
        musicVolume = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.80f);
        sfxVolume = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.90f);
        if (musicSource != null) musicSource.volume = musicVolume * MusicBaseVolume;
    }

    // =========================================================================
    // CONTROL DE MÚSICA (BGM)
    // =========================================================================

    public void PlayBGM(AudioClip clip = null)
    {
        AudioClip toPlay = clip != null ? clip : desertNightBGM;
        if (toPlay == null || musicSource == null) return;

        if (musicSource.clip == toPlay && musicSource.isPlaying) return;

        musicSource.clip = toPlay;
        musicSource.loop = true;
        musicSource.volume = musicVolume * MusicBaseVolume;
        musicSource.Play();
    }

    public void StopBGM()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }

    // =========================================================================
    // EFECTOS DEL JUGADOR Y DEL MUNDO
    // =========================================================================

    /// <summary>
    /// Reproduce pasos en arena con variación sutil de tono (pitch) para evitar monotonía.
    /// </summary>
    public void PlayFootstep()
    {
        if (footstepSource == null) return;
        AudioClip clip = Pick(footstepSandClips);
        if (clip == null) return;

        footstepSource.pitch = Random.Range(0.90f, 1.10f);
        footstepSource.PlayOneShot(clip, 0.55f * sfxVolume);
    }

    /// <summary>Sonido de succión/hundimiento al entrar en arena movediza (con cooldown anti-spam).</summary>
    public void PlayQuicksand()
    {
        if (!CooldownReady("quicksand", 0.5f)) return;
        PlayOneShotSFX(quicksandSinkClip, 0.9f);
    }

    /// <summary>Un trago suave al recoger agua o suministros.</summary>
    public void PlayDrinkWater()
    {
        if (!CooldownReady("drink", 0.15f)) return;
        PlayOneShotSFX(drinkWaterClip, 0.5f, Random.Range(0.95f, 1.05f));
    }

    /// <summary>Golpe musical de alerta cuando Mateo es descubierto.</summary>
    public void PlayAlert()
    {
        if (!CooldownReady("alert", 2.5f)) return;
        PlayOneShotSFX(alertStingClip, 0.75f);
    }

    /// <summary>Ladrido sin ubicación (se conserva por compatibilidad; los perros usan PlayDogBarkAt).</summary>
    public void PlayDogBark()
    {
        if (!CooldownReady("dogbark", 1.0f)) return;
        PlayOneShotSFX(dogBarkClips != null && dogBarkClips.Length > 0 ? Pick(dogBarkClips) : dogBarkClip, 0.9f);
    }

    /// <summary>Quejido de Mateo al recibir daño.</summary>
    public void PlayDamage()
    {
        AudioClip clip = damageHurtClips != null && damageHurtClips.Length > 0 ? Pick(damageHurtClips) : damageHurtClip;
        PlayOneShotSFX(clip, 0.95f, Random.Range(0.96f, 1.06f));
    }

    /// <summary>Melodía de Game Over (la música de fondo se detiene).</summary>
    public void PlayGameOver()
    {
        StopBGM();
        PlayOneShotSFX(gameOverClip, 0.9f);
    }

    /// <summary>Melodía de victoria al llegar a la meta (la música de fondo se detiene).</summary>
    public void PlayVictory()
    {
        StopBGM();
        PlayOneShotSFX(victoryClip, 0.9f);
    }

    public void PlayCustomSFX(AudioClip clip, float volume = 1f)
    {
        PlayOneShotSFX(clip, volume);
    }

    private void PlayOneShotSFX(AudioClip clip, float volume, float pitch = 1f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.pitch = pitch;
        sfxSource.PlayOneShot(clip, volume * sfxVolume);
    }

    // =========================================================================
    // SONIDOS DE CRIATURAS (ubicados en el mundo)
    // =========================================================================

    public void PlayCoyoteStepAt(Vector3 pos) => PlayAt(Pick(coyoteStepClips), pos, 0.5f, 0.12f);

    public void PlayCoyoteSuspectAt(Vector3 pos, int ownerId)
    {
        if (!CooldownReady("suspect" + ownerId, 6f) || !CooldownReady("anyshout", 1.2f)) return;
        PlayVoiceAt(Pick(coyoteSuspectClips), pos, 0.9f, 1.6f, false);
    }

    public void PlayCoyoteSpotAt(Vector3 pos, int ownerId)
    {
        if (!CooldownReady("spot" + ownerId, 4f) || !CooldownReady("anyshout", 0.8f)) return;
        PlayVoiceAt(Pick(coyoteSpotClips), pos, 1f, 2f, true);
    }

    public void PlayCoyoteCatchAt(Vector3 pos)
    {
        PlayVoiceAt(Pick(coyoteCatchClips), pos, 1f, 3f, true);
    }

    public void PlayDogBarkAt(Vector3 pos) => PlayAt(Pick(dogBarkClips), pos, 0.85f, 0.1f, 1.4f);
    public void PlayDogBiteAt(Vector3 pos) => PlayAt(dogBiteClip, pos, 0.95f, 0.06f);
    public void PlayDogSnoreAt(Vector3 pos) => PlayAt(dogSnoreClip, pos, 0.45f, 0.08f);

    /// <summary>Aviso de la fauna cuando Mateo se acerca (cascabel, chasquidos, patitas).</summary>
    public void PlayFaunaAlertAt(HazardFauna.FaunaType type, Vector3 pos)
    {
        switch (type)
        {
            case HazardFauna.FaunaType.Snake: PlayAt(snakeRattleClip, pos, 1f, 0.06f); break;
            case HazardFauna.FaunaType.Scorpion: PlayAt(scorpionClickClip, pos, 1f, 0.1f); break;
            default: PlayAt(spiderSkitterClip, pos, 1f, 0.12f); break;
        }
    }

    /// <summary>Sonido de ataque de la fauna (siseo, aguijonazo, mordida de araña).</summary>
    public void PlayFaunaAttackAt(HazardFauna.FaunaType type, Vector3 pos)
    {
        switch (type)
        {
            case HazardFauna.FaunaType.Snake: PlayAt(snakeHissClip, pos, 1f, 0.06f); break;
            case HazardFauna.FaunaType.Scorpion: PlayAt(scorpionStingClip, pos, 1f, 0.08f); break;
            default: PlayAt(spiderHissClip, pos, 1f, 0.08f); break;
        }
    }

    /// <summary>Patitas de la araña y el alacrán al caminar (muy bajito).</summary>
    public void PlayFaunaMoveAt(HazardFauna.FaunaType type, Vector3 pos)
    {
        if (type == HazardFauna.FaunaType.Snake) return;
        PlayAt(type == HazardFauna.FaunaType.Spider ? spiderSkitterClip : scorpionClickClip, pos, 0.4f, 0.15f, 0.7f);
    }

    /// <summary>
    /// Reproduce un sonido ubicado en el mundo: se atenúa con la distancia a la cámara y se panea a izquierda/derecha.
    /// </summary>
    public void PlayAt(AudioClip clip, Vector3 worldPos, float volume, float pitchVariation = 0.05f, float rangeMultiplier = 1f)
    {
        if (clip == null || worldSources.Count == 0) return;

        Vector3 listener = GetListenerPosition();
        float distance = Vector2.Distance(listener, worldPos);
        float far = maxHearingDistance * rangeMultiplier;
        float near = fullVolumeDistance * rangeMultiplier;
        float attenuation = 1f - Mathf.Clamp01((distance - near) / Mathf.Max(0.01f, far - near));
        if (attenuation <= 0.01f) return;

        AudioSource src = worldSources[nextWorldSource];
        nextWorldSource = (nextWorldSource + 1) % worldSources.Count;

        src.panStereo = Mathf.Clamp((worldPos.x - listener.x) / 3f, -0.8f, 0.8f);
        src.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        src.PlayOneShot(clip, volume * attenuation * attenuation * sfxVolume);
    }

    /// <summary>
    /// Voz de un coyote ubicada en el mundo. Si interrupt es verdadero, corta la voz que estuviera sonando.
    /// </summary>
    private void PlayVoiceAt(AudioClip clip, Vector3 worldPos, float volume, float rangeMultiplier, bool interrupt)
    {
        if (clip == null || voiceSource == null) return;
        if (voiceSource.isPlaying && !interrupt) return;

        Vector3 listener = GetListenerPosition();
        float distance = Vector2.Distance(listener, worldPos);
        float far = maxHearingDistance * rangeMultiplier;
        float near = fullVolumeDistance * rangeMultiplier;
        float attenuation = 1f - Mathf.Clamp01((distance - near) / Mathf.Max(0.01f, far - near));
        if (attenuation <= 0.01f) return;

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.panStereo = Mathf.Clamp((worldPos.x - listener.x) / 3f, -0.8f, 0.8f);
        voiceSource.pitch = Random.Range(0.97f, 1.03f);
        voiceSource.volume = volume * attenuation * sfxVolume;
        voiceSource.Play();
    }

    private Vector3 GetListenerPosition()
    {
        Camera cam = Camera.main;
        return cam != null ? cam.transform.position : Vector3.zero;
    }

    private bool CooldownReady(string key, float cooldown)
    {
        float now = Time.unscaledTime;
        if (lastPlayTimes.TryGetValue(key, out float last) && now - last < cooldown) return false;
        lastPlayTimes[key] = now;
        return true;
    }

    private static AudioClip Pick(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0) return null;
        return clips[Random.Range(0, clips.Length)];
    }
}
