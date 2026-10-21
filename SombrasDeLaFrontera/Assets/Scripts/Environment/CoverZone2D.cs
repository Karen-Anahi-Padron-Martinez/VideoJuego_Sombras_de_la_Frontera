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

    // Estado global y local de ocultamiento
    private static int activeCoversCount = 0;
    public static bool isHiding => activeCoversCount > 0;
    public bool isPlayerInside = false;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (IsPlayer(collision, out GameObject playerRoot, out SpriteRenderer sr))
        {
            activeCoversCount++;
            isPlayerInside = true;

            // 1. Guardar color original y aplicar atenuación de sigilo
            if (sr != null)
            {
                originalColor = Color.white;
                sr.color = hiddenColor;
            }

            // 2. Notificar dinámicamente al jugador
            playerRoot.SendMessage("SetHidingState", true, SendMessageOptions.DontRequireReceiver);

            Debug.Log($"[CoverZone2D] {playerRoot.name} se ha OCULTADO en {gameObject.name}. (isHiding={isHiding})");
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (IsPlayer(collision, out GameObject playerRoot, out SpriteRenderer sr))
        {
            activeCoversCount = Mathf.Max(0, activeCoversCount - 1);
            isPlayerInside = false;

            // 1. Restaurar color original del sprite si ya no está en ninguna cobertura
            if (activeCoversCount == 0 && sr != null)
            {
                sr.color = Color.white;
            }

            // 2. Notificar estado de sigilo actualizado al jugador
            playerRoot.SendMessage("SetHidingState", isHiding, SendMessageOptions.DontRequireReceiver);

            Debug.Log($"[CoverZone2D] {playerRoot.name} ha SALIDO de la cobertura {gameObject.name}. (isHiding={isHiding})");
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
