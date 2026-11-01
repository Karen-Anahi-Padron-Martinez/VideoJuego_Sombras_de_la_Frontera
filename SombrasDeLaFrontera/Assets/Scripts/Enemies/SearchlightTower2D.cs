using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Reflector de una torre de vigilancia (Nivel 3):
/// - Un haz de luz barre el terreno de lado a lado con un patrón fijo y predecible (más lento en los extremos).
/// - Sale de lo alto de la torre. Las coberturas le hacen sombra: el haz se recorta y no alumbra detrás de ellas.
/// - Si alumbra a Mateo al descubierto, se queda sobre él, se pone rojo, da la alarma y lo captura tras un instante.
///   Si Mateo se agacha tras una cobertura o sale del haz, el reflector vuelve a su barrido.
/// En el editor (fuera de Play) solo dibuja el haz en su posición inicial para poder acomodar las torres.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SearchlightTower2D : MonoBehaviour
{
    [Header("Barrido")]
    [Tooltip("Dirección central del barrido en grados (0 = derecha, 90 = arriba, 180 = izquierda)")]
    [SerializeField] private float centerAngle = 0f;
    [Tooltip("Cuántos grados se aleja del centro hacia cada lado")]
    [SerializeField] private float sweepHalfRange = 50f;
    [Tooltip("Segundos que tarda una ida y vuelta completa")]
    [SerializeField] private float sweepPeriod = 7f;
    [Tooltip("Desfase del barrido (0 a 1) para que dos torres no se muevan igual")]
    [Range(0f, 1f)] [SerializeField] private float phaseOffset = 0f;

    [Header("Haz de luz")]
    [SerializeField] private float beamLength = 3.6f;
    [SerializeField] private float beamWidthAngle = 20f;
    [SerializeField] private int rays = 18;
    [SerializeField] private Color beamColor = new Color(1f, 0.97f, 0.8f, 1f);
    [SerializeField] private Color alertColor = new Color(1f, 0.22f, 0.12f, 1f);
    [SerializeField] private float beamAlpha = 0.3f;
    [SerializeField] private string sortingLayerName = "Foreground";
    [SerializeField] private int sortingOrder = 4;

    [Header("Captura")]
    [Tooltip("Segundos bajo el haz, al descubierto, antes de la captura")]
    [SerializeField] private float timeToCapture = 1.2f;
    [Tooltip("Velocidad (grados/s) con la que el haz sigue a Mateo o regresa a su barrido")]
    [SerializeField] private float trackSpeed = 80f;
    [SerializeField] private float alarmRadius = 5f;

    private static readonly List<SearchlightTower2D> activeLights = new List<SearchlightTower2D>();

    /// <summary>Mayor progreso de captura entre todos los reflectores (0 = calma, 1 = captura).</summary>
    public static float MaxDetectionProgress
    {
        get
        {
            float max = 0f;
            foreach (SearchlightTower2D s in activeLights) if (s != null) max = Mathf.Max(max, s.DetectionProgress);
            return max;
        }
    }

    public float DetectionProgress => Mathf.Clamp01(detectionTimer / timeToCapture);

    private Mesh mesh;
    private Material material;
    private Light2D beamLight;
    private float[] rayLengths;

    private float currentAngle;
    private float detectionTimer;
    private bool trackingPlayer;
    private float lastAlarmTime = -10f;
    private PlayerController2D player;
    private PlayerHealth playerHealth;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => activeLights.Clear();

    private void OnEnable() { if (!activeLights.Contains(this)) activeLights.Add(this); }
    private void OnDisable() => activeLights.Remove(this);

    private void OnDestroy()
    {
        if (mesh != null) DestroyImmediate(mesh);
        if (material != null) DestroyImmediate(material);
    }

    private void Awake()
    {
        // Malla y material solo en memoria (no se guardan en la escena)
        mesh = new Mesh { name = "SearchlightMesh", hideFlags = HideFlags.DontSave };
        GetComponent<MeshFilter>().sharedMesh = mesh;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        material = new Material(shader) { hideFlags = HideFlags.DontSave };
        MeshRenderer mr = GetComponent<MeshRenderer>();
        mr.sharedMaterial = material;
        mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder = sortingOrder;

        beamLight = GetComponentInChildren<Light2D>();
        rayLengths = new float[rays + 1];
        currentAngle = PatternAngle();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            if (mesh == null) Awake();
            currentAngle = PatternAngle();
            RebuildBeam();
            return;
        }

        FindPlayer();

        bool exposed = IsPlayerInBeam(out float angleToPlayer);
        if (exposed)
        {
            // Se queda sobre Mateo y lo sigue
            trackingPlayer = true;
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, angleToPlayer, trackSpeed * Time.deltaTime);
            detectionTimer += Time.deltaTime;

            if (Time.time - lastAlarmTime > 2f)
            {
                lastAlarmTime = Time.time;
                AlarmSystem2D.Raise(player.GetFeetPosition(), alarmRadius);
                AudioManager.Instance?.PlayAlert();
            }

            if (detectionTimer >= timeToCapture && playerHealth != null && !playerHealth.isDead)
            {
                Debug.Log($"[SearchlightTower2D] ¡CAPTURA! El reflector de {transform.root.name} alumbró a Mateo al descubierto.");
                playerHealth.Capture(DefeatReason.SearchlightSpotted);
                detectionTimer = 0f;
            }
        }
        else
        {
            detectionTimer = Mathf.Max(0f, detectionTimer - Time.deltaTime * 1.5f);

            // Regresa con suavidad al barrido (sin saltos)
            float target = PatternAngle();
            if (trackingPlayer)
            {
                currentAngle = Mathf.MoveTowardsAngle(currentAngle, target, trackSpeed * Time.deltaTime);
                if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, target)) < 1f) trackingPlayer = false;
            }
            else
            {
                currentAngle = target;
            }
        }

        RebuildBeam();
    }

    /// <summary>Ángulo del barrido en este momento: va y viene como un péndulo, más despacio en los extremos.</summary>
    private float PatternAngle()
    {
        float t = (Application.isPlaying ? Time.time : 0f) / Mathf.Max(0.5f, sweepPeriod) + phaseOffset;
        return centerAngle + sweepHalfRange * Mathf.Sin(t * Mathf.PI * 2f);
    }

    private void FindPlayer()
    {
        if (player != null) return;
        player = FindAnyObjectByType<PlayerController2D>();
        if (player != null) playerHealth = player.GetComponent<PlayerHealth>();
    }

    private bool IsPlayerInBeam(out float angleToPlayer)
    {
        angleToPlayer = 0f;
        if (player == null || playerHealth == null || playerHealth.isDead) return false;
        if (CoverZone2D.isHiding || player.isHiding) return false;

        Vector2 origin = transform.position;
        Vector2 toPlayer = player.GetFeetPosition() - origin;
        float distance = toPlayer.magnitude;
        if (distance > beamLength || distance < 0.01f) return false;

        angleToPlayer = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        if (Mathf.Abs(Mathf.DeltaAngle(currentAngle, angleToPlayer)) > beamWidthAngle * 0.5f) return false;

        // Sombra de los obstáculos entre la torre y Mateo
        return ClearDistance(toPlayer / distance, distance) >= distance - 0.02f;
    }

    /// <summary>Distancia libre en una dirección hasta una cobertura (o el largo indicado).</summary>
    private float ClearDistance(Vector2 dir, float maxDistance) =>
        LightOcclusion2D.ClearDistance(transform.position, dir, maxDistance);

    private void RebuildBeam()
    {
        // Malla en abanico con dos anillos (a 40% y al final de cada rayo) para un haz suave:
        // tenue junto a la torre, más intenso al centro, bordes laterales difuminados y desvanecido al final.
        float half = beamWidthAngle * 0.5f;
        var vertices = new Vector3[1 + (rays + 1) * 2];
        var colors = new Color[vertices.Length];
        var triangles = new int[rays * 9];

        Color c = Color.Lerp(beamColor, alertColor, DetectionProgress);
        float alpha = beamAlpha * Mathf.Lerp(1f, 1.5f, DetectionProgress);
        // El mesh está en espacio local; se compensa la escala del padre para medir en unidades de mundo
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x;
        float sy = Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y;

        vertices[0] = Vector3.zero;
        colors[0] = new Color(c.r, c.g, c.b, alpha * 0.2f);
        for (int i = 0; i <= rays; i++)
        {
            float a = (currentAngle - half + beamWidthAngle * i / rays) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            rayLengths[i] = ClearDistance(dir, beamLength);
            float edge = Mathf.Clamp01((1f - Mathf.Abs(2f * i / rays - 1f)) * 3f); // 0 en los bordes, 1 al centro

            float mid = Mathf.Min(rayLengths[i], beamLength * 0.4f);
            vertices[1 + i * 2] = new Vector3(dir.x * mid * sx, dir.y * mid * sy, 0f);
            vertices[2 + i * 2] = new Vector3(dir.x * rayLengths[i] * sx, dir.y * rayLengths[i] * sy, 0f);
            colors[1 + i * 2] = new Color(c.r, c.g, c.b, alpha * edge);
            colors[2 + i * 2] = new Color(c.r, c.g, c.b, alpha * 0.25f * edge);
        }
        for (int i = 0; i < rays; i++)
        {
            int m0 = 1 + i * 2, e0 = 2 + i * 2, m1 = 3 + i * 2, e1 = 4 + i * 2;
            int t = i * 9;
            triangles[t] = 0; triangles[t + 1] = m1; triangles[t + 2] = m0;
            triangles[t + 3] = m0; triangles[t + 4] = m1; triangles[t + 5] = e1;
            triangles[t + 6] = m0; triangles[t + 7] = e1; triangles[t + 8] = e0;
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
        material.color = Color.white;

        if (beamLight != null)
        {
            beamLight.transform.rotation = Quaternion.Euler(0f, 0f, currentAngle - 90f);
            beamLight.color = c;
        }
    }
}
