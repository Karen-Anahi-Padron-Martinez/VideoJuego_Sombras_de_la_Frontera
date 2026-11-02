using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Zona iluminada bajo un poste de luz (Nivel 3).
/// Mientras Mateo está dentro y al descubierto, si una patrulla lo ve lo atrapa al instante.
/// Si está agachado tras una cobertura, la luz no lo delata.
/// </summary>
public class LightZone2D : MonoBehaviour
{
    [Tooltip("Radios de la elipse iluminada en el suelo (vista 3/4)")]
    [SerializeField] private Vector2 radii = new Vector2(1.15f, 0.7f);

    private static readonly List<LightZone2D> zones = new List<LightZone2D>();
    private static PlayerController2D player;
    private static int cachedFrame = -1;
    private static bool cachedLit;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        zones.Clear();
        player = null;
        cachedFrame = -1;
    }

    private void OnEnable() { if (!zones.Contains(this)) zones.Add(this); }
    private void OnDisable() => zones.Remove(this);

    /// <summary>Verdadero si Mateo está bajo alguna lámpara y al descubierto.</summary>
    public static bool PlayerLit { get { Refresh(); return cachedLit; } }

    private static void Refresh()
    {
        if (cachedFrame == Time.frameCount) return;
        cachedFrame = Time.frameCount;
        cachedLit = false;
        if (zones.Count == 0) return;

        if (player == null) player = FindAnyObjectByType<PlayerController2D>();
        if (player == null || CoverZone2D.isHiding || player.isHiding) return;

        Vector2 feet = player.GetFeetPosition();
        foreach (LightZone2D zone in zones)
        {
            if (zone == null || !zone.Contains(feet)) continue;
            cachedLit = true;
            return;
        }
    }

    private bool Contains(Vector2 point)
    {
        Vector2 d = point - (Vector2)transform.position;
        float nx = d.x / Mathf.Max(0.01f, radii.x);
        float ny = d.y / Mathf.Max(0.01f, radii.y);
        return nx * nx + ny * ny <= 1f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
        Vector3 c = transform.position;
        Vector3 prev = c + new Vector3(radii.x, 0f, 0f);
        for (int i = 1; i <= 32; i++)
        {
            float a = i / 32f * Mathf.PI * 2f;
            Vector3 p = c + new Vector3(Mathf.Cos(a) * radii.x, Mathf.Sin(a) * radii.y, 0f);
            Gizmos.DrawLine(prev, p);
            prev = p;
        }
    }
}
