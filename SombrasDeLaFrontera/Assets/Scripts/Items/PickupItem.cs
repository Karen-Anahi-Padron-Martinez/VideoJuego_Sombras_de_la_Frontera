using UnityEngine;

/// <summary>
/// Gestiona los consumibles del suelo en el desierto y los flotantes del Río Bravo:
/// - Cantimplora: Recupera agua (+25%).
/// - Tuna: Recupera agua (+7%) y otorga boost de velocidad por 3 segundos.
/// - Botiquín: Recupera salud (+1 corazón de vida).
/// - Bidón (Nivel 2): Impulso de nado (+50% de velocidad) por unos segundos.
/// - Bolsa impermeable (Nivel 2): Recupera salud (+1 corazón de vida).
/// - Carrizo (Nivel 2): +3 segundos de aire extra para bucear.
/// </summary>
public class PickupItem : MonoBehaviour
{
    public enum ItemType
    {
        Cantimplora,
        Botella,
        Tuna,
        Botiquin,
        Bidon,
        BolsaImpermeable,
        Carrizo
    }

    [Header("Tipo de Ítem")]
    [SerializeField] private ItemType itemType = ItemType.Cantimplora;

    [Header("Parámetros de Recuperación")]
    [Tooltip("Cantidad de agua que recarga este ítem (Cantimplora: 25%, Botella: 10%, Tuna: 7%)")]
    [SerializeField] private float waterAmount = 25f;

    [Tooltip("Cantidad de vidas que recupera (Botiquín y Bolsa impermeable)")]
    [SerializeField] private int healthAmount = 1;

    [Header("Parámetros de Boost (Tuna y Bidón)")]
    [Tooltip("Multiplicador de velocidad (Tuna: 1.35 = +35%; Bidón: 1.5 = +50%)")]
    [SerializeField] private float speedBoost = 1.35f;

    [Tooltip("Duración en segundos del boost de velocidad")]
    [SerializeField] private float boostDuration = 3f;

    [Header("Parámetros de Aire (Solo Carrizo)")]
    [Tooltip("Segundos de aire extra que otorga el carrizo para bucear")]
    [SerializeField] private float airBonusSeconds = 3f;

    [Header("Efectos Opcionales")]
    [SerializeField] private AudioClip pickupSound;

    private bool isCollected = false;

    public ItemType Type => itemType;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        // Comprobar si el que entra es Mateo (o un collider hijo de Mateo): caminando en el desierto o nadando en el río
        PlayerHealth health = collision.GetComponentInParent<PlayerHealth>();
        if (health == null || health.isDead) return;

        PlayerController2D player = health.GetComponent<PlayerController2D>();
        SwimController2D swimmer = health.GetComponent<SwimController2D>();

        isCollected = true;

        switch (itemType)
        {
            case ItemType.Cantimplora:
                WaterSystem waterSys = health.GetComponent<WaterSystem>();
                if (waterSys != null)
                {
                    waterSys.AddWater(waterAmount);
                }
                Debug.Log($"[PickupItem] Mateo recogió Cantimplora (+{waterAmount}% agua).");
                break;

            case ItemType.Botella:
                WaterSystem botWater = health.GetComponent<WaterSystem>();
                if (botWater != null)
                {
                    botWater.AddWater(waterAmount);
                }
                Debug.Log($"[PickupItem] Mateo bebió una Botella de agua (+{waterAmount}% agua).");
                break;

            case ItemType.Tuna:
                WaterSystem tunaWater = health.GetComponent<WaterSystem>();
                if (tunaWater != null)
                {
                    tunaWater.AddWater(waterAmount);
                }
                if (player != null) player.ApplySpeedBoost(speedBoost, boostDuration);
                Debug.Log($"[PickupItem] Mateo comió una Tuna (+{waterAmount}% agua y boost de velocidad).");
                break;

            case ItemType.Botiquin:
                health.Heal(healthAmount);
                AudioManager.Instance?.PlayDrinkWater();
                Debug.Log($"[PickupItem] Mateo recogió Botiquín (+{healthAmount} vida).");
                break;

            case ItemType.Bidon:
                if (swimmer != null) swimmer.ApplySpeedBoost(speedBoost, boostDuration);
                else if (player != null) player.ApplySpeedBoost(speedBoost, boostDuration);
                AudioManager.Instance?.PlayDrinkWater();
                Debug.Log($"[PickupItem] Mateo se impulsó con un Bidón (x{speedBoost:F2} de velocidad por {boostDuration}s).");
                break;

            case ItemType.BolsaImpermeable:
                health.Heal(healthAmount);
                AudioManager.Instance?.PlayDrinkWater();
                Debug.Log($"[PickupItem] Mateo recogió una Bolsa impermeable (+{healthAmount} vida).");
                break;

            case ItemType.Carrizo:
                // El medidor de aire (DiveController2D) recibe el aire extra
                health.SendMessage("AddAirBonus", airBonusSeconds, SendMessageOptions.DontRequireReceiver);
                AudioManager.Instance?.PlayDrinkWater();
                Debug.Log($"[PickupItem] Mateo recogió un Carrizo (+{airBonusSeconds}s de aire para bucear).");
                break;
        }

        // Reproducir sonido si existe AudioSource global o en la cámara
        if (pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        // Destruir el ítem del suelo
        Destroy(gameObject);
    }
}
