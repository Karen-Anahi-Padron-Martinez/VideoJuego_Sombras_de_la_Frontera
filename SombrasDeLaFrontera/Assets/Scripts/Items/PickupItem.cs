using UnityEngine;

/// <summary>
/// Gestiona los consumibles del suelo en el desierto:
/// - Cantimplora: Recupera agua (+25%).
/// - Tuna: Recupera agua (+7%) y otorga boost de velocidad por 3 segundos.
/// - Botiquín: Recupera salud (+1 corazón de vida).
/// </summary>
public class PickupItem : MonoBehaviour
{
    public enum ItemType
    {
        Cantimplora,
        Botella,
        Tuna,
        Botiquin
    }

    [Header("Tipo de Ítem")]
    [SerializeField] private ItemType itemType = ItemType.Cantimplora;

    [Header("Parámetros de Recuperación")]
    [Tooltip("Cantidad de agua que recarga este ítem (Cantimplora: 25%, Botella: 10%, Tuna: 7%)")]
    [SerializeField] private float waterAmount = 25f;

    [Tooltip("Cantidad de vidas que recupera (para Botiquín)")]
    [SerializeField] private int healthAmount = 1;

    [Header("Parámetros de Boost (Solo Tuna)")]
    [Tooltip("Multiplicador de velocidad al comer la tuna (1.35 = +35% de velocidad)")]
    [SerializeField] private float speedBoost = 1.35f;

    [Tooltip("Duración en segundos del boost de velocidad")]
    [SerializeField] private float boostDuration = 3f;

    [Header("Efectos Opcionales")]
    [SerializeField] private AudioClip pickupSound;

    private bool isCollected = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (isCollected) return;

        // Comprobar si el que entra es Mateo (o un collider hijo de Mateo)
        PlayerController2D player = collision.GetComponentInParent<PlayerController2D>();
        if (player == null) return;

        isCollected = true;

        switch (itemType)
        {
            case ItemType.Cantimplora:
                WaterSystem waterSys = player.GetComponent<WaterSystem>();
                if (waterSys != null)
                {
                    waterSys.AddWater(waterAmount);
                }
                Debug.Log($"[PickupItem] Mateo recogió Cantimplora (+{waterAmount}% agua).");
                break;

            case ItemType.Botella:
                WaterSystem botWater = player.GetComponent<WaterSystem>();
                if (botWater != null)
                {
                    botWater.AddWater(waterAmount);
                }
                Debug.Log($"[PickupItem] Mateo bebió una Botella de agua (+{waterAmount}% agua).");
                break;

            case ItemType.Tuna:
                WaterSystem tunaWater = player.GetComponent<WaterSystem>();
                if (tunaWater != null)
                {
                    tunaWater.AddWater(waterAmount);
                }
                player.ApplySpeedBoost(speedBoost, boostDuration);
                Debug.Log($"[PickupItem] Mateo comió una Tuna (+{waterAmount}% agua y boost de velocidad).");
                break;

            case ItemType.Botiquin:
                PlayerHealth healthSys = player.GetComponent<PlayerHealth>();
                if (healthSys != null)
                {
                    healthSys.Heal(healthAmount);
                }
                Debug.Log($"[PickupItem] Mateo recogió Botiquín (+{healthAmount} vida).");
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
