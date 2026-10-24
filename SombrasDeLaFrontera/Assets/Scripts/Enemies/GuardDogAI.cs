using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inteligencia Artificial del Perro Guardián (Bulldog).
/// Características:
/// 1. Patrulla rutas fijas mediante PatrolAI.
/// 2. Círculo de visión invisible en MEDIO CÍRCULO (180 grados) apuntando hacia donde mira/camina el perro.
/// 3. Si detecta a Mateo al descubierto:
///    - El perro se detiene en seco.
///    - Mateo continúa con vida (no muere por ser visto).
///    - El perro ladra y activa la alerta máxima de los coyotes durante 5 SEGUNDOS.
///    - Durante esos 5 segundos, si cualquier coyote ve a Mateo, lo captura al instante.
/// 4. Solo deja de ladrar cuando Mateo sale de su medio círculo o se esconde en cobertura.
/// 5. Si Mateo se acerca demasiado (círculo pequeño magenta), el perro le muerde restándole 1 vida.
/// </summary>
[RequireComponent(typeof(PatrolAI))]
public class GuardDogAI : MonoBehaviour
{
    [Header("Rangos de Detección y Ataque")]
    [Tooltip("Radio del medio círculo de visión/olfato frontal (en metros)")]
    public float detectionRadius = 3.2f;

    [Tooltip("Radio de proximidad para morder al jugador")]
    public float biteRadius = 0.75f;

    [Tooltip("Tiempo mínimo entre mordidas consecutivas")]
    public float biteCooldown = 1.5f;

    [Tooltip("Duración en segundos que los coyotes permanecen en alerta máxima tras vernos el perro")]
    public float coyoteAlertDuration = 5.0f;

    [Tooltip("Tiempo tras perder de vista a Mateo antes de calmarse y reanudar la patrulla")]
    public float calmDownDelay = 0.8f;

    [Header("Máscara de Detección")]
    [SerializeField] private LayerMask obstacleMask;

    // Estado interno
    public bool isBarking { get; private set; } = false;
    private float nextBiteTime = 0f;
    private float lostSightTime = -1f;
    private float nextAlertRefreshTime = 0f;
    private PatrolAI patrolAI;
    private SpriteRenderer spriteRenderer;
    private PlayerHealth cachedPlayerHealth;
    private Transform playerTransform;

    // Eventos estáticos de alerta global para que otros componentes escuchen
    public static event System.Action<bool, Vector2> OnDogBarkStateChanged;

    private void Awake()
    {
        patrolAI = GetComponent<PatrolAI>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        FindPlayerReference();
    }

    private void FindPlayerReference()
    {
        if (cachedPlayerHealth == null)
        {
            cachedPlayerHealth = FindAnyObjectByType<PlayerHealth>();
            if (cachedPlayerHealth != null)
            {
                playerTransform = cachedPlayerHealth.transform;
            }
        }
    }

    /// <summary>
    /// Obtiene la dirección hacia donde mira el perro actualmente (frente).
    /// </summary>
    public Vector2 GetFacingDirection()
    {
        if (patrolAI != null && patrolAI.CurrentFacingDirection.sqrMagnitude > 0.01f)
        {
            return patrolAI.CurrentFacingDirection.normalized;
        }

        if (spriteRenderer != null)
        {
            return spriteRenderer.flipX ? Vector2.left : Vector2.right;
        }

        return Vector2.right;
    }

    private void Update()
    {
        if (cachedPlayerHealth == null || playerTransform == null)
        {
            FindPlayerReference();
            if (cachedPlayerHealth == null) return;
        }

        if (cachedPlayerHealth.isDead)
        {
            if (isBarking)
            {
                StopBarking();
            }
            return;
        }

        Vector2 dogPos = transform.position;
        Vector2 playerPos = playerTransform.position;
        Vector2 toPlayer = (playerPos - dogPos);
        float distanceToPlayer = toPlayer.magnitude;

        // 1. Chequeo de distancia (radio)
        bool playerInDistance = distanceToPlayer <= detectionRadius;

        // 2. Chequeo de MEDIO CÍRCULO FRONTAL (180 grados en la dirección hacia donde mira el perro)
        bool playerInFrontHalfCircle = false;
        Vector2 facing = GetFacingDirection();

        if (playerInDistance && distanceToPlayer > 0.001f)
        {
            // El producto punto >= 0 corresponde a un ángulo entre -90° y +90° respecto al frente (180° total)
            float dot = Vector2.Dot(facing.normalized, toPlayer.normalized);
            playerInFrontHalfCircle = (dot >= 0f);
        }

        // 3. Chequeo de cobertura y sigilo
        bool isPlayerHiding = CoverZone2D.isHiding;

        // 4. Chequeo de muros/obstáculos físicos
        bool lineOfSightBlocked = false;
        LayerMask effectiveMask = obstacleMask != 0 ? obstacleMask : LayerMask.GetMask("Default", "Environment");
        if (playerInDistance && playerInFrontHalfCircle)
        {
            RaycastHit2D hit = Physics2D.Raycast(dogPos, toPlayer.normalized, distanceToPlayer, effectiveMask);
            if (hit.collider != null && !hit.collider.isTrigger && !hit.collider.CompareTag("Player") && !hit.collider.name.ToLower().Contains("mateo"))
            {
                lineOfSightBlocked = true;
            }
        }

        // Condición de detección final: Dentro del medio círculo frontal, fuera de cobertura y sin muros
        bool canDetectMateo = playerInDistance && playerInFrontHalfCircle && !isPlayerHiding && !lineOfSightBlocked;

        if (canDetectMateo)
        {
            lostSightTime = -1f;

            // 1. Iniciar ladrido y alertar a los coyotes por 5 segundos
            if (!isBarking)
            {
                StartBarking();
            }
            else if (Time.time >= nextAlertRefreshTime)
            {
                // Renovar la alerta espaciada en el tiempo mientras Mateo siga visible (sin spamear cada frame)
                nextAlertRefreshTime = Time.time + 3.0f;
                PatrolAI.TriggerDogAlert(coyoteAlertDuration);
            }

            // 2. Si Mateo está a rango de mordida cuerpo a cuerpo, dañarlo (-1 vida)
            if (distanceToPlayer <= biteRadius)
            {
                TryBitePlayer();
            }
        }
        else
        {
            // Si Mateo salió del medio círculo o se escondió en un cactus/roca
            if (isBarking)
            {
                if (lostSightTime < 0f)
                {
                    lostSightTime = Time.time;
                }

                // Esperar el breve tiempo de calma antes de apagar el ladrido y reanudar la patrulla
                if (Time.time - lostSightTime >= calmDownDelay)
                {
                    StopBarking();
                }
            }
        }
    }

    private void StartBarking()
    {
        isBarking = true;
        nextAlertRefreshTime = Time.time + 3.0f;

        if (patrolAI != null)
        {
            patrolAI.isAlerted = true; // El perro se detiene en seco
        }

        Debug.Log($"<color=yellow>[GuardDogAI] ¡GUAU! ¡GUAU! {gameObject.name} detectó a Mateo. ¡Coyotes en alerta máxima por {coyoteAlertDuration} segundos!</color>");
        OnDogBarkStateChanged?.Invoke(true, transform.position);

        // Dispara la alerta de 5 segundos en todos los coyotes (capturan al instante si ven a Mateo)
        PatrolAI.TriggerDogAlert(coyoteAlertDuration);
    }

    private void StopBarking()
    {
        isBarking = false;
        lostSightTime = -1f;

        if (patrolAI != null)
        {
            patrolAI.isAlerted = false; // El perro reanuda su patrullaje
        }

        Debug.Log($"[GuardDogAI] {gameObject.name} se calmó. Mateo salió del medio círculo o se ocultó. Reanudando patrulla.");
        OnDogBarkStateChanged?.Invoke(false, transform.position);
    }

    private void TryBitePlayer()
    {
        if (Time.time < nextBiteTime) return;

        if (cachedPlayerHealth != null && !cachedPlayerHealth.isDead && !cachedPlayerHealth.isInvulnerable)
        {
            nextBiteTime = Time.time + biteCooldown;
            Debug.Log($"<color=red>[GuardDogAI] ¡MORDIDA! {gameObject.name} mordió a Mateo (-1 corazón).</color>");
            cachedPlayerHealth.TakeDamage(1, DefeatReason.DogAttack);
        }
    }

    private void OnDisable()
    {
        if (isBarking)
        {
            StopBarking();
        }
    }

    private void OnDrawGizmosSelected()
    {
        Vector2 center = transform.position;
        Vector2 facing = Application.isPlaying ? GetFacingDirection() : Vector2.right;
        if (facing.sqrMagnitude < 0.01f) facing = Vector2.right;

        float baseAngle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg;

        Gizmos.color = isBarking ? Color.red : Color.yellow;

        // Dibujar arco de 180 grados (MEDIO CÍRCULO) hacia el frente
        int segments = 24;
        Vector3 lastPoint = Vector3.zero;
        Vector3 firstPoint = Vector3.zero;

        for (int i = 0; i <= segments; i++)
        {
            // De -90° a +90° respecto al frente
            float angle = (baseAngle - 90f) + (180f / segments) * i;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 currentPoint = (Vector3)center + new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * detectionRadius;

            if (i == 0) firstPoint = currentPoint;
            if (i > 0)
            {
                Gizmos.DrawLine(lastPoint, currentPoint);
            }
            lastPoint = currentPoint;
        }

        // Línea plana de diámetro para cerrar visualmente el medio círculo
        Gizmos.DrawLine(firstPoint, lastPoint);

        // Círculo pequeño de mordida
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(center, biteRadius);
    }
}
