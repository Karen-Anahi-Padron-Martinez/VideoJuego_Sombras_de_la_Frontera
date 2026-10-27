using UnityEngine;

/// <summary>
/// Icono flotante sobre la cabeza de los enemigos (usa los sprites de UI.png):
/// - "?" amarillo: el enemigo sospecha (la linterna empieza a detectar a Mateo).
/// - "!" rojo: el enemigo lo descubrió (captura inminente o perro ladrando).
/// Aparece con un pequeño rebote y se mantiene siempre encima del sprite del dueño.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class AlertIndicator : MonoBehaviour
{
    public enum Kind { None, Question, Exclamation }

    [SerializeField] private Sprite questionSprite;
    [SerializeField] private Sprite exclamationSprite;

    [Tooltip("SpriteRenderer del enemigo sobre el cual flota el icono")]
    [SerializeField] private SpriteRenderer ownerRenderer;

    [Tooltip("Alto del icono en unidades de mundo")]
    [SerializeField] private float worldHeight = 0.22f;

    [Tooltip("Separación vertical sobre la cabeza del enemigo")]
    [SerializeField] private float headOffset = 0.06f;

    private SpriteRenderer iconRenderer;
    private Kind currentKind = Kind.None;
    private float shownTime;
    private bool unscaled;

    private float Now => unscaled ? Time.unscaledTime : Time.time;

    private void Awake()
    {
        iconRenderer = GetComponent<SpriteRenderer>();
        if (ownerRenderer == null && transform.parent != null)
            ownerRenderer = transform.parent.GetComponent<SpriteRenderer>();
        iconRenderer.enabled = false;
    }

    public void UseUnscaledTime()
    {
        unscaled = true;
    }

    public void Show(Kind kind)
    {
        if (kind == currentKind) return;
        currentKind = kind;
        shownTime = Now;

        if (iconRenderer == null) return;
        iconRenderer.enabled = kind != Kind.None;
        if (kind == Kind.Question) iconRenderer.sprite = questionSprite;
        else if (kind == Kind.Exclamation) iconRenderer.sprite = exclamationSprite;
    }

    private void LateUpdate()
    {
        if (iconRenderer == null || !iconRenderer.enabled || iconRenderer.sprite == null || ownerRenderer == null) return;

        // Rebote de aparición (0.25 s) y flotación suave
        float t = Now - shownTime;
        float pop = t < 0.25f ? 1f + Mathf.Sin(t / 0.25f * Mathf.PI) * 0.35f : 1f;
        float bob = Mathf.Sin(Now * 5f) * 0.015f;

        Bounds ownerBounds = ownerRenderer.bounds;
        float iconHeight = worldHeight * pop;
        transform.position = new Vector3(ownerBounds.center.x, ownerBounds.max.y + headOffset + iconHeight * 0.5f + bob, transform.position.z);

        // Escala independiente de la escala del enemigo
        float spriteHeight = iconRenderer.sprite.bounds.size.y;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float s = spriteHeight > 0f ? iconHeight / spriteHeight : 1f;
        transform.localScale = new Vector3(s / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)), s / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)), 1f);
    }
}
