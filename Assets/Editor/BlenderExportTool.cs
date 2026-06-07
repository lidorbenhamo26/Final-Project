using UnityEngine;
using UnityEditor;
using UnityEditor.Formats.Fbx.Exporter;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public static class BlenderExportTool
{
    private static readonly string[] RootNames = new string[]
    {
        "Environment_Root", "Stations_Root", "MeshyProps_Root", "SkyBodies_Root",
        "FX_VolumeRoot", "FX_Root", "Ambience_Root", "Astronaut",
        "FillLight_TopFront",
        "Lamp_Hub", "Lamp_Engine", "Lamp_Navigation", "Lamp_LifeSupport", "Lamp_Comms",
        "Spot_Engine", "Spot_Navigation", "Spot_Comms", "Spot_LifeSupport",
        "Spot_Crates_A", "Spot_Crates_B"
    };

    private const string ExportDir = @"C:\Users\Lidor\Desktop\Final-Project\BlenderExports";

    [MenuItem("Tools/Blender Export/Export MainScene FBX (Lean)")]
    public static void ExportMainSceneFbxLean()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool wasDirtyBefore = scene.isDirty;

        var rootMap = scene.GetRootGameObjects().ToDictionary(go => go.name, go => go);
        var toExport = new List<UnityEngine.Object>();
        var missing = new List<string>();
        var found = new List<string>();
        foreach (var name in RootNames)
        {
            if (rootMap.TryGetValue(name, out var go)) { toExport.Add(go); found.Add(name); }
            else missing.Add(name);
        }

        Directory.CreateDirectory(ExportDir);
        string outputPath = Path.Combine(ExportDir, "MainScene.fbx");

        var opts = new ExportModelOptions
        {
            ExportFormat = ExportFormat.Binary,
            ModelAnimIncludeOption = Include.Model,
            LODExportType = LODExportType.Highest,
            ObjectPosition = ObjectPosition.LocalCentered,
            AnimateSkinnedMesh = false,
            EmbedTextures = false,
            KeepInstances = true,
            UseMayaCompatibleNames = false,
            PreserveImportSettings = false,
            ExportUnrendered = false,
        };

        string resultPath = ModelExporter.ExportObjects(outputPath, toExport.ToArray(), opts);
        long size = File.Exists(outputPath) ? new FileInfo(outputPath).Length : 0;
        double sizeMB = System.Math.Round(size / 1024.0 / 1024.0, 2);

        Debug.Log($"[BlenderExport] Found={found.Count}, Missing=[{string.Join(",", missing)}], Path={resultPath}, Size={sizeMB}MB, DirtyBefore={wasDirtyBefore}, DirtyAfter={scene.isDirty}");
    }
}
