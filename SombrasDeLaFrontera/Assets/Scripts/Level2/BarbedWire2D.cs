using UnityEngine;

/// <summary>
/// Alambrado de púas en el Río Bravo (Nivel 2): obstáculo sólido que cruza parte del río y obliga a buscar el hueco.
/// Si Mateo lo toca pierde 1 corazón (PlayerHealth aplica la invulnerabilidad temporal, así que no lo hiere en cada cuadro).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BarbedWire2D : MonoBehaviour
{
    [Tooltip("Corazones que resta al tocar las púas")]
    [SerializeField] private int damageAmount = 1;

    private void OnCollisionEnter2D(Collision2D collision) => Hurt(collision.collider);

    private void OnCollisionStay2D(Collision2D collision) => Hurt(collision.collider);

    private void Hurt(Collider2D other)
    {
        PlayerHealth health = other.GetComponentInParent<PlayerHealth>();
        if (health == null || health.isDead || health.isInvulnerable) return;

        Debug.Log($"[BarbedWire2D] Mateo se lastimó con el alambre de púas ({gameObject.name}).");
        health.TakeDamage(damageAmount, DefeatReason.BarbedWire);
    }
}
