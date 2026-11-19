using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds the WebGL version used for the hosted demo link.
public static class WebGLBuild {
    [MenuItem("Twin View/Build WebGL demo")]
    public static void Build() {
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
        PlayerSettings.defaultWebScreenWidth = 1280;
        PlayerSettings.defaultWebScreenHeight = 720;
        var report = BuildPipeline.BuildPlayer(
            new[] { "Assets/Scenes/FirstChamber.unity" },
            "Builds/WebGL",
            BuildTarget.WebGL,
            BuildOptions.None);
        if (report.summary.result != BuildResult.Succeeded) throw new Exception("WebGL build failed: " + report.summary.result);
        Debug.Log("TWINVIEW_WEBGL_BUILD_PASSED");
    }
}
