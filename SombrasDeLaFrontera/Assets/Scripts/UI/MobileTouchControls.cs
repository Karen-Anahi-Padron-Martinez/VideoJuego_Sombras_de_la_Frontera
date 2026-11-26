using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controles táctiles de la versión Android (APK). En PC y en la versión Web no existen: no se crean ni se ven.
/// - Se crean solos al iniciar el juego (no hay que agregarlos a ninguna escena) y viven entre escenas.
/// - Joystick: aparece donde el jugador apoya el pulgar en la mitad izquierda de la pantalla y mueve a Mateo
///   caminando (PlayerController2D lee Movimiento) y nadando (se envía a SwimController2D.SetVirtualInput).
/// - Botón BUCEAR: solo en el Nivel 2 (escenas con DiveController2D); mantenerlo presionado sumerge a Mateo.
/// - Solo se muestran durante el juego: se ocultan en menús, cinemáticas, pausa, tutorial y fin de partida.
/// - CutscenePlayer usa ToqueNuevo() para avanzar la historia tocando cualquier parte de la pantalla.
/// Para probarlos en el editor con el ratón: Sombras de la Frontera → Controles táctiles → Simular en el editor.
/// </summary>
[DefaultExecutionOrder(-50)]
public class MobileTouchControls : MonoBehaviour
{
    public const string PREF_SIMULAR = "SombrasFrontera_SimularTactil";

    /// <summary>Verdadero solo en el APK de Android (o en el editor con la simulación activada).</summary>
    public static bool Activos => Application.platform == RuntimePlatform.Android || SimularEnEditor;

#if UNITY_EDITOR
    public static bool SimularEnEditor => UnityEditor.EditorPrefs.GetBool(PREF_SIMULAR, false);
#else
    public static bool SimularEnEditor => false;
#endif

    /// <summary>Dirección del joystick (largo de 0 a 1). Cero cuando no se toca o los controles están ocultos.</summary>
    public static Vector2 Movimiento { get; private set; }

    [Header("Tamaños (en píxeles de una pantalla de 1920 x 1080)")]
    [SerializeField] private float radioJoystick = 150f;
    [SerializeField] private float radioPerilla = 65f;
    [SerializeField] private float radioBoton = 110f;
    [Tooltip("Desplazamiento mínimo (0 a 1) para que el joystick mueva a Mateo")]
    [SerializeField] private float zonaMuerta = 0.18f;

    private static readonly Vector2 PosicionJoystick = new Vector2(260f, 260f);   // Desde la esquina inferior izquierda
    private static readonly Vector2 PosicionBoton = new Vector2(-260f, 260f);     // Desde la esquina inferior derecha
    private const int SIN_DEDO = int.MinValue;
    private const int DEDO_RATON = -1;

    private Canvas canvas;
    private RectTransform baseJoystick, perilla, boton;
    private Image imagenBoton;
    private int dedoJoystick = SIN_DEDO, dedoBoton = SIN_DEDO;
    private Vector2 centroJoystick;     // En coordenadas de pantalla

    private PlayerController2D caminante;
    private SwimController2D nadador;
    private DiveController2D buzo;
    private bool hayJugador;

    // ------------------------------------------------------------------------------------------------ Arranque

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Crear()
    {
        if (!Activos || FindAnyObjectByType<MobileTouchControls>() != null) return;
        var go = new GameObject("ControlesTactiles");
        DontDestroyOnLoad(go);
        go.AddComponent<MobileTouchControls>();
    }

    private void Awake()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            // El juego se diseñó en horizontal: se permite girar solo entre las dos posiciones horizontales
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.AutoRotation;
        }

        ConstruirInterfaz();
        SceneManager.sceneLoaded += AlCargarEscena;
        BuscarJugador();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= AlCargarEscena;
        Movimiento = Vector2.zero;
    }

    private void AlCargarEscena(Scene escena, LoadSceneMode modo)
    {
        SoltarTodo();
        BuscarJugador();
    }

    private void BuscarJugador()
    {
        caminante = FindAnyObjectByType<PlayerController2D>();
        nadador = FindAnyObjectByType<SwimController2D>();
        buzo = FindAnyObjectByType<DiveController2D>();
    }

    // ------------------------------------------------------------------------------------------------ Cada cuadro

    private void Update()
    {
        hayJugador = (caminante != null && caminante.isActiveAndEnabled) || (nadador != null && nadador.isActiveAndEnabled);
        bool visible = hayJugador && Time.timeScale > 0f;
        canvas.enabled = visible;
        boton.gameObject.SetActive(visible && buzo != null && buzo.isActiveAndEnabled);

        if (!visible)
        {
            SoltarTodo();
            return;
        }

        LeerToques();
        AplicarAlJugador();
    }

    private void LeerToques()
    {
        bool joystickSigue = false, botonSigue = false;

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch t = Input.GetTouch(i);
            bool termina = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            ProcesarDedo(t.fingerId, t.position, t.phase == TouchPhase.Began, termina, ref joystickSigue, ref botonSigue);
        }

        // En el editor (simulación) el ratón hace de dedo
        if (SimularEnEditor && Input.touchCount == 0)
        {
            bool empieza = Input.GetMouseButtonDown(0);
            bool presionado = Input.GetMouseButton(0);
            if (empieza || presionado || Input.GetMouseButtonUp(0))
                ProcesarDedo(DEDO_RATON, Input.mousePosition, empieza, !presionado, ref joystickSigue, ref botonSigue);
        }

        // Un dedo que dejó de reportarse (por ejemplo, al perder el foco) suelta su control
        if (!joystickSigue && dedoJoystick != SIN_DEDO) SoltarJoystick();
        if (!botonSigue && dedoBoton != SIN_DEDO) dedoBoton = SIN_DEDO;

        imagenBoton.color = dedoBoton != SIN_DEDO ? new Color(0.35f, 0.75f, 1f, 0.75f) : new Color(1f, 1f, 1f, 0.35f);
    }

    private void ProcesarDedo(int dedo, Vector2 posicion, bool empieza, bool termina, ref bool joystickSigue, ref bool botonSigue)
    {
        if (empieza && dedo != dedoJoystick && dedo != dedoBoton)
        {
            if (boton.gameObject.activeInHierarchy && dedoBoton == SIN_DEDO && TocaBoton(posicion)) dedoBoton = dedo;
            else if (dedoJoystick == SIN_DEDO && EnZonaJoystick(posicion)) TomarJoystick(dedo, posicion);
        }

        if (dedo == dedoJoystick)
        {
            if (termina) SoltarJoystick();
            else { MoverJoystick(posicion); joystickSigue = true; }
        }
        else if (dedo == dedoBoton)
        {
            if (termina) dedoBoton = SIN_DEDO;
            else botonSigue = true;
        }
    }

    private void AplicarAlJugador()
    {
        if (nadador != null && nadador.isActiveAndEnabled) nadador.SetVirtualInput(Movimiento);
        if (buzo != null) buzo.SetVirtualDive(dedoBoton != SIN_DEDO);
    }

    // ------------------------------------------------------------------------------------------------ Joystick

    private float Escala => canvas.scaleFactor;

    // Mitad izquierda de la pantalla y por debajo del HUD (corazones y barras arriba a la izquierda)
    private static bool EnZonaJoystick(Vector2 p) => p.x < Screen.width * 0.5f && p.y < Screen.height * 0.75f;

    private bool TocaBoton(Vector2 p)
    {
        Vector2 centro = RectTransformUtility.WorldToScreenPoint(null, boton.position);
        return Vector2.Distance(p, centro) <= radioBoton * 1.3f * Escala;
    }

    private void TomarJoystick(int dedo, Vector2 posicion)
    {
        dedoJoystick = dedo;
        // El joystick aparece bajo el pulgar, sin salirse de la pantalla
        float margen = radioJoystick * Escala;
        centroJoystick = new Vector2(Mathf.Max(posicion.x, margen), Mathf.Max(posicion.y, margen));
        baseJoystick.position = centroJoystick;
        MoverJoystick(posicion);
    }

    private void MoverJoystick(Vector2 posicion)
    {
        float radio = radioJoystick * Escala;
        Vector2 desplazamiento = Vector2.ClampMagnitude(posicion - centroJoystick, radio);
        perilla.anchoredPosition = desplazamiento / Escala;

        Vector2 valor = desplazamiento / radio;
        Movimiento = valor.magnitude < zonaMuerta ? Vector2.zero : valor;
        baseJoystick.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.45f);
    }

    private void SoltarJoystick()
    {
        dedoJoystick = SIN_DEDO;
        Movimiento = Vector2.zero;
        perilla.anchoredPosition = Vector2.zero;
        baseJoystick.anchoredPosition = PosicionJoystick;
        baseJoystick.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
    }

    private void SoltarTodo()
    {
        if (baseJoystick != null) SoltarJoystick();
        dedoBoton = SIN_DEDO;
        if (nadador != null) nadador.SetVirtualInput(Vector2.zero);
        if (buzo != null) buzo.SetVirtualDive(false);
    }

    // ------------------------------------------------------------------------------------------------ Interfaz

    private void ConstruirInterfaz()
    {
        canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 40;   // Encima del juego y del HUD; el menú de pausa no importa porque en pausa se ocultan
        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Sprite aro = CrearCirculo(256, true);
        Sprite disco = CrearCirculo(256, false);

        baseJoystick = CrearImagen("Joystick", transform, aro, radioJoystick * 2f, new Color(1f, 1f, 1f, 0.25f));
        baseJoystick.anchorMin = baseJoystick.anchorMax = Vector2.zero;
        baseJoystick.anchoredPosition = PosicionJoystick;
        perilla = CrearImagen("Perilla", baseJoystick, disco, radioPerilla * 2f, new Color(1f, 1f, 1f, 0.6f));

        boton = CrearImagen("BotonBucear", transform, disco, radioBoton * 2f, new Color(1f, 1f, 1f, 0.35f));
        boton.anchorMin = boton.anchorMax = new Vector2(1f, 0f);
        boton.anchoredPosition = PosicionBoton;
        imagenBoton = boton.GetComponent<Image>();

        var textoGo = new GameObject("Texto", typeof(RectTransform));
        textoGo.transform.SetParent(boton, false);
        RectTransform rt = (RectTransform)textoGo.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        Text texto = textoGo.AddComponent<Text>();
        texto.text = "BUCEAR";
        texto.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        texto.fontSize = 40;
        texto.fontStyle = FontStyle.Bold;
        texto.alignment = TextAnchor.MiddleCenter;
        texto.color = new Color(0.1f, 0.15f, 0.25f, 0.9f);
        texto.raycastTarget = false;
    }

    private static RectTransform CrearImagen(string nombre, Transform padre, Sprite sprite, float diametro, Color color)
    {
        var go = new GameObject(nombre, typeof(RectTransform));
        go.transform.SetParent(padre, false);
        RectTransform rt = (RectTransform)go.transform;
        rt.sizeDelta = new Vector2(diametro, diametro);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;   // Los toques se leen directamente; no bloquean los botones del HUD
        return rt;
    }

    /// <summary>Círculo blanco suavizado (aro con relleno tenue para la base del joystick, disco lleno para la perilla y el botón).</summary>
    private static Sprite CrearCirculo(int tamano, bool aro)
    {
        var tex = new Texture2D(tamano, tamano, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float c = (tamano - 1) * 0.5f, r = c - 2f;
        var pixeles = new Color32[tamano * tamano];
        for (int y = 0; y < tamano; y++)
            for (int x = 0; x < tamano; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float borde = Mathf.Clamp01(r - d);                                        // Antialiasing del contorno
                float alfa = aro ? Mathf.Max(Mathf.Clamp01((d - (r - 10f)) / 2f) * borde, 0.35f * borde) : borde;
                pixeles[y * tamano + x] = new Color32(255, 255, 255, (byte)(alfa * 255));
            }
        tex.SetPixels32(pixeles);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, tamano, tamano), new Vector2(0.5f, 0.5f), 100f);
    }

    // ------------------------------------------------------------------------------------------------ Toques para las cinemáticas

    /// <summary>
    /// Verdadero el cuadro en que un dedo toca la pantalla (en cualquier parte). Solo en el APK de Android
    /// (o en la simulación del editor, donde ya avanza con el clic del ratón).
    /// </summary>
    public static bool ToqueNuevo()
    {
        if (!Activos) return false;
        for (int i = 0; i < Input.touchCount; i++)
            if (Input.GetTouch(i).phase == TouchPhase.Began) return true;
        return false;
    }
}
