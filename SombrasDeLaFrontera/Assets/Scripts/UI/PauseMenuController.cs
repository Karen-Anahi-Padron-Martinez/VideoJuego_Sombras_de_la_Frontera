using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Menú de pausa del nivel:
/// - Se abre/cierra con ESC o P, o con el botón de pausa del HUD.
/// - Congela el juego (Time.timeScale = 0) y ofrece: Continuar, Reiniciar Nivel y Volver al Menú.
/// - No se puede abrir durante el tutorial inicial, la caída de Mateo, ni las pantallas de Game Over / Victoria.
/// </summary>
public class PauseMenuController : MonoBehaviour
{
    [Header("Panel de Pausa")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [Header("Botón de Pausa en el HUD")]
    [SerializeField] private Button hudPauseButton;

    [Header("Configuración de Escenas")]
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";

    public static bool IsPaused { get; private set; }

    private static PauseMenuController instance;
    private PlayerHealth playerHealth;

    private void Awake()
    {
        instance = this;
        IsPaused = false;
        AudioListener.pause = false;

        if (pausePanel != null) pausePanel.SetActive(false);

        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (restartButton != null) restartButton.onClick.AddListener(RestartLevel);
        if (menuButton != null) menuButton.onClick.AddListener(GoToMainMenu);
        if (hudPauseButton != null) hudPauseButton.onClick.AddListener(TogglePause);
    }

    private void Start()
    {
        playerHealth = FindAnyObjectByType<PlayerHealth>();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            IsPaused = false;
        }
    }

    private void Update()
    {
        if (PausePressedThisFrame())
        {
            TogglePause();
        }

        // El botón del HUD solo se muestra cuando realmente se puede pausar
        if (hudPauseButton != null)
        {
            bool showButton = !IsPaused && CanPause();
            if (hudPauseButton.gameObject.activeSelf != showButton) hudPauseButton.gameObject.SetActive(showButton);
        }
    }

    private bool PausePressedThisFrame()
    {
        bool pressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            pressed = Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!pressed)
        {
            pressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        }
#endif

        return pressed;
    }

    private bool CanPause()
    {
        if (EndGameModalController.IsShowing) return false;
        if (TutorialPopup.IsOpen) return false;
        if (playerHealth != null && playerHealth.isDead) return false;
        return true;
    }

    public void TogglePause()
    {
        if (IsPaused) Resume();
        else Pause();
    }

    public void Pause()
    {
        if (IsPaused || !CanPause()) return;

        IsPaused = true;
        Time.timeScale = 0f;
        AudioListener.pause = true; // Pausa los efectos; la música sigue sonando
        if (pausePanel != null) pausePanel.SetActive(true);

        // Seleccionar "Continuar" para poder navegar con teclado
        if (resumeButton != null && EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
        }

        Debug.Log("[PauseMenu] Juego en pausa.");
    }

    public void Resume()
    {
        if (!IsPaused) return;

        IsPaused = false;
        if (pausePanel != null) pausePanel.SetActive(false);
        Time.timeScale = 1f;
        AudioListener.pause = false;

        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        Debug.Log("[PauseMenu] Juego reanudado.");
    }

    public void RestartLevel()
    {
        IsPaused = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        PatrolAI.ResetGlobalAlertState();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void GoToMainMenu()
    {
        IsPaused = false;
        AudioListener.pause = false;
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    /// <summary>
    /// Cierra la pausa sin reanudar el tiempo (lo usan las pantallas de Game Over / Victoria).
    /// </summary>
    public static void ForceClose()
    {
        if (instance == null) return;
        IsPaused = false;
        if (instance.pausePanel != null) instance.pausePanel.SetActive(false);
    }
}
