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

    private Vector2 movementInput;

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

        if (moveX < 0 && spriteRenderer != null)
        {
            spriteRenderer.flipX = true;  // Mirar a la izquierda
        }
        else if (moveX > 0 && spriteRenderer != null)
        {
            spriteRenderer.flipX = false; // Mirar a la derecha
        }

        bool isMoving = movementInput.sqrMagnitude > 0.01f;

        if (animator != null)
        {
            animator.SetBool("isMoving", isMoving);
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) return;

        float finalSpeed = baseSpeed * speedMultiplier;
        rb.linearVelocity = movementInput * finalSpeed;
    }
}
