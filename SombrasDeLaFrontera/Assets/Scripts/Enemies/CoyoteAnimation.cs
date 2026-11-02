using UnityEngine;

/// <summary>
/// Decide qué animación muestra el vigilante coyote en cada momento:
/// - Walk:  avanzando entre waypoints.
/// - Idle:  esperando en un waypoint o en guardia por el ladrido de un perro.
/// - Alert: su linterna está iluminando a Mateo (señala hacia él mientras el cono pasa de amarillo a rojo).
/// - Catch: atrapó a Mateo (se reproduce aunque el juego quede congelado en el Game Over).
/// También controla el icono "?" / "!" que aparece sobre su cabeza y sus sonidos:
/// pasos al caminar, murmullo al sospechar ("¿Eh? ¿Quién anda ahí?"), grito al descubrir a Mateo ("¡Ahí está!")
/// y grito al atraparlo ("¡Te tengo!").
/// </summary>
[RequireComponent(typeof(PatrolAI))]
public class CoyoteAnimation : MonoBehaviour
{
    public enum VoiceSet { Coyote, Police }

    [Tooltip("Voces y pasos: coyote (Nivel 1) o policía fronterizo (Nivel 3)")]
    [SerializeField] private VoiceSet voiceSet = VoiceSet.Coyote;

    [Tooltip("Progreso mínimo de detección para que el coyote reaccione y señale a Mateo")]
    [SerializeField] private float alertThreshold = 0.05f;

    [Tooltip("Segundos entre pasos mientras camina")]
    [SerializeField] private float stepInterval = 0.42f;

    private PatrolAI patrolAI;
    private VisionCone2D visionCone;
    private CreatureAnimator creatureAnimator;
    private AlertIndicator alertIndicator;
    private bool hasCaught = false;
    private float nextStepTime;
    private AlertIndicator.Kind lastKind = AlertIndicator.Kind.None;
    private static int nextVoiceId;
    private readonly int voiceId = ++nextVoiceId;

    private void Awake()
    {
        patrolAI = GetComponent<PatrolAI>();
        visionCone = GetComponentInChildren<VisionCone2D>();
        creatureAnimator = GetComponentInChildren<CreatureAnimator>();
        alertIndicator = GetComponentInChildren<AlertIndicator>(true);
    }

    private void Update()
    {
        if (hasCaught || creatureAnimator == null) return;

        float progress = visionCone != null ? visionCone.DetectionProgress : 0f;
        bool seesPlayer = visionCone != null && visionCone.IsPlayerExposed;

        if (seesPlayer || progress > alertThreshold)
        {
            creatureAnimator.Play("Alert");
        }
        else if (patrolAI != null && patrolAI.IsMoving)
        {
            creatureAnimator.Play("Walk");
            if (Time.time >= nextStepTime)
            {
                nextStepTime = Time.time + stepInterval;
                if (voiceSet == VoiceSet.Police) AudioManager.Instance?.PlayPoliceStepAt(transform.position);
                else AudioManager.Instance?.PlayCoyoteStepAt(transform.position);
            }
        }
        else
        {
            creatureAnimator.Play("Idle");
        }

        AlertIndicator.Kind kind = AlertIndicator.Kind.None;
        if (seesPlayer && (PatrolAI.isHighAlertFromDog || progress > 0.6f)) kind = AlertIndicator.Kind.Exclamation;
        else if (seesPlayer || progress > alertThreshold) kind = AlertIndicator.Kind.Question;

        if (alertIndicator != null) alertIndicator.Show(kind);

        // Voz del coyote solo al cambiar de estado (sospecha -> descubre)
        if (kind != lastKind)
        {
            if (kind == AlertIndicator.Kind.Question && lastKind == AlertIndicator.Kind.None)
            {
                if (voiceSet == VoiceSet.Police) AudioManager.Instance?.PlayPoliceSuspectAt(transform.position, voiceId);
                else AudioManager.Instance?.PlayCoyoteSuspectAt(transform.position, voiceId);
                AlarmSystem2D.Raise(transform.position);
            }
            else if (kind == AlertIndicator.Kind.Exclamation)
            {
                if (voiceSet == VoiceSet.Police) AudioManager.Instance?.PlayPoliceSpotAt(transform.position, voiceId);
                else AudioManager.Instance?.PlayCoyoteSpotAt(transform.position, voiceId);
                AlarmSystem2D.Raise(transform.position);
                AudioManager.Instance?.PlayAlert();
            }
            lastKind = kind;
        }
    }

    /// <summary>
    /// Animación de captura: el coyote se abalanza sobre Mateo.
    /// </summary>
    public void PlayCatch()
    {
        hasCaught = true;
        if (voiceSet == VoiceSet.Police) AudioManager.Instance?.PlayPoliceCatchAt(transform.position, voiceId);
        else AudioManager.Instance?.PlayCoyoteCatchAt(transform.position);
        if (creatureAnimator != null)
        {
            creatureAnimator.UseUnscaledTime();
            creatureAnimator.Play("Catch");
        }

        if (alertIndicator != null)
        {
            alertIndicator.UseUnscaledTime();
            alertIndicator.Show(AlertIndicator.Kind.Exclamation);
        }
    }
}
