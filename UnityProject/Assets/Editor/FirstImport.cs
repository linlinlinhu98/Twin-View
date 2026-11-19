using System.IO;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FirstImport {
    static FirstImport() {
        if(Application.isBatchMode) return;
        EditorApplication.update += BuildWhenReady;
    }
    static void BuildWhenReady() {
        if(EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        EditorApplication.update -= BuildWhenReady;
        if(!File.Exists("Assets/Scenes/FirstChamber.unity")) ChamberBuilder.BuildScene();
    }
}
