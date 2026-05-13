using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Fixes 2 problems with Meshy-generated interior props:
    ///  1. Scale — Meshy FBX exports come at ~2cm; scale them up to real-world sizes
    ///  2. Materials — preview-only generations have no textures, props look featureless
    ///     Apply cartoon/Pixar-style flat materials with subtle emission to match
    ///     the astronaut character's visual language.
    /// Also brightens the scene by adding fill lighting + ambient skybox + boosting
    /// the existing per-room neon lights.
    /// </summary>
    public static class PropsFixup
    {
        private const string MatFolder = "Assets/Materials/Props";

        private struct PropFix
        {
            public string Name;
            public float Scale;          // multiplier for the GameObject's localScale
            public Color BaseColor;
            public Color EmissionColor;  // Color.black = no emission
            public float Smoothness;
        }

        private static readonly PropFix[] Fixes = new[]
        {
            // Interior props — Meshy FBX is ~2cm so scale 60 -> ~1.2m, scale 100 -> ~2m, etc.
            new PropFix { Name = "OxygenTank",     Scale = 80f,  BaseColor = new Color(0.95f,0.95f,0.95f), EmissionColor = Color.black,                  Smoothness = 0.45f },
            new PropFix { Name = "ToolsCaddy",     Scale = 60f,  BaseColor = new Color(0.55f,0.62f,0.70f), EmissionColor = Color.black,                  Smoothness = 0.55f },
            new PropFix { Name = "RobotHelper",    Scale = 80f,  BaseColor = new Color(0.85f,0.85f,0.90f), EmissionColor = new Color(0.0f,0.5f,1.0f) * 0.8f, Smoothness = 0.55f },
            new PropFix { Name = "StarMapStand",   Scale = 70f,  BaseColor = new Color(0.30f,0.45f,0.65f), EmissionColor = new Color(0.2f,0.5f,1.0f) * 1.5f, Smoothness = 0.40f },
            new PropFix { Name = "CoffeeMug",      Scale = 30f,  BaseColor = new Color(0.95f,0.95f,0.95f), EmissionColor = Color.black,                  Smoothness = 0.40f },
            new PropFix { Name = "AlienBuddy",     Scale = 90f,  BaseColor = new Color(0.45f,0.85f,0.45f), EmissionColor = new Color(0.1f,0.4f,0.1f) * 0.4f, Smoothness = 0.35f },
            new PropFix { Name = "RobotPet",       Scale = 60f,  BaseColor = new Color(0.95f,0.95f,1.0f),  EmissionColor = new Color(0.0f,0.6f,1.0f) * 0.6f, Smoothness = 0.55f },
            new PropFix { Name = "StarCrystal",    Scale = 40f,  BaseColor = new Color(1.0f,0.95f,0.4f),   EmissionColor = new Color(1.0f,0.85f,0.3f) * 3.5f, Smoothness = 0.20f },
            new PropFix { Name = "PlanetGlobe",    Scale = 50f,  BaseColor = new Color(0.30f,0.55f,0.95f), EmissionColor = new Color(0.1f,0.3f,0.6f) * 0.5f, Smoothness = 0.50f },
            new PropFix { Name = "HelmetStand",    Scale = 70f,  BaseColor = new Color(0.95f,0.95f,0.95f), EmissionColor = Color.black,                  Smoothness = 0.55f },
        };

        [MenuItem("Setup/6 - Fix Props (scale + cartoon materials)")]
        public static void Fix()
        {
            EnsureFolder(MatFolder);
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) { Debug.LogError("[PropsFixup] URP/Lit shader not found."); return; }

            int fixedCount = 0, missing = 0;
            foreach (var def in Fixes)
            {
                var go = GameObject.Find(def.Name);
                if (go == null) { Debug.LogWarning($"[PropsFixup] '{def.Name}' not found."); missing++; continue; }

                // Multiply existing localScale (preserves any per-prop relative scaling)
                go.transform.localScale = go.transform.localScale * def.Scale;

                // Build/update material
                var matPath = $"{MatFolder}/{def.Name}_Mat.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                {
                    mat = new Material(litShader);
                    AssetDatabase.CreateAsset(mat, matPath);
                }
                mat.shader = litShader;
                mat.SetColor("_BaseColor", def.BaseColor);
                mat.SetFloat("_Smoothness", def.Smoothness);
                if (def.EmissionColor.maxColorComponent > 0.001f)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", def.EmissionColor);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    mat.DisableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", Color.black);
                }
                EditorUtility.SetDirty(mat);

                // Apply to all renderers in the prop hierarchy
                var rends = go.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    var arr = new Material[r.sharedMaterials.Length == 0 ? 1 : r.sharedMaterials.Length];
                    for (int i = 0; i < arr.Length; i++) arr[i] = mat;
                    r.sharedMaterials = arr;
                }

                fixedCount++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[PropsFixup] Fixed {fixedCount} props (missing {missing}).");
        }

        [MenuItem("Setup/7 - Brighten Lighting")]
        public static void Brighten()
        {
            // 1. Flat ambient at high intensity. Trilight made the floor much
            //    darker than the walls; Flat keeps everything evenly lit even
            //    when the closed ceiling blocks the directional light.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.85f, 0.88f, 0.92f);
            RenderSettings.ambientIntensity = 3.0f;
            RenderSettings.reflectionIntensity = 1.0f;

            // 2. Very light fog
            RenderSettings.fog = true;
            RenderSettings.fogDensity = 0.0008f;
            RenderSettings.fogColor = new Color(0.55f, 0.58f, 0.65f);

            // 3. Stronger directionals, NO shadows (the ceiling blocks the
            //    directional in Play Mode otherwise). Boost point/spot lights.
            int boosted = 0;
            bool firstDir = true;
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (light.type == LightType.Directional)
                {
                    if (firstDir)
                    {
                        light.intensity = 1.2f;
                        light.color = new Color(0.92f, 0.95f, 1.0f);
                        firstDir = false;
                    }
                    else
                    {
                        light.intensity = Mathf.Min(light.intensity, 0.5f);
                    }
                    light.shadows = LightShadows.None;
                    boosted++;
                }
                else if (light.type == LightType.Point || light.type == LightType.Spot)
                {
                    light.intensity = Mathf.Clamp(light.intensity * 1.4f, 5.0f, 12f);
                    light.range = Mathf.Max(light.range, 12f);
                    boosted++;
                }
            }

            // 4. Soft fill directional from above-front (rim/back light)
            var fillName = "FillLight_TopFront";
            var existing = GameObject.Find(fillName);
            if (existing != null) Object.DestroyImmediate(existing);
            var fillGo = new GameObject(fillName);
            fillGo.transform.position = new Vector3(0, 20, -10);
            fillGo.transform.rotation = Quaternion.Euler(50, 30, 0);
            var fl = fillGo.AddComponent<Light>();
            fl.type = LightType.Directional;
            fl.color = new Color(0.95f, 0.97f, 1.0f);
            fl.intensity = 0.5f;
            fl.shadows = LightShadows.None;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[PropsFixup] Brightened lighting. Adjusted {boosted} lights, ambient now bright.");
        }

        private static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
