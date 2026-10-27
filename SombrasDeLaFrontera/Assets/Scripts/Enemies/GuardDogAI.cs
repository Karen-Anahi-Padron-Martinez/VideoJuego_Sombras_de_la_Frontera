using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inteligencia Artificial del Perro Guardián (Bulldog / Rottweiler).
/// Características:
/// 1. Patrulla rutas fijas mediante PatrolAI (animación Walk / Idle).
/// 2. Cada cierto tiempo se echa a DESCANSAR (animación Sleep). Dormido solo despierta si Mateo
///    pasa muy cerca y al descubierto (lo oye/huele), en cuyo caso se levanta ladrando.
/// 3. Círculo de visión invisible en MEDIO CÍRCULO (180 grados) apuntando hacia donde mira/camina el perro.
/// 4. Si detecta a Mateo al descubierto:
///    - El perro se detiene en seco y LADRA (animación Bark + icono "!"). El ladrido SOLO alerta, no quita vida.
///    - Activa la alerta máxima de los coyotes durante 5 SEGUNDOS.
///    - Durante esos 5 segundos, si cualquier coyote ve a Mateo, lo captura al instante.
/// 5. Solo deja de ladrar cuando Mateo sale de su medio círculo o se esconde en cobertura.
/// 6. Solo si Mateo se pega demasiado al perro (casi tocándolo), éste le MUERDE (animación Bite) restándole 1 vida.
/// </summary>
[RequireComponent(typeof(PatrolAI))]
public class GuardDogAI : MonoBehaviour
{
    [Header("Rangos de Detección y Ataque")]
    [Tooltip("Radio del medio círculo de visión/olfato frontal (en metros)")]
    public float detectionRadius = 3.2f;

    [Tooltip("(Solo referencia visual) Radio aproximado de mordida dibujado en la escena")]
    public float biteRadius = 0.75f;

    [Tooltip("Separación máxima entre el cuerpo del perro y los pies de Mateo para que lo muerda (casi tocándolo)")]
    [SerializeField] private float biteDistance = 0.08f;

    [Tooltip("Tiempo mínimo entre mordidas consecutivas")]
    public float biteCooldown = 1.5f;

    [Tooltip("Duración en segundos que los coyotes permanecen en alerta máxima tras vernos el perro")]
    public float coyoteAlertDuration = 5.0f;

    [Tooltip("Tiempo tras perder de vista a Mateo antes de calmarse y reanudar la patrulla")]
    public float calmDownDelay = 0.8f;

    [Header("Descanso (Dormir)")]
    [Tooltip("Permite que el perro se eche a dormir periódicamente")]
    [SerializeField] private bool canRest = true;

    [Tooltip("Segundos de patrulla (mínimo y máximo) antes de echarse a descansar")]
    [SerializeField] private Vector2 patrolTimeBeforeRest = new Vector2(10f, 16f);

    [Tooltip("Segundos (mínimo y máximo) que permanece dormido")]
    [SerializeField] private Vector2 restDuration = new Vector2(6f, 9f);

    [Tooltip("Distancia a la que el perro dormido escucha a Mateo y despierta ladrando")]
    [SerializeField] private float sleepingHearingRadius = 1.1f;

    [Header("Máscara de Detección")]
    [SerializeField] private LayerMask obstacleMask;

    // Estado interno
    public bool isBarking { get; private set; } = false;
    public bool isResting { get; private set; } = false;
    private float nextBiteTime = 0f;
    private float lostSightTime = -1f;
    private float nextAlertRefreshTime = 0f;
    private float nextRestTime = 0f;
    private float nextBarkSoundTime = 0f;
    private float nextSnoreTime = 0f;
    private float restEndTime = 0f;
    private PatrolAI patrolAI;
    private SpriteRenderer spriteRenderer;
    private CreatureAnimator creatureAnimator;
    private AlertIndicator alertIndicator;
    private Collider2D[] dogColliders;
    private Collider2D playerCollider;
    private PlayerHealth cachedPlayerHealth;
    private Transform playerTransform;

    // Eventos estáticos de alerta global para que otros componentes escuchen
    public static event System.Action<bool, Vector2> OnDogBarkStateChanged;

    private void Awake()
    {
        patrolAI = GetComponent<PatrolAI>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        creatureAnimator = GetComponentInChildren<CreatureAnimator>();
        alertIndicator = GetComponentInChildren<AlertIndicator>(true);
        dogColliders = GetComponents<Collider2D>();
    }

    private void Start()
    {
        FindPlayerReference();
        ScheduleNextRest();
    }

    private void FindPlayerReference()
    {
        if (cachedPlayerHealth == null)
        {
            cachedPlayerHealth = FindAnyObjectByType<PlayerHealth>();
            if (cachedPlayerHealth != null)
            {
                playerTransform = cachedPlayerHealth.transform;
                playerCollider = cachedPlayerHealth.GetComponent<Collider2D>();
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
        bool isPlayerHiding = CoverZone2D.isHiding;

        // 0. Perro dormido: solo despierta si Mateo pasa muy cerca al descubierto
        if (isResting)
        {
            if (!isPlayerHiding && distanceToPlayer <= sleepingHearingRadius)
            {
                Debug.Log($"[GuardDogAI] {gameObject.name} despertó: escuchó a Mateo muy cerca.");
                EndRest();
                StartBarking();
            }
            else if (Time.time >= restEndTime)
            {
                EndRest();
            }
            UpdateAnimation();
            return;
        }

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

        // 3. Chequeo de muros/obstáculos físicos
        bool lineOfSightBlocked = false;
        LayerMask effectiveMask = obstacleMask != 0 ? obstacleMask : LayerMask.GetMask("Default", "Environment");
        if (playerInDistance && playerInFrontHalfCircle)
        {
            // RaycastAll: el rayo nace dentro del propio collider del perro, así que se ignoran
            // el perro mismo, Mateo, otras criaturas y los triggers; solo bloquean obstáculos sólidos.
            RaycastHit2D[] hits = Physics2D.RaycastAll(dogPos, toPlayer.normalized, distanceToPlayer, effectiveMask);
            foreach (RaycastHit2D hit in hits)
            {
                Collider2D col = hit.collider;
                if (col == null || col.isTrigger) continue;
                if (col.GetComponentInParent<PlayerHealth>() != null || col.CompareTag("Player")) continue;
                if (col.GetComponentInParent<PatrolAI>() != null || col.GetComponentInParent<HazardFauna>() != null) continue;

                lineOfSightBlocked = true;
                break;
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
            else if (canRest && Time.time >= nextRestTime)
            {
                StartRest();
            }
        }

        // Mordida: solo si Mateo se pega demasiado al perro (mire hacia donde mire)
        if (BodyGapToPlayer() <= biteDistance)
        {
            if (!isBarking) StartBarking();
            TryBitePlayer();
        }

        UpdateAnimation();
    }

    /// <summary>
    /// Separación real entre los colliders del perro y el collider de los pies de Mateo.
    /// </summary>
    private float BodyGapToPlayer()
    {
        if (playerCollider == null || !playerCollider.enabled || dogColliders == null) return float.MaxValue;

        float best = float.MaxValue;
        foreach (Collider2D col in dogColliders)
        {
            if (col == null || !col.enabled) continue;
            ColliderDistance2D d = col.Distance(playerCollider);
            if (d.isValid) best = Mathf.Min(best, d.distance);
        }
        return best;
    }

    /// <summary>
    /// Selecciona la animación según el estado: dormido, ladrando, caminando o quieto.
    /// La mordida se reproduce como animación única (PlayOneShot) desde TryBitePlayer.
    /// </summary>
    private void UpdateAnimation()
    {
        // Sonidos: ladridos repetidos mientras alerta y ronquidos mientras duerme
        if (isBarking && Time.time >= nextBarkSoundTime)
        {
            nextBarkSoundTime = Time.time + Random.Range(0.7f, 1.1f);
            AudioManager.Instance?.PlayDogBarkAt(transform.position);
        }
        else if (isResting && Time.time >= nextSnoreTime)
        {
            nextSnoreTime = Time.time + 2.2f;
            AudioManager.Instance?.PlayDogSnoreAt(transform.position);
        }

        if (creatureAnimator != null)
        {
            if (isResting) creatureAnimator.Play("Sleep");
            else if (isBarking) creatureAnimator.Play("Bark");
            else if (patrolAI != null && patrolAI.IsMoving) creatureAnimator.Play("Walk");
            else creatureAnimator.Play("Idle");
        }

        if (alertIndicator != null)
        {
            alertIndicator.Show(isBarking ? AlertIndicator.Kind.Exclamation : AlertIndicator.Kind.None);
        }
    }

    private void ScheduleNextRest()
    {
        nextRestTime = Time.time + Random.Range(patrolTimeBeforeRest.x, patrolTimeBeforeRest.y);
    }

    private void StartRest()
    {
        isResting = true;
        restEndTime = Time.time + Random.Range(restDuration.x, restDuration.y);
        if (patrolAI != null) patrolAI.isPaused = true;
        Debug.Log($"[GuardDogAI] {gameObject.name} se echó a descansar.");
    }

    private void EndRest()
    {
        isResting = false;
        if (patrolAI != null) patrolAI.isPaused = false;
        ScheduleNextRest();
    }

    private void StartBarking()
    {
        isBarking = true;
        nextAlertRefreshTime = Time.time + 3.0f;

        if (patrolAI != null)
        {
            patrolAI.isAlerted = true; // El perro se detiene en seco
        }

        nextBarkSoundTime = Time.time + Random.Range(0.7f, 1.1f);
        AudioManager.Instance?.PlayDogBarkAt(transform.position);
        AudioManager.Instance?.PlayAlert();

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

        // Tras un susto no se duerme de inmediato
        ScheduleNextRest();

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

            if (creatureAnimator != null) creatureAnimator.PlayOneShot("Bite", "Bark");
            AudioManager.Instance?.PlayDogBiteAt(transform.position);

            cachedPlayerHealth.TakeDamage(1, DefeatReason.DogAttack);

            // Si la mordida fue la última, terminar de verla aunque el juego se congele
            if (cachedPlayerHealth.isDead && creatureAnimator != null) creatureAnimator.UseUnscaledTime();
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

        // Círculo de oído mientras duerme
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(center, sleepingHearingRadius);
    }
}
