using UnityEngine;
using UnityEditor;

/// <summary>
/// Depuración: un solo botón que desbloquea los 4 niveles o, si ya están desbloqueados,
/// los vuelve a bloquear (solo Nivel 1). Útil para saltar al Nivel 4 y probar el Final.
/// </summary>
public static class DesbloquearNivelesEditor
{
    private const string MENU = "Tools/Story/Niveles desbloqueados (prueba)";
    private const string PREF_MAX_LEVEL = "NivelMaximo";

    private static bool Desbloqueados => PlayerPrefs.GetInt(PREF_MAX_LEVEL, 1) >= 4;

    [MenuItem(MENU)]
    public static void Alternar()
    {
        bool desbloquear = !Desbloqueados;
        PlayerPrefs.SetInt(PREF_MAX_LEVEL, desbloquear ? 4 : 1);
        if (!desbloquear) PlayerPrefs.DeleteKey(GoalTrigger.GAME_COMPLETED_KEY);
        PlayerPrefs.Save();

        Debug.Log(desbloquear
            ? "[Depuración] Niveles 1 a 4 desbloqueados en el menú."
            : "[Depuración] Niveles bloqueados: solo el Nivel 1 está disponible.");
    }

    // Muestra la palomita en el menú cuando los niveles están desbloqueados
    [MenuItem(MENU, true)]
    private static bool Validar()
    {
        Menu.SetChecked(MENU, Desbloqueados);
        return true;
    }
}
