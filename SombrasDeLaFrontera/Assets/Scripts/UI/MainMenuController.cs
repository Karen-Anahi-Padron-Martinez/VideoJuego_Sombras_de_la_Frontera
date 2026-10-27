using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controlador principal del Menú de Inicio (01_MainMenu).
/// Administra:
/// 1. Botón JUGAR: Inicia la travesía directamente desde el Nivel 1.
/// 2. Botón NIVELES: Pantalla dedicada con 4 cuadros interactivos y miniaturas para elegir el tramo.
/// 3. Botón OPCIONES: Modal con sliders de AudioMixer (General, Música y SFX) y persistencia en PlayerPrefs.
/// 4. Botón CRÉDITOS: Modal institucional con datos de UTNG, docente, equipo y licencias.
/// 5. Botón SALIR: Cierra el juego de forma segura.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Configuración de Escenas")]
    [SerializeField] private string level1SceneName = "02_Level1_Desert";
    [SerializeField] private string level2SceneName = "03_Level2_RioBravo";
    [SerializeField] private string level3SceneName = "04_Level3_BorderWall";
    [SerializeField] private string level4SceneName = "05_Level4_SafeHaven";

    [Header("Contenedores de Pantalla")]
    [SerializeField] private GameObject mainMenuButtons;
    [SerializeField] private GameObject levelsScreen;
    [SerializeField] private GameObject optionsPanel;
    [SerializeField] private GameObject creditsPanel;

    [Header("Botones del Menú Principal")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button levelsButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button creditsButton;
    [SerializeField] private Button quitButton;

    [Header("Botones de Regreso")]
    [SerializeField] private Button backFromLevelsButton;
    [SerializeField] private Button closeOptionsButton;
    [SerializeField] private Button closeCreditsButton;

    [Header("Cuadros de los 4 Niveles")]
    [SerializeField] private Button cardLevel1Btn;
    [SerializeField] private Button cardLevel2Btn;
    [SerializeField] private Button cardLevel3Btn;
    [SerializeField] private Button cardLevel4Btn;

    [SerializeField] private Text level2StatusText;
    [SerializeField] private Text level3StatusText;
    [SerializeField] private Text level4StatusText;

    [Header("Ajustes de Audio (AudioMixer)")]
    [Tooltip("AudioMixer del proyecto con parámetros expuestos: MasterVolume, MusicVolume, SFXVolume")]
    [SerializeField] private AudioMixer audioMixer;

    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Text masterVolumeText;

    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Text musicVolumeText;

    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Text sfxVolumeText;

    // Claves de PlayerPrefs
    private const string PREF_MASTER_VOL = "Volume_Master";
    private const string PREF_MUSIC_VOL = "Volume_Music";
    private const string PREF_SFX_VOL = "Volume_SFX";
    private const string PREF_MAX_LEVEL = "NivelMaximo";
    private const string PREF_GAME_STARTED = "PartidaIniciada";

    private void Awake()
    {
        // Botones del menú principal
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (levelsButton != null) levelsButton.onClick.AddListener(OpenLevelsScreen);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        // Botones de regreso
        if (backFromLevelsButton != null) backFromLevelsButton.onClick.AddListener(ShowMainMenuButtons);
        if (closeOptionsButton != null) closeOptionsButton.onClick.AddListener(ShowMainMenuButtons);
        if (closeCreditsButton != null) closeCreditsButton.onClick.AddListener(ShowMainMenuButtons);

        // Clic en los 4 cuadros de niveles
        if (cardLevel1Btn != null) cardLevel1Btn.onClick.AddListener(() => LaunchLevel(1, level1SceneName));
        if (cardLevel2Btn != null) cardLevel2Btn.onClick.AddListener(() => LaunchLevel(2, level2SceneName));
        if (cardLevel3Btn != null) cardLevel3Btn.onClick.AddListener(() => LaunchLevel(3, level3SceneName));
        if (cardLevel4Btn != null) cardLevel4Btn.onClick.AddListener(() => LaunchLevel(4, level4SceneName));

        // Sliders de audio
        if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
    }

    private void Start()
    {
        // Estado inicial: Menú principal visible, pantallas secundarias ocultas
        ShowMainMenuButtons();

        // Actualizar desbloqueo de niveles
        UpdateLevelsProgress();

        // Cargar preferencias de audio
        LoadAudioPreferences();
    }

    // -------------------------------------------------------------
    // NAVEGACIÓN ENTRE PANTALLAS
    // -------------------------------------------------------------

    public void ShowMainMenuButtons()
    {
        if (mainMenuButtons != null) mainMenuButtons.SetActive(true);
        if (levelsScreen != null) levelsScreen.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void OpenLevelsScreen()
    {
        if (mainMenuButtons != null) mainMenuButtons.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        UpdateLevelsProgress();
        if (levelsScreen != null) levelsScreen.SetActive(true);
    }

    public void OpenOptions()
    {
        if (mainMenuButtons != null) mainMenuButtons.SetActive(false);
        if (levelsScreen != null) levelsScreen.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);

        if (optionsPanel != null) optionsPanel.SetActive(true);
    }

    public void OpenCredits()
    {
        if (mainMenuButtons != null) mainMenuButtons.SetActive(false);
        if (levelsScreen != null) levelsScreen.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);

        if (creditsPanel != null) creditsPanel.SetActive(true);
    }

    // -------------------------------------------------------------
    // GESTIÓN DE LOS 4 NIVELES
    // -------------------------------------------------------------

    private void UpdateLevelsProgress()
    {
        int maxUnlocked = PlayerPrefs.GetInt(PREF_MAX_LEVEL, 1);

        // Nivel 1: Siempre disponible
        if (cardLevel1Btn != null) cardLevel1Btn.interactable = true;

        // Niveles 2, 3 y 4 según progreso
        ConfigureLevelCard(cardLevel2Btn, level2StatusText, maxUnlocked >= 2);
        ConfigureLevelCard(cardLevel3Btn, level3StatusText, maxUnlocked >= 3);
        ConfigureLevelCard(cardLevel4Btn, level4StatusText, maxUnlocked >= 4);
    }

    private void ConfigureLevelCard(Button cardBtn, Text statusText, bool isUnlocked)
    {
        if (cardBtn != null)
        {
            cardBtn.interactable = isUnlocked;
        }

        if (statusText != null)
        {
            if (isUnlocked)
            {
                statusText.text = "DESBLOQUEADO";
                statusText.color = new Color(0.35f, 0.90f, 0.45f, 1f); // Verde activo
            }
            else
            {
                statusText.text = "🔒 BLOQUEADO";
                statusText.color = new Color(0.7f, 0.7f, 0.7f, 0.65f); // Gris bloqueado
            }
        }
    }

    public void OnPlayClicked()
    {
        PlayerPrefs.SetInt(PREF_GAME_STARTED, 1);
        PlayerPrefs.Save();

        LaunchLevel(1, level1SceneName);
    }

    public void LaunchLevel(int levelNum, string sceneName)
    {
        PatrolAI.ResetGlobalAlertState();
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(sceneName))
        {
            SceneManager.LoadScene(sceneName);
        }
        else
        {
            if (levelNum == 1)
            {
                SceneManager.LoadScene(level1SceneName);
            }
            else
            {
                Debug.Log($"[MainMenu] El Nivel {levelNum} ('{sceneName}') estará disponible en el Sprint 3.");
            }
        }
    }

    public void OnQuitClicked()
    {
        Debug.Log("[MainMenu] Saliendo del juego...");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // -------------------------------------------------------------
    // AJUSTES DE AUDIO
    // -------------------------------------------------------------

    private void LoadAudioPreferences()
    {
        float master = PlayerPrefs.GetFloat(PREF_MASTER_VOL, 0.85f);
        float music = PlayerPrefs.GetFloat(PREF_MUSIC_VOL, 0.80f);
        float sfx = PlayerPrefs.GetFloat(PREF_SFX_VOL, 0.90f);

        if (masterVolumeSlider != null) masterVolumeSlider.value = master;
        if (musicVolumeSlider != null) musicVolumeSlider.value = music;
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = sfx;

        ApplyVolumeToMixer("MasterVolume", master);
        ApplyVolumeToMixer("MusicVolume", music);
        ApplyVolumeToMixer("SFXVolume", sfx);

        UpdateVolumeLabel(masterVolumeText, master);
        UpdateVolumeLabel(musicVolumeText, music);
        UpdateVolumeLabel(sfxVolumeText, sfx);
    }

    public void SetMasterVolume(float value)
    {
        ApplyVolumeToMixer("MasterVolume", value);
        PlayerPrefs.SetFloat(PREF_MASTER_VOL, value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(masterVolumeText, value);
        AudioListener.volume = value;
        AudioManager.Instance?.RefreshVolumes();
    }

    public void SetMusicVolume(float value)
    {
        ApplyVolumeToMixer("MusicVolume", value);
        PlayerPrefs.SetFloat(PREF_MUSIC_VOL, value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(musicVolumeText, value);
        AudioManager.Instance?.RefreshVolumes();
    }

    public void SetSFXVolume(float value)
    {
        ApplyVolumeToMixer("SFXVolume", value);
        PlayerPrefs.SetFloat(PREF_SFX_VOL, value);
        PlayerPrefs.Save();
        UpdateVolumeLabel(sfxVolumeText, value);
        AudioManager.Instance?.RefreshVolumes();
    }

    private void ApplyVolumeToMixer(string paramName, float linearValue)
    {
        if (audioMixer == null) return;

        float clamped = Mathf.Clamp(linearValue, 0.0001f, 1f);
        float dB = Mathf.Log10(clamped) * 20f;
        audioMixer.SetFloat(paramName, dB);
    }

    private void UpdateVolumeLabel(Text label, float value)
    {
        if (label != null)
        {
            label.text = $"{Mathf.RoundToInt(value * 100f)}%";
        }
    }
}
