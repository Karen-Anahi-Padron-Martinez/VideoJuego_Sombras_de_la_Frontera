using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Configura automáticamente la profundidad 2D (Y-Sort) en toda la escena:
/// 1. Todos los obstáculos (árboles, cactus, rocas, arbustos, camionetas, carros, nopaleras)
///    se ordenan según su base inferior (bounds.min.y).
/// 2. Todas las criaturas móviles (coyotes, perros, serpientes, alacranes y Mateo)
///    actualizan dinámicamente su capa de profundidad cada frame según su posición Y.
/// 3. Resultado: Cuando cualquier enemigo o Mateo pasa por detrás o entra a una cobertura,
///    el obstáculo se dibuja por DELANTE y lo oculta de forma 100% natural.
/// </summary>
public static class SetupDepthSortingEditor
{
    private static readonly string[] OBSTACLE_PARENT_NAMES = new string[]
    {
        "Nopaleras",
        "Cactus",
        "Roca_Pequeña",
        "Roca_Grande",
        "Arbol",
        "Arbol_Pequeño",
        "Arbusto",
        "Arbusto_Seco",
        "Carro",
        "Camioneta"
    };

    [MenuItem("Sombras de la Frontera/Configurar Profundidad y Cobertura de Obstáculos (Y-Sort)")]
    public static void ApplyDepthSortingToScene()
    {
        int obstaclesConfigured = 0;
        int creaturesConfigured = 0;

        // 1. Configurar todos los obstáculos estáticos de la escena
        foreach (string groupName in OBSTACLE_PARENT_NAMES)
        {
            GameObject group = GameObject.Find(groupName);
            if (group == null) continue;

            SpriteRenderer[] renderers = group.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in renderers)
            {
                if (sr == null) continue;

                Undo.RecordObject(sr, "Update Sorting");
                sr.sortingLayerName = "Environment";

                YSort2D ysort = sr.GetComponent<YSort2D>();
                if (ysort == null)
                {
                    ysort = Undo.AddComponent<YSort2D>(sr.gameObject);
                }

                Undo.RecordObject(ysort, "Update YSort");
                ysort.isStatic = true;
                ysort.useBottomBounds = true;
                ysort.baseOrder = 5000;
                ysort.UpdateSortingOrder();

                EditorUtility.SetDirty(sr);
                EditorUtility.SetDirty(ysort);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sr);

                obstaclesConfigured++;
            }
        }

        // 2. Configurar todas las criaturas enemigas en la escena
        GameObject enemiesRoot = GameObject.Find("EnemiesAndHazards");
        if (enemiesRoot != null)
        {
            SpriteRenderer[] enemyRenderers = enemiesRoot.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in enemyRenderers)
            {
                if (sr == null) continue;

                // Si es un cono de visión, no tocarlo (usa Foreground)
                if (sr.GetComponent<VisionCone2D>() != null || sr.name.Contains("VisionCone")) continue;

                // Sombras (Background) e iconos de alerta (Foreground) tienen su propia capa
                if (sr.GetComponent<BlobShadow>() != null || sr.GetComponent<AlertIndicator>() != null) continue;

                sr.sortingLayerName = "Environment";

                YSort2D ysort = sr.GetComponent<YSort2D>();
                if (ysort == null)
                {
                    ysort = Undo.AddComponent<YSort2D>(sr.gameObject);
                }

                ysort.isStatic = false;
                ysort.useBottomBounds = false;
                ysort.baseOrder = 5000;
                ysort.UpdateSortingOrder();

                creaturesConfigured++;
            }
        }

        // 3. Configurar a Mateo (Player)
        GameObject playerObj = GameObject.Find("mateo_part1_7");
        if (playerObj == null) playerObj = GameObject.Find("Player");

        if (playerObj != null)
        {
            SpriteRenderer playerSr = playerObj.GetComponentInChildren<SpriteRenderer>();
            if (playerSr != null)
            {
                playerSr.sortingLayerName = "Environment";

                YSort2D ysort = playerSr.GetComponent<YSort2D>();
                if (ysort == null)
                {
                    ysort = Undo.AddComponent<YSort2D>(playerSr.gameObject);
                }

                ysort.isStatic = false;
                ysort.useBottomBounds = false;
                // Pies de Mateo: el pivote de su sprite está al centro del cuerpo
                ysort.yOffset = playerSr.sprite != null ? playerSr.sprite.bounds.min.y * playerSr.transform.lossyScale.y : -0.2f;
                ysort.baseOrder = 5000;
                ysort.UpdateSortingOrder();

                creaturesConfigured++;
            }
        }

        // 4. Actualizar prefabs de enemigos para que futuras instancias nazcan con YSort2D
        ConfigurePrefabYSort("Assets/Prefabs/Enemies/Hazard_Snake.prefab");
        ConfigurePrefabYSort("Assets/Prefabs/Enemies/Hazard_Scorpion.prefab");
        ConfigurePrefabYSort("Assets/Prefabs/Enemies/Enemy_GuardDog.prefab");
        ConfigurePrefabYSort("Assets/Prefabs/Enemies/Enemy_GuardDog_2.prefab");
        ConfigurePrefabYSort("Assets/Prefabs/Enemies/Enemy_CoyotePatrol.prefab");

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"<color=green><b>[Profundidad 2D]</b> ¡Configuración completada! {obstaclesConfigured} obstáculos y {creaturesConfigured} criaturas ordenados por profundidad Y.</color>");
    }

    private static void ConfigurePrefabYSort(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) return;

        SpriteRenderer sr = prefab.GetComponentInChildren<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Environment";

            YSort2D ysort = sr.GetComponent<YSort2D>();
            if (ysort == null)
            {
                ysort = sr.gameObject.AddComponent<YSort2D>();
            }

            ysort.isStatic = false;
            ysort.useBottomBounds = false;
            ysort.baseOrder = 5000;

            EditorUtility.SetDirty(prefab);
        }
    }
}
