using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Nivel 2 "Río Bravo" (Issue: lanchas de patrulla con reflector y coberturas flotantes).
/// 1. Rebana la hoja de la lancha (pivote fijo en el casco para que la estela no la haga brincar), los troncos y las matas de juncos.
/// 2. Crea el prefab de la lancha (PatrolAI + PatrolBoat + reflector VisionCone2D con barrido y brillo de aviso)
///    y los prefabs de coberturas (CoverZone2D con vaivén de FloatingBob2D).
/// 3. Coloca 3 lanchas en carriles horizontales que cruzan la pantalla y coberturas al alcance de sus reflectores,
///    sin encimarse con la fauna, las corrientes ni los consumibles.
/// </summary>
public static partial class SetupNivel2Editor
{
    private const string BOAT_SHEET = RIVER_SPRITES + "Obstacles/lancha_patrulla_sheet.png";
    private const string REEDS_SHEET = RIVER_SPRITES + "Obstacles/juncos_sheet.png";
    private const string GLOW_SPRITE = "Assets/Sprites/Effects/LightGlow.png";

    // Las lanchas esperan fuera de la pantalla (más allá de las orillas laterales) antes de volver a cruzar
    private const float BOAT_LANE_HALF = MAP_HALF_W + 1.5f;
    private const float BOAT_LANE_HEIGHT = 0.7f;

    // Escala de los obstáculos de la hoja de troncos (comparten hoja y PPU con las serpientes de agua) para que
    // concuerden con Mateo nadando (~0.6 de largo, cabeza de ~0.21): tronco ~0.8, ramas ~0.65, postes del alambre ~0.3 de alto
    private const float LOG_SCALE = 0.8f;
    private const float BRANCHES_SCALE = 0.65f;
    private const float FENCE_SCALE = 0.55f;

    private struct BoatSpot
    {
        public float y, speed, wait; public bool startLeft;
        public BoatSpot(float y, float speed, float wait, bool startLeft) { this.y = y; this.speed = speed; this.wait = wait; this.startLeft = startLeft; }
    }

    private static readonly BoatSpot[] BoatLayout =
    {
        new BoatSpot(12.3f, 1.4f, 2.0f, true),
        new BoatSpot(21.3f, 1.6f, 1.6f, false),
        new BoatSpot(32.75f, 1.8f, 1.3f, true),
    };

    // Coberturas sólidas que cortan la luz: troncos, matas de juncos y montones de ramas.
    // Mateo solo queda oculto pegado a una y del lado contrario al reflector.
    private static readonly (string prefab, Vector2 pos)[] CoverLayout =
    {
        ("Cobertura_Ramas_1", new Vector2(1.5f, 2.6f)),
        ("Cobertura_Tronco_1", new Vector2(-1.3f, 3.9f)),
        ("Cobertura_Tronco_3", new Vector2(1.2f, 9.4f)),
        ("Cobertura_Juncos_4", new Vector2(-1.3f, 9.9f)),
        ("Cobertura_Juncos_1", new Vector2(-1.9f, 16.3f)),
        ("Cobertura_Tronco_2", new Vector2(0.9f, 16.3f)),
        ("Cobertura_Juncos_3", new Vector2(1.7f, 18.3f)),
        ("Cobertura_Ramas_2", new Vector2(0.1f, 18.4f)),
        ("Cobertura_Tronco_5", new Vector2(0.9f, 24.6f)),
        ("Cobertura_Juncos_5", new Vector2(-1.5f, 24.7f)),
        ("Cobertura_Juncos_2", new Vector2(1.7f, 28.4f)),
        ("Cobertura_Tronco_4", new Vector2(1.3f, 29.7f)),
        ("Cobertura_Ramas_4", new Vector2(-1.6f, 29.75f)),
        ("Cobertura_Juncos_7", new Vector2(1.6f, 31.3f)),
    };

    // Alambrados de púas que cruzan el río dejando un solo hueco: (altura, desde x, hasta x)
    private static readonly (float y, float from, float to)[] FenceLayout =
    {
        (6.5f, -MAP_HALF_W, 0.9f),     // Hueco a la derecha
        (15.2f, -0.9f, MAP_HALF_W),    // Hueco a la izquierda
        (22.2f, -MAP_HALF_W, -0.6f),   // Hueco al centro (dos tramos)
        (22.2f, 0.6f, MAP_HALF_W),
    };

    // =========================================================================
    // MENÚ
    // =========================================================================

    [MenuItem("Sombras de la Frontera/Nivel 2/4. Colocar Lanchas y Coberturas")]
    public static void PlaceBoatsMenu()
    {
        if (!EditorSceneManagerSave()) return;
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(SCENE, UnityEditor.SceneManagement.OpenSceneMode.Single);
        PlaceBoatsAndCovers();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
        int problems = VerifyLevel2();
        EditorUtility.DisplayDialog("Nivel 2", problems == 0 ? "Lanchas y coberturas colocadas sin problemas." : $"{problems} problema(s). Revisa la consola.", "OK");
    }

    // =========================================================================
    // CONSTRUCCIÓN
    // =========================================================================

    public static void PlaceBoatsAndCovers()
    {
        BuildBoatAndCoverPrefabs();

        foreach (string group in new[] { "Lanchas", "Coberturas", "Alambrados" })
        {
            GameObject old = FindRoot(group);
            if (old != null) Object.DestroyImmediate(old);
        }

        Transform boatsRoot = new GameObject("Lanchas").transform;
        Transform coversRoot = new GameObject("Coberturas").transform;
        Transform fencesRoot = new GameObject("Alambrados").transform;

        for (int i = 0; i < BoatLayout.Length; i++)
        {
            BoatSpot spot = BoatLayout[i];
            float startX = spot.startLeft ? -BOAT_LANE_HALF : BOAT_LANE_HALF;
            GameObject boat = Instantiate("Lancha_Patrulla", boatsRoot, new Vector2(startX, spot.y));
            if (boat == null) continue;
            boat.name = $"Lancha_{i + 1}";

            // Carril: de la orilla de inicio a la contraria (fuera de la pantalla en ambos extremos)
            var lane = new GameObject($"Carril_Lancha_{i + 1}").transform;
            lane.SetParent(boatsRoot, false);
            var from = new GameObject("Inicio").transform;
            var to = new GameObject("Fin").transform;
            from.SetParent(lane, false);
            to.SetParent(lane, false);
            from.position = new Vector3(startX, spot.y, 0f);
            to.position = new Vector3(-startX, spot.y, 0f);

            PatrolAI patrol = boat.GetComponent<PatrolAI>();
            patrol.SetWaypoints(new List<Transform> { from, to }, PatrolAI.PatrolMode.PingPong);
            patrol.moveSpeed = spot.speed;
            patrol.waitTimeAtWaypoint = spot.wait;
            PrefabUtility.RecordPrefabInstancePropertyModifications(patrol);

            // Mirando hacia el río desde el inicio
            boat.GetComponentInChildren<SpriteRenderer>().flipX = !spot.startLeft;
        }

        foreach (var (prefab, pos) in CoverLayout)
        {
            GameObject cover = Instantiate(prefab, coversRoot, pos);
            if (cover != null) cover.name = prefab.Replace("Cobertura_", "");
        }

        // Cada tramo de alambrado se arma con uno o varios alambres estirados para cubrir justo de "from" a "to"
        int fenceCount = 0;
        foreach (var (y, from, to) in FenceLayout)
        {
            GameObject sample = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Alambre_1.prefab");
            if (sample == null) { Debug.LogError("[Nivel2] No existe el prefab Alambre_1."); break; }
            float spriteWidth = sample.GetComponentInChildren<SpriteRenderer>().sprite.bounds.size.x;
            float fenceScale = sample.transform.localScale.y;  // Escala del alambre para que concuerde con Mateo
            float length = to - from;
            int pieces = Mathf.Max(1, Mathf.CeilToInt(length / (spriteWidth * fenceScale * 1.1f)));
            float pieceWidth = length / pieces;
            for (int p = 0; p < pieces; p++)
            {
                GameObject fence = Instantiate($"Alambre_{fenceCount % 3 + 1}", fencesRoot, new Vector2(from + pieceWidth * (p + 0.5f), y));
                if (fence == null) continue;
                fence.name = $"Alambre_{++fenceCount}";
                fence.transform.localScale = new Vector3(pieceWidth / spriteWidth, fenceScale, 1f);
            }
        }

        Debug.Log($"<color=green><b>[Nivel2]</b> Colocadas {BoatLayout.Length} lanchas, {CoverLayout.Length} coberturas y {fenceCount} tramos de alambrado.</color>");
    }

    private static void BuildBoatAndCoverPrefabs()
    {
        AssetDatabase.Refresh();
        SliceBoatSheet(out float hullHalfLength);
        SliceObstacleRow("tronco", new Vector2(0.75f, 0.98f), 6);   // Fila 1: troncos
        SliceObstacleRow("ramas", new Vector2(0.58f, 0.76f), 6);    // Fila 2: montones de ramas (el último son lirios)
        SliceObstacleRow("alambre", new Vector2(0.0f, 0.17f), 3);   // Fila 5: alambres de púas
        SliceSheet(REEDS_SHEET, "juncos", new[] { 4, 4 }, targetWidth: 0.95f, mergeMargin: 5);

        Dictionary<string, Sprite> boat = LoadSprites(BOAT_SHEET);
        Dictionary<string, Sprite> logs = LoadSprites(OBSTACLES_SHEET);
        Dictionary<string, Sprite> reeds = LoadSprites(REEDS_SHEET);

        // Lancha: 4 cuadros con la estela (el quinto, con el foco encendido, se usa al alumbrar)
        Sprite[] boatFrames = Pick(boat, "lancha_r0_c0", "lancha_r0_c1", "lancha_r0_c2", "lancha_r1_c0");
        BuildBoatPrefab(boatFrames, hullHalfLength);

        for (int i = 0; i < 6; i++)
        {
            if (logs.TryGetValue($"tronco_c{i}", out Sprite log))
                BuildCoverPrefab($"Cobertura_Tronco_{i + 1}", log, new Vector2(0.85f, 0.5f), bob: 0.03f, sway: 3f, scale: LOG_SCALE);
            else Debug.LogWarning($"[Nivel2] Falta el sprite tronco_c{i}.");
        }

        for (int i = 0; i < 5; i++)
        {
            if (logs.TryGetValue($"ramas_c{i}", out Sprite branches))
                BuildCoverPrefab($"Cobertura_Ramas_{i + 1}", branches, new Vector2(0.75f, 0.6f), bob: 0.015f, sway: 2f, scale: BRANCHES_SCALE);
            else Debug.LogWarning($"[Nivel2] Falta el sprite ramas_c{i}.");
        }

        int n = 1;
        for (int r = 0; r < 2; r++)
            for (int c = 0; c < 4; c++, n++)
            {
                if (reeds.TryGetValue($"juncos_r{r}_c{c}", out Sprite reed))
                    BuildCoverPrefab($"Cobertura_Juncos_{n}", reed, new Vector2(0.7f, 0.55f), bob: 0.008f, sway: 2.5f);
                else Debug.LogWarning($"[Nivel2] Falta el sprite juncos_r{r}_c{c}.");
            }

        for (int i = 0; i < 3; i++)
        {
            if (logs.TryGetValue($"alambre_c{i}", out Sprite wire)) BuildFencePrefab($"Alambre_{i + 1}", wire, FENCE_SCALE);
            else Debug.LogWarning($"[Nivel2] Falta el sprite alambre_c{i}.");
        }

        AssetDatabase.SaveAssets();
    }

    private static void BuildBoatPrefab(Sprite[] frames, float hullHalfLength)
    {
        if (frames.Length == 0) { Debug.LogError("[Nivel2] Sin cuadros para la lancha."); return; }

        var root = new GameObject("Lancha_Patrulla");

        var hullGo = new GameObject("Casco");
        hullGo.transform.SetParent(root.transform, false);
        SpriteRenderer hull = hullGo.AddComponent<SpriteRenderer>();
        hull.sprite = frames[0];
        hull.sortingLayerName = "Foreground";
        hull.sortingOrder = 2;

        // Reflector en la proa: PatrolAI orienta el pivote hacia donde avanza y VisionCone2D barre de lado a lado
        var pivot = new GameObject("ReflectorPivot");
        pivot.transform.SetParent(root.transform, false);
        var beam = new GameObject("Reflector");
        beam.transform.SetParent(pivot.transform, false);
        beam.AddComponent<PolygonCollider2D>().isTrigger = true;
        VisionCone2D cone = beam.AddComponent<VisionCone2D>();
        var coneSo = new SerializedObject(cone);
        coneSo.FindProperty("viewDistance").floatValue = 3.4f;
        coneSo.FindProperty("viewAngle").floatValue = 36f;
        coneSo.FindProperty("originOffset").floatValue = 0.1f;
        coneSo.FindProperty("segments").intValue = 16;
        coneSo.FindProperty("detectionTimeToCapture").floatValue = 0.5f;  // Más de medio segundo en la luz: captura
        coneSo.FindProperty("patrolColor").colorValue = new Color(1f, 0.97f, 0.75f, 0.5f);
        coneSo.FindProperty("alertColor").colorValue = new Color(1f, 0.2f, 0.1f, 0.7f);
        coneSo.FindProperty("closeDetectionRadius").floatValue = 0f;       // Solo ve con la luz
        coneSo.FindProperty("frontDetectionRadius").floatValue = 0f;
        coneSo.FindProperty("sweepHalfAngle").floatValue = 50f;
        coneSo.FindProperty("sweepPeriod").floatValue = 3.5f;
        coneSo.FindProperty("beamAlpha").floatValue = 0.55f;
        coneSo.FindProperty("defeatReason").enumValueIndex = (int)DefeatReason.BoatSearchlight;
        coneSo.FindProperty("sortingLayerName").stringValue = "Foreground";
        coneSo.FindProperty("sortingOrder").intValue = 5;
        coneSo.ApplyModifiedPropertiesWithoutUndo();

        // Brillo de aviso en la orilla (se coloca en tiempo real)
        var glowGo = new GameObject("Aviso");
        glowGo.transform.SetParent(root.transform, false);
        SpriteRenderer glow = glowGo.AddComponent<SpriteRenderer>();
        glow.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_SPRITE);
        glow.color = new Color(1f, 0.9f, 0.5f, 0.9f);
        glow.sortingLayerName = "Foreground";
        glow.sortingOrder = 6;
        glow.enabled = false;
        if (glow.sprite != null)
        {
            float size = Mathf.Max(glow.sprite.bounds.size.x, 0.01f);
            glowGo.transform.localScale = Vector3.one * (1.1f / size);
        }

        PatrolAI patrol = root.AddComponent<PatrolAI>();
        var patrolSo = new SerializedObject(patrol);
        patrolSo.FindProperty("spinAtWaypoints").floatValue = 0f;   // La lancha no da vueltas: espera y regresa
        patrolSo.FindProperty("flashlightPivot").objectReferenceValue = pivot.transform;
        patrolSo.FindProperty("spriteRenderer").objectReferenceValue = hull;
        patrolSo.FindProperty("moveSpeed").floatValue = 1.5f;
        patrolSo.FindProperty("waitTimeAtWaypoint").floatValue = 1.8f;
        patrolSo.ApplyModifiedPropertiesWithoutUndo();

        var boatSo = new SerializedObject(root.AddComponent<PatrolBoat>());
        SetSpriteArray(boatSo.FindProperty("frames"), frames);
        boatSo.FindProperty("hull").objectReferenceValue = hull;
        boatSo.FindProperty("reflectorPivot").objectReferenceValue = pivot.transform;
        boatSo.FindProperty("reflectorOffset").vector2Value = new Vector2(hullHalfLength * 0.85f, 0.08f);
        boatSo.FindProperty("warningGlow").objectReferenceValue = glow;
        boatSo.FindProperty("riverHalfWidth").floatValue = MAP_HALF_W;
        boatSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/Lancha_Patrulla.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>
    /// Cobertura flotante sólida: Mateo no la atraviesa y el haz de los reflectores se corta en ella (LightOcclusion2D),
    /// así que solo lo oculta si se pega a ella del lado contrario a la luz.
    /// </summary>
    private static void BuildCoverPrefab(string prefabName, Sprite sprite, Vector2 solidFactor, float bob, float sway, float scale = 1f)
    {
        var root = new GameObject(prefabName);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(scale, scale, 1f);  // A escala con Mateo (el collider de la raíz ya va escalado)
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Characters";
        YSort2D sort = visual.AddComponent<YSort2D>();
        sort.useBottomBounds = true;

        // Cuerpo sólido (la parte que flota); el collider va junto a CoverZone2D para que corte la luz
        BoxCollider2D solid = root.AddComponent<BoxCollider2D>();
        solid.size = Vector2.Scale(sprite.bounds.size, solidFactor) * scale;
        solid.offset = sprite.bounds.center * scale;
        root.AddComponent<CoverZone2D>().Configure(solidFactor.x, 0.9f, 0.3f);

        var bobSo = new SerializedObject(root.AddComponent<FloatingBob2D>());
        bobSo.FindProperty("visual").objectReferenceValue = sr;
        bobSo.FindProperty("bobAmplitude").floatValue = bob;
        bobSo.FindProperty("bobSpeed").floatValue = 0.35f;
        bobSo.FindProperty("swayAngle").floatValue = sway;
        bobSo.ApplyModifiedPropertiesWithoutUndo();

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
    }

    /// <summary>Alambre de púas: sólido (no se atraviesa, no oculta) y quita 1 corazón al tocarlo.</summary>
    private static void BuildFencePrefab(string prefabName, Sprite sprite, float scale)
    {
        var root = new GameObject(prefabName);
        var visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        SpriteRenderer sr = visual.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingLayerName = "Characters";
        YSort2D sort = visual.AddComponent<YSort2D>();
        sort.useBottomBounds = true;
        sort.isStatic = true;

        BoxCollider2D solid = root.AddComponent<BoxCollider2D>();
        solid.size = new Vector2(sprite.bounds.size.x * 1.03f, sprite.bounds.size.y * 0.45f);  // Se traslapa con el tramo vecino: sin rendijas
        solid.offset = sprite.bounds.center;
        root.AddComponent<BarbedWire2D>();
        // Escala uniforme en el prefab; al colocarlo se estira a lo ancho para cubrir justo su tramo
        root.transform.localScale = new Vector3(scale, scale, 1f);

        PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_DIR}/{prefabName}.prefab");
        Object.DestroyImmediate(root);
    }

    // =========================================================================
    // REBANADO
    // =========================================================================

    /// <summary>
    /// Lancha: 3 cuadros arriba y 2 abajo. La estela cambia de largo entre cuadros, así que el pivote se fija a la misma
    /// distancia de la proa (borde derecho) y de la cabina (borde de arriba) en todos, para que el casco no brinque.
    /// </summary>
    private static void SliceBoatSheet(out float hullHalfLength)
    {
        hullHalfLength = 0.75f;
        if (!File.Exists(BOAT_SHEET)) { Debug.LogError($"[Nivel2] No existe {BOAT_SHEET}."); return; }

        List<RectInt> boxes = DetectFigures(BOAT_SHEET, out int texW, out int texH).Where(b => b.width > 120).ToList();

        // La espuma de una estela puede tocar la proa de la lancha de al lado: esas figuras se parten en el hueco entre ambas
        Color32[] px = ReadPixels(BOAT_SHEET);
        int single = boxes.Min(b => b.width);
        boxes = boxes.SelectMany(b => b.width > single * 1.7f ? SplitAtGap(b, px, texW) : new[] { b }).ToList();

        List<List<RectInt>> rows = ArrangeRows(boxes);
        if (rows.Count != 2 || rows[0].Count != 3 || rows[1].Count != 2)
        {
            Debug.LogError($"[Nivel2] Lancha: se esperaban filas de [3,2] cuadros y se detectaron [{string.Join(",", rows.Select(r => r.Count))}]: " +
                           string.Join(" ", boxes.Select(b => $"[x{b.x} y{b.y} {b.width}x{b.height}]")));
            return;
        }

        // El cuadro con menos estela marca el tamaño del casco
        int hullPx = boxes.Min(b => b.width);
        int hullHeightPx = boxes.Min(b => b.height);
        float ppu = Mathf.Round(hullPx / 1.6f);   // Casco con poca estela de ~1.6 unidades (Mateo nadando mide ~0.6)
        hullHalfLength = hullPx * 0.45f / ppu;

        var cells = new List<(string name, Rect rect, Vector2 pivot)>();
        for (int r = 0; r < rows.Count; r++)
            for (int c = 0; c < rows[r].Count; c++)
            {
                Rect rect = Pad(rows[r][c], texW, texH);
                float pivotX = 1f - (hullPx * 0.45f + (rect.xMax - rows[r][c].xMax)) / rect.width;
                float pivotY = 1f - (hullHeightPx * 0.5f + (rect.yMax - rows[r][c].yMax)) / rect.height;
                cells.Add(($"lancha_r{r}_c{c}", rect, new Vector2(pivotX, pivotY)));
            }

        ConfigureSheetImport(BOAT_SHEET, ppu, false);
        WriteSpriteRects(BOAT_SHEET, cells);
    }

    private static Color32[] ReadPixels(string texPath)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(texPath));
        Color32[] px = tex.GetPixels32();
        Object.DestroyImmediate(tex);
        return px;
    }

    /// <summary>
    /// Parte en dos una figura que en realidad son dos cuadros pegados: corta en la columna con menos píxeles opacos
    /// del tercio central y ajusta cada mitad a su contenido.
    /// </summary>
    private static RectInt[] SplitAtGap(RectInt box, Color32[] px, int texW)
    {
        int Opaque(int x, int y0, int y1) { int n = 0; for (int y = y0; y < y1; y++) if (px[y * texW + x].a >= 128) n++; return n; }

        int bestX = box.x + box.width / 2, best = int.MaxValue;
        for (int x = box.x + box.width / 3; x < box.x + box.width * 2 / 3; x++)
        {
            int n = Opaque(x, box.yMin, box.yMax);
            if (n < best) { best = n; bestX = x; }
        }

        RectInt Fit(int x0, int x1)
        {
            int minX = int.MaxValue, maxX = -1, minY = int.MaxValue, maxY = -1;
            for (int y = box.yMin; y < box.yMax; y++)
                for (int x = x0; x < x1; x++)
                    if (px[y * texW + x].a >= 128)
                    {
                        if (x < minX) minX = x; if (x > maxX) maxX = x;
                        if (y < minY) minY = y; if (y > maxY) maxY = y;
                    }
            return maxX < 0 ? new RectInt(x0, box.y, x1 - x0, box.height) : new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        return new[] { Fit(box.xMin, bestX), Fit(bestX + 1, box.xMax) };
    }

    /// <summary>
    /// Una fila de la hoja de troncos y fauna (franja de alturas en fracciones, contando desde abajo).
    /// Usa la misma escala que las serpientes de agua de esa hoja. Los cuadros quedan como "prefijo_cN".
    /// </summary>
    private static void SliceObstacleRow(string prefix, Vector2 centerYRange, int expected)
    {
        if (!File.Exists(OBSTACLES_SHEET)) return;
        List<RectInt> figures = DetectFigures(OBSTACLES_SHEET, out int texW, out int texH, centerYRange, mergeMargin: 6)
                                .Where(b => b.width > 100).OrderBy(b => b.x).ToList();
        if (figures.Count != expected)
        {
            Debug.LogError($"[Nivel2] Se esperaban {expected} figuras '{prefix}' y se detectaron {figures.Count}: " +
                           string.Join(" ", figures.Select(b => $"[x{b.x} y{b.y} {b.width}x{b.height}]")));
            return;
        }
        WriteSpriteRects(OBSTACLES_SHEET, figures.Select((b, i) => ($"{prefix}_c{i}", Pad(b, texW, texH))).ToList());
    }

    // =========================================================================
    // VERIFICACIÓN
    // =========================================================================

    /// <summary>
    /// Lanchas (carril, reflector con barrido y captura en 0.5 s, aviso previo) y coberturas (CoverZone2D sin collider sólido).
    /// Agrega sus zonas a la lista para comprobar que nada se encime.
    /// </summary>
    private static int VerifyBoatsAndCovers(List<(string name, Rect rect)> areas)
    {
        int problems = 0;
        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel2][OK] {message}");
            else { Debug.LogWarning($"[Nivel2][FALLA] {message}"); problems++; }
        }

        PatrolBoat[] boats = Object.FindObjectsByType<PatrolBoat>(FindObjectsSortMode.None);
        Expect(boats.Length >= 3, $"Hay {boats.Length} lanchas de patrulla");
        foreach (PatrolBoat boat in boats)
        {
            PatrolAI patrol = boat.GetComponent<PatrolAI>();
            var patrolSo = new SerializedObject(patrol);
            SerializedProperty wps = patrolSo.FindProperty("waypoints");
            bool laneOk = wps.arraySize == 2;
            if (laneOk)
            {
                Vector3 a = ((Transform)wps.GetArrayElementAtIndex(0).objectReferenceValue).position;
                Vector3 b = ((Transform)wps.GetArrayElementAtIndex(1).objectReferenceValue).position;
                laneOk = Mathf.Abs(a.y - b.y) < 0.01f && Mathf.Min(a.x, b.x) < -MAP_HALF_W - 1f && Mathf.Max(a.x, b.x) > MAP_HALF_W + 1f;
            }
            Expect(laneOk && patrolSo.FindProperty("spinAtWaypoints").floatValue == 0f,
                $"{boat.name} recorre un carril horizontal que cruza toda la pantalla");

            VisionCone2D cone = boat.GetComponentInChildren<VisionCone2D>();
            bool coneOk = cone != null;
            if (coneOk)
            {
                var so = new SerializedObject(cone);
                coneOk = so.FindProperty("sweepHalfAngle").floatValue > 0f &&
                         Mathf.Approximately(so.FindProperty("detectionTimeToCapture").floatValue, 0.5f) &&
                         so.FindProperty("defeatReason").enumValueIndex == (int)DefeatReason.BoatSearchlight;
            }
            Expect(coneOk, $"{boat.name} barre con su reflector y captura tras 0.5 s en la luz");

            var boatSo = new SerializedObject(boat);
            SpriteRenderer glow = boatSo.FindProperty("warningGlow").objectReferenceValue as SpriteRenderer;
            Expect(glow != null && glow.sprite != null && Mathf.Approximately(boatSo.FindProperty("warningLeadTime").floatValue, 1.5f),
                $"{boat.name} avisa con un brillo 1.5 s antes de cruzar");
            Expect(boatSo.FindProperty("frames").arraySize >= 4, $"{boat.name} tiene la animación de la estela");

            float y = boat.transform.position.y;
            Expect(y - BOAT_LANE_HEIGHT * 0.5f > WATER_START_Y && y + BOAT_LANE_HEIGHT * 0.5f < WATER_END_Y, $"{boat.name} navega dentro del río (y {y:F2})");
            areas.Add((boat.name, new Rect(-MAP_HALF_W, y - BOAT_LANE_HEIGHT * 0.5f, MAP_WIDTH, BOAT_LANE_HEIGHT)));
        }

        CoverZone2D[] covers = Object.FindObjectsByType<CoverZone2D>(FindObjectsSortMode.None);
        int logs = covers.Count(c => c.name.StartsWith("Tronco"));
        int reeds = covers.Count(c => c.name.StartsWith("Juncos"));
        int branches = covers.Count(c => c.name.StartsWith("Ramas"));
        Expect(logs > 0 && reeds > 0 && branches > 0, $"Hay coberturas flotantes: {logs} troncos, {reeds} matas de juncos y {branches} montones de ramas");
        foreach (CoverZone2D cover in covers)
        {
            SpriteRenderer sr = cover.GetComponentInChildren<SpriteRenderer>();
            Collider2D solid = cover.GetComponent<Collider2D>();
            Expect(sr != null && sr.sprite != null && solid != null && !solid.isTrigger && cover.GetComponent<FloatingBob2D>() != null,
                $"{cover.name} flota con vaivén, no se atraviesa y corta la luz de los reflectores");

            Bounds sb = solid != null ? solid.bounds : sr.bounds;
            Expect(sb.size.x >= 0.4f && sb.size.y >= 0.2f, $"{cover.name} es lo bastante grande para esconderse detrás ({sb.size.x:F2} x {sb.size.y:F2})");
            Bounds b = sr.bounds;
            Expect(b.min.y > WATER_START_Y && b.max.y < WATER_END_Y && b.min.x > -MAP_HALF_W && b.max.x < MAP_HALF_W, $"{cover.name} está dentro del río");
            areas.Add((cover.name, new Rect(b.min, b.size)));
        }

        BarbedWire2D[] fences = Object.FindObjectsByType<BarbedWire2D>(FindObjectsSortMode.None);
        Expect(fences.Length >= FenceLayout.Length, $"Hay {fences.Length} tramos de alambrado de púas");
        foreach (BarbedWire2D fence in fences)
        {
            Collider2D solid = fence.GetComponent<Collider2D>();
            SpriteRenderer sr = fence.GetComponentInChildren<SpriteRenderer>();
            Expect(solid != null && !solid.isTrigger && sr != null && sr.sprite != null, $"{fence.name} es sólido y quita 1 corazón al tocarlo");
            Bounds b = sr.bounds;
            areas.Add((fence.name, new Rect(b.min.x, b.min.y, b.size.x, b.size.y)));
        }
        foreach (var (y, from, to) in FenceLayout)
        {
            Expect(SolidAt(new Vector2((from + to) * 0.5f, y)), $"El alambrado en y={y} cierra de x={from:F1} a x={to:F1}");
        }

        problems += VerifyPathToGoal();

        // Cada lancha debe tener al menos una cobertura al alcance de su reflector
        foreach (PatrolBoat boat in boats)
        {
            float y = boat.transform.position.y;
            Expect(covers.Any(c => Mathf.Abs(c.transform.position.y - y) < 3.4f), $"{boat.name} tiene coberturas cerca para esconderse de su luz");
        }

        return problems;
    }

    /// <summary>Hay algo sólido (pared, cobertura o alambrado) en ese punto. El cierre de la orilla sur no cuenta (inicia apagado).</summary>
    private static bool SolidAt(Vector2 point, Vector2? size = null)
    {
        Collider2D[] hits = size.HasValue ? Physics2D.OverlapBoxAll(point, size.Value, 0f) : Physics2D.OverlapPointAll(point);
        return hits.Any(c => !c.isTrigger && c.enabled && c.attachedRigidbody == null && c.GetComponent<ShoreLock2D>() == null);
    }

    /// <summary>
    /// Comprueba que todavía se puede cruzar: busca un camino nadando (cuadrícula de 0.1) desde la arena sur hasta la arena norte
    /// que no atraviese paredes, coberturas ni alambrados, con el tamaño del cuerpo de Mateo.
    /// </summary>
    private static int VerifyPathToGoal()
    {
        Physics2D.SyncTransforms();
        const float cell = 0.1f;
        Vector2 body = new Vector2(0.32f, 0.2f);
        int cols = Mathf.FloorToInt(MAP_WIDTH / cell);
        int rows = Mathf.FloorToInt(MAP_TOP_Y / cell);
        Vector2 Center(int c, int r) => new Vector2(-MAP_HALF_W + (c + 0.5f) * cell, (r + 0.5f) * cell);

        var free = new bool[cols, rows];
        for (int c = 0; c < cols; c++)
            for (int r = 0; r < rows; r++)
                free[c, r] = !SolidAt(Center(c, r), body);

        var visited = new bool[cols, rows];
        var queue = new Queue<Vector2Int>();
        int startRow = Mathf.FloorToInt(PlayerSpawn.y / cell) + 2;
        for (int c = 0; c < cols; c++)
            if (free[c, startRow]) { visited[c, startRow] = true; queue.Enqueue(new Vector2Int(c, startRow)); }

        int goalRow = Mathf.CeilToInt((WATER_END_Y + 0.1f) / cell);
        bool reached = false;
        var steps = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        while (queue.Count > 0 && !reached)
        {
            Vector2Int p = queue.Dequeue();
            if (p.y >= goalRow) { reached = true; break; }
            foreach (Vector2Int s in steps)
            {
                Vector2Int n = p + s;
                if (n.x < 0 || n.y < 0 || n.x >= cols || n.y >= rows || visited[n.x, n.y] || !free[n.x, n.y]) continue;
                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }

        if (reached) { Debug.Log("[Nivel2][OK] Existe un camino nadando de la orilla sur a la orilla norte (pasando por los huecos)"); return 0; }
        Debug.LogWarning("[Nivel2][FALLA] No hay camino de la orilla sur a la norte: los obstáculos cierran el río");
        return 1;
    }
}
