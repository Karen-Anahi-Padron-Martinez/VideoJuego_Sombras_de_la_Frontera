using UnityEngine;

/// <summary>
/// Sombra suave (elipse) bajo los pies de un personaje para que se vea apoyado sobre la arena.
/// Sigue la base del sprite del dueño y ajusta su ancho al de la animación actual
/// (por ejemplo, se ensancha cuando Mateo cae al suelo o el perro se echa a dormir).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class BlobShadow : MonoBehaviour
{
    [Tooltip("SpriteRenderer del personaje que proyecta la sombra")]
    [SerializeField] private SpriteRenderer ownerRenderer;

    [Tooltip("Ancho de la sombra respecto al ancho del sprite del personaje")]
    [SerializeField] private float widthFactor = 0.6f;

    [Tooltip("Proporción alto/ancho de la elipse")]
    [SerializeField] private float heightRatio = 0.3f;

    [Tooltip("Desplazamiento vertical respecto a la base del sprite")]
    [SerializeField] private float verticalOffset = 0.015f;

    private SpriteRenderer shadowRenderer;

    public void Configure(SpriteRenderer owner, float width, float ratio)
    {
        ownerRenderer = owner;
        widthFactor = width;
        heightRatio = ratio;
    }

    private void Awake()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        if (ownerRenderer == null && transform.parent != null)
            ownerRenderer = transform.parent.GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        if (ownerRenderer == null || shadowRenderer == null || shadowRenderer.sprite == null) return;

        shadowRenderer.enabled = ownerRenderer.enabled;
        Bounds b = ownerRenderer.bounds;
        transform.position = new Vector3(b.center.x, b.min.y + verticalOffset, transform.position.z);

        Vector2 spriteSize = shadowRenderer.sprite.bounds.size;
        float width = b.size.x * widthFactor;
        float height = width * heightRatio;
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(
            width / Mathf.Max(0.0001f, spriteSize.x * Mathf.Abs(parentScale.x)),
            height / Mathf.Max(0.0001f, spriteSize.y * Mathf.Abs(parentScale.y)),
            1f);
    }
}
