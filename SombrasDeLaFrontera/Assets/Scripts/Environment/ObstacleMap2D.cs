using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Registro de las áreas que ocupan los obstáculos del escenario (cactus, rocas, arbustos, vehículos...).
/// La fauna lo consulta para no caminar encima ni a través de ellos.
/// Un obstáculo es todo sprite estático ordenado por su base (YSort2D estático) que tiene un collider sólido.
/// </summary>
public static class ObstacleMap2D
{
    private static readonly List<Rect> rects = new List<Rect>();
    private static Scene cachedScene;
    private static bool built;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        rects.Clear();
        built = false;
    }

    private static void EnsureBuilt()
    {
        Scene active = SceneManager.GetActiveScene();
        if (built && active == cachedScene) return;

        built = true;
        cachedScene = active;
        rects.Clear();

        foreach (YSort2D ysort in Object.FindObjectsByType<YSort2D>(FindObjectsSortMode.None))
        {
            if (!ysort.isStatic || !ysort.useBottomBounds) continue;

            bool hasSolid = false;
            foreach (Collider2D col in ysort.GetComponents<Collider2D>())
            {
                if (col.enabled && !col.isTrigger) { hasSolid = true; break; }
            }
            if (!hasSolid) continue;

            SpriteRenderer sr = ysort.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            // Se recortan un poco los márgenes transparentes del sprite
            Bounds b = sr.bounds;
            float insetX = b.size.x * 0.08f;
            rects.Add(new Rect(b.min.x + insetX, b.min.y, b.size.x - 2f * insetX, b.size.y * 0.92f));
        }
    }

    /// <summary>Verdadero si el rectángulo (cuerpo de una criatura) se encima con algún obstáculo.</summary>
    public static bool Overlaps(Rect body)
    {
        EnsureBuilt();
        for (int i = 0; i < rects.Count; i++)
        {
            if (rects[i].Overlaps(body)) return true;
        }
        return false;
    }

    /// <summary>
    /// Verdadero si una criatura de tamaño bodySize (pies en 'from') puede caminar en línea recta hasta 'to'
    /// sin encimarse con ningún obstáculo.
    /// </summary>
    public static bool IsPathClear(Vector2 from, Vector2 to, Vector2 bodySize)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.08f));
        for (int i = 1; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / (float)steps);
            if (Overlaps(new Rect(p.x - bodySize.x * 0.5f, p.y, bodySize.x, bodySize.y))) return false;
        }
        return true;
    }
}
