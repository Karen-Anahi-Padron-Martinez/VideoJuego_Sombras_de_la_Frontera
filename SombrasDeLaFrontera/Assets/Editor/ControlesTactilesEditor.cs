using UnityEditor;

/// <summary>
/// Activa o desactiva la simulación de los controles táctiles del APK en el editor:
/// con la opción marcada, al dar Play aparecen el joystick y el botón Bucear, y el ratón hace de dedo
/// (clic y arrastrar en la mitad izquierda de la ventana Game para mover a Mateo).
/// No afecta a las compilaciones: en el APK los controles siempre están y en PC/Web nunca.
/// </summary>
public static class ControlesTactilesEditor
{
    private const string MENU = "Sombras de la Frontera/Controles táctiles/Simular en el editor";

    [MenuItem(MENU)]
    private static void Alternar()
    {
        bool activo = !EditorPrefs.GetBool(MobileTouchControls.PREF_SIMULAR, false);
        EditorPrefs.SetBool(MobileTouchControls.PREF_SIMULAR, activo);
        EditorUtility.DisplayDialog("Controles táctiles",
            activo ? "Simulación activada.\nDa Play: el ratón hace de dedo (arrastra en la mitad izquierda para mover a Mateo)."
                   : "Simulación desactivada.", "Aceptar");
    }

    [MenuItem(MENU, true)]
    private static bool Validar()
    {
        Menu.SetChecked(MENU, EditorPrefs.GetBool(MobileTouchControls.PREF_SIMULAR, false));
        return !EditorApplication.isPlaying;
    }
}
