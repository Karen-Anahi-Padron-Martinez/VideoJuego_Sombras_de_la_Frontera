using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la fauna nociva del desierto (serpientes de cascabel, alacranes)
/// con movimiento de merodeo autónomo, detección de obstáculos y daño por contacto (1 corazón).
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HazardFauna : MonoBehaviour
{
    public enum FaunaType
    {
        Snake,     // Serpiente de cascabel
        Scorpion,  // Alacrán del desierto
        Spider     // Araña viuda negra
    }

    [Header("Identificación de Fauna")]
    [Tooltip("Tipo de criatura peligrosa")]
    [SerializeField] private FaunaType faunaType = FaunaType.Snake;

    [Tooltip("Cantidad de corazones/daño que resta al contacto")]
    [SerializeField] private int damageAmount = 1;

    [Tooltip("Tiempo de enfriamiento mínimo entre ataques al jugador")]
    [SerializeField] private float damageCooldown = 1.2f;

    [Header("Movimiento Autónomo de Merodeo")]
    [Tooltip("Permite que la criatura merodee cerca de su nido")]
    [SerializeField] private bool canWander = true;

    [Tooltip("Radio máximo de merodeo alrededor de su posición inicial")]
    [SerializeField] private float wanderRadius = 1.8f;

    [Tooltip("Velocidad de movimiento en unidades por segundo")]
    [SerializeField] private float moveSpeed = 0.9f;

    [Tooltip("Tiempo de pausa/acecho al llegar a un punto")]
    [SerializeField] private float waitTimeAtPoint = 3.0f;

    [Header("Límites del Mapa y Detección de Obstáculos")]
    [Tooltip("Capa de obstáculos sólidos que la fauna no puede atravesar (rocas, cactus, paredes)")]
    [SerializeField] private LayerMask obstacleMask;

    [Tooltip("Límite inferior izquierdo del desierto caminable")]
    [SerializeField] private Vector2 minMapBounds = new Vector2(-2.7f, 2.5f);

    [Tooltip("Límite superior derecho del desierto caminable")]
    [SerializeField] private Vector2 maxMapBounds = new Vector2(2.7f, 34.0f);

    [Header("Animación Procedural de Acecho")]
    [Tooltip("Pequeña oscilación / respiración mientras está quieta")]
    [SerializeField] private bool idleBreathing = true;
    [SerializeField] private float breatheFrequency = 3.5f;
    [SerializeField] private float breatheMagnitude = 0.04f;

    // Estado interno
    private Vector2 spawnPosition;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private float nextDamageTime = 0f;
    private Vector3 initialScale;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        initialScale = transform.localScale;
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        spawnPosition = transform.position;
        targetPosition = spawnPosition;
    }

    private void Start()
    {
        spawnPosition = transform.position;
        if (canWander)
        {
            StartCoroutine(WanderRoutine());
        }
    }

    private void Update()
    {
        // 1. Animación procedural de respiración mientras está quieta
        if (idleBreathing && !isMoving)
        {
            float scaleOffset = Mathf.Sin(Time.time * breatheFrequency) * breatheMagnitude;
            transform.localScale = initialScale + new Vector3(scaleOffset, -scaleOffset * 0.5f, 0f);
        }

        // 2. Movimiento suave hacia el punto objetivo
        if (isMoving)
        {
            Vector2 currentPos = transform.position;
            Vector2 dir = (targetPosition - currentPos);
            float distance = dir.magnitude;

            if (distance > 0.05f)
            {
                // Mover
                Vector2 step = dir.normalized * (moveSpeed * Time.deltaTime);
                transform.position = currentPos + step;

                // Voltear sprite horizontalmente según la dirección
                if (spriteRenderer != null)
                {
                    if (dir.x < -0.05f)
                        spriteRenderer.flipX = true;
                    else if (dir.x > 0.05f)
                        spriteRenderer.flipX = false;
                }
            }
            else
            {
                // Llegó a su destino
                transform.position = targetPosition;
                isMoving = false;
            }
        }
    }

    private IEnumerator WanderRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0.5f, 2.0f));

        while (true)
        {
            // Elegir un nuevo punto válido dentro del radio y sin obstáculos
            if (TryPickNewWanderPoint(out Vector2 newPoint))
            {
                targetPosition = newPoint;
                isMoving = true;

                // Esperar a que termine de moverse
                while (isMoving)
                {
                    yield return null;
                }
            }

            // Descansar / acechar en el punto
            yield return new WaitForSeconds(Random.Range(waitTimeAtPoint * 0.7f, waitTimeAtPoint * 1.3f));
        }
    }

    /// <summary>
    /// Intenta encontrar un punto aleatorio en el radio que esté dentro del mapa y sin obstáculos.
    /// </summary>
    private bool TryPickNewWanderPoint(out Vector2 validPoint)
    {
        validPoint = transform.position;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            Vector2 randomOffset = Random.insideUnitCircle * wanderRadius;
            Vector2 candidate = spawnPosition + randomOffset;

            // 1. Limitar a los límites del mapa
            candidate.x = Mathf.Clamp(candidate.x, minMapBounds.x, maxMapBounds.x);
            candidate.y = Mathf.Clamp(candidate.y, minMapBounds.y, maxMapBounds.y);

            // 2. Comprobar si el punto candidato o el camino tiene obstáculos sólidos
            Vector2 origin = transform.position;
            Vector2 dir = candidate - origin;
            float dist = dir.magnitude;

            if (dist < 0.2f) continue;

            // Verificar si hay obstáculos en el camino
            if (obstacleMask != 0)
            {
                RaycastHit2D hit = Physics2D.CircleCast(origin, 0.35f, dir.normalized, dist, obstacleMask);
                if (hit.collider != null)
                {
                    continue; // Hay un obstáculo en el camino
                }

                Collider2D overlap = Physics2D.OverlapCircle(candidate, 0.35f, obstacleMask);
                if (overlap != null)
                {
                    continue; // El punto destino está dentro de un obstáculo
                }
            }

            validPoint = candidate;
            return true;
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryDamagePlayer(collision.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryDamagePlayer(collision.gameObject);
        // Si choca con un obstáculo físico, detenerse para buscar nuevo camino
        isMoving = false;
    }

    private void TryDamagePlayer(GameObject target)
    {
        if (Time.time < nextDamageTime) return;

        PlayerHealth health = target.GetComponentInParent<PlayerHealth>();
        if (health == null) health = target.GetComponentInChildren<PlayerHealth>();

        if (health != null && !health.isDead && !health.isInvulnerable)
        {
            nextDamageTime = Time.time + damageCooldown;
            string creatureName = (faunaType == FaunaType.Snake) ? "Serpiente de cascabel" : "Alacrán";

            Debug.Log($"[HazardFauna] ¡{creatureName} atacó a Mateo en {gameObject.name}! (-{damageAmount} corazón).");
            health.TakeDamage(damageAmount);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = (faunaType == FaunaType.Snake) ? Color.green : Color.red;
        Vector2 center = Application.isPlaying ? spawnPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);
    }
}
