using UnityEngine;

/// <summary>
/// Gestiona las zonas de cobertura física (cactus, rocas, camionetas).
/// Cuando Mateo se posiciona dentro del área, activa el estado de sigilo
/// (IsHidden = true) y atenúa visualmente su sprite para indicar que está oculto.
/// </summary>
public class CoverZone2D : MonoBehaviour
{
    [Header("Retroalimentación Visual")]
    [Tooltip("Color o atenuación aplicada al sprite del jugador mientras está escondido")]
    [SerializeField] private Color hiddenColor = new Color(0.55f, 0.55f, 0.55f, 0.85f);
    
    private Color originalColor = Color.white;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsPlayer(collision, out GameObject playerRoot, out SpriteRenderer sr))
        {
            // 1. Guardar color original y aplicar atenuación de sigilo
            if (sr != null)
            {
                originalColor = Color.white;
                sr.color = hiddenColor;
            }

            // 2. Notificar dinámicamente al jugador (cuando Karen cree PlayerController2D, lo recibirá automáticamente)
            playerRoot.SendMessage("SetHidingState", true, SendMessageOptions.DontRequireReceiver);

            Debug.Log($"[CoverZone2D] {playerRoot.name} se ha OCULTADO en {gameObject.name}");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (IsPlayer(collision, out GameObject playerRoot, out SpriteRenderer sr))
        {
            // 1. Restaurar color original del sprite
            if (sr != null)
            {
                sr.color = Color.white;
            }

            // 2. Desactivar estado de sigilo en el jugador
            playerRoot.SendMessage("SetHidingState", false, SendMessageOptions.DontRequireReceiver);

            Debug.Log($"[CoverZone2D] {playerRoot.name} ha SALIDO de la cobertura {gameObject.name}");
        }
    }

    /// <summary>
    /// Comprueba si el objeto que entra en la cobertura es el jugador,
    /// buscando en la raíz o en los hijos (por ejemplo, mateo_part1_7 dentro de Player).
    /// </summary>
    private bool IsPlayer(Collider2D col, out GameObject playerRoot, out SpriteRenderer sr)
    {
        playerRoot = null;
        sr = null;

        if (col.CompareTag("Player") || col.name.ToLower().Contains("player") || col.transform.root.name.ToLower().Contains("player"))
        {
            playerRoot = col.transform.root.gameObject;
            sr = playerRoot.GetComponentInChildren<SpriteRenderer>();
            return true;
        }

        return false;
    }
}
