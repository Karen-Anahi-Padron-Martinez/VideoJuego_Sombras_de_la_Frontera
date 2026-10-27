using UnityEngine;

/// <summary>
/// Control de cámara vertical fija y centrada:
/// - X siempre centrado en 0 (no se mueve hacia los lados).
/// - Al inicio: La cámara se detiene exactamente en el borde inferior del mapa (mapBottomY),
///   mostrando a Mateo en la parte inferior de la pantalla sin mostrar nada por debajo del mapa.
/// - Durante la travesía: La cámara sigue suavemente a Mateo hacia el norte con visión hacia adelante.
/// - Al final: La cámara se detiene en el borde superior del mapa (mapTopY),
///   permitiendo a Mateo avanzar hasta la meta en el río sin pasarse del mapa.
/// - Sacudida breve al recibir daño o al ser derrotado (funciona aunque el juego esté congelado).
/// </summary>
public class CameraFollowVertical : MonoBehaviour
{
    [Header("Objetivo a Seguir")]
    [Tooltip("El transform de Mateo (mateo_part1_7)")]
    [SerializeField] private Transform target;

    [Header("Límites Exactos del Mapa del Desierto")]
    [Tooltip("Borde inferior del mapa (donde inicia el Tramo 1)")]
    [SerializeField] private float mapBottomY = 0.0f;

    [Tooltip("Borde superior del mapa (orilla del río en el Tramo 4)")]
    [SerializeField] private float mapTopY = 35.5f;

    [Header("Ajustes de Seguimiento")]
    [Tooltip("Offset vertical hacia el norte para ver más pantalla hacia adelante")]
    [SerializeField] private float verticalOffset = 1.0f;

    [Tooltip("Velocidad de suavizado vertical")]
    [SerializeField] private float smoothSpeed = 6f;

    [Header("Encuadre")]
    [Tooltip("Acerca la cámara lo necesario para que el mapa llene todo el ancho de la pantalla (sin franjas lisas a los lados)")]
    [SerializeField] private bool fitMapWidth = true;

    [Tooltip("Ancho del mapa del desierto en unidades (572 px / 100)")]
    [SerializeField] private float mapWidth = 5.72f;

    [Header("Sacudida de Cámara")]
    [Tooltip("Intensidad de la sacudida al recibir daño")]
    [SerializeField] private float damageShake = 0.06f;

    [Tooltip("Intensidad de la sacudida al ser derrotado")]
    [SerializeField] private float defeatShake = 0.1f;

    private Camera cam;
    private float baseOrthoSize;
    private float followY;
    private float shakeMagnitude;
    private float shakeEndTime;
    private float shakeDuration;

    private void OnEnable()
    {
        PlayerHealth.OnDamaged += HandleDamaged;
        PlayerHealth.OnPlayerDefeated += HandleDefeated;
    }

    private void OnDisable()
    {
        PlayerHealth.OnDamaged -= HandleDamaged;
        PlayerHealth.OnPlayerDefeated -= HandleDefeated;
    }

    private void HandleDamaged(int amount) => Shake(damageShake, 0.25f);
    private void HandleDefeated() => Shake(defeatShake, 0.35f);

    /// <summary>
    /// Sacude la cámara durante unos instantes (usa tiempo real).
    /// </summary>
    public void Shake(float magnitude, float duration)
    {
        shakeMagnitude = magnitude;
        shakeDuration = duration;
        shakeEndTime = Time.unscaledTime + duration;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
        followY = transform.position.y;
        if (cam != null) baseOrthoSize = cam.orthographicSize;
    }

    /// <summary>
    /// Ajusta el tamaño ortográfico para no mostrar más allá de los bordes laterales del mapa.
    /// Nunca aleja la cámara más que el tamaño configurado en la escena.
    /// </summary>
    private void FitToMapWidth()
    {
        if (!fitMapWidth || cam == null || !cam.orthographic || cam.aspect <= 0f) return;
        float sizeForWidth = mapWidth / (2f * cam.aspect);
        cam.orthographicSize = Mathf.Min(baseOrthoSize, sizeForWidth);
    }

    private void Start()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.Find("mateo_part1_7");
            if (playerObj == null) playerObj = GameObject.Find("Player");
            if (playerObj != null) target = playerObj.transform;
        }

        if (cam == null) cam = GetComponent<Camera>();
        FitToMapWidth();

        // Posicionar de inmediato al iniciar
        if (target != null && cam != null)
        {
            float halfHeight = cam.orthographicSize;
            float minY = mapBottomY + halfHeight;
            float maxY = mapTopY - halfHeight;

            float initialY = Mathf.Clamp(target.position.y + verticalOffset, minY, maxY);
            followY = initialY;
            transform.position = new Vector3(0f, initialY, -10f);
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();
        FitToMapWidth(); // Por si cambia el tamaño de la ventana

        float halfHeight = (cam != null) ? cam.orthographicSize : 3.5f;

        // El borde inferior de la cámara NUNCA baja de mapBottomY (0.0)
        float minY = mapBottomY + halfHeight;

        // El borde superior de la cámara NUNCA sube de mapTopY (35.5)
        float maxY = mapTopY - halfHeight;

        // Si el mapa es más alto que la vista, acotar Y
        if (minY < maxY)
        {
            float targetY = Mathf.Clamp(target.position.y + verticalOffset, minY, maxY);
            followY = Mathf.Lerp(followY, targetY, smoothSpeed * Time.deltaTime);

            // X estrictamente centrado en 0 (más la sacudida temporal)
            Vector2 shake = Vector2.zero;
            float remaining = shakeEndTime - Time.unscaledTime;
            if (remaining > 0f && shakeDuration > 0f)
            {
                shake = Random.insideUnitCircle * shakeMagnitude * (remaining / shakeDuration);
            }

            transform.position = new Vector3(shake.x, followY + shake.y, -10f);
        }
    }
}
