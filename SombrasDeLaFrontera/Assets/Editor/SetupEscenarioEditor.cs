using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Acomoda el escenario del Nivel 1 para que se vea ordenado y que los obstáculos solo tapen o estorben con lógica:
/// 1. Reacomoda los obstáculos para que no queden encimados (bases separadas, sprites que no se tapan entre sí),
///    lejos de los bordes, de los pozos de arena, del río y del punto de inicio. Si no cabe, lo desactiva.
/// 2. Collider sólido solo en la BASE de cada obstáculo (tronco, base de la roca, llantas del carro):
///    Mateo y los enemigos pueden pasar por detrás de la parte alta del sprite.
/// 3. Coberturas (CoverZone2D) solo en obstáculos que realmente pueden tapar a una persona agachada
///    (Mateo se esconde pegado al lado de abajo del obstáculo).
/// 4. Profundidad 2D: obstáculos, ítems, criaturas y Mateo en la misma capa "Environment" ordenados por sus pies.
/// 5. Collider de Mateo reducido a sus pies, sombra bajo Mateo.
/// 6. Ítems y fauna que quedaron detrás o dentro de un obstáculo se mueven al lugar libre más cercano.
/// 7. Coloca 5 arañas (Hazard_Spider) en zonas despejadas.
/// </summary>
public static class SetupEscenarioEditor
{
    private const string LEVEL_SCENE = "Assets/Scenes/02_Level1_Desert.unity";
    private const string SPIDER_PREFAB = "Assets/Prefabs/Enemies/Hazard_Spider.prefab";

    // Límites del área jugable (paredes en x = ±2.85, río al norte)
    private const float MAP_HALF_W = 2.70f;
    private const float MIN_Y = 0.9f;
    private const float MAX_Y = 33.2f;
    private const float GOAL_Y = 33.4f;
    private const float SPAWN_CLEAR_RADIUS = 1.1f;
    private const float FOOT_FRAC = 0.35f;
    private const float VIS_PAD = 0.06f;
    private const float MAX_VIS_OVERLAP = 0.10f;
    private const float STEP = 0.12f;

    private class ObstacleType
    {
        public string prefix; public int priority;
        public float footW, footH;           // Collider sólido en la base (fracción del sprite)
        public bool isCover; public float coverW, coverH, hideDistance;
        public ObstacleType(string prefix, int priority, float footW, float footH, bool isCover, float coverW = 0f, float coverH = 0f, float hideDistance = 0f)
        {
            this.prefix = prefix; this.priority = priority; this.footW = footW; this.footH = footH;
            this.isCover = isCover; this.coverW = coverW; this.coverH = coverH; this.hideDistance = hideDistance;
        }
    }

    // Orden importante: prefijos largos primero ("arbol_pequeño" antes que "arbol")
    private static readonly ObstacleType[] Types =
    {
        new ObstacleType("arbol_pequeño", 4, 0.25f, 0.12f, false),
        new ObstacleType("arbol_pequeno", 4, 0.25f, 0.12f, false),
        new ObstacleType("arbusto_seco", 6, 0.50f, 0.20f, false),
        new ObstacleType("roca_grande",  2, 0.85f, 0.30f, true, 0.90f, 0.90f, 0.30f),
        new ObstacleType("roca_pequeña", 3, 0.85f, 0.30f, true, 0.90f, 0.90f, 0.30f),
        new ObstacleType("roca_pequena", 3, 0.85f, 0.30f, true, 0.90f, 0.90f, 0.30f),
        new ObstacleType("camioneta",    0, 0.90f, 0.45f, true, 0.92f, 0.80f, 0.30f),
        new ObstacleType("carro",        0, 0.90f, 0.45f, true, 0.92f, 0.80f, 0.30f),
        new ObstacleType("nopalera",     2, 0.70f, 0.22f, true, 0.85f, 0.85f, 0.30f),
        new ObstacleType("arbusto",      5, 0.60f, 0.25f, true, 0.85f, 0.85f, 0.30f),
        new ObstacleType("cactus",       1, 0.30f, 0.14f, true, 0.45f, 0.90f, 0.30f),
        new ObstacleType("arbol",        4, 0.18f, 0.10f, true, 0.22f, 0.60f, 0.30f),
    };

    private class Obstacle
    {
        public GameObject go; public SpriteRenderer sr; public ObstacleType type;
        public Vector2 origin; public Vector2 pos; public Vector2 visOffset; public Vector2 visSize;
    }

    [MenuItem("Sombras de la Frontera/Sprint 2/2. Acomodar Escenario, Coberturas y Profundidad")]
    public static void ArrangeScenery()
    {
        if (!EnsureLevelSceneOpen()) return;

        Physics2D.SyncTransforms();

        List<Obstacle> obstacles = CollectObstacles(out List<Rect> sandRects);
        Vector2 spawn = GetPlayerSpawn();

        int moved, hidden;
        LayoutObstacles(obstacles, sandRects, spawn, out moved, out hidden);

        int colliders = ConfigureObstacleColliders(obstacles);
        ConfigureSandPits();
        ConfigurePlayer();
        PlaceSpiders(obstacles);
        int creatures = ConfigureCreatureSorting();
        int relocated = RelocateItemsAndFauna(obstacles, sandRects);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"<color=green><b>[Escenario]</b> {obstacles.Count} obstáculos revisados: {moved} reacomodados, {hidden} desactivados por falta de espacio. " +
                  $"{colliders} colliders ajustados a la base. {creatures} criaturas con profundidad 2D. {relocated} ítems/fauna movidos a zonas libres. Guarda la escena (Ctrl+S).</color>");
    }

    public static bool EnsureLevelSceneOpen()
    {
        if (EditorSceneManager.GetActiveScene().path == LEVEL_SCENE) return true;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return false;
        EditorSceneManager.OpenScene(LEVEL_SCENE, OpenSceneMode.Single);
        return EditorSceneManager.GetActiveScene().path == LEVEL_SCENE;
    }

    // =====================================================================
    // RECOLECCIÓN
    // =====================================================================

    private static ObstacleType TypeOf(string objectName)
    {
        string n = objectName.Trim().ToLowerInvariant();
        return Types.FirstOrDefault(t => n.StartsWith(t.prefix));
    }

    private static bool IsUnder(Transform t, params string[] rootNames)
    {
        while (t != null)
        {
            if (rootNames.Contains(t.name.Trim())) return true;
            t = t.parent;
        }
        return false;
    }

    private static List<Obstacle> CollectObstacles(out List<Rect> sandRects)
    {
        var list = new List<Obstacle>();
        sandRects = new List<Rect>();

        SpriteRenderer[] renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (SpriteRenderer sr in renderers)
        {
            if (sr.sprite == null) continue;
            if (IsUnder(sr.transform, "EnemiesAndHazards", "Pickups", "Player", "HUD_Canvas")) continue;

            string lower = sr.gameObject.name.Trim().ToLowerInvariant();
            if (lower.StartsWith("pozoarena") || lower.StartsWith("posoarena"))
            {
                Bounds sb = sr.bounds;
                sandRects.Add(new Rect(sb.min.x, sb.min.y, sb.size.x, sb.size.y));
                continue;
            }

            ObstacleType type = TypeOf(sr.gameObject.name);
            if (type == null) continue;

            Bounds b = sr.bounds;
            Vector2 p = sr.transform.position;
            list.Add(new Obstacle
            {
                go = sr.gameObject, sr = sr, type = type, origin = p, pos = p,
                visOffset = new Vector2(b.min.x, b.min.y) - p,
                visSize = new Vector2(b.size.x, b.size.y)
            });
        }
        return list;
    }

    private static Vector2 GetPlayerSpawn()
    {
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        return player != null ? (Vector2)player.transform.position : new Vector2(0f, 0.25f);
    }

    // =====================================================================
    // REACOMODO DE OBSTÁCULOS
    // =====================================================================

    private static Rect Vis(Obstacle o, Vector2 pos) => new Rect(pos + o.visOffset, o.visSize);

    private static Rect Foot(Obstacle o, Vector2 pos)
    {
        Rect v = Vis(o, pos);
        return new Rect(v.x + v.width * 0.1f, v.y, v.width * 0.8f, v.height * FOOT_FRAC);
    }

    private static Rect Pad(Rect r, float p) => new Rect(r.x - p, r.y - p, r.width + 2f * p, r.height + 2f * p);

    private static float IntersectionArea(Rect a, Rect b)
    {
        float ix = Mathf.Min(a.xMax, b.xMax) - Mathf.Max(a.xMin, b.xMin);
        float iy = Mathf.Min(a.yMax, b.yMax) - Mathf.Max(a.yMin, b.yMin);
        return Mathf.Max(0f, ix) * Mathf.Max(0f, iy);
    }

    private static bool IsValidPlacement(Obstacle o, Vector2 pos, List<Obstacle> placed, List<Rect> sands, Vector2 spawn)
    {
        Rect v = Vis(o, pos);
        if (v.xMin < -MAP_HALF_W || v.xMax > MAP_HALF_W) return false;
        if (v.yMin < MIN_Y || v.yMin > MAX_Y) return false;
        if (v.yMin + v.height * 0.5f > GOAL_Y) return false;

        Vector2 baseCenter = new Vector2(v.center.x, v.yMin + v.height * 0.2f);
        if (Vector2.Distance(baseCenter, spawn) < SPAWN_CLEAR_RADIUS + v.width * 0.4f) return false;

        Rect foot = Pad(Foot(o, pos), 0.05f);
        foreach (Rect s in sands)
        {
            if (IntersectionArea(foot, Pad(s, 0.05f)) > 0f) return false;
        }

        Rect paddedFoot = Pad(foot, 0.12f);
        Rect paddedVis = Pad(v, VIS_PAD);
        foreach (Obstacle p in placed)
        {
            if (IntersectionArea(paddedFoot, Foot(p, p.pos)) > 0f) return false;
            Rect pv = Vis(p, p.pos);
            float overlap = IntersectionArea(paddedVis, pv);
            if (overlap > MAX_VIS_OVERLAP * Mathf.Min(v.width * v.height, pv.width * pv.height)) return false;
        }
        return true;
    }

    private static void LayoutObstacles(List<Obstacle> obstacles, List<Rect> sands, Vector2 spawn, out int moved, out int hidden)
    {
        moved = 0; hidden = 0;
        var placed = new List<Obstacle>();
        var ordered = obstacles.OrderBy(o => o.type.priority).ThenBy(o => o.origin.y).ThenBy(o => o.origin.x).ToList();

        foreach (Obstacle o in ordered)
        {
            bool found = false;
            Vector2 best = o.origin;

            for (int ring = 0; ring < 14 && !found; ring++)
            {
                float radius = ring * STEP;
                float bestDist = float.MaxValue;

                if (ring == 0)
                {
                    if (IsValidPlacement(o, o.origin, placed, sands, spawn)) { best = o.origin; found = true; }
                    continue;
                }

                int count = 2 * (4 + ring * 2);
                float angleStep = Mathf.PI / (4 + ring * 2);
                for (int i = 0; i < count; i++)
                {
                    Vector2 offset = new Vector2(Mathf.Cos(i * angleStep), Mathf.Sin(i * angleStep)) * radius;
                    Vector2 candidate = o.origin + offset;
                    if (IsValidPlacement(o, candidate, placed, sands, spawn) && offset.sqrMagnitude < bestDist)
                    {
                        bestDist = offset.sqrMagnitude;
                        best = candidate;
                        found = true;
                    }
                }
            }

            if (found)
            {
                o.pos = best;
                placed.Add(o);
                if ((best - o.origin).sqrMagnitude > 1e-8f)
                {
                    Undo.RecordObject(o.go.transform, "Reacomodar obstáculo");
                    Vector3 p = o.go.transform.position;
                    o.go.transform.position = new Vector3(best.x, best.y, p.z);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(o.go.transform);
                    moved++;
                }
            }
            else
            {
                Undo.RecordObject(o.go, "Desactivar obstáculo encimado");
                o.go.SetActive(false);
                hidden++;
            }
        }

        obstacles.RemoveAll(o => !o.go.activeSelf);
    }

    // =====================================================================
    // COLLIDERS, COBERTURAS Y PROFUNDIDAD DE OBSTÁCULOS
    // =====================================================================

    private static int ConfigureObstacleColliders(List<Obstacle> obstacles)
    {
        int count = 0;
        foreach (Obstacle o in obstacles)
        {
            Bounds local = o.sr.sprite.bounds; // Espacio local del sprite (no depende de la escala)
            Vector2 footSize = new Vector2(local.size.x * o.type.footW, local.size.y * o.type.footH);
            Vector2 footOffset = new Vector2(local.center.x, local.min.y + footSize.y * 0.5f);

            bool hasSolid = false;
            foreach (Collider2D col in o.go.GetComponents<Collider2D>())
            {
                Undo.RecordObject(col, "Ajustar collider de obstáculo");
                if (col.isTrigger)
                {
                    // Los triggers del sistema de cobertura anterior ya no se usan (se tocaban con un pixel del pie)
                    col.enabled = false;
                }
                else if (col is CapsuleCollider2D cap)
                {
                    cap.direction = CapsuleDirection2D.Horizontal;
                    cap.size = footSize;
                    cap.offset = footOffset;
                    cap.enabled = true;
                    hasSolid = true;
                }
                else if (col is BoxCollider2D box)
                {
                    box.size = footSize;
                    box.offset = footOffset;
                    box.enabled = true;
                    hasSolid = true;
                }
                PrefabUtility.RecordPrefabInstancePropertyModifications(col);
                count++;
            }

            if (!hasSolid)
            {
                CapsuleCollider2D cap = Undo.AddComponent<CapsuleCollider2D>(o.go);
                cap.direction = CapsuleDirection2D.Horizontal;
                cap.size = footSize;
                cap.offset = footOffset;
                count++;
            }

            // Cobertura solo en obstáculos que pueden tapar a una persona agachada
            CoverZone2D cover = o.go.GetComponent<CoverZone2D>();
            if (o.type.isCover)
            {
                if (cover == null) cover = Undo.AddComponent<CoverZone2D>(o.go);
                Undo.RecordObject(cover, "Configurar cobertura");
                cover.Configure(o.type.coverW, o.type.coverH, o.type.hideDistance);
                EditorUtility.SetDirty(cover);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cover);
            }
            else if (cover != null)
            {
                Undo.DestroyObjectImmediate(cover);
            }

            // Profundidad: se ordena por la base del obstáculo
            Undo.RecordObject(o.sr, "Profundidad obstáculo");
            o.sr.sortingLayerName = "Environment";
            YSort2D ysort = o.go.GetComponent<YSort2D>();
            if (ysort == null) ysort = Undo.AddComponent<YSort2D>(o.go);
            Undo.RecordObject(ysort, "Profundidad obstáculo");
            ysort.isStatic = true;
            ysort.useBottomBounds = true;
            ysort.yOffset = 0f;
            ysort.baseOrder = 5000;
            ysort.UpdateSortingOrder();
            EditorUtility.SetDirty(ysort);
            PrefabUtility.RecordPrefabInstancePropertyModifications(o.sr);
            PrefabUtility.RecordPrefabInstancePropertyModifications(ysort);
        }
        return count;
    }

    private static void ConfigureSandPits()
    {
        foreach (SpriteRenderer sr in Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            string lower = sr.gameObject.name.Trim().ToLowerInvariant();
            if (!lower.StartsWith("pozoarena") && !lower.StartsWith("posoarena")) continue;

            // Los pozos de arena son parte del suelo: siempre debajo de personajes y obstáculos
            YSort2D ysort = sr.GetComponent<YSort2D>();
            if (ysort != null) Undo.DestroyObjectImmediate(ysort);

            Undo.RecordObject(sr, "Pozo de arena al suelo");
            sr.sortingLayerName = "Background";
            sr.sortingOrder = 5;
            PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
        }
    }

    // =====================================================================
    // MATEO
    // =====================================================================

    private static void ConfigurePlayer()
    {
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();
        if (player == null) return;

        SpriteRenderer sr = player.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        Undo.RecordObject(sr, "Configurar Mateo");
        sr.sortingLayerName = "Environment";
        sr.color = Color.white;

        Bounds local = sr.sprite.bounds;

        // Profundidad por los pies (el pivote del sprite de Mateo está al centro del cuerpo)
        YSort2D ysort = player.GetComponent<YSort2D>();
        if (ysort == null) ysort = Undo.AddComponent<YSort2D>(player.gameObject);
        Undo.RecordObject(ysort, "Configurar Mateo");
        ysort.isStatic = false;
        ysort.useBottomBounds = false;
        ysort.yOffset = local.min.y * player.transform.lossyScale.y;
        ysort.baseOrder = 5000;
        ysort.UpdateSortingOrder();
        EditorUtility.SetDirty(ysort);

        // Collider solo en los pies: puede pasar por detrás de la parte alta de los obstáculos
        CapsuleCollider2D cap = player.GetComponent<CapsuleCollider2D>();
        if (cap != null)
        {
            Undo.RecordObject(cap, "Collider de pies de Mateo");
            cap.direction = CapsuleDirection2D.Horizontal;
            cap.size = new Vector2(local.size.x * 0.45f, local.size.y * 0.16f);
            cap.offset = new Vector2(local.center.x, local.min.y + cap.size.y * 0.5f);
            EditorUtility.SetDirty(cap);
        }

        SetupAnimacionesEditor.EnsureShadow(player.gameObject, sr, 0.55f);
    }

    // =====================================================================
    // CRIATURAS
    // =====================================================================

    private static int ConfigureCreatureSorting()
    {
        int count = 0;
        var creatures = new List<GameObject>();
        creatures.AddRange(Object.FindObjectsByType<PatrolAI>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject));
        creatures.AddRange(Object.FindObjectsByType<HazardFauna>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.gameObject));

        foreach (GameObject creature in creatures.Distinct())
        {
            SpriteRenderer sr = creature.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            Undo.RecordObject(sr, "Profundidad criatura");
            sr.sortingLayerName = "Environment";
            PrefabUtility.RecordPrefabInstancePropertyModifications(sr);

            YSort2D ysort = creature.GetComponent<YSort2D>();
            if (ysort == null) ysort = Undo.AddComponent<YSort2D>(creature);
            Undo.RecordObject(ysort, "Profundidad criatura");
            ysort.isStatic = false;
            ysort.useBottomBounds = false;
            ysort.yOffset = 0f; // El pivote de los sprites animados ya está en los pies
            ysort.baseOrder = 5000;
            ysort.UpdateSortingOrder();
            PrefabUtility.RecordPrefabInstancePropertyModifications(ysort);
            count++;
        }
        return count;
    }

    private static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t;
        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Crear grupo " + name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void PlaceSpiders(List<Obstacle> obstacles)
    {
        GameObject spiderPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SPIDER_PREFAB);
        if (spiderPrefab == null)
        {
            Debug.LogWarning("[Escenario] Aún no existe Hazard_Spider.prefab. Ejecuta primero 'Sprint 2/1. Generar Animaciones'.");
            return;
        }

        GameObject root = GameObject.Find("EnemiesAndHazards");
        if (root == null) return;
        Transform fauna = FindOrCreateChild(root.transform, "Fauna_Nociva");
        Transform spiders = FindOrCreateChild(fauna, "Arañas");
        if (spiders.childCount > 0) return; // Ya colocadas

        var occupied = Object.FindObjectsByType<HazardFauna>(FindObjectsSortMode.None).Select(f => (Vector2)f.transform.position).ToList();
        occupied.AddRange(Object.FindObjectsByType<PatrolAI>(FindObjectsSortMode.None).Select(p => (Vector2)p.transform.position));

        float[] spiderY = { 8.6f, 15.8f, 21.9f, 26.8f, 30.4f };
        for (int i = 0; i < spiderY.Length; i++)
        {
            Vector2 best = new Vector2(0f, spiderY[i]);
            float bestScore = float.MinValue;
            for (float x = -2.3f; x <= 2.3f; x += 0.1f)
            {
                for (float dy = -0.6f; dy <= 0.6f; dy += 0.2f)
                {
                    Vector2 c = new Vector2(x, spiderY[i] + dy);
                    Rect body = new Rect(c.x - 0.18f, c.y, 0.36f, 0.3f);
                    if (obstacles.Any(o => IntersectionArea(Pad(Vis(o, o.pos), 0.1f), body) > 0f)) continue;
                    float nearest = occupied.Count > 0 ? occupied.Min(p => Vector2.Distance(p, c)) : 10f;
                    if (nearest > bestScore) { bestScore = nearest; best = c; }
                }
            }

            GameObject spider = (GameObject)PrefabUtility.InstantiatePrefab(spiderPrefab, spiders);
            Undo.RegisterCreatedObjectUndo(spider, "Colocar araña");
            spider.name = $"Hazard_Spider_{i + 1}";
            spider.transform.position = new Vector3(best.x, best.y, 0f);
            occupied.Add(best);
        }
    }

    // =====================================================================
    // ÍTEMS Y FAUNA FUERA DE LOS OBSTÁCULOS
    // =====================================================================

    private static int RelocateItemsAndFauna(List<Obstacle> obstacles, List<Rect> sands)
    {
        int relocated = 0;
        var blockers = obstacles.Select(o => Pad(Vis(o, o.pos), 0.05f)).ToList();

        var movables = new List<SpriteRenderer>();
        {
            foreach (PickupItem item in Object.FindObjectsByType<PickupItem>(FindObjectsSortMode.None))
            {
                SpriteRenderer sr = item.GetComponent<SpriteRenderer>();
                if (sr == null) sr = item.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) movables.Add(sr);

                // Ítems en el suelo: profundidad por su base
                if (sr != null)
                {
                    Undo.RecordObject(sr, "Profundidad ítem");
                    sr.sortingLayerName = "Environment";
                    YSort2D ys = sr.GetComponent<YSort2D>();
                    if (ys == null) ys = Undo.AddComponent<YSort2D>(sr.gameObject);
                    ys.isStatic = true;
                    ys.useBottomBounds = true;
                    ys.baseOrder = 5000;
                    ys.UpdateSortingOrder();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
                }
            }
        }
        foreach (HazardFauna f in Object.FindObjectsByType<HazardFauna>(FindObjectsSortMode.None))
        {
            SpriteRenderer sr = f.GetComponent<SpriteRenderer>();
            if (sr != null) movables.Add(sr);
        }

        foreach (SpriteRenderer sr in movables)
        {
            Bounds b = sr.bounds;
            Rect rect = new Rect(b.min.x, b.min.y, b.size.x, b.size.y);
            if (!blockers.Any(r => IntersectionArea(r, rect) > 0f)) continue;

            Vector2 offsetToTransform = (Vector2)sr.transform.position - rect.position;
            Vector2 found = rect.position;
            bool ok = false;
            for (float radius = 0.1f; radius <= 2.4f && !ok; radius += 0.1f)
            {
                int steps = Mathf.Max(8, Mathf.RoundToInt(radius * 40f));
                for (int i = 0; i < steps; i++)
                {
                    float a = i * Mathf.PI * 2f / steps;
                    Rect cand = new Rect(rect.position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, rect.size);
                    if (cand.xMin < -MAP_HALF_W || cand.xMax > MAP_HALF_W || cand.yMin < MIN_Y || cand.yMax > GOAL_Y) continue;
                    if (blockers.Any(r => IntersectionArea(r, cand) > 0f)) continue;
                    found = cand.position;
                    ok = true;
                    break;
                }
            }

            if (ok)
            {
                Undo.RecordObject(sr.transform, "Mover a zona libre");
                Vector3 p = sr.transform.position;
                Vector2 newPos = found + offsetToTransform;
                sr.transform.position = new Vector3(newPos.x, newPos.y, p.z);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr.transform);
                relocated++;
            }
        }
        return relocated;
    }
}
