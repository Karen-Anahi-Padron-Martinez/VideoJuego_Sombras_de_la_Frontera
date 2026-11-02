using UnityEngine;

/// <summary>
/// Lancha de la Patrulla Fronteriza (Nivel 2).
/// - PatrolAI la mueve de lado a lado por su carril horizontal: sale de la pantalla por una orilla, espera y regresa.
/// - Su reflector (VisionCone2D en modo reflector) barre el agua frente a la proa; si alumbra a Mateo en la superficie
///   y al descubierto más de medio segundo, lo captura. Sumergido o escondido bajo un tronco o junco, la luz pasa de largo.
/// - 1.5 s antes de entrar a la pantalla aparece un brillo intermitente en la orilla por donde va a cruzar.
/// - El motor suena más fuerte mientras más cerca está la lancha de la cámara.
/// </summary>
[RequireComponent(typeof(PatrolAI))]
public class PatrolBoat : MonoBehaviour
{
    [Header("Animación")]
    [Tooltip("Cuadros de la lancha con la estela de espuma")]
    [SerializeField] private Sprite[] frames;

    [SerializeField] private float framesPerSecond = 8f;

    [Tooltip("Sprite del casco (lo voltea PatrolAI según hacia dónde avanza)")]
    [SerializeField] private SpriteRenderer hull;

    [Header("Reflector")]
    [Tooltip("Pivote del reflector (PatrolAI lo orienta hacia donde avanza la lancha)")]
    [SerializeField] private Transform reflectorPivot;

    [Tooltip("Posición del reflector respecto al centro de la lancha mirando a la derecha (en la proa)")]
    [SerializeField] private Vector2 reflectorOffset = new Vector2(0.5f, 0.1f);

    [Header("Aviso Previo")]
    [Tooltip("Brillo que parpadea en la orilla antes de que la lancha entre a la pantalla")]
    [SerializeField] private SpriteRenderer warningGlow;

    [Tooltip("Segundos de anticipación del aviso")]
    [SerializeField] private float warningLeadTime = 1.5f;

    [Tooltip("Medio ancho del río visible en pantalla (unidades)")]
    [SerializeField] private float riverHalfWidth = 2.86f;

    [Tooltip("Distancia del brillo al borde de la pantalla")]
    [SerializeField] private float glowInset = 0.3f;

    [Header("Motor")]
    [SerializeField] private float engineVolume = 0.35f;

    [Tooltip("Distancia a la cámara a partir de la cual ya no se escucha el motor")]
    [SerializeField] private float engineHearingRange = 7f;

    private PatrolAI patrol;
    private AudioSource engine;
    private Camera cam;
    private Vector2 lastPosition;
    private float stoppedSince;
    private bool wasMoving = true;
    private Color glowBaseColor = Color.white;

    private static AudioClip engineClip;
    private float basePitch = 1f;
    private float lastCameraDistance = -1f;

    /// <summary>Verdadero mientras el brillo de aviso está encendido (la lancha está por entrar a la pantalla).</summary>
    public bool IsWarning => warningGlow != null && warningGlow.enabled;

    private void Awake()
    {
        patrol = GetComponent<PatrolAI>();
        if (hull == null) hull = GetComponentInChildren<SpriteRenderer>();
        if (warningGlow != null)
        {
            glowBaseColor = warningGlow.color;
            warningGlow.enabled = false;
        }

        engine = gameObject.AddComponent<AudioSource>();
        engine.clip = GetEngineClip();
        engine.loop = true;
        engine.playOnAwake = false;
        engine.spatialBlend = 0f;
        engine.volume = 0f;
        basePitch = Random.Range(0.93f, 1.07f);  // Cada lancha con su propio tono de motor
        engine.pitch = basePitch;
    }

    private void Start()
    {
        cam = Camera.main;
        lastPosition = transform.position;
        stoppedSince = Time.time;
        // Por el canal de Efectos del mezclador: se apaga bajo el agua como todo lo demás
        if (AudioManager.Instance != null && AudioManager.Instance.SfxGroup != null)
            engine.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
        // Motor fuera de borda grabado en el AudioManager; si no está, se usa el generado por código
        if (AudioManager.Instance != null && AudioManager.Instance.BoatEngineClip != null)
            engine.clip = AudioManager.Instance.BoatEngineClip;
        if (engine.clip != null) engine.time = Random.Range(0f, engine.clip.length * 0.95f);  // Las lanchas no suenan sincronizadas
        engine.Play();
    }

    private void Update()
    {
        Animate();
        PlaceReflector();
        UpdateWarning();
        UpdateEngine();
        lastPosition = transform.position;
    }

    private void Animate()
    {
        if (hull == null || frames == null || frames.Length == 0) return;
        int index = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
        if (hull.sprite != frames[index]) hull.sprite = frames[index];
    }

    /// <summary>El reflector va en la proa: cambia de lado cuando la lancha da la vuelta.</summary>
    private void PlaceReflector()
    {
        if (reflectorPivot == null || hull == null) return;
        float side = hull.flipX ? -1f : 1f;
        reflectorPivot.localPosition = new Vector3(reflectorOffset.x * side, reflectorOffset.y, 0f);
    }

    /// <summary>
    /// Enciende el brillo en la orilla cuando la lancha está fuera de la pantalla y le faltan menos de
    /// warningLeadTime segundos para entrar (contando lo que le queda de espera en el extremo de su carril).
    /// </summary>
    private void UpdateWarning()
    {
        if (warningGlow == null) return;

        Vector2 pos = transform.position;
        Vector2 delta = pos - lastPosition;
        bool moving = delta.sqrMagnitude > 0.000001f;
        if (wasMoving && !moving) stoppedSince = Time.time;
        wasMoving = moving;

        float halfLength = hull != null ? hull.bounds.extents.x * 0.6f : 0.6f;
        float distanceToScreen = Mathf.Abs(pos.x) - halfLength - riverHalfWidth;
        float speed = Mathf.Max(0.1f, patrol.moveSpeed);

        float eta = -1f;
        if (distanceToScreen > 0f)
        {
            if (moving && Mathf.Sign(delta.x) != Mathf.Sign(pos.x))
            {
                eta = distanceToScreen / speed;                                    // Ya viene hacia el río
            }
            else if (!moving && !patrol.isAlerted)
            {
                float remainingWait = Mathf.Max(0f, patrol.waitTimeAtWaypoint - (Time.time - stoppedSince));
                eta = remainingWait + distanceToScreen / speed;                    // Esperando en el extremo
            }
        }

        bool show = eta >= 0f && eta <= warningLeadTime;
        warningGlow.enabled = show;
        if (!show) return;

        warningGlow.transform.position = new Vector3(Mathf.Sign(pos.x) * (riverHalfWidth - glowInset), pos.y, 0f);
        float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.time * 7f));
        Color c = glowBaseColor;
        c.a = glowBaseColor.a * pulse;
        warningGlow.color = c;
        warningGlow.transform.localScale = Vector3.one * Mathf.Lerp(0.85f, 1.15f, pulse);
    }

    /// <summary>
    /// Volumen según la distancia a la cámara (más fuerte al acercarse) y efecto Doppler:
    /// el tono sube mientras la lancha se acerca y baja cuando se aleja.
    /// </summary>
    private void UpdateEngine()
    {
        if (engine == null) return;
        if (cam == null) cam = Camera.main;

        float volume = 0f;
        float targetPitch = basePitch;
        if (cam != null && Time.timeScale > 0f)
        {
            float distance = Vector2.Distance(cam.transform.position, transform.position);
            float closeness = Mathf.Clamp01(1f - distance / engineHearingRange);
            float sfx = AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f;
            volume = engineVolume * Mathf.Pow(closeness, 1.6f) * sfx;

            if (lastCameraDistance >= 0f && Time.deltaTime > 0f)
            {
                float approachSpeed = (lastCameraDistance - distance) / Time.deltaTime;  // Positivo al acercarse
                targetPitch = basePitch * (1f + Mathf.Clamp(approachSpeed * 0.035f, -0.08f, 0.08f));
            }
            lastCameraDistance = distance;
        }
        engine.volume = Mathf.MoveTowards(engine.volume, volume, Time.unscaledDeltaTime * 2f);
        engine.pitch = Mathf.Lerp(engine.pitch, targetPitch, Time.unscaledDeltaTime * 3f);
    }

    /// <summary>
    /// Sonido de motor fuera de borda generado por código (un segundo en bucle):
    /// zumbido grave con su armónico, un poco de ruido de agua y el "pop-pop" del escape.
    /// </summary>
    private static AudioClip GetEngineClip()
    {
        if (engineClip != null) return engineClip;

        const int sampleRate = 22050;
        var samples = new float[sampleRate];
        var random = new System.Random(7);
        for (int i = 0; i < samples.Length; i++)
        {
            float t = i / (float)sampleRate;
            float saw = 2f * ((t * 48f) % 1f) - 1f;                     // Zumbido del motor (48 Hz)
            float hum = Mathf.Sin(2f * Mathf.PI * 96f * t);              // Armónico
            float noise = (float)(random.NextDouble() * 2.0 - 1.0);      // Agua y vibración
            float pop = 1f + 0.35f * Mathf.Sin(2f * Mathf.PI * 8f * t);  // Escape (8 golpes por segundo)
            samples[i] = (0.45f * saw + 0.3f * hum + 0.12f * noise) * pop * 0.5f;
        }

        engineClip = AudioClip.Create("Motor_Lancha", samples.Length, 1, sampleRate, false);
        engineClip.SetData(samples, 0);
        return engineClip;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.8f);
        Gizmos.DrawLine(new Vector3(-riverHalfWidth, transform.position.y, 0f), new Vector3(riverHalfWidth, transform.position.y, 0f));
    }
}
