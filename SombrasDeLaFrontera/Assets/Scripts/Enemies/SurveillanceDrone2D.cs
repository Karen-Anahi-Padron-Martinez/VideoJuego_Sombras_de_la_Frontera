using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dron de vigilancia del Nivel 4. Sobrevuela el desierto siguiendo una ruta en bucle y proyecta un círculo de luz en el suelo.
/// - El objeto raíz es el punto del suelo que alumbra; el cuerpo del dron vuela por encima (vista 3/4) y su sombra queda abajo.
/// - Mientras la luz alumbra a Mateo al descubierto, cuenta como zona iluminada (LightZone2D): si un agente lo ve, lo atrapa al instante.
/// - Si Mateo permanece en la luz al descubierto, el dron lo FIJA: la luz se pone roja y lo sigue. Los agentes cercanos
///   dejan su ruta y van directo al punto que marca el dron (se actualiza mientras lo sigue) y los perros K-9 corren hacia él.
/// - Esconderse tras una cobertura rompe el seguimiento: el dron regresa a su ruta y los agentes a las suyas.
/// </summary>
public class SurveillanceDrone2D : MonoBehaviour
{
    [Header("Ruta (bucle)")]
    [SerializeField] private List<Vector2> route = new List<Vector2>();
    [Tooltip("Velocidad de vuelo en su ruta (Mateo camina a 1 unidad por segundo)")]
    [SerializeField] private float patrolSpeed = 0.55f;
    [Tooltip("Velocidad al seguir a Mateo después de fijarlo")]
    [SerializeField] private float trackSpeed = 0.85f;

    [Header("Luz en el suelo")]
    [Tooltip("Radios de la elipse iluminada (vista 3/4)")]
    [SerializeField] private Vector2 spotRadii = new Vector2(0.7f, 0.45f);
    [SerializeField] private Color searchColor = new Color(0.85f, 0.95f, 1f, 1f);
    [SerializeField] private Color lockColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Header("Detección")]
    [Tooltip("Segundos al descubierto dentro de la luz para que el dron fije a Mateo")]
    [SerializeField] private float lockTime = 0.7f;
    [Tooltip("Segundos que lo sigue después de perderlo de vista antes de volver a su ruta")]
    [SerializeField] private float loseTrackTime = 1.2f;
    [Tooltip("Radio en el que el dron llama a los perros K-9")]
    [SerializeField] private float callRadius = 4.5f;
    [Tooltip("Cada cuántos segundos vuelve a llamar a los perros mientras sigue a Mateo")]
    [SerializeField] private float callInterval = 1.5f;
    [Tooltip("Radio en el que los agentes acuden al punto que marca el dron")]
    [SerializeField] private float agentCallRadius = 6f;
    [Tooltip("Cada cuántos segundos actualiza a los agentes el punto donde está Mateo")]
    [SerializeField] private float markInterval = 0.3f;

    [Header("Partes (las arma el editor del Nivel 4)")]
    [SerializeField] private Transform body;
    [SerializeField] private SpriteRenderer spotRenderer;
    [SerializeField] private UnityEngine.Rendering.Universal.Light2D spotLight;
    [SerializeField] private SpriteRenderer ledRenderer;

    private enum State { Patrol, Tracking, Returning }
    private State state = State.Patrol;
    private int routeIndex;
    private float exposure;
    private float lastSeenTime;
    private float nextCallTime;
    private Vector3 bodyBaseLocal;
    private float bobPhase;
    private PlayerController2D player;
    private AudioSource humSource;
    private float nextMarkTime;
    private readonly HashSet<PatrolAI> summonedAgents = new HashSet<PatrolAI>();

    public bool IsTracking => state == State.Tracking;

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController2D>();
        if (body != null) bodyBaseLocal = body.localPosition;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);
        routeIndex = NearestRoutePoint();
        SetColor(searchColor, 0f);
    }

    private void Update()
    {
        if (player == null) { player = FindAnyObjectByType<PlayerController2D>(); if (player == null) return; }

        Vector2 feet = player.GetFeetPosition();
        bool hidden = CoverZone2D.isHiding || player.isHiding;
        bool exposed = !hidden && InSpot(feet);

        switch (state)
        {
            case State.Patrol:
            case State.Returning:
                exposure = exposed ? exposure + Time.deltaTime : Mathf.Max(0f, exposure - Time.deltaTime * 2f);
                if (exposure >= lockTime) { Lock(feet); break; }
                FollowRoute();
                break;

            case State.Tracking:
                if (!hidden) lastSeenTime = Time.time; // Lo sigue mientras esté al descubierto
                if (Time.time - lastSeenTime > loseTrackTime)
                {
                    // Mateo se escondió: los agentes que acudieron regresan a sus rutas
                    state = State.Returning;
                    exposure = 0f;
                    routeIndex = NearestRoutePoint();
                    ReleaseAgents();
                    break;
                }
                MoveTowards(feet, trackSpeed);
                if (!hidden && Time.time >= nextMarkTime) MarkForAgents(feet);
                if (exposed && Time.time >= nextCallTime) CallDogs(feet);
                break;
        }

        float lockBlend = state == State.Tracking ? 1f : Mathf.Clamp01(exposure / lockTime) * 0.6f;
        SetColor(Color.Lerp(searchColor, lockColor, lockBlend), lockBlend);
        AnimateBody();
        UpdateHum();
    }

    /// <summary>
    /// Zumbido de las hélices: más fuerte mientras más cerca está la cámara y más agudo al seguir a Mateo.
    /// Respeta el volumen de Efectos y se pausa con el menú de pausa.
    /// </summary>
    private void UpdateHum()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null || audio.DroneHumClip == null) return;

        if (humSource == null)
        {
            humSource = gameObject.AddComponent<AudioSource>();
            humSource.clip = audio.DroneHumClip;
            humSource.loop = true;
            humSource.playOnAwake = false;
            humSource.spatialBlend = 0f;
            humSource.time = Random.Range(0f, humSource.clip.length);
            humSource.Play();
        }

        audio.GetWorldMix(transform.position, 0.9f, out float volume, out float pan);
        humSource.volume = volume * 0.4f;
        humSource.panStereo = pan;
        humSource.pitch = Mathf.MoveTowards(humSource.pitch, state == State.Tracking ? 1.15f : 1f, Time.deltaTime * 0.6f);
    }

    private void Lock(Vector2 feet)
    {
        state = State.Tracking;
        lastSeenTime = Time.time;
        AudioManager.Instance?.PlayDroneLock();
        MarkForAgents(feet);
        CallDogs(feet);
        Debug.Log($"<color=red>[SurveillanceDrone2D] {name} fijó a Mateo y llamó a la patrulla.</color>");
    }

    /// <summary>Avisa a los perros cercanos: corren hacia Mateo olfateando.</summary>
    private void CallDogs(Vector2 feet)
    {
        nextCallTime = Time.time + callInterval;
        foreach (GuardDogAI dog in FindObjectsByType<GuardDogAI>())
        {
            if (dog != null && Vector2.Distance(dog.transform.position, feet) <= callRadius) dog.AlertToPosition(feet);
        }
    }

    /// <summary>Los agentes cercanos van directo al punto donde el dron ve a Mateo (se actualiza mientras lo sigue).</summary>
    private void MarkForAgents(Vector2 feet)
    {
        nextMarkTime = Time.time + markInterval;
        foreach (PatrolAI agent in FindObjectsByType<PatrolAI>())
        {
            if (agent == null || agent.GetComponent<GuardDogAI>() != null) continue;
            bool alreadyComing = summonedAgents.Contains(agent);
            if (!alreadyComing && Vector2.Distance(agent.transform.position, feet) > agentCallRadius) continue;
            agent.InvestigatePosition(feet);
            summonedAgents.Add(agent);
        }
    }

    private void ReleaseAgents()
    {
        foreach (PatrolAI agent in summonedAgents)
        {
            if (agent != null) agent.EndInvestigation();
        }
        summonedAgents.Clear();
    }

    private void OnDisable() => ReleaseAgents();

    private void FollowRoute()
    {
        if (route.Count == 0) return;
        Vector2 target = route[routeIndex];
        if (MoveTowards(target, patrolSpeed))
        {
            routeIndex = (routeIndex + 1) % route.Count;
            if (state == State.Returning) state = State.Patrol;
        }
    }

    /// <summary>Mueve el punto alumbrado hacia el destino. Devuelve verdadero al llegar.</summary>
    private bool MoveTowards(Vector2 target, float speed)
    {
        Vector2 pos = transform.position;
        Vector2 next = Vector2.MoveTowards(pos, target, speed * Time.deltaTime);
        transform.position = new Vector3(next.x, next.y, transform.position.z);
        return (next - target).sqrMagnitude < 0.0004f;
    }

    private int NearestRoutePoint()
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int i = 0; i < route.Count; i++)
        {
            float d = (route[i] - (Vector2)transform.position).sqrMagnitude;
            if (d < bestDist) { bestDist = d; best = i; }
        }
        return best;
    }

    private bool InSpot(Vector2 point)
    {
        Vector2 d = point - (Vector2)transform.position;
        float nx = d.x / spotRadii.x, ny = d.y / spotRadii.y;
        return nx * nx + ny * ny <= 1f;
    }

    private void SetColor(Color c, float lockBlend)
    {
        if (spotRenderer != null) spotRenderer.color = new Color(c.r, c.g, c.b, 0.32f + 0.12f * lockBlend);
        if (spotLight != null) spotLight.color = c;
        if (ledRenderer != null)
        {
            // El LED parpadea; más rápido cuando sigue a Mateo
            float rate = state == State.Tracking ? 8f : 2f;
            float on = Mathf.Repeat(Time.time * rate, 1f) < 0.5f ? 1f : 0.15f;
            ledRenderer.color = new Color(1f, 0.2f, 0.15f, on);
        }
    }

    private void AnimateBody()
    {
        if (body == null) return;
        body.localPosition = bodyBaseLocal + new Vector3(0f, Mathf.Sin(Time.time * 2.2f + bobPhase) * 0.04f, 0f);
    }

    /// <summary>Configuración desde el editor del Nivel 4.</summary>
    public void Configure(List<Vector2> newRoute, Transform bodyTransform, SpriteRenderer spot, UnityEngine.Rendering.Universal.Light2D light, SpriteRenderer led)
    {
        route = newRoute;
        body = bodyTransform;
        spotRenderer = spot;
        spotLight = light;
        ledRenderer = led;
    }

    public Vector2 SpotRadii => spotRadii;
    public IReadOnlyList<Vector2> Route => route;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        for (int i = 0; i < route.Count; i++) Gizmos.DrawLine(route[i], route[(i + 1) % route.Count]);
        Gizmos.color = new Color(1f, 0.3f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, callRadius);
    }
}
