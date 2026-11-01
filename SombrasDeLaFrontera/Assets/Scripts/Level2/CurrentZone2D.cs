using UnityEngine;

/// <summary>
/// Zona de corriente del Río Bravo (Nivel 2).
/// - Lateral: el agua arrastra a Mateo hacia un lado; si se queda quieto termina contra la orilla.
/// - Contracorriente: el agua baja hacia el sur y frena su avance hacia la meta.
/// Mientras Mateo nada dentro del trigger, su SwimController2D suma la velocidad del agua (flow) a la suya.
/// También anima los cuadros de agua del sprite (espuma y flechas) para que se vea la dirección del flujo.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class CurrentZone2D : MonoBehaviour
{
    public enum CurrentType
    {
        Lateral,         // Empuje hacia un lado (izquierda o derecha)
        CounterCurrent   // Contracorriente que frena el avance hacia el norte
    }

    [Header("Corriente")]
    [SerializeField] private CurrentType currentType = CurrentType.Lateral;

    [Tooltip("Velocidad (unidades/segundo) y dirección con la que el agua arrastra a Mateo")]
    [SerializeField] private Vector2 flow = new Vector2(1.4f, 0f);

    [Header("Animación del Agua")]
    [Tooltip("Cuadros de la animación de la corriente (espuma y flechas)")]
    [SerializeField] private Sprite[] frames;

    [SerializeField] private float framesPerSecond = 6f;

    [SerializeField] private SpriteRenderer waterRenderer;

    private SwimController2D swimmer;
    private bool swimmerInside;

    public CurrentType Type => currentType;
    public Vector2 Flow => flow;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
        if (waterRenderer == null) waterRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Update()
    {
        if (waterRenderer == null || frames == null || frames.Length == 0) return;
        int index = Mathf.FloorToInt(Time.time * framesPerSecond) % frames.Length;
        if (waterRenderer.sprite != frames[index]) waterRenderer.sprite = frames[index];
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (swimmerInside) return;
        SwimController2D swim = collision.GetComponentInParent<SwimController2D>();
        if (swim == null) return;

        swimmer = swim;
        swimmerInside = true;
        swimmer.AddCurrent(flow);
        Debug.Log($"[CurrentZone2D] Mateo entró a {(currentType == CurrentType.Lateral ? "una corriente lateral" : "una contracorriente")} ({gameObject.name}).");
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!swimmerInside || collision.GetComponentInParent<SwimController2D>() != swimmer) return;
        ReleaseSwimmer();
    }

    private void OnDisable()
    {
        if (swimmerInside) ReleaseSwimmer();
    }

    private void ReleaseSwimmer()
    {
        if (swimmer != null) swimmer.RemoveCurrent(flow);
        swimmerInside = false;
        swimmer = null;
    }

    private void OnDrawGizmos()
    {
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col == null) return;
        Vector3 center = transform.position + (Vector3)col.offset;
        Gizmos.color = currentType == CurrentType.Lateral ? new Color(0.2f, 0.7f, 1f, 0.35f) : new Color(1f, 0.3f, 0.3f, 0.35f);
        Gizmos.DrawCube(center, col.size);
        Gizmos.color = Color.white;
        Gizmos.DrawLine(center, center + (Vector3)(flow.normalized * Mathf.Min(col.size.x, col.size.y) * 0.45f));
    }
}
