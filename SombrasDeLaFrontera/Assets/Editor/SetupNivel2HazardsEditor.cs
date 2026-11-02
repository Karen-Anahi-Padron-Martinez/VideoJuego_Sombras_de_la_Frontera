using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Nivel 2 "Río Bravo" (Issue: fauna letal, corrientes de agua y consumibles flotantes).
/// 1. Rebana las hojas del río detectando cada figura (pejelagarto, cocodrilo, serpiente de agua, consumibles y corrientes)
///    y ajusta sus Pixels Per Unit para que se vean a escala con Mateo.
/// 2. Crea las animaciones (Walk/Attack para CreatureAnimator) y los prefabs de fauna, consumibles y corrientes.
/// 3. Los distribuye en carriles del río sin encimarse entre sí (y deja libres las orillas).
/// </summary>
public static partial class SetupNivel2Editor
{
    private const string RIVER_SPRITES = "Assets/Sprites/Level2_River/";
    private const string GAR_SHEET = RIVER_SPRITES + "Fauna/pejelagarto_sheet.png";
    private const string CROC_SHEET = RIVER_SPRITES + "Fauna/cocodrilo_sheet.png";
    private const string OBSTACLES_SHEET = RIVER_SPRITES + "Obstacles/obstaculos_rio_sheet.png";
    private const string ITEMS_SHEET = RIVER_SPRITES + "Items/consumibles_rio_sheet.png";
    private const string CURRENTS_SHEET = RIVER_SPRITES + "Effects/corrientes_sheet.png";
    private const string CURRENTS_SOFT_SHEET = RIVER_SPRITES + "Effects/corrientes_suave.png";  // Generada: solo espuma y flechas
    private const string PREFAB_DIR = "Assets/Prefabs/Level2_River";
    private const string ANIM_DIR = "Assets/Animations/Level2";

    // Límites para la fauna: dentro del agua y sin pisar las orillas
    private static readonly Vector2 FaunaMinBounds = new Vector2(-2.7f, WATER_START_Y + 0.3f);
    private static readonly Vector2 FaunaMaxBounds = new Vector2(2.7f, WATER_END_Y - 0.3f);

    private struct FaunaSpot
    {
        public string prefab; public float y, halfWidth, speed, amplitude, frequency; public bool startLeft;
        public FaunaSpot(string prefab, float y, float halfWidth, float speed, bool startLeft, float amplitude = 0f, float frequency = 0f)
        {
            this.prefab = prefab; this.y = y; this.halfWidth = halfWidth; this.speed = speed; this.startLeft = startLeft;
            this.amplitude = amplitude; this.frequency = frequency;
        }
    }

    private struct CurrentSpot
    {
        public string name; public bool counter; public Rect area; public Vector2 flow;
        public CurrentSpot(string name, bool counter, Rect area, Vector2 flow) { this.name = name; this.counter = counter; this.area = area; this.flow = flow; }
    }

    // Recorrido del río de sur a norte: primero una corriente suave para aprender, luego fauna y contracorrientes alternadas
    private static readonly FaunaSpot[] FaunaLayout =
    {
        new FaunaSpot("Fauna_Pejelagarto", 7.5f, 1.9f, 1.3f, false),
        new FaunaSpot("Fauna_SerpienteAgua", 11.0f, 2.0f, 1.05f, true, 0.45f, 0.6f),
        new FaunaSpot("Fauna_Cocodrilo", 17.0f, 1.8f, 0.85f, false),
        new FaunaSpot("Fauna_Pejelagarto", 23.5f, 2.0f, 1.6f, true),
        new FaunaSpot("Fauna_SerpienteAgua", 26.5f, 2.0f, 1.25f, false, 0.5f, 0.75f),
        new FaunaSpot("Fauna_Pejelagarto", 30.5f, 1.9f, 1.9f, false),
    };

    private static readonly CurrentSpot[] CurrentLayout =
    {
        new CurrentSpot("Corriente_Lateral_Derecha", false, new Rect(-MAP_HALF_W, 4.4f, MAP_WIDTH, 1.2f), new Vector2(1.3f, 0f)),
        new CurrentSpot("Contracorriente_1", true, new Rect(-MAP_HALF_W, 13.0f, MAP_WIDTH, 1.6f), new Vector2(0f, -1.1f)),
        new CurrentSpot("Corriente_Lateral_Izquierda", false, new Rect(-MAP_HALF_W, 19.35f, MAP_WIDTH, 1.3f), new Vector2(-1.5f, 0f)),
        new CurrentSpot("Contracorriente_2", true, new Rect(-MAP_HALF_W, 27.7f, MAP_HALF_W, 1.4f), new Vector2(0f, -1.3f)),
    };

    private static readonly (string prefab, Vector2 pos)[] PickupLayout =
    {
        ("Pickup_Bidon", new Vector2(-2.0f, 8.6f)),
        ("Pickup_BolsaImpermeable", new Vector2(2.2f, 16.3f)),
        ("Pickup_Carrizo", new Vector2(-1.6f, 18.4f)),
        ("Pickup_Carrizo", new Vector2(-0.2f, 25.4f)),
        ("Pickup_Bidon", new Vector2(1.8f, 25.0f)),
        ("Pickup_BolsaImpermeable", new Vector2(-1.9f, 31.8f)),
        // Agua para la sed: nadar cansa (varias en los huecos de los alambrados)
        ("Pickup_Botella_Rio", new Vector2(-0.6f, 2.4f)),
        ("Pickup_Cantimplora_Rio", new Vector2(2.2f, 8.3f)),
        ("Pickup_Botella_Rio", new Vector2(-2.2f, 15.1f)),
        ("Pickup_Botella_Rio", new Vector2(-1.0f, 19.0f)),
        ("Pickup_Botella_Rio", new Vector2(0f, 22.2f)),
        ("Pickup_Cantimplora_Rio", new Vector2(2.35f, 29.2f)),
        ("Pickup_Botella_Rio", new Vector2(0.2f, 31.9f)),
    };

    // =========================================================================
    // MENÚ
    // =========================================================================

    [MenuItem("Sombras de la Frontera/Nivel 2/3. Colocar Fauna, Corrientes y Consumibles")]
    public static void PlaceHazardsMenu()
    {
        if (!EditorSceneManagerSave()) return;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SCENE, UnityEditor.SceneManagement.OpenSceneMode.Single);
        PlaceHazardsAndPickups();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        int problems = VerifyLevel2();
        EditorUtility.DisplayDialog("Nivel 2", problems == 0 ? "Fauna, corrientes y consumibles colocados sin problemas." : $"{problems} problema(s). Revisa la consola.", "OK");
    }

    private static bool EditorSceneManagerSave() => UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();

    // =========================================================================
    // CONSTRUCCIÓN
    // =========================================================================

    public static void PlaceHazardsAndPickups()
    {
        BuildHazardPrefabs();

        foreach (string group in new[] { "Fauna_Rio", "Corrientes", "Consumibles" })
        {
            GameObject old = FindRoot(group);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform faunaRoot = new GameObject("Fauna_Rio").transform;
        Transform currentsRoot = new GameObject("Corrientes").transform;
        Transform pickupsRoot = new GameObject("Consumibles").transform;

        for (int i = 0; i < CurrentLayout.Length; i++)
        {
            CurrentSpot spot = CurrentLayout[i];
            GameObject zone = Instantiate(spot.counter ? "Corriente_Contracorriente" : "Corriente_Lateral", currentsRoot, spot.area.center);
            if (zone == null) continue;
            zone.name = spot.name;
            zone.GetComponent<BoxCollider2D>().size = spot.area.size;
            SpriteRenderer water = zone.GetComponentInChildren<SpriteRenderer>();
            FitCurrentWater(water, spot.area.size);
            water.flipX = spot.flow.x < 0f;
            var so = new SerializedObject(zone.GetComponent<CurrentZone2D>());
            so.FindProperty("flow").vector2Value = spot.flow;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        for (int i = 0; i < FaunaLayout.Length; i++)
        {
            FaunaSpot spot = FaunaLayout[i];
            GameObject creature = Instantiate(spot.prefab, faunaRoot, new Vector2(0f, spot.y));
            if (creature == null) continue;
            creature.name = $"{spot.prefab.Replace("Fauna_", "")}_{i + 1}";
            creature.GetComponentInChildren<SpriteRenderer>().flipX = spot.startLeft;
            var so = new SerializedObject(creature.GetComponent<HazardFauna>());
            so.FindProperty("patrolHalfWidth").floatValue = spot.halfWidth;
            so.FindProperty("moveSpeed").floatValue = spot.speed;
            if (spot.amplitude > 0f)
            {
                so.FindProperty("zigzagAmplitude").floatValue = spot.amplitude;
                so.FindProperty("zigzagFrequency").floatValue = spot.frequency;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        for (int i = 0; i < PickupLayout.Length; i++)
        {
            GameObject item = Instantiate(PickupLayout[i].prefab, pickupsRoot, PickupLayout[i].pos);
            if (item == null) continue;
            item.name = $"{PickupLayout[i].prefab.Replace("Pickup_", "")}_{i + 1}";
        }

        Debug.Log($"<color=green><b>[Nivel2]</b> Colocados {FaunaLayout.Length} animales, {CurrentLayout.Length} corrientes y {PickupLayout.Length} consumibles.</color>");
    }

    private static GameObject Instantiate(string prefabName, Transform parent, Vector2 position)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/{prefabName}.prefab");
        if (prefab == null)
        {
            Debug.LogError($"[Nivel2] No se pudo colocar {prefabName}: el prefab no existe (revisa el rebanado de su hoja).");
            return null;
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        go.transform.position = position;
        return go;
    }

    // =========================================================================
    // SPRITES, ANIMACIONES Y PREFABS
    // =========================================================================

    private static void BuildHazardPrefabs()
    {
        AssetDatabase.Refresh();
        EnsureFolder(ANIM_DIR);

        // Rebanado: (hoja, filas esperadas, ancho/alto objetivo en unidades del cuadro típico)
        SliceSheet(GAR_SHEET, "pejelagarto", new[] { 4, 4 }, targetWidth: 1.05f);
        SliceSheet(CROC_SHEET, "cocodrilo", new[] { 3, 3 }, targetWidth: 1.35f);
        SliceSheet(ITEMS_SHEET, "consumible", new[] { 3, 3 }, targetWidth: 0.5f);
        BuildSoftCurrentsSheet();
        SliceWaterSnakes();

        Dictionary<string, Sprite> gar = LoadSprites(GAR_SHEET);
        Dictionary<string, Sprite> croc = LoadSprites(CROC_SHEET);
        Dictionary<string, Sprite> snake = LoadSprites(OBSTACLES_SHEET);
        Dictionary<string, Sprite> items = LoadSprites(ITEMS_SHEET);
        Dictionary<string, Sprite> currents = LoadSprites(CURRENTS_SOFT_SHEET);

        // Pejelagarto: 6 cuadros nadando y 2 con el hocico abierto (mordida)
        Sprite[] garSwim = Pick(gar, "pejelagarto_r0_c0", "pejelagarto_r0_c1", "pejelagarto_r0_c2", "pejelagarto_r0_c3", "pejelagarto_r1_c0", "pejelagarto_r1_c1");
        Sprite[] garBite = Pick(gar, "pejelagarto_r1_c2", "pejelagarto_r1_c3", "pejelagarto_r1_c2");
        BuildFaunaPrefab("Fauna_Pejelagarto", "Pejelagarto", HazardFauna.FaunaType.Gar, HazardFauna.MovePattern.Patrol,
            garSwim, 8f, garBite, 8f, new Vector2(0.7f, 0.35f), chaseRadius: 1.2f);

        // Cocodrilo: 6 cuadros ondulando; la mordida es una embestida rápida con los mismos cuadros
        Sprite[] crocSwim = Pick(croc, "cocodrilo_r0_c0", "cocodrilo_r0_c1", "cocodrilo_r0_c2", "cocodrilo_r1_c0", "cocodrilo_r1_c1", "cocodrilo_r1_c2");
        Sprite[] crocBite = Pick(croc, "cocodrilo_r0_c1", "cocodrilo_r1_c2", "cocodrilo_r0_c0");
        BuildFaunaPrefab("Fauna_Cocodrilo", "Cocodrilo", HazardFauna.FaunaType.Crocodile, HazardFauna.MovePattern.Patrol,
            crocSwim, 6f, crocBite, 10f, new Vector2(0.7f, 0.3f), chaseRadius: 1.4f);

        // Serpiente de agua: 7 cuadros ondulando
        Sprite[] snakeSwim = Enumerable.Range(0, 7).Select(i => snake.TryGetValue($"serpiente_agua_c{i}", out Sprite s) ? s : null).Where(s => s != null).ToArray();
        Sprite[] snakeBite = snakeSwim.Length >= 2 ? new[] { snakeSwim[0], snakeSwim[1], snakeSwim[0] } : snakeSwim;
        BuildFaunaPrefab("Fauna_SerpienteAgua", "SerpienteAgua", HazardFauna.FaunaType.WaterSnake, HazardFauna.MovePattern.ZigZag,
            snakeSwim, 8f, snakeBite, 10f, new Vector2(0.6f, 0.35f), chaseRadius: 1.0f);

        // Consumibles: columna 0 bidón, 1 bolsa impermeable, 2 carrizo; filas = 2 cuadros de las ondas del agua
        BuildPickupPrefab("Pickup_Bidon", PickupItem.ItemType.Bidon, Pick(items, "consumible_r0_c0", "consumible_r1_c0"));
        BuildPickupPrefab("Pickup_BolsaImpermeable", PickupItem.ItemType.BolsaImpermeable, Pick(items, "consumible_r0_c1", "consumible_r1_c1"));
        BuildPickupPrefab("Pickup_Carrizo", PickupItem.ItemType.Carrizo, Pick(items, "consumible_r0_c2", "consumible_r1_c2"));
        BuildWaterPickup("Pickup_Botella_Rio", "Assets/Prefabs/Items/Pickup_Botella.prefab", PickupItem.ItemType.Botella, 15f);
        BuildWaterPickup("Pickup_Cantimplora_Rio", "Assets/Prefabs/Items/Pickup_Cantimplora_1.prefab", PickupItem.ItemType.Cantimplora, 30f);

        // Corrientes: fila 0 flujo lateral con flechas, fila 1 rápidos (contracorriente)
        BuildCurrentPrefab("Corriente_Lateral", CurrentZone2D.CurrentType.Lateral, new Vector2(1.3f, 0f),
            Pick(currents, "corriente_r0_c0", "corriente_r0_c1", "corriente_r0_c2", "corriente_r0_c3"));
        BuildCurrentPrefab("Corriente_Contracorriente", CurrentZone2D.CurrentType.CounterCurrent, new Vector2(0f, -1.1f),
            Pick(currents, "corriente_r1_c0", "corriente_r1_c1", "corriente_r1_c2", "corriente_r1_c3"));

        AssetDatabase.SaveAssets();
    }

    private static Sprite[] Pick(Dictionary<string, Sprite> sprites, params string[] names)
    {
        var result = new List<Sprite>();
        foreach (string n in names)
        {
            if (sprites.TryGetValue(n, out Sprite s)) result.Add(s);
            else Debug.LogWarning($"[Nivel2] Falta el sprite {n}.");
        }
        return result.ToArray();
    }

    private static void BuildFaunaPrefab(string prefabName, string creature, HazardFauna.FaunaType type, HazardFauna.MovePattern pattern,
        Sprite[] swim, float swimFps, Sprite[] bite, float biteFps, Vector2 bodyFactor, float chaseRadius)
    {
        if (swim.Length == 0) { Debug.LogError($"[Nivel2] Sin cuadros para {creature}."); return; }

        AnimationClip walkClip = CreateClip($"{ANIM_DIR}/{creature}_Walk.anim", swim, swimFps, true);
        AnimationClip attackClip = CreateClip($"{ANIM_DIR}/{creature}_Attack.anim", bite, biteFps, false);
        AnimatorController controller = BuildController($"{ANIM_DIR}/{creature}_Controller.controller", walkClip, attackClip);

        var root = new GameObject(prefabName);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = swim[0];
        sr.sortingLayerName = "Characters";
        visual.AddComponent<Animator>().runtimeAnimatorController = controller;
        visual.AddComponent<CreatureAnimator>();
        visual.AddComponent<YSort2D>();

        // Cuerpo para medir el contacto con Mateo (trigger: no lo empuja, solo lo muerde)
        CapsuleCollider2D body = root.AddComponent<CapsuleCollider2D>();
        body.direction = CapsuleDirection2D.Horizontal;
        body.size = Vector2.Scale(swim[0].bounds.size, bodyFactor);
        body.isTrigger = true;

        HazardFauna fauna = root.AddComponent<HazardFauna>();
        var so = new SerializedObject(fauna);
        so.FindProperty("faunaType").enumValueIndex = (int)type;
        so.FindProperty("movePattern").enumValueIndex = (int)pattern;
        so.FindProperty("damageAmount").intValue = 1;
        so.FindProperty("damageCooldown").floatValue = 1.6f;   // Mayor que la invulnerabilidad de Mateo (1.5 s)
        so.FindProperty("alertRadius").floatValue = 1.0f;
        so.FindProperty("attackRange").floatValue = 0.05f;     // Al contacto
        so.FindProperty("attackHitDelay").floatValue = 0.12f;
        so.FindProperty("attackReachBonus").floatValue = 0.15f;
        so.FindProperty("canWander").boolValue = false;
        so.FindProperty("turnDuration").floatValue = 0.5f;
        so.FindProperty("chaseRadius").floatValue = chaseRadius;        // Detecta a Mateo alrededor (también por detrás)
        so.FindProperty("chaseSpeedMultiplier").floatValue = 1.8f;
        so.FindProperty("chaseLeash").floatValue = 1.6f;
        so.FindProperty("minMapBounds").vector2Value = FaunaMinBounds;
        so.FindProperty("maxMapBounds").vector2Value = FaunaMaxBounds;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Botella o cantimplora del Nivel 1 en versión flotante para el río: mismo sprite y escala que en el desierto,
    /// con vaivén y la cantidad de agua indicada (nadar cansa).
    /// </summary>
    private static void BuildWaterPickup(string prefabName, string sourcePrefab, PickupItem.ItemType type, float waterAmount)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefab);
        SpriteRenderer sourceSr = source != null ? source.GetComponentInChildren<SpriteRenderer>() : null;
        if (sourceSr == null || sourceSr.sprite == null) { Debug.LogError($"[Nivel2] No se encontró el sprite de {sourcePrefab}."); return; }
        BuildPickupPrefab(prefabName, type, new[] { sourceSr.sprite }, source.transform.localScale.x, waterAmount);
    }

    private static void BuildPickupPrefab(string prefabName, PickupItem.ItemType type, Sprite[] frames, float scale = 1f, float waterAmount = 0f)
    {
        if (frames.Length == 0) { Debug.LogError($"[Nivel2] Sin cuadros para {prefabName}."); return; }

        var root = new GameObject(prefabName);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(scale, scale, 1f);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sortingLayerName = "Characters";
        visual.AddComponent<YSort2D>();

        CircleCollider2D col = root.AddComponent<CircleCollider2D>();
        col.radius = Mathf.Min(frames[0].bounds.size.x, frames[0].bounds.size.y) * 0.4f * scale;
        col.isTrigger = true;

        var pickup = new SerializedObject(root.AddComponent<PickupItem>());
        pickup.FindProperty("itemType").enumValueIndex = (int)type;
        pickup.FindProperty("waterAmount").floatValue = waterAmount;
        pickup.FindProperty("healthAmount").intValue = 1;
        pickup.FindProperty("speedBoost").floatValue = 1.5f;     // Bidón: +50% de velocidad
        pickup.FindProperty("boostDuration").floatValue = 5f;
        pickup.FindProperty("airBonusSeconds").floatValue = 3f;  // Carrizo: +3 s de aire
        pickup.ApplyModifiedPropertiesWithoutUndo();

        var bob = new SerializedObject(root.AddComponent<FloatingBob2D>());
        bob.FindProperty("visual").objectReferenceValue = sr;
        SetSpriteArray(bob.FindProperty("frames"), frames);
        bob.FindProperty("framesPerSecond").floatValue = 2f;
        bob.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
    }

    private static void BuildCurrentPrefab(string prefabName, CurrentZone2D.CurrentType type, Vector2 flow, Sprite[] frames)
    {
        if (frames.Length == 0) { Debug.LogError($"[Nivel2] Sin cuadros para {prefabName}."); return; }

        var root = new GameObject(prefabName);
        var water = new GameObject("Agua");
        water.transform.SetParent(root.transform, false);
        SpriteRenderer sr = water.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        FitCurrentWater(sr, new Vector2(MAP_WIDTH, 1.2f));
        sr.color = new Color(0.9f, 0.97f, 1f, 0.85f);  // Espuma y flechas casi blancas sobre el agua del fondo
        sr.sortingLayerName = "Background";
        sr.sortingOrder = 5;

        BoxCollider2D box = root.AddComponent<BoxCollider2D>();
        box.size = new Vector2(MAP_WIDTH, 1.2f);
        box.isTrigger = true;

        var so = new SerializedObject(root.AddComponent<CurrentZone2D>());
        so.FindProperty("currentType").enumValueIndex = (int)type;
        so.FindProperty("flow").vector2Value = flow;
        so.FindProperty("waterRenderer").objectReferenceValue = sr;
        SetSpriteArray(so.FindProperty("frames"), frames);
        so.FindProperty("framesPerSecond").floatValue = 6f;
        so.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
    }

    private static void SetSpriteArray(SerializedProperty array, Sprite[] sprites)
    {
        array.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static AnimationClip CreateClip(string path, Sprite[] frames, float fps, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();

        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        // Clave extra al final para que el último cuadro dure lo mismo que los demás
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++) keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = frames.Length / fps, value = loop ? frames[0] : frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);

        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.stopTime = frames.Length / fps;
        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (isNew) AssetDatabase.CreateAsset(clip, path);
        else EditorUtility.SetDirty(clip);
        return clip;
    }

    /// <summary>Controlador sin transiciones: HazardFauna/CreatureAnimator deciden qué estado se ve (Walk, Idle, Attack).</summary>
    private static AnimatorController BuildController(string path, AnimationClip walk, AnimationClip attack)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        AnimatorStateMachine sm = controller.layers[0].stateMachine;

        AnimatorState AddOrGet(string name, AnimationClip clip, Vector3 pos)
        {
            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
            if (state == null) state = sm.AddState(name, pos);
            state.motion = clip;
            state.writeDefaultValues = false;
            return state;
        }

        AnimatorState walkState = AddOrGet("Walk", walk, new Vector3(260f, 60f, 0f));
        AddOrGet("Idle", walk, new Vector3(260f, 150f, 0f));
        AddOrGet("Attack", attack, new Vector3(490f, 60f, 0f));
        sm.defaultState = walkState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // =========================================================================
    // REBANADO POR DETECCIÓN DE FIGURAS
    // =========================================================================

    /// <summary>
    /// Rebana una hoja con filas de figuras separadas por transparencia. Los nombres quedan como "prefijo_rFila_cColumna"
    /// (fila 0 = la de arriba). El PPU se calcula para que el cuadro típico mida targetWidth (o targetHeight) unidades.
    /// </summary>
    private static void SliceSheet(string texPath, string prefix, int[] expectedPerRow, float targetWidth = 0f, float targetHeight = 0f, bool fullRect = false, float inset = 0f, int mergeMargin = 9)
    {
        if (!File.Exists(texPath)) { Debug.LogError($"[Nivel2] No existe {texPath}."); return; }

        List<RectInt> boxes = DetectFigures(texPath, out int texW, out int texH, null, mergeMargin);
        List<List<RectInt>> rows = ArrangeRows(boxes);
        if (rows.Count != expectedPerRow.Length || rows.Where((r, i) => r.Count != expectedPerRow[i]).Any())
        {
            Debug.LogError($"[Nivel2] {texPath}: se esperaban filas de [{string.Join(",", expectedPerRow)}] figuras y se detectaron [{string.Join(",", rows.Select(r => r.Count))}].");
            return;
        }

        float ppu = ComputePpu(boxes, targetWidth, targetHeight);
        var cells = new List<(string name, Rect rect)>();
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < rows[r].Count; c++)
                cells.Add(($"{prefix}_r{r}_c{c}", Inset(Pad(rows[r][c], texW, texH), inset)));

        ConfigureSheetImport(texPath, ppu, fullRect);
        WriteSpriteRects(texPath, cells);
    }

    /// <summary>
    /// Genera corrientes_suave.png a partir de la hoja de corrientes: el gris del fondo se vuelve transparente según su brillo
    /// (solo quedan la espuma blanca y las flechas), se recortan los bordes irregulares de cada cuadro y se desvanecen
    /// los bordes de arriba y abajo. Así la corriente se funde con el río en lugar de verse como un bloque gris.
    /// Los cuadros quedan en las mismas posiciones y se rebanan como "corriente_rFila_cColumna".
    /// </summary>
    private static void BuildSoftCurrentsSheet()
    {
        if (!File.Exists(CURRENTS_SHEET)) { Debug.LogError($"[Nivel2] No existe {CURRENTS_SHEET}."); return; }

        List<RectInt> boxes = DetectFigures(CURRENTS_SHEET, out int texW, out int texH);
        List<List<RectInt>> rows = ArrangeRows(boxes);
        if (rows.Count != 2 || rows.Any(r => r.Count != 4))
        {
            Debug.LogError($"[Nivel2] Corrientes: se esperaban 2 filas de 4 cuadros y se detectaron [{string.Join(",", rows.Select(r => r.Count))}].");
            return;
        }

        var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        src.LoadImage(File.ReadAllBytes(CURRENTS_SHEET));
        Color32[] input = src.GetPixels32();
        Object.DestroyImmediate(src);
        var output = new Color32[input.Length];

        var cells = new List<(string name, Rect rect)>();
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Count; c++)
            {
                Rect cell = Inset(Pad(rows[r][c], texW, texH), 0.14f);
                cells.Add(($"corriente_r{r}_c{c}", cell));

                int x0 = (int)cell.xMin, x1 = (int)cell.xMax, y0 = (int)cell.yMin, y1 = (int)cell.yMax;
                float fade = cell.height * 0.3f;
                for (int y = y0; y < y1; y++)
                {
                    // Desvanecer arriba y abajo para que la corriente no tenga un borde recto
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(y - y0, y1 - 1 - y) / fade);
                    for (int x = x0; x < x1; x++)
                    {
                        Color32 p = input[y * texW + x];
                        float lum = (p.r + p.g + p.b) / (3f * 255f);
                        float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 0.92f, lum)) * edge * (p.a / 255f);
                        output[y * texW + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                    }
                }
            }
        }

        var dst = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        dst.SetPixels32(output);
        dst.Apply();
        File.WriteAllBytes(CURRENTS_SOFT_SHEET, dst.EncodeToPNG());
        Object.DestroyImmediate(dst);
        AssetDatabase.ImportAsset(CURRENTS_SOFT_SHEET, ImportAssetOptions.ForceUpdate);

        float ppu = ComputePpu(boxes, 0f, 1.0f);
        ConfigureSheetImport(CURRENTS_SOFT_SHEET, ppu, true);
        WriteSpriteRects(CURRENTS_SOFT_SHEET, cells);
    }

    /// <summary>De la hoja de troncos y fauna solo se usan las 7 serpientes de agua (cuarta fila de figuras).</summary>
    private static void SliceWaterSnakes()
    {
        if (!File.Exists(OBSTACLES_SHEET)) { Debug.LogError($"[Nivel2] No existe {OBSTACLES_SHEET}."); return; }

        // Las serpientes están entre el 18% y el 36% de la altura contando desde abajo (y de Unity hacia arriba);
        // se filtra antes de unir pedacitos para que no se peguen con el cardumen de peces de la fila de arriba
        List<RectInt> boxes = DetectFigures(OBSTACLES_SHEET, out int texW, out int texH, new Vector2(0.18f, 0.36f), mergeMargin: 3);
        List<RectInt> snakes = boxes.Where(b => b.width > 80).OrderBy(b => b.x).ToList();
        if (snakes.Count != 7)
        {
            Debug.LogError($"[Nivel2] Se esperaban 7 serpientes de agua y se detectaron {snakes.Count}: " +
                           string.Join(" ", boxes.Select(b => $"[x{b.x} y{b.y} {b.width}x{b.height}]")));
            return;
        }

        float ppu = ComputePpu(snakes, 0.85f, 0f);
        ConfigureSheetImport(OBSTACLES_SHEET, ppu, false);
        WriteSpriteRects(OBSTACLES_SHEET, snakes.Select((b, i) => ($"serpiente_agua_c{i}", Pad(b, texW, texH))).ToList());
    }

    private static float ComputePpu(List<RectInt> boxes, float targetWidth, float targetHeight)
    {
        if (targetHeight > 0f)
        {
            List<int> heights = boxes.Select(b => b.height).OrderBy(h => h).ToList();
            return Mathf.Round(heights[heights.Count / 2] / targetHeight);
        }
        List<int> widths = boxes.Select(b => b.width).OrderBy(w => w).ToList();
        return Mathf.Round(widths[widths.Count / 2] / targetWidth);
    }

    /// <summary>Recorta un porcentaje de cada lado (quita los bordes irregulares para que el mosaico no tenga costuras).</summary>
    private static Rect Inset(Rect r, float fraction)
    {
        if (fraction <= 0f) return r;
        float dx = Mathf.Round(r.width * fraction), dy = Mathf.Round(r.height * fraction);
        return new Rect(r.x + dx, r.y + dy, r.width - dx * 2f, r.height - dy * 2f);
    }

    /// <summary>
    /// Ajusta el agua de una corriente al área: una sola fila de cuadros que llena todo el alto
    /// (escala uniforme) y se repite a lo ancho.
    /// </summary>
    private static void FitCurrentWater(SpriteRenderer sr, Vector2 area)
    {
        float tileH = sr.sprite != null ? sr.sprite.bounds.size.y : 1f;
        float s = area.y / tileH;
        sr.transform.localScale = new Vector3(s, s, 1f);
        sr.size = new Vector2(area.x / s, tileH);
    }

    private static Rect Pad(RectInt b, int texW, int texH)
    {
        const int pad = 2;
        int x0 = Mathf.Max(0, b.xMin - pad), y0 = Mathf.Max(0, b.yMin - pad);
        int x1 = Mathf.Min(texW, b.xMax + pad), y1 = Mathf.Min(texH, b.yMax + pad);
        return new Rect(x0, y0, x1 - x0, y1 - y0);
    }

    /// <summary>
    /// Encuentra las figuras de la hoja: manchas opacas conectadas, unidas con sus pedacitos cercanos
    /// (ondas del agua, espuma, lengua de la serpiente). Coordenadas de Unity (y = 0 abajo).
    /// Con centerYRange (fracciones de la altura) solo se toman las manchas cuyo centro cae en esa franja;
    /// mergeMargin es la distancia máxima (px) para unir pedacitos con su figura.
    /// </summary>
    private static List<RectInt> DetectFigures(string texPath, out int width, out int height, Vector2? centerYRange = null, int mergeMargin = 9)
    {
        const byte alphaThreshold = 128;
        const int minFigureArea = 2500;

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(texPath));
        width = tex.width;
        height = tex.height;
        Color32[] px = tex.GetPixels32();
        Object.DestroyImmediate(tex);

        int w = width, h = height;
        var visited = new bool[px.Length];
        var boxes = new List<RectInt>();
        var stack = new Stack<int>();

        for (int i = 0; i < px.Length; i++)
        {
            if (visited[i] || px[i].a < alphaThreshold) continue;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, area = 0;
            visited[i] = true;
            stack.Push(i);
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                int x = p % w, y = p / w;
                area++;
                if (x < minX) minX = x; if (x > maxX) maxX = x;
                if (y < minY) minY = y; if (y > maxY) maxY = y;
                if (x > 0) Visit(p - 1);
                if (x < w - 1) Visit(p + 1);
                if (y > 0) Visit(p - w);
                if (y < h - 1) Visit(p + w);
            }
            if (area >= 12) boxes.Add(new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }

        void Visit(int q)
        {
            if (visited[q] || px[q].a < alphaThreshold) return;
            visited[q] = true;
            stack.Push(q);
        }

        if (centerYRange.HasValue)
        {
            float lo = centerYRange.Value.x * h, hi = centerYRange.Value.y * h;
            boxes.RemoveAll(b => b.center.y < lo || b.center.y > hi);
        }

        // Unir manchas cercanas en una sola figura
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (int i = 0; i < boxes.Count; i++)
            {
                RectInt grown = new RectInt(boxes[i].x - mergeMargin, boxes[i].y - mergeMargin, boxes[i].width + mergeMargin * 2, boxes[i].height + mergeMargin * 2);
                for (int j = boxes.Count - 1; j > i; j--)
                {
                    if (!grown.Overlaps(boxes[j])) continue;
                    RectInt a = boxes[i], b = boxes[j];
                    int x0 = Mathf.Min(a.xMin, b.xMin), y0 = Mathf.Min(a.yMin, b.yMin);
                    boxes[i] = new RectInt(x0, y0, Mathf.Max(a.xMax, b.xMax) - x0, Mathf.Max(a.yMax, b.yMax) - y0);
                    boxes.RemoveAt(j);
                    changed = true;
                    grown = new RectInt(boxes[i].x - mergeMargin, boxes[i].y - mergeMargin, boxes[i].width + mergeMargin * 2, boxes[i].height + mergeMargin * 2);
                }
            }
        }

        boxes.RemoveAll(b => b.width * b.height < minFigureArea);
        return boxes;
    }

    /// <summary>Agrupa las figuras en filas (de arriba hacia abajo) y ordena cada fila de izquierda a derecha.</summary>
    private static List<List<RectInt>> ArrangeRows(List<RectInt> boxes)
    {
        var rows = new List<List<RectInt>>();
        foreach (RectInt b in boxes.OrderByDescending(b => b.center.y))
        {
            List<RectInt> row = rows.LastOrDefault();
            if (row != null && Mathf.Abs(row.Average(r => r.center.y) - b.center.y) < row.Average(r => r.height) * 0.5f) row.Add(b);
            else rows.Add(new List<RectInt> { b });
        }
        return rows.Select(r => r.OrderBy(b => b.x).ToList()).ToList();
    }

    private static void ConfigureSheetImport(string texPath, float ppu, bool fullRect)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = ppu;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 2048;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        // Las corrientes se dibujan en mosaico (Tiled), que requiere malla rectangular
        settings.spriteMeshType = fullRect ? SpriteMeshType.FullRect : SpriteMeshType.Tight;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Agrega o actualiza sprites en la hoja sin tocar los cortes existentes (conserva sus IDs y referencias).
    /// </summary>
    private static void WriteSpriteRects(string texPath, List<(string name, Rect rect)> cells) =>
        WriteSpriteRects(texPath, cells.Select(c => (c.name, c.rect, new Vector2(0.5f, 0.5f))).ToList());

    /// <summary>Igual que la anterior, con un pivote propio para cada cuadro (fracciones del rectángulo).</summary>
    private static void WriteSpriteRects(string texPath, List<(string name, Rect rect, Vector2 pivot)> cells)
    {
        TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
        if (importer == null) return;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider dataProvider = factory.GetSpriteEditorDataProviderFromObject(importer);
        dataProvider.InitSpriteEditorDataProvider();

        List<SpriteRect> rects = dataProvider.GetSpriteRects().ToList();
        ISpriteNameFileIdDataProvider nameProvider = dataProvider.GetDataProvider<ISpriteNameFileIdDataProvider>();
        List<SpriteNameFileIdPair> pairs = nameProvider != null ? nameProvider.GetNameFileIdPairs().ToList() : new List<SpriteNameFileIdPair>();

        // Quitar los cortes automáticos que Unity crea al importar ("<archivo>_0", "<archivo>_1"...): no se usan
        string autoPrefix = Path.GetFileNameWithoutExtension(texPath) + "_";
        bool IsAutoSlice(string name) => name.StartsWith(autoPrefix) && int.TryParse(name.Substring(autoPrefix.Length), out _);
        // (los que se van a escribir se conservan aunque se llamen igual, p. ej. "swimup_0": así no cambia su ID)
        rects.RemoveAll(r => IsAutoSlice(r.name) && !cells.Any(c => c.name == r.name));
        pairs.RemoveAll(p => IsAutoSlice(p.name) && !cells.Any(c => c.name == p.name));

        foreach (var cell in cells)
        {
            SpriteRect spriteRect = rects.FirstOrDefault(r => r.name == cell.name);
            if (spriteRect == null)
            {
                spriteRect = new SpriteRect { name = cell.name, spriteID = GUID.Generate() };
                rects.Add(spriteRect);
            }

            spriteRect.rect = cell.rect;
            bool centered = Mathf.Approximately(cell.pivot.x, 0.5f) && Mathf.Approximately(cell.pivot.y, 0.5f);
            spriteRect.alignment = centered ? SpriteAlignment.Center : SpriteAlignment.Custom;
            spriteRect.pivot = cell.pivot;
            spriteRect.border = Vector4.zero;

            if (!pairs.Any(p => p.name == cell.name)) pairs.Add(new SpriteNameFileIdPair(cell.name, spriteRect.spriteID));
        }

        dataProvider.SetSpriteRects(rects.ToArray());
        if (nameProvider != null) nameProvider.SetNameFileIdPairs(pairs);
        dataProvider.Apply();
        importer.SaveAndReimport();
    }

    private static Dictionary<string, Sprite> LoadSprites(string texPath)
    {
        var dict = new Dictionary<string, Sprite>();
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(texPath))
        {
            if (obj is Sprite s) dict[s.name] = s;
        }
        return dict;
    }

    // =========================================================================
    // VERIFICACIÓN (fauna, corrientes y consumibles sin encimarse y dentro del agua)
    // =========================================================================

    private static int VerifyHazards()
    {
        int problems = 0;
        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel2][OK] {message}");
            else { Debug.LogWarning($"[Nivel2][FALLA] {message}"); problems++; }
        }

        HazardFauna[] fauna = Object.FindObjectsByType<HazardFauna>(FindObjectsSortMode.None);
        CurrentZone2D[] currents = Object.FindObjectsByType<CurrentZone2D>(FindObjectsSortMode.None);
        PickupItem[] pickups = Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None);

        Expect(fauna.Any(f => f.Type == HazardFauna.FaunaType.Gar), "Hay pejelagartos patrulleros");
        Expect(fauna.Any(f => f.Type == HazardFauna.FaunaType.WaterSnake), "Hay serpientes de agua en zigzag");
        Expect(currents.Any(c => c.Type == CurrentZone2D.CurrentType.Lateral && c.Flow.x > 0f) &&
               currents.Any(c => c.Type == CurrentZone2D.CurrentType.Lateral && c.Flow.x < 0f), "Hay corrientes laterales hacia ambos lados");
        Expect(currents.Any(c => c.Type == CurrentZone2D.CurrentType.CounterCurrent && c.Flow.y < 0f), "Hay contracorrientes que frenan el avance");
        foreach (PickupItem.ItemType t in new[] { PickupItem.ItemType.Bidon, PickupItem.ItemType.BolsaImpermeable, PickupItem.ItemType.Carrizo })
            Expect(pickups.Any(p => p.Type == t), $"Hay al menos un consumible {t}");
        int water = pickups.Count(p => p.Type == PickupItem.ItemType.Botella || p.Type == PickupItem.ItemType.Cantimplora);
        Expect(water >= 5 && pickups.Where(p => p.Type == PickupItem.ItemType.Botella || p.Type == PickupItem.ItemType.Cantimplora)
                                    .All(p => new SerializedObject(p).FindProperty("waterAmount").floatValue > 0f),
            $"Hay {water} botellas y cantimploras flotando para recuperar agua");

        // Cada animal: con animación de nado y mordida, cuerpo que muerde y carril completo dentro del agua
        var areas = new List<(string name, Rect rect)>();
        foreach (HazardFauna f in fauna)
        {
            var so = new SerializedObject(f);
            SpriteRenderer sr = f.GetComponentInChildren<SpriteRenderer>();
            Animator anim = f.GetComponentInChildren<Animator>();
            bool animated = sr != null && sr.sprite != null && anim != null && anim.runtimeAnimatorController != null &&
                            anim.runtimeAnimatorController.animationClips.Any(c => c.name.EndsWith("_Attack"));
            Expect(animated, $"{f.name} tiene sprite y animaciones de nado y mordida");
            Expect(f.GetComponent<Collider2D>().isTrigger && so.FindProperty("damageAmount").intValue == 1, $"{f.name} resta 1 corazón al contacto");

            float half = so.FindProperty("patrolHalfWidth").floatValue;
            float amp = (HazardFauna.MovePattern)so.FindProperty("movePattern").enumValueIndex == HazardFauna.MovePattern.ZigZag
                ? so.FindProperty("zigzagAmplitude").floatValue : 0f;
            Vector2 body = f.GetComponent<CapsuleCollider2D>().size;
            Vector2 p = f.transform.position;
            Rect lane = new Rect(p.x - half - body.x * 0.5f, p.y - amp - body.y * 0.5f, (half * 2f) + body.x, (amp * 2f) + body.y);
            Expect(lane.xMin >= -MAP_HALF_W - 0.2f && lane.xMax <= MAP_HALF_W + 0.2f && lane.yMin > WATER_START_Y && lane.yMax < WATER_END_Y,
                $"{f.name} nada dentro del río (y {lane.yMin:F1} a {lane.yMax:F1})");
            areas.Add((f.name, lane));
        }

        foreach (CurrentZone2D c in currents)
        {
            Bounds b = c.GetComponent<BoxCollider2D>().bounds;
            SpriteRenderer sr = c.GetComponentInChildren<SpriteRenderer>();
            Expect(sr != null && sr.sprite != null && sr.drawMode == SpriteDrawMode.Tiled, $"{c.name} muestra el agua animada de la corriente");
            Expect(b.min.y > WATER_START_Y && b.max.y < WATER_END_Y, $"{c.name} está dentro del agua");
            areas.Add((c.name, new Rect(b.min, b.size)));
        }

        foreach (PickupItem item in pickups)
        {
            CircleCollider2D col = item.GetComponent<CircleCollider2D>();
            SpriteRenderer sr = item.GetComponentInChildren<SpriteRenderer>();
            Expect(col != null && col.isTrigger && sr != null && sr.sprite != null && item.GetComponent<FloatingBob2D>() != null,
                $"{item.name} flota y se puede recoger");
            Vector2 p = item.transform.position;
            float r = col != null ? col.radius : 0.2f;
            areas.Add((item.name, new Rect(p.x - r, p.y - r, r * 2f, r * 2f)));
        }

        // Nada se encima: ni los carriles de la fauna, ni las corrientes, ni los consumibles entre sí
        for (int i = 0; i < areas.Count; i++)
            for (int j = i + 1; j < areas.Count; j++)
                if (areas[i].rect.Overlaps(areas[j].rect))
                {
                    Debug.LogWarning($"[Nivel2][FALLA] {areas[i].name} se encima con {areas[j].name}");
                    problems++;
                }
        problems += VerifyBoatsAndCovers(areas);
        Expect(true, $"Revisadas {areas.Count} zonas de fauna, corrientes, consumibles, lanchas y coberturas para que no se encimen");

        return problems;
    }
}
