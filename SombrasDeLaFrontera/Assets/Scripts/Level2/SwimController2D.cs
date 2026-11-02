using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Controlador de movimiento y nado en 8 direcciones para Mateo en el Río Bravo (Nivel 2).
/// Transiciona automáticamente entre caminar en las orillas (arena) y nadar en el agua (río)
/// según la altura de sus pies: el agua va de waterStartY (orilla sur) a waterEndY (orilla norte).
/// Incluye aceleración e inercia acuática, soporte para corrientes y sincronización con el Animator.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SwimController2D : MonoBehaviour
{
    [Header("Parámetros en Tierra (Orillas)")]
    [Tooltip("Velocidad caminando en la orilla")]
    [SerializeField] private float walkSpeed = 3.0f;

    [Tooltip("Fricción / desaceleración en tierra firme al soltar controles")]
    [SerializeField] private float groundDrag = 25f;

    [Header("Parámetros en Agua (Río Bravo)")]
    [Tooltip("Velocidad máxima de nado en unidades/segundo")]
    [SerializeField] private float swimSpeed = 2.6f;

    [Tooltip("Aceleración en el agua (mayor valor = respuesta más inmediata)")]
    [SerializeField] private float waterAcceleration = 4f;

    [Tooltip("Fricción en el agua al soltar controles (menor valor = Mateo se desliza más por inercia)")]
    [SerializeField] private float waterDrag = 1.8f;

    [Tooltip("Multiplicador general de velocidad (ej. al bucear se reduce)")]
    public float speedMultiplier = 1.0f;

    [Tooltip("Multiplicador temporal de velocidad (ej. bidón flotante)")]
    public float boostMultiplier = 1.0f;

    [Header("Límites del Agua")]
    [Tooltip("Altura (Y) donde los pies de Mateo entran al agua desde la orilla sur")]
    [SerializeField] private float waterStartY = 1.2f;

    [Tooltip("Altura (Y) donde los pies de Mateo pisan la arena de la orilla norte")]
    [SerializeField] private float waterEndY = 33.8f;

    [Header("Animación")]
    [Tooltip("Cuánto debe dominar un eje sobre el otro para cambiar entre nado de lado y nado vertical (evita trabas en diagonal)")]
    [SerializeField] private float directionHysteresis = 1.3f;

    [Tooltip("Segundos que sigue la animación de nado tras soltar los controles (evita parpadeos de reposo entre teclas)")]
    [SerializeField] private float animMoveGrace = 0.12f;

    [Header("Sonido")]
    [Tooltip("Segundos entre brazadas al nadar en la superficie")]
    [SerializeField] private float strokeInterval = 0.55f;

    [Tooltip("Segundos entre pasos al caminar por la arena de las orillas")]
    [SerializeField] private float footstepInterval = 0.38f;

    [Header("Sigilo en el Río")]
    [Tooltip("Distancia máxima (unidades) entre Mateo y un tronco, junco o montón de ramas para contar como pegado a él")]
    [SerializeField] private float hideDistance = 0.35f;

    [Header("Referencias")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    [Header("Estado Actual (Lectura)")]
    public Vector2 movementInput;
    public bool inWater = false;
    public bool isMoving = false;
    public bool isSwimming = false;
    public bool isDiving = false; // Lo controla DiveController2D (buceo)
    public bool isHiding = false; // Pegado a un tronco, junco o ramas y a la sombra de los reflectores (CoverZone2D)

    // Velocidad del agua de las corrientes en las que está Mateo (suma de todas las zonas que toca)
    private Vector2 currentFlow = Vector2.zero;
    private Coroutine boostCoroutine;
    private float soundTimer;

    private enum SwimFacing { Side, Up, Down }
    private SwimFacing facing = SwimFacing.Side;
    private float lastHorizontalSign = 1f;
    private float animMoveTimer;

    /// <summary>Velocidad con la que el agua arrastra a Mateo en este momento.</summary>
    public Vector2 CurrentFlow => currentFlow;

    // Input virtual (pantalla táctil / joystick en UI)
    private Vector2 virtualInput = Vector2.zero;

    private Collider2D bodyCollider;

    private static readonly int InWaterHash = Animator.StringToHash("inWater");
    private static readonly int IsMovingHash = Animator.StringToHash("isMoving");
    private static readonly int IsMovingUpHash = Animator.StringToHash("isMovingUp");
    private static readonly int IsMovingDownHash = Animator.StringToHash("isMovingDown");
    private static readonly int IsDivingHash = Animator.StringToHash("isDiving");
    private bool animatorHasDiving;

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        bodyCollider = GetComponent<Collider2D>();
        animatorHasDiving = HasAnimatorParameter(IsDivingHash);
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

        // 1. Lectura del Nuevo Input System
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) moveX -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) moveX += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) moveY -= 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) moveY += 1f;
        }

        if (Gamepad.current != null && moveX == 0f && moveY == 0f)
        {
            Vector2 stick = Gamepad.current.leftStick.ReadValue();
            moveX = stick.x;
            moveY = stick.y;
        }
#endif

        // 2. Fallback de Input Legacy
#if ENABLE_LEGACY_INPUT_MANAGER
        if (moveX == 0f && moveY == 0f)
        {
            moveX = Input.GetAxisRaw("Horizontal");
            moveY = Input.GetAxisRaw("Vertical");
        }
#endif

        // 3. Fallback de Input Virtual / Táctil (UI Joystick)
        if (moveX == 0f && moveY == 0f && virtualInput.sqrMagnitude > 0.01f)
        {
            moveX = virtualInput.x;
            moveY = virtualInput.y;
        }

        movementInput = new Vector2(moveX, moveY);
        if (movementInput.sqrMagnitude > 1f)
            movementInput.Normalize();

        isMoving = movementInput.sqrMagnitude > 0.01f;

        // 4. Determinar si Mateo está en el agua o en alguna de las orillas
        float feetY = GetFeetPosition().y;
        inWater = feetY >= waterStartY && feetY < waterEndY;
        isSwimming = inWater && isMoving;

        EvaluateCover(feetY);
        UpdateMovementSound();

        // 5. Animación estable: un instante sin teclas no corta el nado, y en las diagonales la dirección
        //    solo cambia cuando el otro eje domina claramente (si no, el clip se reinicia y parece trabado)
        animMoveTimer = isMoving ? animMoveGrace : Mathf.Max(0f, animMoveTimer - Time.deltaTime);
        bool animMoving = isMoving || animMoveTimer > 0f;
        if (isMoving) UpdateFacing(moveX, moveY);

        bool isMovingUp = animMoving && facing == SwimFacing.Up;
        bool isMovingDown = animMoving && facing == SwimFacing.Down;

        // 6. Volteo horizontal (FlipX)
        if (spriteRenderer != null)
        {
            if (animMoving && facing == SwimFacing.Side) spriteRenderer.flipX = lastHorizontalSign < 0f;  // De lado: hacia donde nada
            else if (inWater || isMovingUp || isMovingDown) spriteRenderer.flipX = false;                 // Flotando o nado vertical
            else if (moveX < -0.05f) spriteRenderer.flipX = true;                                       // Caminando en la orilla
            else if (moveX > 0.05f) spriteRenderer.flipX = false;
        }

        // 7. Parámetros para Animator (Swim_Up, Swim_Side, Swim_Down, Idle_Float y caminata en orilla)
        if (animator != null)
        {
            animator.SetBool(InWaterHash, inWater);
            animator.SetBool(IsMovingHash, animMoving);
            animator.SetBool(IsMovingUpHash, isMovingUp);
            animator.SetBool(IsMovingDownHash, isMovingDown);
            if (animatorHasDiving) animator.SetBool(IsDivingHash, isDiving);
        }
    }

    /// <summary>
    /// Dirección de la animación con margen: pasa de "de lado" a "arriba/abajo" solo si el eje vertical supera
    /// claramente al horizontal (y al revés), así en las diagonales no alterna entre clips en cada cuadro.
    /// </summary>
    private void UpdateFacing(float moveX, float moveY)
    {
        float ax = Mathf.Abs(moveX), ay = Mathf.Abs(moveY);
        if (ax > 0.05f) lastHorizontalSign = Mathf.Sign(moveX);

        if (facing == SwimFacing.Side)
        {
            if (ay > ax * directionHysteresis) facing = moveY > 0f ? SwimFacing.Up : SwimFacing.Down;
        }
        else if (ax > ay * directionHysteresis)
        {
            facing = SwimFacing.Side;
        }
        else if (ay > 0.1f)
        {
            facing = moveY > 0f ? SwimFacing.Up : SwimFacing.Down;
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Velocidad según el medio (tierra o agua)
        float currentMaxSpeed = (inWater ? swimSpeed : walkSpeed) * speedMultiplier * boostMultiplier;
        Vector2 targetVelocity = movementInput * currentMaxSpeed;

        // Las corrientes (CurrentZone2D) arrastran a Mateo: quieto se deja llevar, nadando en contra avanza más lento
        if (inWater)
        {
            targetVelocity += currentFlow;
        }

        // Fricción / Inercia: en tierra se detiene casi al instante, en agua acelera y se desliza suavemente
        float lerpRate;
        if (inWater)
        {
            lerpRate = isMoving ? waterAcceleration : waterDrag;
        }
        else
        {
            lerpRate = groundDrag;
        }

        Vector2 currentVelocity = rb.linearVelocity;
        // Dentro de una corriente el agua lo jala con la aceleración del nado aunque suelte los controles
        if (inWater && !isMoving && currentFlow.sqrMagnitude > 0.0001f)
        {
            lerpRate = waterAcceleration;
        }

        rb.linearVelocity = Vector2.Lerp(currentVelocity, targetVelocity, lerpRate * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Los troncos, juncos y ramas son sólidos y cortan la luz: Mateo queda oculto solo si está pegado a uno
    /// y, además, ningún reflector que lo alcance lo ve directamente (la cobertura queda entre la luz y él).
    /// Del lado equivocado de la cobertura lo descubren. Avisa a linternas y reflectores con CoverZone2D.SetPlayerHidden.
    /// </summary>
    private void EvaluateCover(float feetY)
    {
        bool hidden = false;
        if (inWater && bodyCollider != null)
        {
            var covers = CoverZone2D.ActiveCovers;
            for (int i = 0; i < covers.Count && !hidden; i++)
            {
                if (covers[i] == null) continue;
                Collider2D solid = covers[i].GetComponent<Collider2D>();
                if (solid == null || solid.isTrigger) continue;
                ColliderDistance2D d = bodyCollider.Distance(solid);
                if (d.isValid && d.distance <= hideDistance) hidden = true;
            }

            if (hidden)
            {
                Vector2 feet = new Vector2(GetFeetPosition().x, feetY);
                var lights = VisionCone2D.ActiveCones;
                for (int i = 0; i < lights.Count && hidden; i++)
                {
                    VisionCone2D light = lights[i];
                    if (light == null) continue;
                    Vector2 origin = light.transform.position;
                    if (Vector2.Distance(origin, feet) > light.Range + 0.5f) continue;
                    if (!LightOcclusion2D.IsBlocked(origin, feet)) hidden = false;  // Esta luz lo ve directamente
                }
            }
        }

        if (hidden != isHiding)
        {
            Debug.Log($"[SwimController2D] Estado de sigilo de Mateo: {(hidden ? "OCULTO tras una cobertura" : "AL DESCUBIERTO")}");
        }
        isHiding = hidden;
        CoverZone2D.SetPlayerHidden(isHiding);
    }

    /// <summary>Brazadas al nadar en la superficie (más seguidas con el bidón) y pasos en la arena de las orillas.</summary>
    private void UpdateMovementSound()
    {
        if (!isMoving || isDiving)
        {
            soundTimer = 0f;
            return;
        }

        soundTimer -= Time.deltaTime;
        if (soundTimer > 0f) return;

        if (inWater)
        {
            soundTimer = strokeInterval / Mathf.Max(0.5f, speedMultiplier * boostMultiplier);
            AudioManager.Instance?.PlaySwimStroke();
        }
        else
        {
            soundTimer = footstepInterval;
            AudioManager.Instance?.PlayFootstep();
        }
    }

    /// <summary>Punto de los pies de Mateo (base de su collider).</summary>
    public Vector2 GetFeetPosition()
    {
        if (bodyCollider != null)
        {
            Bounds b = bodyCollider.bounds;
            return new Vector2(b.center.x, b.min.y);
        }
        return transform.position;
    }

    private void OnDisable()
    {
        // Al morir o desactivarse, Mateo deja de moverse y de contar como oculto
        movementInput = Vector2.zero;
        isMoving = false;
        isHiding = false;
        CoverZone2D.SetPlayerHidden(false);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsMovingHash, false);
        }
    }

    /// <summary>Una zona de corriente (CurrentZone2D) empieza a arrastrar a Mateo con la velocidad indicada.</summary>
    public void AddCurrent(Vector2 flow)
    {
        currentFlow += flow;
    }

    /// <summary>Mateo sale de una zona de corriente.</summary>
    public void RemoveCurrent(Vector2 flow)
    {
        currentFlow -= flow;
        if (currentFlow.sqrMagnitude < 0.0001f) currentFlow = Vector2.zero;
    }

    /// <summary>
    /// Aumento de velocidad temporal (bidón flotante). Si ya hay uno activo, se reinicia su duración.
    /// </summary>
    public void ApplySpeedBoost(float boost, float duration)
    {
        if (boostCoroutine != null) StopCoroutine(boostCoroutine);
        boostCoroutine = StartCoroutine(SpeedBoostRoutine(boost, duration));
    }

    private System.Collections.IEnumerator SpeedBoostRoutine(float boost, float duration)
    {
        boostMultiplier = boost;
        Debug.Log($"[SwimController2D] ¡Impulso del bidón! Velocidad x{boost:F2} por {duration} segundos.");
        yield return new WaitForSeconds(duration);
        boostMultiplier = 1.0f;
        boostCoroutine = null;
        Debug.Log("[SwimController2D] Impulso del bidón terminado.");
    }

    /// <summary>
    /// Permite asignar input desde joysticks virtuales táctiles en pantalla
    /// </summary>
    public void SetVirtualInput(Vector2 input)
    {
        virtualInput = input;
    }
}
