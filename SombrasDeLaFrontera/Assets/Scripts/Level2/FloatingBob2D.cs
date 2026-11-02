using UnityEngine;

/// <summary>
/// Vaivén de los objetos que flotan en el Río Bravo (consumibles, troncos a la deriva).
/// Mueve suavemente el sprite hacia arriba y abajo, lo balancea unos grados y alterna sus cuadros de animación
/// (por ejemplo, las ondas del agua alrededor del bidón). Cada objeto empieza en una fase distinta para que no floten sincronizados.
/// Mueve solo el hijo visual: el collider del objeto se queda en su lugar.
/// </summary>
public class FloatingBob2D : MonoBehaviour
{
    [Header("Vaivén")]
    [Tooltip("Altura del sube y baja (unidades)")]
    [SerializeField] private float bobAmplitude = 0.04f;

    [Tooltip("Ciclos de sube y baja por segundo")]
    [SerializeField] private float bobSpeed = 0.6f;

    [Tooltip("Grados que se balancea a cada lado")]
    [SerializeField] private float swayAngle = 4f;

    [Header("Animación")]
    [Tooltip("Cuadros que se alternan (opcional)")]
    [SerializeField] private Sprite[] frames;

    [SerializeField] private float framesPerSecond = 2f;

    [SerializeField] private SpriteRenderer visual;

    private Vector3 baseLocalPosition;
    private float phase;

    private void Awake()
    {
        if (visual == null) visual = GetComponentInChildren<SpriteRenderer>();
        if (visual != null) baseLocalPosition = visual.transform.localPosition;
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (visual == null) return;

        float t = Time.time * bobSpeed * Mathf.PI * 2f + phase;
        visual.transform.localPosition = baseLocalPosition + Vector3.up * (Mathf.Sin(t) * bobAmplitude);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f) * swayAngle);

        if (frames != null && frames.Length > 0)
        {
            int index = Mathf.FloorToInt(Time.time * framesPerSecond + phase) % frames.Length;
            if (visual.sprite != frames[index]) visual.sprite = frames[index];
        }
    }
}
