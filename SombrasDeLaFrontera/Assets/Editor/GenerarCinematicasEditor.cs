using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Generador de las cinemáticas de Intro y Final (guion en HISTORIA_CUTSCENES.md):
/// 1. Configura las viñetas de Assets/Story/Intro y Assets/Story/Final como Sprite (2048, High Quality).
/// 2. Crea/actualiza Assets/Story/Intro.asset y Assets/Story/Final.asset con los pasos y textos.
/// 3. Crea las escenas Intro y Final con Canvas + CutscenePlayer y las agrega a Build Settings.
/// </summary>
public static class GenerarCinematicasEditor
{
    private const string STORY_PATH = "Assets/Story";
    private const string INTRO_ASSET_PATH = "Assets/Story/Intro.asset";
    private const string FINAL_ASSET_PATH = "Assets/Story/Final.asset";
    private const string INTRO_SCENE_PATH = "Assets/Scenes/Intro.unity";
    private const string FINAL_SCENE_PATH = "Assets/Scenes/Final.unity";
    private const string MENU_SCENE_PATH = "Assets/Scenes/01_MainMenu.unity";

    private const string LEVEL1_SCENE_NAME = "02_Level1_Desert";
    private const string MENU_SCENE_NAME = "01_MainMenu";

    [MenuItem("Tools/Story/Generar cinemáticas")]
    public static void GenerarCinematicas()
    {
        if (Resources.Load<TMP_Settings>("TMP Settings") == null)
        {
            Debug.LogError("[Cinemáticas] Primero importa Window > TextMeshPro > Import TMP Essential Resources.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        // 1. Importar viñetas como Sprite
        ConfigurarImagenes(STORY_PATH + "/Intro");
        ConfigurarImagenes(STORY_PATH + "/Final");

        // 2. Assets de datos
        GuardarCinematica(INTRO_ASSET_PATH, PasosIntro(), LEVEL1_SCENE_NAME);
        GuardarCinematica(FINAL_ASSET_PATH, PasosFinal(), MENU_SCENE_NAME);
        AssetDatabase.SaveAssets();

        // 3. Escenas
        ConstruirEscena(INTRO_SCENE_PATH, INTRO_ASSET_PATH);
        ConstruirEscena(FINAL_SCENE_PATH, FINAL_ASSET_PATH);
        ActualizarBuildSettings();

        EditorSceneManager.OpenScene(INTRO_SCENE_PATH);

        Debug.Log("<color=green><b>[Cinemáticas]</b> Intro y Final generadas y agregadas a Build Settings.</color>");
    }

    // =========================================================================
    // GUION (textos exactos de HISTORIA_CUTSCENES.md)
    // =========================================================================

    private static CutsceneData.Paso[] PasosIntro()
    {
        return new[]
        {
            Negro("En un pueblo seco del norte de México, Mateo, de 19 años, cuida a su madre enferma. El campo ya no da para comprar sus medicinas."),
            Imagen("Intro/Intro_01"),
            Negro("Mateo llega a un albergue de la frontera norte, lleno de gente que viene de todas partes. Ahí aparece El Tejón, un coyote con diente de oro y una linterna roja en el cinturón."),
            Imagen("Intro/Intro_02"),
            Negro("Mateo le paga con todo lo que tiene."),
            Imagen("Intro/Intro_03"),
            Negro("Esa noche, Mateo no puede dormir."),
            Imagen("Intro/Intro_04"),
            Negro("Junto a la fogata, escucha a El Tejón hablar por teléfono..."),
            Imagen("Intro/Intro_05"),
            Imagen("Intro/Intro_06"),
            Negro("Mateo se escabulle entre las camionetas mientras un guardia pasa con la linterna. Un perro ladra..."),
            Imagen("Intro/Intro_07"),
            Negro("Sale el sol, y una sola silueta corre entre las dunas. El desierto es ahora su única salida."),
        };
    }

    private static CutsceneData.Paso[] PasosFinal()
    {
        return new[]
        {
            Negro("Ya casi amanece. Mateo lleva tres noches caminando por el desierto del otro lado y ya no tiene agua. La brújula se rompió en la frontera, así que se guía por la Estrella Polar, como le enseñó su padre."),
            Imagen("Final/Final_01"),
            Negro("Sube el último cerro arrastrando los pies y cae de rodillas en la cima. Abajo, a lo lejos, se ven las luces de un pueblo y una carretera que corta el desierto en línea recta."),
            Imagen("Final/Final_02"),
            Negro("Mateo saca la brújula rota y la mira. Piensa en su madre, sola en la casa de adobe."),
            Imagen("Final/Final_03"),
            Negro("Por primera vez en todo el viaje, Mateo sonríe. Baja el cerro y llega a la orilla de la carretera. A lo lejos aparecen dos faros que se acercan despacio."),
            Imagen("Final/Final_04"),
            Negro("Los faros lo iluminan de frente y el vehículo se detiene. Se abre una puerta. Mateo aprieta la brújula en su mano."),
            Negro("Cada año, miles de personas cruzan estas sombras. Todas creen que al final del camino algo va a cambiar.", duracion: 3f, velocidad: 0.5f), // más lento + pausa antes de "FIN"
            Negro("FIN", duracion: 4f, velocidad: 0f, tamano: 160f, fade: 2.5f),   // grande, aparece completo con fade lento
        };
    }

    private static CutsceneData.Paso Negro(string texto, float duracion = 0f, float velocidad = 1f, float tamano = 0f, float fade = 0f)
    {
        return new CutsceneData.Paso
        {
            tipo = CutsceneData.TipoPaso.Negro, texto = texto, duracion = duracion,
            velocidadTexto = velocidad, tamanoTexto = tamano, duracionFade = fade
        };
    }

    private static CutsceneData.Paso Imagen(string rutaRelativa)
    {
        string ruta = STORY_PATH + "/" + rutaRelativa + ".png";
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ruta);
        if (sprite == null)
            Debug.LogWarning($"[Cinemáticas] No se encontró la viñeta '{ruta}'.");

        return new CutsceneData.Paso { tipo = CutsceneData.TipoPaso.Imagen, imagen = sprite };
    }

    // =========================================================================
    // ASSETS
    // =========================================================================

    private static void ConfigurarImagenes(string carpeta)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { carpeta }))
        {
            string ruta = AssetDatabase.GUIDToAssetPath(guid);
            TextureImporter importer = AssetImporter.GetAtPath(ruta) as TextureImporter;
            if (importer == null) continue;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }

    private static void GuardarCinematica(string ruta, CutsceneData.Paso[] pasos, string siguienteEscena)
    {
        // Si ya existe se actualiza en lugar de recrearlo, para conservar su GUID
        CutsceneData data = AssetDatabase.LoadAssetAtPath<CutsceneData>(ruta);
        if (data == null)
        {
            data = ScriptableObject.CreateInstance<CutsceneData>();
            AssetDatabase.CreateAsset(data, ruta);
        }

        data.pasos = pasos;
        data.siguienteEscena = siguienteEscena;
        EditorUtility.SetDirty(data);
    }

    // =========================================================================
    // ESCENAS
    // =========================================================================

    private static void ConstruirEscena(string ruta, string rutaData)
    {
        UnityEngine.SceneManagement.Scene escena;
        if (File.Exists(ruta))
        {
            escena = EditorSceneManager.OpenScene(ruta, OpenSceneMode.Single);
            foreach (GameObject raiz in escena.GetRootGameObjects())
                Object.DestroyImmediate(raiz);
        }
        else
        {
            escena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        // Cargar el asset DESPUÉS de abrir la escena: abrirla en modo Single descarga los assets en memoria
        CutsceneData data = AssetDatabase.LoadAssetAtPath<CutsceneData>(rutaData);

        // Cámara
        GameObject camObj = new GameObject("Main Camera");
        camObj.tag = "MainCamera";
        Camera cam = camObj.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        cam.orthographic = true;
        camObj.AddComponent<AudioListener>();

        // Canvas
        GameObject canvasObj = new GameObject("CutsceneCanvas");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Fondo negro (letterbox)
        GameObject fondo = CreateUIElement("Fondo", canvasObj.transform);
        StretchFull(fondo.GetComponent<RectTransform>());
        AddImage(fondo, Color.black);

        // Contenido que hace fade entre pasos
        GameObject contenidoObj = CreateUIElement("Contenido", canvasObj.transform);
        StretchFull(contenidoObj.GetComponent<RectTransform>());
        CanvasGroup contenido = contenidoObj.AddComponent<CanvasGroup>();
        contenido.alpha = 0f;

        // Viñeta a pantalla completa respetando proporción
        GameObject vinetaObj = CreateUIElement("Vineta", contenidoObj.transform);
        StretchFull(vinetaObj.GetComponent<RectTransform>());
        Image vineta = AddImage(vinetaObj, Color.white);
        vineta.preserveAspect = true;
        vineta.enabled = false;

        // Texto narrado
        TextMeshProUGUI narracion = CreateText("Narracion", contenidoObj.transform, 40, new Vector2(1400f, 700f));
        narracion.color = new Color(0.92f, 0.90f, 0.86f, 1f);
        narracion.alignment = TextAlignmentOptions.Center;

        // Aviso de saltar (abajo a la izquierda)
        GameObject avisoObj = CreateUIElement("AvisoSaltar", canvasObj.transform);
        RectTransform avisoRect = avisoObj.GetComponent<RectTransform>();
        avisoRect.sizeDelta = new Vector2(320f, 50f);
        AnclarEsquina(avisoRect, Vector2.zero, new Vector2(60f, 40f));
        CanvasGroup aviso = avisoObj.AddComponent<CanvasGroup>();
        aviso.alpha = 0.35f;

        TextMeshProUGUI avisoTxt = CreateText("Texto", avisoObj.transform, 20, new Vector2(320f, 30f));
        avisoTxt.text = "Mantén Esc para saltar";
        avisoTxt.alignment = TextAlignmentOptions.Left;
        avisoTxt.rectTransform.anchoredPosition = new Vector2(0f, 8f);

        GameObject barra = CreateUIElement("Barra", avisoObj.transform);
        RectTransform barraRect = barra.GetComponent<RectTransform>();
        barraRect.anchorMin = Vector2.zero;
        barraRect.anchorMax = new Vector2(1f, 0f);
        barraRect.pivot = Vector2.zero; // escala desde la izquierda
        barraRect.sizeDelta = new Vector2(0f, 4f);
        barraRect.anchoredPosition = Vector2.zero;
        barraRect.localScale = new Vector3(0f, 1f, 1f);
        AddImage(barra, new Color(0.95f, 0.75f, 0.25f, 1f));

        // Reproductor
        CutscenePlayer player = canvasObj.AddComponent<CutscenePlayer>();
        SerializedObject so = new SerializedObject(player);
        so.FindProperty("cinematica").objectReferenceValue = data;
        so.FindProperty("contenido").objectReferenceValue = contenido;
        so.FindProperty("vineta").objectReferenceValue = vineta;
        so.FindProperty("narracion").objectReferenceValue = narracion;
        so.FindProperty("avisoSaltar").objectReferenceValue = aviso;
        so.FindProperty("barraSaltar").objectReferenceValue = barraRect;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(escena, ruta);
    }

    private static void ActualizarBuildSettings()
    {
        List<EditorBuildSettingsScene> escenas = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        escenas.RemoveAll(e => e.path == INTRO_SCENE_PATH || e.path == FINAL_SCENE_PATH);

        // Intro justo después del menú principal; Final al final de la lista
        int indiceMenu = escenas.FindIndex(e => e.path == MENU_SCENE_PATH);
        escenas.Insert(indiceMenu >= 0 ? indiceMenu + 1 : escenas.Count, new EditorBuildSettingsScene(INTRO_SCENE_PATH, true));
        escenas.Add(new EditorBuildSettingsScene(FINAL_SCENE_PATH, true));

        EditorBuildSettings.scenes = escenas.ToArray();
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

    private static void AnclarEsquina(RectTransform rect, Vector2 esquina, Vector2 posicion)
    {
        rect.anchorMin = esquina;
        rect.anchorMax = esquina;
        rect.pivot = esquina;
        rect.anchoredPosition = posicion;
    }

    private static Image AddImage(GameObject go, Color color)
    {
        Image img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static TextMeshProUGUI CreateText(string name, Transform parent, float fontSize, Vector2 size)
    {
        GameObject textObj = CreateUIElement(name, parent);
        textObj.GetComponent<RectTransform>().sizeDelta = size;

        TextMeshProUGUI txt = textObj.AddComponent<TextMeshProUGUI>();
        txt.fontSize = fontSize;
        txt.textWrappingMode = TextWrappingModes.Normal;
        txt.raycastTarget = false;
        return txt;
    }
}
