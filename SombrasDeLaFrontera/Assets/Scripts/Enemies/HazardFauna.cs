using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la fauna nociva del desierto (serpientes de cascabel, alacranes y arañas)
/// con movimiento de merodeo autónomo, detección de obstáculos y ataque animado (1 corazón).
/// Comportamiento:
/// - Merodea cerca de su nido con pausas cortas. Antes de cambiar de dirección se VOLTEA despacio
///   (se gira sobre sí misma) y después avanza.
/// - Nunca camina encima ni a través de cactus, rocas, arbustos o vehículos (los rodea o elige otro punto).
/// Estados de animación (todos controlados desde aquí):
/// - Walk:   avanzando hacia un punto cercano a su nido.
/// - Idle:   descansando o volteándose.
/// - Alert:  Mateo está cerca; se detiene, lo encara y avisa (la serpiente agita el cascabel).
/// - Attack: Mateo entró a su rango de ataque; el daño solo se aplica si el golpe conecta.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class HazardFauna : MonoBehaviour
{
    public enum FaunaType
    {
        Snake,     // Serpiente de cascabel
        Scorpion,  // Alacrán del desierto
        Spider     // Araña (tarántula)
    }

    [Header("Identificación de Fauna")]
    [Tooltip("Tipo de criatura peligrosa")]
    [SerializeField] private FaunaType faunaType = FaunaType.Snake;

    [Tooltip("Cantidad de corazones/daño que resta al contacto")]
    [SerializeField] private int damageAmount = 1;

    [Tooltip("Tiempo de enfriamiento mínimo entre ataques al jugador")]
    [SerializeField] private float damageCooldown = 1.2f;

    [Header("Ataque y Alerta")]
    [Tooltip("Distancia (desde su cuerpo hasta los pies de Mateo) a la que la criatura lo detecta y se pone en guardia")]
    [SerializeField] private float alertRadius = 1.3f;

    [Tooltip("Distancia a la que la criatura lanza su ataque")]
    [SerializeField] private float attackRange = 0.22f;

    [Tooltip("Segundos desde que inicia la animación de ataque hasta que el golpe conecta")]
    [SerializeField] private float attackHitDelay = 0.22f;

    [Tooltip("Margen extra de alcance en el momento del golpe (Mateo puede esquivar si se aleja a tiempo)")]
    [SerializeField] private float attackReachBonus = 0.12f;

    [Header("Movimiento Autónomo de Merodeo")]
    [Tooltip("Permite que la criatura merodee cerca de su nido")]
    [SerializeField] private bool canWander = true;

    [Tooltip("Radio máximo de merodeo alrededor de su posición inicial")]
    [SerializeField] private float wanderRadius = 1.8f;

    [Tooltip("Velocidad de movimiento en unidades por segundo")]
    [SerializeField] private float moveSpeed = 0.9f;

    [Tooltip("Pausa mínima y máxima (segundos) al llegar a cada punto antes de seguir merodeando")]
    [SerializeField] private Vector2 restTimeRange = new Vector2(0.4f, 1.2f);

    [Tooltip("Duración del giro al cambiar de dirección (se voltea despacio antes de avanzar)")]
    [SerializeField] private float turnDuration = 0.55f;

    [Tooltip("Duración del giro rápido cuando va a atacar")]
    [SerializeField] private float attackTurnDuration = 0.2f;

    [Header("Límites del Mapa")]
    [Tooltip("Límite inferior izquierdo del desierto caminable")]
    [SerializeField] private Vector2 minMapBounds = new Vector2(-2.7f, 2.5f);

    [Tooltip("Límite superior derecho del desierto caminable")]
    [SerializeField] private Vector2 maxMapBounds = new Vector2(2.7f, 34.0f);

    // Estado interno
    private Vector2 spawnPosition;
    private Vector2 targetPosition;
    private bool isMoving = false;
    private bool isAlerted = false;
    private bool isAttacking = false;
    private bool isTurning = false;
    private float nextDamageTime = 0f;
    private float nextAlertSoundTime = 0f;
    private float nextMoveSoundTime = 0f;
    private bool wasAlerted = false;
    private Vector3 baseScale;
    private SpriteRenderer spriteRenderer;
    private CreatureAnimator creatureAnimator;
    private Collider2D[] myColliders;
    private PlayerHealth playerHealth;
    private Collider2D playerCollider;

    public FaunaType Type => faunaType;

    private void Awake()
    {
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        creatureAnimator = GetComponentInChildren<CreatureAnimator>();
        myColliders = GetComponents<Collider2D>();
        baseScale = transform.localScale;
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

    private void FindPlayer()
    {
        if (playerHealth != null) return;
        playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerCollider = playerHealth.GetComponent<Collider2D>();
        }
    }

    /// <summary>Tamaño de la parte baja del cuerpo, usado para no encimarse con obstáculos.</summary>
    private Vector2 BodySize
    {
        get
        {
            if (spriteRenderer == null) return new Vector2(0.25f, 0.12f);
            Vector3 s = spriteRenderer.bounds.size;
            return new Vector2(s.x * 0.6f, s.y * 0.45f);
        }
    }

    private void Update()
    {
        FindPlayer();

        // 1. Detección de Mateo: ponerse en guardia o atacar
        float distance = DistanceToPlayer();
        bool playerAvailable = playerHealth != null && !playerHealth.isDead;

        if (!isAttacking)
        {
            if (playerAvailable && distance <= attackRange && Time.time >= nextDamageTime)
            {
                StartCoroutine(AttackRoutine());
            }
            else
            {
                isAlerted = playerAvailable && distance <= alertRadius;
            }
        }

        // Sonido de aviso (cascabel / chasquidos / patitas) mientras vigila a Mateo
        if (isAlerted && !isAttacking && (!wasAlerted || Time.time >= nextAlertSoundTime))
        {
            nextAlertSoundTime = Time.time + (faunaType == FaunaType.Snake ? 1.05f : 1.4f);
            AudioManager.Instance?.PlayFaunaAlertAt(faunaType, transform.position);
        }
        wasAlerted = isAlerted;

        if (isAlerted && !isAttacking)
        {
            isMoving = false;
            // Se voltea despacio para encarar a Mateo
            if (!isTurning && NeedsToTurnTowards(playerHealth.transform.position))
            {
                StartCoroutine(TurnRoutine(!spriteRenderer.flipX, turnDuration));
            }
        }

        // 2. Movimiento suave hacia el punto objetivo (sin atravesar obstáculos)
        if (isMoving && !isTurning)
        {
            Vector2 currentPos = transform.position;
            Vector2 dir = (targetPosition - currentPos);
            float dist = dir.magnitude;

            if (dist > 0.05f)
            {
                Vector2 next = currentPos + dir.normalized * Mathf.Min(dist, moveSpeed * Time.deltaTime);
                Vector2 body = BodySize;
                bool blocked = ObstacleMap2D.Overlaps(new Rect(next.x - body.x * 0.5f, next.y, body.x, body.y))
                               && !ObstacleMap2D.Overlaps(new Rect(currentPos.x - body.x * 0.5f, currentPos.y, body.x, body.y));
                if (blocked)
                {
                    isMoving = false; // Se detiene antes de pisar el obstáculo y busca otro punto
                }
                else
                {
                    transform.position = next;
                    if (Time.time >= nextMoveSoundTime)
                    {
                        nextMoveSoundTime = Time.time + 0.55f;
                        AudioManager.Instance?.PlayFaunaMoveAt(faunaType, transform.position);
                    }
                }
            }
            else
            {
                // Llegó a su destino
                transform.position = targetPosition;
                isMoving = false;
            }
        }

        // 3. Animación según el estado (el ataque la gestiona AttackRoutine)
        if (creatureAnimator != null && !isAttacking)
        {
            if (isAlerted) creatureAnimator.Play("Alert");
            else if (isMoving && !isTurning) creatureAnimator.Play("Walk");
            else creatureAnimator.Play("Idle");
        }
    }

    /// <summary>
    /// Distancia real entre los colliders de la criatura y el collider de los pies de Mateo.
    /// </summary>
    private float DistanceToPlayer()
    {
        if (playerHealth == null) return float.MaxValue;

        if (playerCollider != null && playerCollider.enabled && myColliders != null)
        {
            float best = float.MaxValue;
            foreach (Collider2D col in myColliders)
            {
                if (col == null || !col.enabled) continue;
                ColliderDistance2D d = col.Distance(playerCollider);
                if (d.isValid) best = Mathf.Min(best, d.distance);
            }
            if (best < float.MaxValue) return Mathf.Max(0f, best);
        }

        return Vector2.Distance(transform.position, playerHealth.transform.position);
    }

    /// <summary>Los sprites miran a la derecha; flipX = true significa que mira a la izquierda.</summary>
    private bool NeedsToTurnTowards(Vector2 point)
    {
        if (spriteRenderer == null) return false;
        float dx = point.x - transform.position.x;
        if (Mathf.Abs(dx) < 0.05f) return false;
        return (dx < 0f) != spriteRenderer.flipX;
    }

    /// <summary>
    /// Giro sobre sí misma: se "aplana" de lado, cambia de dirección a la mitad y vuelve a su ancho normal.
    /// </summary>
    private IEnumerator TurnRoutine(bool targetFlipX, float duration)
    {
        if (spriteRenderer == null || spriteRenderer.flipX == targetFlipX) yield break;

        isTurning = true;
        float half = Mathf.Max(0.05f, duration * 0.5f);
        float t = 0f;

        while (t < half)
        {
            t += Time.deltaTime;
            SetTurnWidth(Mathf.Lerp(1f, 0.15f, Mathf.SmoothStep(0f, 1f, t / half)));
            yield return null;
        }

        spriteRenderer.flipX = targetFlipX;

        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            SetTurnWidth(Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0f, 1f, t / half)));
            yield return null;
        }

        SetTurnWidth(1f);
        isTurning = false;
    }

    private void SetTurnWidth(float factor)
    {
        transform.localScale = new Vector3(baseScale.x * factor, baseScale.y, baseScale.z);
    }

    private IEnumerator AttackRoutine()
    {
        isAttacking = true;
        isAlerted = true;
        isMoving = false;
        nextDamageTime = Time.time + damageCooldown;

        // Encarar a Mateo con un giro rápido antes de atacar
        while (isTurning) yield return null;
        if (playerHealth != null && NeedsToTurnTowards(playerHealth.transform.position))
        {
            yield return TurnRoutine(!spriteRenderer.flipX, attackTurnDuration);
        }

        if (creatureAnimator != null) creatureAnimator.PlayOneShot("Attack", "Alert");
        AudioManager.Instance?.PlayFaunaAttackAt(faunaType, transform.position);

        yield return new WaitForSeconds(attackHitDelay);

        // El golpe solo conecta si Mateo sigue al alcance (puede esquivarlo alejándose a tiempo)
        if (playerHealth != null && !playerHealth.isDead && !playerHealth.isInvulnerable && DistanceToPlayer() <= attackRange + attackReachBonus)
        {
            playerHealth.TakeDamage(damageAmount, GetDefeatReason());
            if (playerHealth.isDead && creatureAnimator != null) creatureAnimator.UseUnscaledTime();
        }

        // Esperar a que termine la animación de ataque
        while (creatureAnimator != null && creatureAnimator.IsPlayingOneShot)
        {
            yield return null;
        }

        isAttacking = false;
    }

    private DefeatReason GetDefeatReason()
    {
        switch (faunaType)
        {
            case FaunaType.Snake: return DefeatReason.SnakeBite;
            case FaunaType.Spider: return DefeatReason.SpiderBite;
            default: return DefeatReason.ScorpionSting;
        }
    }

    private IEnumerator WanderRoutine()
    {
        yield return new WaitForSeconds(Random.Range(0.2f, 0.8f));

        while (true)
        {
            // No merodear mientras vigila o ataca a Mateo
            while (isAlerted || isAttacking || isTurning)
            {
                yield return null;
            }

            // Elegir un nuevo punto válido dentro del radio y sin obstáculos en el camino
            if (TryPickNewWanderPoint(out Vector2 newPoint))
            {
                // Voltearse despacio si el nuevo punto queda del otro lado
                if (NeedsToTurnTowards(newPoint))
                {
                    yield return TurnRoutine(!spriteRenderer.flipX, turnDuration);
                }

                if (!isAlerted && !isAttacking)
                {
                    targetPosition = newPoint;
                    isMoving = true;

                    // Esperar a que termine de moverse
                    while (isMoving)
                    {
                        yield return null;
                    }
                }
            }

            // Pausa corta antes del siguiente tramo
            yield return new WaitForSeconds(Random.Range(restTimeRange.x, restTimeRange.y));
        }
    }

    /// <summary>
    /// Intenta encontrar un punto aleatorio en el radio, dentro del mapa y con el camino libre de obstáculos.
    /// </summary>
    private bool TryPickNewWanderPoint(out Vector2 validPoint)
    {
        validPoint = transform.position;
        Vector2 origin = transform.position;
        Vector2 body = BodySize;
        bool startsInsideObstacle = ObstacleMap2D.Overlaps(new Rect(origin.x - body.x * 0.5f, origin.y, body.x, body.y));

        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 candidate = spawnPosition + Random.insideUnitCircle * wanderRadius;

            // 1. Limitar a los límites del mapa
            candidate.x = Mathf.Clamp(candidate.x, minMapBounds.x, maxMapBounds.x);
            candidate.y = Mathf.Clamp(candidate.y, minMapBounds.y, maxMapBounds.y);

            // Tramos ni demasiado cortos ni demasiado largos para que se vea natural
            float dist = Vector2.Distance(origin, candidate);
            if (dist < 0.35f) continue;

            // 2. El destino y el camino deben quedar libres de obstáculos
            if (ObstacleMap2D.Overlaps(new Rect(candidate.x - body.x * 0.5f, candidate.y, body.x, body.y))) continue;
            if (!startsInsideObstacle && !ObstacleMap2D.IsPathClear(origin, candidate, body)) continue;

            validPoint = candidate;
            return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = (faunaType == FaunaType.Snake) ? Color.green : Color.red;
        Vector2 center = Application.isPlaying ? spawnPosition : (Vector2)transform.position;
        Gizmos.DrawWireSphere(center, wanderRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, alertRadius);
    }
}
