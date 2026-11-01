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
/// Nivel 3 "El Muro Fronterizo" (Issue 1: escena, mapa nocturno, iluminación y obstáculos).
/// 1. Importa el mapa continuo nocturno y los obstáculos (Assets/Sprites/Level3_BorderWall).
/// 2. Crea los prefabs de obstáculos con colisión en la base y cobertura del lado visible.
/// 3. Construye 04_Level3_BorderWall a partir del Nivel 1 (HUD, pausa, Victoria/Game Over, cámara y Mateo ya conectados),
///    quita lo propio del desierto y coloca mapa, muro con hueco, obstáculos, lámparas y la iluminación de noche.
/// 4. La registra en Build Settings y pone la miniatura en la tarjeta del menú.
/// </summary>
public static class SetupNivel3Editor
{
    private const string SOURCE_SCENE = "Assets/Scenes/02_Level1_Desert.unity";
    public const string SCENE = "Assets/Scenes/04_Level3_BorderWall.unity";
    private const string MENU_SCENE = "Assets/Scenes/01_MainMenu.unity";
    private const string SPRITES = "Assets/Sprites/Level3_BorderWall/";
    private const string MAP_SPRITE = SPRITES + "mapa_completo_nivel3_seamless.png";
    private const string OBSTACLE_SPRITES = SPRITES + "Obstacles/";
    private const string PREFAB_DIR = "Assets/Prefabs/Level3_BorderWall";
    private const string PROFILE_PATH = "Assets/Settings/Level3_PostProcess.asset";
    private const string THUMBNAIL = "Assets/Sprites/UI/Thumbnail_Level3.jpg";
    private const string GLOW_SPRITE = "Assets/Sprites/Effects/LightGlow.png";
    private const string LEVEL1_OBSTACLES = "Assets/Sprites/Environment/obstacle_1.png";
    private const string UNLIT_MATERIAL = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // Poste de luz: el poste está a la izquierda del sprite y el foco arriba a la derecha (coordenadas locales)
    private static readonly Vector2 LampPole = new Vector2(-0.155f, 0f);
    private static readonly Vector2 LampHead = new Vector2(0.085f, 0.67f);
    private static readonly Color LampColor = new Color(1f, 0.84f, 0.58f, 1f);
    private const string NEXT_LEVEL_SCENE = "05_Level4_SafeHaven";
    private const string PREF_MAX_LEVEL = "NivelMaximo";

    // Hueco del muro (x) y altura de su base; coincide con el dibujo del mapa
    private const float GAP_LEFT = -0.18f;
    private const float GAP_RIGHT = 0.32f;
    private const float WALL_BASE_Y = 32.95f;
    private const float MAP_TOP_Y = 35.6f;
    private const float MAP_HALF_W = 2.86f;

    private enum Kind { Solid, Lamp, Tower }

    private class ObstacleDef
    {
        public string name; public Kind kind; public float scale;
        public float footW, footH;              // Collider sólido en la base (fracción del sprite)
        public bool isCover; public float coverW, coverH;
        public string level1Slice;              // Si no es nulo, el sprite sale de la hoja de obstáculos del Nivel 1
        public ObstacleDef(string name, Kind kind, float scale, float footW, float footH, bool isCover, float coverW = 0f, float coverH = 0f, string level1Slice = null)
        {
            this.name = name; this.kind = kind; this.scale = scale; this.footW = footW; this.footH = footH;
            this.isCover = isCover; this.coverW = coverW; this.coverH = coverH; this.level1Slice = level1Slice;
        }
    }

    // Escala: proporción respecto a Mateo (~0.6 unidades de alto).
    // Solo estorban (no ocultan): lo que se ve a través (mallas, portón, torre, poste, arbusto seco)
    // y lo demasiado bajo para tapar a una persona (tarimas, barril tirado, generador).
    private static readonly ObstacleDef[] Defs =
    {
        new ObstacleDef("contenedor_azul",        Kind.Solid, 0.80f, 0.94f, 0.42f, true,  0.95f, 0.90f),
        new ObstacleDef("contenedor_rojo",        Kind.Solid, 0.80f, 0.94f, 0.42f, true,  0.95f, 0.90f),
        new ObstacleDef("cajas_madera",           Kind.Solid, 0.60f, 0.85f, 0.35f, true,  0.90f, 0.90f),
        new ObstacleDef("barriles",               Kind.Solid, 0.50f, 0.85f, 0.32f, true,  0.85f, 0.90f),
        new ObstacleDef("costales",               Kind.Solid, 0.60f, 0.90f, 0.40f, true,  0.92f, 0.90f),
        new ObstacleDef("barrera_concreto",       Kind.Solid, 0.62f, 0.90f, 0.30f, true,  0.90f, 0.90f),
        new ObstacleDef("barrera_concreto_doble", Kind.Solid, 0.62f, 0.90f, 0.32f, true,  0.90f, 0.90f),
        new ObstacleDef("contenedor_basura",      Kind.Solid, 0.62f, 0.88f, 0.35f, true,  0.90f, 0.90f),
        new ObstacleDef("caseta_guardia",         Kind.Solid, 0.75f, 0.90f, 0.35f, true,  0.92f, 0.90f),
        new ObstacleDef("patrulla_pickup",        Kind.Solid, 0.75f, 0.90f, 0.45f, true,  0.92f, 0.80f),
        new ObstacleDef("patrulla_suv",           Kind.Solid, 0.75f, 0.90f, 0.45f, true,  0.92f, 0.80f),
        new ObstacleDef("barril_tirado",          Kind.Solid, 0.55f, 0.75f, 0.35f, false),
        new ObstacleDef("generador",              Kind.Solid, 0.55f, 0.80f, 0.30f, false),
        new ObstacleDef("tarimas",                Kind.Solid, 0.58f, 0.90f, 0.35f, false),
        new ObstacleDef("arbusto_seco_l3",        Kind.Solid, 0.60f, 0.45f, 0.18f, false),
        new ObstacleDef("malla_recta",            Kind.Solid, 0.70f, 1.00f, 0.12f, false),
        new ObstacleDef("malla_esquina",          Kind.Solid, 0.70f, 1.00f, 0.12f, false),
        new ObstacleDef("porton",                 Kind.Solid, 0.70f, 1.00f, 0.12f, false),
        new ObstacleDef("torre_vigilancia",       Kind.Tower, 0.95f, 0.75f, 0.10f, false),
        new ObstacleDef("poste_luz",              Kind.Lamp,  0.80f, 0.22f, 0.05f, false),

        // Vegetación y rocas del desierto del Nivel 1 (mismo sprite, escala y cobertura que allá)
        new ObstacleDef("cactus_l1",       Kind.Solid, 1f, 0.30f, 0.14f, true,  0.45f, 0.90f, "obstacle_1_0"),
        new ObstacleDef("nopalera_l1",     Kind.Solid, 1f, 0.70f, 0.22f, true,  0.85f, 0.85f, "obstacle_1_1"),
        new ObstacleDef("roca_pequena_l1", Kind.Solid, 1f, 0.85f, 0.30f, true,  0.90f, 0.90f, "obstacle_1_4"),
        new ObstacleDef("roca_grande_l1",  Kind.Solid, 1f, 0.85f, 0.30f, true,  0.90f, 0.90f, "obstacle_1_5"),
        new ObstacleDef("arbusto_seco_l1", Kind.Solid, 1f, 0.50f, 0.20f, false, 0f,    0f,    "obstacle_1_6"),
        new ObstacleDef("arbusto_l1",      Kind.Solid, 1f, 0.60f, 0.25f, true,  0.85f, 0.85f, "obstacle_1_7"),
    };

    // (prefab, x, base y, voltear)
    private static readonly (string name, float x, float baseY, bool flip)[] Layout =
    {
        ("arbusto_seco_l3", -2.0f, 1.8f, false),
        ("arbusto_seco_l3", 2.3f, 2.6f, true),
        ("costales", 1.3f, 3.4f, false),
        ("barril_tirado", -1.2f, 3.9f, false),
        ("barrera_concreto", -2.2f, 5.0f, false),
        ("arbusto_seco_l3", 0.4f, 5.3f, false),
        ("barriles", 2.2f, 5.8f, false),
        ("tarimas", -0.6f, 6.8f, false),
        ("costales", -2.0f, 7.6f, false),
        ("barrera_concreto_doble", 1.5f, 7.6f, false),
        ("malla_recta", -2.45f, 9.4f, false),
        ("malla_recta", -1.65f, 9.4f, false),
        ("porton", 0.6f, 9.4f, false),
        ("caseta_guardia", 1.6f, 9.3f, false),
        ("malla_recta", 2.45f, 9.4f, false),
        ("generador", 1.15f, 10.2f, false),
        ("patrulla_pickup", -1.8f, 12.0f, false),
        ("barriles", 2.2f, 11.3f, true),
        ("poste_luz", -0.9f, 12.55f, false),   // Ilumina la ruta del policía del patio
        ("cajas_madera", 1.4f, 13.6f, false),
        ("generador", -0.4f, 13.9f, false),
        ("contenedor_basura", 2.2f, 15.0f, false),
        ("patrulla_suv", -1.6f, 15.4f, true),
        ("barrera_concreto", 0.6f, 15.8f, false),
        ("tarimas", 1.9f, 16.9f, false),
        ("barril_tirado", -0.3f, 17.0f, true),
        ("poste_luz", 0.75f, 16.6f, false),    // Ilumina la ruta vertical del policía central
        ("costales", -1.4f, 17.8f, false),
        ("barrera_concreto_doble", -2.1f, 19.3f, false),
        ("barrera_concreto", -0.8f, 19.5f, false),
        ("caseta_guardia", 0.4f, 19.1f, false),
        ("barrera_concreto", 1.5f, 19.5f, false),
        ("arbusto_seco_l3", 2.5f, 20.2f, false),
        ("contenedor_azul", -1.9f, 21.5f, false),
        ("barriles", 0.0f, 21.6f, false),
        ("poste_luz", 0.1f, 23.05f, false),    // Cruce del estacionamiento de contenedores
        ("contenedor_rojo", 1.8f, 23.2f, false),
        ("cajas_madera", -0.8f, 23.6f, false),
        ("tarimas", -2.3f, 24.4f, false),
        ("generador", 0.5f, 24.9f, false),
        ("contenedor_azul", -1.5f, 26.0f, true),
        ("malla_recta", 1.0f, 25.8f, false),
        ("malla_esquina", 1.85f, 25.8f, false),
        ("cajas_madera", 2.3f, 27.0f, true),
        ("patrulla_pickup", 0.3f, 27.6f, true),
        ("barril_tirado", -2.3f, 27.8f, false),
        ("poste_luz", -0.4f, 8.9f, false),     // Paso abierto de la malla (vigilado desde el desierto)
        ("torre_vigilancia", -2.3f, 29.3f, false),
        ("contenedor_basura", 0.7f, 29.8f, true),
        ("barrera_concreto_doble", -1.0f, 30.8f, false),
        ("torre_vigilancia", 2.35f, 30.4f, false),
        ("torre_vigilancia", 2.5f, 17.95f, false),
        ("costales", 1.5f, 31.4f, false),
        ("generador", -2.2f, 31.9f, false),
        ("poste_luz", 0.75f, 32.3f, false),    // Llegada al hueco del muro
        ("barriles", 2.4f, 32.3f, false),
        // Vegetación y rocas del desierto (acomodadas con otros/nivel3/desierto_extra.py)
        ("cactus_l1", -0.21f, 3.28f, false),
        ("cactus_l1", 1.69f, 6.54f, true),
        ("cactus_l1", 1.48f, 2.23f, true),
        ("nopalera_l1", 1.95f, 5.01f, true),
        ("nopalera_l1", -0.62f, 1.22f, true),
        ("nopalera_l1", -1.28f, 5.74f, false),
        ("roca_grande_l1", 0.66f, 7.08f, true),
        ("roca_grande_l1", -1.04f, 5.25f, false),
        ("roca_pequena_l1", 0.99f, 1.15f, false),
        ("roca_pequena_l1", -1.17f, 1.83f, true),
        ("roca_pequena_l1", -2.23f, 3.9f, true),
        ("arbusto_l1", 1.67f, 3.96f, true),
        ("arbusto_l1", 1.04f, 2.92f, true),
        ("arbusto_l1", 0.04f, 7.74f, false),
        ("arbusto_seco_l1", -2.26f, 6.71f, false),
        ("arbusto_seco_l1", 0.22f, 6.47f, true),
    };

    // =========================================================================
    // ENEMIGOS, FAUNA Y RECURSOS (Issue 2). Rutas revisadas contra los obstáculos con otros/nivel3/enemigos_preview.py
    // =========================================================================

    // La zona del desierto termina en la malla (y ≈ 9); el resto es la zona de la frontera
    private const float DESERT_TOP_Y = 8.7f;

    // Policías (ida y vuelta entre los puntos): 2 en el desierto y 4 en la frontera
    private static readonly Vector2[][] PoliceRoutes =
    {
        new[] { new Vector2(-1.7f, 4.6f), new Vector2(1.9f, 4.6f) },
        new[] { new Vector2(-1.0f, 8.55f), new Vector2(2.4f, 8.55f) },
        new[] { new Vector2(-2.4f, 13.2f), new Vector2(0.9f, 13.2f) },
        new[] { new Vector2(0.1f, 15.05f), new Vector2(0.1f, 18.4f) },
        new[] { new Vector2(-2.4f, 22.75f), new Vector2(2.4f, 22.75f) },
        new[] { new Vector2(-0.2f, 28.9f), new Vector2(-0.2f, 32.45f) },
    };

    // Perros K-9 (raza, punto A, punto B): 1 en el desierto y 2 en la frontera
    private static readonly (string breed, Vector2 a, Vector2 b)[] DogRoutes =
    {
        ("Rottweiler", new Vector2(-2.3f, 6.3f), new Vector2(-1.2f, 6.3f)),
        ("Aleman", new Vector2(-1.6f, 20.45f), new Vector2(1.4f, 20.45f)),
        ("Doberman", new Vector2(-2.1f, 27.15f), new Vector2(-0.5f, 27.15f)),
    };

    // Fauna nociva solo en el desierto
    private static readonly (string type, float x, float y)[] FaunaSpots =
    {
        ("Snake", -1.9f, 3.0f),
        ("Snake", 0.9f, 6.5f),
        ("Scorpion", 2.0f, 1.6f),
        ("Scorpion", -0.5f, 2.6f),
        ("Spider", 2.35f, 4.0f),
        ("Spider", -2.4f, 8.2f),
    };

    // 6 tunas (desierto), 5 cantimploras y 9 botellas repartidas en todo el mapa
    private static readonly (string type, float x, float y)[] ResourceSpots =
    {
        ("Tuna", -2.44f, 2.03f),
        ("Tuna", 0.81f, 2.21f),
        ("Tuna", 0.18f, 4.51f),
        ("Tuna", -0.54f, 6.26f),
        ("Tuna", -1.34f, 6.81f),
        ("Botella", 2.46f, 7.34f),
        ("Cantimplora", -0.72f, 7.56f),
        ("Botella", 0.7f, 8.04f),
        ("Tuna", -0.44f, 8.57f),
        ("Cantimplora", -1.51f, 11.51f),
        ("Botella", -0.83f, 13.56f),
        ("Botella", -2.02f, 13.65f),
        ("Cantimplora", 2.2f, 13.77f),
        ("Botella", 1.77f, 16.04f),
        ("Cantimplora", 0.42f, 18.28f),
        ("Botella", 1.84f, 21.52f),
        ("Botella", 0.51f, 22.9f),
        ("Botella", -0.23f, 26.18f),
        ("Botella", -1.54f, 29.94f),
        ("Cantimplora", 1.68f, 32.06f),
    };

    // Reflectores de las torres: posición de la torre, dirección central, amplitud (±grados), periodo (s) y desfase.
    // Las dos torres del muro se alternan para dejar momentos seguros para cruzar.
    private static readonly (float x, float y, float center, float range, float period, float phase)[] Searchlights =
    {
        (2.5f, 17.95f, 195f, 40f, 7.5f, 0.00f),   // Patio: barre hacia la izquierda y abajo
        (-2.3f, 29.3f, -10f, 40f, 7.0f, 0.00f),   // Muro, lado izquierdo
        (2.35f, 30.4f, 190f, 40f, 7.0f, 0.50f),   // Muro, lado derecho (a contratiempo)
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

    [MenuItem("Sombras de la Frontera/Nivel 3/1. Construir Escena del Muro Fronterizo")]
    public static void BuildLevel3Menu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (File.Exists(SCENE) && !EditorUtility.DisplayDialog("Nivel 3",
                "La escena 04_Level3_BorderWall ya existe y se reconstruirá desde cero.\n¿Continuar?", "Reconstruir", "Cancelar"))
            return;

        BuildLevel3();
        EditorUtility.DisplayDialog("Nivel 3", "Escena del Muro Fronterizo construida.\n\n" +
            "Se abrió 04_Level3_BorderWall. Para probarla desde el menú usa:\n" +
            "Sombras de la Frontera → Depuración → Desbloquear todos los niveles.", "Aceptar");
    }

    [MenuItem("Sombras de la Frontera/Nivel 3/2. Verificar Colisiones y Coberturas")]
    public static void VerifyMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        int problems = VerifyLevel3();
        EditorUtility.DisplayDialog("Nivel 3", problems == 0 ? "Todo correcto: colisiones, hueco del muro y coberturas."
            : $"Se encontraron {problems} problemas. Revisa la consola.", "Aceptar");
    }

    [MenuItem("Sombras de la Frontera/Nivel 3/3. Colocar Policías, Perros, Fauna y Recursos")]
    /// <summary>Uso por línea de comandos: coloca enemigos y recursos en la escena existente, verifica y captura.</summary>
    public static void PlaceEnemiesFromCommandLine()
    {
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        PlaceEnemiesAndResources();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        VerifyLevel3();
        CapturePreviews(Path.GetFullPath(Path.Combine(Application.dataPath, "../../otros/nivel3/capturas")));
    }

    public static void PlaceEnemiesMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        PlaceEnemiesAndResources();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        EditorUtility.DisplayDialog("Nivel 3", "6 policías, 3 perros K-9, 6 criaturas de fauna y 20 recursos colocados.", "Aceptar");
    }

    [MenuItem("Sombras de la Frontera/Depuración/Desbloquear todos los niveles")]
    public static void UnlockAllLevels()
    {
        PlayerPrefs.SetInt(PREF_MAX_LEVEL, 4);
        PlayerPrefs.Save();
        Debug.Log("[Depuración] Niveles 1 a 4 desbloqueados en el menú.");
    }

    [MenuItem("Sombras de la Frontera/Depuración/Bloquear niveles (solo Nivel 1)")]
    public static void LockLevels()
    {
        PlayerPrefs.SetInt(PREF_MAX_LEVEL, 1);
        PlayerPrefs.Save();
        Debug.Log("[Depuración] Progreso reiniciado: solo el Nivel 1 está desbloqueado.");
    }

    /// <summary>Uso por línea de comandos (Unity -batchmode -executeMethod SetupNivel3Editor.BuildFromCommandLine).</summary>
    public static void BuildFromCommandLine()
    {
        BuildLevel3();
        int problems = VerifyLevel3();
        CapturePreviews(Path.GetFullPath(Path.Combine(Application.dataPath, "../../otros/nivel3/capturas")));
        Debug.Log($"[Nivel3] Construcción terminada. Problemas: {problems}");
    }

    // =========================================================================
    // CONSTRUCCIÓN
    // =========================================================================

    public static void BuildLevel3()
    {
        AssetDatabase.Refresh();
        ConfigureImports();
        Dictionary<string, GameObject> prefabs = BuildPrefabs();
        VolumeProfile profile = BuildPostProcessProfile();

        if (File.Exists(SCENE))
        {
            // Se sobrescribe el contenido y se conserva el .meta: la escena mantiene su GUID (sin cambios innecesarios en git)
            File.Copy(SOURCE_SCENE, SCENE, true);
            AssetDatabase.ImportAsset(SCENE, ImportAssetOptions.ForceUpdate);
        }
        else
        {
            AssetDatabase.CopyAsset(SOURCE_SCENE, SCENE);
        }
        var scene = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);

        RemoveDesertContent(scene);
        SetupMap();
        SetupWallAndGoal();
        PlaceObstacles(prefabs);
        SetupLighting();
        SetupPostProcess(profile);
        SetupPlayerAndTexts();
        SetupAlarmAndHud();
        PlaceEnemiesAndResources();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        RegisterInBuildSettings();
        SetupMenuThumbnail();
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        Debug.Log("<color=green><b>[Nivel3]</b> Escena 04_Level3_BorderWall construida.</color>");
    }

    private static void ConfigureImports()
    {
        ConfigureSprite(MAP_SPRITE, 4096);
        foreach (ObstacleDef def in Defs.Where(d => d.level1Slice == null)) ConfigureSprite(OBSTACLE_SPRITES + def.name + ".png", 512);
        ConfigureSprite(THUMBNAIL, 1024);
        ConfigureSprite(GLOW_SPRITE, 256);
    }

    private static void ConfigureSprite(string path, int maxSize)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning($"[Nivel3] No se encontró {path}"); return; }

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
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR)) AssetDatabase.CreateFolder("Assets/Prefabs", "Level3_BorderWall");

        var result = new Dictionary<string, GameObject>();
        foreach (ObstacleDef def in Defs)
        {
            Sprite sprite = def.level1Slice != null
                ? AssetDatabase.LoadAllAssetsAtPath(LEVEL1_OBSTACLES).OfType<Sprite>().FirstOrDefault(sp => sp.name == def.level1Slice)
                : AssetDatabase.LoadAssetAtPath<Sprite>(OBSTACLE_SPRITES + def.name + ".png");
            if (sprite == null) { Debug.LogWarning($"[Nivel3] Falta el sprite {def.name}"); continue; }

            var go = new GameObject(def.name);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerName = "Environment";

            Bounds local = sprite.bounds;
            Vector2 footSize = new Vector2(local.size.x * def.footW, local.size.y * def.footH);
            float footY = local.min.y + footSize.y * 0.5f;

            CapsuleCollider2D cap = go.AddComponent<CapsuleCollider2D>();
            cap.direction = CapsuleDirection2D.Horizontal;
            cap.size = footSize;
            cap.offset = new Vector2(def.kind == Kind.Lamp ? LampPole.x : local.center.x, footY);

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

            if (def.kind == Kind.Lamp) AddLampLight(go, local);
            if (def.kind == Kind.Tower) AddSearchlight(go, local);
            go.transform.localScale = new Vector3(def.scale, def.scale, 1f);

            string path = $"{PREFAB_DIR}/{def.name}.prefab";
            result[def.name] = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
        }
        return result;
    }

    /// <summary>
    /// Luz del poste: una Light2D que ilumina a personajes y objetos cercanos, un charco de luz cálida en el suelo
    /// y un brillo en el foco (estos dos son sprites sin iluminación, así se ven encendidos en todo momento).
    /// </summary>
    /// <summary>Reflector en la base de la torre: haz que barre el suelo (SearchlightTower2D) con su luz 2D.</summary>
    private static void AddSearchlight(GameObject towerObj, Bounds local)
    {
        var beam = new GameObject("Reflector");
        beam.transform.SetParent(towerObj.transform, false);
        beam.transform.localPosition = new Vector3(local.center.x - 0.12f, local.max.y - 0.55f, 0f);
        beam.AddComponent<MeshFilter>();
        beam.AddComponent<MeshRenderer>();
        beam.AddComponent<SearchlightTower2D>();

        var lightObj = new GameObject("Luz_Reflector");
        lightObj.transform.SetParent(beam.transform, false);
        Light2D light = lightObj.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = new Color(1f, 0.97f, 0.8f, 1f);
        light.intensity = 1.1f;
        light.pointLightInnerRadius = 0.2f;
        light.pointLightOuterRadius = 3.6f;
        light.pointLightOuterAngle = 22f;
        light.pointLightInnerAngle = 10f;
        light.falloffIntensity = 0.6f;
        SetTargetLayers(light, SortingLayer.layers.Select(l => l.id));
    }

    private static void AddLampLight(GameObject lampObj, Bounds local)
    {
        float groundY = local.min.y + 0.05f;

        // Zona iluminada: dentro de ella las patrullas detectan más rápido a Mateo
        var zone = new GameObject("Zona_Luz");
        zone.transform.SetParent(lampObj.transform, false);
        zone.transform.localPosition = new Vector3(LampHead.x, groundY + 0.1f, 0f);
        zone.AddComponent<LightZone2D>();

        var lightObj = new GameObject("Luz");
        lightObj.transform.SetParent(lampObj.transform, false);
        lightObj.transform.localPosition = new Vector3(LampHead.x, groundY + 0.35f, 0f);
        Light2D lamp = lightObj.AddComponent<Light2D>();
        lamp.lightType = Light2D.LightType.Point;
        lamp.color = LampColor;
        lamp.intensity = 1.15f;
        lamp.pointLightInnerRadius = 0.25f;
        lamp.pointLightOuterRadius = 1.9f;
        lamp.falloffIntensity = 0.55f;
        SetTargetLayers(lamp, SortingLayer.layers.Select(l => l.id));

        Sprite glow = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_SPRITE);
        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UNLIT_MATERIAL);
        if (glow == null) return;

        // Charco de luz en el suelo (elipse por la vista 3/4), debajo de personajes y obstáculos
        var pool = new GameObject("Charco_Luz");
        pool.transform.SetParent(lampObj.transform, false);
        pool.transform.localPosition = new Vector3(LampHead.x, groundY + 0.1f, 0f);
        pool.transform.localScale = new Vector3(1.45f, 0.9f, 1f);
        SpriteRenderer poolSr = pool.AddComponent<SpriteRenderer>();
        poolSr.sprite = glow;
        if (unlit != null) poolSr.sharedMaterial = unlit;
        poolSr.color = new Color(LampColor.r, LampColor.g * 0.95f, LampColor.b * 0.8f, 0.30f);
        poolSr.sortingLayerName = "Background";
        poolSr.sortingOrder = 20;

        // Brillo del foco
        var bulb = new GameObject("Brillo_Foco");
        bulb.transform.SetParent(lampObj.transform, false);
        bulb.transform.localPosition = new Vector3(LampHead.x, LampHead.y, 0f);
        bulb.transform.localScale = new Vector3(0.28f, 0.28f, 1f);
        SpriteRenderer bulbSr = bulb.AddComponent<SpriteRenderer>();
        bulbSr.sprite = glow;
        if (unlit != null) bulbSr.sharedMaterial = unlit;
        bulbSr.color = new Color(1f, 0.92f, 0.7f, 0.9f);
        bulbSr.sortingLayerName = "Foreground";
        bulbSr.sortingOrder = 50;
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
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.5f);

        if (!profile.TryGet(out ColorAdjustments color))
        {
            color = profile.Add<ColorAdjustments>(true);
            AssetDatabase.AddObjectToAsset(color, profile);
        }
        color.contrast.Override(12f);
        color.saturation.Override(-12f);
        color.colorFilter.Override(new Color(0.88f, 0.94f, 1f, 1f));

        EditorUtility.SetDirty(vignette);
        EditorUtility.SetDirty(color);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    private static void RemoveDesertContent(UnityEngine.SceneManagement.Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (KeepRoots.Contains(root.name.Trim())) continue;
            Object.DestroyImmediate(root);
        }

        // Grupos vacíos para los enemigos del Issue 2
        var enemies = new GameObject("EnemiesAndHazards");
        new GameObject("Policias").transform.SetParent(enemies.transform, false);
        new GameObject("Perros_K9").transform.SetParent(enemies.transform, false);
        new GameObject("Pickups").transform.SetParent(null, false);
    }

    private static void SetupMap()
    {
        GameObject map = FindRoot("DesertMap");
        foreach (Transform child in map.transform.Cast<Transform>().ToList()) Object.DestroyImmediate(child.gameObject);
        map.name = "BorderMap";
        map.GetComponent<SpriteRenderer>().sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MAP_SPRITE);
    }

    private static void SetupWallAndGoal()
    {
        Transform bounds = FindRoot("LevelBounds").transform;
        Vector2 goalPos = new Vector2((GAP_LEFT + GAP_RIGHT) * 0.5f, WALL_BASE_Y + 0.9f);

        // Muro: dos bloques sólidos desde su base hasta el borde superior, con el hueco libre
        var wall = new GameObject("MuroFronterizo");
        wall.transform.SetParent(bounds, false);
        float height = MAP_TOP_Y - WALL_BASE_Y;
        foreach (var (x0, x1) in new[] { (-MAP_HALF_W - 0.2f, GAP_LEFT), (GAP_RIGHT, MAP_HALF_W + 0.2f) })
        {
            BoxCollider2D box = wall.AddComponent<BoxCollider2D>();
            box.size = new Vector2(x1 - x0, height);
            box.offset = new Vector2((x0 + x1) * 0.5f, WALL_BASE_Y + height * 0.5f);
        }

        // Meta: al cruzar el hueco del muro
        GameObject goal = FindRoot("GoalTrigger_RioBravo");
        goal.name = "GoalTrigger_Muro";
        goal.transform.position = goalPos;
        BoxCollider2D goalBox = goal.GetComponent<BoxCollider2D>();
        goalBox.offset = Vector2.zero;
        goalBox.size = new Vector2(GAP_RIGHT - GAP_LEFT, 0.6f);
        var goalSo = new SerializedObject(goal.GetComponent<GoalTrigger>());
        goalSo.FindProperty("nextLevelIndex").intValue = 4;
        goalSo.ApplyModifiedPropertiesWithoutUndo();

        Transform goalZone = bounds.Find("GoalZone");
        if (goalZone != null)
        {
            goalZone.position = goalPos;
            BoxCollider2D zoneBox = goalZone.GetComponent<BoxCollider2D>();
            if (zoneBox != null) { zoneBox.offset = Vector2.zero; zoneBox.size = goalBox.size; }
        }
    }

    private static void PlaceObstacles(Dictionary<string, GameObject> prefabs)
    {
        var root = new GameObject("Obstaculos_Nivel3").transform;
        foreach (var (name, x, baseY, flip) in Layout)
        {
            if (!prefabs.TryGetValue(name, out GameObject prefab)) continue;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            float halfH = (sr.sprite.bounds.extents.y - sr.sprite.bounds.center.y) * go.transform.localScale.y;
            go.transform.position = new Vector3(x, baseY + halfH, 0f);
            if (flip) sr.flipX = true;
            go.GetComponent<YSort2D>().UpdateSortingOrder();

            SearchlightTower2D beam = go.GetComponentInChildren<SearchlightTower2D>();
            if (beam != null) ConfigureSearchlight(beam, x, baseY);
        }
    }

    private static void ConfigureSearchlight(SearchlightTower2D beam, float towerX, float towerY)
    {
        var cfg = Searchlights.FirstOrDefault(c => Mathf.Abs(c.x - towerX) < 0.01f && Mathf.Abs(c.y - towerY) < 0.01f);
        var so = new SerializedObject(beam);
        if (cfg.period > 0f)
        {
            so.FindProperty("centerAngle").floatValue = cfg.center;
            so.FindProperty("sweepHalfRange").floatValue = cfg.range;
            so.FindProperty("sweepPeriod").floatValue = cfg.period;
            so.FindProperty("phaseOffset").floatValue = cfg.phase;
        }
        else
        {
            beam.enabled = false; // Torre sin reflector configurado
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(beam);
    }

    /// <summary>Sistema de alarma de la escena e indicador "¡ILUMINADO!" en el HUD.</summary>
    private static void SetupAlarmAndHud()
    {
        new GameObject("Sistema_Alarma").AddComponent<AlarmSystem2D>();

        Transform hud = FindRoot("HUD_Canvas").transform;
        Transform stealth = hud.Find("StealthIndicator");
        if (stealth == null) { Debug.LogWarning("[Nivel3] No se encontró StealthIndicator en el HUD."); return; }

        // Mismo lugar que "OCULTO": nunca se muestran a la vez
        GameObject lit = Object.Instantiate(stealth.gameObject, stealth.parent);
        lit.name = "LitIndicator";
        lit.transform.SetSiblingIndex(stealth.GetSiblingIndex() + 1);
        lit.GetComponent<Image>().color = new Color(0.25f, 0.16f, 0.02f, 0.88f);
        Text label = lit.GetComponentInChildren<Text>();
        label.text = "¡ILUMINADO!";
        label.color = new Color(1f, 0.85f, 0.4f, 1f);
        lit.GetComponent<CanvasGroup>().alpha = 0f;
        lit.AddComponent<LightExposureIndicator>();
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
            so.FindProperty("calmIntensity").floatValue = 0.34f;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void SetupPlayerAndTexts()
    {
        // De noche la sed avanza un poco más lento que en el desierto del Nivel 1 (1.5)
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
                    "La noche cubre la frontera, pero la Patrulla Fronteriza no descansa...\n\n" +
                    "🕹️  <b>Controles:</b> Usa <b>W, A, S, D</b> o las <b>Flechas</b> para moverte.\n\n" +
                    "📦 <b>Sigilo:</b> Ocúltate detrás de <b>contenedores, casetas, patrullas y barreras</b>. " +
                    "Las mallas, las tarimas y los generadores no te cubren: solo estorban el paso.\n\n" +
                    "💡 <b>Cuidado:</b> Bajo una lámpara, si un policía te ve te atrapa al instante. Los reflectores de las torres barren el terreno: obsérvalos y cruza cuando se alejen.\n\n" +
                    "🎯 <b>Objetivo:</b> Cruza el patio de detención hacia el norte y escapa por el <b>hueco del muro fronterizo</b>.";
            }
            else if (text.text.Contains("NIVEL 1 COMPLETADO")) text.text = "¡NIVEL 3 COMPLETADO!";
            else if (text.text.Contains("orilla del R")) text.text = "¡Cruzaste el muro fronterizo!\n\nProgreso guardado: Nivel 4 Desbloqueado en disco.";
            EditorUtility.SetDirty(text);
        }

        EndGameModalController modal = hud.GetComponent<EndGameModalController>();
        if (modal != null)
        {
            var so = new SerializedObject(modal);
            so.FindProperty("nextLevelSceneName").stringValue = NEXT_LEVEL_SCENE;
            so.FindProperty("defeatByFlashlight.description").stringValue =
                "La linterna de un agente de la Patrulla Fronteriza iluminó tu silueta.\n\n📦 <b>Consejo:</b> Ocúltate tras contenedores, casetas o barreras de concreto; las mallas no te cubren.";
            so.FindProperty("defeatByDog.description").stringValue =
                "Los perros K-9 te mordieron y sus ladridos alertaron a la patrulla.\n\n🐕 <b>Consejo:</b> Los perros vigilan en semicírculo frontal; pasa por detrás o agáchate a cubierto.";
            so.FindProperty("defeatByDehydration.description").stringValue =
                "La larga caminata nocturna agotó tu última gota de agua.\n\n💧 <b>Consejo:</b> Busca botellas de agua en el camino.";
            so.FindProperty("defeatGeneric.description").stringValue =
                "Mateo ha perdido sus 3 corazones ante los peligros de la frontera.\n\n🛡️ <b>Consejo:</b> Protege tu salud y administra bien tus movimientos.";
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    public static void PlaceEnemiesAndResources()
    {
        SetupAnimacionesEditor.SetupPoliceAndK9Prefabs();

        foreach (string rootName in new[] { "EnemiesAndHazards", "Pickups" })
        {
            GameObject old = FindRoot(rootName);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform enemies = new GameObject("EnemiesAndHazards").transform;
        Transform policeGroup = CreateGroup("Policias", enemies);
        Transform dogGroup = CreateGroup("Perros_K9", enemies);
        Transform faunaGroup = CreateGroup("Fauna_Nociva", enemies);

        GameObject policePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SetupAnimacionesEditor.POLICE_PREFAB);
        for (int i = 0; i < PoliceRoutes.Length; i++)
        {
            Vector2[] route = PoliceRoutes[i];
            string zone = route[0].y < DESERT_TOP_Y ? "Desierto" : "Frontera";
            GameObject police = SpawnCreature(policePrefab, policeGroup, $"Policia_{i + 1}_{zone}", route[0]);
            SetRoute(police, route);
            PatrolAI policePatrol = police.GetComponent<PatrolAI>();
            policePatrol.moveSpeed = 0.55f;
            policePatrol.waitTimeAtWaypoint = 3f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(policePatrol);
        }

        foreach (var (breed, a, b) in DogRoutes)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SetupAnimacionesEditor.K9Prefab(breed));
            string zone = a.y < DESERT_TOP_Y ? "Desierto" : "Frontera";
            GameObject dog = SpawnCreature(prefab, dogGroup, $"Perro_K9_{breed}_{zone}", a);
            SetRoute(dog, new[] { a, b });

            PatrolAI patrol = dog.GetComponent<PatrolAI>();
            patrol.moveSpeed = 0.6f;
            GuardDogAI dogAI = dog.GetComponent<GuardDogAI>();
            dogAI.detectionRadius = 2.8f;
            dogAI.biteRadius = 0.65f;
            dogAI.coyoteAlertDuration = 5.0f;
            dogAI.calmDownDelay = 0.8f;
            PrefabUtility.RecordPrefabInstancePropertyModifications(patrol);
            PrefabUtility.RecordPrefabInstancePropertyModifications(dogAI);
        }

        var faunaCount = new Dictionary<string, int>();
        foreach (var (type, x, y) in FaunaSpots)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/Hazard_{type}.prefab");
            faunaCount[type] = faunaCount.TryGetValue(type, out int n) ? n + 1 : 1;
            GameObject creature = SpawnCreature(prefab, faunaGroup, $"{type}_{faunaCount[type]}", new Vector2(x, y));

            // Que no salgan del desierto
            var so = new SerializedObject(creature.GetComponent<HazardFauna>());
            so.FindProperty("minMapBounds").vector2Value = new Vector2(-2.6f, 0.9f);
            so.FindProperty("maxMapBounds").vector2Value = new Vector2(2.6f, DESERT_TOP_Y);
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        Transform pickups = new GameObject("Pickups").transform;
        var groups = new Dictionary<string, Transform>
        {
            ["Tuna"] = CreateGroup("Tunas", pickups),
            ["Cantimplora"] = CreateGroup("Cantimploras", pickups),
            ["Botella"] = CreateGroup("Botellas", pickups),
        };
        int canteens = 0;
        foreach (var (type, x, y) in ResourceSpots)
        {
            string prefabName = type == "Tuna" ? "Pickup_Tuna"
                : type == "Botella" ? "Pickup_Botella"
                : $"Pickup_Cantimplora_{(canteens++ % 2) + 1}";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Items/{prefabName}.prefab");
            var item = (GameObject)PrefabUtility.InstantiatePrefab(prefab, groups[type]);
            item.transform.position = new Vector3(x, y, 0f);
        }

        Debug.Log($"[Nivel3] {PoliceRoutes.Length} policías, {DogRoutes.Length} perros K-9, {FaunaSpots.Length} criaturas y {ResourceSpots.Length} recursos colocados.");
    }

    private static Transform CreateGroup(string name, Transform parent)
    {
        var group = new GameObject(name).transform;
        group.SetParent(parent, false);
        return group;
    }

    private static GameObject SpawnCreature(GameObject prefab, Transform parent, string name, Vector2 position)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.name = name;
        go.transform.position = new Vector3(position.x, position.y, 0f);

        // Profundidad por los pies, igual que en el Nivel 1 (pasan delante o detrás de los obstáculos)
        SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
        sr.sortingLayerName = "Environment";
        YSort2D ysort = go.GetComponent<YSort2D>();
        if (ysort == null) ysort = go.AddComponent<YSort2D>();
        ysort.isStatic = false;
        ysort.useBottomBounds = false;
        ysort.yOffset = 0f;
        ysort.baseOrder = 5000;
        ysort.UpdateSortingOrder();
        return go;
    }

    private static void SetRoute(GameObject creature, Vector2[] route)
    {
        Transform wpRoot = creature.transform.Find("Waypoints");
        if (wpRoot == null)
        {
            wpRoot = new GameObject("Waypoints").transform;
            wpRoot.SetParent(creature.transform, false);
        }

        var points = new List<Transform>();
        for (int i = 0; i < route.Length; i++)
        {
            Transform wp = wpRoot.Find($"WP_{i}");
            if (wp == null)
            {
                wp = new GameObject($"WP_{i}").transform;
                wp.SetParent(wpRoot, false);
            }
            wp.position = new Vector3(route[i].x, route[i].y, 0f);
            points.Add(wp);
        }

        PatrolAI patrol = creature.GetComponent<PatrolAI>();
        patrol.SetWaypoints(points, PatrolAI.PatrolMode.PingPong);
        PrefabUtility.RecordPrefabInstancePropertyModifications(patrol);
    }

    private static void RegisterInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        if (scenes.Any(s => s.path == SCENE)) return;
        scenes.Add(new EditorBuildSettingsScene(SCENE, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void SetupMenuThumbnail()
    {
        Sprite thumb = AssetDatabase.LoadAssetAtPath<Sprite>(THUMBNAIL);
        if (thumb == null) return;

        var scene = EditorSceneManager.OpenScene(MENU_SCENE, OpenSceneMode.Single);
        Transform frame = scene.GetRootGameObjects()
            .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(t => t.name == "ThumbFrame" && t.parent != null && t.parent.name == "Card_Level3");
        if (frame == null) { Debug.LogWarning("[Nivel3] No se encontró la tarjeta Card_Level3 del menú."); return; }

        Image img = frame.GetComponent<Image>();
        img.sprite = thumb;
        img.color = Color.white;
        img.preserveAspect = false;
        Transform lockIcon = frame.Find("LockIcon");
        if (lockIcon != null) Object.DestroyImmediate(lockIcon.gameObject);
        EditorUtility.SetDirty(img);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    // =========================================================================
    // VERIFICACIÓN (colisiones, hueco del muro, entrada de la malla y coberturas)
    // =========================================================================

    public static int VerifyLevel3()
    {
        Physics2D.SyncTransforms();
        int problems = 0;

        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        CapsuleCollider2D feet = player.GetComponent<CapsuleCollider2D>();
        Vector2 feetSize = Vector2.Scale(feet.size, player.transform.lossyScale);
        Bounds body = player.GetComponent<SpriteRenderer>().bounds;

        bool Blocked(float x, float fromY, float toY)
        {
            RaycastHit2D[] hits = Physics2D.CapsuleCastAll(new Vector2(x, fromY), feetSize, CapsuleDirection2D.Horizontal, 0f, Vector2.up, toY - fromY);
            return hits.Any(h => !h.collider.isTrigger && h.collider.attachedRigidbody == null);
        }

        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel3][OK] {message}");
            else { Debug.LogWarning($"[Nivel3][FALLA] {message}"); problems++; }
        }

        // Muro: solo se cruza por el hueco
        float gapX = (GAP_LEFT + GAP_RIGHT) * 0.5f;
        Expect(!Blocked(gapX, WALL_BASE_Y - 0.6f, WALL_BASE_Y + 1.2f), "El hueco del muro deja pasar a Mateo");
        foreach (float x in new[] { -2.4f, -1.2f, -0.5f, 0.7f, 1.6f, 2.5f })
            Expect(Blocked(x, WALL_BASE_Y - 0.6f, WALL_BASE_Y + 1.2f), $"El muro bloquea en x={x}");

        // Cada obstáculo es sólido en su base y las coberturas ocultan a Mateo agachado del lado visible
        foreach (YSort2D obstacle in Object.FindObjectsByType<YSort2D>().Where(o => o.isStatic && o.transform.parent != null && o.transform.parent.name == "Obstaculos_Nivel3"))
        {
            SpriteRenderer sr = obstacle.GetComponent<SpriteRenderer>();
            Bounds b = sr.bounds;
            float solidX = b.center.x + (obstacle.name.StartsWith("poste_luz") ? LampPole.x * obstacle.transform.lossyScale.x : 0f);
            Expect(Blocked(solidX, b.min.y - 0.5f, b.min.y + 0.4f), $"{obstacle.name} en ({b.center.x:F2}, {b.min.y:F2}) bloquea el paso");

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

        // Rutas de policías y perros libres de obstáculos (con el ancho del cuerpo)
        var routes = PoliceRoutes.Select((r, i) => ($"Policía {i + 1}", r))
            .Concat(DogRoutes.Select(d => ($"Perro {d.breed}", new[] { d.a, d.b })));
        foreach (var (who, route) in routes)
        {
            for (int i = 0; i < route.Length - 1; i++)
            {
                Vector2 dir = route[i + 1] - route[i];
                RaycastHit2D[] hits = Physics2D.CircleCastAll(route[i], 0.12f, dir.normalized, dir.magnitude);
                Collider2D blocker = hits.Select(h => h.collider)
                    .FirstOrDefault(c => !c.isTrigger && c.attachedRigidbody == null && c.GetComponentInParent<PatrolAI>() == null);
                Expect(blocker == null, $"{who}: ruta libre{(blocker != null ? $" (choca con {blocker.name})" : "")}");
            }
        }

        // Recursos fuera de los obstáculos
        foreach (var (type, x, y) in ResourceSpots)
        {
            Collider2D inside = Physics2D.OverlapCircleAll(new Vector2(x, y), 0.15f)
                .FirstOrDefault(c => !c.isTrigger && c.attachedRigidbody == null && c.GetComponentInParent<PatrolAI>() == null);
            Expect(inside == null, $"{type} en ({x:F2}, {y:F2}) accesible");
        }

        int beams = Object.FindObjectsByType<SearchlightTower2D>().Count(b => b.enabled);
        int lightZones = Object.FindObjectsByType<LightZone2D>().Length;
        int lamps = Layout.Count(l => l.name == "poste_luz");
        Expect(beams == Searchlights.Length, $"Reflectores activos: {beams} de {Searchlights.Length}");
        Expect(lightZones == lamps, $"Zonas iluminadas: {lightZones} de {lamps} lámparas");
        Expect(Object.FindAnyObjectByType<AlarmSystem2D>() != null, "Sistema de alarma en la escena");

        Debug.Log($"[Nivel3] Verificación terminada con {problems} problemas.");
        return problems;
    }

    // =========================================================================
    // CAPTURAS (para revisar el aspecto nocturno sin entrar a Play)
    // =========================================================================

    /// <summary>Solo captura la escena ya construida (Unity -batchmode -executeMethod SetupNivel3Editor.CaptureFromCommandLine).</summary>
    public static void CaptureFromCommandLine()
    {
        EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);
        VerifyLevel3();
        CapturePreviews(Path.GetFullPath(Path.Combine(Application.dataPath, "../../otros/nivel3/capturas")));
    }

    public static void CapturePreviews(string folder)
    {
        Directory.CreateDirectory(folder);
        Camera cam = Camera.main;
        if (cam == null) { Debug.LogWarning("[Nivel3] No hay cámara para capturar."); return; }

        const int width = 540, height = 960;
        cam.orthographic = true;
        cam.orthographicSize = 5.72f / 2f / (width / (float)height);
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

        var beamUpdate = typeof(SearchlightTower2D).GetMethod("Update", flags);
        foreach (SearchlightTower2D beam in Object.FindObjectsByType<SearchlightTower2D>()) beamUpdate?.Invoke(beam, null);

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
            File.WriteAllBytes(Path.Combine(folder, $"nivel3_{index++}.png"), tex.EncodeToPNG());
            if (camY < y) break;
        }
        cam.targetTexture = null;
        RenderTexture.active = null;
        Object.DestroyImmediate(rt);
        Object.DestroyImmediate(tex);
        Debug.Log($"[Nivel3] Capturas guardadas en {folder}");
    }
}
