using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Herramienta de Editor para distribuir inteligentemente los consumibles
/// (5 Cantimploras, 10 Botellas y 12 Tunas) en áreas 100% caminables y despejadas,
/// garantizando que NINGÚN ítem quede dentro o detrás de obstáculos sólidos o paredes.
/// </summary>
public static class PlacePickupsEditor
{
    [MenuItem("Sombras de la Frontera/Distribuir Consumibles (5 Cantimploras, 10 Botellas, 12 Tunas - Terreno Despejado)")]
    public static void DistributePickups()
    {
        // 1. Cargar prefabs desde la carpeta de Items
        GameObject cantimplora1Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Pickup_Cantimplora_1.prefab");
        GameObject cantimplora2Prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Pickup_Cantimplora_2.prefab");
        GameObject botellaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Pickup_Botella.prefab");
        GameObject tunaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Items/Pickup_Tuna.prefab");

        if (cantimplora1Prefab == null || cantimplora2Prefab == null || botellaPrefab == null || tunaPrefab == null)
        {
            Debug.LogError("[PlacePickups] Falta algún prefab en 'Assets/Prefabs/Items/'. Verifica que existan: Pickup_Cantimplora_1, Pickup_Cantimplora_2, Pickup_Botella y Pickup_Tuna.");
            return;
        }

        // 2. Limpiar contenedor previo si existía
        GameObject existingRoot = GameObject.Find("Pickups");
        if (existingRoot != null)
        {
            Undo.DestroyObjectImmediate(existingRoot);
        }

        // 3. Crear estructura organizada en la Jerarquía
        GameObject pickupsRoot = new GameObject("Pickups");
        Undo.RegisterCreatedObjectUndo(pickupsRoot, "Distribuir Consumibles Despejados");

        GameObject cantimplorasParent = new GameObject("Cantimploras");
        cantimplorasParent.transform.SetParent(pickupsRoot.transform);

        GameObject botellasParent = new GameObject("Botellas");
        botellasParent.transform.SetParent(pickupsRoot.transform);

        GameObject tunasParent = new GameObject("Tunas");
        tunasParent.transform.SetParent(pickupsRoot.transform);

        // 4. Recopilar todos los colisionadores sólidos de la escena (obstáculos y paredes que bloquean al jugador)
        Physics2D.SyncTransforms();
        Collider2D[] allSceneColliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        List<Collider2D> solidObstacles = new List<Collider2D>();

        foreach (Collider2D col in allSceneColliders)
        {
            // Solo considerar colisionadores físicos sólidos (no triggers de sigilo ni arena)
            if (col != null && !col.isTrigger && col.gameObject.name != "mateo_part1_7")
            {
                solidObstacles.Add(col);
            }
        }

        List<Vector2> placedPickupPositions = new List<Vector2>();

        // 5. DISTRIBUCIÓN: 5 Cantimploras (+25% agua, alternadas)
        float[] cantimploraBaseY = new float[]
        {
            4.8f,   // Tramo 1
            13.2f,  // Tramo 2
            19.5f,  // Tramo 3
            23.8f,  // Tramo 3
            29.5f   // Tramo 4
        };

        for (int i = 0; i < cantimploraBaseY.Length; i++)
        {
            GameObject prefab = (i % 2 == 0) ? cantimplora1Prefab : cantimplora2Prefab;
            Vector2 safePos = FindClearWalkablePosition(cantimploraBaseY[i], solidObstacles, placedPickupPositions);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, cantimplorasParent.transform);
            instance.transform.position = new Vector3(safePos.x, safePos.y, 0f);
            instance.name = $"Cantimplora_{(i % 2 == 0 ? "1" : "2")}_{i + 1}";
            placedPickupPositions.Add(safePos);
            Undo.RegisterCreatedObjectUndo(instance, "Instanciar Cantimplora");
        }

        // 6. DISTRIBUCIÓN: 10 Botellas (+10% agua)
        float[] botellaBaseY = new float[]
        {
            2.5f, 6.8f,             // Tramo 1 (2)
            10.2f, 12.0f, 15.5f,    // Tramo 2 (3)
            17.8f, 21.2f, 25.2f,    // Tramo 3 (3)
            27.6f, 31.8f            // Tramo 4 (2)
        };

        for (int i = 0; i < botellaBaseY.Length; i++)
        {
            Vector2 safePos = FindClearWalkablePosition(botellaBaseY[i], solidObstacles, placedPickupPositions);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(botellaPrefab, botellasParent.transform);
            instance.transform.position = new Vector3(safePos.x, safePos.y, 0f);
            instance.name = $"Botella_{i + 1}";
            placedPickupPositions.Add(safePos);
            Undo.RegisterCreatedObjectUndo(instance, "Instanciar Botella");
        }

        // 7. DISTRIBUCIÓN: 12 Tunas (+7% agua y boost de velocidad 3s)
        float[] tunaBaseY = new float[]
        {
            2.0f, 3.8f, 7.8f,       // Tramo 1 (3)
            9.2f, 11.5f, 14.5f,     // Tramo 2 (3)
            17.0f, 20.2f, 23.0f,    // Tramo 3 (3)
            26.8f, 30.2f, 33.2f     // Tramo 4 (3)
        };

        for (int i = 0; i < tunaBaseY.Length; i++)
        {
            Vector2 safePos = FindClearWalkablePosition(tunaBaseY[i], solidObstacles, placedPickupPositions);

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(tunaPrefab, tunasParent.transform);
            instance.transform.position = new Vector3(safePos.x, safePos.y, 0f);
            instance.name = $"Tuna_{i + 1}";
            placedPickupPositions.Add(safePos);
            Undo.RegisterCreatedObjectUndo(instance, "Instanciar Tuna");
        }

        // 8. Marcar escena como modificada para guardar con Ctrl+S
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green><b>[PlacePickups] ¡Colocación 100% despejada completada!</b></color> Se instanciaron exactamente 5 Cantimploras, 10 Botellas y 12 Tunas verificando que ninguna coincida con obstáculos ni paredes. Presiona Ctrl+S para guardar.");
    }

    /// <summary>
    /// Busca un punto dentro del ancho del camino que esté completamente libre de colisiones sólidas y alejado de otros ítems.
    /// </summary>
    private static Vector2 FindClearWalkablePosition(float targetY, List<Collider2D> solidObstacles, List<Vector2> existingPickups)
    {
        float minX = -1.85f;
        float maxX = 1.85f;
        float itemSafetyRadius = 0.45f;
        float minDistanceBetweenPickups = 0.65f;

        Vector2 bestPos = new Vector2(0f, targetY);
        float bestDistanceToObstacles = -1f;

        // Intentar hasta 100 candidatos aleatorios alrededor del target Y
        for (int attempt = 0; attempt < 100; attempt++)
        {
            float candidateX = Random.Range(minX, maxX);
            float candidateY = targetY + Random.Range(-0.55f, 0.55f);
            Vector2 candidate = new Vector2(candidateX, candidateY);

            // 1. Verificar si está demasiado cerca de otro ítem ya colocado
            bool tooCloseToItem = false;
            foreach (Vector2 placed in existingPickups)
            {
                if (Vector2.Distance(candidate, placed) < minDistanceBetweenPickups)
                {
                    tooCloseToItem = true;
                    break;
                }
            }
            if (tooCloseToItem) continue;

            // 2. Verificar distancia contra todos los obstáculos sólidos de la escena
            bool isBlocked = false;
            float minDistanceToSolid = float.MaxValue;

            foreach (Collider2D solid in solidObstacles)
            {
                if (solid == null) continue;

                // Si el punto está contenido en los bounds del obstáculo
                if (solid.bounds.Contains(candidate))
                {
                    isBlocked = true;
                    break;
                }

                // Si el punto cae dentro del colisionador
                if (solid.OverlapPoint(candidate))
                {
                    isBlocked = true;
                    break;
                }

                // Medir distancia exacta al punto más cercano del obstáculo
                Vector2 closestPoint = solid.ClosestPoint(candidate);
                float dist = Vector2.Distance(candidate, closestPoint);

                if (dist < itemSafetyRadius)
                {
                    isBlocked = true;
                    break;
                }

                if (dist < minDistanceToSolid)
                {
                    minDistanceToSolid = dist;
                }
            }

            // Si el punto está 100% libre de obstáculos y paredes, lo usamos de inmediato
            if (!isBlocked)
            {
                return candidate;
            }

            // Guardar el candidato con mayor distancia a obstáculos como fallback
            if (minDistanceToSolid > bestDistanceToObstacles)
            {
                bestDistanceToObstacles = minDistanceToSolid;
                bestPos = candidate;
            }
        }

        return bestPos;
    }
}
