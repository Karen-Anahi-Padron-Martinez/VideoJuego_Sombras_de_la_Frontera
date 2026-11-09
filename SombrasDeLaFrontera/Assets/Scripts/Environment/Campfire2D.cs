using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Fogata del refugio (Nivel 4): la luz titila como fuego y su crepitar se oye más fuerte mientras Mateo se acerca,
/// así guía hacia la meta. El sonido respeta el volumen de Efectos y se pausa con el menú de pausa.
/// </summary>
public class Campfire2D : MonoBehaviour
{
    [SerializeField] private Light2D fireLight;
    [SerializeField] private SpriteRenderer glow;
    [Tooltip("Cuánto varía la intensidad de la luz al titilar")]
    [SerializeField] private float flicker = 0.25f;
    [Tooltip("Alcance del sonido respecto al de los demás efectos (la fogata se oye desde lejos)")]
    [SerializeField] private float soundRange = 1.4f;

    private float baseIntensity;
    private float baseGlowAlpha;
    private float seed;
    private AudioSource crackle;

    private void Start()
    {
        if (fireLight != null) baseIntensity = fireLight.intensity;
        if (glow != null) baseGlowAlpha = glow.color.a;
        seed = Random.Range(0f, 100f);
    }

    private void Update()
    {
        // Titileo con ruido suave (dos frecuencias) para que no se vea mecánico
        float n = Mathf.PerlinNoise(seed, Time.time * 3.1f) * 0.7f + Mathf.PerlinNoise(seed + 7f, Time.time * 9.3f) * 0.3f;
        float k = 1f + (n - 0.5f) * 2f * flicker;
        if (fireLight != null) fireLight.intensity = baseIntensity * k;
        if (glow != null) { Color c = glow.color; c.a = baseGlowAlpha * k; glow.color = c; }

        UpdateSound();
    }

    private void UpdateSound()
    {
        AudioManager audio = AudioManager.Instance;
        if (audio == null || audio.CampfireLoopClip == null) return;

        if (crackle == null)
        {
            crackle = gameObject.AddComponent<AudioSource>();
            crackle.clip = audio.CampfireLoopClip;
            crackle.loop = true;
            crackle.playOnAwake = false;
            crackle.spatialBlend = 0f;
            crackle.Play();
        }

        audio.GetWorldMix(transform.position, soundRange, out float volume, out float pan);
        crackle.volume = volume * 0.5f;
        crackle.panStereo = pan;
    }

    public void Configure(Light2D light, SpriteRenderer glowRenderer)
    {
        fireLight = light;
        glow = glowRenderer;
    }
}
