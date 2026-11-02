using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Medidor de aire del Nivel 2 (reutiliza la barra de agua del HUD, que en el río no se usa).
/// Muestra los segundos de aire de DiveController2D, el aire extra de los carrizos ("AIRE +3s")
/// y parpadea en rojo cuando a Mateo le queda poco aire bajo el agua.
/// </summary>
public class AirBarUI : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private Image fillImage;
    [SerializeField] private Text label;

    [SerializeField] private Color airColor = new Color(0.7f, 0.93f, 1f, 1f);
    [SerializeField] private Color lowAirColor = new Color(1f, 0.25f, 0.2f, 1f);

    [Tooltip("Fracción de aire a partir de la cual la barra avisa en rojo")]
    [Range(0f, 1f)]
    [SerializeField] private float lowAirThreshold = 0.3f;

    private DiveController2D diver;

    private void Update()
    {
        if (diver == null)
        {
            diver = FindAnyObjectByType<DiveController2D>();
            if (diver == null) return;
        }

        float ratio = diver.MaxAir > 0f ? Mathf.Clamp01(diver.CurrentAir / diver.MaxAir) : 0f;
        if (slider != null) slider.value = ratio;

        if (fillImage != null)
        {
            bool low = diver.IsDiving && ratio <= lowAirThreshold && diver.BonusAir <= 0f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
            fillImage.color = low ? Color.Lerp(lowAirColor, Color.white, pulse * 0.35f) : airColor;
        }

        if (label != null)
        {
            string text = diver.BonusAir > 0.05f ? $"AIRE  +{Mathf.CeilToInt(diver.BonusAir)}s" : "AIRE";
            if (label.text != text) label.text = text;
        }
    }
}
