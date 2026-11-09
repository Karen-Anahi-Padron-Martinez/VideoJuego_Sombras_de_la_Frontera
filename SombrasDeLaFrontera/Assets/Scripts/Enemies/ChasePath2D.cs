using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Búsqueda de camino para las criaturas que persiguen a Mateo (perros K-9 del Nivel 4).
/// Rejilla de celdas de 0.12 unidades: una celda es transitable si en ella no hay ningún collider sólido estático
/// (bases de obstáculos, rocas de las orillas, cercas y paredes). Mateo y las criaturas tienen Rigidbody2D, así que no estorban.
/// Los obstáculos no se mueven: cada celda se revisa una sola vez por escena y se guarda.
/// </summary>
public static class ChasePath2D
{
    private const float CELL = 0.12f;
    private const float CLEARANCE = 0.09f;          // Radio libre alrededor de los pies
    private static readonly Vector2 FeetProbe = new Vector2(0f, 0.06f);
    private const int MAX_EXPANDED = 5000;

    private static readonly Dictionary<Vector2Int, bool> walkable = new Dictionary<Vector2Int, bool>();
    private static readonly Collider2D[] overlapBuffer = new Collider2D[16];
    private static Scene cachedScene;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => walkable.Clear();

    private static Vector2Int ToCell(Vector2 p) => new Vector2Int(Mathf.RoundToInt(p.x / CELL), Mathf.RoundToInt(p.y / CELL));
    private static Vector2 ToWorld(Vector2Int c) => new Vector2(c.x * CELL, c.y * CELL);

    private static bool IsWalkable(Vector2Int cell)
    {
        Scene active = SceneManager.GetActiveScene();
        if (active != cachedScene) { walkable.Clear(); cachedScene = active; }

        if (walkable.TryGetValue(cell, out bool ok)) return ok;
        ok = !HasSolidAt(ToWorld(cell) + FeetProbe, CLEARANCE);
        walkable[cell] = ok;
        return ok;
    }

    private static bool HasSolidAt(Vector2 point, float radius)
    {
        int count = Physics2D.OverlapCircle(point, radius, ContactFilter2D.noFilter, overlapBuffer);
        for (int i = 0; i < count; i++)
        {
            Collider2D col = overlapBuffer[i];
            if (col != null && !col.isTrigger && col.attachedRigidbody == null) return true;
        }
        return false;
    }

    /// <summary>Verdadero si se puede caminar en línea recta de 'from' a 'to' sin tocar obstáculos.</summary>
    public static bool IsStraightClear(Vector2 from, Vector2 to)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / (CELL * 0.5f)));
        for (int i = 1; i <= steps; i++)
        {
            if (!IsWalkable(ToCell(Vector2.Lerp(from, to, i / (float)steps)))) return false;
        }
        return true;
    }

    /// <summary>
    /// Camino (lista de puntos, sin el punto de inicio) desde 'from' hasta 'to' rodeando obstáculos.
    /// Si el destino queda dentro de un obstáculo se usa la celda libre más cercana. Devuelve falso si no hay camino.
    /// </summary>
    public static bool FindPath(Vector2 from, Vector2 to, List<Vector2> result)
    {
        result.Clear();
        if (IsStraightClear(from, to)) { result.Add(to); return true; }

        Vector2Int start = ToCell(from);
        Vector2Int goal = NearestWalkable(ToCell(to));
        if (!IsWalkable(goal)) return false;

        var cost = new Dictionary<Vector2Int, float> { [start] = 0f };
        var parent = new Dictionary<Vector2Int, Vector2Int>();
        var open = new SortedSet<(float f, int x, int y)> { (Vector2Int.Distance(start, goal), start.x, start.y) };
        int expanded = 0;
        bool found = false;

        while (open.Count > 0 && expanded++ < MAX_EXPANDED)
        {
            var top = open.Min; open.Remove(top);
            var cur = new Vector2Int(top.x, top.y);
            if (cur == goal) { found = true; break; }

            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    var nb = new Vector2Int(cur.x + dx, cur.y + dy);
                    if (!IsWalkable(nb)) continue;
                    if (dx != 0 && dy != 0 && (!IsWalkable(new Vector2Int(cur.x + dx, cur.y)) || !IsWalkable(new Vector2Int(cur.x, cur.y + dy)))) continue;
                    float ng = cost[cur] + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                    if (cost.TryGetValue(nb, out float old) && old <= ng) continue;
                    cost[nb] = ng;
                    parent[nb] = cur;
                    open.Add((ng + Vector2Int.Distance(nb, goal), nb.x, nb.y));
                }
        }
        if (!found) return false;

        var cells = new List<Vector2>();
        for (var c = goal; c != start; c = parent[c]) cells.Add(ToWorld(c));
        cells.Reverse();

        // Suavizado: se saltan los puntos intermedios mientras haya línea recta libre
        Vector2 anchor = from;
        for (int i = 0; i < cells.Count; i++)
        {
            bool isLast = i == cells.Count - 1;
            if (isLast || !IsStraightClear(anchor, cells[i + 1]))
            {
                result.Add(cells[i]);
                anchor = cells[i];
            }
        }
        if (IsStraightClear(anchor, to)) result.Add(to);
        return result.Count > 0;
    }

    private static Vector2Int NearestWalkable(Vector2Int cell)
    {
        if (IsWalkable(cell)) return cell;
        for (int r = 1; r <= 4; r++)
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                {
                    if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue;
                    var c = new Vector2Int(cell.x + dx, cell.y + dy);
                    if (IsWalkable(c)) return c;
                }
        return cell;
    }
}
