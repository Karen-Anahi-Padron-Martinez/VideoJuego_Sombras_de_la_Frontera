using UnityEngine;

/// <summary>
/// Sombras de las coberturas para linternas y reflectores:
/// solo los obstáculos que pueden ocultar a Mateo (los que tienen CoverZone2D) cortan la luz;
/// los que solo estorban (mallas, postes, arbustos secos...) la dejan pasar.
/// </summary>
public static class LightOcclusion2D
{
    private static readonly RaycastHit2D[] hits = new RaycastHit2D[16];

    /// <summary>Distancia que recorre la luz desde el origen en esa dirección antes de chocar con una cobertura.</summary>
    public static float ClearDistance(Vector2 origin, Vector2 direction, float maxDistance)
    {
        int count = Physics2D.RaycastNonAlloc(origin, direction, hits, maxDistance);
        float best = maxDistance;
        for (int i = 0; i < count; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || col.isTrigger || col.attachedRigidbody != null) continue;
            if (col.GetComponent<CoverZone2D>() == null) continue;
            best = Mathf.Min(best, hits[i].distance);
        }
        return best;
    }

    /// <summary>Verdadero si una cobertura se interpone entre la luz y el punto.</summary>
    public static bool IsBlocked(Vector2 from, Vector2 to)
    {
        Vector2 delta = to - from;
        float distance = delta.magnitude;
        if (distance < 0.01f) return false;
        return ClearDistance(from, delta / distance, distance) < distance - 0.02f;
    }
}
