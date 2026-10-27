using UnityEngine;
using UnityEditor;
using UnityEngine.Audio;
using UnityEditor.SceneManagement;

public static class SetupAudioSystemEditor
{
    private const string AUDIO_DIR = "Assets/Audio";
    private const string PREFAB_DIR = "Assets/Prefabs/Audio";
    private const string PREFAB_PATH = "Assets/Prefabs/Audio/AudioManager.prefab";
    private const string LEVEL1_SCENE_PATH = "Assets/Scenes/02_Level1_Desert.unity";
    private const string MAIN_MENU_SCENE_PATH = "Assets/Scenes/01_MainMenu.unity";

    [MenuItem("Sombras de la Frontera/Configurar AudioManager, AudioMixer y Partículas de Polvo")]
    public static void SetupCompleteAudioSystem()
    {
        EnsureDirectories();

        // 1. Crear el Prefab del AudioManager
        GameObject audioMgrObj = CreateAudioManagerPrefab();

        // 1b. Asignar los sonidos generados (Assets/Audio) al prefab recién creado
        SetupSonidosEditor.SetupSounds();

        // 2. Configurar Partículas de Polvo en el Player de Level 1
        ConfigureDustParticlesInLevel1();

        // 3. Vincular AudioMixer al MainMenuController si existe
        LinkMixerToMainMenu();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog("Sistema de Audio y Partículas",
            "¡El sistema de Audio y Partículas de Polvo se ha configurado con éxito!\n\n" +
            "• AudioManager Singleton persistente creado en: Assets/Prefabs/Audio/AudioManager.prefab\n" +
            "• Disparadores de audio conectados (Pasos, Arena movediza, Cantimplora, Alerta, Perro, Daño, Victoria)\n" +
            "• Sistema de partículas DustParticles vinculado a Mateo en 02_Level1_Desert\n\n" +
            "Solo coloca tus archivos de sonido (.mp3/.wav/.ogg) en Assets/Audio/BGM y Assets/Audio/SFX, y asígnalos al AudioManager.",
            "Aceptar");
    }

    private static void EnsureDirectories()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefabs/Audio"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Prefabs"))
            {
                AssetDatabase.CreateFolder("Assets", "Prefabs");
            }
            AssetDatabase.CreateFolder("Assets/Prefabs", "Audio");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Audio"))
        {
            AssetDatabase.CreateFolder("Assets", "Audio");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Audio/BGM"))
        {
            AssetDatabase.CreateFolder("Assets/Audio", "BGM");
        }

        if (!AssetDatabase.IsValidFolder("Assets/Audio/SFX"))
        {
            AssetDatabase.CreateFolder("Assets/Audio", "SFX");
        }
    }

    private static GameObject CreateAudioManagerPrefab()
    {
        GameObject go = new GameObject("AudioManager");
        AudioManager am = go.AddComponent<AudioManager>();

        // AudioSource para BGM
        GameObject bgmObj = new GameObject("MusicSource");
        bgmObj.transform.SetParent(go.transform);
        AudioSource bgmSource = bgmObj.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;

        // AudioSource para SFX
        GameObject sfxObj = new GameObject("SFXSource");
        sfxObj.transform.SetParent(go.transform);
        AudioSource sfxSource = sfxObj.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;

        // AudioSource para Pasos
        GameObject stepObj = new GameObject("FootstepSource");
        stepObj.transform.SetParent(go.transform);
        AudioSource stepSource = stepObj.AddComponent<AudioSource>();
        stepSource.loop = false;
        stepSource.playOnAwake = false;

        // Asignar referencias privadas mediante SerializedObject
        SerializedObject so = new SerializedObject(am);
        so.FindProperty("musicSource").objectReferenceValue = bgmSource;
        so.FindProperty("sfxSource").objectReferenceValue = sfxSource;
        so.FindProperty("footstepSource").objectReferenceValue = stepSource;

        // Si existe un AudioMixer en el proyecto, enlazarlo
        string[] mixerGuids = AssetDatabase.FindAssets("t:AudioMixer");
        if (mixerGuids.Length > 0)
        {
            string mixerPath = AssetDatabase.GUIDToAssetPath(mixerGuids[0]);
            AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
            if (mixer != null)
            {
                so.FindProperty("audioMixer").objectReferenceValue = mixer;
                AudioMixerGroup[] groups = mixer.FindMatchingGroups("");
                foreach (var g in groups)
                {
                    if (g.name.ToLower().Contains("music") || g.name.ToLower().Contains("bgm"))
                    {
                        so.FindProperty("musicGroup").objectReferenceValue = g;
                        bgmSource.outputAudioMixerGroup = g;
                    }
                    else if (g.name.ToLower().Contains("sfx") || g.name.ToLower().Contains("efecto"))
                    {
                        so.FindProperty("sfxGroup").objectReferenceValue = g;
                        sfxSource.outputAudioMixerGroup = g;
                        stepSource.outputAudioMixerGroup = g;
                    }
                }
            }
        }

        so.ApplyModifiedProperties();

        // Guardar como Prefab
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PREFAB_PATH);
        Object.DestroyImmediate(go);

        Debug.Log($"[SetupAudio] Prefab del AudioManager generado en: {PREFAB_PATH}");
        return prefab;
    }

    private static void ConfigureDustParticlesInLevel1()
    {
        var currentScene = EditorSceneManager.GetActiveScene();
        bool wasDifferentScene = currentScene.path != LEVEL1_SCENE_PATH;

        var scene = EditorSceneManager.OpenScene(LEVEL1_SCENE_PATH, OpenSceneMode.Single);
        PlayerController2D player = Object.FindAnyObjectByType<PlayerController2D>();

        if (player != null)
        {
            Transform existingDust = player.transform.Find("DustParticles");
            GameObject dustObj;

            if (existingDust != null)
            {
                dustObj = existingDust.gameObject;
            }
            else
            {
                dustObj = new GameObject("DustParticles");
                dustObj.transform.SetParent(player.transform, false);
                dustObj.transform.localPosition = new Vector3(0f, -0.4f, 0f); // A la altura de los pies
            }

            ParticleSystem ps = dustObj.GetComponent<ParticleSystem>();
            if (ps == null)
            {
                ps = dustObj.AddComponent<ParticleSystem>();
            }

            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.28f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.72f, 0.48f, 0.65f); // Arena desértica translúcida

            var emission = ps.emission;
            emission.rateOverTime = 16f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            var colOverLifetime = ps.colorOverLifetime;
            colOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.88f, 0.75f, 0.52f), 0.0f), new GradientColorKey(new Color(0.75f, 0.65f, 0.45f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.7f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colOverLifetime.color = grad;

            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.4f);
            curve.AddKey(1f, 1.0f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, curve);

            // Asignar en PlayerController2D
            SerializedObject pSo = new SerializedObject(player);
            pSo.FindProperty("dustParticles").objectReferenceValue = ps;
            pSo.ApplyModifiedProperties();

            // Agregar instancia de AudioManager a la escena si no existe
            if (Object.FindAnyObjectByType<AudioManager>() == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB_PATH);
                if (prefab != null)
                {
                    PrefabUtility.InstantiatePrefab(prefab);
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupAudio] Partículas de polvo y AudioManager configurados en 02_Level1_Desert.");
        }

        if (wasDifferentScene && !string.IsNullOrEmpty(currentScene.path))
        {
            EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
        }
    }

    private static void LinkMixerToMainMenu()
    {
        string[] mixerGuids = AssetDatabase.FindAssets("t:AudioMixer");
        if (mixerGuids.Length == 0) return;

        string mixerPath = AssetDatabase.GUIDToAssetPath(mixerGuids[0]);
        AudioMixer mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(mixerPath);
        if (mixer == null) return;

        var currentScene = EditorSceneManager.GetActiveScene();
        bool wasDifferentScene = currentScene.path != MAIN_MENU_SCENE_PATH;

        var scene = EditorSceneManager.OpenScene(MAIN_MENU_SCENE_PATH, OpenSceneMode.Single);
        MainMenuController menuCtrl = Object.FindAnyObjectByType<MainMenuController>();
        if (menuCtrl != null)
        {
            SerializedObject mSo = new SerializedObject(menuCtrl);
            mSo.FindProperty("audioMixer").objectReferenceValue = mixer;
            mSo.ApplyModifiedProperties();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SetupAudio] AudioMixer enlazado exitosamente al MainMenuController.");
        }

        if (wasDifferentScene && !string.IsNullOrEmpty(currentScene.path))
        {
            EditorSceneManager.OpenScene(currentScene.path, OpenSceneMode.Single);
        }
    }
}
