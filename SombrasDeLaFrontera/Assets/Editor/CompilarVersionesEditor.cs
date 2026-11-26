using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Compilaciones de entrega (Guía de despliegue, Actividad 1):
/// - APK de Android: horizontal, paquete com.utng.sombrasdelafrontera, Android 8.0+ (API 26), IL2CPP y ARM64.
///   Firma de depuración automática (suficiente para instalar en un celular o emulador).
/// - WebGL: lienzo 960 x 540, compresión Gzip con "Decompression Fallback" (abre en cualquier servidor).
/// Las compilaciones van a SombrasDeLaFrontera/Builds (ignorada por git). Cambiar de plataforma reimporta texturas: tarda.
/// También por línea de comandos: Unity -batchmode -buildTarget Android -executeMethod CompilarVersionesEditor.CompilarApkDesdeConsola -quit
/// </summary>
public static class CompilarVersionesEditor
{
    public const string PAQUETE = "com.utng.sombrasdelafrontera";
    private const string RUTA_APK = "Builds/Android/SombrasDeLaFrontera.apk";
    private const string RUTA_WEB = "Builds/WebGL";

    [MenuItem("Sombras de la Frontera/Compilar/APK de Android")]
    public static void CompilarApkMenu()
    {
        if (!EditorUtility.DisplayDialog("Compilar APK", "Se configurará Android y se generará " + RUTA_APK +
                ".\nSi la plataforma activa no es Android, Unity la cambiará primero (puede tardar varios minutos).", "Compilar", "Cancelar"))
            return;
        BuildReport reporte = CompilarApk();
        MostrarResultado(reporte, RUTA_APK);
    }

    [MenuItem("Sombras de la Frontera/Compilar/WebGL")]
    public static void CompilarWebMenu()
    {
        if (!EditorUtility.DisplayDialog("Compilar WebGL", "Se configurará Web y se generará la carpeta " + RUTA_WEB +
                ".\nSi la plataforma activa no es Web, Unity la cambiará primero.", "Compilar", "Cancelar"))
            return;
        BuildReport reporte = CompilarWeb();
        MostrarResultado(reporte, RUTA_WEB);
    }

    public static void CompilarApkDesdeConsola()
    {
        BuildReport reporte = CompilarApk();
        EditorApplication.Exit(reporte.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    public static void CompilarWebDesdeConsola()
    {
        BuildReport reporte = CompilarWeb();
        EditorApplication.Exit(reporte.summary.result == BuildResult.Succeeded ? 0 : 1);
    }

    // ------------------------------------------------------------------------------------------------ Configuración

    /// <summary>Datos del producto que comparten todas las plataformas.</summary>
    private static void ConfigurarProducto()
    {
        PlayerSettings.companyName = "UTNG";
        PlayerSettings.productName = "Sombras de la Frontera";
        PlayerSettings.bundleVersion = "1.0";
    }

    public static void ConfigurarAndroid()
    {
        ConfigurarProducto();
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetApplicationIdentifier(android, PAQUETE);
        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        // Solo horizontal (el juego y su interfaz están hechos para 16:9)
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        EditorUserBuildSettings.buildAppBundle = false;   // Se entrega un .apk, no un .aab
        EditorUserBuildSettings.development = false;
    }

    public static void ConfigurarWeb()
    {
        ConfigurarProducto();
        PlayerSettings.defaultWebScreenWidth = 960;
        PlayerSettings.defaultWebScreenHeight = 540;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        EditorUserBuildSettings.development = false;
    }

    // ------------------------------------------------------------------------------------------------ Compilación

    public static BuildReport CompilarApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        ConfigurarAndroid();
        return Compilar(RUTA_APK, BuildTarget.Android);
    }

    public static BuildReport CompilarWeb()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);
        ConfigurarWeb();
        return Compilar(RUTA_WEB, BuildTarget.WebGL);
    }

    private static BuildReport Compilar(string ruta, BuildTarget plataforma)
    {
        AssetDatabase.SaveAssets();
        string completa = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ruta));
        Directory.CreateDirectory(plataforma == BuildTarget.Android ? Path.GetDirectoryName(completa) : completa);

        string[] escenas = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Debug.Log($"[Compilar] {plataforma}: {escenas.Length} escenas → {completa}\n" + string.Join("\n", escenas));

        BuildReport reporte = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = escenas,
            locationPathName = completa,
            target = plataforma,
            options = BuildOptions.None,
        });

        BuildSummary r = reporte.summary;
        Debug.Log($"[Compilar] {plataforma}: {r.result} en {r.totalTime.TotalMinutes:F1} min, {r.totalSize / (1024f * 1024f):F1} MB, {r.totalErrors} errores, {r.totalWarnings} avisos.");
        return reporte;
    }

    private static void MostrarResultado(BuildReport reporte, string ruta)
    {
        BuildSummary r = reporte.summary;
        bool ok = r.result == BuildResult.Succeeded;
        EditorUtility.DisplayDialog(ok ? "Compilación terminada" : "La compilación falló",
            ok ? $"Listo: {ruta}\n{r.totalSize / (1024f * 1024f):F1} MB en {r.totalTime.TotalMinutes:F1} minutos."
               : $"Resultado: {r.result}. Revisa la consola ({r.totalErrors} errores).", "Aceptar");
        if (ok) EditorUtility.RevealInFinder(Path.GetFullPath(Path.Combine(Application.dataPath, "..", ruta)));
    }
}
