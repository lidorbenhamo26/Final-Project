using UnityEditor;
using UnityEngine;
using System.IO;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Creates and assigns proper materials to Earth/Sun/Moon — they import from Meshy
    /// with default URP/Lit and look featureless in space.
    /// </summary>
    public static class CelestialMaterials
    {
        [MenuItem("Setup/5 - Apply Celestial Materials")]
        public static void Apply()
        {
            const string matFolder = "Assets/Materials";
            if (!Directory.Exists(matFolder))
                Directory.CreateDirectory(matFolder);

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                Debug.LogError("[CelestialMaterials] URP/Lit shader not found.");
                return;
            }

            // Earth — vivid blue, gentle emission so it reads in dark space
            var earthMat = new Material(litShader) { name = "Earth_Mat" };
            earthMat.SetColor("_BaseColor", new Color(0.20f, 0.55f, 0.95f));
            earthMat.SetFloat("_Smoothness", 0.4f);
            earthMat.EnableKeyword("_EMISSION");
            earthMat.SetColor("_EmissionColor", new Color(0.15f, 0.40f, 0.80f) * 0.6f);
            earthMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveOrReplace(earthMat, $"{matFolder}/Earth_Mat.mat");

            // Sun — bright yellow-orange, strong emission so it self-illuminates
            var sunMat = new Material(litShader) { name = "Sun_Mat" };
            sunMat.SetColor("_BaseColor", new Color(1.0f, 0.95f, 0.5f));
            sunMat.EnableKeyword("_EMISSION");
            sunMat.SetColor("_EmissionColor", new Color(2.5f, 2.1f, 1.2f) * 4.0f);
            sunMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            SaveOrReplace(sunMat, $"{matFolder}/Sun_Mat.mat");

            // Moon — gray with slight cool tint
            var moonMat = new Material(litShader) { name = "Moon_Mat" };
            moonMat.SetColor("_BaseColor", new Color(0.78f, 0.80f, 0.85f));
            moonMat.SetFloat("_Smoothness", 0.15f);
            SaveOrReplace(moonMat, $"{matFolder}/Moon_Mat.mat");

            AssetDatabase.SaveAssets();

            ApplyToTarget("Earth", earthMat);
            ApplyToTarget("Sun", sunMat);
            ApplyToTarget("Moon", moonMat);

            Debug.Log("[CelestialMaterials] Applied Earth/Sun/Moon materials.");
        }

        private static void SaveOrReplace(Material mat, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = mat.shader;
                existing.CopyPropertiesFromMaterial(mat);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                AssetDatabase.CreateAsset(mat, path);
            }
        }

        private static void ApplyToTarget(string goName, Material mat)
        {
            var go = GameObject.Find(goName);
            if (go == null)
            {
                Debug.LogWarning($"[CelestialMaterials] '{goName}' not found in scene.");
                return;
            }
            var rends = go.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var r in rends)
            {
                var loaded = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GetAssetPath(mat));
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = loaded;
                r.sharedMaterials = arr;
            }
        }
    }
}
