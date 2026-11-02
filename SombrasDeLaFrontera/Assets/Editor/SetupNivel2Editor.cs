using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Nivel 2 "Río Bravo" (Issues: escenario vertical y nado; fauna, corrientes y consumibles en SetupNivel2HazardsEditor).
/// 1. Construye 03_Level2_River a partir del Nivel 1 (HUD, pausa, Victoria/Game Over, cámara vertical y Mateo ya conectados)
///    y quita lo propio del desierto.
/// 2. Coloca el fondo vertical del río (River_Map) con sus límites laterales, la pared norte y el cierre de la orilla sur.
/// 3. Cambia a Mateo de caminar (PlayerController2D) a nadar (SwimController2D) con el Animator de nado.
/// 4. Mueve la meta a la arena de la orilla norte y ajusta textos, cámara y progreso para el Nivel 2.
/// </summary>
public static partial class SetupNivel2Editor
{
    private const string SOURCE_SCENE = "Assets/Scenes/02_Level1_Desert.unity";
    public const string SCENE = "Assets/Scenes/03_Level2_River.unity";
    private const string RIVER_MAP_PREFAB = "Assets/Prefabs/Level2_River/River_Map.prefab";
    private const string SWIM_CONTROLLER = "Assets/Animations/Clips/Visual.controller";
    private const string NEXT_LEVEL_SCENE = "04_Level3_BorderWall";
    private const float WATER_DRAIN_RATE = 1.2f;  // Sed nadando (desierto 1.5, muro de noche 1.0)
    private const string BUBBLE_SPRITE ="Assets/Sprites/Level2_River/Effects/burbuja.png";
    private const string UNLIT_SPRITE_MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // Medidas del mapa del río (100 PPU): 5.72 de ancho, de y = 0 (orilla sur) a y = 34.8 (orilla norte)
    private const float MAP_WIDTH = 5.72f;
    private const float MAP_HALF_W = 2.86f;
    private const float MAP_TOP_Y = 34.8f;

    // Línea de agua medida sobre el dibujo de las orillas
    private const float WATER_START_Y = 1.2f;   // Fin de la arena de la orilla sur
    private const float WATER_END_Y = 33.8f;    // Inicio de la arena de la orilla norte
    private const float SHORE_LOCK_Y = 1.6f;    // Al pasar esta altura nadando, la orilla sur se cierra

    private static readonly Vector2 PlayerSpawn = new Vector2(0f, 0.3f);

    private static readonly string[] KeepRoots =
    {
        "GoalTrigger_RioBravo", "Main Camera", "Global Light 2D", "CameraBounds",
        "PostProcess_Volume", "Player", "EventSystem", "CM_FollowPlayer", "HUD_Canvas", "CinemachineCamera", "AudioManager",
    };

    // =========================================================================
    // MENÚS
    // =========================================================================

    [MenuItem("Sombras de la Frontera/Nivel 2/1. Construir Escena del Río Bravo")]
    public static void BuildLevel2Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildLevel2();
        int problems = VerifyLevel2();
        EditorUtility.DisplayDialog("Nivel 2",
            problems == 0 ? "Escena del Río Bravo construida y verificada sin problemas."
                          : $"Escena construida con {problems} problema(s). Revisa la consola.", "OK");
    }

    [MenuItem("Sombras de la Frontera/Nivel 2/2. Verificar Escena del Río Bravo")]
    public static void VerifyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        int problems = VerifyLevel2();
        EditorUtility.DisplayDialog("Nivel 2", problems == 0 ? "Sin problemas." : $"{problems} problema(s). Revisa la consola.", "OK");
    }

    /// <summary>Uso por línea de comandos (Unity -batchmode -executeMethod SetupNivel2Editor.BuildFromCommandLine).</summary>
    public static void BuildFromCommandLine()
    {
        BuildLevel2();
        int problems = VerifyLevel2();
        Debug.Log($"[Nivel2] Construcción terminada. Problemas: {problems}");
        if (Application.isBatchMode) EditorApplication.Exit(problems == 0 ? 0 : 1);
    }

    /// <summary>
    /// Capturas del río completo con la cámara del juego, de sur a norte (sin guardar cambios en la escena).
    /// Uso: Unity -batchmode -executeMethod SetupNivel2Editor.CaptureFromCommandLine -capturesFolder &lt;carpeta&gt;
    /// </summary>
    public static void CaptureFromCommandLine()
    {
        string[] args = System.Environment.GetCommandLineArgs();
        int i = System.Array.IndexOf(args, "-capturesFolder");
        string folder = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.GetFullPath("Temp/Nivel2_Capturas");
        Directory.CreateDirectory(folder);

        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        if (System.Array.IndexOf(args, "-previewBoats") >= 0) PreviewBoatsForCapture();
        Camera cam = Camera.main;
        if (System.Array.IndexOf(args, "-previewDive") >= 0) PreviewDiveForCapture(cam);
        const int width = 540, height = 960;
        cam.orthographicSize = MAP_WIDTH / 2f / (width / (float)height);
        float half = cam.orthographicSize;
        var rt = new RenderTexture(width, height, 24);
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        cam.targetTexture = rt;

        // Cuadro de calentamiento para que terminen de subir las texturas recién importadas
        cam.transform.position = new Vector3(0f, half, -10f);
        cam.Render();

        int index = 0;
        for (float y = half; ; y += half * 2f - 0.5f)
        {
            float camY = Mathf.Min(y, MAP_TOP_Y - half);
            cam.transform.position = new Vector3(0f, camY, -10f);
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(folder, $"nivel2_{index++}.png"), tex.EncodeToPNG());
            if (camY < y) break;
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        Debug.Log($"[Nivel2] {index} capturas guardadas en {folder}");
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// Solo para las capturas: mete las lanchas al río (empiezan fuera de la pantalla), arma su reflector
    /// (normalmente se crea al dar Play) y enciende el brillo de aviso de la primera.
    /// </summary>
    private static void PreviewBoatsForCapture()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var coneAwake = typeof(VisionCone2D).GetMethod("Awake", flags);
        PatrolBoat[] boats = Object.FindObjectsByType<PatrolBoat>(FindObjectsSortMode.InstanceID);
        for (int i = 0; i < boats.Length; i++)
        {
            PatrolBoat boat = boats[i];
            var so = new SerializedObject(boat);
            bool left = boat.GetComponentInChildren<SpriteRenderer>().flipX;
            boat.transform.position = new Vector3(left ? 0.9f : -0.9f, boat.transform.position.y, 0f);

            var pivot = so.FindProperty("reflectorPivot").objectReferenceValue as Transform;
            Vector2 offset = so.FindProperty("reflectorOffset").vector2Value;
            pivot.localPosition = new Vector3(left ? -offset.x : offset.x, offset.y, 0f);
            pivot.rotation = Quaternion.Euler(0f, 0f, left ? 180f : 0f);

            VisionCone2D cone = boat.GetComponentInChildren<VisionCone2D>();
            coneAwake?.Invoke(cone, null);
            cone.transform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? 35f : -35f);
            Physics2D.SyncTransforms();
            typeof(VisionCone2D).GetMethod("UpdateOcclusion", flags)?.Invoke(cone, null);  // Sombra de las coberturas

            if (i == 0)
            {
                var glow = so.FindProperty("warningGlow").objectReferenceValue as SpriteRenderer;
                glow.enabled = true;
                glow.transform.position = new Vector3(MAP_HALF_W - 0.3f, boat.transform.position.y + 1.6f, 0f);
            }
        }
    }

    /// <summary>
    /// Solo para las capturas: Mateo buceando en el agua (silueta y burbujas) y el HUD dibujado por la cámara
    /// (normalmente es una capa encima de la pantalla) sin el tutorial, con la barra de aire a la mitad y +3 s de carrizo.
    /// </summary>
    private static void PreviewDiveForCapture(Camera cam)
    {
        DiveController2D dive = Object.FindAnyObjectByType<DiveController2D>();
        var so = new SerializedObject(dive);
        var sr = so.FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
        dive.transform.root.position = new Vector3(-0.6f, 2.6f, 0f);
        sr.color = so.FindProperty("diveColor").colorValue;
        sr.sortingLayerName = so.FindProperty("underwaterSortingLayer").stringValue;
        var bubbles = so.FindProperty("bubbles").objectReferenceValue as ParticleSystem;
        bubbles.Simulate(1.2f, true, true);

        GameObject hud = FindRoot("HUD_Canvas");
        Canvas canvas = hud.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = cam;
        canvas.planeDistance = 1f;
        foreach (Transform t in hud.GetComponentsInChildren<Transform>(true))
            if (t.name == "TutorialPanel") t.gameObject.SetActive(false);

        AirBarUI air = Object.FindAnyObjectByType<AirBarUI>();
        var airSo = new SerializedObject(air);
        (airSo.FindProperty("slider").objectReferenceValue as Slider).value = 0.5f;
        (airSo.FindProperty("label").objectReferenceValue as Text).text = "AIRE  +3s";
        Canvas.ForceUpdateCanvases();
    }

    // =========================================================================
    // CONSTRUCCIÓN
    // =========================================================================

    public static void BuildLevel2()
    {
        AssetDatabase.Refresh();
        SetupRiverAudio();   // Mezclador, efectos y música del río en el AudioManager persistente
        FixPlayerAnimations();   // Cuadros de nado sin recortes ni brincos y ciclo de caminar completo

        if (File.Exists(SCENE))
        {
            // Se sobrescribe el contenido y se conserva el .meta: la escena mantiene su GUID (Build Settings sigue igual)
            File.Copy(SOURCE_SCENE, SCENE, true);
            AssetDatabase.ImportAsset(SCENE, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            AssetDatabase.CopyAsset(SOURCE_SCENE, SCENE);
        }
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);

        RemoveDesertContent(scene);
        SetupRiverMap();
        SetupGoal();
        SetupPlayer();
        SetupCamera();
        SetupHudAndTexts();
        PlaceHazardsAndPickups();
        PlaceBoatsAndCovers();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RegisterInBuildSettings();
        Debug.Log("<color=green><b>[Nivel2]</b> Escena 03_Level2_River construida.</color>");
    }

    private static void RemoveDesertContent(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (KeepRoots.Contains(root.name.Trim())) continue;
            Object.DestroyImmediate(root);
        }
    }

    private static void SetupRiverMap()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RIVER_MAP_PREFAB);
        var map = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        map.name = "River_Map";
        map.transform.SetAsFirstSibling();
        map.transform.position = Vector3.zero;

        Transform bounds = map.transform.Find("MapBounds");

        // Pared norte: Mateo no sale por arriba del mapa
        var north = new GameObject("Wall_North");
        north.transform.SetParent(bounds, false);
        north.transform.localPosition = new Vector3(0f, MAP_TOP_Y + 0.1f, 0f);
        north.AddComponent<BoxCollider2D>().size = new Vector2(MAP_WIDTH + 0.4f, 0.2f);

        // Cierre de la orilla sur: se activa en cuanto Mateo se adentra en el río
        var shoreLock = new GameObject("ShoreLock_Sur");
        shoreLock.transform.SetParent(bounds, false);
        shoreLock.transform.localPosition = new Vector3(0f, WATER_START_Y - 0.15f, 0f);
        shoreLock.AddComponent<BoxCollider2D>().size = new Vector2(MAP_WIDTH + 0.4f, 0.2f);
        var lockSo = new SerializedObject(shoreLock.AddComponent<ShoreLock2D>());
        lockSo.FindProperty("lockAboveY").floatValue = SHORE_LOCK_Y;
        lockSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupGoal()
    {
        // Meta: pisar la arena de la orilla norte
        GameObject goal = FindRoot("GoalTrigger_RioBravo");
        goal.name = "GoalTrigger_OrillaNorte";
        float height = MAP_TOP_Y - WATER_END_Y;
        goal.transform.position = new Vector3(0f, WATER_END_Y + height * 0.5f, 0f);

        BoxCollider2D box = goal.GetComponent<BoxCollider2D>();
        box.offset = Vector2.zero;
        box.size = new Vector2(MAP_WIDTH, height);
        box.isTrigger = true;

        var goalSo = new SerializedObject(goal.GetComponent<GoalTrigger>());
        goalSo.FindProperty("nextLevelIndex").intValue = 3;
        goalSo.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupPlayer()
    {
        PlayerHealth health = Object.FindAnyObjectByType<PlayerHealth>();
        GameObject mateo = health.gameObject;
        mateo.transform.root.position = PlayerSpawn;

        // Nadar cansa: la sed sigue (un poco más lenta que en el desierto) y se cambia el control de caminar por el de nado
        WaterSystem water = mateo.GetComponent<WaterSystem>();
        if (water != null)
        {
            var waterSo = new SerializedObject(water);
            waterSo.FindProperty("waterDrainRate").floatValue = WATER_DRAIN_RATE;
            waterSo.ApplyModifiedPropertiesWithoutUndo();
        }
        PlayerController2D walker = mateo.GetComponent<PlayerController2D>();
        if (walker != null) Object.DestroyImmediate(walker);

        Rigidbody2D rb = mateo.GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.linearDamping = 0f;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;

        SpriteRenderer sr = mateo.GetComponent<SpriteRenderer>();
        Animator animator = mateo.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(SWIM_CONTROLLER);

        SwimController2D swim = mateo.GetComponent<SwimController2D>();
        if (swim == null) swim = mateo.AddComponent<SwimController2D>();
        var so = new SerializedObject(swim);
        so.FindProperty("rb").objectReferenceValue = rb;
        so.FindProperty("spriteRenderer").objectReferenceValue = sr;
        so.FindProperty("animator").objectReferenceValue = animator;
        so.FindProperty("waterStartY").floatValue = WATER_START_Y;
        so.FindProperty("waterEndY").floatValue = WATER_END_Y;
        so.ApplyModifiedPropertiesWithoutUndo();

        // La sombra de la arena y el polvo no tienen sentido flotando en el agua
        foreach (BlobShadow shadow in mateo.GetComponentsInChildren<BlobShadow>(true)) shadow.gameObject.SetActive(false);
        foreach (ParticleSystem dust in mateo.GetComponentsInChildren<ParticleSystem>(true)) dust.gameObject.SetActive(false);

        // Buceo: mantener Espacio para sumergirse (4 s de aire), con burbujas mientras está bajo el agua
        DiveController2D dive = mateo.GetComponent<DiveController2D>();
        if (dive == null) dive = mateo.AddComponent<DiveController2D>();
        var diveSo = new SerializedObject(dive);
        diveSo.FindProperty("maxAirSeconds").floatValue = 4f;
        diveSo.FindProperty("diveSpeedMultiplier").floatValue = 0.6f;
        diveSo.FindProperty("spriteRenderer").objectReferenceValue = sr;
        diveSo.FindProperty("bubbles").objectReferenceValue = BuildBubbles(mateo.transform);
        diveSo.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>Burbujas que suben y se desvanecen alrededor de Mateo mientras bucea.</summary>
    private static ParticleSystem BuildBubbles(Transform mateo)
    {
        Transform old = mateo.Find("Burbujas");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var go = new GameObject("Burbujas");
        go.transform.SetParent(mateo, false);
        // Mateo está escalado (0.3): las burbujas se miden en unidades de mundo
        go.transform.localScale = new Vector3(1f / mateo.lossyScale.x, 1f / mateo.lossyScale.y, 1f);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
        main.startColor = new Color(0.85f, 0.96f, 1f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.maxParticles = 40;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 14f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.14f;

        // Suben hacia arriba de la pantalla y se desvanecen creciendo un poco
        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.25f, 0.4f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                     new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
        color.color = fade;

        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.3f));

        ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
        sheet.enabled = true;
        sheet.mode = ParticleSystemAnimationMode.Sprites;
        sheet.AddSprite(BuildBubbleSprite());

        ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UNLIT_SPRITE_MATERIAL);
        renderer.sortingLayerName = "Background";  // Bajo el agua, sobre la silueta de Mateo
        renderer.sortingOrder = 6000;
        return ps;
    }

    /// <summary>Genera (una sola vez) el sprite de una burbuja: anillo claro con un brillo.</summary>
    private static Sprite BuildBubbleSprite()
    {
        if (!File.Exists(BUBBLE_SPRITE))
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Vector2 c = new Vector2(size / 2f - 0.5f, size / 2f - 0.5f);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / (size / 2f);
                    float ring = Mathf.Clamp01(1f - Mathf.Abs(d - 0.8f) / 0.18f);           // Borde de la burbuja
                    float inside = d < 0.8f ? 0.18f : 0f;                                     // Relleno muy tenue
                    float shine = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(x, y), c + new Vector2(-5f, 5f)) / 4f);
                    float a = Mathf.Clamp01(Mathf.Max(ring, inside) + shine);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            File.WriteAllBytes(BUBBLE_SPRITE, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(BUBBLE_SPRITE, ImportAssetOptions.ForceUpdate);

            var importer = (TextureImporter)AssetImporter.GetAtPath(BUBBLE_SPRITE);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 320f;   // 0.1 unidades
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(BUBBLE_SPRITE);
    }

    private static void SetupCamera()
    {
        CameraFollowVertical follow = Object.FindAnyObjectByType<CameraFollowVertical>();
        var so = new SerializedObject(follow);
        so.FindProperty("target").objectReferenceValue = Object.FindAnyObjectByType<SwimController2D>().transform;
        so.FindProperty("mapBottomY").floatValue = 0f;
        so.FindProperty("mapTopY").floatValue = MAP_TOP_Y;
        so.FindProperty("mapWidth").floatValue = MAP_WIDTH;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupHudAndTexts()
    {
        GameObject hud = FindRoot("HUD_Canvas");

        // Barra de agua (sed) como en el Nivel 1 y, debajo, una copia que funciona como medidor de aire del buceo
        Transform waterSlider = hud.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "WaterSlider");
        if (waterSlider != null)
        {
            waterSlider.gameObject.SetActive(true);
            AddBarLabel(hud, waterSlider, "AGUA", new Color(0.85f, 0.95f, 1f, 1f));
            Transform airSlider = Object.Instantiate(waterSlider.gameObject, waterSlider.parent).transform;
            airSlider.SetSiblingIndex(waterSlider.GetSiblingIndex() + 1);
            var waterRect = (RectTransform)waterSlider;
            var airRect = (RectTransform)airSlider;
            airRect.anchoredPosition = waterRect.anchoredPosition - new Vector2(0f, waterRect.sizeDelta.y + 6f);

            // El panel de estado crece para que quepa la barra nueva
            var panel = waterSlider.parent as RectTransform;
            if (panel != null)
            {
                float needed = -(airRect.anchoredPosition.y - airRect.sizeDelta.y) + 4f;
                if (panel.sizeDelta.y < needed) panel.sizeDelta = new Vector2(panel.sizeDelta.x, needed);
            }
            SetupAirBar(hud, airSlider);
        }

        foreach (Text text in hud.GetComponentsInChildren<Text>(true))
        {
            if (text.text.Contains("El sol abrasa"))
            {
                text.text =
                    "Llegaste al Río Bravo. La otra orilla está cerca, pero la corriente no perdona...\n\n" +
                    "🕹️  <b>Controles:</b> Usa <b>W, A, S, D</b> o las <b>Flechas</b> para nadar en 8 direcciones.\n\n" +
                    "🌊 <b>Nado:</b> En el agua Mateo acelera poco a poco y sigue deslizándose al soltar los controles; calcula tus movimientos.\n\n" +
                    "⚠️ <b>Cuidado:</b> Una vez que te metes al río ya no puedes volver al desierto. Pejelagartos, cocodrilos y serpientes de agua te quitan 1 corazón si te tocan.\n\n" +
                    "🌀 <b>Corrientes:</b> El agua con flechas te arrastra hacia un lado y los rápidos te frenan. Recoge <b>bidones</b> (más velocidad), <b>bolsas impermeables</b> (+1 corazón) y <b>carrizos</b> (aire extra).\n\n" +
                    "🚤 <b>Lanchas:</b> La Patrulla Fronteriza barre el río con reflectores. Cuando veas un brillo en la orilla, pégate a un <b>tronco</b>, unos <b>juncos</b> o unas <b>ramas</b> <i>del lado contrario a la luz</i>: la sombra te oculta. Del lado equivocado te descubren.\n\n" +
                    "💧 <b>Agua:</b> Nadar cansa y la barra de <b>AGUA</b> baja poco a poco. Recoge <b>botellas</b> y <b>cantimploras</b> que flotan en el río.\n\n" +
                    "🤿 <b>Buceo:</b> Mantén <b>ESPACIO</b> para sumergirte: los reflectores no te ven, pero nadas más lento y solo tienes <b>4 segundos de aire</b>. Suelta para respirar antes de que la barra de aire se vacíe.\n\n" +
                    "🚧 <b>Alambrados:</b> Las púas cruzan el río y te quitan 1 corazón si las tocas; busca el hueco. Los animales te detectan si pasas cerca, aunque sea por detrás.\n\n" +
                    "🎯 <b>Objetivo:</b> Cruza el río hacia el norte y pisa la <b>arena de la orilla norte</b>.";
            }
            else if (text.text.Contains("TRAMO 1")) text.text = "SOMBRAS DE LA FRONTERA — TRAMO 2";
            else if (text.text.Contains("NIVEL 1 COMPLETADO")) text.text = "¡NIVEL 2 COMPLETADO!";
            else if (text.text.Contains("orilla del R")) text.text = "¡Cruzaste el Río Bravo!\n\nProgreso guardado: Nivel 3 Desbloqueado en disco.";
            EditorUtility.SetDirty(text);
        }

        EndGameModalController modal = hud.GetComponent<EndGameModalController>();
        if (modal != null)
        {
            var so = new SerializedObject(modal);
            so.FindProperty("nextLevelSceneName").stringValue = NEXT_LEVEL_SCENE;
            so.FindProperty("defeatGeneric.description").stringValue =
                "Mateo ha perdido sus 3 corazones ante los peligros del Río Bravo.\n\n🛡️ <b>Consejo:</b> Protege tu salud y administra bien tus movimientos.";
            so.FindProperty("defeatByDehydration.title").stringValue = "¡AGOTADO EN EL RÍO!";
            so.FindProperty("defeatByDehydration.description").stringValue =
                "Nadar contra la corriente te dejó sin fuerzas y sin agua para seguir.\n\n💧 <b>Consejo:</b> Recoge las botellas y cantimploras que flotan en el río para mantener tu barra de agua llena.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    /// <summary>Barra de aire: copia de la barra de agua con su etiqueta "AIRE" y el color del aire (AirBarUI la actualiza).</summary>
    private static void SetupAirBar(GameObject hud, Transform bar)
    {
        bar.gameObject.SetActive(true);
        bar.name = "AirSlider";
        Slider slider = bar.GetComponent<Slider>();
        Image fill = bar.GetComponentsInChildren<Image>(true).FirstOrDefault(i => i.name == "Fill");
        Text label = AddBarLabel(hud, bar, "AIRE", new Color(0.85f, 0.97f, 1f, 1f));

        AirBarUI air = bar.GetComponent<AirBarUI>();
        if (air == null) air = bar.gameObject.AddComponent<AirBarUI>();
        var so = new SerializedObject(air);
        so.FindProperty("slider").objectReferenceValue = slider;
        so.FindProperty("fillImage").objectReferenceValue = fill;
        so.FindProperty("label").objectReferenceValue = label;
        so.ApplyModifiedPropertiesWithoutUndo();
        if (slider != null) slider.value = 1f;
        if (fill != null) fill.color = new Color(0.7f, 0.93f, 1f, 1f);
    }

    /// <summary>Etiqueta dentro de una barra del HUD (encima quedan los corazones). Reemplaza la que ya tuviera.</summary>
    private static Text AddBarLabel(GameObject hud, Transform bar, string text, Color color)
    {
        Transform oldLabel = bar.Find("BarLabel");
        if (oldLabel != null) Object.DestroyImmediate(oldLabel.gameObject);
        var labelGo = new GameObject("BarLabel", typeof(RectTransform));
        labelGo.transform.SetParent(bar, false);
        var rt = (RectTransform)labelGo.transform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(6f, 0f);
        rt.sizeDelta = new Vector2(-24f, 0f);
        Text label = labelGo.AddComponent<Text>();
        Text reference = hud.GetComponentsInChildren<Text>(true).FirstOrDefault(t => t.font != null);
        if (reference != null) label.font = reference.font;
        label.text = text;
        label.fontSize = 16;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleLeft;
        label.color = color;
        label.raycastTarget = false;
        labelGo.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.8f);
        return label;
    }

    private static GameObject FindRoot(string name) =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name.Trim() == name);

    private static void RegisterInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == SCENE)) return;
        scenes.Add(new EditorBuildSettingsScene(SCENE, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // =========================================================================
    // VERIFICACIÓN (nado, límites, orilla sur cerrada y meta en la orilla norte)
    // =========================================================================

    public static int VerifyLevel2()
    {
        Physics2D.SyncTransforms();
        int problems = 0;

        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel2][OK] {message}");
            else { Debug.LogWarning($"[Nivel2][FALLA] {message}"); problems++; }
        }

        SwimController2D swim = Object.FindAnyObjectByType<SwimController2D>();
        Expect(swim != null, "Mateo tiene SwimController2D");
        Expect(Object.FindAnyObjectByType<PlayerController2D>() == null, "No queda PlayerController2D (caminar) en la escena");
        WaterSystem thirst = swim.GetComponent<WaterSystem>();
        Expect(thirst != null && Mathf.Approximately(new SerializedObject(thirst).FindProperty("waterDrainRate").floatValue, WATER_DRAIN_RATE),
            $"Mateo se cansa nadando: la barra de agua baja {WATER_DRAIN_RATE}% por segundo");
        if (swim == null) return problems;

        Animator animator = swim.GetComponent<Animator>();
        Expect(animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.name == "Visual",
            "Mateo usa el Animator de nado (Swim_Up, Swim_Side, Swim_Down, Idle_Float)");
        Expect(swim.GetComponent<PlayerHealth>() != null, "Mateo conserva sus 3 corazones (PlayerHealth)");

        // Buceo: 4 s de aire, 60% de velocidad, burbujas y medidor de aire en el HUD
        DiveController2D dive = swim.GetComponent<DiveController2D>();
        Expect(dive != null, "Mateo puede bucear (DiveController2D)");
        if (dive != null)
        {
            var diveSo = new SerializedObject(dive);
            Expect(Mathf.Approximately(diveSo.FindProperty("maxAirSeconds").floatValue, 4f) &&
                   Mathf.Approximately(diveSo.FindProperty("diveSpeedMultiplier").floatValue, 0.6f),
                "El buceo dura hasta 4 s de aire y reduce la velocidad al 60%");
            var bubbles = diveSo.FindProperty("bubbles").objectReferenceValue as ParticleSystem;
            Expect(bubbles != null && bubbles.textureSheetAnimation.spriteCount > 0 &&
                   bubbles.GetComponent<ParticleSystemRenderer>().sharedMaterial != null, "Mateo suelta burbujas al bucear");
            Expect(diveSo.FindProperty("spriteRenderer").objectReferenceValue != null, "La silueta de Mateo se oscurece bajo el agua");
        }
        AirBarUI airBar = Object.FindAnyObjectByType<AirBarUI>(FindObjectsInactive.Include);
        Expect(airBar != null && airBar.gameObject.activeInHierarchy && new SerializedObject(airBar).FindProperty("slider").objectReferenceValue != null,
            "El HUD muestra la barra de aire");

        Vector2 feet = swim.GetFeetPosition();
        Expect(feet.y > 0f && feet.y < WATER_START_Y, $"Mateo inicia en la arena de la orilla sur (pies en y={feet.y:F2})");

        // Límites laterales y norte: sólidos que no son triggers
        bool SolidAt(Vector2 p) => Physics2D.OverlapPointAll(p).Any(c => !c.isTrigger && c.enabled && c.attachedRigidbody == null);
        Expect(SolidAt(new Vector2(-MAP_HALF_W - 0.1f, 17f)) && SolidAt(new Vector2(MAP_HALF_W + 0.1f, 17f)), "Paredes laterales en los bordes del río (x = ±2.96)");
        Expect(!SolidAt(new Vector2(-MAP_HALF_W + 0.2f, 17f)) && !SolidAt(new Vector2(MAP_HALF_W - 0.2f, 17f)), "El río es nadable de orilla a orilla");
        Expect(SolidAt(new Vector2(0f, MAP_TOP_Y + 0.1f)), "Pared norte en el borde superior del mapa");
        Expect(SolidAt(new Vector2(0f, -0.1f)), "Pared sur en el borde inferior del mapa");

        ShoreLock2D shoreLock = Object.FindAnyObjectByType<ShoreLock2D>();
        Expect(shoreLock != null, "Existe el cierre de la orilla sur (ShoreLock2D)");
        if (shoreLock != null)
        {
            BoxCollider2D lockBox = shoreLock.GetComponent<BoxCollider2D>();
            Expect(!lockBox.isTrigger && lockBox.bounds.max.y <= WATER_START_Y && lockBox.bounds.size.x >= MAP_WIDTH,
                "El cierre sur bloquea todo el ancho justo debajo de la línea de agua");
        }

        // Meta en la arena norte
        GoalTrigger goal = Object.FindAnyObjectByType<GoalTrigger>();
        Expect(goal != null, "Existe la meta (GoalTrigger)");
        if (goal != null)
        {
            Bounds g = goal.GetComponent<BoxCollider2D>().bounds;
            Expect(goal.GetComponent<BoxCollider2D>().isTrigger && Mathf.Abs(g.min.y - WATER_END_Y) < 0.05f && g.size.x >= MAP_WIDTH - 0.01f,
                $"La meta cubre la arena de la orilla norte (y {g.min.y:F2} a {g.max.y:F2})");
            var goalSo = new SerializedObject(goal);
            Expect(goalSo.FindProperty("nextLevelIndex").intValue == 3, "La victoria desbloquea el Nivel 3");
        }

        CameraFollowVertical follow = Object.FindAnyObjectByType<CameraFollowVertical>();
        Expect(follow != null && follow.enabled, "La cámara usa CameraFollowVertical del Nivel 1");
        if (follow != null)
        {
            var camSo = new SerializedObject(follow);
            Expect(Mathf.Approximately(camSo.FindProperty("mapTopY").floatValue, MAP_TOP_Y) &&
                   camSo.FindProperty("target").objectReferenceValue == swim.transform, "La cámara sigue a Mateo hasta la orilla norte");
        }

        EndGameModalController modal = Object.FindAnyObjectByType<EndGameModalController>();
        Expect(modal != null && new SerializedObject(modal).FindProperty("nextLevelSceneName").stringValue == NEXT_LEVEL_SCENE,
            "El botón de siguiente nivel lleva al Nivel 3");

        return problems + VerifyHazards() + VerifyAudio() + VerifyPlayerAnimations();
    }
}
