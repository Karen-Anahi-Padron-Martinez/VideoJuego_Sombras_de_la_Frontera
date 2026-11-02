using UnityEngine;

/// <summary>
/// Indicador "¡ILUMINADO!" del HUD (Nivel 3): aparece mientras Mateo está bajo la luz de una lámpara y al descubierto.
/// Ocupa el mismo lugar que el indicador "OCULTO" (nunca se muestran a la vez).
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class LightExposureIndicator : MonoBehaviour
{
    [SerializeField] private float fadeSpeed = 6f;
    [SerializeField] private float pulseSpeed = 5f;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.alpha = 0f;
    }

    private void Update()
    {
        float target = LightZone2D.PlayerLit ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * pulseSpeed) : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * fadeSpeed);
    }
}
