using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    public static class FirstPersonHandsInstaller
    {
        private const string ModelPath = "Assets/Models/MeshyProps/FirstPersonHands/FirstPersonHands.fbx";
        private const string PrefabPath = "Assets/Prefabs/Player/FirstPersonHands.prefab";

        [MenuItem("Setup/21 - Install First Person Hands")]
        public static void Install()
        {
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

            var dock = Object.FindFirstObjectByType<StationDockController>();
            var station = Object.FindFirstObjectByType<TaskStation>();
            var player = Object.FindFirstObjectByType<AstronautController>();
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
            float maxSize = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            float localScale = maxSize > 0.0001f ? Mathf.Clamp(0.85f / maxSize, 0.04f, 0.55f) : 0.35f;

            var so = new SerializedObject(view);
            so.FindProperty("handsPrefab").objectReferenceValue = prefab;
            so.FindProperty("localPosition").vector3Value = new Vector3(0f, -0.62f, 0.85f);
            so.FindProperty("localEulerAngles").vector3Value = new Vector3(6f, 0f, 0f);
            so.FindProperty("localScale").vector3Value = Vector3.one * localScale;
            so.ApplyModifiedProperties();
        }

        private static void ConfigureDockController(FirstPersonHandsView view)
        {
            var dock = Object.FindFirstObjectByType<StationDockController>();
            if (dock == null) return;

            var so = new SerializedObject(dock);
            so.FindProperty("handsView").objectReferenceValue = view;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(dock);
        }
    }
}
