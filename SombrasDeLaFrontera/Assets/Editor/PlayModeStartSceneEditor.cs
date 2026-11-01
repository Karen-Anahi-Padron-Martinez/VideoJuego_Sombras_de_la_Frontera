using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Hace que al presionar Play en el Editor el juego arranque SIEMPRE en el Menú Principal,
/// aunque tengas abierta la escena del nivel (también se puede elegir iniciar desde el Splash).
/// Al terminar la prueba, el Editor regresa a la escena en la que estabas trabajando.
/// Se puede cambiar desde el menú "Sombras de la Frontera/Inicio del Play/...".
/// </summary>
[InitializeOnLoad]
public static class PlayModeStartSceneEditor
{
    private const string PREF_KEY = "SombrasDeLaFrontera.PlayStartMode";
    private const string SPLASH_SCENE = "Assets/Scenes/00_Splash.unity";
    private const string MENU_SCENE = "Assets/Scenes/01_MainMenu.unity";

    private const string MENU_SPLASH = "Sombras de la Frontera/Inicio del Play/Desde el Splash (inicio real del juego)";
    private const string MENU_MAIN = "Sombras de la Frontera/Inicio del Play/Directo al Menú Principal";
    private const string MENU_CURRENT = "Sombras de la Frontera/Inicio del Play/Escena abierta (para pruebas rápidas)";

    // 0 = Splash, 1 = Menú Principal (predeterminado), 2 = escena actual
    private static int Mode
    {
        get => EditorPrefs.GetInt(PREF_KEY, 1);
        set { EditorPrefs.SetInt(PREF_KEY, value); Apply(); }
    }

    static PlayModeStartSceneEditor()
    {
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        string path = Mode == 0 ? SPLASH_SCENE : Mode == 1 ? MENU_SCENE : null;
        EditorSceneManager.playModeStartScene = path != null ? AssetDatabase.LoadAssetAtPath<SceneAsset>(path) : null;
    }

    [MenuItem(MENU_SPLASH, false, 100)] private static void UseSplash() => Mode = 0;
    [MenuItem(MENU_MAIN, false, 101)] private static void UseMainMenu() => Mode = 1;
    [MenuItem(MENU_CURRENT, false, 102)] private static void UseCurrentScene() => Mode = 2;

    [MenuItem(MENU_SPLASH, true)] private static bool ValidateSplash() { Menu.SetChecked(MENU_SPLASH, Mode == 0); return true; }
    [MenuItem(MENU_MAIN, true)] private static bool ValidateMain() { Menu.SetChecked(MENU_MAIN, Mode == 1); return true; }
    [MenuItem(MENU_CURRENT, true)] private static bool ValidateCurrent() { Menu.SetChecked(MENU_CURRENT, Mode == 2); return true; }
}
