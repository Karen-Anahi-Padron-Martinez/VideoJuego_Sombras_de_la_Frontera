using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona de cobertura física (cactus, rocas, nopaleras, matorrales y vehículos abandonados).
/// Sigilo con lógica de vista 3/4:
/// - Mateo se esconde del lado de ABAJO del obstáculo (el lado que ve el jugador): el obstáculo queda
///   entre Mateo y el desierto hacia el norte, por donde patrullan coyotes y perros.
/// - Debe estar pegado a la base del obstáculo y alineado con él (ya no basta con rozarlo con un pixel del pie).
/// - El obstáculo debe ser lo bastante alto para tapar su cuerpo: agachado se esconde tras rocas, arbustos y carros;
///   de pie solo tras obstáculos altos (nopaleras, cactus).
/// PlayerController2D usa esta medición: si Mateo se queda quieto junto a una cobertura, se agacha y queda oculto.
/// </summary>
public class CoverZone2D : MonoBehaviour
{
    [Header("Área que tapa el obstáculo")]
    [Tooltip("Ancho del área de cobertura respecto al ancho del sprite (el sprite tiene márgenes; un tronco delgado tapa menos)")]
    [Range(0.1f, 1f)]
    [SerializeField] private float coverWidth = 0.8f;

    [Tooltip("Alto del área de cobertura respecto al alto del sprite, medido desde la base")]
    [Range(0.1f, 1f)]
    [SerializeField] private float coverHeight = 0.9f;

    [Tooltip("Distancia máxima (unidades) entre los pies de Mateo y la base del obstáculo para contar como pegado a él")]
    [SerializeField] private float hideDistance = 0.3f;

    [Tooltip("Margen de altura: el obstáculo debe sobrepasar el cuerpo de Mateo este porcentaje para taparlo por completo")]
    [SerializeField] private float heightMargin = 1.3f;

    // Registro de todas las coberturas activas del nivel
    private static readonly List<CoverZone2D> activeCovers = new List<CoverZone2D>();
    public static IReadOnlyList<CoverZone2D> ActiveCovers => activeCovers;

    // Estado global de ocultamiento (lo escribe PlayerController2D y lo leen linternas y perros)
    private static bool playerHidden = false;
    public static bool isHiding => playerHidden;

    private SpriteRenderer spriteRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        activeCovers.Clear();
        playerHidden = false;
    }

    public static void SetPlayerHidden(bool hidden)
    {
        playerHidden = hidden;
    }

    public void Configure(float width, float height, float distance)
    {
        coverWidth = width;
        coverHeight = height;
        hideDistance = distance;
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        if (!activeCovers.Contains(this)) activeCovers.Add(this);
    }

    private void OnDisable()
    {
        activeCovers.Remove(this);
    }

    /// <summary>Altura (Y) de la base del obstáculo, donde toca la arena.</summary>
    public float BaseY => spriteRenderer != null ? spriteRenderer.bounds.min.y : transform.position.y;

    /// <summary>Rectángulo en mundo que realmente tapa el obstáculo.</summary>
    public Rect CoverRect
    {
        get
        {
            if (spriteRenderer == null) return new Rect(transform.position, Vector2.zero);
            Bounds b = spriteRenderer.bounds;
            float w = b.size.x * coverWidth;
            float h = b.size.y * coverHeight;
            return new Rect(b.center.x - w * 0.5f, b.min.y, w, h);
        }
    }

    /// <summary>
    /// Verdadero si los pies de Mateo están del lado de abajo del obstáculo y pegados a su base.
    /// </summary>
    public bool IsOnHidingSide(float feetY)
    {
        if (spriteRenderer == null) return false;
        float baseY = BaseY;
        return feetY <= baseY + 0.03f && feetY >= baseY - hideDistance;
    }

    /// <summary>
    /// Qué tanto tapa el obstáculo al cuerpo de Mateo visto desde el norte (0 a 1):
    /// fracción del ancho del cuerpo alineada con el obstáculo × qué tanto supera el obstáculo su estatura.
    /// </summary>
    public float Coverage(Rect body)
    {
        if (body.width <= 0f || body.height <= 0f) return 0f;
        Rect c = CoverRect;

        float ix = Mathf.Min(body.xMax, c.xMax) - Mathf.Max(body.xMin, c.xMin);
        if (ix <= 0f) return 0f;
        float horizontal = Mathf.Clamp01(ix / body.width);
        float vertical = Mathf.Clamp01((c.yMax - body.yMin) / (body.height * heightMargin));
        return horizontal * vertical;
    }

    private void OnDrawGizmosSelected()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;
        Rect r = CoverRect;
        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.8f);
        Gizmos.DrawWireCube(r.center, r.size);

        // Franja donde Mateo debe pararse para esconderse
        Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(r.center.x, BaseY - hideDistance * 0.5f, 0f), new Vector3(r.width, hideDistance, 0f));
    }
}
