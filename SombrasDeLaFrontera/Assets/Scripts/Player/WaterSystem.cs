using UnityEngine;

/// <summary>
/// Gestiona la deshidratación progresiva de Mateo a lo largo del desierto.
/// El medidor se vacía continuamente con el tiempo y causa Game Over al llegar a 0%.
/// Puede recargarse al recoger cantimploras o tunas del suelo.
/// </summary>
public class WaterSystem : MonoBehaviour
{
    [Header("Configuración de Hidratación")]
    [Tooltip("Capacidad máxima de agua (porcentaje base 100)")]
    [SerializeField] private float maxWater = 100f;

    [Tooltip("Cantidad de agua consumida por segundo en el desierto")]
    [SerializeField] private float waterDrainRate = 1.5f;

    [Header("Estado")]
    public float currentWater { get; private set; }
    public bool isDraining = true;
    public bool isDehydrated { get; private set; }

    // Eventos estáticos para el HUD y GameManager
    public static event System.Action<float, float> OnWaterChanged; // (actual, máxima)
    public static event System.Action OnDehydrationDied;

    private void Awake()
    {
        currentWater = maxWater;
    }

    private void Start()
    {
        // Notificar agua inicial al HUD
        OnWaterChanged?.Invoke(currentWater, maxWater);
    }

    private void Update()
    {
        if (!isDraining || isDehydrated) return;

        // Vaciado progresivo por tiempo
        if (currentWater > 0f)
        {
            currentWater -= waterDrainRate * Time.deltaTime;
            currentWater = Mathf.Max(0f, currentWater);

            OnWaterChanged?.Invoke(currentWater, maxWater);

            if (currentWater <= 0f)
            {
                DieFromDehydration();
            }
        }
    }

    /// <summary>
    /// Rellena el medidor de agua al recoger un ítem consumible.
    /// </summary>
    public void AddWater(float amount)
    {
        if (isDehydrated) return;

        currentWater = Mathf.Min(maxWater, currentWater + amount);
        Debug.Log($"[WaterSystem] Mateo bebió agua (+{amount:F1}%). Nivel actual: {currentWater:F1}%/{maxWater:F1}%");
        OnWaterChanged?.Invoke(currentWater, maxWater);
    }

    private void DieFromDehydration()
    {
        isDehydrated = true;
        isDraining = false;
        Debug.Log("[WaterSystem] ¡GAME OVER! Mateo ha colapsado por deshidratación extrema en el desierto.");

        // Desactivar movimiento del jugador
        PlayerController2D controller = GetComponent<PlayerController2D>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        OnDehydrationDied?.Invoke();
    }
}
