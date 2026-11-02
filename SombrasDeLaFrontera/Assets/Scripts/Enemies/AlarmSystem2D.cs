using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sistema de alarma del Nivel 3: un ladrido, la sospecha de un policía o el reflector de una torre
/// avisan a las patrullas cercanas, que caminan más rápido durante unos segundos.
/// Solo funciona en las escenas que tienen este componente (en el Nivel 1 no existe y no cambia nada).
/// </summary>
public class AlarmSystem2D : MonoBehaviour
{
    [Tooltip("Multiplicador de velocidad de las patrullas alertadas")]
    [SerializeField] private float speedMultiplier = 1.6f;

    [Tooltip("Segundos que dura cada alarma")]
    [SerializeField] private float duration = 6f;

    [Tooltip("Radio por defecto en el que se escucha la alarma")]
    [SerializeField] private float defaultRadius = 4.5f;

    private struct Alarm { public Vector2 position; public float radius; public float endTime; }

    private static AlarmSystem2D instance;
    private readonly List<Alarm> alarms = new List<Alarm>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState() => instance = null;

    private void OnEnable() => instance = this;
    private void OnDisable() { if (instance == this) instance = null; }

    /// <summary>Da la alarma en un punto. Si ya hay una alarma cerca, solo se renueva.</summary>
    public static void Raise(Vector2 position, float radius = -1f)
    {
        if (instance == null) return;
        if (radius <= 0f) radius = instance.defaultRadius;
        float end = Time.time + instance.duration;

        for (int i = 0; i < instance.alarms.Count; i++)
        {
            Alarm a = instance.alarms[i];
            if (a.endTime > Time.time && Vector2.Distance(a.position, position) < 1.5f)
            {
                a.endTime = end;
                a.radius = Mathf.Max(a.radius, radius);
                instance.alarms[i] = a;
                return;
            }
        }
        instance.alarms.Add(new Alarm { position = position, radius = radius, endTime = end });
        AudioManager.Instance?.PlayAlarmSiren();
        AudioManager.Instance?.PlayRadioAt(position);
        Debug.Log($"<color=orange>[AlarmSystem2D] ¡Alarma en {position}! Las patrullas cercanas aceleran el paso.</color>");
    }

    /// <summary>Multiplicador de velocidad para una patrulla en esa posición (1 si no hay alarma cerca).</summary>
    public static float SpeedMultiplierAt(Vector2 position)
    {
        if (instance == null) return 1f;
        foreach (Alarm a in instance.alarms)
        {
            if (a.endTime > Time.time && (a.position - position).sqrMagnitude <= a.radius * a.radius)
                return instance.speedMultiplier;
        }
        return 1f;
    }

    /// <summary>Verdadero mientras haya alguna alarma activa en el nivel.</summary>
    public static bool AnyActive
    {
        get
        {
            if (instance == null) return false;
            foreach (Alarm a in instance.alarms) if (a.endTime > Time.time) return true;
            return false;
        }
    }

    private void Update()
    {
        alarms.RemoveAll(a => a.endTime <= Time.time);
    }
}
