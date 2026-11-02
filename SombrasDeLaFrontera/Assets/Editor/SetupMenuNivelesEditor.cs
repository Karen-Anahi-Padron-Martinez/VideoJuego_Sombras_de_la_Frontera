using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Tarjetas de la pantalla de niveles del Menú Principal:
/// - Pone las miniaturas de los niveles 1, 2 y 3 (Assets/Sprites/UI/Thumbnail_LevelN.jpg; las de 1 y 2 salen de
///   otros/menu/miniaturas_niveles.py).
/// - Asegura el candado en las tarjetas 2, 3 y 4 (se muestra solo mientras el nivel está bloqueado).
/// - Bloquea de nuevo todos los niveles (solo queda el Nivel 1).
/// El aspecto bloqueado/desbloqueado lo aplica MainMenuController al abrir la pantalla de niveles.
/// </summary>
public static class SetupMenuNivelesEditor
{
    private const string MENU_SCENE = "Assets/Scenes/01_MainMenu.unity";
    private const string THUMB = "Assets/Sprites/UI/Thumbnail_Level{0}.jpg";

    [MenuItem("Sombras de la Frontera/Menú/Actualizar Tarjetas de Niveles")]
    public static void UpdateCardsMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        UpdateCards();
        EditorUtility.DisplayDialog("Menú", "Miniaturas y candados de las tarjetas de nivel listos.\nTodos los niveles vuelven a estar bloqueados (salvo el Nivel 1).", "Aceptar");
    }

    /// <summary>Uso por línea de comandos (Unity -batchmode -executeMethod SetupMenuNivelesEditor.UpdateCards).</summary>
    public static void UpdateCards()
    {
        var scene = EditorSceneManager.OpenScene(MENU_SCENE, OpenSceneMode.Single);
        Transform[] all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
        Transform Card(int n) => all.FirstOrDefault(t => t.name == $"Card_Level{n}");

        // Candado de referencia (el de la tarjeta 2) para las tarjetas que no lo tengan
        Transform lockTemplate = Card(2)?.Find("ThumbFrame/LockIcon");

        for (int n = 1; n <= 4; n++)
        {
            Transform card = Card(n);
            if (card == null) { Debug.LogWarning($"[Menú] No se encontró Card_Level{n}"); continue; }
            Transform frame = card.Find("ThumbFrame");
            Image img = frame.GetComponent<Image>();

            string path = string.Format(THUMB, n);
            if (System.IO.File.Exists(path))
            {
                ConfigureSprite(path);
                img.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                img.preserveAspect = false;
                img.color = Color.white;
                EditorUtility.SetDirty(img);
            }

            if (n >= 2 && frame.Find("LockIcon") == null && lockTemplate != null)
            {
                GameObject lockIcon = Object.Instantiate(lockTemplate.gameObject, frame);
                lockIcon.name = "LockIcon";
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        PlayerPrefs.SetInt("NivelMaximo", 1);
        PlayerPrefs.Save();
        Debug.Log("<color=green><b>[Menú]</b> Tarjetas de nivel actualizadas y progreso reiniciado (solo Nivel 1).</color>");
    }

    private static void ConfigureSprite(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null || (importer.textureType == TextureImporterType.Sprite && importer.spriteImportMode == SpriteImportMode.Single)) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }
}
