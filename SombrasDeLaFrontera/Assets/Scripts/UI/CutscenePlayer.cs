using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Reproduce cualquier CutsceneData: fade entre pasos, texto con máquina de escribir
/// y viñetas fijas. Clic / Espacio / Enter / botón A avanza
/// (o completa el texto si aún se escribe). Mantener Esc salta toda la cinemática.
/// </summary>
public class CutscenePlayer : MonoBehaviour
{
    [SerializeField] private CutsceneData cinematica;

    [Header("Referencias UI")]
    [SerializeField] private CanvasGroup contenido;
    [SerializeField] private Image vineta;
    [SerializeField] private TextMeshProUGUI narracion;
    [SerializeField] private CanvasGroup avisoSaltar;
    [SerializeField] private RectTransform barraSaltar;

    [Header("Ritmo")]
    [SerializeField] private float caracteresPorSegundo = 35f;
    [SerializeField] private float duracionFade = 0.6f;
    [SerializeField] private float tiempoMantenerSaltar = 1f;

    [Header("Ambiente")]
    [SerializeField] private float volumenAmbiente = 0.6f;
    [SerializeField] private float duracionCrossfade = 1.5f;

    private float tiempoEsc;
    private bool terminando;
    private float tamanoTextoBase;
    private AudioSource[] ambientes;
    private int ambienteActual;
    private Coroutine crossfade;

    private void Start()
    {
        Time.timeScale = 1f;
        contenido.alpha = 0f;
        if (cinematica == null)
        {
            Debug.LogError("[CutscenePlayer] No hay CutsceneData asignada en 'cinematica'.", this);
            enabled = false;
            return;
        }
        tamanoTextoBase = narracion.fontSize;
        ambientes = new[] { CrearFuenteAmbiente(), CrearFuenteAmbiente() };
        StartCoroutine(Reproducir());
    }

    private void Update()
    {
        if (terminando) return;

        Keyboard kb = Keyboard.current;
        tiempoEsc = kb != null && kb.escapeKey.isPressed ? tiempoEsc + Time.unscaledDeltaTime : 0f;

        float progreso = Mathf.Clamp01(tiempoEsc / tiempoMantenerSaltar);
        barraSaltar.localScale = new Vector3(progreso, 1f, 1f);
        avisoSaltar.alpha = progreso > 0f ? 1f : 0.35f;

        if (progreso >= 1f) Terminar();
    }

    private IEnumerator Reproducir()
    {
        foreach (CutsceneData.Paso paso in cinematica.pasos)
        {
            bool esImagen = paso.tipo == CutsceneData.TipoPaso.Imagen;
            vineta.enabled = esImagen;
            vineta.sprite = paso.imagen;
            narracion.text = esImagen ? "" : paso.texto;
            narracion.fontSize = paso.tamanoTexto > 0f ? paso.tamanoTexto : tamanoTextoBase;
            bool escribir = paso.velocidadTexto > 0f;
            narracion.maxVisibleCharacters = escribir ? 0 : int.MaxValue;
            float fade = paso.duracionFade > 0f ? paso.duracionFade : duracionFade;

            if (paso.ambiente != null && paso.ambiente != ambientes[ambienteActual].clip)
            {
                if (crossfade != null) StopCoroutine(crossfade);
                crossfade = StartCoroutine(CambiarAmbiente(paso.ambiente));
            }

            yield return Fundir(1f, fade);

            // Máquina de escribir: el input durante la escritura completa el texto
            float visibles = 0f;
            while (escribir && visibles < narracion.text.Length && !AvancePresionado())
            {
                visibles += caracteresPorSegundo * paso.velocidadTexto * Time.unscaledDeltaTime;
                narracion.maxVisibleCharacters = (int)visibles;
                yield return null;
            }
            narracion.maxVisibleCharacters = int.MaxValue;
            yield return null; // no reutilizar el mismo clic para avanzar

            // Con duración: pausa fija y avanza solo. Sin duración: espera al jugador.
            if (paso.duracion > 0f)
                yield return new WaitForSecondsRealtime(paso.duracion);
            else
                while (!AvancePresionado()) yield return null;

            yield return Fundir(0f, fade);
        }

        Terminar();
    }

    private IEnumerator Fundir(float destino, float duracion)
    {
        while (!Mathf.Approximately(contenido.alpha, destino))
        {
            contenido.alpha = Mathf.MoveTowards(contenido.alpha, destino, Time.unscaledDeltaTime / duracion);
            yield return null;
        }
    }

    private AudioSource CrearFuenteAmbiente()
    {
        AudioSource fuente = gameObject.AddComponent<AudioSource>();
        fuente.loop = true;
        fuente.playOnAwake = false;
        if (AudioManager.Instance != null) fuente.outputAudioMixerGroup = AudioManager.Instance.SfxGroup;
        return fuente;
    }

    private IEnumerator CambiarAmbiente(AudioClip clip)
    {
        AudioSource sale = ambientes[ambienteActual];
        ambienteActual = 1 - ambienteActual;
        AudioSource entra = ambientes[ambienteActual];

        entra.clip = clip;
        entra.volume = 0f;
        entra.Play();

        float volumenSale = sale.volume;
        for (float t = 0f; t < duracionCrossfade; t += Time.unscaledDeltaTime)
        {
            float k = t / duracionCrossfade;
            entra.volume = k * volumenAmbiente;
            sale.volume = (1f - k) * volumenSale;
            yield return null;
        }

        entra.volume = volumenAmbiente;
        sale.Stop();
    }

    private void Terminar()
    {
        if (terminando) return;
        terminando = true;
        StopAllCoroutines();
        SceneManager.LoadScene(cinematica.siguienteEscena);
    }

    private static bool AvancePresionado()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;

        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            || (mouse != null && mouse.leftButton.wasPressedThisFrame)
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }
}
