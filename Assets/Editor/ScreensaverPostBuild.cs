#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public sealed class ScreensaverPostBuild : IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        var target = report.summary.platform;

        if (target != BuildTarget.StandaloneWindows && target != BuildTarget.StandaloneWindows64) return;
        var exePath = report.summary.outputPath;
        if (!exePath.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase)) return;

        var scrPath = Path.ChangeExtension(exePath, ".scr");

        // If a .scr already exists from a previous build, remove it
        TryDeleteIfExists(scrPath);
        // Copy exe to scr, we copy so we can maintain Build and Run behaviour
        // The exe is not actually needed for the Screensaver to run
        File.Copy(exePath, scrPath);

        Debug.Log($"[Screensaver] Renamed to: {scrPath}");
    }

    private static void TryDeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[Screensaver] Could not delete existing {path}: {e.Message}"); }
    }

}
#endif
