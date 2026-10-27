using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Viñeta de post-procesado que reacciona al peligro:
/// - En calma: viñeta cálida y suave (atmósfera de desierto).
/// - Cuando una linterna empieza a detectar a Mateo o un perro alerta a los coyotes,
///   los bordes de la pantalla se tiñen de rojo y se cierran progresivamente.
/// </summary>
[RequireComponent(typeof(Volume))]
public class DangerVignette : MonoBehaviour
{
    [SerializeField] private Color calmColor = new Color(0.16f, 0.07f, 0.02f, 1f);
    [SerializeField] private Color dangerColor = new Color(0.55f, 0.02f, 0.02f, 1f);
    [SerializeField] private float calmIntensity = 0.28f;
    [SerializeField] private float dangerIntensity = 0.48f;
    [SerializeField] private float blendSpeed = 4f;

    private Vignette vignette;
    private float currentDanger;

    private void Start()
    {
        Volume volume = GetComponent<Volume>();
        // volume.profile crea una copia en ejecución, así no se modifica el asset del proyecto
        if (volume != null && volume.profile != null)
        {
            volume.profile.TryGet(out vignette);
        }
    }

    private void Update()
    {
        if (vignette == null) return;

        float danger = VisionCone2D.MaxDetectionProgress;
        if (PatrolAI.isHighAlertFromDog) danger = Mathf.Max(danger, 0.5f);

        currentDanger = Mathf.MoveTowards(currentDanger, danger, Time.unscaledDeltaTime * blendSpeed);
        vignette.color.Override(Color.Lerp(calmColor, dangerColor, currentDanger));
        vignette.intensity.Override(Mathf.Lerp(calmIntensity, dangerIntensity, currentDanger));
    }
}
