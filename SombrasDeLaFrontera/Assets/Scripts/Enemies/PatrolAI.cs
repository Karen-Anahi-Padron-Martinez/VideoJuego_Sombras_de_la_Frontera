using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controla el movimiento de patrullaje de los vigilantes (coyotes)
/// entre una lista fija de Waypoints en bucle (Loop) o vaivén (PingPong).
/// </summary>
public class PatrolAI : MonoBehaviour
{
    public enum PatrolMode
    {
        PingPong, // Va del punto A al B y regresa (vaivén)
        Loop      // Recorre A -> B -> C -> A (bucle continuo)
    }

    [Header("Configuración de Ruta")]
    [Tooltip("Lista de puntos de ruta (Waypoints). Si está vacía, buscará hijos de un objeto llamado 'Waypoints'")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();

    [Tooltip("Modo de recorrido de la ruta")]
    [SerializeField] private PatrolMode patrolMode = PatrolMode.PingPong;

    [Tooltip("Velocidad de movimiento en unidades por segundo")]
    public float moveSpeed = 2.2f;

    [Tooltip("Tiempo de espera en cada punto de ruta antes de avanzar al siguiente")]
    public float waitTimeAtWaypoint = 1.2f;

    [Header("Referencias Visuales")]
    [Tooltip("Pivote de la linterna / cono de visión que debe apuntar hacia donde se camina")]
    [SerializeField] private Transform flashlightPivot;

    [Tooltip("SpriteRenderer del enemigo para voltearlo horizontalmente")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("Animator opcional del enemigo")]
    [SerializeField] private Animator animator;

    [Header("Estado de Alerta")]
    [Tooltip("Si es verdadero, el coyote se detiene porque su linterna detectó a Mateo")]
    public bool isAlerted = false;

    // Estado interno
    private int currentWaypointIndex = 0;
    private int patrolDirection = 1; // 1 = adelante, -1 = hacia atrás (en PingPong)
    private bool isWaiting = false;
    private Vector2 lastMoveDirection = Vector2.right;
    private List<Vector2> fixedPositions = new List<Vector2>();

    public Vector2 CurrentFacingDirection => lastMoveDirection;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (flashlightPivot == null)
        {
            Transform fPivot = transform.Find("FlashlightPivot");
            if (fPivot != null) flashlightPivot = fPivot;
        }

        // Si la lista de waypoints está vacía, buscar contenedor 'Waypoints'
        if (waypoints.Count == 0)
        {
            Transform wpContainer = transform.Find("Waypoints");
            if (wpContainer != null)
            {
                foreach (Transform child in wpContainer)
                {
                    waypoints.Add(child);
                }
            }
        }
    }

    private void Start()
    {
        // Si los waypoints están como hijos del coyote, desacoplarlos en ejecución
        // para que no se desplacen junto con él (efecto zanahoria)
        Transform wpContainer = transform.Find("Waypoints");
        if (wpContainer != null)
        {
            wpContainer.SetParent(transform.parent);
        }
        else
        {
            foreach (Transform wp in waypoints)
            {
                if (wp != null && wp.IsChildOf(transform))
                {
                    wp.SetParent(transform.parent);
                }
            }
        }

        // Registrar las posiciones fijas en el mundo
        fixedPositions.Clear();
        foreach (Transform wp in waypoints)
        {
            if (wp != null) fixedPositions.Add(wp.position);
        }

        if (fixedPositions.Count > 0)
        {
            OrientTowards(fixedPositions[0]);
        }
    }

    private void Update()
    {
        // Si la alerta global de 5 segundos del perro expiró, restaurar a los coyotes
        if (dogAlertEndTime > 0f && Time.time >= dogAlertEndTime)
        {
            dogAlertEndTime = -1f;
            SetGlobalAlert(false);
        }

        if (waypoints == null || waypoints.Count == 0 || isWaiting || isAlerted)
        {
            if (animator != null) animator.SetBool("isMoving", false);
            return;
        }

        // Obtener posición objetivo fija
        Vector2 targetPos;
        if (fixedPositions.Count > currentWaypointIndex)
        {
            targetPos = fixedPositions[currentWaypointIndex];
        }
        else if (waypoints[currentWaypointIndex] != null)
        {
            targetPos = waypoints[currentWaypointIndex].position;
        }
        else
        {
            return;
        }

        Vector2 currentPos = transform.position;
        Vector2 dir = (targetPos - currentPos);
        float distance = dir.magnitude;

        if (distance > 0.08f)
        {
            // Mover hacia el waypoint
            Vector2 step = dir.normalized * (moveSpeed * Time.deltaTime);
            transform.position = currentPos + step;

            // Orientar linterna y sprite
            OrientTowards(targetPos);

            if (animator != null) animator.SetBool("isMoving", true);
        }
        else
        {
            // Llegó al waypoint
            transform.position = targetPos;
            StartCoroutine(WaitAtWaypointRoutine());
        }
    }

    /// <summary>
    /// Orienta la linterna hacia la dirección de avance y voltea el sprite.
    /// </summary>
    private void OrientTowards(Vector2 targetPos)
    {
        Vector2 dir = (targetPos - (Vector2)transform.position);
        if (dir.sqrMagnitude < 0.001f) return;

        dir.Normalize();
        lastMoveDirection = dir;

        // 1. Voltear sprite horizontalmente si cambia de lado
        if (spriteRenderer != null)
        {
            if (dir.x < -0.1f)
                spriteRenderer.flipX = true;
            else if (dir.x > 0.1f)
                spriteRenderer.flipX = false;
        }

        // 2. Rotar el pivote de la linterna para que apunte hacia la dirección del movimiento
        if (flashlightPivot != null)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            flashlightPivot.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    private IEnumerator WaitAtWaypointRoutine()
    {
        isWaiting = true;
        if (animator != null) animator.SetBool("isMoving", false);

        yield return new WaitForSeconds(waitTimeAtWaypoint);

        AdvanceToNextWaypoint();
        isWaiting = false;
    }

    private void AdvanceToNextWaypoint()
    {
        if (waypoints.Count <= 1) return;

        if (patrolMode == PatrolMode.Loop)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }
        else if (patrolMode == PatrolMode.PingPong)
        {
            currentWaypointIndex += patrolDirection;

            if (currentWaypointIndex >= waypoints.Count)
            {
                currentWaypointIndex = waypoints.Count - 2;
                patrolDirection = -1;
            }
            else if (currentWaypointIndex < 0)
            {
                currentWaypointIndex = 1;
                patrolDirection = 1;
            }
        }
    }

    /// <summary>
    /// Permite asignar los waypoints programáticamente desde el editor o inicializador.
    /// </summary>
    public void SetWaypoints(List<Transform> newWaypoints, PatrolMode mode = PatrolMode.PingPong)
    {
        waypoints = newWaypoints;
        patrolMode = mode;
        currentWaypointIndex = 0;
        patrolDirection = 1;

        fixedPositions.Clear();
        foreach (Transform wp in waypoints)
        {
            if (wp != null) fixedPositions.Add(wp.position);
        }
    }

    // Estado de alerta global provocada por el perro
    public static bool isHighAlertFromDog => Time.time < dogAlertEndTime;
    private static float dogAlertEndTime = -1f;

    /// <summary>
    /// Dispara la alerta máxima de los coyotes durante una duración determinada (5 segundos).
    /// Durante este tiempo, los coyotes se detienen en guardia y si ven a Mateo lo capturan al instante.
    /// </summary>
    public static void TriggerDogAlert(float duration = 5f)
    {
        dogAlertEndTime = Time.time + duration;
        SetGlobalAlert(true);
    }

    /// <summary>
    /// Alerta o calma a todos los coyotes patrulleros del nivel (usado cuando ladra un perro guardián).
    /// </summary>
    public static void SetGlobalAlert(bool alert)
    {
        PatrolAI[] allEnemies = FindObjectsByType<PatrolAI>(FindObjectsSortMode.None);
        foreach (var enemy in allEnemies)
        {
            if (enemy == null) continue;
            // Solo alertar a los coyotes vigilantes (el perro gestiona su propio estado internamente)
            if (enemy.GetComponent<GuardDogAI>() == null)
            {
                enemy.isAlerted = alert;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null) continue;

            Gizmos.DrawWireSphere(waypoints[i].position, 0.25f);

            int nextIndex = (i + 1) % waypoints.Count;
            if (patrolMode == PatrolMode.PingPong && i == waypoints.Count - 1)
                continue;

            if (waypoints[nextIndex] != null)
            {
                Gizmos.DrawLine(waypoints[i].position, waypoints[nextIndex].position);
            }
        }
    }
}
