using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Setup/17 — Replace the old ControlTable visuals on the 4 mission stations
    ///            with the new Meshy-generated cognitive console FBXs.
    /// Setup/18 — Add StationProximityPrompt to each station so [E] hint appears
    ///            when the astronaut is in range.
    /// Setup/19 — Spawn the dock controller + interact-input binding GameObjects,
    ///            add a FirstPersonStationCamera to Main Camera, ensure EventSystem
    ///            and PhysicsRaycaster exist for clickable in-station UI.
    /// Setup/20 — Push any Meshy props out of the way of the new consoles so the
    ///            player has a clear approach + dock pose.
    /// </summary>
    public static class InstallCognitiveStations
    {
        private const string MeshyRoot = "Assets/Models/MeshyStations";
        private const string MaterialFolder = "Assets/Materials/Stations";

        // The four stations to upgrade. Names must match exactly the GameObject names
        // produced by StationsBuilder + the literal switch arms in CognitiveTaskCatalog.
        private static readonly StationSpec[] Specs = new StationSpec[]
        {
            new StationSpec("EngineStation",      "EngineConsole"),
            new StationSpec("NavigationStation",  "NavConsole"),
            new StationSpec("CommsStation",       "CommsConsole"),
            new StationSpec("LifeSupportStation", "LifeConsole"),
        };

        private struct StationSpec
        {
            public string StationName;
            public string ConsoleFolder;     // matches Assets/Models/MeshyStations/<this>/
            public StationSpec(string sn, string cf) { StationName = sn; ConsoleFolder = cf; }
            public string FbxPath        => $"{MeshyRoot}/{ConsoleFolder}/{ConsoleFolder}.fbx";
            public string BaseColorPath  => $"{MeshyRoot}/{ConsoleFolder}/{ConsoleFolder}_base_color.png";
            public string NormalPath     => $"{MeshyRoot}/{ConsoleFolder}/{ConsoleFolder}_normal.png";
            public string MetallicPath   => $"{MeshyRoot}/{ConsoleFolder}/{ConsoleFolder}_metallic.png";
            public string RoughnessPath  => $"{MeshyRoot}/{ConsoleFolder}/{ConsoleFolder}_roughness.png";
            public string EmissionPath   => $"{MeshyRoot}/{ConsoleFolder}/{ConsoleFolder}_emission.png";
        }

        // ─────────────────────────────────────────────────────────────────────
        // Setup/17 — Install Cognitive Consoles
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Setup/17 - Install Cognitive Stations (replace ControlTables)")]
        public static void InstallConsoles()
        {
            EnsureFolder(MaterialFolder);

            var litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null)
            {
                Debug.LogError("[InstallCognitive] URP/Lit shader not found. Aborting.");
                return;
            }

            int installed = 0;
            foreach (var spec in Specs)
            {
                var stationGO = GameObject.Find(spec.StationName);
                if (stationGO == null)
                {
                    Debug.LogError($"[InstallCognitive] Station '{spec.StationName}' not found in scene.");
                    continue;
                }

                // Make sure the FBX importers are configured before we instantiate.
                FixNormalImporter(spec.NormalPath);

                // Build (or refresh) the URP/Lit material from the Meshy PBR PNGs.
                var mat = BuildPbrMaterial(spec.ConsoleFolder + "_Mat", litShader, spec);

                // Replace 'Visual' child with the new Meshy console.
                var oldVisual = stationGO.transform.Find("Visual");
                if (oldVisual != null) Object.DestroyImmediate(oldVisual.gameObject);

                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(spec.FbxPath);
                if (fbx == null)
                {
                    Debug.LogError($"[InstallCognitive] FBX not found at {spec.FbxPath}");
                    continue;
                }
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(fbx, stationGO.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one;

                ApplyMaterialToAllRenderers(visual, mat);

                // Meshy meshes are tiny (~2cm). Scale and ground.
                NormalizeAndGround(visual, targetHeight: 1.5f);

                // Make sure there's a DockPoint child where the FP camera will park.
                EnsureDockPoint(stationGO);

                Debug.Log($"[InstallCognitive] '{spec.StationName}' ← {spec.FbxPath} (mat: {mat.name})");
                installed++;
            }

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[InstallCognitive] Installed {installed}/{Specs.Length} cognitive consoles.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Setup/17b — Add solid colliders so the astronaut can't walk through
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Setup/17b - Add Solid Colliders to Consoles")]
        public static void AddSolidColliders()
        {
            int added = 0;
            foreach (var spec in Specs)
            {
                var stationGO = GameObject.Find(spec.StationName);
                if (stationGO == null) continue;
                var visualT = stationGO.transform.Find("Visual");
                if (visualT == null) continue;
                var visual = visualT.gameObject;

                // Remove any existing solid collider so re-running is idempotent.
                foreach (var bc in visual.GetComponentsInChildren<BoxCollider>())
                    Object.DestroyImmediate(bc);

                // Compute combined renderer bounds in world space, then convert to
                // visual-local space for a tight box collider.
                var rends = visual.GetComponentsInChildren<Renderer>(includeInactive: true);
                if (rends.Length == 0) continue;
                Bounds worldB = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) worldB.Encapsulate(rends[i].bounds);

                // Place the collider on the visual itself so it scales naturally.
                var box = visual.AddComponent<BoxCollider>();
                box.isTrigger = false;

                // Convert world-space bounds to local-space size + center
                Vector3 localCenter = visual.transform.InverseTransformPoint(worldB.center);
                Vector3 localExtents = visual.transform.InverseTransformVector(worldB.extents);
                box.center = localCenter;
                box.size = new Vector3(
                    Mathf.Abs(localExtents.x) * 2f,
                    Mathf.Abs(localExtents.y) * 2f,
                    Mathf.Abs(localExtents.z) * 2f);
                added++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[InstallCognitive] Added solid BoxColliders to {added}/{Specs.Length} console visuals.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Setup/18 — Wire StationProximityPrompt
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Setup/18 - Wire Cognitive Tasks (Proximity Prompt)")]
        public static void WireProximity()
        {
            int wired = 0;
            foreach (var spec in Specs)
            {
                var stationGO = GameObject.Find(spec.StationName);
                if (stationGO == null) continue;

                if (stationGO.GetComponent<StationProximityPrompt>() == null)
                {
                    stationGO.AddComponent<StationProximityPrompt>();
                }
                wired++;
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[InstallCognitive] Proximity prompts wired on {wired}/{Specs.Length} stations.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Setup/19 — Dock controller + Input + EventSystem + FP camera
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Setup/19 - Setup Dock Controller + Input")]
        public static void SetupDockController()
        {
            // EventSystem (needed for any UI clicks at all)
            var es = Object.FindAnyObjectByType<EventSystem>();
            if (es == null)
            {
                var esGO = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                Debug.Log("[InstallCognitive] Created EventSystem with Input System UI module.");
            }

            // PhysicsRaycaster on Main Camera → enables clicking 3D buttons later
            var cam = Camera.main;
            if (cam != null && cam.GetComponent<PhysicsRaycaster>() == null)
            {
                cam.gameObject.AddComponent<PhysicsRaycaster>();
            }

            // FirstPersonStationCamera lives on Main Camera (disabled by default,
            // dock controller toggles it on).
            if (cam != null)
            {
                var fp = cam.GetComponent<FirstPersonStationCamera>();
                if (fp == null) fp = cam.gameObject.AddComponent<FirstPersonStationCamera>();
                fp.enabled = false;
            }

            // _DockController GameObject
            var dockGO = GameObject.Find("_DockController");
            if (dockGO == null)
            {
                dockGO = new GameObject("_DockController");
            }
            if (dockGO.GetComponent<StationDockController>() == null)
                dockGO.AddComponent<StationDockController>();
            if (dockGO.GetComponent<InteractInputBinding>() == null)
                dockGO.AddComponent<InteractInputBinding>();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[InstallCognitive] Dock controller + input + FP camera installed.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Setup/20 — Clear blocking props
        // ─────────────────────────────────────────────────────────────────────

        [MenuItem("Setup/20 - Clear Blocking Props in Mission Rooms")]
        public static void ClearBlockingProps()
        {
            const float clearRadius = 2.0f;
            var meshyRoot = GameObject.Find("MeshyProps_Root");
            if (meshyRoot == null)
            {
                Debug.Log("[InstallCognitive] MeshyProps_Root not found, nothing to clear.");
                return;
            }

            var stationPositions = new List<Vector3>();
            foreach (var spec in Specs)
            {
                var s = GameObject.Find(spec.StationName);
                if (s != null) stationPositions.Add(s.transform.position);
            }

            int moved = 0;
            foreach (Transform prop in meshyRoot.transform)
            {
                foreach (var sp in stationPositions)
                {
                    Vector3 delta = prop.position - sp;
                    delta.y = 0f;
                    if (delta.magnitude < clearRadius)
                    {
                        // Push the prop radially outward to the room edge (~3.5m from station).
                        Vector3 push = delta.sqrMagnitude < 0.001f ? Vector3.right : delta.normalized;
                        prop.position = sp + push * 3.5f + Vector3.up * (prop.position.y - sp.y);
                        moved++;
                        break;
                    }
                }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[InstallCognitive] Pushed {moved} blocking props out of station footprints.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────

        private static Material BuildPbrMaterial(string name, Shader shader, StationSpec spec)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.BaseColorPath);
            if (bc != null) mat.SetTexture("_BaseMap", bc);
            mat.SetColor("_BaseColor", Color.white);

            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.NormalPath);
            if (n != null)
            {
                mat.SetTexture("_BumpMap", n);
                mat.SetFloat("_BumpScale", 1.0f);
                mat.EnableKeyword("_NORMALMAP");
            }

            var m = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.MetallicPath);
            if (m != null)
            {
                mat.SetTexture("_MetallicGlossMap", m);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Metallic", 0.6f);
            mat.SetFloat("_Smoothness", 0.55f);

            var e = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.EmissionPath);
            if (e != null)
            {
                mat.SetTexture("_EmissionMap", e);
                mat.SetColor("_EmissionColor", Color.white * 1.2f);
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

        private static void ApplyMaterialToAllRenderers(GameObject root, Material mat)
        {
            var rends = root.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (var r in rends)
            {
                int count = Mathf.Max(1, r.sharedMaterials.Length);
                var arr = new Material[count];
                for (int i = 0; i < count; i++) arr[i] = mat;
                r.sharedMaterials = arr;
            }
        }

        private static void NormalizeAndGround(GameObject visual, float targetHeight)
        {
            var rends = visual.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            if (b.size.y < 0.0001f) return;
            float scale = targetHeight / b.size.y;
            visual.transform.localScale = Vector3.one * scale;

            // Recompute bounds after scaling and shift so bottom sits at parent y=0.
            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float deltaY = visual.transform.parent.position.y - b.min.y;
            visual.transform.position += new Vector3(0f, deltaY, 0f);
        }

        private static void EnsureDockPoint(GameObject station)
        {
            var existing = station.transform.Find("DockPoint");
            if (existing != null) return;

            var dp = new GameObject("DockPoint");
            dp.transform.SetParent(station.transform, false);

            // Place 1.4m in front of the console (toward the hub at world origin) at eye height.
            Vector3 stationPos = station.transform.position;
            Vector3 toHub = (Vector3.zero - new Vector3(stationPos.x, 0f, stationPos.z));
            if (toHub.sqrMagnitude < 0.001f) toHub = Vector3.back;
            toHub.Normalize();

            // World position: 1.4m toward hub from station, at 1.6m height.
            Vector3 dockWorld = stationPos + toHub * 1.4f + Vector3.up * 1.6f;
            dp.transform.position = dockWorld;

            // Rotation looks at the console at ~1.2m height.
            Vector3 lookTarget = stationPos + Vector3.up * 1.2f;
            dp.transform.rotation = Quaternion.LookRotation(lookTarget - dockWorld, Vector3.up);
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
