using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Construye en la escena del Nivel 1:
/// 1. Menú de Pausa (ESC / P / botón en el HUD) con Continuar, Reiniciar Nivel y Volver al Menú.
/// 2. Retroalimentación del HUD: destello rojo al recibir daño e indicador "OCULTO".
/// 3. Post-procesado: viñeta cálida que se tiñe de rojo cuando una linterna detecta a Mateo,
///    y un ligero ajuste de contraste/saturación para el desierto.
/// También incluye el menú maestro que aplica todo el Sprint 2 en orden.
/// </summary>
public static class SetupPausaYVisualesEditor
{
    private const string VOLUME_PROFILE_PATH = "Assets/Settings/Level1_PostProcess.asset";
    private static Font defaultFont;

    [MenuItem("Sombras de la Frontera/Sprint 2/★ Aplicar TODO (Animaciones, Escenario, Pausa, Visuales y Sonido)", false, 0)]
    public static void ApplyEverything()
    {
        if (!SetupEscenarioEditor.EnsureLevelSceneOpen()) return;

        SetupAnimacionesEditor.GenerateAllAnimations();
        SetupEscenarioEditor.ArrangeScenery();
        BuildPauseAndVisuals();

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        SetupSonidosEditor.SetupSounds();
        Debug.Log("<color=cyan><b>[Sprint 2]</b> ¡Todo aplicado (incluidos sonidos y música) y las escenas fueron guardadas! Presiona Play para probar.</color>");
    }

    [MenuItem("Sombras de la Frontera/Sprint 2/3. Crear Menú de Pausa y Mejoras Visuales")]
    public static void BuildPauseAndVisuals()
    {
        if (!SetupEscenarioEditor.EnsureLevelSceneOpen()) return;

        defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        Canvas canvas = null;
        GameObject hud = GameObject.Find("HUD_Canvas");
        if (hud != null) canvas = hud.GetComponent<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[Pausa] No se encontró HUD_Canvas en la escena. Genera primero el HUD.");
            return;
        }

        BuildPauseMenu(canvas);
        BuildHudFeedback(canvas);
        BuildPostProcessing();
        FixDepthSortingTool();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green><b>[Pausa y Visuales]</b> Menú de pausa, destello de daño, indicador OCULTO y viñeta de peligro listos.</color>");
    }

    // =====================================================================
    // MENÚ DE PAUSA
    // =====================================================================

    private static void BuildPauseMenu(Canvas canvas)
    {
        Transform old = canvas.transform.Find("PausePanel");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        Transform oldBtn = canvas.transform.Find("PauseButton");
        if (oldBtn != null) Undo.DestroyObjectImmediate(oldBtn.gameObject);

        // Botón de pausa en la esquina superior derecha
        GameObject hudBtnObj = CreateButton("PauseButton", canvas.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                            new Vector2(-250f, -100f), new Vector2(-40f, -40f),
                                            new Color(0.14f, 0.11f, 0.08f, 0.85f), "II  PAUSA (Esc)", 22);
        Undo.RegisterCreatedObjectUndo(hudBtnObj, "Crear botón de pausa");

        // Panel a pantalla completa
        GameObject panel = CreateUIElement("PausePanel", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(panel, "Crear menú de pausa");
        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.75f);

        GameObject card = CreateUIElement("Card", panel.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-380f, -330f), new Vector2(380f, 330f));
        Image cardImg = card.AddComponent<Image>();
        cardImg.color = new Color(0.14f, 0.11f, 0.08f, 0.97f);

        GameObject title = CreateUIElement("Title", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -120f), new Vector2(-25f, -25f));
        Text titleText = title.AddComponent<Text>();
        titleText.font = defaultFont;
        titleText.fontSize = 56;
        titleText.fontStyle = FontStyle.Bold;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.color = new Color(0.96f, 0.78f, 0.28f, 1f);
        titleText.text = "PAUSA";

        GameObject hint = CreateUIElement("Hint", card.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(25f, -175f), new Vector2(-25f, -115f));
        Text hintText = hint.AddComponent<Text>();
        hintText.font = defaultFont;
        hintText.fontSize = 22;
        hintText.alignment = TextAnchor.MiddleCenter;
        hintText.color = new Color(0.9f, 0.88f, 0.8f, 0.85f);
        hintText.text = "Quieto y pegado al lado de abajo de un obstáculo, Mateo se agacha y se esconde.";

        Button resume = CreateButton("ResumeButton", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -290f), new Vector2(260f, -210f),
                                     new Color(0.18f, 0.65f, 0.35f, 1f), "Continuar", 28).GetComponent<Button>();
        Button restart = CreateButton("RestartButton", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -400f), new Vector2(260f, -320f),
                                      new Color(0.85f, 0.45f, 0.12f, 1f), "Reiniciar Nivel", 28).GetComponent<Button>();
        Button menu = CreateButton("MenuButton", card.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-260f, -510f), new Vector2(260f, -430f),
                                   new Color(0.45f, 0.48f, 0.52f, 1f), "Volver al Menú", 28).GetComponent<Button>();

        // Navegación con flechas del teclado
        SetVerticalNavigation(resume, menu, restart);
        SetVerticalNavigation(restart, resume, menu);
        SetVerticalNavigation(menu, restart, resume);

        panel.SetActive(false);

        PauseMenuController controller = canvas.GetComponent<PauseMenuController>();
        if (controller == null) controller = Undo.AddComponent<PauseMenuController>(canvas.gameObject);
        var so = new SerializedObject(controller);
        so.FindProperty("pausePanel").objectReferenceValue = panel;
        so.FindProperty("resumeButton").objectReferenceValue = resume;
        so.FindProperty("restartButton").objectReferenceValue = restart;
        so.FindProperty("menuButton").objectReferenceValue = menu;
        so.FindProperty("hudPauseButton").objectReferenceValue = hudBtnObj.GetComponent<Button>();
        so.ApplyModifiedProperties();

        // Orden de dibujo: el botón sobre el HUD, la pausa por encima de todo
        hudBtnObj.transform.SetAsLastSibling();
        panel.transform.SetAsLastSibling();
    }

    private static void SetVerticalNavigation(Button button, Button up, Button down)
    {
        Navigation nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = up, selectOnDown = down };
        button.navigation = nav;
    }

    // =====================================================================
    // RETROALIMENTACIÓN DEL HUD
    // =====================================================================

    private static void BuildHudFeedback(Canvas canvas)
    {
        HUDController hudController = canvas.GetComponent<HUDController>();
        if (hudController == null) return;

        Transform oldFlash = canvas.transform.Find("DamageFlash");
        if (oldFlash != null) Undo.DestroyObjectImmediate(oldFlash.gameObject);
        Transform oldStealth = canvas.transform.Find("StealthIndicator");
        if (oldStealth != null) Undo.DestroyObjectImmediate(oldStealth.gameObject);

        // Destello rojo a pantalla completa (debajo de los paneles modales)
        GameObject flash = CreateUIElement("DamageFlash", canvas.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        Undo.RegisterCreatedObjectUndo(flash, "Crear destello de daño");
        Image flashImg = flash.AddComponent<Image>();
        flashImg.color = new Color(0.85f, 0.05f, 0.05f, 0f);
        flashImg.raycastTarget = false;
        flash.transform.SetAsFirstSibling();

        // Indicador "OCULTO" debajo de corazones y barra de agua
        GameObject stealth = CreateUIElement("StealthIndicator", canvas.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -235f), new Vector2(330f, -180f));
        Undo.RegisterCreatedObjectUndo(stealth, "Crear indicador de sigilo");
        Image stealthBg = stealth.AddComponent<Image>();
        stealthBg.color = new Color(0.06f, 0.16f, 0.10f, 0.85f);
        stealthBg.raycastTarget = false;
        CanvasGroup group = stealth.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        GameObject label = CreateUIElement("Text", stealth.transform, Vector2.zero, Vector2.one, new Vector2(10f, 0f), new Vector2(-10f, 0f));
        Text labelText = label.AddComponent<Text>();
        labelText.font = defaultFont;
        labelText.fontSize = 26;
        labelText.fontStyle = FontStyle.Bold;
        labelText.alignment = TextAnchor.MiddleCenter;
        labelText.color = new Color(0.55f, 0.95f, 0.6f, 1f);
        labelText.text = "OCULTO";
        labelText.raycastTarget = false;

        var so = new SerializedObject(hudController);
        so.FindProperty("damageFlashImage").objectReferenceValue = flashImg;
        so.FindProperty("stealthIndicator").objectReferenceValue = group;
        so.ApplyModifiedProperties();
    }

    // =====================================================================
    // POST-PROCESADO
    // =====================================================================

    private static void BuildPostProcessing()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VOLUME_PROFILE_PATH);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, VOLUME_PROFILE_PATH);
        }

        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
            AssetDatabase.AddObjectToAsset(vignette, profile);
        }
        vignette.color.Override(new Color(0.16f, 0.07f, 0.02f, 1f));
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.45f);

        if (!profile.TryGet(out ColorAdjustments colorAdjustments))
        {
            colorAdjustments = profile.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(colorAdjustments, profile);
        }
        colorAdjustments.contrast.Override(8f);
        colorAdjustments.saturation.Override(6f);

        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(colorAdjustments);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject volumeObj = GameObject.Find("PostProcess_Volume");
        if (volumeObj == null)
        {
            volumeObj = new GameObject("PostProcess_Volume");
            Undo.RegisterCreatedObjectUndo(volumeObj, "Crear volumen de post-procesado");
        }

        Volume volume = volumeObj.GetComponent<Volume>();
        if (volume == null) volume = Undo.AddComponent<Volume>(volumeObj);
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = profile;

        if (volumeObj.GetComponent<DangerVignette>() == null) Undo.AddComponent<DangerVignette>(volumeObj);

        // Activar post-procesado en la cámara principal
        Camera cam = Camera.main;
        if (cam != null)
        {
            UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                Undo.RecordObject(data, "Activar post-procesado");
                data.renderPostProcessing = true;
                EditorUtility.SetDirty(data);
            }
        }
    }

    /// <summary>
    /// La herramienta de profundidad anterior agregaba YSort a TODOS los sprites de los enemigos;
    /// se quita de las sombras e iconos de alerta por si se volvió a ejecutar.
    /// </summary>
    private static void FixDepthSortingTool()
    {
        foreach (BlobShadow shadow in Object.FindObjectsByType<BlobShadow>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            YSort2D ys = shadow.GetComponent<YSort2D>();
            if (ys != null) Undo.DestroyObjectImmediate(ys);
        }
        foreach (AlertIndicator icon in Object.FindObjectsByType<AlertIndicator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            YSort2D ys = icon.GetComponent<YSort2D>();
            if (ys != null) Undo.DestroyObjectImmediate(ys);
        }
    }

    // =====================================================================
    // UTILIDADES DE UI (mismo estilo que los modales de Game Over / Victoria)
    // =====================================================================

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
        cb.selectedColor = btnColor * 1.15f;
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
