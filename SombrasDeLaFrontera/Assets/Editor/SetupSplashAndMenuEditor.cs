using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;

/// <summary>
/// Generador automatizado de la Pantalla de Presentación (00_Splash) y del Menú Principal (01_MainMenu).
/// Configura Canvas responsivos (1920x1080), imagen de fondo personalizada,
/// controladores, sliders de audio, modales de Selección de Niveles (4 cuadros con miniaturas), Opciones y Créditos,
/// y actualiza Build Settings.
/// </summary>
public static class SetupSplashAndMenuEditor
{
    private const string SPLASH_SCENE_PATH = "Assets/Scenes/00_Splash.unity";
    private const string MENU_SCENE_PATH = "Assets/Scenes/01_MainMenu.unity";
    private const string LEVEL1_SCENE_PATH = "Assets/Scenes/02_Level1_Desert.unity";
    private const string BG_IMAGE_PATH = "Assets/Sprites/UI/Menu_Background.png";
    private const string THUMB_LEVEL1_PATH = "Assets/Sprites/UI/Thumbnail_Level1.jpg";

    [MenuItem("Sombras de la Frontera/Configurar Escenas de Splash y Menú Principal")]
    public static void GenerateSplashAndMainMenu()
    {
        // 1. Guardar escena actual si está abierta
        EditorSceneManager.SaveOpenScenes();

        // 2. Construir escena 00_Splash
        BuildSplashScene();

        // 3. Construir escena 01_MainMenu con la pantalla de 4 niveles
        BuildMainMenuScene();

        // 4. Actualizar Build Settings con el orden de escenas correcto
        UpdateBuildSettings();

        // 5. Cargar Menú Principal al finalizar para que el usuario pueda verlo y probarlo
        EditorSceneManager.OpenScene(MENU_SCENE_PATH);

        Debug.Log("<color=green><b>[Menú & Splash]</b> ¡Escenas 00_Splash y 01_MainMenu configuradas con éxito y añadidas a Build Settings!</color>");
    }

    // =========================================================================
    // CONSTRUCCIÓN DE ESCENA 00_SPLASH
    // =========================================================================

    private static void BuildSplashScene()
    {
        UnityEngine.SceneManagement.Scene splashScene = EditorSceneManager.OpenScene(SPLASH_SCENE_PATH, OpenSceneMode.Single);

        foreach (GameObject rootObj in splashScene.GetRootGameObjects())
        {
            Object.DestroyImmediate(rootObj);
        }

        // Cámara Principal
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.05f, 0.04f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camObj.AddComponent<AudioListener>();

        CreateEventSystem();

        // Canvas
        GameObject canvasObj = new GameObject("SplashCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Fondo oscuro
        GameObject bgObj = CreateUIElement("Background", canvasObj.transform);
        StretchFull(bgObj.GetComponent<RectTransform>());
        Image bgImg = bgObj.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.04f, 0.03f, 1f);

        // CanvasGroup para animación
        GameObject contentGroup = CreateUIElement("SplashContent", canvasObj.transform);
        StretchFull(contentGroup.GetComponent<RectTransform>());
        CanvasGroup cg = contentGroup.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        // Tarjeta central
        GameObject card = CreateUIElement("Card", contentGroup.transform);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(1200, 700);
        cardRect.anchoredPosition = Vector2.zero;

        // Encabezado UTNG
        CreateText("UTNG_Header", card.transform,
            "UNIVERSIDAD TECNOLÓGICA DEL NORTE DE GUANAJUATO",
            30, FontStyle.Bold, new Color(0.18f, 0.75f, 0.70f, 1f),
            new Vector2(0f, 240f), new Vector2(1100f, 50f));

        CreateText("UTNG_Subheader", card.transform,
            "CARRERA DE TECNOLOGÍAS DE LA INFORMACIÓN • ÁREA DESARROLLO DE SOFTWARE MULTIPLATAFORMA",
            19, FontStyle.Normal, new Color(0.75f, 0.82f, 0.90f, 0.9f),
            new Vector2(0f, 195f), new Vector2(1100f, 40f));

        GameObject divider = CreateUIElement("GoldDivider", card.transform);
        RectTransform divRect = divider.GetComponent<RectTransform>();
        divRect.sizeDelta = new Vector2(500, 3);
        divRect.anchoredPosition = new Vector2(0, 155);
        Image divImg = divider.AddComponent<Image>();
        divImg.color = new Color(0.95f, 0.75f, 0.25f, 0.85f);

        CreateText("Presents_Label", card.transform,
            "—   P   R   E   S   E   N   T   A   —",
            22, FontStyle.Bold, new Color(0.85f, 0.85f, 0.85f, 0.8f),
            new Vector2(0f, 110f), new Vector2(800f, 40f));

        CreateText("Game_Title", card.transform,
            "SOMBRAS DE LA FRONTERA",
            58, FontStyle.Bold, new Color(1.0f, 0.72f, 0.25f, 1f),
            new Vector2(0f, 30f), new Vector2(1150f, 85f));

        CreateText("Game_Subtitle", card.transform,
            "TRAVESÍA DE SUPERVIVENCIA Y SIGILO EN EL DESIERTO",
            24, FontStyle.Italic, new Color(1.0f, 0.90f, 0.75f, 0.9f),
            new Vector2(0f, -35f), new Vector2(1000f, 40f));

        CreateText("Academic_Info", card.transform,
            "Evaluación de Unidad 2: Desarrollo de Videojuegos 2D\nDocente: <b>Prof. Gabriel Barrón Rodríguez</b>",
            22, FontStyle.Normal, new Color(0.85f, 0.85f, 0.85f, 0.95f),
            new Vector2(0f, -130f), new Vector2(1000f, 70f));

        CreateText("Team_Info", card.transform,
            "Equipo de Desarrollo: <b>Juan Loredo</b>  &  <b>Karen Anahí Padrón Martínez</b>",
            22, FontStyle.Normal, new Color(0.95f, 0.80f, 0.40f, 1f),
            new Vector2(0f, -200f), new Vector2(1000f, 50f));

        CreateText("Skip_Hint", contentGroup.transform,
            "Presiona cualquier tecla o haz clic para continuar...",
            18, FontStyle.Italic, new Color(0.6f, 0.6f, 0.6f, 0.6f),
            new Vector2(0f, -480f), new Vector2(600f, 30f));

        canvasObj.AddComponent<SplashController>();

        EditorSceneManager.SaveScene(splashScene);
        Debug.Log("[SetupSplash] Escena 00_Splash creada exitosamente.");
    }

    // =========================================================================
    // CONSTRUCCIÓN DE ESCENA 01_MAINMENU
    // =========================================================================

    private static void BuildMainMenuScene()
    {
        UnityEngine.SceneManagement.Scene menuScene = EditorSceneManager.OpenScene(MENU_SCENE_PATH, OpenSceneMode.Single);

        foreach (GameObject rootObj in menuScene.GetRootGameObjects())
        {
            Object.DestroyImmediate(rootObj);
        }

        // Cámara Principal
        GameObject camObj = new GameObject("Main Camera");
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.08f, 0.06f, 0.05f, 1f);
        cam.orthographic = true;
        cam.orthographicSize = 5f;
        camObj.AddComponent<AudioListener>();

        CreateEventSystem();

        // Canvas Principal
        GameObject canvasObj = new GameObject("MainMenuCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // Cargar sprites
        Sprite userBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BG_IMAGE_PATH);
        Sprite level1ThumbSprite = AssetDatabase.LoadAssetAtPath<Sprite>(THUMB_LEVEL1_PATH);

        // -------------------------------------------------------------
        // FONDO DEL MENÚ PRINCIPAL
        // -------------------------------------------------------------
        GameObject bgObj = CreateUIElement("MenuBackground", canvasObj.transform);
        StretchFull(bgObj.GetComponent<RectTransform>());
        Image bgImg = bgObj.AddComponent<Image>();

        if (userBgSprite != null)
        {
            bgImg.sprite = userBgSprite;
            bgImg.color = Color.white;
        }
        else
        {
            bgImg.color = new Color(0.92f, 0.55f, 0.20f, 1f);
        }

        // Viñeta oscura suave para contraste
        GameObject vignetteObj = CreateUIElement("VignetteOverlay", canvasObj.transform);
        StretchFull(vignetteObj.GetComponent<RectTransform>());
        Image vigImg = vignetteObj.AddComponent<Image>();
        vigImg.color = new Color(0.04f, 0.03f, 0.02f, 0.38f);

        // -------------------------------------------------------------
        // TÍTULO DEL JUEGO
        // -------------------------------------------------------------
        GameObject headerObj = CreateUIElement("GameHeader", canvasObj.transform);
        RectTransform headerRect = headerObj.GetComponent<RectTransform>();
        headerRect.anchorMin = new Vector2(0f, 1f);
        headerRect.anchorMax = new Vector2(1f, 1f);
        headerRect.pivot = new Vector2(0.5f, 1f);
        headerRect.anchoredPosition = new Vector2(0f, -80f);
        headerRect.sizeDelta = new Vector2(1800f, 200f);

        CreateText("MainTitle", headerObj.transform,
            "SOMBRAS DE LA FRONTERA",
            64, FontStyle.Bold, new Color(1.0f, 0.78f, 0.28f, 1f),
            new Vector2(0f, -30f), new Vector2(1600f, 85f));

        CreateText("Tagline", headerObj.transform,
            "El viaje de supervivencia y sigilo de Mateo hacia el Río Bravo",
            26, FontStyle.Italic, new Color(1.0f, 0.92f, 0.78f, 0.95f),
            new Vector2(0f, -100f), new Vector2(1400f, 40f));

        // -------------------------------------------------------------
        // CONTENEDOR DE BOTONES DEL MENÚ PRINCIPAL
        // -------------------------------------------------------------
        GameObject mainMenuButtonsPanel = CreateUIElement("MainMenuButtons", canvasObj.transform);
        RectTransform menuRect = mainMenuButtonsPanel.GetComponent<RectTransform>();
        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0.5f, 0.5f);
        menuRect.anchoredPosition = new Vector2(0f, -70f);
        menuRect.sizeDelta = new Vector2(480f, 440f);

        Button playBtn = CreateMenuButton("Btn_Play", mainMenuButtonsPanel.transform, "JUGAR / NUEVA PARTIDA", 0, new Color(0.18f, 0.65f, 0.32f, 1f));
        Button levelsBtn = CreateMenuButton("Btn_Levels", mainMenuButtonsPanel.transform, "SELECCIÓN DE NIVELES", 1, new Color(0.85f, 0.62f, 0.18f, 1f));
        Button optionsBtn = CreateMenuButton("Btn_Options", mainMenuButtonsPanel.transform, "OPCIONES DE AUDIO", 2, new Color(0.24f, 0.45f, 0.68f, 1f));
        Button creditsBtn = CreateMenuButton("Btn_Credits", mainMenuButtonsPanel.transform, "CRÉDITOS OFICIALES", 3, new Color(0.52f, 0.38f, 0.68f, 1f));
        Button quitBtn = CreateMenuButton("Btn_Quit", mainMenuButtonsPanel.transform, "SALIR DEL JUEGO", 4, new Color(0.75f, 0.22f, 0.20f, 1f));

        // -------------------------------------------------------------
        // PANTALLA DEDICADA DE SELECCIÓN DE LOS 4 NIVELES
        // (Usa la misma imagen pero más opaca/oscura como solicitó el usuario)
        // -------------------------------------------------------------
        GameObject levelsScreenObj = CreateUIElement("LevelsScreen", canvasObj.transform);
        StretchFull(levelsScreenObj.GetComponent<RectTransform>());

        // Fondo con la misma imagen
        GameObject levBg = CreateUIElement("LevelsBackground", levelsScreenObj.transform);
        StretchFull(levBg.GetComponent<RectTransform>());
        Image levBgImg = levBg.AddComponent<Image>();
        if (userBgSprite != null) levBgImg.sprite = userBgSprite;

        // Capa más opaca / oscura (75% oscuridad) para que los 4 cuadros resalten
        GameObject levDarkOverlay = CreateUIElement("DarkOverlay", levelsScreenObj.transform);
        StretchFull(levDarkOverlay.GetComponent<RectTransform>());
        Image darkImg = levDarkOverlay.AddComponent<Image>();
        darkImg.color = new Color(0.03f, 0.02f, 0.02f, 0.78f);

        // Encabezado de Niveles
        CreateText("LevelsHeader_Title", levelsScreenObj.transform,
            "SELECCIÓN DE NIVELES",
            48, FontStyle.Bold, new Color(1.0f, 0.80f, 0.30f, 1f),
            new Vector2(0f, 440f), new Vector2(1200f, 60f));

        CreateText("LevelsHeader_Subtitle", levelsScreenObj.transform,
            "Elige un tramo de la travesía de Mateo hacia el Río Bravo",
            22, FontStyle.Italic, new Color(0.95f, 0.90f, 0.80f, 0.9f),
            new Vector2(0f, 395f), new Vector2(1000f, 35f));

        // Contenedor de la cuadrícula 2x2 para los 4 cuadros
        GameObject gridContainer = CreateUIElement("Grid2x2", levelsScreenObj.transform);
        RectTransform gridRect = gridContainer.GetComponent<RectTransform>();
        gridRect.sizeDelta = new Vector2(1650f, 640f);
        gridRect.anchoredPosition = new Vector2(0f, -40f);

        // Cuadro 1: Nivel 1 (Arriba Izquierda) - CON CAPTURA DEL ESCENARIO
        CreateLevelCard("Card_Level1", gridContainer.transform,
            new Vector2(-415f, 150f),
            "NIVEL 1: EL DESIERTO",
            "TRAMO 1: DESIERTO DE SONORA",
            "Supera la vigilancia de coyotes, víboras de cascabel, alacranes y pozos de arena movediza.",
            level1ThumbSprite,
            true,
            out Button cardLvl1Btn,
            out Text lvl1StatusText);

        // Cuadro 2: Nivel 2 (Arriba Derecha)
        CreateLevelCard("Card_Level2", gridContainer.transform,
            new Vector2(415f, 150f),
            "NIVEL 2: EL RÍO BRAVO",
            "TRAMO 2: CORRIENTES ACUÁTICAS",
            "Navega y cruza las fuertes aguas evadiendo reflectores y lanchas de patrulla fronteriza.",
            null,
            false,
            out Button cardLvl2Btn,
            out Text lvl2StatusText);

        // Cuadro 3: Nivel 3 (Abajo Izquierda)
        CreateLevelCard("Card_Level3", gridContainer.transform,
            new Vector2(-415f, -170f),
            "NIVEL 3: EL MURO FRONTERIZO",
            "TRAMO 3: BARRERAS Y REFLECTORES",
            "Infiltración nocturna a través de alambradas de púas, cámaras térmicas y torres de vigilancia.",
            null,
            false,
            out Button cardLvl3Btn,
            out Text lvl3StatusText);

        // Cuadro 4: Nivel 4 (Abajo Derecha)
        CreateLevelCard("Card_Level4", gridContainer.transform,
            new Vector2(415f, -170f),
            "NIVEL 4: EL REFUGIO SEGURO",
            "TRAMO 4: ZONA URBANA CLANDESTINA",
            "Evade las redadas urbanas finales y alcanza el santuario seguro al otro lado de la frontera.",
            null,
            false,
            out Button cardLvl4Btn,
            out Text lvl4StatusText);

        // Botón inferior para regresar al Menú Principal
        Button backFromLevelsBtn = CreateReturnButton("Btn_BackFromLevels", levelsScreenObj.transform, "◀  REGRESAR AL MENÚ PRINCIPAL", new Vector2(0f, -460f));

        // -------------------------------------------------------------
        // PANEL MODAL: OPCIONES (AUDIO SLIDERS)
        // -------------------------------------------------------------
        GameObject optionsModal = CreateModalWindow("OptionsModal", canvasObj.transform, "AJUSTES DE AUDIO", new Vector2(850f, 620f));
        Transform optBody = optionsModal.transform.Find("Card/Body");

        Slider masterSlider = CreateAudioSlider("Slider_Master", optBody, "Volumen General (Master):", 0, out Text masterValText);
        Slider musicSlider = CreateAudioSlider("Slider_Music", optBody, "Música de Fondo (BGM):", 1, out Text musicValText);
        Slider sfxSlider = CreateAudioSlider("Slider_SFX", optBody, "Efectos de Sonido (SFX):", 2, out Text sfxValText);

        Button closeOptBtn = CreateModalCloseButton("Btn_CloseOptions", optionsModal.transform.Find("Card"), "GUARDAR Y REGRESAR");

        // -------------------------------------------------------------
        // PANEL MODAL: CRÉDITOS OFICIALES
        // -------------------------------------------------------------
        GameObject creditsModal = CreateModalWindow("CreditsModal", canvasObj.transform, "CRÉDITOS OFICIALES", new Vector2(980f, 720f));
        Transform credBody = creditsModal.transform.Find("Card/Body");

        string creditsContent =
            "<b>UNIVERSIDAD TECNOLÓGICA DEL NORTE DE GUANAJUATO (UTNG)</b>\n" +
            "Área de Tecnologías de la Información • Desarrollo de Software Multiplataforma\n\n" +
            "<b>MATERIA:</b> Desarrollo de Videojuegos 2D (Evaluación Unidad 2)\n" +
            "<b>DOCENTE TITULAR:</b> Prof. Gabriel Barrón Rodríguez\n\n" +
            "<b>EQUIPO DE DESARROLLO:</b>\n" +
            "• José de Jesús Loredo Melendez\n" +
            "• Karen Anahí Padrón Martínez\n\n" +
            "<b>ASSETS Y LICENCIAS DE RECURSOS:</b>\n" +
            "• Sprites ambientales, flora y fauna nociva: Kenney.nl & OpenGameArt (Licencia CC0 / CC-BY)\n" +
            "• Audio y efectos desérticos: Freesound.org bajo licencia de uso educativo no comercial\n" +
            "• Motor de juego: Unity 6 (6000.x) — Universal Render Pipeline 2D\n\n" +
            "<i>Octubre 2026 • Dolores Hidalgo C.I.N., Gto.</i>";

        CreateText("CreditsContent", credBody,
            creditsContent,
            21, FontStyle.Normal, new Color(0.92f, 0.92f, 0.92f, 1f),
            Vector2.zero, new Vector2(900f, 460f));

        Button closeCredBtn = CreateModalCloseButton("Btn_CloseCredits", creditsModal.transform.Find("Card"), "VOLVER AL MENÚ PRINCIPAL");

        // -------------------------------------------------------------
        // VINCULACIÓN AL CONTROLADOR MAINMENUCONTROLLER
        // -------------------------------------------------------------
        MainMenuController menuCtrl = canvasObj.AddComponent<MainMenuController>();

        SerializedObject so = new SerializedObject(menuCtrl);

        so.FindProperty("mainMenuButtons").objectReferenceValue = mainMenuButtonsPanel;
        so.FindProperty("levelsScreen").objectReferenceValue = levelsScreenObj;
        so.FindProperty("optionsPanel").objectReferenceValue = optionsModal;
        so.FindProperty("creditsPanel").objectReferenceValue = creditsModal;

        so.FindProperty("playButton").objectReferenceValue = playBtn;
        so.FindProperty("levelsButton").objectReferenceValue = levelsBtn;
        so.FindProperty("optionsButton").objectReferenceValue = optionsBtn;
        so.FindProperty("creditsButton").objectReferenceValue = creditsBtn;
        so.FindProperty("quitButton").objectReferenceValue = quitBtn;

        so.FindProperty("backFromLevelsButton").objectReferenceValue = backFromLevelsBtn;
        so.FindProperty("closeOptionsButton").objectReferenceValue = closeOptBtn;
        so.FindProperty("closeCreditsButton").objectReferenceValue = closeCredBtn;

        // Cuadros de los 4 niveles
        so.FindProperty("cardLevel1Btn").objectReferenceValue = cardLvl1Btn;
        so.FindProperty("cardLevel2Btn").objectReferenceValue = cardLvl2Btn;
        so.FindProperty("cardLevel3Btn").objectReferenceValue = cardLvl3Btn;
        so.FindProperty("cardLevel4Btn").objectReferenceValue = cardLvl4Btn;

        so.FindProperty("level2StatusText").objectReferenceValue = lvl2StatusText;
        so.FindProperty("level3StatusText").objectReferenceValue = lvl3StatusText;
        so.FindProperty("level4StatusText").objectReferenceValue = lvl4StatusText;

        // Sliders de audio
        so.FindProperty("masterVolumeSlider").objectReferenceValue = masterSlider;
        so.FindProperty("masterVolumeText").objectReferenceValue = masterValText;
        so.FindProperty("musicVolumeSlider").objectReferenceValue = musicSlider;
        so.FindProperty("musicVolumeText").objectReferenceValue = musicValText;
        so.FindProperty("sfxVolumeSlider").objectReferenceValue = sfxSlider;
        so.FindProperty("sfxVolumeText").objectReferenceValue = sfxValText;

        so.ApplyModifiedProperties();

        // Ocultar pantallas secundarias por defecto
        levelsScreenObj.SetActive(false);
        optionsModal.SetActive(false);
        creditsModal.SetActive(false);

        EditorSceneManager.SaveScene(menuScene);
        Debug.Log("[SetupMenu] Escena 01_MainMenu creada exitosamente con 4 Cuadros de Niveles y miniatura de Nivel 1.");
    }

    // =========================================================================
    // ACTUALIZACIÓN DE BUILD SETTINGS
    // =========================================================================

    private static void UpdateBuildSettings()
    {
        EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
        {
            new EditorBuildSettingsScene(SPLASH_SCENE_PATH, true),
            new EditorBuildSettingsScene(MENU_SCENE_PATH, true),
            new EditorBuildSettingsScene(LEVEL1_SCENE_PATH, true)
        };

        EditorBuildSettings.scenes = scenes;
        Debug.Log("[SetupBuild] Build Settings actualizado: 0=Splash, 1=MainMenu, 2=Level1_Desert.");
    }

    // =========================================================================
    // MÉTODOS AUXILIARES DE CONSTRUCCIÓN UI
    // =========================================================================

    private static GameObject CreateUIElement(string name, Transform parent)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void CreateEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<UnityEngine.EventSystems.EventSystem>();
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }
    }

    private static Text CreateText(string name, Transform parent, string content, int fontSize, FontStyle style, Color color, Vector2 pos, Vector2 size)
    {
        GameObject textObj = CreateUIElement(name, parent);
        RectTransform rt = textObj.GetComponent<RectTransform>();
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Text txt = textObj.AddComponent<Text>();
        txt.text = content;
        txt.fontSize = fontSize;
        txt.fontStyle = style;
        txt.color = color;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.horizontalOverflow = HorizontalWrapMode.Wrap;
        txt.verticalOverflow = VerticalWrapMode.Overflow;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return txt;
    }

    private static Button CreateMenuButton(string name, Transform parent, string text, int index, Color normalColor)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(460f, 62f);
        rt.anchoredPosition = new Vector2(0f, 150f - (index * 78f));

        Image img = btnObj.AddComponent<Image>();
        img.color = normalColor;

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = normalColor;
        cb.highlightedColor = normalColor * 1.25f;
        cb.pressedColor = normalColor * 0.85f;
        cb.disabledColor = new Color(normalColor.r * 0.4f, normalColor.g * 0.4f, normalColor.b * 0.4f, 0.45f);
        btn.colors = cb;

        GameObject txtObj = CreateUIElement("Text", btnObj.transform);
        StretchFull(txtObj.GetComponent<RectTransform>());
        Text txt = txtObj.AddComponent<Text>();
        txt.text = text;
        txt.fontSize = 24;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    private static void CreateLevelCard(string name, Transform parent, Vector2 pos, string title, string subheader, string description, Sprite thumbnail, bool isLevel1, out Button cardButton, out Text statusText)
    {
        GameObject cardObj = CreateUIElement(name, parent);
        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.sizeDelta = new Vector2(780f, 290f);
        cardRect.anchoredPosition = pos;

        Image cardImg = cardObj.AddComponent<Image>();
        cardImg.color = isLevel1 ? new Color(0.14f, 0.12f, 0.10f, 0.95f) : new Color(0.08f, 0.08f, 0.09f, 0.90f);

        // Contorno exterior (dorado para nivel activo, gris para bloqueados)
        Outline cardOutline = cardObj.AddComponent<Outline>();
        cardOutline.effectColor = isLevel1 ? new Color(0.95f, 0.72f, 0.25f, 0.85f) : new Color(0.35f, 0.35f, 0.40f, 0.5f);
        cardOutline.effectDistance = new Vector2(2.5f, -2.5f);

        // Toda la tarjeta funciona como botón clicable
        cardButton = cardObj.AddComponent<Button>();
        ColorBlock cb = cardButton.colors;
        cb.normalColor = isLevel1 ? new Color(0.14f, 0.12f, 0.10f, 0.95f) : new Color(0.08f, 0.08f, 0.09f, 0.90f);
        cb.highlightedColor = isLevel1 ? new Color(0.24f, 0.20f, 0.14f, 1f) : new Color(0.12f, 0.12f, 0.14f, 0.95f);
        cb.pressedColor = isLevel1 ? new Color(0.10f, 0.08f, 0.06f, 1f) : new Color(0.06f, 0.06f, 0.07f, 0.95f);
        cb.disabledColor = new Color(0.06f, 0.06f, 0.07f, 0.75f);
        cardButton.colors = cb;
        cardButton.interactable = isLevel1;

        // -------------------------------------------------------------
        // MARCO DE MINIATURA (IZQUIERDA)
        // -------------------------------------------------------------
        GameObject thumbFrame = CreateUIElement("ThumbFrame", cardObj.transform);
        RectTransform frameRect = thumbFrame.GetComponent<RectTransform>();
        frameRect.sizeDelta = new Vector2(310f, 250f);
        frameRect.anchoredPosition = new Vector2(-215f, 0f);

        Image frameImg = thumbFrame.AddComponent<Image>();
        frameImg.color = isLevel1 ? Color.white : new Color(0.12f, 0.12f, 0.14f, 1f);

        if (thumbnail != null)
        {
            frameImg.sprite = thumbnail;
            frameImg.preserveAspect = false;
        }
        else
        {
            // Placeholder para niveles 2, 3 y 4 con candado
            CreateText("LockIcon", thumbFrame.transform,
                "🔒",
                60, FontStyle.Normal, new Color(0.6f, 0.6f, 0.6f, 0.5f),
                Vector2.zero, new Vector2(100f, 100f));
        }

        Outline thumbOutline = thumbFrame.AddComponent<Outline>();
        thumbOutline.effectColor = new Color(0.05f, 0.05f, 0.05f, 0.9f);
        thumbOutline.effectDistance = new Vector2(2f, -2f);

        // -------------------------------------------------------------
        // DETALLES DEL NIVEL (DERECHA)
        // -------------------------------------------------------------
        Transform detailsParent = cardObj.transform;

        // Título
        CreateText("LevelTitle", detailsParent,
            title,
            24, FontStyle.Bold, isLevel1 ? new Color(1.0f, 0.82f, 0.35f, 1f) : new Color(0.75f, 0.75f, 0.75f, 0.8f),
            new Vector2(165f, 85f), new Vector2(400f, 34f));

        // Subtítulo del tramo
        CreateText("LevelSubheader", detailsParent,
            subheader,
            16, FontStyle.Bold, isLevel1 ? new Color(0.35f, 0.85f, 0.80f, 1f) : new Color(0.55f, 0.55f, 0.60f, 0.8f),
            new Vector2(165f, 52f), new Vector2(400f, 26f));

        // Descripción
        CreateText("LevelDesc", detailsParent,
            description,
            15, FontStyle.Normal, new Color(0.85f, 0.85f, 0.85f, 0.9f),
            new Vector2(165f, 0f), new Vector2(400f, 60f));

        // Estado (Desbloqueado / Bloqueado)
        statusText = CreateText("StatusBadge", detailsParent,
            isLevel1 ? "DISPONIBLE" : "🔒 BLOQUEADO",
            17, FontStyle.Bold, isLevel1 ? new Color(0.35f, 0.90f, 0.45f, 1f) : new Color(0.65f, 0.65f, 0.65f, 0.65f),
            new Vector2(165f, -50f), new Vector2(400f, 26f));

        // Botón indicador dentro de la tarjeta
        GameObject actionBtn = CreateUIElement("ActionBadge", detailsParent);
        RectTransform actRect = actionBtn.GetComponent<RectTransform>();
        actRect.sizeDelta = new Vector2(240f, 42f);
        actRect.anchoredPosition = new Vector2(165f, -95f);

        Image actImg = actionBtn.AddComponent<Image>();
        actImg.color = isLevel1 ? new Color(0.18f, 0.65f, 0.32f, 1f) : new Color(0.25f, 0.25f, 0.28f, 0.6f);

        CreateText("ActionText", actionBtn.transform,
            isLevel1 ? "INICIAR TRAVESÍA ▶" : "BLOQUEADO 🔒",
            17, FontStyle.Bold, Color.white,
            Vector2.zero, new Vector2(240f, 42f));
    }

    private static Button CreateReturnButton(string name, Transform parent, string label, Vector2 pos)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(460f, 58f);
        rt.anchoredPosition = pos;

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.20f, 0.20f, 0.22f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.20f, 0.20f, 0.22f, 1f);
        cb.highlightedColor = new Color(0.32f, 0.32f, 0.36f, 1f);
        cb.pressedColor = new Color(0.14f, 0.14f, 0.16f, 1f);
        btn.colors = cb;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.75f, 0.25f, 0.85f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject txtObj = CreateUIElement("Text", btnObj.transform);
        StretchFull(txtObj.GetComponent<RectTransform>());
        Text txt = txtObj.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }

    private static GameObject CreateModalWindow(string name, Transform parent, string title, Vector2 size)
    {
        GameObject modalRoot = CreateUIElement(name, parent);
        StretchFull(modalRoot.GetComponent<RectTransform>());

        Image backdrop = modalRoot.AddComponent<Image>();
        backdrop.color = new Color(0.03f, 0.02f, 0.02f, 0.88f);

        GameObject card = CreateUIElement("Card", modalRoot.transform);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.sizeDelta = size;
        cardRect.anchoredPosition = Vector2.zero;

        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.08f, 0.08f, 0.10f, 0.98f);

        Outline outline = card.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.72f, 0.25f, 0.75f);
        outline.effectDistance = new Vector2(3f, -3f);

        CreateText("Title", card.transform,
            title,
            34, FontStyle.Bold, new Color(1.0f, 0.78f, 0.25f, 1f),
            new Vector2(0f, (size.y * 0.5f) - 55f), new Vector2(size.x - 80f, 50f));

        GameObject div = CreateUIElement("Divider", card.transform);
        RectTransform divRect = div.GetComponent<RectTransform>();
        divRect.sizeDelta = new Vector2(size.x - 100f, 2f);
        divRect.anchoredPosition = new Vector2(0f, (size.y * 0.5f) - 90f);
        Image divImg = div.AddComponent<Image>();
        divImg.color = new Color(0.95f, 0.72f, 0.25f, 0.4f);

        GameObject body = CreateUIElement("Body", card.transform);
        RectTransform bodyRect = body.GetComponent<RectTransform>();
        bodyRect.anchoredPosition = new Vector2(0f, -35f);
        bodyRect.sizeDelta = new Vector2(size.x - 80f, size.y - 180f);

        return modalRoot;
    }

    private static Slider CreateAudioSlider(string name, Transform parent, string label, int index, out Text valueText)
    {
        float yPos = 80f - (index * 95f);

        CreateText($"{name}_Label", parent,
            label,
            23, FontStyle.Bold, new Color(0.95f, 0.92f, 0.85f, 1f),
            new Vector2(-150f, yPos + 26f), new Vector2(450f, 32f));

        valueText = CreateText($"{name}_ValueText", parent,
            "85%",
            22, FontStyle.Bold, new Color(1.0f, 0.82f, 0.35f, 1f),
            new Vector2(280f, yPos), new Vector2(100f, 32f));

        GameObject sliderObj = CreateUIElement(name, parent);
        RectTransform sliderRect = sliderObj.GetComponent<RectTransform>();
        sliderRect.sizeDelta = new Vector2(500f, 26f);
        sliderRect.anchoredPosition = new Vector2(-60f, yPos);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0.0001f;
        slider.maxValue = 1.0f;
        slider.value = 0.85f;

        GameObject sBg = CreateUIElement("Background", sliderObj.transform);
        StretchFull(sBg.GetComponent<RectTransform>());
        Image sBgImg = sBg.AddComponent<Image>();
        sBgImg.color = new Color(0.2f, 0.2f, 0.22f, 1f);

        GameObject fillArea = CreateUIElement("Fill Area", sliderObj.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
        fillAreaRect.offsetMin = new Vector2(5f, 0f);
        fillAreaRect.offsetMax = new Vector2(-5f, 0f);

        GameObject fill = CreateUIElement("Fill", fillArea.transform);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.95f, 0.65f, 0.18f, 1f);

        GameObject handleArea = CreateUIElement("Handle Slide Area", sliderObj.transform);
        StretchFull(handleArea.GetComponent<RectTransform>());

        GameObject handle = CreateUIElement("Handle", handleArea.transform);
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.sizeDelta = new Vector2(24f, 34f);
        Image handleImg = handle.AddComponent<Image>();
        handleImg.color = Color.white;

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handleImg;
        slider.direction = Slider.Direction.LeftToRight;

        return slider;
    }

    private static Button CreateModalCloseButton(string name, Transform parent, string label)
    {
        GameObject btnObj = CreateUIElement(name, parent);
        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(400f, 55f);
        rt.anchoredPosition = new Vector2(0f, -((parent.GetComponent<RectTransform>().sizeDelta.y * 0.5f) - 45f));

        Image img = btnObj.AddComponent<Image>();
        img.color = new Color(0.25f, 0.25f, 0.28f, 1f);

        Button btn = btnObj.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        cb.highlightedColor = new Color(0.35f, 0.35f, 0.40f, 1f);
        cb.pressedColor = new Color(0.18f, 0.18f, 0.20f, 1f);
        btn.colors = cb;

        Outline outline = btnObj.AddComponent<Outline>();
        outline.effectColor = new Color(0.95f, 0.75f, 0.25f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        GameObject txtObj = CreateUIElement("Text", btnObj.transform);
        StretchFull(txtObj.GetComponent<RectTransform>());
        Text txt = txtObj.AddComponent<Text>();
        txt.text = label;
        txt.fontSize = 22;
        txt.fontStyle = FontStyle.Bold;
        txt.color = Color.white;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        return btn;
    }
}
