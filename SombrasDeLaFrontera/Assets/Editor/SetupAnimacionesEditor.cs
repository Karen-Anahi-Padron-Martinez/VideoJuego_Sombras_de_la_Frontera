using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Genera todas las animaciones del Nivel 1 a partir de los sprite sheets:
/// 1. Corta cada hoja en una cuadrícula uniforme con el pivote en los pies (sprites "anim_*"),
///    así los cuadros no "brincan" al cambiar de animación. Los cortes originales se conservan.
/// 2. Crea los AnimationClips y un AnimatorController por criatura:
///    - Perros (Bulldog, Rottweiler, Pastor Alemán, Dóberman): Walk, Idle, Bark, Bite, Sleep.
///    - Coyote: Walk, Idle, Alert (señala con la linterna), Catch (atrapa a Mateo).
///    - Serpiente, Alacrán y Araña: Walk, Idle, Alert, Attack.
/// 3. Agrega a Mateo las animaciones Agacharse (Mateo_Crouch) y Caer (Mateo_Death), y reemplaza sus caminatas
///    (lateral, de frente y de espaldas) por cuadros con movimiento de piernas (mateo_walk_gen.png).
///    El coyote también camina con la hoja coyote_walk_gen.png (piernas alternando).
///    Ambas hojas se generaron con Docs/generar_caminatas.py a partir de los sprites originales.
/// 4. Configura los prefabs de enemigos (Animator, CreatureAnimator, icono de alerta y sombra)
///    y crea el prefab de la Araña (Hazard_Spider).
/// Las animaciones se disparan desde código (GuardDogAI, HazardFauna, CoyoteAnimation, PlayerController2D)
/// únicamente cuando la situación lo requiere.
/// </summary>
public static class SetupAnimacionesEditor
{
    private const string ENEMY_SPRITES = "Assets/Sprites/Enemies/";
    private const string MATEO_SHEET = "Assets/Sprites/Characters/mateo_part3.png";
    private const string MATEO_WALK_SHEET = "Assets/Sprites/Characters/mateo_walk_gen.png";
    private const string CLIPS_DIR = "Assets/Animations/Clips/Creatures";
    private const string MATEO_CLIPS_DIR = "Assets/Animations/Clips";
    private const string CONTROLLERS_DIR = "Assets/Animations/Animators";
    private const string MATEO_CONTROLLER = "Assets/Animations/Animators/Mateo_Controller.controller";
    private const string PREFAB_DIR = "Assets/Prefabs/Enemies";
    private const string SPIDER_PREFAB = PREFAB_DIR + "/Hazard_Spider.prefab";
    public const string SHADOW_SPRITE = "Assets/Sprites/Effects/BlobShadow.png";
    private const string UI_SHEET = "Assets/Sprites/UI/UI.png";

    // Hojas a cortar: (archivo, columnas, filas)
    private static readonly (string file, int cols, int rows)[] Sheets =
    {
        ("bulldog_part1", 6, 3), ("bulldog_part2", 6, 3),
        ("rottweiler_part1", 6, 3), ("rottweiler_part2", 6, 3),
        ("aleman_part1", 6, 3), ("aleman_part2", 6, 3),
        ("doberman_part1", 6, 3), ("doberman_part2", 6, 3),
        ("coyote_part1", 6, 3), ("coyote_part2", 6, 3), ("coyote_walk_gen", 6, 1),
        ("snake", 5, 4), ("scorpion", 6, 3), ("spider", 6, 3)
    };

    private struct AnimSpec
    {
        public string state; public string sheet; public int row; public float fps; public bool loop;
        public AnimSpec(string state, string sheet, int row, float fps, bool loop)
        {
            this.state = state; this.sheet = sheet; this.row = row; this.fps = fps; this.loop = loop;
        }
    }

    // Filas numeradas de arriba hacia abajo tal como se ven en la imagen
    private static AnimSpec[] DogAnims(string breed) => new[]
    {
        new AnimSpec("Idle",  breed + "_part1", 2, 5f,  true),
        new AnimSpec("Walk",  breed + "_part1", 0, 10f, true),
        new AnimSpec("Bark",  breed + "_part2", 0, 9f,  true),
        new AnimSpec("Bite",  breed + "_part2", 1, 12f, false),
        new AnimSpec("Sleep", breed + "_part2", 2, 3f,  true),
    };

    private static readonly Dictionary<string, AnimSpec[]> Creatures = new Dictionary<string, AnimSpec[]>
    {
        { "Bulldog", DogAnims("bulldog") },
        { "Rottweiler", DogAnims("rottweiler") },
        { "Aleman", DogAnims("aleman") },
        { "Doberman", DogAnims("doberman") },
        { "Coyote", new[]
            {
                new AnimSpec("Idle",  "coyote_part1", 1, 4f,  true),
                new AnimSpec("Walk",  "coyote_walk_gen", 0, 10f, true),
                new AnimSpec("Alert", "coyote_part2", 1, 8f,  true),
                new AnimSpec("Catch", "coyote_part2", 2, 10f, true),
            }
        },
        { "Snake", new[]
            {
                new AnimSpec("Idle",   "snake", 1, 3f,  true),
                new AnimSpec("Walk",   "snake", 0, 8f,  true),
                new AnimSpec("Alert",  "snake", 3, 8f,  true),
                new AnimSpec("Attack", "snake", 2, 12f, false),
            }
        },
        { "Scorpion", new[]
            {
                new AnimSpec("Idle",   "scorpion", 2, 4f,  true),
                new AnimSpec("Walk",   "scorpion", 0, 10f, true),
                new AnimSpec("Alert",  "scorpion", 2, 10f, true),
                new AnimSpec("Attack", "scorpion", 1, 12f, false),
            }
        },
        { "Spider", new[]
            {
                new AnimSpec("Idle",   "spider", 1, 4f,  true),
                new AnimSpec("Walk",   "spider", 0, 10f, true),
                new AnimSpec("Alert",  "spider", 1, 12f, true),
                new AnimSpec("Attack", "spider", 2, 12f, false),
            }
        },
    };

    // Cuadros de Mateo en mateo_part3.png (fila inferior): de pie, hincado, manos al suelo, boca abajo, tendido con polvo.
    // Línea del suelo en y = 20 px; el pivote queda 98 px arriba del suelo (igual que el centro del sprite de reposo de 196 px),
    // así Mateo no se desplaza al pasar de caminar a agacharse o caer.
    private static readonly RectInt[] MateoFrames =
    {
        new RectInt(5, 20, 97, 202),
        new RectInt(116, 23, 113, 182),
        new RectInt(228, 22, 118, 131),
        new RectInt(345, 20, 136, 102),
        new RectInt(484, 14, 200, 125),
    };
    private const float MATEO_PIVOT_Y_PX = 118f;
    private const float MATEO_PIVOT_ABOVE_FEET_PX = 98f;

    [MenuItem("Sombras de la Frontera/Sprint 2/1. Generar Animaciones (Perros, Coyotes, Fauna y Mateo)")]
    public static void GenerateAllAnimations()
    {
        try
        {
            AssetDatabase.StartAssetEditing();
            EnsureFolder(CLIPS_DIR);
            EnsureFolder(CONTROLLERS_DIR);
            EnsureFolder("Assets/Sprites/Effects");
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
        }

        EnsureSpriteImport(SHADOW_SPRITE, false);

        // 1. Cortes con pivote en los pies
        foreach (var sheet in Sheets)
        {
            string path = ENEMY_SPRITES + sheet.file + ".png";
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Animaciones] No se encontró la hoja {path}");
                continue;
            }
            SliceGrid(path, sheet.cols, sheet.rows, "anim_" + sheet.file);
        }
        SliceMateo();
        // Caminatas de Mateo: pivote 98 px sobre los pies, igual que el centro del sprite de reposo (196 px)
        if (File.Exists(MATEO_WALK_SHEET)) SliceGrid(MATEO_WALK_SHEET, 6, 3, "anim_mateo_walk_gen", MATEO_PIVOT_ABOVE_FEET_PX);

        // 2. Clips y controladores
        var controllers = new Dictionary<string, AnimatorController>();
        foreach (var creature in Creatures)
        {
            controllers[creature.Key] = BuildCreatureController(creature.Key, creature.Value);
        }
        SetupMateoController();

        // 3. Prefabs
        SetupCreaturePrefab($"{PREFAB_DIR}/Enemy_CoyotePatrol.prefab", "Coyote", controllers["Coyote"], 0.55f, root =>
        {
            if (root.GetComponent<CoyoteAnimation>() == null) root.AddComponent<CoyoteAnimation>();
        });

        SetupCreaturePrefab($"{PREFAB_DIR}/Enemy_GuardDog.prefab", DogBreedOf($"{PREFAB_DIR}/Enemy_GuardDog.prefab", "Bulldog"), null, 0.75f, null, controllers);
        SetupCreaturePrefab($"{PREFAB_DIR}/Enemy_GuardDog_2.prefab", DogBreedOf($"{PREFAB_DIR}/Enemy_GuardDog_2.prefab", "Rottweiler"), null, 0.75f, null, controllers);
        SetupCreaturePrefab($"{PREFAB_DIR}/Hazard_Snake.prefab", "Snake", controllers["Snake"], 0.8f, null);
        SetupCreaturePrefab($"{PREFAB_DIR}/Hazard_Scorpion.prefab", "Scorpion", controllers["Scorpion"], 0.75f, null);
        CreateSpiderPrefab(controllers["Spider"]);

        AssetDatabase.SaveAssets();
        Debug.Log("<color=green><b>[Animaciones]</b> ¡Listo! Clips, AnimatorControllers y prefabs de Perros, Coyotes, Serpientes, Alacranes, Arañas y Mateo configurados.</color>");
    }

    // =====================================================================
    // CORTE DE SPRITES
    // =====================================================================

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    public static void EnsureSpriteImport(string path, bool multiple)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; changed = true; }
        SpriteImportMode mode = multiple ? SpriteImportMode.Multiple : SpriteImportMode.Single;
        if (importer.spriteImportMode != mode) { importer.spriteImportMode = mode; changed = true; }
        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f)) { importer.spritePixelsPerUnit = 100f; changed = true; }
        if (!importer.alphaIsTransparency) { importer.alphaIsTransparency = true; changed = true; }

        if (!multiple)
        {
            // Sprites suaves (sombra): malla rectangular para no recortar el degradado
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                changed = true;
            }
        }

        if (changed) importer.SaveAndReimport();
    }

    /// <summary>
    /// Detecta las franjas horizontales con contenido (una por fila de animación) leyendo el PNG.
    /// Devuelve las franjas de abajo hacia arriba (coordenadas de Unity, y = 0 abajo).
    /// </summary>
    private static List<Vector2Int> DetectRowBands(Color32[] pixels, int width, int height, int expectedRows)
    {
        var bands = new List<Vector2Int>();
        int start = -1, last = -1;
        for (int y = 0; y < height; y++)
        {
            bool has = false;
            int rowOffset = y * width;
            for (int x = 0; x < width; x++)
            {
                if (pixels[rowOffset + x].a > 40) { has = true; break; }
            }

            if (has)
            {
                if (start < 0) start = y;
                last = y;
            }
            else if (start >= 0 && y - last > 6)
            {
                bands.Add(new Vector2Int(start, last));
                start = -1;
            }
        }
        if (start >= 0) bands.Add(new Vector2Int(start, last));
        bands.RemoveAll(b => b.y - b.x <= 12);

        if (bands.Count != expectedRows)
        {
            // Respaldo: cuadrícula uniforme
            bands.Clear();
            float rowH = height / (float)expectedRows;
            for (int r = 0; r < expectedRows; r++)
                bands.Add(new Vector2Int(Mathf.RoundToInt(r * rowH) + 3, Mathf.RoundToInt((r + 1) * rowH) - 4));
        }
        return bands;
    }

    private static void SliceGrid(string texPath, int cols, int rows, string prefix, float pivotAboveFeetPx = 0f)
    {
        EnsureSpriteImport(texPath, true);

        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(texPath));
        int width = tex.width, height = tex.height;
        Color32[] pixels = tex.GetPixels32();
        Object.DestroyImmediate(tex);

        List<Vector2Int> bands = DetectRowBands(pixels, width, height, rows);
        float cellW = width / (float)cols;

        var cells = new List<(string name, Rect rect, Vector2 pivot)>();
        for (int bandIndex = 0; bandIndex < bands.Count; bandIndex++)
        {
            int row = rows - 1 - bandIndex; // fila contada desde arriba
            int y0 = Mathf.Max(0, bands[bandIndex].x - 3);
            int y1 = Mathf.Min(height, bands[bandIndex].y + 4);
            for (int c = 0; c < cols; c++)
            {
                int x0 = Mathf.RoundToInt(c * cellW);
                int x1 = Mathf.RoundToInt((c + 1) * cellW);
                var rect = new Rect(x0, y0, x1 - x0, y1 - y0);
                // Pivote en la línea de los pies (fondo de la franja) y centrado en la celda
                var pivot = new Vector2(0.5f, (bands[bandIndex].x + pivotAboveFeetPx - y0) / (float)(y1 - y0));
                cells.Add(($"{prefix}_r{row}_c{c}", rect, pivot));
            }
        }

        WriteSpriteRects(texPath, cells);
    }

    private static void SliceMateo()
    {
        if (!File.Exists(MATEO_SHEET)) return;
        EnsureSpriteImport(MATEO_SHEET, true);

        var cells = new List<(string name, Rect rect, Vector2 pivot)>();
        for (int i = 0; i < MateoFrames.Length; i++)
        {
            RectInt r = MateoFrames[i];
            var pivot = new Vector2(0.5f, (MATEO_PIVOT_Y_PX - r.y) / r.height);
            cells.Add(($"anim_mateo_c{i}", new Rect(r.x, r.y, r.width, r.height), pivot));
        }
        WriteSpriteRects(MATEO_SHEET, cells);
    }

    /// <summary>
    /// Agrega o actualiza sprites en la hoja sin tocar los cortes existentes (conserva sus IDs y referencias).
    /// </summary>
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

        foreach (var cell in cells)
        {
            SpriteRect spriteRect = rects.FirstOrDefault(r => r.name == cell.name);
            if (spriteRect == null)
            {
                spriteRect = new SpriteRect { name = cell.name, spriteID = GUID.Generate() };
                rects.Add(spriteRect);
            }

            spriteRect.rect = cell.rect;
            spriteRect.alignment = SpriteAlignment.Custom;
            spriteRect.pivot = cell.pivot;
            spriteRect.border = Vector4.zero;

            if (!pairs.Any(p => p.name == cell.name))
            {
                pairs.Add(new SpriteNameFileIdPair(cell.name, spriteRect.spriteID));
            }
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

    private static Sprite[] RowFrames(string sheet, int row) => RowFramesAt(ENEMY_SPRITES + sheet + ".png", sheet, row);

    private static Sprite[] RowFramesAt(string texPath, string sheet, int row)
    {
        var sprites = LoadSprites(texPath);
        string prefix = $"anim_{sheet}_r{row}_c";
        return sprites.Where(kv => kv.Key.StartsWith(prefix))
                      .OrderBy(kv => int.Parse(kv.Key.Substring(prefix.Length)))
                      .Select(kv => kv.Value).ToArray();
    }

    // =====================================================================
    // CLIPS Y CONTROLADORES
    // =====================================================================

    private static AnimationClip CreateClip(string path, Sprite[] frames, float fps, bool loop)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        bool isNew = clip == null;
        if (isNew) clip = new AnimationClip();

        clip.frameRate = fps;
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

        // Clave extra al final para que el último cuadro dure lo mismo que los demás
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
        {
            keys[i] = new ObjectReferenceKeyframe { time = i / fps, value = frames[i] };
        }
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

    private static AnimatorController BuildCreatureController(string creature, AnimSpec[] anims)
    {
        string ctrlPath = $"{CONTROLLERS_DIR}/{creature}_Controller.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ctrlPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idleState = null;

        for (int i = 0; i < anims.Length; i++)
        {
            AnimSpec spec = anims[i];
            Sprite[] frames = RowFrames(spec.sheet, spec.row);
            if (frames.Length == 0)
            {
                Debug.LogWarning($"[Animaciones] Sin cuadros para {creature}_{spec.state} ({spec.sheet} fila {spec.row}).");
                continue;
            }

            AnimationClip clip = CreateClip($"{CLIPS_DIR}/{creature}_{spec.state}.anim", frames, spec.fps, spec.loop);

            AnimatorState state = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == spec.state);
            if (state == null) state = sm.AddState(spec.state, new Vector3(260f + (i % 3) * 230f, 60f + (i / 3) * 90f, 0f));
            state.motion = clip;
            state.writeDefaultValues = false;

            if (spec.state == "Idle") idleState = state;
        }

        if (idleState != null) sm.defaultState = idleState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void SetupMateoController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(MATEO_CONTROLLER);
        if (controller == null)
        {
            Debug.LogWarning("[Animaciones] No se encontró Mateo_Controller.controller");
            return;
        }

        var sprites = LoadSprites(MATEO_SHEET);
        Sprite[] all = Enumerable.Range(0, MateoFrames.Length)
                                 .Select(i => sprites.TryGetValue($"anim_mateo_c{i}", out Sprite s) ? s : null)
                                 .Where(s => s != null).ToArray();
        if (all.Length < MateoFrames.Length)
        {
            Debug.LogWarning("[Animaciones] No se pudieron cortar los cuadros de Mateo en mateo_part3.png");
            return;
        }

        AnimationClip crouchClip = CreateClip($"{MATEO_CLIPS_DIR}/Mateo_Crouch.anim", new[] { all[0], all[1] }, 12f, false);

        // Caminatas con movimiento de piernas (reemplazan los cuadros de los clips existentes)
        string[] walkClips = { "Mateo_Walk", "Mateo_Walk_Down", "Mateo_Walk_Up" };
        for (int row = 0; row < walkClips.Length; row++)
        {
            Sprite[] walkFrames = RowFramesAt(MATEO_WALK_SHEET, "mateo_walk_gen", row);
            if (walkFrames.Length > 0) CreateClip($"{MATEO_CLIPS_DIR}/{walkClips[row]}.anim", walkFrames, 10f, true);
            else Debug.LogWarning($"[Animaciones] No se encontraron cuadros para {walkClips[row]} en mateo_walk_gen.png");
        }
        AnimationClip deathClip = CreateClip($"{MATEO_CLIPS_DIR}/Mateo_Death.anim", all, 6f, false);

        EnsureParameter(controller, "isCrouching", AnimatorControllerParameterType.Bool);
        EnsureParameter(controller, "isDead", AnimatorControllerParameterType.Bool);

        AnimatorStateMachine sm = controller.layers[0].stateMachine;
        AnimatorState idle = sm.defaultState;

        AnimatorState crouch = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Mateo_Crouch")
                               ?? sm.AddState("Mateo_Crouch", new Vector3(300f, 300f, 0f));
        crouch.motion = crouchClip;

        AnimatorState death = sm.states.Select(s => s.state).FirstOrDefault(s => s.name == "Mateo_Death")
                              ?? sm.AddState("Mateo_Death", new Vector3(560f, 300f, 0f));
        death.motion = deathClip;

        // Reconstruir transiciones de forma idempotente
        foreach (AnimatorStateTransition t in sm.anyStateTransitions.ToArray())
        {
            if (t.destinationState == crouch || t.destinationState == death) sm.RemoveAnyStateTransition(t);
        }
        foreach (AnimatorStateTransition t in crouch.transitions.ToArray()) crouch.RemoveTransition(t);

        AnimatorStateTransition toDeath = sm.AddAnyStateTransition(death);
        toDeath.AddCondition(AnimatorConditionMode.If, 0f, "isDead");
        toDeath.hasExitTime = false;
        toDeath.duration = 0f;
        toDeath.canTransitionToSelf = false;

        AnimatorStateTransition toCrouch = sm.AddAnyStateTransition(crouch);
        toCrouch.AddCondition(AnimatorConditionMode.If, 0f, "isCrouching");
        toCrouch.AddCondition(AnimatorConditionMode.IfNot, 0f, "isDead");
        toCrouch.hasExitTime = false;
        toCrouch.duration = 0f;
        toCrouch.canTransitionToSelf = false;

        if (idle != null)
        {
            AnimatorStateTransition standUp = crouch.AddTransition(idle);
            standUp.AddCondition(AnimatorConditionMode.IfNot, 0f, "isCrouching");
            standUp.hasExitTime = false;
            standUp.duration = 0f;
        }

        EditorUtility.SetDirty(controller);
    }

    private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        if (controller.parameters.Any(p => p.name == name)) return;
        controller.AddParameter(name, type);
    }

    // =====================================================================
    // PREFABS
    // =====================================================================

    private static string DogBreedOf(string prefabPath, string fallback)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        SpriteRenderer sr = prefab != null ? prefab.GetComponent<SpriteRenderer>() : null;
        string texName = sr != null && sr.sprite != null ? sr.sprite.texture.name.ToLower() : "";
        if (texName.Contains("rottweiler")) return "Rottweiler";
        if (texName.Contains("aleman")) return "Aleman";
        if (texName.Contains("doberman")) return "Doberman";
        if (texName.Contains("bulldog")) return "Bulldog";
        return fallback;
    }

    private static void SetupCreaturePrefab(string prefabPath, string creature, AnimatorController controller, float shadowWidth,
                                            System.Action<GameObject> extraSetup, Dictionary<string, AnimatorController> controllers = null)
    {
        if (controller == null && controllers != null) controllers.TryGetValue(creature, out controller);
        if (controller == null || !File.Exists(prefabPath))
        {
            Debug.LogWarning($"[Animaciones] No se pudo configurar {prefabPath} ({creature}).");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            SpriteRenderer sr = root.GetComponent<SpriteRenderer>();
            if (sr == null) return;

            // Primer cuadro de reposo como sprite base
            AnimationClip idleClip = controller.animationClips.FirstOrDefault(c => c.name.EndsWith("_Idle"));
            Sprite idleSprite = null;
            if (idleClip != null)
            {
                var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
                ObjectReferenceKeyframe[] keys = AnimationUtility.GetObjectReferenceCurve(idleClip, binding);
                if (keys != null && keys.Length > 0) idleSprite = keys[0].value as Sprite;
            }

            if (idleSprite != null)
            {
                // Al migrar del sprite antiguo (pivote al centro) al nuevo (pivote en los pies),
                // desplazar colliders y linterna para que sigan alineados con el cuerpo.
                bool alreadyMigrated = sr.sprite != null && sr.sprite.name.StartsWith("anim_");
                if (!alreadyMigrated)
                {
                    Vector2 oldCenter = sr.sprite != null ? (Vector2)sr.sprite.bounds.center : Vector2.zero;
                    Vector2 delta = (Vector2)idleSprite.bounds.center - oldCenter;
                    foreach (Collider2D col in root.GetComponents<Collider2D>()) col.offset += delta;

                    Transform flashlight = root.transform.Find("FlashlightPivot");
                    if (flashlight != null) flashlight.localPosition += new Vector3(0f, delta.y, 0f);
                }
                sr.sprite = idleSprite;
            }

            Animator animator = root.GetComponent<Animator>();
            if (animator == null) animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;

            if (root.GetComponent<CreatureAnimator>() == null) root.AddComponent<CreatureAnimator>();

            EnsureAlertIcon(root, sr);
            EnsureShadow(root, sr, shadowWidth);

            extraSetup?.Invoke(root);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static void EnsureAlertIcon(GameObject root, SpriteRenderer owner)
    {
        Transform icon = root.transform.Find("AlertIcon");
        if (icon == null)
        {
            icon = new GameObject("AlertIcon").transform;
            icon.SetParent(root.transform, false);
        }

        SpriteRenderer iconSr = icon.GetComponent<SpriteRenderer>();
        if (iconSr == null) iconSr = icon.gameObject.AddComponent<SpriteRenderer>();
        iconSr.sortingLayerName = "Foreground";
        iconSr.sortingOrder = 50;
        iconSr.enabled = false;

        AlertIndicator indicator = icon.GetComponent<AlertIndicator>();
        if (indicator == null) indicator = icon.gameObject.AddComponent<AlertIndicator>();

        var uiSprites = LoadSprites(UI_SHEET);
        var so = new SerializedObject(indicator);
        if (uiSprites.TryGetValue("UI_8", out Sprite question)) so.FindProperty("questionSprite").objectReferenceValue = question;
        if (uiSprites.TryGetValue("UI_11", out Sprite exclamation)) so.FindProperty("exclamationSprite").objectReferenceValue = exclamation;
        so.FindProperty("ownerRenderer").objectReferenceValue = owner;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void EnsureShadow(GameObject owner, SpriteRenderer ownerRenderer, float widthFactor)
    {
        Sprite shadowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SHADOW_SPRITE);
        if (shadowSprite == null) return;

        Transform shadow = owner.transform.Find("Shadow");
        if (shadow == null)
        {
            shadow = new GameObject("Shadow").transform;
            shadow.SetParent(owner.transform, false);
        }

        SpriteRenderer sr = shadow.GetComponent<SpriteRenderer>();
        if (sr == null) sr = shadow.gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = shadowSprite;
        sr.sortingLayerName = "Background";
        sr.sortingOrder = 10;

        BlobShadow blob = shadow.GetComponent<BlobShadow>();
        if (blob == null) blob = shadow.gameObject.AddComponent<BlobShadow>();
        blob.Configure(ownerRenderer, widthFactor, 0.3f);
        EditorUtility.SetDirty(blob);
    }

    private static void CreateSpiderPrefab(AnimatorController spiderController)
    {
        if (!File.Exists(SPIDER_PREFAB))
        {
            if (!AssetDatabase.CopyAsset($"{PREFAB_DIR}/Hazard_Scorpion.prefab", SPIDER_PREFAB))
            {
                Debug.LogWarning("[Animaciones] No se pudo crear Hazard_Spider.prefab");
                return;
            }
        }

        SetupCreaturePrefab(SPIDER_PREFAB, "Spider", spiderController, 0.8f, root =>
        {
            root.name = "Hazard_Spider";
            root.transform.localScale = new Vector3(0.26f, 0.26f, 1f);

            HazardFauna fauna = root.GetComponent<HazardFauna>();
            if (fauna != null)
            {
                var so = new SerializedObject(fauna);
                so.FindProperty("faunaType").enumValueIndex = (int)HazardFauna.FaunaType.Spider;
                so.FindProperty("moveSpeed").floatValue = 1.1f;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        });
    }
}
