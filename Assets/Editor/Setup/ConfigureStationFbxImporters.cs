using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    public static class ConfigureStationFbxImporters
    {
        private static readonly string[] FbxPaths = new[] {
            "Assets/Models/MeshyStations/EngineConsole/EngineConsole.fbx",
            "Assets/Models/MeshyStations/NavConsole/NavConsole.fbx",
            "Assets/Models/MeshyStations/CommsConsole/CommsConsole.fbx",
            "Assets/Models/MeshyStations/LifeConsole/LifeConsole.fbx",
        };

        [MenuItem("Setup/17a - Configure Cognitive Station FBX Importers")]
        public static void Configure()
        {
            int ok = 0;
            foreach (var p in FbxPaths)
            {
                var imp = AssetImporter.GetAtPath(p) as ModelImporter;
                if (imp == null) { Debug.LogWarning($"[ConfigStations] No ModelImporter at {p}"); continue; }
                imp.animationType = ModelImporterAnimationType.None;
                imp.importAnimation = false;
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                imp.SaveAndReimport();
                ok++;
            }
            Debug.Log($"[ConfigStations] Configured {ok}/{FbxPaths.Length} station FBX importers.");
        }
    }
}
