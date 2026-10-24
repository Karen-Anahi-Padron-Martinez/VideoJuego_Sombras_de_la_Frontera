using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador visual del HUD en pantalla:
/// - Muestra los 3 corazones de vida (se apagan o vacían al recibir daño).
/// - Muestra la barra deslizable (Slider) de hidratación (se vacía con el tiempo y sube con cantimploras/botellas).
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

    [Header("Pruebas y Depuración")]
    [Tooltip("Presiona esta tecla durante el juego para simular daño (-1 corazón)")]
    [SerializeField] private KeyCode testDamageKey = KeyCode.H;

    private PlayerHealth cachedPlayerHealth;

    private void OnEnable()
    {
        PlayerHealth.OnHealthChanged += UpdateHearts;
        WaterSystem.OnWaterChanged += UpdateWaterBar;
    }

    private void OnDisable()
    {
        PlayerHealth.OnHealthChanged -= UpdateHearts;
        WaterSystem.OnWaterChanged -= UpdateWaterBar;
    }

    private void Start()
    {
        cachedPlayerHealth = FindAnyObjectByType<PlayerHealth>();

        // Si ya existen datos iniciales en la escena, actualizar inmediatamente
        if (cachedPlayerHealth != null)
        {
            UpdateHearts(cachedPlayerHealth.currentHealth, 3);
        }

        WaterSystem water = FindAnyObjectByType<WaterSystem>();
        if (water != null)
        {
            UpdateWaterBar(water.currentWater, 100f);
        }
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

        if (damageKeyPressed)
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
            waterSlider.value = fillRatio;

            // Cambiar a color de alerta si queda menos de 25% de agua
            if (waterFillImage != null)
            {
                waterFillImage.color = (fillRatio <= 0.25f) ? criticalWaterColor : normalWaterColor;
            }
        }
    }
}
