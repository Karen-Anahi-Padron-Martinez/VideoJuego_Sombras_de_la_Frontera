using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor script para construir y configurar automáticamente en la escena:
/// 1. Cartel emergente tutorial del Tramo 1 (TutorialPopup).
/// 2. Modal de Game Over con botones de Reintentar y Menú (EndGameModalController).
/// 3. Modal de Victoria con guardado de NivelMaximo = 2.
/// 4. GoalTrigger en la ribera norte del río (Y = 34.5).
/// </summary>
public static class SetupEndGameAndTutorialUIEditor
{
    private static Font defaultFont;

    [InitializeOnLoadMethod]
    private static void AutoSetupIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (EditorSceneManager.GetActiveScene().name != "02_Level1_Desert") return;

            GameObject existingGoal = GameObject.Find("GoalTrigger_RioBravo");
            GameObject existingTutorial = GameObject.Find("TutorialPanel");
            GameObject existingHUD = GameObject.Find("HUD_Canvas");

            if (existingGoal == null || existingTutorial == null || existingHUD == null)
            {
                BuildAllUIAndGoal();
            }
        };
    }

    [MenuItem("Sombras de la Frontera/Configurar UI (Tutorial, Game Over, Victoria y Meta del Río)")]
    public static void BuildAllUIAndGoal()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            CreateHUDEditor.GenerateHUD();
            canvas = Object.FindAnyObjectByType<Canvas>();
        }

        if (canvas == null)
        {
            Debug.LogError("[SetupUI] No se encontró ni se pudo crear Canvas en la escena 02_Level1_Desert.");
            return;
        }

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

        // 1. Asegurar o crear EndGameModalController en el Canvas
        EndGameModalController modalCtrl = canvas.GetComponent<EndGameModalController>();
        if (modalCtrl == null)
        {
            modalCtrl = canvas.gameObject.AddComponent<EndGameModalController>();
        }

        // 2. Crear GoalTrigger en la ribera del río (Y = 34.5)
        GameObject existingGoal = GameObject.Find("GoalTrigger_RioBravo");
        if (existingGoal != null) Undo.DestroyObjectImmediate(existingGoal);

        GameObject goalObj = new GameObject("GoalTrigger_RioBravo");
        goalObj.transform.position = new Vector3(0f, 34.5f, 0f);
        BoxCollider2D goalCol = goalObj.AddComponent<BoxCollider2D>();
        goalCol.isTrigger = true;
        goalCol.size = new Vector2(7.5f, 1.8f);
        goalObj.AddComponent<GoalTrigger>();
        Undo.RegisterCreatedObjectUndo(goalObj, "Crear GoalTrigger");

        // Limpiar solo GameOverPanel y VictoryPanel para evitar duplicados y CONSERVAR el TutorialPanel del usuario
        for (int i = canvas.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child.name == "GameOverPanel" || child.name == "VictoryPanel")
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        // 3. Crear Panel de Game Over
        GameObject gameOverPanel = CreateGameOverPanel(canvas.transform, out Button retryBtn, out Button goMenuBtn, out Text goTitleText, out Text goDescText);
        gameOverPanel.SetActive(false);

        // 4. Crear Panel de Victoria
        GameObject victoryPanel = CreateVictoryPanel(canvas.transform, out Button nextBtn, out Button vicMenuBtn);
        victoryPanel.SetActive(false);

        // 5. Vincular referencias en EndGameModalController con SerializedObject
        SerializedObject soModal = new SerializedObject(modalCtrl);
        soModal.FindProperty("gameOverPanel").objectReferenceValue = gameOverPanel;
        soModal.FindProperty("gameOverTitleText").objectReferenceValue = goTitleText;
        soModal.FindProperty("gameOverDescriptionText").objectReferenceValue = goDescText;
        soModal.FindProperty("retryButton").objectReferenceValue = retryBtn;
        soModal.FindProperty("gameOverMenuButton").objectReferenceValue = goMenuBtn;
        soModal.FindProperty("victoryPanel").objectReferenceValue = victoryPanel;
        soModal.FindProperty("nextLevelButton").objectReferenceValue = nextBtn;
        soModal.FindProperty("victoryMenuButton").objectReferenceValue = vicMenuBtn;
        soModal.ApplyModifiedProperties();

        // 6. Conservar el Cartel Emergente de Tutorial del usuario si ya existe
        Transform existingTut = canvas.transform.Find("TutorialPanel");
        if (existingTut == null)
        {
            GameObject tutorialPanel = CreateTutorialPanel(canvas.transform);
            tutorialPanel.SetActive(true);
        }
        else
        {
            existingTut.gameObject.SetActive(true);
        }

        // 7. Aplicar profundidad 2D (Y-Sort) para que obstáculos cubran enemigos naturalmente
        SetupDepthSortingEditor.ApplyDepthSortingToScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green>[SetupUI] ¡UI completa (Tutorial, Game Over, Victoria y GoalTrigger) configurada con éxito!</color>");
    }

    private static GameObject CreateTutorialPanel(Transform parent)
    {
        // Fondo atenuado
        GameObject root = CreateUIElement("TutorialPanel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.82f);

        // Tarjeta central estilo pergamino desértico (ampliada para mejor lectura)
        GameObject card = CreateUIElement("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-550, -350), new Vector2(550, 350));
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.14f, 0.11f, 0.08f, 0.96f);

        // Título
        GameObject titleObj = CreateUIElement("Title", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25, -85), new Vector2(-25, -15));
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 36;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.96f, 0.78f, 0.28f, 1f); // Dorado desierto
        titleText.text = "SOMBRAS DE LA FRONTERA — TRAMO 1";

        // Cuerpo narrativo y controles
        GameObject bodyObj = CreateUIElement("Body", card.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(45, 105), new Vector2(-45, -95));
        Text bodyText = bodyObj.AddComponent<Text>();
        bodyText.font = defaultFont;
        bodyText.fontSize = 24;
        bodyText.lineSpacing = 1.35f;
        bodyText.alignment = TextAnchor.MiddleLeft;
        bodyText.color = new Color(0.94f, 0.94f, 0.94f, 1f);
        bodyText.text = "El sol abrasa y los vigilantes patrullan la frontera...\n\n" +
                        "🕹️  <b>Controles:</b> Usa <b>W, A, S, D</b> o las <b>Flechas</b> para moverte.\n\n" +
                        "🌵 <b>Sigilo:</b> Ocúltate en <b>cactus, rocas y nopaleras</b>. Las linternas de los coyotes y los perros guardianes te ignorarán mientras estés a cubierto.\n\n" +
                        "💧 <b>Supervivencia:</b> El calor vacía tu hidratación. Recoge cantimploras y evita mordeduras de fauna.\n\n" +
                        "🎯 <b>Objetivo:</b> Cruza el desierto hacia el norte hasta alcanzar la ribera del <b>Río Bravo</b>.";

        // Botón Comenzar
        GameObject btnObj = CreateButton("StartButton", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-220, 25), new Vector2(220, 85),
                                         new Color(0.85f, 0.45f, 0.12f, 1f), "¡INICIAR TRAVESÍA! (Espacio)", 22);
        Button startBtn = btnObj.GetComponent<Button>();

        // Componente TutorialPopup
        TutorialPopup popup = root.AddComponent<TutorialPopup>();
        SerializedObject so = new SerializedObject(popup);
        so.FindProperty("tutorialPanel").objectReferenceValue = root;
        so.FindProperty("closeButton").objectReferenceValue = startBtn;
        so.FindProperty("pauseOnStart").boolValue = true;
        so.ApplyModifiedProperties();

        return root;
    }

    private static GameObject CreateGameOverPanel(Transform parent, out Button retryBtn, out Button menuBtn, out Text titleText, out Text descriptionText)
    {
        // Fondo rojo oscuro atenuado
        GameObject root = CreateUIElement("GameOverPanel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.02f, 0.02f, 0.88f);

        // Tarjeta central ampliada (1100 x 700) para máxima legibilidad
        GameObject card = CreateUIElement("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-550, -350), new Vector2(550, 350));
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.15f, 0.08f, 0.08f, 0.96f);

        // Título de derrota grande
        GameObject titleObj = CreateUIElement("Title", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25, -100), new Vector2(-25, -20));
        titleText = titleObj.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 44;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.92f, 0.22f, 0.22f, 1f); // Rojo alerta
        titleText.text = "¡HAS SIDO DESCUBIERTO!";

        // Descripción y consejos de supervivencia grandes
        GameObject descObj = CreateUIElement("Description", card.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(50, 115), new Vector2(-50, -110));
        descriptionText = descObj.AddComponent<Text>();
        descriptionText.font = defaultFont;
        descriptionText.fontSize = 28;
        descriptionText.lineSpacing = 1.35f;
        descriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
        descriptionText.verticalOverflow = VerticalWrapMode.Overflow;
        descriptionText.alignment = TextAnchor.MiddleCenter;
        descriptionText.color = new Color(0.92f, 0.92f, 0.92f, 1f);
        descriptionText.text = "La linterna del vigilante coyote iluminó tu silueta en campo abierto.\n\n🌵 <b>Consejo:</b> Ocúltate tras cactus, rocas o nopaleras para que los reflectores no te vean.";

        // Botón Reintentar
        GameObject btnRetryObj = CreateButton("RetryButton", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-280, 25), new Vector2(-15, 95),
                                              new Color(0.18f, 0.65f, 0.35f, 1f), "Reintentar Nivel", 24);
        retryBtn = btnRetryObj.GetComponent<Button>();

        // Botón Menú
        GameObject btnMenuObj = CreateButton("MenuButton", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(15, 25), new Vector2(280, 95),
                                             new Color(0.45f, 0.48f, 0.52f, 1f), "Volver al Menú", 24);
        menuBtn = btnMenuObj.GetComponent<Button>();

        return root;
    }

    private static GameObject CreateVictoryPanel(Transform parent, out Button nextBtn, out Button menuBtn)
    {
        // Fondo verde bosque oscuro atenuado
        GameObject root = CreateUIElement("VictoryPanel", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Image bg = root.AddComponent<Image>();
        bg.color = new Color(0.02f, 0.08f, 0.04f, 0.88f);

        // Tarjeta central ampliada (1100 x 700)
        GameObject card = CreateUIElement("Card", root.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-550, -350), new Vector2(550, 350));
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.08f, 0.16f, 0.10f, 0.96f);

        // Título de Victoria grande
        GameObject titleObj = CreateUIElement("Title", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25, -100), new Vector2(-25, -20));
        Text titleText = titleObj.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 44;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.24f, 0.82f, 0.42f, 1f); // Verde victoria
        titleText.text = "¡NIVEL 1 COMPLETADO!";

        // Subtítulo grande
        GameObject subObj = CreateUIElement("Subtitle", card.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(50, 115), new Vector2(-50, -110));
        Text subText = subObj.AddComponent<Text>();
        subText.font = defaultFont;
        subText.fontSize = 28;
        subText.lineSpacing = 1.35f;
        subText.horizontalOverflow = HorizontalWrapMode.Wrap;
        subText.verticalOverflow = VerticalWrapMode.Overflow;
        subText.alignment = TextAnchor.MiddleCenter;
        subText.color = new Color(0.95f, 0.88f, 0.65f, 1f);
        subText.text = "¡Has alcanzado la orilla del Río Bravo!\n\nProgreso guardado: Nivel 2 Desbloqueado en disco.";

        // Botón Siguiente Nivel / Continuar
        GameObject btnNextObj = CreateButton("NextLevelButton", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-280, 25), new Vector2(-15, 95),
                                              new Color(0.18f, 0.65f, 0.35f, 1f), "Continuar", 24);
        nextBtn = btnNextObj.GetComponent<Button>();

        // Botón Menú
        GameObject btnMenuObj = CreateButton("MenuButton", card.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(15, 25), new Vector2(280, 95),
                                             new Color(0.45f, 0.48f, 0.52f, 1f), "Volver al Menú", 24);
        menuBtn = btnMenuObj.GetComponent<Button>();

        return root;
    }

    private static GameObject CreateUIElement(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return obj;
    }

    private static GameObject CreateButton(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color btnColor, string text, int fontSize)
    {
        GameObject btnObj = CreateUIElement(name, parent, anchorMin, anchorMax, offsetMin, offsetMax);
        Image img = btnObj.AddComponent<Image>();
        img.color = btnColor;
        Button btn = btnObj.AddComponent<Button>();

        ColorBlock cb = btn.colors;
        cb.highlightedColor = btnColor * 1.15f;
        cb.pressedColor = btnColor * 0.85f;
        btn.colors = cb;

        GameObject textObj = CreateUIElement("Text", btnObj.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Text t = textObj.AddComponent<Text>();
        t.font = defaultFont;
        t.fontSize = fontSize;
        t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = Color.white;
        t.text = text;

        return btnObj;
    }
}
