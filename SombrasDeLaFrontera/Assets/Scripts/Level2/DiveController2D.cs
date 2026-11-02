using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Buceo de Mateo en el Río Bravo (Nivel 2), la mecánica principal de sigilo acuático:
/// - Mantener el botón de acción (Espacio / botón A del control) lo sumerge; al soltarlo sale a la superficie.
/// - Sumergido (isDiving) los reflectores de las lanchas no lo ven, nada al 60% de velocidad, se ve como una silueta
///   oscura translúcida bajo los troncos y la fauna, y suelta burbujas.
/// - Tiene 4 segundos de aire que se recargan solos en la superficie. Los carrizos dan segundos de aire extra
///   (se gastan después del aire normal y no se recargan).
/// - Si el aire llega a 0 bajo el agua, Mateo se ahoga (Game Over).
/// </summary>
[RequireComponent(typeof(SwimController2D))]
public class DiveController2D : MonoBehaviour
{
    [Header("Aire")]
    [Tooltip("Segundos máximos de inmersión continua")]
    [SerializeField] private float maxAirSeconds = 4f;

    [Tooltip("Segundos de aire que recupera por cada segundo en la superficie")]
    [SerializeField] private float rechargePerSecond = 2f;

    [Tooltip("Aire mínimo (segundos) para volver a sumergirse después de salir a respirar")]
    [SerializeField] private float minAirToDive = 0.4f;

    [Header("Buceo")]
    [Tooltip("Velocidad de nado bajo el agua respecto a la superficie (0.6 = 60%)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float diveSpeedMultiplier = 0.6f;

    [Tooltip("Color de la silueta de Mateo bajo el agua (oscura y translúcida)")]
    [SerializeField] private Color diveColor = new Color(0.2f, 0.32f, 0.42f, 0.5f);

    [Tooltip("Capa de dibujado bajo el agua: por debajo de troncos, fauna y lanchas")]
    [SerializeField] private string underwaterSortingLayer = "Background";

    [Tooltip("Burbujas que suelta mientras bucea")]
    [SerializeField] private ParticleSystem bubbles;

    [Header("Referencias")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    private SwimController2D swimmer;
    private PlayerHealth health;
    private bool virtualDive;
    private string surfaceSortingLayer;

    public float MaxAir => maxAirSeconds;
    public float CurrentAir { get; private set; }
    public float BonusAir { get; private set; }
    public bool IsDiving { get; private set; }

    private void Awake()
    {
        swimmer = GetComponent<SwimController2D>();
        health = GetComponent<PlayerHealth>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) surfaceSortingLayer = spriteRenderer.sortingLayerName;
        CurrentAir = maxAirSeconds;
    }

    private void Update()
    {
        if (Time.timeScale <= 0f) return;
        if (health != null && health.isDead) return;

        bool wantsDive = ReadDiveInput();
        if (!IsDiving)
        {
            if (wantsDive && swimmer.inWater && CurrentAir + BonusAir >= minAirToDive) StartDive();
        }
        else if (!wantsDive || !swimmer.inWater)
        {
            Surface();
        }

        if (IsDiving)
        {
            // Primero se gasta el aire normal y después el extra de los carrizos
            float use = Time.deltaTime;
            float fromMain = Mathf.Min(CurrentAir, use);
            CurrentAir -= fromMain;
            BonusAir = Mathf.Max(0f, BonusAir - (use - fromMain));
            AudioManager.Instance?.PlayBubbles();  // Tiene su propio intervalo

            if (CurrentAir <= 0f && BonusAir <= 0f) Drown();
        }
        else
        {
            CurrentAir = Mathf.MoveTowards(CurrentAir, maxAirSeconds, rechargePerSecond * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        // La silueta se aplica después de todo (incluido el parpadeo de invulnerabilidad de PlayerHealth)
        if (IsDiving && spriteRenderer != null) spriteRenderer.color = diveColor;
    }

    private bool ReadDiveInput()
    {
        bool pressed = virtualDive;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) pressed = true;
        if (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed) pressed = true;
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.Space)) pressed = true;
#endif
        return pressed;
    }

    private void StartDive()
    {
        IsDiving = true;
        swimmer.isDiving = true;
        swimmer.speedMultiplier = diveSpeedMultiplier;
        if (spriteRenderer != null) spriteRenderer.sortingLayerName = underwaterSortingLayer;
        if (bubbles != null) bubbles.Play();
        AudioManager.Instance?.PlayDive();
        AudioManager.Instance?.SetUnderwater(true);   // Todo el sonido se oye apagado bajo el agua
        Debug.Log($"[DiveController2D] Mateo se sumergió (aire: {CurrentAir + BonusAir:F1}s).");
    }

    private void Surface()
    {
        IsDiving = false;
        swimmer.isDiving = false;
        swimmer.speedMultiplier = 1f;
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerName = surfaceSortingLayer;
            spriteRenderer.color = Color.white;
        }
        if (bubbles != null) bubbles.Stop();
        AudioManager.Instance?.SetUnderwater(false);
        AudioManager.Instance?.PlaySurface();
        Debug.Log($"[DiveController2D] Mateo salió a respirar (aire: {CurrentAir + BonusAir:F1}s).");
    }

    private void Drown()
    {
        Debug.Log("[DiveController2D] ¡Mateo se quedó sin aire bajo el agua!");
        if (bubbles != null) bubbles.Stop();
        AudioManager.Instance?.PlayDrown();   // Se oye apagado; el Game Over regresa el sonido a la superficie
        if (health != null) health.Kill(DefeatReason.Drowning);
        IsDiving = false;
    }

    /// <summary>Aire extra del carrizo (PickupItem lo envía con SendMessage).</summary>
    public void AddAirBonus(float seconds)
    {
        BonusAir += seconds;
        Debug.Log($"[DiveController2D] Aire extra del carrizo: +{seconds}s (extra disponible: {BonusAir:F1}s).");
    }

    /// <summary>Botón de buceo táctil en pantalla.</summary>
    public void SetVirtualDive(bool pressed)
    {
        virtualDive = pressed;
    }

    private void OnDisable()
    {
        // Al morir (PlayerHealth desactiva el nado) se dejan de soltar burbujas
        if (bubbles != null) bubbles.Stop();
        if (IsDiving) AudioManager.Instance?.SetUnderwater(false);
    }
}
