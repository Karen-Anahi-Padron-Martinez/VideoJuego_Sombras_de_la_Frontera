using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controla la pantalla de presentación (00_Splash) con logotipo y créditos de UTNG,
/// animación suave de Fade-In / Fade-Out y transición automática hacia el Menú Principal.
/// </summary>
public class SplashController : MonoBehaviour
{
    [Header("Configuración de Transición")]
    [Tooltip("Nombre de la escena a cargar al terminar la presentación")]
    [SerializeField] private string nextSceneName = "01_MainMenu";

    [Tooltip("Tiempo en segundos que tarda en aparecer")]
    [SerializeField] private float fadeInDuration = 1.2f;

    [Tooltip("Tiempo en segundos que permanece visible en pantalla")]
    [SerializeField] private float displayDuration = 2.2f;

    [Tooltip("Tiempo en segundos que tarda en desvanecerse")]
    [SerializeField] private float fadeOutDuration = 1.0f;

    [Header("Referencias UI")]
    [Tooltip("CanvasGroup que contiene los elementos visuales del Splash")]
    [SerializeField] private CanvasGroup splashCanvasGroup;

    [Tooltip("Texto o indicador opcional para saltar la presentación")]
    [SerializeField] private Text skipPromptText;

    private bool isTransitioning = false;

    private void Awake()
    {
        if (splashCanvasGroup == null)
            splashCanvasGroup = GetComponentInChildren<CanvasGroup>();
    }

    private void Start()
    {
        if (splashCanvasGroup != null)
        {
            splashCanvasGroup.alpha = 0f;
        }

        StartCoroutine(SplashSequenceRoutine());
    }

    private void Update()
    {
        // Permitir saltar la presentación al presionar cualquier tecla o clic
        if (!isTransitioning && (Input.anyKeyDown || Input.GetMouseButtonDown(0)))
        {
            StopAllCoroutines();
            StartCoroutine(FastFadeAndLoadRoutine());
        }
    }

    private IEnumerator SplashSequenceRoutine()
    {
        // 1. Fade In
        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.deltaTime;
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = Mathf.Clamp01(timer / fadeInDuration);
            }
            yield return null;
        }

        if (splashCanvasGroup != null) splashCanvasGroup.alpha = 1f;

        // 2. Tiempo de exposición
        yield return new WaitForSeconds(displayDuration);

        // 3. Fade Out
        timer = 0f;
        while (timer < fadeOutDuration)
        {
            timer += Time.deltaTime;
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = Mathf.Clamp01(1f - (timer / fadeOutDuration));
            }
            yield return null;
        }

        if (splashCanvasGroup != null) splashCanvasGroup.alpha = 0f;

        // 4. Cargar Menú Principal
        LoadNextScene();
    }

    private IEnumerator FastFadeAndLoadRoutine()
    {
        isTransitioning = true;
        float currentAlpha = splashCanvasGroup != null ? splashCanvasGroup.alpha : 1f;
        float timer = 0f;
        float fastDuration = 0.35f;

        while (timer < fastDuration)
        {
            timer += Time.deltaTime;
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = Mathf.Lerp(currentAlpha, 0f, timer / fastDuration);
            }
            yield return null;
        }

        LoadNextScene();
    }

    private void LoadNextScene()
    {
        isTransitioning = true;
        if (Application.CanStreamedLevelBeLoaded(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning($"[SplashController] La escena '{nextSceneName}' no está en Build Settings. Intentando por índice.");
            SceneManager.LoadScene(1);
        }
    }
}
