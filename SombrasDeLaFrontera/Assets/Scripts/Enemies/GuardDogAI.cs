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
/// 7. Persecución (opcional, Nivel 4): tras ladrar, el perro corre tras Mateo rodeando obstáculos (ChasePath2D).
///    Si Mateo se esconde tras una cobertura, el perro ya no lo ve ni lo muerde: va a revisar hasta donde lo vio por última
///    vez, olfatea unos segundos y regresa a patrullar. Solo lo vuelve a perseguir si Mateo sale a la vista.
///    También se rinde si se aleja demasiado de su ruta.
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

    [Header("Persecución (Nivel 4)")]
    [Tooltip("Si está activo, el perro persigue a Mateo en vez de quedarse ladrando en su lugar")]
    [SerializeField] private bool chasesPlayer = false;

    [Tooltip("Velocidad al perseguir (Mateo camina a 1 unidad por segundo)")]
    public float chaseSpeed = 0.92f;

    [Tooltip("Segundos que ladra en su lugar antes de arrancar tras Mateo (tiempo para reaccionar)")]
    [SerializeField] private float chaseReactionDelay = 0.45f;

    [Tooltip("Distancia máxima que se aleja del punto donde dejó su ruta antes de rendirse")]
    public float maxChaseDistance = 5f;

    [Tooltip("Mientras lo persigue, distancia a la que sigue viendo a Mateo (no necesita tenerlo enfrente)")]
    [SerializeField] private float chaseSightRadius = 4f;

    [Tooltip("Mientras lo busca, distancia a la que olfatea a Mateo si está al descubierto aunque no lo vea (no si está escondido)")]
    [SerializeField] private float sniffRadius = 0.4f;

    [Tooltip("Si Mateo está escondido, el perro se detiene a esta distancia del punto donde lo vio por última vez")]
    [SerializeField] private float hiddenSearchStop = 0.6f;

    [Tooltip("Segundos que olfatea donde lo vio por última vez antes de rendirse")]
    [SerializeField] private float searchDuration = 2.2f;

    [Tooltip("Al perseguir, distancia entre las patas del perro y los pies de Mateo a la que lo alcanza y muerde")]
    [SerializeField] private float chaseBiteReach = 0.3f;

    [Tooltip("Segundos que se queda quieto después de morder (ventana para escapar)")]
    [SerializeField] private float biteRecoverTime = 1.1f;

    [Tooltip("Velocidad de la animación de caminar mientras corre")]
    [SerializeField] private float chaseAnimationSpeed = 1.7f;

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

    // Persecución
    private enum ChaseState { None, Chasing, Searching, Returning }
    private ChaseState chaseState = ChaseState.None;
    private Vector2 chaseOrigin;
    private Vector2 lastSeenPos;
    private float chaseStartTime;
    private float searchEndTime;
    private float recoverUntil;
    private float nextRepathTime;
    private Vector2 pathTarget;
    private readonly List<Vector2> path = new List<Vector2>();
    private bool movedThisFrame;
    public bool IsChasing => chaseState == ChaseState.Chasing;

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

        movedThisFrame = false;
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

        if (chasesPlayer)
        {
            UpdateChase(canDetectMateo, playerPos, distanceToPlayer, isPlayerHiding, effectiveMask);
            UpdateAnimation();
            return;
        }

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

    // =========================================================================
    // PERSECUCIÓN (Nivel 4)
    // =========================================================================

    private void UpdateChase(bool canDetectMateo, Vector2 playerCenter, float distanceToPlayer, bool isPlayerHiding, LayerMask mask)
    {
        // Mientras lo persigue no necesita tenerlo enfrente: basta con verlo (no escondido y sin obstáculos en medio)
        bool tracking = !isPlayerHiding && distanceToPlayer <= chaseSightRadius && !IsSightBlocked(playerCenter, distanceToPlayer, mask);

        // El perro (pivote en las patas) persigue los PIES de Mateo, no el centro de su sprite
        Vector2 playerPos = PlayerFeet(playerCenter);
        float feetDistance = Vector2.Distance(transform.position, playerPos);
        // Patrullando solo muerde si de verdad lo toca; persiguiéndolo o buscándolo, al alcanzarlo.
        // Escondido tras una cobertura está a salvo: el perro no lo encuentra ni lo muerde.
        bool hunting = chaseState == ChaseState.Chasing || chaseState == ChaseState.Searching;
        bool touching = !isPlayerHiding && (BodyGapToPlayer() <= biteDistance || (hunting && feetDistance <= chaseBiteReach));

        if (touching)
        {
            if (chaseState == ChaseState.None || chaseState == ChaseState.Returning) BeginChase(playerPos);
            if (TryBitePlayer()) recoverUntil = Time.time + biteRecoverTime;
        }

        switch (chaseState)
        {
            case ChaseState.None:
                if (canDetectMateo) BeginChase(playerPos);
                else if (canRest && Time.time >= nextRestTime) StartRest();
                break;

            case ChaseState.Chasing:
                if (!tracking)
                {
                    // Lo perdió de vista: irá olfateando hasta donde lo vio por última vez
                    chaseState = ChaseState.Searching;
                    searchEndTime = -1f;
                    path.Clear();
                    break;
                }
                lastSeenPos = playerPos;
                if (Time.time >= nextAlertRefreshTime)
                {
                    nextAlertRefreshTime = Time.time + 3.0f;
                    PatrolAI.TriggerDogAlert(coyoteAlertDuration);
                }
                if (Vector2.Distance(transform.position, chaseOrigin) > maxChaseDistance) { BeginReturn(); break; }

                bool reacting = Time.time - chaseStartTime < chaseReactionDelay;
                if (reacting || touching || Time.time < recoverUntil) patrolAI.FaceTowards(playerPos);
                else MoveAlongPath(playerPos, chaseSpeed);
                break;

            case ChaseState.Searching:
                if (tracking || (!isPlayerHiding && feetDistance <= sniffRadius))
                {
                    // Lo volvió a ver o lo olfateó al descubierto junto a él
                    chaseState = ChaseState.Chasing;
                    lastSeenPos = playerPos;
                    AudioManager.Instance?.PlayDogBarkAt(transform.position);
                    PatrolAI.TriggerDogAlert(coyoteAlertDuration);
                    break;
                }
                if (Vector2.Distance(transform.position, chaseOrigin) > maxChaseDistance) { BeginReturn(); break; }

                if (searchEndTime < 0f)
                {
                    // Si Mateo se escondió justo ahí, se detiene antes para no toparse con él
                    float stop = isPlayerHiding ? hiddenSearchStop : 0.1f;
                    bool arrived = Vector2.Distance(transform.position, lastSeenPos) <= stop;
                    if (arrived || !MoveAlongPath(lastSeenPos, chaseSpeed)) searchEndTime = Time.time + searchDuration;
                }
                else if (Time.time >= searchEndTime)
                {
                    BeginReturn();
                }
                break;

            case ChaseState.Returning:
                if (canDetectMateo) { BeginChase(playerPos); break; }
                if (Vector2.Distance(transform.position, chaseOrigin) > 0.06f && MoveAlongPath(chaseOrigin, patrolAI.moveSpeed)) break;

                // De vuelta en su ruta: retoma la patrulla
                transform.position = chaseOrigin;
                chaseState = ChaseState.None;
                patrolAI.isPaused = false;
                ScheduleNextRest();
                break;
        }
    }

    /// <summary>
    /// Un aviso externo (el dron de vigilancia) manda al perro a revisar un punto: despierta, ladra y corre hacia allá olfateando.
    /// Si en el camino ve a Mateo, lo persigue. Solo responde si está en modo persecución.
    /// </summary>
    public void AlertToPosition(Vector2 position)
    {
        if (!chasesPlayer || chaseState == ChaseState.Chasing) return;
        if (isResting) EndRest();
        if (chaseState == ChaseState.None) chaseOrigin = transform.position;
        chaseState = ChaseState.Searching;
        searchEndTime = -1f;
        lastSeenPos = position;
        path.Clear();
        patrolAI.isPaused = false;
        if (!isBarking) StartBarking();
    }

    private Vector2 PlayerFeet(Vector2 fallback)
    {
        if (playerCollider == null || !playerCollider.enabled) return fallback;
        Bounds b = playerCollider.bounds;
        return new Vector2(b.center.x, b.min.y);
    }

    private void BeginChase(Vector2 playerPos)
    {
        if (chaseState == ChaseState.None) chaseOrigin = transform.position;
        chaseState = ChaseState.Chasing;
        chaseStartTime = Time.time;
        lastSeenPos = playerPos;
        path.Clear();
        patrolAI.isPaused = false;
        if (!isBarking) StartBarking();
    }

    private void BeginReturn()
    {
        StopBarking();
        chaseState = ChaseState.Returning;
        patrolAI.isPaused = true; // Regresa por su cuenta rodeando obstáculos; luego reanuda la patrulla
        path.Clear();
    }

    /// <summary>Avanza hacia el destino rodeando obstáculos. Devuelve falso si no hay camino o ya llegó.</summary>
    private bool MoveAlongPath(Vector2 target, float speed)
    {
        if (path.Count == 0 || Time.time >= nextRepathTime || (target - pathTarget).sqrMagnitude > 0.09f)
        {
            nextRepathTime = Time.time + 0.3f;
            pathTarget = target;
            if (!ChasePath2D.FindPath(transform.position, target, path))
            {
                path.Clear();
                // Ya casi lo alcanza (p. ej. Mateo pegado a la base de un obstáculo): se lanza directo
                if (Vector2.Distance(transform.position, target) > 1.2f) return false;
                path.Add(target);
            }
        }
        if (path.Count == 0) return false;

        Vector2 pos = transform.position;
        Vector2 next = path[0];
        Vector2 dir = next - pos;
        float distance = dir.magnitude;
        float step = speed * Time.deltaTime;
        if (distance <= step)
        {
            transform.position = next;
            path.RemoveAt(0);
        }
        else
        {
            transform.position = pos + dir / distance * step;
        }
        patrolAI.FaceTowards(next);
        movedThisFrame = true;
        return true;
    }

    private bool IsSightBlocked(Vector2 playerPos, float distanceToPlayer, LayerMask mask)
    {
        Vector2 dogPos = transform.position;
        Vector2 dir = (playerPos - dogPos).normalized;
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(dogPos, dir, distanceToPlayer, mask))
        {
            Collider2D col = hit.collider;
            if (col == null || col.isTrigger) continue;
            if (col.GetComponentInParent<PlayerHealth>() != null || col.CompareTag("Player")) continue;
            if (col.GetComponentInParent<PatrolAI>() != null || col.GetComponentInParent<HazardFauna>() != null) continue;
            return true;
        }
        return false;
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
            else if (movedThisFrame) creatureAnimator.Play("Walk");  // Persiguiendo, buscando o regresando
            else if (isBarking) creatureAnimator.Play("Bark");
            else if (patrolAI != null && patrolAI.IsMoving) creatureAnimator.Play("Walk");
            else creatureAnimator.Play("Idle");

            bool running = movedThisFrame && chaseState != ChaseState.Returning;
            creatureAnimator.SetSpeed(running ? chaseAnimationSpeed : 1f);
        }

        if (alertIndicator != null)
        {
            AlertIndicator.Kind kind = chaseState == ChaseState.Searching ? AlertIndicator.Kind.Question
                : isBarking ? AlertIndicator.Kind.Exclamation : AlertIndicator.Kind.None;
            alertIndicator.Show(kind);
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
        AlarmSystem2D.Raise(transform.position);
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

    /// <summary>Muerde a Mateo si ya pasó el tiempo entre mordidas. Devuelve verdadero si lo mordió.</summary>
    private bool TryBitePlayer()
    {
        if (Time.time < nextBiteTime) return false;

        if (cachedPlayerHealth != null && !cachedPlayerHealth.isDead && !cachedPlayerHealth.isInvulnerable)
        {
            nextBiteTime = Time.time + biteCooldown;
            Debug.Log($"<color=red>[GuardDogAI] ¡MORDIDA! {gameObject.name} mordió a Mateo (-1 corazón).</color>");

            if (creatureAnimator != null) creatureAnimator.PlayOneShot("Bite", "Bark");
            AudioManager.Instance?.PlayDogBiteAt(transform.position);

            cachedPlayerHealth.TakeDamage(1, DefeatReason.DogAttack);

            // Si la mordida fue la última, terminar de verla aunque el juego se congele
            if (cachedPlayerHealth.isDead && creatureAnimator != null) creatureAnimator.UseUnscaledTime();
            return true;
        }
        return false;
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
