#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RiverMapSetup
{
    static RiverMapSetup()
    {
        EditorApplication.delayCall += FixRiverSprites;
    }

    [MenuItem("Sombras/Corregir Sprites del Rio (Nivel 2)")]
    public static void FixRiverSprites()
    {
        string surPath = "Assets/Sprites/Level2_River/orilla_inferior_dia.png";
        string aguaPath = "Assets/Sprites/Level2_River/agua_repetible_dia.png";
        string nortePath = "Assets/Sprites/Level2_River/orilla_superior_dia.png";

        EnsureSpriteImporter(surPath);
        EnsureSpriteImporter(aguaPath);
        EnsureSpriteImporter(nortePath);

        Sprite surSprite = LoadSprite(surPath);
        Sprite aguaSprite = LoadSprite(aguaPath);
        Sprite norteSprite = LoadSprite(nortePath);

        Debug.Log($"[RiverMapSetup] Sprites cargados: Sur={surSprite != null}, Agua={aguaSprite != null}, Norte={norteSprite != null}");

        // Actualizar en la escena activa
        Scene activeScene = SceneManager.GetActiveScene();
        if (activeScene.isLoaded)
        {
            GameObject riverMap = GameObject.Find("River_Map");
            if (riverMap != null)
            {
                AssignToObject(riverMap, surSprite, aguaSprite, norteSprite);
                EditorSceneManager.MarkSceneDirty(activeScene);
                EditorSceneManager.SaveScene(activeScene);
                Debug.Log($"[RiverMapSetup] Escena {activeScene.name} actualizada con exito.");
            }
        }

        // Actualizar en el Prefab
        string prefabPath = "Assets/Prefabs/Level2_River/River_Map.prefab";
        if (System.IO.File.Exists(prefabPath))
        {
            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(prefabPath);
            if (prefabRoot != null)
            {
                AssignToObject(prefabRoot, surSprite, aguaSprite, norteSprite);
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, prefabPath);
                PrefabUtility.UnloadPrefabContents(prefabRoot);
                Debug.Log("[RiverMapSetup] Prefab River_Map actualizado con exito.");
            }
        }
    }

    private static void EnsureSpriteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            bool modified = false;
            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                modified = true;
            }
            if (importer.spritePixelsPerUnit != 100)
            {
                importer.spritePixelsPerUnit = 100;
                modified = true;
            }
            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                modified = true;
            }
            if (importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.wrapMode = TextureWrapMode.Clamp;
                modified = true;
            }
            if (modified)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;

        Object[] all = AssetDatabase.LoadAllAssetsAtPath(path);
        foreach (var obj in all)
        {
            if (obj is Sprite sp) return sp;
        }
        return null;
    }

    private static void AssignToObject(GameObject root, Sprite sur, Sprite agua, Sprite norte)
    {
        Transform tSur = root.transform.Find("01_Orilla_Sur");
        if (tSur != null && sur != null)
        {
            SpriteRenderer sr = tSur.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = sur;
        }

        Transform tAgua1 = root.transform.Find("02_Agua_Tramo1");
        if (tAgua1 != null && agua != null)
        {
            SpriteRenderer sr = tAgua1.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = agua;
        }

        Transform tAgua2 = root.transform.Find("03_Agua_Tramo2");
        if (tAgua2 != null && agua != null)
        {
            SpriteRenderer sr = tAgua2.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = agua;
        }

        Transform tNorte = root.transform.Find("04_Orilla_Norte");
        if (tNorte != null && norte != null)
        {
            SpriteRenderer sr = tNorte.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sprite = norte;
        }
    }
}
#endif
