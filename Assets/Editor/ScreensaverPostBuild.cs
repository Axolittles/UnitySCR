#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public sealed class ScreensaverPostBuild :
    IPostprocessBuildWithReport
{
    // Run fairly early
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        var target = report.summary.platform;

        // Only handle Windows Standalone builds
        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64)
            return;

        // Unity’s outputPath points to the built .exe
        var exePath = report.summary.outputPath;
        if (!exePath.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
            return;

        var scrPath = Path.ChangeExtension(exePath, ".scr");

        // If a .scr already exists from a previous build, remove it
        TryDeleteIfExists(scrPath);

        // Rename .exe -> .scr
        File.Move(exePath, scrPath);

        // Log a friendly note in the Console
        UnityEngine.Debug.Log($"[Screensaver] Renamed to: {scrPath}");
    }

    private static void TryDeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[Screensaver] Could not delete existing {path}: {e.Message}"); }
    }

}
#endif
