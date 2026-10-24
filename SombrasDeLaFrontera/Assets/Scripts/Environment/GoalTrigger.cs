using UnityEngine;

/// <summary>
/// Trigger de meta ubicado en la ribera del río (Tramo 4 al norte del desierto).
/// Al ser alcanzado por Mateo:
/// 1. Desbloquea y guarda el Nivel 2 en PlayerPrefs ("NivelMaximo" = 2).
/// 2. Congela el tiempo de juego (Time.timeScale = 0).
/// 3. Activa la pantalla modal de Victoria (Nivel Completado).
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class GoalTrigger : MonoBehaviour
{
    [Header("Configuración de Meta")]
    [Tooltip("Nivel que se desbloqueará en el guardado de progreso")]
    [SerializeField] private int nextLevelIndex = 2;

    [Tooltip("Clave guardada en PlayerPrefs para el nivel máximo")]
    [SerializeField] private string levelProgressKey = "NivelMaximo";

    private bool hasTriggered = false;

    // Evento estático para notificar a la interfaz de usuario sin acoplamiento
    public static event System.Action OnLevelCompleted;

    private void Awake()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (hasTriggered) return;

        if (IsPlayer(collision))
        {
            hasTriggered = true;
            CompleteLevel();
        }
    }

    private bool IsPlayer(Collider2D col)
    {
        if (col.CompareTag("Player") || col.name.ToLower().Contains("mateo") || col.transform.root.name.ToLower().Contains("player"))
        {
            return true;
        }
        return false;
    }

    private void CompleteLevel()
    {
        // 1. Guardar progreso de nivel desbloqueado en disco
        int currentMax = PlayerPrefs.GetInt(levelProgressKey, 1);
        if (nextLevelIndex > currentMax)
        {
            PlayerPrefs.SetInt(levelProgressKey, nextLevelIndex);
            PlayerPrefs.Save();
            Debug.Log($"<color=green>[GoalTrigger] ¡PROGRESO GUARDADO! Nivel {nextLevelIndex} desbloqueado en PlayerPrefs.</color>");
        }

        // 2. Detener el juego
        Time.timeScale = 0f;

        // 3. Notificar a la interfaz de victoria
        Debug.Log("<color=cyan>[GoalTrigger] ¡VICTORIA! Mateo ha cruzado el desierto y alcanzado la ribera del Río Bravo.</color>");
        OnLevelCompleted?.Invoke();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(0f, 0.8f, 1f, 0.5f);
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.DrawCube(transform.position + (Vector3)col.offset, col.size);
        }
        else
        {
            Gizmos.DrawCube(transform.position, new Vector3(6f, 1.5f, 0f));
        }
    }
}
