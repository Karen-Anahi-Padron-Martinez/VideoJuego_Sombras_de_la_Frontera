using System.Collections;
using UnityEngine;

public enum DefeatReason
{
    Generic,
    FlashlightCaptured,    // Coyote con linterna
    Dehydration,           // Sed / Sin agua
    SnakeBite,             // Mordedura de serpiente
    ScorpionSting,         // Picadura de alacrán
    DogAttack,             // Ataque de perro guardián
    SpiderBite             // Picadura de araña
}

/// <summary>
/// Gestiona la salud de Mateo (3 vidas/corazones), el daño por fauna o enemigos,
/// el tiempo de invulnerabilidad parpadeante tras recibir impacto y la animación de caída al perder.
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

    [Header("Derrota")]
    [Tooltip("Segundos (tiempo real) que dura la animación de caída antes de mostrar la pantalla de Game Over")]
    [SerializeField] private float defeatScreenDelay = 1.6f;

    [Header("Referencias Opcionales")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Animator animator;

    public int currentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool isInvulnerable { get; private set; }
    public bool isDead { get; private set; }
    public DefeatReason lastDefeatReason { get; private set; } = DefeatReason.Generic;

    // Eventos estáticos para que el HUD o GameManager se enteren sin acoplamiento
    public static event System.Action<int, int> OnHealthChanged; // (actual, máxima)
    public static event System.Action<int> OnDamaged;            // (cantidad) para destello rojo y sacudida de cámara
    public static event System.Action OnPlayerDefeated;          // Inicio de la caída (antes del Game Over)
    public static event System.Action OnPlayerDied;
    public static event System.Action<DefeatReason> OnPlayerDiedWithReason;

    private static readonly int IsDeadHash = Animator.StringToHash("isDead");
    private Color normalColor = Color.white;
    private Coroutine invulnerabilityCoroutine;

    private void Awake()
    {
        // Asegurar que el tiempo corra normalmente al entrar o reiniciar el nivel
        Time.timeScale = 1f;

        currentHealth = maxHealth;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

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
    public void TakeDamage(int amount = 1, DefeatReason reason = DefeatReason.Generic)
    {
        if (isDead || isInvulnerable) return;

        if (reason != DefeatReason.Generic)
        {
            lastDefeatReason = reason;
        }

        currentHealth -= amount;
        currentHealth = Mathf.Max(0, currentHealth);

        AudioManager.Instance?.PlayDamage();
        Debug.Log($"[PlayerHealth] Mateo recibió daño (-{amount}, causa: {reason}). Vidas restantes: {currentHealth}/{maxHealth}");
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamaged?.Invoke(amount);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            invulnerabilityCoroutine = StartCoroutine(InvulnerabilityRoutine());
        }
    }

    /// <summary>
    /// Derrota inmediata que ignora la invulnerabilidad (por ejemplo, deshidratación total).
    /// </summary>
    public void Kill(DefeatReason reason)
    {
        if (isDead) return;

        lastDefeatReason = reason;
        currentHealth = 0;
        OnHealthChanged?.Invoke(0, maxHealth);
        OnDamaged?.Invoke(maxHealth);
        Die();
    }

    /// <summary>
    /// Captura instantánea por linterna de vigilante coyote (Game Over directo).
    /// </summary>
    public void Capture()
    {
        if (isDead) return;

        lastDefeatReason = DefeatReason.FlashlightCaptured;
        AudioManager.Instance?.PlayAlert();
        Debug.Log("[PlayerHealth] ¡CAPTURADO! Mateo ha sido descubierto por la linterna del vigilante coyote fuera de cobertura.");
        currentHealth = 0;
        OnHealthChanged?.Invoke(0, maxHealth);
        Die();
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
                c.a = (spriteRenderer.color.a >= 0.99f) ? 0.25f : 1f;
                spriteRenderer.color = c;
            }

            yield return new WaitForSeconds(flashInterval);
            elapsed += flashInterval;
        }

        RestoreSpriteColor();
        isInvulnerable = false;
        invulnerabilityCoroutine = null;
    }

    private void RestoreSpriteColor()
    {
        if (spriteRenderer != null)
        {
            Color c = normalColor;
            c.a = 1f;
            spriteRenderer.color = c;
        }
    }

    private void Die()
    {
        isDead = true;
        AudioManager.Instance?.StopBGM();
        Debug.Log($"[PlayerHealth] ¡GAME OVER! Mateo ha sido derrotado. Causa: {lastDefeatReason}.");

        // Detener el parpadeo para que la caída se vea con el sprite completo
        if (invulnerabilityCoroutine != null)
        {
            StopCoroutine(invulnerabilityCoroutine);
            invulnerabilityCoroutine = null;
        }
        isInvulnerable = false;
        RestoreSpriteColor();

        // Detener completamente el juego (físicas, enemigos, agua y animaciones del mundo)
        Time.timeScale = 0f;

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

        // Animación de caída: corre en tiempo real aunque el mundo esté congelado
        if (animator != null)
        {
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == IsDeadHash)
                {
                    animator.SetBool(IsDeadHash, true);
                    break;
                }
            }
        }

        OnPlayerDefeated?.Invoke();
        StartCoroutine(ShowDefeatAfterFall());
    }

    private IEnumerator ShowDefeatAfterFall()
    {
        yield return new WaitForSecondsRealtime(defeatScreenDelay);

        // La melodía de derrota suena junto con la pantalla de Game Over (después del grito y la caída)
        AudioManager.Instance?.PlayGameOver();

        OnPlayerDied?.Invoke();
        OnPlayerDiedWithReason?.Invoke(lastDefeatReason);
    }
}
