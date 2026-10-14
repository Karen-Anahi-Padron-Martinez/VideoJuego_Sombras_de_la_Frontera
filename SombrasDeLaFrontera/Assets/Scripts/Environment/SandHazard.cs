using UnityEngine;

public class SandHazard : MonoBehaviour
{
    [Header("Configuración de Ralentización")]
    [Tooltip("Multiplicador de velocidad dentro de la arena (0.5 = 50% de velocidad)")]
    [SerializeField] private float slowMultiplier = 0.5f;

    [Tooltip("Multiplicador de velocidad al salir de la arena")]
    [SerializeField] private float normalMultiplier = 1.0f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        PlayerController2D player = collision.GetComponentInParent<PlayerController2D>();
        if (player != null)
        {
            player.speedMultiplier = slowMultiplier;
            Debug.Log($"[SandHazard] Mateo entró en arena movediza ({gameObject.name}). Velocidad reducida al {slowMultiplier * 100}%.");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        PlayerController2D player = collision.GetComponentInParent<PlayerController2D>();
        if (player != null)
        {
            player.speedMultiplier = normalMultiplier;
            Debug.Log($"[SandHazard] Mateo salió de arena movediza ({gameObject.name}). Velocidad restaurada.");
        }
    }
}
