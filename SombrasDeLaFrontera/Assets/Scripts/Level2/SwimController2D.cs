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

    [Tooltip("Multiplicador para objetos flotantes (ej. bidón de impulso)")]
    public float speedMultiplier = 1.0f;

    [Header("Límites del Agua")]
    [Tooltip("Altura (Y) donde los pies de Mateo entran al agua desde la orilla sur")]
    [SerializeField] private float waterStartY = 1.2f;

    [Tooltip("Altura (Y) donde los pies de Mateo pisan la arena de la orilla norte")]
    [SerializeField] private float waterEndY = 33.8f;

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

    // Fuerzas externas (corrientes de agua)
    private Vector2 externalCurrentForce = Vector2.zero;

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

        bool isMovingUp = isMoving && (moveY > 0.1f && moveY >= Mathf.Abs(moveX));
        bool isMovingDown = isMoving && (moveY < -0.1f && Mathf.Abs(moveY) >= Mathf.Abs(moveX));

        // 5. Volteo horizontal (FlipX)
        if (spriteRenderer != null)
        {
            if ((inWater && !isMoving) || isMovingUp || isMovingDown)
            {
                // Flotando quieto (de frente) o nado vertical: vista recta sin voltear
                spriteRenderer.flipX = false;
            }
            else if (moveX < -0.05f)
            {
                spriteRenderer.flipX = true;  // Nado hacia la izquierda
            }
            else if (moveX > 0.05f)
            {
                spriteRenderer.flipX = false; // Nado hacia la derecha
            }
        }

        // 6. Parámetros para Animator (Swim_Up, Swim_Side, Swim_Down, Idle_Float y caminata en orilla)
        if (animator != null)
        {
            animator.SetBool(InWaterHash, inWater);
            animator.SetBool(IsMovingHash, isMoving);
            animator.SetBool(IsMovingUpHash, isMovingUp);
            animator.SetBool(IsMovingDownHash, isMovingDown);
            if (animatorHasDiving) animator.SetBool(IsDivingHash, isDiving);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        // Velocidad según el medio (tierra o agua)
        float currentMaxSpeed = (inWater ? swimSpeed : walkSpeed) * speedMultiplier;
        Vector2 targetVelocity = movementInput * currentMaxSpeed;

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
        Vector2 newVelocity = Vector2.Lerp(currentVelocity, targetVelocity, lerpRate * Time.fixedDeltaTime);

        // Sumar empuje de corriente externa (CurrentZone2D) solo en el agua
        if (inWater)
        {
            newVelocity += externalCurrentForce * Time.fixedDeltaTime;
            externalCurrentForce = Vector2.Lerp(externalCurrentForce, Vector2.zero, 4f * Time.fixedDeltaTime);
        }

        rb.linearVelocity = newVelocity;
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
        CoverZone2D.SetPlayerHidden(false);
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsMovingHash, false);
        }
    }

    /// <summary>
    /// Permite a las zonas de corrientes (CurrentZone2D) empujar a Mateo
    /// </summary>
    public void ApplyCurrentPush(Vector2 force)
    {
        externalCurrentForce += force;
    }

    /// <summary>
    /// Permite asignar input desde joysticks virtuales táctiles en pantalla
    /// </summary>
    public void SetVirtualInput(Vector2 input)
    {
        virtualInput = input;
    }
}
