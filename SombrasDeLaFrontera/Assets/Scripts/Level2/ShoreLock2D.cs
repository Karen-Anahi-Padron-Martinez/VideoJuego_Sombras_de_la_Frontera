using UnityEngine;

/// <summary>
/// Barrera de un solo sentido en la orilla sur del Río Bravo (Nivel 2).
/// Inicia desactivada para que Mateo camine por la arena y se meta al río; en cuanto sus pies
/// pasan lockAboveY (ya nadando), el collider se activa y le impide regresar al desierto.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class ShoreLock2D : MonoBehaviour
{
    [Tooltip("Altura (Y) de los pies de Mateo a partir de la cual la orilla sur queda cerrada")]
    [SerializeField] private float lockAboveY = 1.6f;

    private BoxCollider2D barrier;
    private SwimController2D swimmer;
    private bool locked;

    private void Awake()
    {
        barrier = GetComponent<BoxCollider2D>();
        barrier.isTrigger = false;
        barrier.enabled = false;
    }

    private void Update()
    {
        if (locked) return;

        if (swimmer == null)
        {
            swimmer = FindAnyObjectByType<SwimController2D>();
            if (swimmer == null) return;
        }

        if (swimmer.GetFeetPosition().y > lockAboveY)
        {
            locked = true;
            barrier.enabled = true;
            Debug.Log("[ShoreLock2D] Mateo se adentró en el río: la orilla sur queda cerrada.");
        }
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.6f);
        Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
    }
}
