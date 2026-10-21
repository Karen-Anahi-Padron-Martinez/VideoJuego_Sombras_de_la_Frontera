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

    [Header("Sigilo y Cobertura")]
    [Tooltip("Indica si Mateo se encuentra actualmente a cubierto (ignorado por linternas)")]
    public bool isHiding = false;

    private Vector2 movementInput;

    /// <summary>
    /// Establece el estado de ocultamiento de Mateo cuando entra o sale de una zona de cobertura.
    /// </summary>
    public void SetHidingState(bool state)
    {
        isHiding = state;
        Debug.Log($"[PlayerController2D] Estado de sigilo de Mateo: {(isHiding ? "A CUBIERTO" : "AL DESCUBIERTO")}");
    }

    private void Awake()
    {
        if (rb == null)
            rb = GetComponent<Rigidbody2D>();

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
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

        if (animator != null)
        {
            animator.SetBool("isMoving", isMoving);
            animator.SetBool("isMovingUp", isMovingUp);
            animator.SetBool("isMovingDown", isMovingDown);
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
