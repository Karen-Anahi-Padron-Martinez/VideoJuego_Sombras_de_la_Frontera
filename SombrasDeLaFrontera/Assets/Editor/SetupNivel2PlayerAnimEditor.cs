using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Nivel 2 "Río Bravo": animaciones de Mateo sin recortes ni brincos.
/// 1. Vuelve a rebanar las hojas de nado detectando la silueta completa de cada cuadro (los cortes hechos a mano
///    recortaban pies y manos) y pone el pivote en el centro de masa de la figura, que casi no se mueve aunque
///    cambien brazos y piernas (con el pivote al centro del corte el cuerpo brincaba de un cuadro a otro).
///    Se conservan los nombres de los sprites: los clips de nado siguen funcionando sin cambios.
/// 2. En la arena de las orillas usa las mismas animaciones del Nivel 1 para caminar a los lados, arriba y abajo, y en reposo.
/// </summary>
public static partial class SetupNivel2Editor
{
    private struct SwimSheet
    {
        public string path; public string[] rowPrefixes; public int[] perRow; public int mergeMargin;
        public SwimSheet(string path, string[] rowPrefixes, int[] perRow, int mergeMargin)
        {
            this.path = path; this.rowPrefixes = rowPrefixes; this.perRow = perRow; this.mergeMargin = mergeMargin;
        }
    }

    private static readonly SwimSheet[] SwimSheets =
    {
        // Filas de arriba hacia abajo: espalda (arriba), perfil derecho, perfil izquierdo, de frente (abajo), flotando
        new SwimSheet(RIVER_SPRITES + "mateo_swim_sheet_B.png",
            new[] { "mateo_swim_up_", "mateo_swim_side1_", "mateo_swim_side2_", "mateo_swim_down_", "mateo_swim_float_" },
            new[] { 6, 6, 6, 6, 6 }, 4),
        new SwimSheet(RIVER_SPRITES + "mateo_swim_sheet_A.png", new[] { "mateo_tread_" }, new[] { 6 }, 9),
        new SwimSheet(RIVER_SPRITES + "swimup.png", new[] { "swimup_" }, new[] { 8 }, 3),
    };

    private const string LEVEL1_CONTROLLER = "Assets/Animations/Animators/Mateo_Controller.controller";

    [MenuItem("Sombras de la Frontera/Nivel 2/6. Corregir Animaciones de Mateo (sin recortes ni brincos)")]
    public static void FixPlayerAnimationsMenu()
    {
        int problems = FixPlayerAnimations();
        EditorUtility.DisplayDialog("Nivel 2", problems == 0 ? "Animaciones de Mateo corregidas." : $"{problems} problema(s). Revisa la consola.", "OK");
    }

    public static int FixPlayerAnimations()
    {
        int problems = 0;
        foreach (SwimSheet sheet in SwimSheets) problems += ResliceSwimSheet(sheet);
        problems += FixWalkCycle();
        AssetDatabase.SaveAssets();
        return problems;
    }

    private static int ResliceSwimSheet(SwimSheet sheet)
    {
        if (!File.Exists(sheet.path)) { Debug.LogError($"[Nivel2] No existe {sheet.path}."); return 1; }

        List<List<RectInt>> rows = DetectSwimFrames(sheet, out int texW, out int texH, out bool countsOk);
        if (!countsOk)
        {
            Debug.LogError($"[Nivel2] {sheet.path}: se esperaban filas de [{string.Join(",", sheet.perRow)}] cuadros y se detectaron " +
                           $"[{string.Join(",", rows.Select(r => r.Count))}]. No se modificó la hoja. Figuras: " +
                           string.Join(" ", rows.Select(r => "{" + string.Join(" ", r.Select(b => $"x{b.x} y{b.y} {b.width}x{b.height}")) + "}")));
            return 1;
        }

        Color32[] px = ReadPixels(sheet.path);
        var cells = new List<(string name, Rect rect, Vector2 pivot)>();
        for (int r = 0; r < rows.Count; r++)
        {
            for (int c = 0; c < rows[r].Count; c++)
            {
                RectInt figure = rows[r][c];
                Rect rect = Pad(figure, texW, texH);
                Vector2 center = CenterOfMass(figure, px, texW);
                var pivot = new Vector2((center.x - rect.x) / rect.width, (center.y - rect.y) / rect.height);
                cells.Add(($"{sheet.rowPrefixes[r]}{c}", rect, pivot));
            }
        }

        // Solo los cortes: el PPU de cada hoja (cabeza del mismo tamaño que en el Nivel 1) no cambia
        WriteSpriteRects(sheet.path, cells);
        Debug.Log($"[Nivel2] {Path.GetFileName(sheet.path)}: {cells.Count} cuadros sin recortes con el pivote en el centro de la figura.");
        return 0;
    }

    /// <summary>
    /// Cuadros de una hoja de nado por filas: por silueta y, si figuras de filas vecinas se tocan (se unen),
    /// con una cuadrícula cortada por los valles.
    /// </summary>
    private static List<List<RectInt>> DetectSwimFrames(SwimSheet sheet, out int texW, out int texH, out bool countsOk)
    {
        List<RectInt> boxes = DetectFigures(sheet.path, out texW, out texH, null, sheet.mergeMargin);
        List<List<RectInt>> rows = ArrangeRows(boxes);
        countsOk = rows.Count == sheet.perRow.Length && !rows.Where((r, i) => r.Count != sheet.perRow[i]).Any();
        if (!countsOk && sheet.perRow.Distinct().Count() == 1)
        {
            rows = SliceByValleys(ReadPixels(sheet.path), texW, texH, sheet.perRow.Length, sheet.perRow[0]);
            countsOk = rows.Count == sheet.perRow.Length && rows.All(r => r.Count == sheet.perRow[0]);
        }
        return rows;
    }

    /// <summary>
    /// Cuadrícula de rows x cols cortada en los valles: cerca de cada división regular busca la línea con menos
    /// píxeles opacos (el hueco entre dos figuras aunque se toquen) y ajusta cada celda al contenido que tiene.
    /// Filas de arriba hacia abajo, columnas de izquierda a derecha (coordenadas de Unity, y = 0 abajo).
    /// </summary>
    private static List<List<RectInt>> SliceByValleys(Color32[] px, int w, int h, int rows, int cols)
    {
        int[] Cuts(int count, int length, System.Func<int, int> opacityAt)
        {
            var cuts = new int[count + 1];
            cuts[0] = 0; cuts[count] = length;
            int search = length / count / 4;
            for (int k = 1; k < count; k++)
            {
                int ideal = length * k / count, best = ideal, bestCount = int.MaxValue;
                for (int p = ideal - search; p <= ideal + search; p++)
                {
                    int n = opacityAt(p);
                    if (n < bestCount) { bestCount = n; best = p; }
                }
                cuts[k] = best;
            }
            return cuts;
        }

        int RowOpacity(int y, int x0, int x1) { int n = 0; for (int x = x0; x < x1; x++) if (px[y * w + x].a >= 128) n++; return n; }
        int ColOpacity(int x, int y0, int y1) { int n = 0; for (int y = y0; y < y1; y++) if (px[y * w + x].a >= 128) n++; return n; }

        int[] yCuts = Cuts(rows, h, y => RowOpacity(y, 0, w));
        var result = new List<List<RectInt>>();
        for (int r = rows - 1; r >= 0; r--)   // De arriba hacia abajo
        {
            int y0 = yCuts[r], y1 = yCuts[r + 1];
            int[] xCuts = Cuts(cols, w, x => ColOpacity(x, y0, y1));
            var row = new List<RectInt>();
            for (int c = 0; c < cols; c++)
            {
                RectInt? figure = MainFigureIn(px, w, new RectInt(xCuts[c], y0, xCuts[c + 1] - xCuts[c], y1 - y0));
                if (figure.HasValue) row.Add(figure.Value);
            }
            result.Add(row);
        }
        return result;
    }

    /// <summary>
    /// La figura principal dentro de una celda: sus manchas grandes (al menos 3% de la mayor) y no los pedacitos
    /// de las figuras vecinas que se cuelan por el borde (se verían flotando junto a Mateo).
    /// </summary>
    private static RectInt? MainFigureIn(Color32[] px, int w, RectInt cell)
    {
        var visited = new bool[cell.width * cell.height];
        var parts = new List<(RectInt box, int area)>();
        var stack = new Stack<Vector2Int>();
        for (int y = cell.yMin; y < cell.yMax; y++)
            for (int x = cell.xMin; x < cell.xMax; x++)
            {
                int idx = (y - cell.yMin) * cell.width + (x - cell.xMin);
                if (visited[idx] || px[y * w + x].a < 128) continue;
                int minX = x, maxX = x, minY = y, maxY = y, area = 0;
                visited[idx] = true;
                stack.Push(new Vector2Int(x, y));
                while (stack.Count > 0)
                {
                    Vector2Int p = stack.Pop();
                    area++;
                    if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                    if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
                    foreach (Vector2Int n in new[] { new Vector2Int(p.x + 1, p.y), new Vector2Int(p.x - 1, p.y), new Vector2Int(p.x, p.y + 1), new Vector2Int(p.x, p.y - 1) })
                    {
                        if (n.x < cell.xMin || n.x >= cell.xMax || n.y < cell.yMin || n.y >= cell.yMax) continue;
                        int ni = (n.y - cell.yMin) * cell.width + (n.x - cell.xMin);
                        if (visited[ni] || px[n.y * w + n.x].a < 128) continue;
                        visited[ni] = true;
                        stack.Push(n);
                    }
                }
                parts.Add((new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1), area));
            }

        if (parts.Count == 0) return null;
        int largest = parts.Max(p => p.area);
        var kept = parts.Where(p => p.area >= largest * 0.03f).Select(p => p.box).ToList();
        int x0 = kept.Min(b => b.xMin), y0 = kept.Min(b => b.yMin);
        return new RectInt(x0, y0, kept.Max(b => b.xMax) - x0, kept.Max(b => b.yMax) - y0);
    }

    /// <summary>Centro de masa de la figura (promedio de sus píxeles visibles, pesado por su opacidad).</summary>
    private static Vector2 CenterOfMass(RectInt box, Color32[] px, int texW)
    {
        double sx = 0, sy = 0, total = 0;
        for (int y = box.yMin; y < box.yMax; y++)
            for (int x = box.xMin; x < box.xMax; x++)
            {
                byte a = px[y * texW + x].a;
                if (a < 40) continue;
                sx += (x + 0.5) * a; sy += (y + 0.5) * a; total += a;
            }
        return total > 0 ? new Vector2((float)(sx / total), (float)(sy / total)) : box.center;
    }

    /// <summary>
    /// En la arena de las orillas Mateo camina y descansa con las mismas animaciones del Nivel 1:
    /// los estados de orilla del Animator del río usan los clips que reproduce el Animator del Nivel 1
    /// (caminar de lado, arriba y abajo, y reposo) y cambian entre sí con las mismas condiciones
    /// (isMoving, isMovingUp, isMovingDown). Desde cualquiera de ellos, al entrar al agua pasa a nadar.
    /// </summary>
    private static int FixWalkCycle()
    {
        var level1 = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(LEVEL1_CONTROLLER);
        var river = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(SWIM_CONTROLLER);
        if (level1 == null || river == null) { Debug.LogError("[Nivel2] No se encontraron los Animator de Mateo."); return 1; }

        Motion Level1Motion(string state) =>
            level1.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == state)?.motion;
        UnityEditor.Animations.AnimatorStateMachine sm = river.layers[0].stateMachine;
        UnityEditor.Animations.AnimatorState State(string name, Vector3 pos)
        {
            UnityEditor.Animations.AnimatorState s = sm.states.Select(c => c.state).FirstOrDefault(c => c.name == name);
            return s != null ? s : sm.AddState(name, pos);
        }

        var idle = State("Mateo_Land_Idle", new Vector3(250f, 120f, 0f));
        var walk = State("Mateo_Land_Walk", new Vector3(250f, 220f, 0f));
        var walkUp = State("Mateo_Land_Walk_Up", new Vector3(30f, 320f, 0f));
        var walkDown = State("Mateo_Land_Walk_Down", new Vector3(470f, 320f, 0f));
        var swimIdle = sm.states.Select(c => c.state).FirstOrDefault(c => c.name == "Mateo_Swim_Idle");

        var motions = new[] { (idle, "New Mateo_Idle"), (walk, "Mateo_Walk"), (walkUp, "Mateo_Walk_Up"), (walkDown, "Mateo_Walk_Down") };
        foreach (var (state, level1State) in motions)
        {
            Motion m = Level1Motion(level1State);
            if (m == null) { Debug.LogError($"[Nivel2] El Animator del Nivel 1 no tiene el estado {level1State}."); return 1; }
            state.motion = m;
            state.writeDefaultValues = false;
        }

        // Transiciones de la orilla (se rehacen): inmediatas, sin esperar a que termine el clip
        var land = new[] { idle, walk, walkUp, walkDown };
        foreach (var from in land)
        {
            foreach (var t in from.transitions.ToArray()) from.RemoveTransition(t);
            void To(UnityEditor.Animations.AnimatorState dst, params (UnityEditor.Animations.AnimatorConditionMode mode, string param)[] conditions)
            {
                if (dst == null || dst == from) return;
                var t = from.AddTransition(dst);
                t.hasExitTime = false;
                t.duration = 0f;
                foreach (var (mode, param) in conditions) t.AddCondition(mode, 0f, param);
            }
            const UnityEditor.Animations.AnimatorConditionMode If = UnityEditor.Animations.AnimatorConditionMode.If;
            const UnityEditor.Animations.AnimatorConditionMode IfNot = UnityEditor.Animations.AnimatorConditionMode.IfNot;
            To(swimIdle, (If, "inWater"));
            To(idle, (IfNot, "inWater"), (IfNot, "isMoving"));
            To(walkUp, (IfNot, "inWater"), (If, "isMoving"), (If, "isMovingUp"));
            To(walkDown, (IfNot, "inWater"), (If, "isMoving"), (If, "isMovingDown"));
            To(walk, (IfNot, "inWater"), (If, "isMoving"), (IfNot, "isMovingUp"), (IfNot, "isMovingDown"));
        }

        EditorUtility.SetDirty(river);
        return 0;
    }

    // =========================================================================
    // VERIFICACIÓN
    // =========================================================================

    private static int VerifyPlayerAnimations()
    {
        int problems = 0;
        void Expect(bool ok, string message)
        {
            if (ok) Debug.Log($"[Nivel2][OK] {message}");
            else { Debug.LogWarning($"[Nivel2][FALLA] {message}"); problems++; }
        }

        foreach (SwimSheet sheet in SwimSheets)
        {
            // Cada cuadro debe contener la figura completa (ningún pie ni mano fuera del corte)
            List<RectInt> figures = DetectSwimFrames(sheet, out _, out _, out _).SelectMany(r => r).ToList();
            Dictionary<string, Sprite> sprites = LoadSprites(sheet.path);
            List<Sprite> used = sprites.Where(kv => sheet.rowPrefixes.Any(p => kv.Key.StartsWith(p))).Select(kv => kv.Value).ToList();
            int cropped = figures.Count(f => !used.Any(s => s.rect.xMin <= f.xMin && s.rect.yMin <= f.yMin && s.rect.xMax >= f.xMax && s.rect.yMax >= f.yMax));
            Expect(cropped == 0 && used.Count == sheet.perRow.Sum(), $"{Path.GetFileName(sheet.path)}: {used.Count} cuadros completos, sin recortes");
        }

        // Caminar (lados, arriba, abajo) y reposo en la orilla: los mismos clips del Animator del Nivel 1
        var level1 = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(LEVEL1_CONTROLLER);
        var river = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(SWIM_CONTROLLER);
        Motion MotionOf(UnityEditor.Animations.AnimatorController c, string state) =>
            c == null ? null : c.layers[0].stateMachine.states.Select(s => s.state).FirstOrDefault(s => s.name == state)?.motion;
        foreach (var (riverState, level1State, label) in new[]
        {
            ("Mateo_Land_Walk", "Mateo_Walk", "caminar a los lados"), ("Mateo_Land_Walk_Up", "Mateo_Walk_Up", "caminar hacia arriba"),
            ("Mateo_Land_Walk_Down", "Mateo_Walk_Down", "caminar hacia abajo"), ("Mateo_Land_Idle", "New Mateo_Idle", "reposo"),
        })
        {
            Motion expected = MotionOf(level1, level1State);
            Expect(expected != null && MotionOf(river, riverState) == expected, $"En la orilla, {label} usa la misma animación del Nivel 1 ({expected?.name})");
        }
        return problems;
    }
}
