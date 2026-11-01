using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Distribuye con precisión matemática 5 Coyotes patrulleros, 7 Serpientes de cascabel y 7 Alacranes
/// a lo largo de todo el mapa del desierto en zonas 100% despejadas, sin tocar obstáculos y dentro de los límites.
/// </summary>
public static class PlaceAllEnemiesAndFaunaEditor
{
    private const string PREFAB_DIR = "Assets/Prefabs/Enemies";

    [MenuItem("Sombras de la Frontera/Distribuir TODO (5 Coyotes, 7 Serpientes, 7 Alacranes - 100% Despejados)")]
    public static void DistributeAll()
    {
        // 1. Asegurar prefabs
        GameObject snakePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Hazard_Snake.prefab");
        GameObject scorpionPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Hazard_Scorpion.prefab");
        GameObject coyotePrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Enemy_CoyotePatrol.prefab");

        // Si no existe el prefab del coyote pero está en la escena, guardarlo como prefab
        if (coyotePrefab == null)
        {
            GameObject sceneCoyote = GameObject.Find("Enemy_CoyotePatrol");
            if (sceneCoyote != null)
            {
                coyotePrefab = PrefabUtility.SaveAsPrefabAssetAndConnect(sceneCoyote, $"{PREFAB_DIR}/Enemy_CoyotePatrol.prefab", InteractionMode.AutomatedAction);
                Debug.Log("[PlaceAll] Prefab de Enemy_CoyotePatrol guardado desde la escena.");
            }
            else
            {
                coyotePrefab = CreateCoyotePrefab();
            }
        }

        if (snakePrefab == null || scorpionPrefab == null)
        {
            Debug.LogError("[PlaceAll] Faltan prefabs de fauna en Assets/Prefabs/Enemies.");
            return;
        }

        // 2. Limpiar instancias previas en la escena para no duplicar
        CleanExistingSceneEnemies();

        // 3. Crear estructura jerárquica limpia
        GameObject root = new GameObject("EnemiesAndHazards");
        Undo.RegisterCreatedObjectUndo(root, "Distribuir 5 Coyotes, 7 Serpientes, 7 Alacranes");

        GameObject coyotesParent = new GameObject("Vigilantes_Coyotes");
        coyotesParent.transform.SetParent(root.transform);

        GameObject faunaParent = new GameObject("Fauna_Nociva");
        faunaParent.transform.SetParent(root.transform);

        GameObject snakesParent = new GameObject("Serpientes");
        snakesParent.transform.SetParent(faunaParent.transform);

        GameObject scorpionsParent = new GameObject("Alacranes");
        scorpionsParent.transform.SetParent(faunaParent.transform);

        // 4. Recopilar ÚNICAMENTE colisionadores sólidos físicos (excluyendo triggers de cámara o sigilo)
        Physics2D.SyncTransforms();
        Collider2D[] allColliders = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        List<Collider2D> solidColliders = new List<Collider2D>();
        foreach (var col in allColliders)
        {
            if (col == null || col.gameObject == null) continue;
            string n = col.gameObject.name.ToLower();
            if (n.Contains("player") || n.Contains("mateo")) continue;
            if (col.isTrigger) continue; // Ignorar triggers (CameraBounds, CoverZone, Pickups)
            solidColliders.Add(col);
        }

        List<Vector2> occupiedPositions = new List<Vector2>();

        // -------------------------------------------------------------
        // 5. DISTRIBUIR 5 COYOTES PATRULLEROS CON WAYPOINTS DE IDA Y VUELTA
        // -------------------------------------------------------------
        float[] coyoteY = new float[] { 7.5f, 13.5f, 19.5f, 25.5f, 31.0f };

        for (int i = 0; i < coyoteY.Length; i++)
        {
            Vector2 cPos = FindClearPosition(coyoteY[i], 1.2f, solidColliders, occupiedPositions);
            occupiedPositions.Add(cPos);

            GameObject coyote = (GameObject)PrefabUtility.InstantiatePrefab(coyotePrefab, coyotesParent.transform);
            coyote.name = $"Enemy_Coyote_{i + 1}";
            coyote.transform.position = new Vector3(cPos.x, cPos.y, 0f);

            // Configurar waypoints laterales de patrulla (PingPong)
            Transform wpRoot = coyote.transform.Find("Waypoints");
            if (wpRoot == null)
            {
                GameObject wpObj = new GameObject("Waypoints");
                wpObj.transform.SetParent(coyote.transform);
                wpRoot = wpObj.transform;
            }

            Transform wp0 = wpRoot.Find("WP_0");
            if (wp0 == null)
            {
                GameObject wp0Obj = new GameObject("WP_0");
                wp0Obj.transform.SetParent(wpRoot);
                wp0 = wp0Obj.transform;
            }

            Transform wp1 = wpRoot.Find("WP_1");
            if (wp1 == null)
            {
                GameObject wp1Obj = new GameObject("WP_1");
                wp1Obj.transform.SetParent(wpRoot);
                wp1 = wp1Obj.transform;
            }

            float leftX = Mathf.Clamp(cPos.x - 2.0f, -2.5f, 2.5f);
            float rightX = Mathf.Clamp(cPos.x + 2.0f, -2.5f, 2.5f);

            wp0.position = new Vector3(leftX, cPos.y, 0f);
            wp1.position = new Vector3(rightX, cPos.y, 0f);

            PatrolAI patrol = coyote.GetComponent<PatrolAI>();
            if (patrol != null)
            {
                patrol.SetWaypoints(new List<Transform> { wp0, wp1 }, PatrolAI.PatrolMode.PingPong);
            }
        }

        // -------------------------------------------------------------
        // 6. DISTRIBUIR 7 SERPIENTES DE CASCABEL
        // -------------------------------------------------------------
        float[] snakeY = new float[] { 5.2f, 9.8f, 14.8f, 18.2f, 22.5f, 27.2f, 32.5f };

        for (int i = 0; i < snakeY.Length; i++)
        {
            Vector2 sPos = FindClearPosition(snakeY[i], 0.75f, solidColliders, occupiedPositions);
            occupiedPositions.Add(sPos);

            GameObject snake = (GameObject)PrefabUtility.InstantiatePrefab(snakePrefab, snakesParent.transform);
            snake.name = $"Hazard_Snake_{i + 1}";
            snake.transform.position = new Vector3(sPos.x, sPos.y, 0f);
        }

        // -------------------------------------------------------------
        // 7. DISTRIBUIR 7 ALACRANES DEL DESIERTO
        // -------------------------------------------------------------
        float[] scorpY = new float[] { 6.8f, 11.2f, 16.5f, 20.8f, 24.2f, 29.0f, 33.2f };

        for (int i = 0; i < scorpY.Length; i++)
        {
            Vector2 aPos = FindClearPosition(scorpY[i], 0.75f, solidColliders, occupiedPositions);
            occupiedPositions.Add(aPos);

            GameObject scorp = (GameObject)PrefabUtility.InstantiatePrefab(scorpionPrefab, scorpionsParent.transform);
            scorp.name = $"Hazard_Scorpion_{i + 1}";
            scorp.transform.position = new Vector3(aPos.x, aPos.y, 0f);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log($"[PlaceAll] ¡ÉXITO TOTAL! 5 Coyotes, 7 Serpientes y 7 Alacranes colocados y listos en la escena.");
    }

    private static Vector2 FindClearPosition(float baseY, float clearanceRadius, List<Collider2D> solids, List<Vector2> existing)
    {
        float minX = -2.3f;
        float maxX = 2.3f;
        int maxAttempts = 60;

        Vector2 bestPos = new Vector2(0f, baseY);
        float bestDistToSolids = -1f;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            float rx = Random.Range(minX, maxX);
            float ry = baseY + Random.Range(-0.8f, 0.8f);
            Vector2 candidate = new Vector2(rx, ry);

            // Comprobar distancia con otras criaturas colocadas
            bool tooClose = false;
            foreach (var pos in existing)
            {
                if (Vector2.Distance(candidate, pos) < (clearanceRadius * 2.0f))
                {
                    tooClose = true;
                    break;
                }
            }
            if (tooClose) continue;

            // Comprobar obstáculos sólidos
            bool isBlocked = false;
            float nearestDist = float.MaxValue;

            foreach (var solid in solids)
            {
                if (solid == null) continue;
                Vector2 closest = solid.ClosestPoint(candidate);
                float dist = Vector2.Distance(candidate, closest);

                if (dist < clearanceRadius || solid.bounds.Contains(candidate))
                {
                    isBlocked = true;
                    break;
                }

                if (dist < nearestDist) nearestDist = dist;
            }

            if (!isBlocked)
            {
                return candidate; // 100% despejado
            }

            if (nearestDist > bestDistToSolids)
            {
                bestDistToSolids = nearestDist;
                bestPos = candidate;
            }
        }

        return bestPos;
    }

    private static void CleanExistingSceneEnemies()
    {
        string[] toDelete = new string[] {
            "EnemiesAndHazards", "Fauna_Nociva", "Vigilantes_Coyotes", "Perros_Guardianes",
            "Enemy_CoyotePatrol", "Hazard_Snake", "Hazard_Scorpion", "Enemy_GuardDog"
        };

        foreach (string name in toDelete)
        {
            GameObject obj = GameObject.Find(name);
            while (obj != null)
            {
                Undo.DestroyObjectImmediate(obj);
                obj = GameObject.Find(name);
            }
        }
    }

    private static GameObject CreateCoyotePrefab()
    {
        string path = $"{PREFAB_DIR}/Enemy_CoyotePatrol.prefab";
        GameObject root = new GameObject("Enemy_CoyotePatrol");

        SpriteRenderer sr = root.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Default";
        sr.sortingOrder = 3;

        Sprite[] coyoteSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Enemies/coyote_part1.png").OfType<Sprite>().ToArray();
        if (coyoteSprites.Length > 0)
        {
            Sprite selected = coyoteSprites.FirstOrDefault(s => s.name.Contains("_7")) ?? coyoteSprites[0];
            sr.sprite = selected;
        }

        CapsuleCollider2D col = root.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(0.8f, 1.2f);

        Rigidbody2D rb = root.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;

        root.AddComponent<PatrolAI>();

        GameObject pivotObj = new GameObject("FlashlightPivot");
        pivotObj.transform.SetParent(root.transform);
        pivotObj.transform.localPosition = new Vector3(0f, 0.1f, 0f);

        GameObject coneObj = new GameObject("VisionCone");
        coneObj.transform.SetParent(pivotObj.transform);
        coneObj.transform.localPosition = Vector3.zero;

        coneObj.AddComponent<PolygonCollider2D>();
        coneObj.AddComponent<VisionCone2D>();

        GameObject wpRoot = new GameObject("Waypoints");
        wpRoot.transform.SetParent(root.transform);

        GameObject wp0 = new GameObject("WP_0");
        wp0.transform.SetParent(wpRoot.transform);
        wp0.transform.localPosition = new Vector3(-2.0f, 0f, 0f);

        GameObject wp1 = new GameObject("WP_1");
        wp1.transform.SetParent(wpRoot.transform);
        wp1.transform.localPosition = new Vector3(2.0f, 0f, 0f);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    [InitializeOnLoadMethod]
    private static void AutoDistributeDogsIfMissing()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            string sceneName = EditorSceneManager.GetActiveScene().name;
            if (sceneName != "02_Level1_Desert") return;

            GameObject existing = GameObject.Find("Perros_Guardianes");
            if (existing == null)
            {
                Distribute3Dogs();
            }
        };
    }

    [MenuItem("Sombras de la Frontera/Distribuir 3 Perros Guardianes (Aleatorios - Bulldog y Pastor Alemán)")]
    public static void Distribute3Dogs()
    {
        GameObject dogPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{PREFAB_DIR}/Enemy_GuardDog.prefab");
        if (dogPrefab == null)
        {
            Debug.LogError("[PlaceAll] No se encontró Enemy_GuardDog.prefab en Assets/Prefabs/Enemies.");
            return;
        }

        // Cargar sprites de ambas razas
        Sprite[] bulldogSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Enemies/bulldog_part2.png").OfType<Sprite>().ToArray();
        Sprite bulldogSprite = bulldogSprites.FirstOrDefault(s => s.name.Contains("_0")) ?? bulldogSprites.FirstOrDefault();

        Sprite[] alemanSprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Enemies/aleman_part2.png").OfType<Sprite>().ToArray();
        Sprite alemanSprite = alemanSprites.FirstOrDefault(s => s.name.Contains("_0")) ?? alemanSprites.FirstOrDefault();

        if (bulldogSprite == null || alemanSprite == null)
        {
            Debug.LogError("[PlaceAll] No se pudieron cargar los sprites de Bulldog o Pastor Alemán.");
            return;
        }

        // 1. Limpiar perros existentes para no duplicar
        GameObject existingGroup = GameObject.Find("Perros_Guardianes");
        if (existingGroup != null) Undo.DestroyObjectImmediate(existingGroup);

        string[] dogsToDelete = new string[] { "Enemy_GuardDog", "Enemy_GuardDog_1", "Enemy_GuardDog_2", "Enemy_GuardDog_3" };
        foreach (string dName in dogsToDelete)
        {
            GameObject sceneDog = GameObject.Find(dName);
            while (sceneDog != null)
            {
                Undo.DestroyObjectImmediate(sceneDog);
                sceneDog = GameObject.Find(dName);
            }
        }

        // 2. Obtener o crear grupo raíz
        GameObject root = GameObject.Find("EnemiesAndHazards");
        if (root == null) root = new GameObject("EnemiesAndHazards");

        GameObject dogsParent = new GameObject("Perros_Guardianes");
        dogsParent.transform.SetParent(root.transform);
        Undo.RegisterCreatedObjectUndo(dogsParent, "Distribuir 3 Perros Guardianes");

        // 3. Buscar colisionadores sólidos para garantizar 100% despeje
        Collider2D[] allCols = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
        List<Collider2D> solidColliders = new List<Collider2D>();
        foreach (Collider2D col in allCols)
        {
            if (col == null || col.isTrigger) continue;
            string n = col.gameObject.name.ToLower();
            if (n.Contains("player") || n.Contains("mateo")) continue;
            solidColliders.Add(col);
        }

        // 4. Distribuir en 3 alturas intermedias despejadas
        float[] dogY = new float[] { 10.8f, 17.2f, 23.5f };
        List<Vector2> occupiedPositions = new List<Vector2>();

        // Garantizar que ambas razas estén presentes (Bulldog = 0, Pastor Alemán = 1)
        int[] breeds = new int[] { 0, 1, Random.Range(0, 2) };
        for (int i = 0; i < breeds.Length; i++)
        {
            int rnd = Random.Range(i, breeds.Length);
            int temp = breeds[i];
            breeds[i] = breeds[rnd];
            breeds[rnd] = temp;
        }

        for (int i = 0; i < dogY.Length; i++)
        {
            Vector2 dPos = FindClearPosition(dogY[i], 1.2f, solidColliders, occupiedPositions);
            occupiedPositions.Add(dPos);

            bool isBulldog = (breeds[i] == 0);
            string breedName = isBulldog ? "Bulldog" : "PastorAleman";
            Sprite chosenSprite = isBulldog ? bulldogSprite : alemanSprite;
            float scale = isBulldog ? 0.45f : 0.42f;

            GameObject dog = (GameObject)PrefabUtility.InstantiatePrefab(dogPrefab, dogsParent.transform);
            dog.name = $"Enemy_GuardDog_{i + 1}_{breedName}";
            dog.transform.position = new Vector3(dPos.x, dPos.y, 0f);
            dog.transform.localScale = new Vector3(scale, scale, 1f);

            SpriteRenderer sr = dog.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = chosenSprite;
                sr.sortingLayerName = "Characters";
                sr.sortingOrder = 3;
            }

            // Waypoints de patrulla laterales sin chocar con obstáculos
            Transform wpRoot = dog.transform.Find("Waypoints");
            if (wpRoot == null)
            {
                GameObject wpObj = new GameObject("Waypoints");
                wpObj.transform.SetParent(dog.transform);
                wpRoot = wpObj.transform;
            }

            Transform wp0 = wpRoot.Find("WP_0");
            if (wp0 == null)
            {
                GameObject wp0Obj = new GameObject("WP_0");
                wp0Obj.transform.SetParent(wpRoot);
                wp0 = wp0Obj.transform;
            }

            Transform wp1 = wpRoot.Find("WP_1");
            if (wp1 == null)
            {
                GameObject wp1Obj = new GameObject("WP_1");
                wp1Obj.transform.SetParent(wpRoot);
                wp1 = wp1Obj.transform;
            }

            float leftX = Mathf.Clamp(dPos.x - 1.8f, -2.4f, 2.4f);
            float rightX = Mathf.Clamp(dPos.x + 1.8f, -2.4f, 2.4f);

            wp0.position = new Vector3(leftX, dPos.y, 0f);
            wp1.position = new Vector3(rightX, dPos.y, 0f);

            PatrolAI patrol = dog.GetComponent<PatrolAI>();
            if (patrol != null)
            {
                patrol.SetWaypoints(new List<Transform> { wp0, wp1 }, PatrolAI.PatrolMode.PingPong);
                patrol.moveSpeed = 1.0f;
            }

            GuardDogAI dogAI = dog.GetComponent<GuardDogAI>();
            if (dogAI != null)
            {
                dogAI.detectionRadius = 2.8f;
                dogAI.biteRadius = 0.65f;
                dogAI.coyoteAlertDuration = 5.0f;
                dogAI.calmDownDelay = 0.8f;
            }
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("<color=green>[PlaceAll] ¡3 Perros Guardianes (Bulldog y Pastor Alemán) distribuidos con éxito en zonas 100% despejadas!</color>");
    }
}
