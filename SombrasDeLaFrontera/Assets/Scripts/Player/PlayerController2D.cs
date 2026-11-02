using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class PlayerController2D : MonoBehaviour
{
    [Header("Parámetros de Velocidad")]
    [Tooltip("Velocidad base de Mateo en unidades por segundo")]
    [SerializeField] private float baseSpeed = 4.5f;

    [Tooltip("Multiplicador para arena movediza (0.5) o boost de tuna (1.3)")]
    public float speedMultiplier = 1.0f;

    [Header("Referencias de Componentes")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Audio y Partículas")]
    [Tooltip("Sistema de partículas de polvo emitido al desplazarse sobre la arena")]
    [SerializeField] private ParticleSystem dustParticles;

    [Tooltip("Cadencia de los pasos sobre la arena en segundos")]
    [SerializeField] private float footstepInterval = 0.38f;
    private float footstepTimer = 0f;

    [Header("Sigilo y Cobertura")]
    [Tooltip("Indica si Mateo se encuentra actualmente a cubierto (ignorado por linternas y perros)")]
    public bool isHiding = false;

    [Tooltip("Segundos que Mateo debe quedarse quieto junto a una cobertura (del lado de abajo) para agacharse")]
    [SerializeField] private float crouchDelay = 0.2f;

    [Tooltip("Porcentaje del cuerpo AGACHADO que debe tapar el obstáculo para poder esconderse")]
    [Range(0.1f, 1f)]
    [SerializeField] private float crouchCoverRequired = 0.6f;

    [Tooltip("Porcentaje del cuerpo DE PIE que debe tapar el obstáculo para quedar oculto sin agacharse (caminando)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float standingCoverRequired = 0.9f;

    [Tooltip("Alto del cuerpo agachado respecto al cuerpo de pie")]
    [Range(0.2f, 1f)]
    [SerializeField] private float crouchHeightFactor = 0.6f;

    [Tooltip("Ancho del cuerpo de Mateo respecto al ancho de su sprite (el sprite tiene márgenes transparentes)")]
    [Range(0.2f, 1f)]
    [SerializeField] private float bodyWidthFactor = 0.55f;

    /// <summary>Verdadero mientras Mateo está agachado junto a una cobertura.</summary>
    public bool isCrouching { get; private set; }

    private Vector2 movementInput;
    private Collider2D bodyCollider;
    private Vector2 standingSize;
    private float stillTimer = 0f;

    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsMovingUpHash = Animator.StringToHash("isMovingUp");
    private static readonly int IsMovingDownHash = Animator.StringToHash("isMovingDown");
    private static readonly int IsCrouchingHash = Animator.StringToHash("isCrouching");
    private bool animatorHasCrouch;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (dustParticles == null)
            dustParticles = GetComponentInChildren<ParticleSystem>();

        bodyCollider = GetComponent<Collider2D>();
        animatorHasCrouch = HasAnimatorParameter(IsCrouchingHash);
    }

    private void Start()
    {
        // Tamaño del cuerpo de pie (sprite de reposo) para medir cuánto lo tapan las coberturas
        if (spriteRenderer != null)
        {
            Vector3 size = spriteRenderer.bounds.size;
            standingSize = new Vector2(size.x * bodyWidthFactor, size.y);
        }
    }

    private bool HasAnimatorParameter(int hash)
    {
        if (animator == null || animator.runtimeAnimatorController == null) return false;
        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.nameHash == hash) return true;
        }
        return false;
    }

    private void Update()
    {
        // Juego en pausa, tutorial abierto o fin de partida: no leer controles
        if (Time.timeScale <= 0f)
        {
            movementInput = Vector2.zero;
            return;
        }

        float moveX = 0f;
        float moveY = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY += 1f;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (moveX == 0f && moveY == 0f)
        {
            moveX = Input.GetAxisRaw("Horizontal");
            moveY = Input.GetAxisRaw("Vertical");
        }
#endif

        movementInput = new Vector2(moveX, moveY).normalized;

        bool isMoving = movementInput.sqrMagnitude > 0.01f;
        bool isMovingUp = isMoving && (moveY > 0.1f && moveY >= Mathf.Abs(moveX));
        bool isMovingDown = isMoving && (moveY < -0.1f && Mathf.Abs(moveY) >= Mathf.Abs(moveX));

        if (isMovingUp || isMovingDown)
        {
            if (spriteRenderer != null) spriteRenderer.flipX = false;
        }
        else if (moveX < 0 && spriteRenderer != null)
        {
            spriteRenderer.flipX = true;  // Mirar a la izquierda
        }
        else if (moveX > 0 && spriteRenderer != null)
        {
            spriteRenderer.flipX = false; // Mirar a la derecha
        }

        EvaluateStealth(isMoving);

        if (animator != null)
        {
            animator.SetBool(IsMovingHash, isMoving);
            animator.SetBool(IsMovingUpHash, isMovingUp);
            animator.SetBool(IsMovingDownHash, isMovingDown);
            if (animatorHasCrouch) animator.SetBool(IsCrouchingHash, isCrouching);
        }

        // Manejo de sonido de pasos sobre la arena y partículas de polvo
        if (isMoving)
        {
            footstepTimer -= Time.deltaTime;
            if (footstepTimer <= 0f)
            {
                float currentSpeedFactor = Mathf.Max(0.5f, speedMultiplier * boostMultiplier);
                footstepTimer = footstepInterval / currentSpeedFactor;
                AudioManager.Instance?.PlayFootstep();
            }

            if (dustParticles != null && !dustParticles.isPlaying)
            {
                dustParticles.Play();
            }
        }
        else
        {
            footstepTimer = 0f;
            if (dustParticles != null && dustParticles.isPlaying)
            {
                dustParticles.Stop();
            }
        }
    }

    /// <summary>
    /// Mide cuánto tapan las coberturas cercanas el cuerpo de Mateo y decide si se agacha y si queda oculto:
    /// - Quieto pegado al lado de abajo de un obstáculo que tapa su cuerpo agachado: se agacha y queda oculto.
    /// - Caminando: solo queda oculto si el obstáculo lo tapa casi por completo estando de pie.
    /// </summary>
    private void EvaluateStealth(bool isMoving)
    {
        stillTimer = isMoving ? 0f : stillTimer + Time.deltaTime;

        Vector2 feet = GetFeetPosition();
        float bodyWidth = standingSize.x > 0f ? standingSize.x : 0.16f;
        float bodyHeight = standingSize.y > 0f ? standingSize.y : 0.58f;

        Rect standingBody = new Rect(feet.x - bodyWidth * 0.5f, feet.y, bodyWidth, bodyHeight);
        Rect crouchedBody = new Rect(feet.x - bodyWidth * 0.5f, feet.y, bodyWidth, bodyHeight * crouchHeightFactor);

        float bestStanding = 0f;
        float bestCrouched = 0f;
        var covers = CoverZone2D.ActiveCovers;
        for (int i = 0; i < covers.Count; i++)
        {
            CoverZone2D cover = covers[i];
            if (cover == null || !cover.IsOnHidingSide(feet.y)) continue;

            bestStanding = Mathf.Max(bestStanding, cover.Coverage(standingBody));
            bestCrouched = Mathf.Max(bestCrouched, cover.Coverage(crouchedBody));
        }

        isCrouching = !isMoving && stillTimer >= crouchDelay && bestCrouched >= crouchCoverRequired;
        bool hidden = isCrouching || bestStanding >= standingCoverRequired;

        if (hidden != isHiding)
        {
            Debug.Log($"[PlayerController2D] Estado de sigilo de Mateo: {(hidden ? (isCrouching ? "AGACHADO Y OCULTO" : "A CUBIERTO") : "AL DESCUBIERTO")}");
        }

        isHiding = hidden;
        CoverZone2D.SetPlayerHidden(isHiding);
    }

    /// <summary>Punto de los pies de Mateo (base de su collider).</summary>
    public Vector2 GetFeetPosition()
    {
        if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            return new Vector2(b.center.x, b.min.y);
        }
        if (spriteRenderer != null)
        {
            Bounds b = spriteRenderer.bounds;
            return new Vector2(b.center.x, b.min.y);
        }
        return transform.position;
    }

    private void OnDisable()
    {
        // Al morir o desactivarse, Mateo deja de contar como oculto
        isCrouching = false;
        isHiding = false;
        CoverZone2D.SetPlayerHidden(false);
        if (animator != null)
        {
            animator.SetBool(IsMovingHash, false);
            if (animatorHasCrouch) animator.SetBool(IsCrouchingHash, false);
        }
    }

    [Tooltip("Multiplicador temporal de velocidad (ej. boost de Tuna)")]
    public float boostMultiplier = 1.0f;

    private Coroutine boostCoroutine;

    /// <summary>
    /// Aplica un aumento de velocidad temporal acumulable con el terreno.
    /// </summary>
    public void ApplySpeedBoost(float boost, float duration)
    {
        if (boostCoroutine != null) StopCoroutine(boostCoroutine);
        boostCoroutine = StartCoroutine(SpeedBoostRoutine(boost, duration));
    }

    private System.Collections.IEnumerator SpeedBoostRoutine(float boost, float duration)
    {
        boostMultiplier = boost;
        Debug.Log($"[PlayerController2D] ¡Boost de Tuna activado! Velocidad aumentada a x{boost:F2} por {duration} segundos.");
        yield return new WaitForSeconds(duration);
        boostMultiplier = 1.0f;
        Debug.Log("[PlayerController2D] Boost de velocidad finalizado.");
        boostCoroutine = null;
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        float finalSpeed = baseSpeed * speedMultiplier * boostMultiplier;
        rb.linearVelocity = movementInput * finalSpeed;
    }
}
