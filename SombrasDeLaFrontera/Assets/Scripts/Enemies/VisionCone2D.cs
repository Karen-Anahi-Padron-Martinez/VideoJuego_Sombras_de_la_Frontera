using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Cono de visión 2D para la linterna de los vigilantes (Coyotes).
/// Incorpora PolygonCollider2D trigger, luz 2D y desvanecimiento suave.
/// Sistema de Detección Progresiva:
/// - Si Mateo entra a la luz al descubierto, el cono cambia gradualmente de Amarillo a Rojo durante 1.5 segundos.
/// - Si Mateo se refugia en cobertura o sale de la luz, el medidor se enfría de regreso a Amarillo.
/// - Si el medidor llega a 1.5s (100% Rojo), se activa la captura (Game Over).
/// Detección por cercanía (aunque Mateo esté fuera del triángulo de luz):
/// - Pegado al coyote en cualquier dirección (lo escucha), o
/// - Justo enfrente de él a corta distancia.
/// En ambos casos el coyote lo encara y el medidor sube más rápido, salvo que Mateo esté escondido.
/// Modo reflector (lanchas del Nivel 2): el haz barre de lado a lado (sweepHalfAngle) con más intensidad;
/// Mateo nadando pasa inadvertido si está sumergido (isDiving) o escondido bajo un tronco o entre los juncos.
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

    [Header("Detección por Cercanía")]
    [Tooltip("Radio alrededor del coyote (en cualquier dirección) en el que detecta a Mateo aunque no lo ilumine")]
    [SerializeField] private float closeDetectionRadius = 0.5f;

    [Tooltip("Distancia frente al coyote en la que detecta a Mateo aunque esté fuera del triángulo de luz")]
    [SerializeField] private float frontDetectionRadius = 1.1f;

    [Tooltip("Medio ángulo (grados) del frente del coyote para la detección cercana")]
    [SerializeField] private float frontDetectionHalfAngle = 75f;

    [Tooltip("Multiplicador de velocidad del medidor cuando Mateo está así de cerca")]
    [SerializeField] private float closeDetectionSpeed = 2.0f;

    [Header("Modo Reflector (Lanchas)")]
    [Tooltip("Grados que barre el haz hacia cada lado de su dirección (0 = sin barrido, como la linterna de los coyotes)")]
    [SerializeField] private float sweepHalfAngle = 0f;

    [Tooltip("Segundos de una ida y vuelta completa del barrido")]
    [SerializeField] private float sweepPeriod = 4f;

    [Tooltip("Opacidad del borde del haz (0.4 = linterna; más alto = reflector de alta intensidad)")]
    [SerializeField] private float beamAlpha = 0.40f;

    [Tooltip("Motivo que se muestra en la pantalla de Game Over al ser capturado por esta luz")]
    [SerializeField] private DefeatReason defeatReason = DefeatReason.FlashlightCaptured;

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
    private float lastCloseExposedTime = -1f;
    private PlayerHealth trackedPlayer;        // Mateo (caminando con PlayerController2D o nadando con SwimController2D)
    private PlayerController2D walker;
    private SwimController2D swimmer;
    private PlayerHealth targetPlayerHealth;
    private float sweepTime;
    private PatrolAI parentPatrolAI;
    private readonly Collider2D[] overlapResults = new Collider2D[16];
    private ContactFilter2D contactFilter;
    private Vector3[] fullConeVertices;     // Cono sin recortar (espacio local)
    private Vector3[] occludedVertices;     // Cono recortado por las coberturas

    public float DetectionProgress => Mathf.Clamp01(detectionTimer / detectionTimeToCapture);

    /// <summary>Verdadero mientras Mateo está dentro del haz de luz y al descubierto.</summary>
    public bool IsPlayerExposed => (Time.time - lastExposedTime) < 0.15f;

    // Registro de todas las linternas activas (para la viñeta de peligro y los indicadores de alerta)
    private static readonly List<VisionCone2D> activeCones = new List<VisionCone2D>();
    public static IReadOnlyList<VisionCone2D> ActiveCones => activeCones;

    /// <summary>Alcance del haz de luz (unidades).</summary>
    public float Range => viewDistance;

    /// <summary>Mayor progreso de detección entre todas las linternas del nivel (0 = calma, 1 = captura).</summary>
    public static float MaxDetectionProgress
    {
        get
        {
            float max = 0f;
            for (int i = 0; i < activeCones.Count; i++)
            {
                if (activeCones[i] != null) max = Mathf.Max(max, activeCones[i].DetectionProgress);
            }
            return max;
        }
    }

    private void OnEnable()
    {
        if (!activeCones.Contains(this)) activeCones.Add(this);
    }

    private void OnDisable()
    {
        activeCones.Remove(this);
    }

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
        fullConeVertices = vertices;
        occludedVertices = (Vector3[])vertices.Clone();

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
                colors[i] = new Color(baseColor.r, baseColor.g, baseColor.b, beamAlpha * 0.375f);
            }
            else
            {
                // Borde suave del haz de luz
                colors[i] = new Color(baseColor.r, baseColor.g, baseColor.b, beamAlpha);
            }
        }
        coneMesh.colors = colors;
    }

    /// <summary>
    /// Recorta el haz donde lo corta una cobertura (CoverZone2D): la luz no alumbra detrás de lo que puede ocultar a Mateo.
    /// </summary>
    private void UpdateOcclusion()
    {
        if (coneMesh == null || fullConeVertices == null || fullConeVertices.Length != coneMesh.vertexCount) return;

        Vector2 origin = transform.position;
        for (int i = 1; i < fullConeVertices.Length; i++)
        {
            Vector2 worldEnd = transform.TransformPoint(fullConeVertices[i]);
            Vector2 delta = worldEnd - origin;
            float length = delta.magnitude;
            if (length < 0.001f) { occludedVertices[i] = fullConeVertices[i]; continue; }
            float clear = LightOcclusion2D.ClearDistance(origin, delta / length, length);
            occludedVertices[i] = fullConeVertices[i] * (clear / length);
        }
        coneMesh.vertices = occludedVertices;
        coneMesh.RecalculateBounds();
    }

    private void Update()
    {
        // Barrido del reflector: va y viene alrededor de su dirección; se queda quieto mientras alumbra a Mateo
        if (sweepHalfAngle > 0f)
        {
            if (!IsPlayerExposed) sweepTime += Time.deltaTime;
            float angle = sweepHalfAngle * Mathf.Sin(sweepTime / Mathf.Max(0.5f, sweepPeriod) * Mathf.PI * 2f);
            transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        UpdateOcclusion();

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

        // 0b. Detección por cercanía (fuera del triángulo de luz)
        CheckCloseDetection();

        // 1. Progresión del temporizador de detección (con margen suave para evitar parpadeo)
        bool isExposed = IsPlayerExposed;
        if (isExposed)
        {
            bool isClose = (Time.time - lastCloseExposedTime) < 0.15f;
            detectionTimer += Time.deltaTime * (isClose ? closeDetectionSpeed : 1f);
            // Bajo una lámpara (LightZone2D, Nivel 3) Mateo queda a la vista: si lo ven, lo atrapan al instante
            if (LightZone2D.PlayerLit) detectionTimer = Mathf.Max(detectionTimer, detectionTimeToCapture);
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
                CoyoteAnimation coyoteAnim = GetComponentInParent<CoyoteAnimation>();
                if (coyoteAnim != null) coyoteAnim.PlayCatch();
                targetPlayerHealth.Capture(defeatReason);
                detectionTimer = 0f;
            }
        }
    }

    /// <summary>
    /// Detecta a Mateo si está pegado al coyote o justo enfrente de él, aunque no esté dentro del haz de luz.
    /// </summary>
    private void CheckCloseDetection()
    {
        if (closeDetectionRadius <= 0f && frontDetectionRadius <= 0f) return;
        if (!FindPlayer()) return;

        PlayerHealth health = trackedPlayer;
        if (health == null || health.isDead) return;
        if (IsPlayerConcealed()) return;

        Vector2 coyotePos = parentPatrolAI != null ? (Vector2)parentPatrolAI.transform.position : (Vector2)transform.position;
        Vector2 feet = PlayerFeet((Vector2)health.transform.position);
        if (LightOcclusion2D.IsBlocked(coyotePos, feet)) return; // Una cobertura se interpone
        Vector2 toPlayer = feet - coyotePos;
        float distance = toPlayer.magnitude;

        bool close = distance <= closeDetectionRadius;
        if (!close && distance <= frontDetectionRadius && distance > 0.001f)
        {
            Vector2 facing = parentPatrolAI != null ? parentPatrolAI.CurrentFacingDirection : (Vector2)transform.right;
            close = Vector2.Angle(facing, toPlayer) <= frontDetectionHalfAngle;
        }
        if (!close) return;

        lastExposedTime = Time.time;
        lastCloseExposedTime = Time.time;
        targetPlayerHealth = health;

        // El coyote voltea hacia Mateo (y con él la linterna)
        if (parentPatrolAI != null) parentPatrolAI.FaceTowards(feet);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        CheckDetection(collision);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckDetection(collision);
    }

    /// <summary>Busca a Mateo (una sola vez) y sus controles de caminar o nadar.</summary>
    private bool FindPlayer()
    {
        if (trackedPlayer != null) return true;
        trackedPlayer = FindAnyObjectByType<PlayerHealth>();
        if (trackedPlayer == null) return false;
        walker = trackedPlayer.GetComponent<PlayerController2D>();
        swimmer = trackedPlayer.GetComponent<SwimController2D>();
        return true;
    }

    /// <summary>
    /// Verdadero si la luz no puede ver a Mateo: escondido tras una cobertura (cactus, roca, tronco, juncos)
    /// o sumergido bajo el agua del río.
    /// </summary>
    private bool IsPlayerConcealed()
    {
        if (CoverZone2D.isHiding) return true;
        if (walker != null && walker.isHiding) return true;
        if (swimmer != null && (swimmer.isHiding || swimmer.isDiving)) return true;
        return false;
    }

    private Vector2 PlayerFeet(Vector2 fallback)
    {
        if (walker != null) return walker.GetFeetPosition();
        if (swimmer != null) return swimmer.GetFeetPosition();
        return fallback;
    }

    private void CheckDetection(Collider2D col)
    {
        if (!IsPlayerCollider(col, out PlayerHealth health))
            return;

        if (health == null || health.isDead) return;

        // Si Mateo está a cubierto (cactus, roca, nopalera, tronco o juncos) o sumergido, la luz lo ignora
        if (IsPlayerConcealed()) return;

        // Una cobertura entre la linterna y Mateo le hace sombra
        Vector2 targetFeet = PlayerFeet((Vector2)col.transform.position);
        if (LightOcclusion2D.IsBlocked(transform.position, targetFeet)) return;

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

    private bool IsPlayerCollider(Collider2D col, out PlayerHealth health)
    {
        health = col.GetComponentInParent<PlayerHealth>();
        if (health == null) health = col.GetComponentInChildren<PlayerHealth>();

        if (health != null)
        {
            if (trackedPlayer != health)
            {
                trackedPlayer = health;
                walker = health.GetComponent<PlayerController2D>();
                swimmer = health.GetComponent<SwimController2D>();
            }
            return true;
        }

        if (col.CompareTag("Player") || col.name.ToLower().Contains("mateo") || col.transform.root.name.ToLower().Contains("player"))
        {
            return true;
        }

        return false;
    }
}
