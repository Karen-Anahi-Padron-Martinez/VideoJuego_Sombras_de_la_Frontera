using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cono de visión 2D para la linterna de los vigilantes (Coyotes).
/// Incorpora PolygonCollider2D trigger, luz 2D y desvanecimiento suave.
/// Sistema de Detección Progresiva:
/// - Si Mateo entra a la luz al descubierto, el cono cambia gradualmente de Amarillo a Rojo durante 1.5 segundos.
/// - Si Mateo se refugia en cobertura o sale de la luz, el medidor se enfría de regreso a Amarillo.
/// - Si el medidor llega a 1.5s (100% Rojo), se activa la captura (Game Over).
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class VisionCone2D : MonoBehaviour
{
    [Header("Geometría del Cono")]
    [Tooltip("Alcance de la luz de la linterna")]
    [SerializeField] private float viewDistance = 3.5f;

    [Tooltip("Ángulo de apertura del haz de luz en grados")]
    [SerializeField] private float viewAngle = 45f;

    [Tooltip("Distancia inicial para separar el cono del pecho del coyote y desvanecer la punta")]
    [SerializeField] private float originOffset = 0.35f;

    [Tooltip("Segmentos de subdivisión del arco")]
    [SerializeField] private int segments = 14;

    [Header("Tiempo de Detección y Alerta")]
    [Tooltip("Segundos continuos que Mateo debe estar en la luz para ser capturado")]
    [SerializeField] private float detectionTimeToCapture = 1.5f;

    [Tooltip("Color normal de patrulla (calma)")]
    [SerializeField] private Color patrolColor = new Color(1f, 0.95f, 0.4f, 0.35f);

    [Tooltip("Color de alerta máxima (captura)")]
    [SerializeField] private Color alertColor = new Color(1f, 0.15f, 0.1f, 0.65f);

    [Header("Máscaras de Detección")]
    [SerializeField] private LayerMask obstacleMask;

    [Header("Capa de Dibujado (Sorting Layer)")]
    [SerializeField] private string sortingLayerName = "Foreground";
    [SerializeField] private int sortingOrder = 5;

    // Componentes internos
    private PolygonCollider2D coneCollider;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh coneMesh;
    private UnityEngine.Rendering.Universal.Light2D light2D;
    private Material runtimeMaterial;

    // Estado de detección
    private float detectionTimer = 0f;
    private float lastExposedTime = -1f;
    private PlayerHealth targetPlayerHealth;
    private PatrolAI parentPatrolAI;
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private ContactFilter2D contactFilter;

    public float DetectionProgress => Mathf.Clamp01(detectionTimer / detectionTimeToCapture);

    private void Awake()
    {
        InitializeComponents();
        RebuildCone();
    }

    private void Start()
    {
        InitializeComponents();
        RebuildCone();
    }

    private void OnValidate()
    {
        if (coneCollider == null) coneCollider = GetComponent<PolygonCollider2D>();
        if (coneCollider != null) coneCollider.isTrigger = true;
        RebuildCone();
    }

    private void InitializeComponents()
    {
        coneCollider = GetComponent<PolygonCollider2D>();
        if (coneCollider != null) coneCollider.isTrigger = true;

        parentPatrolAI = GetComponentInParent<PatrolAI>();

        // Asegurar que el objeto cono tenga su propio Rigidbody2D Kinematic
        // para que Unity 2D le despache eventos de física directamente al hijo
        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.simulated = true;

        contactFilter = new ContactFilter2D();
        contactFilter.useTriggers = true;
        contactFilter.SetLayerMask(Physics2D.AllLayers);

        // Si existe un LineRenderer residual en el objeto, apagarlo para eliminar la línea vertical rígida
        LineRenderer lr = GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.enabled = false;
        }

        SetupVisualMesh();
        SetupLight2D();
    }

    private void SetupVisualMesh()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null) meshFilter = gameObject.AddComponent<MeshFilter>();

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                     ?? Shader.Find("Sprites/Default")
                     ?? Shader.Find("Unlit/Color");

        if (runtimeMaterial == null)
        {
            runtimeMaterial = new Material(shader);
        }

        runtimeMaterial.color = patrolColor;
        meshRenderer.sharedMaterial = runtimeMaterial;
        meshRenderer.sortingLayerName = sortingLayerName;
        meshRenderer.sortingOrder = sortingOrder;
    }

    private void SetupLight2D()
    {
        light2D = GetComponent<UnityEngine.Rendering.Universal.Light2D>();

        if (light2D != null)
        {
            light2D.lightType = UnityEngine.Rendering.Universal.Light2D.LightType.Point;
            light2D.pointLightOuterAngle = viewAngle;
            light2D.pointLightInnerAngle = viewAngle * 0.4f;
            light2D.pointLightOuterRadius = viewDistance;
            light2D.color = patrolColor;
            light2D.intensity = 1.3f;
        }
    }

    public void RebuildCone()
    {
        List<Vector2> points = new List<Vector2> { new Vector2(originOffset, 0f) };

        float halfAngle = viewAngle * 0.5f;
        float angleStep = viewAngle / segments;

        for (int i = 0; i <= segments; i++)
        {
            float currentAngle = -halfAngle + (angleStep * i);
            float rad = currentAngle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            points.Add(dir * viewDistance);
        }

        if (coneCollider != null)
        {
            coneCollider.SetPath(0, points.ToArray());
        }

        // Construir malla con doble cara y desvanecimiento suave
        if (coneMesh == null)
        {
            coneMesh = new Mesh { name = "VisionConeMesh" };
        }
        else
        {
            coneMesh.Clear();
        }

        Vector3[] vertices = new Vector3[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            vertices[i] = new Vector3(points[i].x, points[i].y, 0f);
        }

        int numTriangles = segments;
        int[] triangles = new int[numTriangles * 6];

        for (int i = 0; i < numTriangles; i++)
        {
            // Cara Frontal
            triangles[i * 6] = 0;
            triangles[i * 6 + 1] = i + 1;
            triangles[i * 6 + 2] = i + 2;

            // Cara Trasera
            triangles[i * 6 + 3] = 0;
            triangles[i * 6 + 4] = i + 2;
            triangles[i * 6 + 5] = i + 1;
        }

        UpdateMeshColors(patrolColor);

        coneMesh.vertices = vertices;
        coneMesh.triangles = triangles;
        coneMesh.RecalculateNormals();

        if (meshFilter != null)
        {
            meshFilter.mesh = coneMesh;
        }
    }

    private void UpdateMeshColors(Color baseColor)
    {
        if (coneMesh == null || coneMesh.vertexCount == 0) return;

        Color[] colors = new Color[coneMesh.vertexCount];
        for (int i = 0; i < colors.Length; i++)
        {
            if (i == 0)
            {
                // Origen desvanecido para que no choque abruptamente con el cuerpo del coyote
                colors[i] = new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
            }
            else
            {
                // Borde suave del haz de luz
                colors[i] = new Color(baseColor.r, baseColor.g, baseColor.b, 0.40f);
            }
        }
        coneMesh.colors = colors;
    }

    private void Update()
    {
        // 0. Chequeo activo de solapamiento físico en cada fotograma
        if (coneCollider != null)
        {
            int count = coneCollider.Overlap(contactFilter, overlapResults);
            for (int i = 0; i < count; i++)
            {
                if (overlapResults[i] != null && overlapResults[i] != coneCollider)
                {
                    CheckDetection(overlapResults[i]);
                }
            }
        }

        // 1. Progresión del temporizador de detección (con margen suave para evitar parpadeo)
        bool isExposed = (Time.time - lastExposedTime) < 0.15f;
        if (isExposed)
        {
            detectionTimer += Time.deltaTime;
            // El coyote se detiene en seco al detectar al jugador
            if (parentPatrolAI != null)
            {
                parentPatrolAI.isAlerted = true;
            }
        }
        else
        {
            // Si Mateo escapó de la luz o se metió a una cobertura, enfriar la alerta de regreso a amarillo
            detectionTimer = Mathf.Max(0f, detectionTimer - (Time.deltaTime * 1.5f));
            if (parentPatrolAI != null && detectionTimer <= 0.05f)
            {
                // Al enfriarse la alerta, el coyote reanuda su patrullaje normal (a menos que haya alerta global por el perro)
                if (!PatrolAI.isHighAlertFromDog)
                {
                    parentPatrolAI.isAlerted = false;
                }
            }
        }

        // 2. Transición de color de Amarillo a Rojo según el progreso
        bool isDogAlert = PatrolAI.isHighAlertFromDog;
        float progress = isDogAlert && isExposed ? 1f : (isDogAlert ? Mathf.Max(0.45f, DetectionProgress) : DetectionProgress);
        Color currentColor = Color.Lerp(patrolColor, alertColor, progress);

        if (runtimeMaterial != null)
        {
            runtimeMaterial.color = currentColor;
        }

        if (light2D != null)
        {
            light2D.color = currentColor;
        }

        UpdateMeshColors(currentColor);

        // 3. ¡CAPTURA! (Instantánea si los coyotes están en alerta por el perro, o tras 1.5s normalmente)
        float activeTimeToCapture = isDogAlert ? 0.05f : detectionTimeToCapture;
        if (detectionTimer >= activeTimeToCapture)
        {
            if (targetPlayerHealth != null && !targetPlayerHealth.isDead)
            {
                string reason = isDogAlert ? "¡Los coyotes en alerta por el perro atraparon a Mateo al instante!" : $"Mateo permaneció {detectionTimeToCapture}s en el haz de la linterna fuera de cobertura.";
                Debug.Log($"[VisionCone2D] ¡CAPTURA! {reason}");
                targetPlayerHealth.Capture();
                detectionTimer = 0f;
            }
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        CheckDetection(collision);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckDetection(collision);
    }

    private void CheckDetection(Collider2D col)
    {
        if (!IsPlayerCollider(col, out PlayerHealth health, out PlayerController2D controller))
            return;

        if (health == null || health.isDead) return;

        // Si Mateo se encuentra dentro de una cobertura (CoverZone2D), la linterna lo ignora
        bool isHiding = CoverZone2D.isHiding;
        if (controller != null && controller.isHiding) isHiding = true;

        if (isHiding)
        {
            // Mateo está a cubierto en cactus, roca o nopalera
            return;
        }

        // Comprobar si hay obstáculos sólidos interrumpiendo la línea de visión
        if (obstacleMask != 0)
        {
            Vector2 origin = transform.position;
            Vector2 target = col.transform.position;
            Vector2 rayDir = (target - origin);
            RaycastHit2D hit = Physics2D.Raycast(origin, rayDir.normalized, rayDir.magnitude, obstacleMask);
            if (hit.collider != null && hit.collider != col)
            {
                return; // Bloqueado por un muro sólido
            }
        }

        // Mateo está expuesto en la luz
        lastExposedTime = Time.time;
        targetPlayerHealth = health;
    }

    private bool IsPlayerCollider(Collider2D col, out PlayerHealth health, out PlayerController2D controller)
    {
        health = col.GetComponentInParent<PlayerHealth>();
        if (health == null) health = col.GetComponentInChildren<PlayerHealth>();

        controller = col.GetComponentInParent<PlayerController2D>();
        if (controller == null) controller = col.GetComponentInChildren<PlayerController2D>();

        if (health != null) return true;

        if (col.CompareTag("Player") || col.name.ToLower().Contains("mateo") || col.transform.root.name.ToLower().Contains("player"))
        {
            return true;
        }

        return false;
    }
}
