#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class CodexWindowsBuild
{
    private const string RequestPath = "Temp/CodexWindowsBuild.request";
    private const string OutputPath = "Builds/Windows/Meadom.exe";
    private static readonly string[] GameScenes =
    {
        "Assets/MainScenes/StartMenu 1.unity",
        "Assets/Scenes/Levels/OutDoors/Level_Farm.unity",
        "Assets/MainScenes/Core 1.unity"
    };

    [InitializeOnLoadMethod]
    private static void QueueRequestedBuild()
    {
        if (!File.Exists(RequestPath))
            return;

        EditorApplication.delayCall += BuildRequestedPlayer;
    }

    [MenuItem("Tools/Build/Build Windows Test EXE")]
    private static void RequestBuildFromMenu()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(RequestPath) ?? "Temp");
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
        BuildRequestedPlayer();
    }

    public static void BuildFromCommandLine()
    {
        BuildPlayerNow();
    }

    private static void BuildRequestedPlayer()
    {
        if (!File.Exists(RequestPath) || BuildPipeline.isBuildingPlayer)
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= BuildAfterPlayMode;
            EditorApplication.playModeStateChanged += BuildAfterPlayMode;
            EditorApplication.isPlaying = false;
            return;
        }

        File.Delete(RequestPath);
        BuildPlayerNow();
    }

    private static void BuildPlayerNow()
    {
        string outputDirectory = Path.GetDirectoryName(OutputPath) ?? "Builds/Windows";
        Directory.CreateDirectory(outputDirectory);

        string[] scenes = GameScenes.Where(File.Exists).ToArray();
        if (scenes.Length == 0)
            throw new InvalidOperationException("No enabled scenes were found in Build Settings.");

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        };

        Debug.Log($"CODEX_BUILD_START: {OutputPath} ({scenes.Length} scenes)");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"CODEX_BUILD_RESULT: {summary.result}; errors={summary.totalErrors}; warnings={summary.totalWarnings}; size={summary.totalSize}; path={OutputPath}");

        if (summary.result != BuildResult.Succeeded)
            throw new InvalidOperationException($"Windows build failed: {summary.result} ({summary.totalErrors} errors).");
    }

    private static void BuildAfterPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= BuildAfterPlayMode;
        EditorApplication.delayCall += BuildRequestedPlayer;
    }
}
#endif
