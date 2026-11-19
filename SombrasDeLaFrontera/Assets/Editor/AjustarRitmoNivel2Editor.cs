using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Ajusta lanchas, fauna y corrientes del Nivel 2 a la velocidad de nado actual de Mateo.
/// Los valores originales se diseñaron con nado = 2.6; se escalan por k = nadoActual / 2.6:
/// - Velocidades (lanchas, fauna, corrientes, zigzag de serpientes) × k
/// - Tiempos de reacción (espera de lanchas, captura del reflector, aviso previo, barrido) ÷ k
/// Así el nivel conserva su dificultad, solo con otro ritmo. Se puede volver a ejecutar sin acumular cambios.
/// </summary>
public static class AjustarRitmoNivel2Editor
{
    private const string SCENE = "Assets/Scenes/03_Level2_River.unity";
    private const float NADO_BASE = 2.6f;

    // Valores originales de SetupNivel2BoatsEditor / SetupNivel2HazardsEditor
    private static readonly (string nombre, float velocidad, float espera)[] Lanchas =
    {
        ("Lancha_1", 1.4f, 2.0f),
        ("Lancha_2", 1.6f, 1.6f),
        ("Lancha_3", 1.8f, 1.3f),
    };
    private const float CAPTURA_REFLECTOR = 0.5f;
    private const float BARRIDO_REFLECTOR = 3.5f;
    private const float AVISO_LANCHA = 1.5f;

    private static readonly (string nombre, float velocidad, float zigzagFrecuencia)[] Fauna =
    {
        ("Pejelagarto_1", 1.3f, 0f),
        ("SerpienteAgua_2", 1.05f, 0.6f),
        ("Cocodrilo_3", 0.85f, 0f),
        ("Pejelagarto_4", 1.6f, 0f),
        ("SerpienteAgua_5", 1.25f, 0.75f),
        ("Pejelagarto_6", 1.9f, 0f),
    };

    private static readonly (string nombre, Vector2 flujo)[] Corrientes =
    {
        ("Corriente_Lateral_Derecha", new Vector2(1.3f, 0f)),
        ("Contracorriente_1", new Vector2(0f, -1.1f)),
        ("Corriente_Lateral_Izquierda", new Vector2(-1.5f, 0f)),
        ("Contracorriente_2", new Vector2(0f, -1.3f)),
    };

    [MenuItem("Sombras de la Frontera/Nivel 2/4. Ajustar enemigos y corrientes a la velocidad de Mateo")]
    public static void Ajustar()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var escena = EditorSceneManager.OpenScene(SCENE, OpenSceneMode.Single);

        SwimController2D mateo = Object.FindAnyObjectByType<SwimController2D>();
        if (mateo == null) { Debug.LogError("[Ritmo N2] No se encontró a Mateo (SwimController2D)."); return; }

        float nado = new SerializedObject(mateo).FindProperty("swimSpeed").floatValue;
        float k = nado / NADO_BASE;
        int faltantes = 0;

        foreach (var (nombre, velocidad, espera) in Lanchas)
        {
            GameObject go = GameObject.Find(nombre);
            if (go == null) { faltantes++; Debug.LogWarning($"[Ritmo N2] No se encontró '{nombre}'."); continue; }

            Set(go.GetComponent<PatrolAI>(), so =>
            {
                so.FindProperty("moveSpeed").floatValue = R(velocidad * k);
                so.FindProperty("waitTimeAtWaypoint").floatValue = R(espera / k);
            });
            Set(go.GetComponent<PatrolBoat>(), so => so.FindProperty("warningLeadTime").floatValue = R(AVISO_LANCHA / k));
            Set(go.GetComponentInChildren<VisionCone2D>(), so =>
            {
                so.FindProperty("detectionTimeToCapture").floatValue = R(CAPTURA_REFLECTOR / k);
                so.FindProperty("sweepPeriod").floatValue = R(BARRIDO_REFLECTOR / k);
            });
        }

        foreach (var (nombre, velocidad, frecuencia) in Fauna)
        {
            GameObject go = GameObject.Find(nombre);
            if (go == null) { faltantes++; Debug.LogWarning($"[Ritmo N2] No se encontró '{nombre}'."); continue; }

            Set(go.GetComponent<HazardFauna>(), so =>
            {
                so.FindProperty("moveSpeed").floatValue = R(velocidad * k);
                if (frecuencia > 0f) so.FindProperty("zigzagFrequency").floatValue = R(frecuencia * k);
            });
        }

        foreach (var (nombre, flujo) in Corrientes)
        {
            GameObject go = GameObject.Find(nombre);
            if (go == null) { faltantes++; Debug.LogWarning($"[Ritmo N2] No se encontró '{nombre}'."); continue; }

            Set(go.GetComponent<CurrentZone2D>(), so =>
                so.FindProperty("flow").vector2Value = new Vector2(R(flujo.x * k), R(flujo.y * k)));
        }

        EditorSceneManager.MarkSceneDirty(escena);
        EditorSceneManager.SaveScene(escena);
        Debug.Log($"<color=green><b>[Ritmo N2]</b> Nado de Mateo = {nado} → factor {k:0.00}. " +
                  $"Lanchas, fauna y corrientes ajustadas{(faltantes > 0 ? $" ({faltantes} objeto(s) no encontrados)" : "")}.</color>");
    }

    private static void Set(Object componente, System.Action<SerializedObject> cambios)
    {
        if (componente == null) return;
        var so = new SerializedObject(componente);
        cambios(so);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static float R(float v) => Mathf.Round(v * 100f) / 100f;
}
