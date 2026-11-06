using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// One-click HTML build of the prototype for presenting it in a browser (FishingJam menu).
// Output: D:/luyentayunityxgame/builds/FishingJam_WebGL (open index.html through a web server, not by double-click).
public static class WebGLBuild
{
    static string OutDir => Path.GetFullPath(Path.Combine(Application.dataPath, "../../../builds/FishingJam_WebGL"));

    [MenuItem("FishingJam/Build bản HTML (WebGL)")]
    static void Build() => Run(false);

    [MenuItem("FishingJam/Build bản HTML và chạy thử")]
    static void BuildAndRun() => Run(true);

    static void Run(bool run)
    {
        if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
        {
            EditorUtility.DisplayDialog("Thiếu module WebGL",
                "Unity chưa cài WebGL Build Support.\nMở Unity Hub → Installs → 2022.3.62f3 → Add modules → WebGL Build Support.", "OK");
            return;
        }

        // a phone-shaped page; gzip with a fallback so it works on any static host (itch.io, GitHub Pages, a shared folder server)
        PlayerSettings.defaultWebScreenWidth = 540;
        PlayerSettings.defaultWebScreenHeight = 960;
        PlayerSettings.runInBackground = true;
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
        PlayerSettings.WebGL.template = "PROJECT:FishingJam"; // Assets/WebGLTemplates/FishingJam: fits the 9:16 game to any window

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/SampleScene.unity" },
            locationPathName = OutDir,
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = run ? BuildOptions.AutoRunPlayer : BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var summary = report.summary;
        Debug.Log($"WebGL build: {summary.result}, {summary.totalSize / (1024f * 1024f):F1} MB, {summary.totalTime.TotalSeconds:F0} s → {OutDir}");
        if (summary.result == BuildResult.Succeeded && !run) EditorUtility.RevealInFinder(OutDir);
    }
}
