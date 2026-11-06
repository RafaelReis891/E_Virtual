using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.WebGL;
using UnityEngine;

public static class CiBuild
{
    public static void BuildWebGL()
    {
        var scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("No enabled scenes in EditorBuildSettings.");
            EditorApplication.Exit(1);
            return;
        }

        UserBuildSettings.codeOptimization = WasmCodeOptimization.BuildTimes;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "build/WebGL",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError($"WebGL build failed: {report.summary.result} ({report.summary.totalErrors} errors).");
            EditorApplication.Exit(1);
            return;
        }

        Debug.Log($"WebGL build succeeded: {report.summary.outputPath}");
        EditorApplication.Exit(0);
    }
}
