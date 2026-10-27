using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Configura todo el sonido del juego:
/// 1. Ajustes de importación (efectos cargados en memoria, música en streaming).
/// 2. Prefab Assets/Prefabs/Audio/AudioManager.prefab con todos los clips asignados:
///    música (menú y desierto), pasos, arena movediza, agua, daño, Game Over, Victoria,
///    coyotes (pasos y gritos), perros (ladrido, mordida, ronquido), serpiente, alacrán y araña.
/// 3. Coloca el AudioManager en el Splash, el Menú Principal y el Nivel 1 (es persistente y no se duplica).
/// Los sonidos se generaron con Docs/generar_sonidos.py.
/// </summary>
public static class SetupSonidosEditor
{
    private const string SFX = "Assets/Audio/SFX/";
    private const string BGM = "Assets/Audio/BGM/";
    private const string PREFAB_DIR = "Assets/Prefabs/Audio";
    private const string PREFAB_PATH = PREFAB_DIR + "/AudioManager.prefab";
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/00_Splash.unity",
        "Assets/Scenes/01_MainMenu.unity",
        "Assets/Scenes/02_Level1_Desert.unity",
    };

    [MenuItem("Sombras de la Frontera/Sprint 2/4. Configurar Sonidos y Música")]
    public static void SetupSounds()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        ConfigureImportSettings();
        GameObject prefab = BuildAudioManagerPrefab();
        if (prefab == null) return;

        string previousScene = EditorSceneManager.GetActiveScene().path;
        foreach (string scenePath in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            if (Object.FindAnyObjectByType<AudioManager>(FindObjectsInactive.Include) == null)
            {
                PrefabUtility.InstantiatePrefab(prefab, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }
        if (!string.IsNullOrEmpty(previousScene)) EditorSceneManager.OpenScene(previousScene, OpenSceneMode.Single);

        Debug.Log("<color=green><b>[Sonidos]</b> AudioManager con música y efectos listo en Splash, Menú Principal y Nivel 1.</color>");
    }

    private static void ConfigureImportSettings()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { "Assets/Audio" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            AudioImporter importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            bool isMusic = path.StartsWith(BGM);
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = isMusic ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = isMusic ? 0.6f : 0.8f;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = !isMusic;
            importer.loadInBackground = isMusic;
            importer.SaveAndReimport();
        }
    }

    private static AudioClip Clip(string path)
    {
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path + ".wav");
        if (clip == null) Debug.LogWarning($"[Sonidos] No se encontró {path}.wav");
        return clip;
    }

    private static AudioClip[] Clips(string prefix, int count) =>
        Enumerable.Range(1, count).Select(i => Clip($"{prefix}_{i}")).Where(c => c != null).ToArray();

    private static void SetArray(SerializedObject so, string field, AudioClip[] clips)
    {
        SerializedProperty prop = so.FindProperty(field);
        prop.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) prop.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
    }

    private static GameObject BuildAudioManagerPrefab()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs")) AssetDatabase.CreateFolder("Assets", "Prefabs");
        if (!AssetDatabase.IsValidFolder(PREFAB_DIR)) AssetDatabase.CreateFolder("Assets/Prefabs", "Audio");

        bool fromPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH) != null;
        GameObject root = fromPrefab ? PrefabUtility.LoadPrefabContents(PREFAB_PATH) : new GameObject("AudioManager");

        try
        {
            AudioManager manager = root.GetComponent<AudioManager>();
            if (manager == null) manager = root.AddComponent<AudioManager>();

            AudioSource music = EnsureSource(root, "MusicSource", true);
            AudioSource sfx = EnsureSource(root, "SFXSource", false);
            AudioSource steps = EnsureSource(root, "FootstepSource", false);

            var so = new SerializedObject(manager);
            so.FindProperty("musicSource").objectReferenceValue = music;
            so.FindProperty("sfxSource").objectReferenceValue = sfx;
            so.FindProperty("footstepSource").objectReferenceValue = steps;

            so.FindProperty("desertNightBGM").objectReferenceValue = Clip(BGM + "Desert_Night_Loop");
            so.FindProperty("menuBGM").objectReferenceValue = Clip(BGM + "Menu_Theme_Loop");

            SetArray(so, "footstepSandClips", Clips(SFX + "Footstep_Sand", 4));
            so.FindProperty("quicksandSinkClip").objectReferenceValue = Clip(SFX + "Quicksand_Sink");
            so.FindProperty("drinkWaterClip").objectReferenceValue = Clip(SFX + "Drink_Water");
            so.FindProperty("alertStingClip").objectReferenceValue = Clip(SFX + "Alert_Sting");
            so.FindProperty("dogBarkClip").objectReferenceValue = Clip(SFX + "Dog_Bark_1");
            so.FindProperty("damageHurtClip").objectReferenceValue = Clip(SFX + "Mateo_Hurt_1");
            SetArray(so, "damageHurtClips", Clips(SFX + "Mateo_Hurt", 2));
            so.FindProperty("gameOverClip").objectReferenceValue = Clip(SFX + "Game_Over");
            so.FindProperty("victoryClip").objectReferenceValue = Clip(SFX + "Victory");

            SetArray(so, "coyoteStepClips", Clips(SFX + "Coyote_Step", 3));
            SetArray(so, "coyoteSuspectClips", Clips(SFX + "Coyote_Suspect", 2));
            SetArray(so, "coyoteSpotClips", Clips(SFX + "Coyote_Spot", 3));
            SetArray(so, "coyoteCatchClips", Clips(SFX + "Coyote_Catch", 2));

            SetArray(so, "dogBarkClips", Clips(SFX + "Dog_Bark", 3));
            so.FindProperty("dogBiteClip").objectReferenceValue = Clip(SFX + "Dog_Bite");
            so.FindProperty("dogSnoreClip").objectReferenceValue = Clip(SFX + "Dog_Snore");

            so.FindProperty("snakeRattleClip").objectReferenceValue = Clip(SFX + "Snake_Rattle");
            so.FindProperty("snakeHissClip").objectReferenceValue = Clip(SFX + "Snake_Hiss");
            so.FindProperty("scorpionClickClip").objectReferenceValue = Clip(SFX + "Scorpion_Click");
            so.FindProperty("scorpionStingClip").objectReferenceValue = Clip(SFX + "Scorpion_Sting");
            so.FindProperty("spiderSkitterClip").objectReferenceValue = Clip(SFX + "Spider_Skitter");
            so.FindProperty("spiderHissClip").objectReferenceValue = Clip(SFX + "Spider_Hiss");
            so.ApplyModifiedPropertiesWithoutUndo();

            return PrefabUtility.SaveAsPrefabAsset(root, PREFAB_PATH);
        }
        finally
        {
            if (fromPrefab) PrefabUtility.UnloadPrefabContents(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static AudioSource EnsureSource(GameObject root, string name, bool loop)
    {
        Transform child = root.transform.Find(name);
        if (child == null)
        {
            child = new GameObject(name).transform;
            child.SetParent(root.transform, false);
        }

        AudioSource src = child.GetComponent<AudioSource>();
        if (src == null) src = child.gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = 0f;
        return src;
    }
}
