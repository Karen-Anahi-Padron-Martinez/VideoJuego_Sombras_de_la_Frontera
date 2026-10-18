using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Herramientas de Editor para el HUD y la Cámara:
/// 1. Genera el Canvas HUD con los 3 corazones rojos vibrantes y la barra de agua.
/// 2. Configura la cámara vertical fija (centrada en X, sin fondo azul, con límites al inicio y al final).
/// </summary>
public static class CreateHUDEditor
{
    [MenuItem("Sombras de la Frontera/1. Generar HUD (Corazones Rojos y Barra de Agua)")]
    public static void GenerateHUD()
    {
        // 1. Cargar sprites de UI.png
        Sprite heartSprite = null;
        Sprite barFrameSprite = null;
        Sprite barFillSprite = null;

        Object[] allUiAssets = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/UI/UI.png");
        foreach (Object obj in allUiAssets)
        {
            if (obj is Sprite s)
            {
                if (s.name == "UI_0" || s.name == "UI_1" || s.name == "UI_2")
                {
                    if (heartSprite == null) heartSprite = s;
                }
                else if (s.name == "UI_4" || s.name == "UI_6")
                {
                    if (barFrameSprite == null) barFrameSprite = s;
                }
                else if (s.name == "UI_5")
                {
                    if (barFillSprite == null) barFillSprite = s;
                }
            }
        }

        // 2. Limpiar Canvas previo si existía
        GameObject existingCanvas = GameObject.Find("HUD_Canvas");
        if (existingCanvas != null)
        {
            Undo.DestroyObjectImmediate(existingCanvas);
        }

        // 3. Crear Canvas principal
        GameObject canvasObj = new GameObject("HUD_Canvas");
        Undo.RegisterCreatedObjectUndo(canvasObj, "Crear HUD Canvas");

        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObj.AddComponent<GraphicRaycaster>();

        // 4. Asegurar EventSystem con New Input System
        UnityEngine.EventSystems.EventSystem es = Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>();
        if (es == null)
        {
            GameObject eventSystemObj = new GameObject("EventSystem");
            es = eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            Undo.RegisterCreatedObjectUndo(eventSystemObj, "Crear EventSystem");
        }

        UnityEngine.EventSystems.StandaloneInputModule oldModule = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        if (oldModule != null)
        {
            Undo.DestroyObjectImmediate(oldModule);
        }

        if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() == null)
        {
            es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // 5. Panel superior izquierdo
        GameObject statsPanel = new GameObject("PlayerStatusPanel");
        statsPanel.transform.SetParent(canvasObj.transform, false);
        RectTransform panelRect = statsPanel.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.anchoredPosition = new Vector2(40, -40);
        panelRect.sizeDelta = new Vector2(350, 160);

        // 6. Contenedor de 3 Corazones Rojos
        GameObject heartsContainer = new GameObject("HeartsContainer");
        heartsContainer.transform.SetParent(statsPanel.transform, false);
        RectTransform heartsRect = heartsContainer.AddComponent<RectTransform>();
        heartsRect.anchorMin = new Vector2(0, 1);
        heartsRect.anchorMax = new Vector2(0, 1);
        heartsRect.pivot = new Vector2(0, 1);
        heartsRect.anchoredPosition = new Vector2(0, 0);
        heartsRect.sizeDelta = new Vector2(240, 60);

        Image[] heartImages = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject heartObj = new GameObject($"Heart_{i + 1}");
            heartObj.transform.SetParent(heartsContainer.transform, false);
            RectTransform hRect = heartObj.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0, 0.5f);
            hRect.anchorMax = new Vector2(0, 0.5f);
            hRect.pivot = new Vector2(0, 0.5f);
            hRect.anchoredPosition = new Vector2(i * 68, 0);
            hRect.sizeDelta = new Vector2(56, 52);

            Image img = heartObj.AddComponent<Image>();
            if (heartSprite != null) img.sprite = heartSprite;
            // Color rojo vibrante para asegurar que los corazones resalten
            img.color = new Color(0.95f, 0.18f, 0.22f, 1f);
            heartImages[i] = img;
        }

        // 7. Barra de Agua
        GameObject sliderObj = new GameObject("WaterSlider");
        sliderObj.transform.SetParent(statsPanel.transform, false);
        RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 1);
        sliderRect.anchorMax = new Vector2(0, 1);
        sliderRect.pivot = new Vector2(0, 1);
        sliderRect.anchoredPosition = new Vector2(0, -75);
        sliderRect.sizeDelta = new Vector2(280, 42);

        Slider slider = sliderObj.AddComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;

        // Fondo
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(sliderObj.transform, false);
        RectTransform bgRect = bgObj.AddComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;
        Image bgImg = bgObj.AddComponent<Image>();
        if (barFrameSprite != null) bgImg.sprite = barFrameSprite;
        bgImg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);

        // Área de llenado
        GameObject fillArea = new GameObject("Fill Area");
        fillArea.transform.SetParent(sliderObj.transform, false);
        RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0, 0.12f);
        fillAreaRect.anchorMax = new Vector2(1, 0.88f);
        fillAreaRect.anchoredPosition = new Vector2(0, 0);
        fillAreaRect.sizeDelta = new Vector2(-12, 0);

        // Relleno
        GameObject fillObj = new GameObject("Fill");
        fillObj.transform.SetParent(fillArea.transform, false);
        RectTransform fillRect = fillObj.AddComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        Image fillImg = fillObj.AddComponent<Image>();
        if (barFillSprite != null) fillImg.sprite = barFillSprite;
        fillImg.color = new Color(0.18f, 0.65f, 0.95f, 1f);

        slider.targetGraphic = fillImg;
        slider.fillRect = fillRect;

        // 8. Vincular HUDController
        HUDController hudController = canvasObj.AddComponent<HUDController>();
        SerializedObject so = new SerializedObject(hudController);
        SerializedProperty heartsProp = so.FindProperty("heartImages");
        heartsProp.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            heartsProp.GetArrayElementAtIndex(i).objectReferenceValue = heartImages[i];
        }

        so.FindProperty("waterSlider").objectReferenceValue = slider;
        so.FindProperty("waterFillImage").objectReferenceValue = fillImg;
        so.FindProperty("fullHeartColor").colorValue = new Color(0.95f, 0.18f, 0.22f, 1f);
        so.FindProperty("emptyHeartColor").colorValue = new Color(0.2f, 0.2f, 0.2f, 0.35f);
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green><b>[HUD] ¡Canvas HUD creado con éxito!</b></color> 3 Corazones Rojos y Barra de Agua instalados. Presiona Ctrl+S.");
    }

    [MenuItem("Sombras de la Frontera/2. Configurar Cámara Vertical (Fija en X, Sin Fondo Azul, Límites Norte-Sur)")]
    public static void SetupVerticalCamera()
    {
        // 1. Desactivar cámaras de Cinemachine para evitar conflictos de movimiento
        GameObject cm1 = GameObject.Find("CinemachineCamera");
        if (cm1 != null) cm1.SetActive(false);

        GameObject cm2 = GameObject.Find("CM_FollowPlayer");
        if (cm2 != null) cm2.SetActive(false);

        // 2. Obtener Main Camera
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            mainCam = Object.FindAnyObjectByType<Camera>();
        }

        if (mainCam == null)
        {
            Debug.LogError("[SetupCamera] No se encontró la Main Camera en la escena.");
            return;
        }

        // 3. Quitar el color azul feo de fondo y ponerle color arena del desierto
        mainCam.clearFlags = CameraClearFlags.SolidColor;
        mainCam.backgroundColor = new Color(0.85f, 0.58f, 0.28f, 1f); // #D99447 (Arena cálida)
        mainCam.orthographicSize = 3.6f; // Tamaño óptimo para ver el camino completo y a Mateo claro

        // Desactivar CinemachineBrain si estuviera forzando el movimiento
        Unity.Cinemachine.CinemachineBrain brain = mainCam.GetComponent<Unity.Cinemachine.CinemachineBrain>();
        if (brain != null)
        {
            brain.enabled = false;
        }

        // 4. Agregar o configurar CameraFollowVertical
        CameraFollowVertical follow = mainCam.GetComponent<CameraFollowVertical>();
        if (follow == null)
        {
            follow = mainCam.gameObject.AddComponent<CameraFollowVertical>();
        }

        // Buscar a Mateo
        GameObject mateo = GameObject.Find("mateo_part1_7");
        if (mateo == null) mateo = GameObject.Find("Player");

        SerializedObject so = new SerializedObject(follow);
        if (mateo != null)
        {
            so.FindProperty("target").objectReferenceValue = mateo.transform;
        }
        so.FindProperty("mapBottomY").floatValue = 0.0f;  // Detener exactamente en el fondo del desierto
        so.FindProperty("mapTopY").floatValue = 35.5f;   // Detener exactamente en la orilla del río
        so.FindProperty("smoothSpeed").floatValue = 6f;
        so.FindProperty("verticalOffset").floatValue = 0.8f; // Mateo visible en pantalla con visión hacia el norte
        so.ApplyModifiedProperties();

        // Centrar de inmediato en X=0 y con Y alineado a la base
        float startY = 0.0f + 3.6f; // Borde inferior exactamente en Y=0
        mainCam.transform.position = new Vector3(0f, startY, -10f);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=cyan><b>[Cámara Vertical] ¡Configurada con éxito!</b></color> Cámara fija en X=0, fondo color arena y límites al inicio y al final. Presiona Ctrl+S.");
    }
}
