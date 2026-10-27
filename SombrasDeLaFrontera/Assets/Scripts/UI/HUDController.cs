using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador visual del HUD en pantalla:
/// - Muestra los 3 corazones de vida (se apagan o vacían al recibir daño).
/// - Muestra la barra deslizable (Slider) de hidratación (se vacía con el tiempo y sube con cantimploras/botellas).
///   Cuando queda poca agua la barra palpita en naranja.
/// - Destello rojo en pantalla al recibir daño.
/// - Indicador "OCULTO" cuando Mateo está a cubierto.
/// - Incluye atajo de teclado (tecla H) para probar daño en tiempo real.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Referencias de Salud (Corazones)")]
    [Tooltip("Arreglo con las 3 imágenes de corazones en el HUD (de izquierda a derecha)")]
    [SerializeField] private Image[] heartImages;

    [Tooltip("Color del corazón lleno")]
    [SerializeField] private Color fullHeartColor = Color.white;

    [Tooltip("Color del corazón vacío/perdido (opacidad tenue o gris)")]
    [SerializeField] private Color emptyHeartColor = new Color(0.2f, 0.2f, 0.2f, 0.35f);

    [Header("Referencias de Hidratación (Barra de Agua)")]
    [Tooltip("Slider que representa la barra de agua")]
    [SerializeField] private Slider waterSlider;

    [Tooltip("Imagen de relleno del Slider (para cambiar color si está crítico)")]
    [SerializeField] private Image waterFillImage;

    [Tooltip("Color del agua normal")]
    [SerializeField] private Color normalWaterColor = new Color(0.18f, 0.65f, 0.95f, 1f);

    [Tooltip("Color del agua cuando está por debajo del 25% (alerta de sed)")]
    [SerializeField] private Color criticalWaterColor = new Color(0.95f, 0.35f, 0.15f, 1f);

    [Header("Retroalimentación Visual")]
    [Tooltip("Imagen roja a pantalla completa que destella al recibir daño")]
    [SerializeField] private Image damageFlashImage;

    [Tooltip("Opacidad máxima del destello de daño")]
    [SerializeField] private float damageFlashAlpha = 0.35f;

    [Tooltip("Grupo del indicador 'OCULTO' que aparece mientras Mateo está a cubierto")]
    [SerializeField] private CanvasGroup stealthIndicator;

    [Header("Pruebas y Depuración")]
    [Tooltip("Presiona esta tecla durante el juego para simular daño (-1 corazón)")]
    [SerializeField] private KeyCode testDamageKey = KeyCode.H;

    private PlayerHealth cachedPlayerHealth;
    private float flashStartTime = -10f;
    private float currentWaterRatio = 1f;
    private const float FlashDuration = 0.45f;

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHearts;
        PlayerHealth.OnDamaged += HandleDamaged;
        WaterSystem.OnWaterChanged += UpdateWaterBar;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHearts;
        PlayerHealth.OnDamaged -= HandleDamaged;
        WaterSystem.OnWaterChanged -= UpdateWaterBar;
    }

    private void Start()
    {
        cachedPlayerHealth = FindAnyObjectByType<PlayerHealth>();

        // Si ya existen datos iniciales en la escena, actualizar inmediatamente
        if (cachedPlayerHealth != null)
        {
            UpdateHearts(cachedPlayerHealth.currentHealth, cachedPlayerHealth.MaxHealth);
        }

        WaterSystem water = FindAnyObjectByType<WaterSystem>();
        if (water != null)
        {
            UpdateWaterBar(water.currentWater, 100f);
        }

        if (damageFlashImage != null)
        {
            Color c = damageFlashImage.color;
            c.a = 0f;
            damageFlashImage.color = c;
            damageFlashImage.raycastTarget = false;
        }

        if (stealthIndicator != null) stealthIndicator.alpha = 0f;
    }

    private void Update()
    {
        bool damageKeyPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
        {
            damageKeyPressed = true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!damageKeyPressed && Input.GetKeyDown(testDamageKey))
        {
            damageKeyPressed = true;
        }
#endif

        if (damageKeyPressed && Time.timeScale > 0f)
        {
            if (cachedPlayerHealth == null)
            {
                cachedPlayerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (cachedPlayerHealth != null)
            {
                Debug.Log("<color=orange>[HUDController] Tecla 'H' presionada -> Probando daño (-1 corazón).</color>");
                cachedPlayerHealth.TakeDamage(1);
            }
        }

        UpdateDamageFlash();
        UpdateStealthIndicator();
        UpdateCriticalWaterPulse();
    }

    private void HandleDamaged(int amount)
    {
        flashStartTime = Time.unscaledTime;
    }

    private void UpdateDamageFlash()
    {
        if (damageFlashImage == null) return;

        float t = (Time.unscaledTime - flashStartTime) / FlashDuration;
        Color c = damageFlashImage.color;
        c.a = t < 1f ? damageFlashAlpha * (1f - t) : 0f;
        damageFlashImage.color = c;
    }

    private void UpdateStealthIndicator()
    {
        if (stealthIndicator == null) return;

        bool hidden = CoverZone2D.isHiding && (cachedPlayerHealth == null || !cachedPlayerHealth.isDead);
        float target = hidden ? 1f : 0f;
        stealthIndicator.alpha = Mathf.MoveTowards(stealthIndicator.alpha, target, Time.unscaledDeltaTime * 6f);
    }

    private void UpdateCriticalWaterPulse()
    {
        if (waterFillImage == null || currentWaterRatio > 0.25f) return;

        // Palpitar entre naranja y rojo intenso para llamar la atención
        float pulse = (Mathf.Sin(Time.unscaledTime * 8f) + 1f) * 0.5f;
        waterFillImage.color = Color.Lerp(criticalWaterColor, new Color(1f, 0.12f, 0.08f, 1f), pulse);
    }

    /// <summary>
    /// Actualiza visualmente los 3 corazones de vida en pantalla.
    /// </summary>
    public void UpdateHearts(int currentHealth, int maxHealth)
    {
        if (heartImages == null || heartImages.Length == 0) return;

        for (int i = 0; i < heartImages.Length; i++)
        {
            if (heartImages[i] == null) continue;

            if (i < currentHealth)
            {
                // Corazón activo/lleno
                heartImages[i].color = fullHeartColor;
                heartImages[i].transform.localScale = Vector3.one;
            }
            else
            {
                // Corazón perdido
                heartImages[i].color = emptyHeartColor;
                heartImages[i].transform.localScale = new Vector3(0.85f, 0.85f, 1f);
            }
        }
    }

    /// <summary>
    /// Actualiza la barra de hidratación continua.
    /// </summary>
    public void UpdateWaterBar(float currentWater, float maxWater)
    {
        if (waterSlider != null && maxWater > 0f)
        {
            float fillRatio = Mathf.Clamp01(currentWater / maxWater);
            currentWaterRatio = fillRatio;
            waterSlider.value = fillRatio;

            // Cambiar a color de alerta si queda menos de 25% de agua
            if (waterFillImage != null && fillRatio > 0.25f)
            {
                waterFillImage.color = normalWaterColor;
            }
        }
    }
}
