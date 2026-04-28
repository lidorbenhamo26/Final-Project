using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Setup/14 — Build proper URP/Lit materials from the Meshy-downloaded PBR
    /// texture sets and apply them to the V2 rigged characters in the scene.
    /// The rig FBX comes without textures because Meshy keeps them in the
    /// refine task; we already downloaded the refine PNGs separately.
    /// </summary>
    public static class ApplyCharacterTextures
    {
        private const string AlienFolder = "Assets/Models/MeshyProps/AlienBuddyV2";
        private const string RobotFolder = "Assets/Models/MeshyProps/RobotPetV2";
        private const string MatFolder = "Assets/Materials/Characters";

        [MenuItem("Setup/14 - Apply Character Textures")]
        public static void Apply()
        {
            EnsureFolder(MatFolder);
            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) { Debug.LogError("[ApplyChars] URP/Lit shader not found."); return; }

            // Mark the normal maps with the right import type so they sample correctly
            FixNormalImporter($"{AlienFolder}/AlienBuddyV2_Refined_normal.png");
            FixNormalImporter($"{RobotFolder}/RobotPetV2_Refined_normal.png");
            AssetDatabase.Refresh();

            var alienMat = BuildPbrMaterial("AlienBuddyV2_Mat", litShader,
                $"{AlienFolder}/AlienBuddyV2_Refined_base_color.png",
                $"{AlienFolder}/AlienBuddyV2_Refined_normal.png",
                $"{AlienFolder}/AlienBuddyV2_Refined_metallic.png",
                $"{AlienFolder}/AlienBuddyV2_Refined_roughness.png",
                $"{AlienFolder}/AlienBuddyV2_Refined_emission.png");

            var robotMat = BuildPbrMaterial("RobotPetV2_Mat", litShader,
                $"{RobotFolder}/RobotPetV2_Refined_base_color.png",
                $"{RobotFolder}/RobotPetV2_Refined_normal.png",
                $"{RobotFolder}/RobotPetV2_Refined_metallic.png",
                $"{RobotFolder}/RobotPetV2_Refined_roughness.png",
                $"{RobotFolder}/RobotPetV2_Refined_emission.png");

            AssetDatabase.SaveAssets();

            ApplyToTarget("AlienBuddy", alienMat);
            ApplyToTarget("RobotPet",   robotMat);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[ApplyChars] PBR materials built and applied to AlienBuddy + RobotPet.");
        }

        private static Material BuildPbrMaterial(string name, Shader shader,
            string baseColor, string normal, string metallic, string roughness, string emission)
        {
            string path = $"{MatFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(baseColor);
            if (bc != null) mat.SetTexture("_BaseMap", bc);
            mat.SetColor("_BaseColor", Color.white);

            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(normal);
            if (n != null)
            {
                mat.SetTexture("_BumpMap", n);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            // URP/Lit uses _MetallicGlossMap (RGB=metallic, A=smoothness in Unity convention,
            // but Meshy keeps them as separate maps — pick metallic and feed it as the gloss
            // map; smoothness comes from inverted roughness). Use the metallic map as-is and
            // dial smoothness with a simple float scale.
            var m = AssetDatabase.LoadAssetAtPath<Texture2D>(metallic);
            if (m != null)
            {
                mat.SetTexture("_MetallicGlossMap", m);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Metallic", 0.4f);
            mat.SetFloat("_Smoothness", 0.55f);

            var e = AssetDatabase.LoadAssetAtPath<Texture2D>(emission);
            if (e != null)
            {
                mat.SetTexture("_EmissionMap", e);
                mat.SetColor("_EmissionColor", Color.white * 1.0f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void FixNormalImporter(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null) return;
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
        }

        private static void ApplyToTarget(string goName, Material mat)
        {
            var go = GameObject.Find(goName);
            if (go == null) { Debug.LogWarning($"[ApplyChars] '{goName}' not found."); return; }
            var rends = go.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (rends.Length == 0) { Debug.LogWarning($"[ApplyChars] '{goName}' has no renderers."); return; }
            int total = 0;
            foreach (var r in rends)
            {
                int count = Mathf.Max(1, r.sharedMaterials.Length);
                var arr = new Material[count];
                for (int i = 0; i < count; i++) arr[i] = mat;
                r.sharedMaterials = arr;
                total += count;
            }
            Debug.Log($"[ApplyChars] '{goName}': applied to {rends.Length} renderers, {total} material slots.");
        }

        private static void EnsureFolder(string path)
        {
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
                AssetDatabase.Refresh();
            }
        }
    }
}
