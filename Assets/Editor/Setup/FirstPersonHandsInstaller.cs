using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    public static class FirstPersonHandsInstaller
    {
        private const string ModelPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHandsV2.fbx";
        private const string PrefabPath = "Assets/Prefabs/Player/FirstPersonHands.prefab";
        private const string MaterialPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHands_Mat.mat";
        private const string BaseColorPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHandsV2_base_color.png";
        private const string MetallicPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHandsV2_metallic.png";
        private const string RoughnessPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHandsV2_roughness.png";
        private const string NormalPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHandsV2_normal.png";
        private const string EmissionPath = "Assets/Models/MeshyProps/FirstPersonHandsV2/FirstPersonHandsV2_emission.png";

        // Target on-screen width of the hands (in local units, before final camera projection).
        // Used to compute localScale dynamically from the imported FBX bounds so the V2 model
        // (which is much larger natively than the old tiny FBX) fits the camera framing.
        private const float TargetHandsWidth = 0.55f;

        [MenuItem("Setup/21 - Install First Person Hands")]
        public static void Install()
        {
            ConfigureTextureImporters();
            AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh();

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
            {
                Debug.LogError("[FirstPersonHandsInstaller] Could not load model at " + ModelPath);
                return;
            }

            var temp = PrefabUtility.InstantiatePrefab(model) as GameObject;
            if (temp == null)
                temp = Object.Instantiate(model);

            temp.name = "FirstPersonHands";
            RemoveColliders(temp);

            var material = CreateOrUpdateHandsMaterial();
            AssignMaterial(temp, material);

            var renderers = temp.GetComponentsInChildren<Renderer>(true);
            Bounds bounds = CalculateBounds(renderers);

            var saved = PrefabUtility.SaveAsPrefabAsset(temp, PrefabPath);
            Object.DestroyImmediate(temp);

            if (saved == null)
            {
                Debug.LogError("[FirstPersonHandsInstaller] Failed to save prefab at " + PrefabPath);
                return;
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                Debug.LogError("[FirstPersonHandsInstaller] No Camera.main found.");
                return;
            }

            var view = camera.GetComponent<FirstPersonHandsView>();
            if (view == null)
                view = camera.gameObject.AddComponent<FirstPersonHandsView>();

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            ConfigureHandsView(view, prefab, bounds);
            ConfigureDockController(view);

            EditorUtility.SetDirty(view);
            EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();

            Debug.Log("[FirstPersonHandsInstaller] Installed " + PrefabPath
                + " renderers=" + renderers.Length
                + " bounds=" + bounds.size);
        }

        [MenuItem("Setup/22 - Smoke Test First Person Dock View")]
        public static void SmokeTestDockView()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[FirstPersonHandsInstaller] Smoke test must run in Play Mode.");
                return;
            }

            var dock = Object.FindAnyObjectByType<StationDockController>();
            var station = Object.FindAnyObjectByType<TaskStation>();
            var player = Object.FindAnyObjectByType<AstronautController>();
            var view = Camera.main != null ? Camera.main.GetComponent<FirstPersonHandsView>() : null;

            if (dock == null || station == null || player == null || view == null)
            {
                Debug.LogError("[FirstPersonHandsInstaller] Smoke test missing refs: dock="
                    + (dock != null) + " station=" + (station != null)
                    + " player=" + (player != null) + " view=" + (view != null));
                return;
            }

            var renderers = player.GetComponentsInChildren<Renderer>(true);
            bool[] original = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                original[i] = renderers[i] != null && renderers[i].enabled;

            dock.EnterDock(station);

            bool bodyHidden = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled)
                    bodyHidden = false;
            }

            bool handsVisible = view.IsVisible;
            bool controlsLocked = !player.ControlsEnabled;

            dock.ExitDock();

            bool bodyRestored = true;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].enabled != original[i])
                    bodyRestored = false;
            }

            bool handsHidden = !view.IsVisible;
            bool controlsRestored = player.ControlsEnabled;

            if (bodyHidden && handsVisible && controlsLocked && bodyRestored && handsHidden && controlsRestored)
            {
                Debug.Log("[FirstPersonHandsInstaller] Smoke test passed: body hidden/restored and hands toggled.");
            }
            else
            {
                Debug.LogError("[FirstPersonHandsInstaller] Smoke test failed: bodyHidden=" + bodyHidden
                    + " handsVisible=" + handsVisible
                    + " controlsLocked=" + controlsLocked
                    + " bodyRestored=" + bodyRestored
                    + " handsHidden=" + handsHidden
                    + " controlsRestored=" + controlsRestored);
            }
        }

        private static void RemoveColliders(GameObject root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = colliders.Length - 1; i >= 0; i--)
                Object.DestroyImmediate(colliders[i]);
        }

        private static void AssignMaterial(GameObject root, Material material)
        {
            if (material == null) return;

            var renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var materials = renderers[i].sharedMaterials;
                for (int j = 0; j < materials.Length; j++)
                    materials[j] = material;

                renderers[i].sharedMaterials = materials;
            }
        }

        private static Material CreateOrUpdateHandsMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                shader = Shader.Find("Standard");

            if (shader == null)
            {
                Debug.LogError("[FirstPersonHandsInstaller] Could not find a compatible lit shader.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = "FirstPersonHands_Mat"
                };
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);

            SetTextureIfPresent(material, "_BaseMap", baseColor);
            SetTextureIfPresent(material, "_MainTex", baseColor);
            SetColorIfPresent(material, "_BaseColor", Color.white);
            SetColorIfPresent(material, "_Color", Color.white);

            SetTextureIfPresent(material, "_MetallicGlossMap", metallic);
            SetFloatIfPresent(material, "_Metallic", 0.12f);
            SetFloatIfPresent(material, "_Smoothness", 0.42f);
            if (metallic != null)
                material.EnableKeyword("_METALLICSPECGLOSSMAP");

            SetTextureIfPresent(material, "_BumpMap", normal);
            SetFloatIfPresent(material, "_BumpScale", 0.8f);
            if (normal != null)
                material.EnableKeyword("_NORMALMAP");

            SetTextureIfPresent(material, "_EmissionMap", emission);
            SetColorIfPresent(material, "_EmissionColor", new Color(0.2f, 0.35f, 0.55f, 1f));
            if (emission != null)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureTextureImporters()
        {
            ConfigureTextureImporter(BaseColorPath, TextureImporterType.Default, true);
            ConfigureTextureImporter(MetallicPath, TextureImporterType.Default, false);
            ConfigureTextureImporter(RoughnessPath, TextureImporterType.Default, false);
            ConfigureTextureImporter(NormalPath, TextureImporterType.NormalMap, false);
            ConfigureTextureImporter(EmissionPath, TextureImporterType.Default, true);
        }

        private static void ConfigureTextureImporter(string path, TextureImporterType textureType, bool sRgb)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = importer.textureType != textureType || importer.sRGBTexture != sRgb;
            if (!changed) return;

            importer.textureType = textureType;
            importer.sRGBTexture = sRgb;
            importer.SaveAndReimport();
        }

        private static void SetTextureIfPresent(Material material, string propertyName, Texture texture)
        {
            if (texture != null && material.HasProperty(propertyName))
                material.SetTexture(propertyName, texture);
        }

        private static void SetColorIfPresent(Material material, string propertyName, Color color)
        {
            if (material.HasProperty(propertyName))
                material.SetColor(propertyName, color);
        }

        private static void SetFloatIfPresent(Material material, string propertyName, float value)
        {
            if (material.HasProperty(propertyName))
                material.SetFloat(propertyName, value);
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            if (renderers == null || renderers.Length == 0)
                return new Bounds(Vector3.zero, Vector3.one);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static void ConfigureHandsView(FirstPersonHandsView view, GameObject prefab, Bounds bounds)
        {
            // Dynamic scale: target ~0.55 world units across both hands. bounds.size.x is in world
            // space (already includes the FBX import scale that's baked into the prefab root), while
            // FirstPersonHandsView OVERRIDES the instance's localScale at runtime — so we must
            // account for the prefab's intrinsic scale to land at the target world size.
            // Final localScale = target / (raw_mesh_width) = target * prefab.scale / bounds.size.x
            float prefabScale = prefab != null ? prefab.transform.localScale.x : 1f;
            float currentWidth = Mathf.Max(bounds.size.x, 0.001f);
            float localScale = Mathf.Clamp(TargetHandsWidth * prefabScale / currentWidth, 0.05f, 200f);

            var so = new SerializedObject(view);
            so.FindProperty("handsPrefab").objectReferenceValue = prefab;
            // Hands sit below + in front of the camera. After the +95° X rotation below, the model's
            // fingers point forward (away from camera) and palms face down — the canonical first-person
            // "reaching the keypad" pose. Wrist cuffs end up at the bottom-near edge of the screen.
            so.FindProperty("localPosition").vector3Value = new Vector3(0f, -0.30f, 0.55f);
            so.FindProperty("localEulerAngles").vector3Value = new Vector3(95f, 0f, 0f);
            so.FindProperty("localScale").vector3Value = Vector3.one * localScale;
            so.FindProperty("idleMotion").boolValue = true;
            so.FindProperty("motionFrequency").floatValue = 0.55f;
            so.FindProperty("bobAmplitude").floatValue = 0.012f;
            so.FindProperty("swayAmplitude").floatValue = 0.016f;
            so.FindProperty("rollAmplitude").floatValue = 0.9f;
            so.FindProperty("pressForwardAmount").floatValue = 0.045f;
            so.FindProperty("pressPitchDegrees").floatValue = 6f;
            so.FindProperty("pressDuration").floatValue = 0.18f;
            so.ApplyModifiedProperties();
        }

        private static void ConfigureDockController(FirstPersonHandsView view)
        {
            var dock = Object.FindAnyObjectByType<StationDockController>();
            if (dock == null) return;

            var so = new SerializedObject(dock);
            so.FindProperty("handsView").objectReferenceValue = view;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(dock);
        }
    }
}
