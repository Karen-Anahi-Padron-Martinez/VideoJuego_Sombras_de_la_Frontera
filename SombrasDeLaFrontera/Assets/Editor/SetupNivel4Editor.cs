using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Nivel 4 "El Refugio Seguro" (Issue 1: escena, mapa del desierto nocturno, iluminación y obstáculos).
/// 1. Importa el mapa continuo (Assets/Sprites/Level4_SafeHaven, unido desde Docs/scenery_level4) y usa los obstáculos
///    del desierto de las hojas del Nivel 1 (cactus, rocas, autos oxidados, combi, costales, cercas...).
/// 2. Crea los prefabs de obstáculos con colisión en la base y cobertura solo en los que tapan a Mateo.
/// 3. Construye 05_Level4_SafeHaven a partir del Nivel 1 (HUD, pausa, Victoria/Game Over, cámara y Mateo ya conectados),
///    quita lo propio del Nivel 1 y coloca el mapa, la cerca rota del inicio, las rocas pintadas de las orillas,
///    el campamento del refugio (meta), los obstáculos y la iluminación de noche.
/// 4. La registra en Build Settings y pone la miniatura en la tarjeta del menú.
/// </summary>
public static class SetupNivel4Editor
{
    private const string SOURCE_SCENE = "Assets/Scenes/02_Level1_Desert.unity";
    public const string SCENE = "Assets/Scenes/05_Level4_SafeHaven.unity";
    private const string MENU_SCENE = "Assets/Scenes/01_MainMenu.unity";
    private const string MAP_SPRITE = "Assets/Sprites/Level4_SafeHaven/mapa_completo_nivel4_seamless.png";
    private const string PREFAB_DIR = "Assets/Prefabs/Level4_SafeHaven";
    private const string PROFILE_PATH = "Assets/Settings/Level4_PostProcess.asset";
    private const string THUMBNAIL = "Assets/Sprites/UI/Thumbnail_Level4.jpg";
    private const string GLOW_SPRITE = "Assets/Sprites/Effects/LightGlow.png";
    private const string SHEET_1 = "Assets/Sprites/Environment/obstacle_1.png";
    private const string SHEET_2 = "Assets/Sprites/Environment/obstacle_2.png";
    private const string UNLIT_MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";
    private const string MAIN_MENU_SCENE = "01_MainMenu";

    // Mapa de 572 x 3556 px a 100 px por unidad, centrado en y = 17.71 (igual que los niveles 1 y 3)
    private const float MAP_TOP_Y = 35.49f;
    private const float MAP_HALF_W = 2.86f;

    // El mapa se unió con 4 partes de 572 px de ancho (de abajo hacia arriba). Fila del mapa donde empieza cada parte
    // (las partes 2 a 4 se recortaron 60 px abajo y se mezclan 121 px con la siguiente).
    private static readonly int[] PartRowOffset = { 2532, 1688, 844, 0 };

    // Inicio (frente a la cerca rota) y meta (al llegar a la fogata del campamento)
    private static readonly Vector2 SpawnPos = new Vector2(0.05f, 2.25f);
    private const float FENCE_TOP_Y = 1.72f;
    private static readonly Vector2 GoalPos = new Vector2(0.12f, 33.45f);
    private static readonly Vector2 GoalSize = new Vector2(1.7f, 0.5f);
    private static readonly Vector2 CampfirePos = new Vector2(0.11f, 33.72f);
    private static readonly Color FireColor = new Color(1f, 0.62f, 0.3f, 1f);

    /// <summary>Convierte píxeles de una parte del mapa (x, fila desde arriba) a coordenadas del mundo.</summary>
    private static Vector2 PartToWorld(int part, float px, float row) =>
        new Vector2(px / 100f - MAP_HALF_W, MAP_TOP_Y - (PartRowOffset[part - 1] + row) / 100f);

    // Rocas y dunas pintadas en las orillas: las 4 partes comparten el mismo dibujo en los bordes (x0, x1, fila0, fila1)
    private static readonly (float x0, float x1, float r0, float r1)[] EdgeRocks =
    {
        (0f, 110f, 0f, 80f),      // Rocas de arriba a la izquierda
        (0f, 90f, 210f, 400f),    // Peñasco grande de la izquierda
        (0f, 50f, 510f, 690f),    // Rocas bajas de la izquierda
        (535f, 572f, 80f, 650f),  // Duna de la derecha
        (495f, 572f, 620f, 800f), // Rocas bajas de la derecha
    };

    // Rodadoras pintadas en el mapa (parte, x, fila de la base, ancho en px)
    private static readonly (int part, float x, float row, float w)[] PaintedTumbleweeds =
    {
        (2, 215f, 570f, 60f), (2, 120f, 895f, 56f), (2, 450f, 895f, 56f),
        (3, 215f, 570f, 60f), (3, 120f, 895f, 56f), (3, 450f, 895f, 56f),
        (4, 120f, 895f, 56f), (4, 450f, 895f, 56f),
    };

    // Carriles horizontales que se dejan libres para las patrullas del Issue 2
    public static readonly float[] PatrolLanes = { 4.6f, 8.4f, 11.4f, 15.6f, 19.6f, 24.0f, 28.4f, 31.8f };
    private const float LANE_HALF = 0.3f;

    private class ObstacleDef
    {
        public string name; public int sheet, slice; public float scale;
        public float footW, footH;              // Collider sólido en la base (fracción del sprite)
        public bool isCover; public float coverW, coverH;
        public ObstacleDef(string name, int sheet, int slice, float scale, float footW, float footH, bool isCover, float coverW = 0f, float coverH = 0f)
        {
            this.name = name; this.sheet = sheet; this.slice = slice; this.scale = scale; this.footW = footW; this.footH = footH;
            this.isCover = isCover; this.coverW = coverW; this.coverH = coverH;
        }
    }

    // Obstáculos del desierto (hojas del Nivel 1, mismo tamaño que allá; Mateo mide ~0.6 unidades).
    // Solo estorban (no ocultan) los que se ven a través: árbol seco, yuca, ocotillo, matorral de ramas, rodadora,
    // cercas y el barril tirado (demasiado bajo).
    private static readonly ObstacleDef[] Defs =
    {
        new ObstacleDef("cactus",            1, 0,  1f, 0.30f, 0.14f, true,  0.45f, 0.90f),
        new ObstacleDef("nopalera",          1, 1,  1f, 0.70f, 0.22f, true,  0.85f, 0.85f),
        new ObstacleDef("matorral_ramas",    1, 2,  1f, 0.40f, 0.15f, false),
        new ObstacleDef("arbol_seco",        1, 3,  1f, 0.18f, 0.10f, false),
        new ObstacleDef("roca_pequena",      1, 4,  1f, 0.85f, 0.30f, true,  0.90f, 0.90f),
        new ObstacleDef("roca_grande",       1, 5,  1f, 0.85f, 0.30f, true,  0.90f, 0.90f),
        new ObstacleDef("rodadora",          1, 6,  1f, 0.50f, 0.20f, false),
        new ObstacleDef("arbusto",           1, 7,  1f, 0.60f, 0.25f, true,  0.85f, 0.85f),
        new ObstacleDef("camioneta_oxidada", 1, 8,  1f, 0.90f, 0.45f, true,  0.92f, 0.80f),
        new ObstacleDef("carro_oxidado",     1, 9,  1f, 0.90f, 0.45f, true,  0.92f, 0.80f),
        new ObstacleDef("barda_rota_a",      1, 10, 1f, 0.95f, 0.12f, false),
        new ObstacleDef("barda_rota_b",      1, 11, 1f, 0.95f, 0.12f, false),
        new ObstacleDef("yuca",              2, 0,  1f, 0.22f, 0.10f, false),
        new ObstacleDef("ocotillo",          2, 1,  1f, 0.35f, 0.10f, false),
        new ObstacleDef("agave",             2, 2,  1f, 0.60f, 0.18f, true,  0.75f, 0.60f),
        new ObstacleDef("cactus_organo",     2, 3,  1f, 0.55f, 0.14f, true,  0.70f, 0.90f),
        new ObstacleDef("craneo_tronco",     2, 4,  1f, 0.85f, 0.32f, true,  0.85f, 0.90f),
        new ObstacleDef("formacion_rocosa",  2, 5,  1f, 0.90f, 0.38f, true,  0.92f, 0.90f),
        new ObstacleDef("barril_tirado",     2, 6,  1f, 0.75f, 0.35f, false),
        new ObstacleDef("arbusto_espinoso",  2, 7,  1f, 0.80f, 0.30f, true,  0.85f, 0.85f),
        new ObstacleDef("combi_oxidada",     2, 8,  1f, 0.92f, 0.40f, true,  0.92f, 0.80f),
        new ObstacleDef("tanque_oxidado",    2, 9,  1f, 0.92f, 0.38f, true,  0.92f, 0.80f),
        new ObstacleDef("costales",          2, 10, 1f, 0.92f, 0.40f, true,  0.92f, 0.90f),
        new ObstacleDef("cerca_alambre",     2, 11, 1f, 1.00f, 0.12f, false),
    };

    // (prefab, x, base y, voltear). Las bases quedan fuera de los carriles de patrulla y lejos de lo pintado en el mapa.
    private static readonly (string name, float x, float baseY, bool flip)[] Layout =
    {
        // Tramo 1: salida de la cerca
        ("roca_grande", -1.5f, 2.7f, false),
        ("arbusto", 1.4f, 2.9f, true),
        ("cactus", -0.8f, 3.6f, false),
        ("nopalera", 0.9f, 3.9f, false),
        // Tramo 2
        ("costales", -1.2f, 5.2f, false),
        ("barril_tirado", 0.3f, 5.4f, false),
        ("carro_oxidado", 1.5f, 5.6f, true),
        ("rodadora", -0.3f, 6.6f, false),
        ("agave", -1.35f, 7.1f, false),
        ("roca_pequena", 0.6f, 7.0f, false),
        ("cactus_organo", 2.0f, 7.3f, false),
        ("arbusto_espinoso", -0.4f, 7.7f, true),
        // Tramo 3
        ("formacion_rocosa", -0.2f, 9.0f, false),
        ("cactus", 1.0f, 10.3f, true),
        ("craneo_tronco", -1.0f, 10.6f, false),
        ("arbusto", 0.45f, 10.95f, false),
        // Tramo 4
        ("camioneta_oxidada", 1.2f, 12.0f, false),
        ("roca_grande", -1.6f, 12.2f, true),
        ("nopalera", 0.25f, 13.3f, true),
        ("barda_rota_a", -1.4f, 14.0f, false),
        ("arbusto_espinoso", 1.6f, 14.4f, false),
        ("cactus_organo", -0.2f, 14.8f, true),
        // Tramo 5
        ("combi_oxidada", -0.9f, 16.2f, false),
        ("roca_pequena", 1.2f, 16.4f, true),
        ("ocotillo", 0.5f, 17.4f, false),
        ("agave", -1.5f, 17.6f, true),
        ("arbusto", 0.1f, 18.9f, true),
        ("barril_tirado", 1.0f, 18.95f, true),
        // Tramo 6
        ("tanque_oxidado", 0.8f, 20.4f, false),
        ("cactus", -1.4f, 20.6f, false),
        ("nopalera", 1.5f, 22.0f, false),
        ("formacion_rocosa", -0.3f, 22.3f, true),
        ("arbusto_espinoso", 0.95f, 23.4f, false),
        ("yuca", -1.7f, 23.1f, false),
        // Tramo 7
        ("carro_oxidado", -0.8f, 24.8f, false),
        ("costales", 1.2f, 25.0f, false),
        ("cactus_organo", 0.3f, 26.2f, false),
        ("roca_grande", -0.9f, 27.3f, false),
        ("arbol_seco", 1.3f, 27.4f, true),
        ("cerca_alambre", 0.4f, 27.7f, false),
        // Tramo 8
        ("arbusto", -1.2f, 29.0f, false),
        ("camioneta_oxidada", 0.9f, 29.2f, true),
        ("rodadora", -0.2f, 30.2f, true),
        ("agave", -1.5f, 30.8f, false),
        ("roca_pequena", 1.6f, 30.9f, false),
        ("cactus", 0.2f, 31.0f, false),
        // Llegada al campamento
        ("arbusto_espinoso", -1.4f, 32.5f, false),
        ("matorral_ramas", 1.9f, 32.6f, true),
    };

    // Objetos del Nivel 1 que se conservan (todo lo demás se quita)
    private static readonly string[] KeepRoots =
    {
        "DesertMap", "GoalTrigger_RioBravo", "Main Camera", "Global Light 2D", "CameraBounds", "LevelBounds",
        "PostProcess_Volume", "Player", "EventSystem", "CM_FollowPlayer", "HUD_Canvas", "CinemachineCamera", "AudioManager",
    };

    // =========================================================================
    // MENÚS
    // =========================================================================

    [MenuItem("Sombras de la Frontera/Nivel 4/1. Construir Escena del Refugio Seguro")]
    public static void BuildLevel4Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(SCENE) && !EditorUtility.DisplayDialog("Nivel 4",
                "La escena 05_Level4_SafeHaven ya existe y se reconstruirá desde cero.\n¿Continuar?", "Reconstruir", "Cancelar"))
            return;

        BuildLevel4();
        EditorUtility.DisplayDialog("Nivel 4", "Escena del Refugio Seguro construida.\n\n" +
            "Se abrió 05_Level4_SafeHaven. Para probarla desde el menú usa:\n" +
            "Sombras de la Frontera → Depuración → Desbloquear todos los niveles.", "Aceptar");
    }

    [MenuItem("Sombras de la Frontera/Nivel 4/2. Verificar Colisiones y Coberturas")]
    public static void VerifyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        int problems = VerifyLevel4();
        EditorUtility.DisplayDialog("Nivel 4", problems == 0 ? "Todo correcto: colisiones, coberturas, carriles y camino a la meta."
            : $"Se encontraron {problems} problemas. Revisa la consola.", "Aceptar");
    }

    /// <summary>Uso por línea de comandos (Unity -batchmode -executeMethod SetupNivel4Editor.BuildFromCommandLine).</summary>
    public static void BuildFromCommandLine()
    {
        BuildLevel4();
        int problems = VerifyLevel4();
        CapturePreviews(CaptureFolder());
        Debug.Log($"[Nivel4] Construcción terminada. Problemas: {problems}");
    }

    /// <summary>Solo verifica y captura la escena ya construida.</summary>
    public static void CaptureFromCommandLine()
    {
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        VerifyLevel4();
        CapturePreviews(CaptureFolder());
    }

    // Las capturas van a Temp (ignorado por git) salvo que se indique otra carpeta con NIVEL4_CAPTURAS
    private static string CaptureFolder()
    {
        string custom = System.Environment.GetEnvironmentVariable("NIVEL4_CAPTURAS");
        return string.IsNullOrEmpty(custom) ? Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/Nivel4_Capturas")) : custom;
    }

    // =========================================================================
    // CONSTRUCCIÓN
    // =========================================================================

    public static void BuildLevel4()
    {
        AssetDatabase.Refresh();
        ConfigureSprite(MAP_SPRITE, 4096);
        ConfigureSprite(THUMBNAIL, 1024);
        Dictionary<string, GameObject> prefabs = BuildPrefabs();
        VolumeProfile profile = BuildPostProcessProfile();

        if (File.Exists(SCENE))
        {
            // Se sobrescribe el contenido y se conserva el .meta: la escena mantiene su GUID
            File.Copy(SOURCE_SCENE, SCENE, true);
            AssetDatabase.ImportAsset(SCENE, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            AssetDatabase.CopyAsset(SOURCE_SCENE, SCENE);
        }
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);

        RemoveLevel1Content(scene);
        SetupMap();
        SetupMapColliders();
        SetupGoal();
        PlaceObstacles(prefabs);
        SetupLighting();
        SetupPostProcess(profile);
        SetupPlayerAndTexts();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RegisterInBuildSettings();
        SetupMenuThumbnail();
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        Debug.Log("<color=green><b>[Nivel4]</b> Escena 05_Level4_SafeHaven construida.</color>");
    }

    private static void ConfigureSprite(string path, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[Nivel4] No se encontró {path}"); return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.filterMode = FilterMode.Bilinear;
        importer.maxTextureSize = maxSize;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    private static Dictionary<string, GameObject> BuildPrefabs()
    {
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR)) AssetDatabase.CreateFolder("Assets/Prefabs", "Level4_SafeHaven");

        Sprite[] sheet1 = AssetDatabase.LoadAllAssetsAtPath(SHEET_1).OfType<Sprite>().ToArray();
        Sprite[] sheet2 = AssetDatabase.LoadAllAssetsAtPath(SHEET_2).OfType<Sprite>().ToArray();

        var result = new Dictionary<string, GameObject>();
        foreach (ObstacleDef def in Defs)
        {
            string sliceName = $"obstacle_{def.sheet}_{def.slice}";
            Sprite sprite = (def.sheet == 1 ? sheet1 : sheet2).FirstOrDefault(sp => sp.name == sliceName);
            if (sprite == null) { Debug.LogWarning($"[Nivel4] Falta el sprite {sliceName} ({def.name})"); continue; }

            var go = new GameObject(def.name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Environment";

            Bounds local = sprite.bounds;
            Vector2 footSize = new Vector2(local.size.x * def.footW, local.size.y * def.footH);

            CapsuleCollider2D cap = go.AddComponent<CapsuleCollider2D>();
            cap.direction = CapsuleDirection2D.Horizontal;
            cap.size = footSize;
            cap.offset = new Vector2(local.center.x, local.min.y + footSize.y * 0.5f);

            if (def.isCover)
            {
                CoverZone2D cover = go.AddComponent<CoverZone2D>();
                cover.Configure(def.coverW, def.coverH, 0.30f);
            }

            YSort2D ysort = go.AddComponent<YSort2D>();
            ysort.isStatic = true;
            ysort.useBottomBounds = true;
            ysort.yOffset = 0f;
            ysort.baseOrder = 5000;

            go.transform.localScale = new Vector3(def.scale, def.scale, 1f);

            string path = $"{PREFAB_DIR}/{def.name}.prefab";
            result[def.name] = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        return result;
    }

    private static VolumeProfile BuildPostProcessProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PROFILE_PATH);
        }

        if (!profile.TryGet(out Vignette vignette))
        {
            vignette = profile.Add<Vignette>(true);
            AssetDatabase.AddObjectToAsset(vignette, profile);
        }
        vignette.color.Override(new Color(0.01f, 0.02f, 0.06f, 1f));
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.5f);

        if (!profile.TryGet(out ColorAdjustments color))
        {
            color = profile.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(color, profile);
        }
        color.contrast.Override(10f);
        color.saturation.Override(-10f);
        color.colorFilter.Override(new Color(0.9f, 0.95f, 1f, 1f));

        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(color);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void RemoveLevel1Content(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (KeepRoots.Contains(root.name.Trim())) continue;
            Object.DestroyImmediate(root);
        }

        // Grupos vacíos para los enemigos (Issue 2) y los recursos (Issue 3)
        var enemies = new GameObject("EnemiesAndHazards");
        new GameObject("Agentes").transform.SetParent(enemies.transform, false);
        new GameObject("Perros_K9").transform.SetParent(enemies.transform, false);
        new GameObject("Pickups").transform.SetParent(null, false);
    }

    private static void SetupMap()
    {
        GameObject map = FindRoot("DesertMap");
        foreach (Transform child in map.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
        map.name = "SafeHavenMap";
        map.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MAP_SPRITE);
    }

    /// <summary>
    /// Colisiones de lo que ya viene pintado en el mapa: la cerca rota del inicio (Mateo no puede regresar),
    /// las rocas y dunas de las orillas, las rodadoras, el campamento y el borde norte.
    /// </summary>
    private static void SetupMapColliders()
    {
        Transform bounds = FindRoot("LevelBounds").transform;
        var root = new GameObject("Colisiones_Mapa").transform;
        root.SetParent(bounds, false);

        void Box(string name, float x0, float y0, float x1, float y1)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            box.offset = new Vector2((x0 + x1) * 0.5f, (y0 + y1) * 0.5f);
        }

        Box("Cerca_Inicio", -MAP_HALF_W - 0.2f, -0.6f, MAP_HALF_W + 0.2f, FENCE_TOP_Y);
        Box("Borde_Norte", -MAP_HALF_W - 0.2f, 35.15f, MAP_HALF_W + 0.2f, MAP_TOP_Y + 0.5f);

        for (int part = 1; part <= 4; part++)
        {
            foreach (var (x0, x1, r0, r1) in EdgeRocks)
            {
                Vector2 a = PartToWorld(part, x0, r0), b = PartToWorld(part, x1, r1);
                // Las rocas de los bordes se extienden fuera del mapa para que no quede rendija con las paredes
                float left = x0 <= 0f ? a.x - 0.3f : a.x;
                float right = x1 >= 572f ? b.x + 0.3f : b.x;
                Box($"Orilla_P{part}_{(x0 <= 0f ? "Izq" : "Der")}_{Mathf.RoundToInt(r0)}", left, b.y, right, a.y);
            }
        }

        foreach (var (part, x, row, w) in PaintedTumbleweeds)
        {
            Vector2 p = PartToWorld(part, x, row);
            var go = new GameObject($"Rodadora_Mapa_P{part}");
            go.transform.SetParent(root, false);
            CapsuleCollider2D cap = go.AddComponent<CapsuleCollider2D>();
            cap.direction = CapsuleDirection2D.Horizontal;
            cap.size = new Vector2(w * 0.7f / 100f, 0.14f);
            cap.offset = new Vector2(p.x, p.y + 0.07f);
        }

        // Campamento: la casa de campaña, los bidones de agua y el asta de la bandera
        Vector2 tentA = PartToWorld(4, 305f, 160f), tentB = PartToWorld(4, 420f, 125f);
        Box("Campamento_Carpa", tentA.x, tentA.y, tentB.x, tentB.y);
        Vector2 jugsA = PartToWorld(4, 228f, 152f), jugsB = PartToWorld(4, 266f, 128f);
        Box("Campamento_Bidones", jugsA.x, jugsA.y, jugsB.x, jugsB.y);
        Vector2 pole = PartToWorld(4, 287f, 132f);
        Box("Campamento_Asta", pole.x - 0.04f, pole.y, pole.x + 0.04f, pole.y + 0.06f);
    }

    private static void SetupGoal()
    {
        Transform bounds = FindRoot("LevelBounds").transform;

        GameObject goal = FindRoot("GoalTrigger_RioBravo");
        goal.name = "GoalTrigger_Refugio";
        goal.transform.position = GoalPos;
        BoxCollider2D goalBox = goal.GetComponent<BoxCollider2D>();
        goalBox.offset = Vector2.zero;
        goalBox.size = GoalSize;
        // Fin del juego: el Issue 4 terminará la pantalla de Victoria final
        var goalSo = new SerializedObject(goal.GetComponent<GoalTrigger>());
        goalSo.FindProperty("nextLevelIndex").intValue = 5;
        goalSo.ApplyModifiedPropertiesWithoutUndo();

        Transform goalZone = bounds.Find("GoalZone");
        if (goalZone != null)
        {
            goalZone.position = GoalPos;
            BoxCollider2D zoneBox = goalZone.GetComponent<BoxCollider2D>();
            if (zoneBox != null) { zoneBox.offset = Vector2.zero; zoneBox.size = GoalSize; }
        }
    }

    private static void PlaceObstacles(Dictionary<string, GameObject> prefabs)
    {
        var root = new GameObject("Obstaculos_Nivel4").transform;
        foreach (var (name, x, baseY, flip) in Layout)
        {
            if (!prefabs.TryGetValue(name, out GameObject prefab)) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            float halfH = (sr.sprite.bounds.extents.y - sr.sprite.bounds.center.y) * go.transform.localScale.y;
            go.transform.position = new Vector3(x, baseY + halfH, 0f);
            if (flip) sr.flipX = true;
            go.GetComponent<YSort2D>().UpdateSortingOrder();
        }
    }

    private static void SetupLighting()
    {
        int background = SortingLayer.NameToID("Background");
        int[] allLayers = SortingLayer.layers.Select(l => l.id).ToArray();

        // Luz de luna para personajes y objetos (el mapa ya está pintado de noche y recibe su propia luz)
        Light2D moon = FindRoot("Global Light 2D").GetComponent<Light2D>();
        moon.color = new Color(0.62f, 0.70f, 0.95f, 1f);
        moon.intensity = 0.6f;
        SetTargetLayers(moon, allLayers.Where(id => id != background));

        var mapLightObj = new GameObject("Global Light 2D (Mapa)");
        Light2D mapLight = mapLightObj.AddComponent<Light2D>();
        mapLight.lightType = Light2D.LightType.Global;
        mapLight.color = new Color(0.9f, 0.93f, 1f, 1f);
        mapLight.intensity = 0.95f;
        SetTargetLayers(mapLight, new[] { background });

        // Halo tenue alrededor de Mateo para que siempre se distinga en la oscuridad
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        var halo = new GameObject("Halo_Nocturno");
        halo.transform.SetParent(player.transform, false);
        Vector3 s = player.transform.lossyScale;
        halo.transform.localScale = new Vector3(1f / s.x, 1f / s.y, 1f);
        Light2D haloLight = halo.AddComponent<Light2D>();
        haloLight.lightType = Light2D.LightType.Point;
        haloLight.color = new Color(0.75f, 0.82f, 1f, 1f);
        haloLight.intensity = 0.35f;
        haloLight.pointLightInnerRadius = 0f;
        haloLight.pointLightOuterRadius = 1.0f;
        haloLight.falloffIntensity = 0.7f;
        SetTargetLayers(haloLight, allLayers);

        SetupCampfire(allLayers);
    }

    /// <summary>Fogata del refugio: luz cálida que se ve desde lejos y guía a Mateo hacia la meta.</summary>
    private static void SetupCampfire(int[] allLayers)
    {
        var fire = new GameObject("Fogata_Refugio");
        fire.transform.position = CampfirePos;

        var lightObj = new GameObject("Luz_Fogata");
        lightObj.transform.SetParent(fire.transform, false);
        Light2D light = lightObj.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = FireColor;
        light.intensity = 1.2f;
        light.pointLightInnerRadius = 0.2f;
        light.pointLightOuterRadius = 2.2f;
        light.falloffIntensity = 0.6f;
        SetTargetLayers(light, allLayers);

        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_SPRITE);
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UNLIT_MATERIAL);
        if (glow == null) return;

        // Resplandor en el suelo (elipse por la vista 3/4), debajo de personajes y obstáculos
        var pool = new GameObject("Resplandor_Fogata");
        pool.transform.SetParent(fire.transform, false);
        pool.transform.localScale = new Vector3(1.8f, 1.1f, 1f);
        SpriteRenderer poolSr = pool.AddComponent<SpriteRenderer>();
        poolSr.sprite = glow;
        if (unlit != null) poolSr.sharedMaterial = unlit;
        poolSr.color = new Color(FireColor.r, FireColor.g, FireColor.b, 0.28f);
        poolSr.sortingLayerName = "Background";
        poolSr.sortingOrder = 20;
    }

    /// <summary>Busca un objeto raíz de la escena ignorando espacios sobrantes en el nombre (p. ej. "LevelBounds ").</summary>
    private static GameObject FindRoot(string name) =>
        UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name.Trim() == name);

    private static void SetTargetLayers(Light2D light, IEnumerable<int> layerIds)
    {
        var so = new SerializedObject(light);
        SerializedProperty prop = so.FindProperty("m_ApplyToSortingLayers");
        int[] ids = layerIds.ToArray();
        prop.arraySize = ids.Length;
        for (int i = 0; i < ids.Length; i++) prop.GetArrayElementAtIndex(i).intValue = ids[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetupPostProcess(VolumeProfile profile)
    {
        GameObject volumeObj = FindRoot("PostProcess_Volume");
        volumeObj.GetComponent<Volume>().sharedProfile = profile;

        DangerVignette danger = volumeObj.GetComponent<DangerVignette>();
        if (danger != null)
        {
            var so = new SerializedObject(danger);
            so.FindProperty("calmColor").colorValue = new Color(0.01f, 0.02f, 0.06f, 1f);
            so.FindProperty("calmIntensity").floatValue = 0.32f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupPlayerAndTexts()
    {
        // Mateo empieza justo después de cruzar la cerca rota
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        player.transform.position = new Vector3(SpawnPos.x, SpawnPos.y, player.transform.position.z);

        // De noche la sed avanza más lento que en el desierto de día del Nivel 1 (1.5); se ajusta en el Issue 3
        WaterSystem water = Object.FindAnyObjectByType<WaterSystem>();
        if (water != null)
        {
            var so = new SerializedObject(water);
            so.FindProperty("waterDrainRate").floatValue = 1.0f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        GameObject hud = FindRoot("HUD_Canvas");
        foreach (Text text in hud.GetComponentsInChildren<Text>(true))
        {
            if (text.text.Contains("El sol abrasa"))
            {
                text.text =
                    "Cruzaste el muro, pero las redadas recorren el desierto del otro lado...\n\n" +
                    "🕹️  <b>Controles:</b> Usa <b>W, A, S, D</b> o las <b>Flechas</b> para moverte.\n\n" +
                    "📦 <b>Sigilo:</b> Quédate quieto pegado a la parte de abajo de <b>rocas, autos oxidados, costales y arbustos</b> para esconderte. " +
                    "Las cercas, los árboles secos, las yucas y los ocotillos no te cubren: solo estorban el paso.\n\n" +
                    "🎯 <b>Objetivo:</b> Avanza hacia el norte y llega al <b>campamento del refugio</b>: busca la luz de la fogata.";
            }
            else if (text.text.Contains("NIVEL 1 COMPLETADO")) text.text = "¡NIVEL 4 COMPLETADO!";
            else if (text.text.Contains("orilla del R")) text.text = "¡Llegaste al refugio seguro!\n\nMateo por fin está a salvo.";
            EditorUtility.SetDirty(text);
        }

        EndGameModalController modal = hud.GetComponent<EndGameModalController>();
        if (modal != null)
        {
            var so = new SerializedObject(modal);
            so.FindProperty("nextLevelSceneName").stringValue = MAIN_MENU_SCENE; // Último nivel
            so.FindProperty("defeatByFlashlight.description").stringValue =
                "La linterna de un agente de la redada iluminó tu silueta.\n\n📦 <b>Consejo:</b> Escóndete tras rocas, autos oxidados o costales; las cercas y los árboles secos no te cubren.";
            so.FindProperty("defeatByDog.description").stringValue =
                "Los perros K-9 te mordieron y sus ladridos alertaron a los agentes.\n\n🐕 <b>Consejo:</b> Los perros vigilan en semicírculo frontal; pasa por detrás o agáchate a cubierto.";
            so.FindProperty("defeatByDehydration.description").stringValue =
                "La última noche de camino agotó tu última gota de agua.\n\n💧 <b>Consejo:</b> Busca agua en el camino antes de que se acabe.";
            so.FindProperty("defeatGeneric.description").stringValue =
                "Mateo ha perdido sus 3 corazones a unos pasos del refugio.\n\n🛡️ <b>Consejo:</b> Protege tu salud y administra bien tus movimientos.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void RegisterInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == SCENE)) return;
        scenes.Add(new EditorBuildSettingsScene(SCENE, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    /// <summary>Miniatura en la tarjeta del Nivel 4 (el candado lo sigue mostrando MainMenuController mientras esté bloqueado).</summary>
    private static void SetupMenuThumbnail()
    {
        Sprite thumb = AssetDatabase.LoadAssetAtPath<Sprite>(THUMBNAIL);
        if (thumb == null) return;

        var scene = EditorSceneManager.OpenScene(MENU_SCENE, OpenSceneMode.Single);
        Transform frame = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "ThumbFrame" && t.parent != null && t.parent.name == "Card_Level4");
        if (frame == null) { Debug.LogWarning("[Nivel4] No se encontró la tarjeta Card_Level4 del menú."); return; }

        Image img = frame.GetComponent<Image>();
        img.sprite = thumb;
        img.color = Color.white;
        img.preserveAspect = false;
        EditorUtility.SetDirty(img);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // =========================================================================
    // VERIFICACIÓN (colisiones, coberturas, carriles de patrulla y camino a la meta)
    // =========================================================================

    public static int VerifyLevel4()
    {
        Physics2D.SyncTransforms();
        int problems = 0;

        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        CapsuleCollider2D feet = player.GetComponent<CapsuleCollider2D>();
        Vector2 feetSize = Vector2.Scale(feet.size, player.transform.lossyScale);
        Bounds body = player.GetComponent<SpriteRenderer>().bounds;

        bool IsSolid(Collider2D c) => !c.isTrigger && c.attachedRigidbody == null;
        bool Blocked(float x, float fromY, float toY) =>
            Physics2D.CapsuleCastAll(new Vector2(x, fromY), feetSize, CapsuleDirection2D.Horizontal, 0f, Vector2.up, toY - fromY)
                .Any(h => IsSolid(h.collider));

        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel4][OK] {message}");
            else { Debug.LogWarning($"[Nivel4][FALLA] {message}"); problems++; }
        }

        // Inicio libre y cerca que impide regresar
        Vector2 start = player.transform.TransformPoint(feet.offset);
        Expect(!Physics2D.OverlapCapsuleAll(start, feetSize, CapsuleDirection2D.Horizontal, 0f).Any(IsSolid), $"Mateo empieza libre en ({start.x:F2}, {start.y:F2})");
        foreach (float x in new[] { -2.3f, -0.3f, 0.05f, 0.4f, 2.3f })
            Expect(Physics2D.OverlapCapsuleAll(new Vector2(x, FENCE_TOP_Y - 0.2f), feetSize, CapsuleDirection2D.Horizontal, 0f).Any(IsSolid),
                $"La cerca del inicio bloquea en x={x}");

        // Cada obstáculo es sólido en su base y las coberturas ocultan a Mateo agachado del lado visible
        foreach (YSort2D obstacle in Object.FindObjectsByType<YSort2D>().Where(o => o.isStatic && o.transform.parent != null && o.transform.parent.name == "Obstaculos_Nivel4"))
        {
            SpriteRenderer sr = obstacle.GetComponent<SpriteRenderer>();
            Bounds b = sr.bounds;
            Expect(Blocked(b.center.x, b.min.y - 0.5f, b.min.y + 0.4f), $"{obstacle.name} en ({b.center.x:F2}, {b.min.y:F2}) bloquea el paso");

            CoverZone2D cover = obstacle.GetComponent<CoverZone2D>();
            if (cover == null) continue;
            float feetY = b.min.y - 0.12f;
            float w = body.size.x * 0.55f, h = body.size.y * 0.6f;
            var crouched = new Rect(b.center.x - w * 0.5f, feetY, w, h);
            float coverage = cover.IsOnHidingSide(feetY) ? cover.Coverage(crouched) : 0f;
            Expect(coverage >= 0.6f, $"{obstacle.name}: cobertura de Mateo agachado = {coverage:P0}");

            float behindY = b.min.y + 0.45f; // Del otro lado (arriba) no debe ocultar
            var behind = new Rect(b.center.x - w * 0.5f, behindY, w, h);
            float coverageBehind = cover.IsOnHidingSide(behindY) ? cover.Coverage(behind) : 0f;
            Expect(coverageBehind < 0.6f, $"{obstacle.name}: no oculta desde el lado de atrás ({coverageBehind:P0})");
        }

        // Carriles libres para las patrullas del Issue 2 (de orilla a orilla del área caminable)
        foreach (float laneY in PatrolLanes)
        {
            RaycastHit2D[] hits = Physics2D.CircleCastAll(new Vector2(-1.75f, laneY), 0.12f, Vector2.right, 3.6f);
            Collider2D blocker = hits.Select(hh => hh.collider).FirstOrDefault(IsSolid);
            Expect(blocker == null, $"Carril de patrulla y={laneY} libre{(blocker != null ? $" (choca con {blocker.name})" : "")}");
        }

        // Hay camino del inicio a la meta
        Expect(FindPathLength(start, GoalPos, feetSize, out float length), $"Hay camino del inicio a la meta ({length:F1} unidades)");

        Expect(Object.FindAnyObjectByType<GoalTrigger>() != null, "Meta del refugio en la escena");
        Expect(EditorBuildSettings.scenes.Any(s => s.path == SCENE && s.enabled), "Escena registrada en Build Settings");

        Debug.Log($"[Nivel4] Verificación terminada con {problems} problemas.");
        return problems;
    }

    /// <summary>A* en una rejilla de 0.1 unidades: una celda es transitable si los pies de Mateo caben sin tocar nada sólido.</summary>
    private static bool FindPathLength(Vector2 start, Vector2 goal, Vector2 feetSize, out float length)
    {
        length = 0f;
        const float cell = 0.1f;
        const float minX = -2.85f, minY = 0f;
        int nx = 58, ny = 356;
        var walkable = new bool[nx, ny];
        for (int ix = 0; ix < nx; ix++)
            for (int iy = 0; iy < ny; iy++)
            {
                Vector2 c = new Vector2(minX + ix * cell, minY + iy * cell);
                walkable[ix, iy] = !Physics2D.OverlapCapsuleAll(c, feetSize, CapsuleDirection2D.Horizontal, 0f)
                    .Any(col => !col.isTrigger && col.attachedRigidbody == null);
            }

        Vector2Int ToCell(Vector2 p) => new Vector2Int(Mathf.Clamp(Mathf.RoundToInt((p.x - minX) / cell), 0, nx - 1), Mathf.Clamp(Mathf.RoundToInt((p.y - minY) / cell), 0, ny - 1));
        Vector2Int s0 = ToCell(start), g0 = ToCell(goal);
        var cost = new Dictionary<Vector2Int, float> { [s0] = 0f };
        var open = new SortedSet<(float f, int x, int y)> { (Vector2.Distance(s0, g0), s0.x, s0.y) };
        while (open.Count > 0)
        {
            var top = open.Min; open.Remove(top);
            var cur = new Vector2Int(top.x, top.y);
            if (cur == g0) { length = cost[cur] * cell; return true; }
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nb = new Vector2Int(cur.x + dx, cur.y + dy);
                    if (nb.x < 0 || nb.y < 0 || nb.x >= nx || nb.y >= ny || !walkable[nb.x, nb.y]) continue;
                    if (dx != 0 && dy != 0 && (!walkable[cur.x + dx, cur.y] || !walkable[cur.x, cur.y + dy])) continue;
                    float ng = cost[cur] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (cost.TryGetValue(nb, out float old) && old <= ng) continue;
                    cost[nb] = ng;
                    open.Add((ng + Vector2.Distance(nb, g0), nb.x, nb.y));
                }
        }
        return false;
    }

    // =========================================================================
    // CAPTURAS (para revisar el aspecto nocturno sin entrar a Play)
    // =========================================================================

    public static void CapturePreviews(string folder)
    {
        Directory.CreateDirectory(folder);
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[Nivel4] No hay cámara para capturar."); return; }

        const int width = 540, height = 960;
        cam.orthographic = true;
        cam.orthographicSize = MAP_HALF_W * 2f / 2f / (width / (float)height);
        var rt = new RenderTexture(width, height, 24);
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        float half = cam.orthographicSize;

        // En modo batch no corre el ciclo del editor: las luces puntuales arman su malla en LateUpdate
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
        var updateMesh = typeof(Light2D).GetMethod("UpdateMesh", flags);
        var lateUpdate = typeof(Light2D).GetMethod("LateUpdate", flags);
        foreach (Light2D light in Object.FindObjectsByType<Light2D>())
        {
            updateMesh?.Invoke(light, new object[] { true });
            lateUpdate?.Invoke(light, null);
        }

        // Cuadro de calentamiento para que terminen de subir las texturas recién importadas
        cam.targetTexture = rt;
        cam.transform.position = new Vector3(0f, half, -10f);
        cam.Render();

        int index = 0;
        for (float y = half - 0.1f; y < MAP_TOP_Y + half; y += half * 2f - 0.5f)
        {
            float camY = Mathf.Min(y, MAP_TOP_Y - half - 0.05f);
            cam.transform.position = new Vector3(0f, camY, -10f);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(folder, $"nivel4_{index++}.png"), tex.EncodeToPNG());
            if (camY < y) break;
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Debug.Log($"[Nivel4] Capturas guardadas en {folder}");
    }
}
