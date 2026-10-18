using System.Collections;
using UnityEngine;

/// <summary>
/// Gestiona la salud de Mateo (3 vidas/corazones), el daño por fauna o enemigos,
/// y el tiempo de invulnerabilidad parpadeante tras recibir impacto.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    [Header("Configuración de Vidas")]
    [Tooltip("Cantidad máxima de corazones de Mateo")]
    [SerializeField] private int maxHealth = 3;

    [Tooltip("Duración de la invulnerabilidad tras recibir daño (segundos)")]
    [SerializeField] private float invulnerabilityDuration = 1.5f;

    [Tooltip("Intervalo de parpadeo visual durante la invulnerabilidad")]
    [SerializeField] private float flashInterval = 0.15f;

    [Header("Referencias Opcionales")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    public int currentHealth { get; private set; }
    public bool isInvulnerable { get; private set; }
    public bool isDead { get; private set; }

    // Eventos estáticos para que el HUD o GameManager se enteren sin acoplamiento
    public static event System.Action<int, int> OnHealthChanged; // (actual, máxima)
    public static event System.Action OnPlayerDied;

    private Color normalColor = Color.white;

    private void Awake()
    {
        currentHealth = maxHealth;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (spriteRenderer != null)
            normalColor = spriteRenderer.color;
    }

    private void Start()
    {
        // Notificar salud inicial al HUD
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    /// <summary>
    /// Aplica daño a Mateo restando vidas y activando invulnerabilidad temporal.
    /// </summary>
    public void TakeDamage(int amount = 1)
    {
        if (isDead || isInvulnerable) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        Debug.Log($"[PlayerHealth] Mateo recibió daño (-{amount}). Vidas restantes: {currentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            StartCoroutine(InvulnerabilityRoutine());
        }
    }

    /// <summary>
    /// Recupera vidas (por ejemplo, con un botiquín).
    /// </summary>
    public void Heal(int amount = 1)
    {
        if (isDead) return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Debug.Log($"[PlayerHealth] Mateo recuperó salud (+{amount}). Vidas: {currentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        isInvulnerable = true;
        float elapsed = 0f;

        while (elapsed < invulnerabilityDuration)
        {
            if (spriteRenderer != null)
            {
                // Alternar visibilidad/alfa para efecto de parpadeo
                Color c = normalColor;
                c.a = (c.a == 1f) ? 0.25f : 1f;
                spriteRenderer.color = c;
            }

            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        // Restaurar estado normal
        if (spriteRenderer != null)
        {
            Color c = normalColor;
            c.a = 1f;
            spriteRenderer.color = c;
        }

        isInvulnerable = false;
    }

    private void Die()
    {
        isDead = true;
        Debug.Log("[PlayerHealth] ¡GAME OVER! Mateo se ha quedado sin vidas.");

        // Desactivar movimiento del jugador
        PlayerController2D controller = GetComponent<PlayerController2D>();
        if (controller != null)
        {
            controller.enabled = false;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        OnPlayerDied?.Invoke();
    }
}
