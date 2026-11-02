using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Gestiona el cartel emergente narrativo de tutorial al inicio del Tramo 1.
/// Pausa el juego para explicar los controles (WASD), sigilo en coberturas,
/// hidratación y el objetivo de alcanzar la ribera del Río Bravo.
/// </summary>
public class TutorialPopup : MonoBehaviour
{
    [Header("Referencias de UI")]
    [Tooltip("Panel contenedor del tutorial que se ocultará al iniciar")]
    [SerializeField] private GameObject tutorialPanel;

    [Tooltip("Botón para cerrar el tutorial y comenzar a jugar")]
    [SerializeField] private Button closeButton;

    [Header("Comportamiento")]
    [Tooltip("Pausar el juego automáticamente al abrir el cartel tutorial")]
    [SerializeField] private bool pauseOnStart = true;

    private bool isClosed = false;

    /// <summary>Verdadero mientras el cartel del tutorial está en pantalla (bloquea la pausa).</summary>
    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        if (tutorialPanel == null)
        {
            tutorialPanel = gameObject;
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseTutorial);
        }
    }

    private void Start()
    {
        // Mostrar cartel y pausar juego al iniciar
        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(true);
        }

        IsOpen = tutorialPanel != null && tutorialPanel.activeInHierarchy;

        if (pauseOnStart)
        {
            Time.timeScale = 0f;
        }
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (isClosed) return;

        // Permitir cerrar con teclado (Espacio, Enter o E)
        bool keyPressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame ||
                Keyboard.current.enterKey.wasPressedThisFrame ||
                Keyboard.current.eKey.wasPressedThisFrame)
            {
                keyPressed = true;
            }
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (!keyPressed)
        {
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.E))
            {
                keyPressed = true;
            }
        }
#endif

        if (keyPressed)
        {
            CloseTutorial();
        }
    }

    /// <summary>
    /// Oculta el cartel y reanuda el tiempo de juego normal.
    /// </summary>
    public void CloseTutorial()
    {
        if (isClosed) return;
        isClosed = true;
        IsOpen = false;

        if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }

        Time.timeScale = 1f;
        Debug.Log("<color=green>[TutorialPopup] Tutorial cerrado. ¡Comienza la travesía en el desierto!</color>");
    }
}
