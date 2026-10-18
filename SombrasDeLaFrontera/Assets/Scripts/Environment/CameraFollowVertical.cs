using UnityEngine;

/// <summary>
/// Control de cámara vertical fija y centrada:
/// - X siempre centrado en 0 (no se mueve hacia los lados).
/// - Al inicio: La cámara se detiene exactamente en el borde inferior del mapa (mapBottomY),
///   mostrando a Mateo en la parte inferior de la pantalla sin mostrar nada por debajo del mapa.
/// - Durante la travesía: La cámara sigue suavemente a Mateo hacia el norte con visión hacia adelante.
/// - Al final: La cámara se detiene en el borde superior del mapa (mapTopY),
///   permitiendo a Mateo avanzar hasta la meta en el río sin pasarse del mapa.
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

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
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

        // Posicionar de inmediato al iniciar
        if (target != null && cam != null)
        {
            float halfHeight = cam.orthographicSize;
            float minY = mapBottomY + halfHeight;
            float maxY = mapTopY - halfHeight;

            float initialY = Mathf.Clamp(target.position.y + verticalOffset, minY, maxY);
            transform.position = new Vector3(0f, initialY, -10f);
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;
        if (cam == null) cam = GetComponent<Camera>();

        float halfHeight = (cam != null) ? cam.orthographicSize : 3.5f;

        // El borde inferior de la cámara NUNCA baja de mapBottomY (0.0)
        float minY = mapBottomY + halfHeight;

        // El borde superior de la cámara NUNCA sube de mapTopY (35.5)
        float maxY = mapTopY - halfHeight;

        // Si el mapa es más alto que la vista, acotar Y
        if (minY < maxY)
        {
            float targetY = Mathf.Clamp(target.position.y + verticalOffset, minY, maxY);
            float currentY = transform.position.y;
            float smoothY = Mathf.Lerp(currentY, targetY, smoothSpeed * Time.deltaTime);

            // X estrictamente centrado en 0
            transform.position = new Vector3(0f, smoothY, -10f);
        }
    }
}
