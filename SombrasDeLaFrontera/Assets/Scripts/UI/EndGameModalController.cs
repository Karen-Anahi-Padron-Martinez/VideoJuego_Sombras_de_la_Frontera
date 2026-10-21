using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Mensaje y configuración visual de la pantalla de Game Over según la causa de derrota.
/// Puede ser modificado directamente desde el Inspector de Unity.
/// </summary>
[System.Serializable]
public class DefeatMessageConfig
{
    [Tooltip("Título principal en letras grandes")]
    public string title;

    [Tooltip("Color del título para distinguir el tipo de peligro")]
    public Color titleColor = new Color(0.92f, 0.22f, 0.22f, 1f);

    [TextArea(3, 5)]
    [Tooltip("Explicación narrativa de la derrota y consejo de supervivencia")]
    public string description;
}

/// <summary>
/// Controlador de pantallas modales de fin de partida:
/// - Game Over (Derrota dinámica): Muestra pantalla específica según si perdió por:
///   1. Quedarse sin agua (Deshidratación).
///   2. Picadura de serpiente de cascabel.
///   3. Picadura de alacrán.
///   4. Ataque o mordida de perro guardián.
///   5. Captura por linterna del vigilante coyote.
///   Incluye botones: 'Reintentar Nivel' y 'Volver al Menú'.
/// - Victoria (Nivel Completado): Activada al cruzar la ribera del Río Bravo (GoalTrigger).
/// </summary>
public class EndGameModalController : MonoBehaviour
{
    [Header("Panel Modal de Game Over (Derrota)")]
    [Tooltip("Panel contenedor de Game Over")]
    [SerializeField] private GameObject gameOverPanel;

    [Tooltip("Texto para el título del motivo de derrota")]
    [SerializeField] private Text gameOverTitleText;

    [Tooltip("Texto para la explicación y consejos del motivo de derrota")]
    [SerializeField] private Text gameOverDescriptionText;

    [Tooltip("Botón para recargar el nivel actual")]
    [SerializeField] private Button retryButton;

    [Tooltip("Botón para regresar al menú principal desde Game Over")]
    [SerializeField] private Button gameOverMenuButton;

    [Header("Pantallas Dinámicas de Derrota (Modificables en Inspector)")]
    [SerializeField] private DefeatMessageConfig defeatByFlashlight = new DefeatMessageConfig
    {
        title = "¡HAS SIDO DESCUBIERTO!",
        titleColor = new Color(0.95f, 0.22f, 0.22f, 1f),
        description = "La linterna del vigilante coyote iluminó tu silueta en campo abierto.\n\n🌵 <b>Consejo:</b> Ocúltate tras cactus, rocas o nopaleras para que los reflectores no te vean."
    };

    [SerializeField] private DefeatMessageConfig defeatByDehydration = new DefeatMessageConfig
    {
        title = "¡COLAPSO POR DESHIDRATACIÓN!",
        titleColor = new Color(0.95f, 0.55f, 0.15f, 1f), // Ámbar/Naranja ardiente
        description = "El calor extremo del desierto agotó tu última gota de agua y no pudiste continuar.\n\n💧 <b>Consejo:</b> Busca cantimploras y tunas para mantener tu barra de agua siempre llena."
    };

    [SerializeField] private DefeatMessageConfig defeatBySnake = new DefeatMessageConfig
    {
        title = "¡MORDEDURA DE SERPIENTE!",
        titleColor = new Color(0.60f, 0.85f, 0.25f, 1f), // Verde ponzoñoso
        description = "Una víbora de cascabel te mordió al pisar su territorio.\n\n🐍 <b>Consejo:</b> Escucha el sonido del cascabel y dale un amplio rodeo a su zona."
    };

    [SerializeField] private DefeatMessageConfig defeatByScorpion = new DefeatMessageConfig
    {
        title = "¡PICADURA DE ALACRÁN!",
        titleColor = new Color(0.85f, 0.35f, 0.85f, 1f), // Púrpura ponzoña
        description = "Un alacrán venenoso te clavó su aguijón entre las piedras.\n\n🦂 <b>Consejo:</b> Mira bien el suelo y no te detengas sobre madrigueras de fauna nociva."
    };

    [SerializeField] private DefeatMessageConfig defeatByDog = new DefeatMessageConfig
    {
        title = "¡ACORRALADO POR PERROS!",
        titleColor = new Color(0.92f, 0.32f, 0.18f, 1f), // Rojo ladrillo
        description = "Los perros guardianes te mordieron y ladraron alertando a todos los coyotes.\n\n🐕 <b>Consejo:</b> Los perros vigilan en semicírculo frontal; pasa por detrás o agáchate a cubierto."
    };

    [SerializeField] private DefeatMessageConfig defeatGeneric = new DefeatMessageConfig
    {
        title = "¡TE HAS QUEDADO SIN VIDAS!",
        titleColor = new Color(0.90f, 0.20f, 0.20f, 1f),
        description = "Mateo ha perdido sus 3 corazones ante los peligros del desierto.\n\n🛡️ <b>Consejo:</b> Protege tu salud y administra bien tus movimientos."
    };

    [Header("Panel Modal de Victoria (Nivel Completado)")]
    [Tooltip("Panel modal de Victoria")]
    [SerializeField] private GameObject victoryPanel;

    [Tooltip("Botón para avanzar al siguiente nivel o menú")]
    [SerializeField] private Button nextLevelButton;

    [Tooltip("Botón para regresar al menú principal desde Victoria")]
    [SerializeField] private Button victoryMenuButton;

    [Header("Configuración de Escenas")]
    [SerializeField] private string mainMenuSceneName = "01_MainMenu";
    [SerializeField] private string nextLevelSceneName = "03_Level2_RioBravo";

    private void Awake()
    {
        // Asegurar que los paneles inicien ocultos
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);

        // Configurar escuchadores de botones
        if (retryButton != null)
        {
            retryButton.onClick.AddListener(RetryLevel);
        }

        if (gameOverMenuButton != null)
        {
            gameOverMenuButton.onClick.AddListener(GoToMainMenu);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(LoadNextLevel);
        }

        if (victoryMenuButton != null)
        {
            victoryMenuButton.onClick.AddListener(GoToMainMenu);
        }
    }

    private void OnEnable()
    {
        PlayerHealth.OnPlayerDiedWithReason += ShowGameOver;
        WaterSystem.OnDehydrationDied += HandleDehydrationDeath;
        GoalTrigger.OnLevelCompleted += ShowVictory;
    }

    private void OnDisable()
    {
        PlayerHealth.OnPlayerDiedWithReason -= ShowGameOver;
        WaterSystem.OnDehydrationDied -= HandleDehydrationDeath;
        GoalTrigger.OnLevelCompleted -= ShowVictory;
    }

    private void HandleDehydrationDeath()
    {
        ShowGameOver(DefeatReason.Dehydration);
    }

    /// <summary>
    /// Despliega el modal de Game Over adaptando título, mensaje y color según el motivo de la derrota.
    /// </summary>
    public void ShowGameOver(DefeatReason reason)
    {
        DefeatMessageConfig config = GetMessageConfig(reason);

        if (gameOverTitleText != null)
        {
            gameOverTitleText.text = config.title;
            gameOverTitleText.color = config.titleColor;
        }

        if (gameOverDescriptionText != null)
        {
            gameOverDescriptionText.text = config.description;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        Time.timeScale = 0f;
        Debug.Log($"<color=red>[EndGameModal] Pantalla de Game Over desplegada. Motivo: {reason}.</color>");
    }

    private DefeatMessageConfig GetMessageConfig(DefeatReason reason)
    {
        switch (reason)
        {
            case DefeatReason.FlashlightCaptured:
                return defeatByFlashlight;
            case DefeatReason.Dehydration:
                return defeatByDehydration;
            case DefeatReason.SnakeBite:
                return defeatBySnake;
            case DefeatReason.ScorpionSting:
                return defeatByScorpion;
            case DefeatReason.DogAttack:
                return defeatByDog;
            default:
                return defeatGeneric;
        }
    }

    /// <summary>
    /// Despliega el modal de Victoria y congela el tiempo.
    /// </summary>
    public void ShowVictory()
    {
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        Time.timeScale = 0f;
        Debug.Log("<color=green>[EndGameModal] Pantalla de Victoria desplegada. ¡Nivel 1 Superado!</color>");
    }

    /// <summary>
    /// Reintenta el nivel actual recargando la escena y restaurando timeScale.
    /// </summary>
    public void RetryLevel()
    {
        Time.timeScale = 1f;
        PatrolAI.ResetGlobalAlertState();
        string currentScene = SceneManager.GetActiveScene().name;
        SceneManager.LoadScene(currentScene);
    }

    /// <summary>
    /// Carga el siguiente nivel si existe, o regresa al menú.
    /// </summary>
    public void LoadNextLevel()
    {
        Time.timeScale = 1f;

        // Comprobar si la escena del siguiente nivel está en BuildSettings
        if (Application.CanStreamedLevelBeLoaded(nextLevelSceneName))
        {
            SceneManager.LoadScene(nextLevelSceneName);
        }
        else
        {
            Debug.Log($"[EndGameModal] '{nextLevelSceneName}' aún no está creada. Regresando al Menú Principal con nivel desbloqueado.");
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }

    /// <summary>
    /// Regresa a la escena del Menú Principal restaurando timeScale.
    /// </summary>
    public void GoToMainMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }
}
