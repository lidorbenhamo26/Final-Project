using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Setup/21 — Install the Battery Cell Delivery mission that replaces the
    /// N-back task on the Life Support station:
    ///   - configures the Meshy battery + charging-rack FBX importers
    ///   - builds URP/Lit materials from the Meshy PBR PNGs
    ///   - builds the carryable BatteryCell prefab into Resources
    ///   - places the charging rack in the central hub with a spawn point
    ///   - adds a BatterySocket to the Life Support console
    ///   - removes that station's dock prompt + legacy LifeSupportTask relic
    /// Idempotent — safe to re-run.
    /// </summary>
    public static class InstallBatteryMission
    {
        private const string BatteryDir = "Assets/Models/MeshyProps/BatteryCell";
        private const string RackDir    = "Assets/Models/MeshyProps/BatteryChargingRack";
        private const string BatteryFbx = BatteryDir + "/BatteryCell.fbx";
        private const string RackFbx    = RackDir + "/BatteryChargingRack.fbx";
        private const string MatFolder  = "Assets/Materials/Props";
        private const string PrefabPath = "Assets/Resources/BatteryCell.prefab";

        [MenuItem("Setup/21 - Install Battery Delivery Mission")]
        public static void Install()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) { Debug.LogError("[BatteryMission] URP/Lit shader missing. Aborting."); return; }

            EnsureFolder("Assets/Materials");
            EnsureFolder(MatFolder);
            EnsureFolder("Assets/Resources");

            ConfigureModelImporter(BatteryFbx);
            ConfigureModelImporter(RackFbx);
            FixNormalMap(BatteryDir + "/BatteryCell_normal.png");
            FixNormalMap(RackDir + "/BatteryChargingRack_normal.png");

            var batteryMat = BuildPbrMaterial("BatteryCell_Mat", lit, BatteryDir + "/BatteryCell", 2.6f);
            var rackMat    = BuildPbrMaterial("BatteryChargingRack_Mat", lit, RackDir + "/BatteryChargingRack", 1.7f);

            BuildBatteryPrefab(batteryMat);
            PlaceChargingRack(rackMat);
            InstallSocket();
            CleanLifeSupportStation();

            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[BatteryMission] Install complete.");
        }

        [MenuItem("Setup/22 - Test - Spawn Battery Mission (Play Mode)")]
        public static void TestSpawnBatteryMission()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BatteryMission] Enter Play Mode first, then run this to spawn the mission.");
                return;
            }
            var gm = GameManager.Instance;
            if (gm == null) { Debug.LogWarning("[BatteryMission] GameManager.Instance is null."); return; }
            gm.ForceSpawnLifeSupportTask();
            Debug.Log("[BatteryMission] Spawned the battery mission at Life Support.");
        }

        [MenuItem("Setup/23 - Test - Pick Up Battery (Play Mode)")]
        public static void TestPickUpBattery()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[BatteryMission] Enter Play Mode first.");
                return;
            }
            var cell = Object.FindAnyObjectByType<CarryableBattery>();
            if (cell == null)
            {
                Debug.LogWarning("[BatteryMission] No battery found — spawn the mission first (Setup/22).");
                return;
            }
            cell.PickUp();
            Debug.Log("[BatteryMission] Forced battery pickup.");
        }

        // ──────────────────────────────────────────────────────────── prefab

        private static void BuildBatteryPrefab(Material mat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(BatteryFbx);
            if (fbx == null) { Debug.LogError("[BatteryMission] Battery FBX missing: " + BatteryFbx); return; }

            var root = new GameObject("BatteryCell");
            var model = (GameObject)Object.Instantiate(fbx);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            ApplyMaterial(model, mat);
            NormalizeAndGround(model, 0.42f);

            root.AddComponent<CarryableBattery>();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            Debug.Log("[BatteryMission] Built carryable prefab: " + PrefabPath);
        }

        // ────────────────────────────────────────────────────────────── rack

        private static void PlaceChargingRack(Material mat)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(RackFbx);
            if (fbx == null) { Debug.LogError("[BatteryMission] Rack FBX missing: " + RackFbx); return; }

            var old = GameObject.Find("BatteryChargingRack");
            if (old != null) Object.DestroyImmediate(old);

            var parent = GameObject.Find("MeshyProps_Root");
            var rack = (GameObject)Object.Instantiate(fbx);
            rack.name = "BatteryChargingRack";
            if (parent != null) rack.transform.SetParent(parent.transform, true);
            rack.transform.position = new Vector3(2.8f, 0f, 2.8f);
            rack.transform.rotation = Quaternion.Euler(0f, 215f, 0f);

            ApplyMaterial(rack, mat);
            NormalizeAndGround(rack, 1.5f);

            var rackComp = rack.AddComponent<BatteryStorageRack>();

            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(rack.transform, true);
            spawn.transform.position = new Vector3(rack.transform.position.x, 1.3f, rack.transform.position.z);
            rackComp.spawnPoint = spawn.transform;

            Debug.Log("[BatteryMission] Placed charging rack in the hub at " + rack.transform.position);
        }

        // ──────────────────────────────────────────────────────────── socket

        private static void InstallSocket()
        {
            var station = GameObject.Find("LifeSupportStation");
            if (station == null) { Debug.LogWarning("[BatteryMission] LifeSupportStation not found."); return; }

            var existing = station.transform.Find("BatterySocket");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);

            var socket = new GameObject("BatterySocket");
            socket.transform.SetParent(station.transform, false);
            socket.transform.localPosition = new Vector3(0f, 1.0f, 0.5f);
            socket.transform.localRotation = Quaternion.identity;
            socket.AddComponent<BatterySocket>();
            Debug.Log("[BatteryMission] Added BatterySocket to the Life Support console.");
        }

        private static void CleanLifeSupportStation()
        {
            var station = GameObject.Find("LifeSupportStation");
            if (station == null) return;

            // String lookups so this script does not depend on the soon-deleted
            // NBack/LifeSupportTask types compiling.
            var prompt = station.GetComponent("StationProximityPrompt");
            if (prompt != null) Object.DestroyImmediate(prompt);
            var relic = station.GetComponent("LifeSupportTask");
            if (relic != null) Object.DestroyImmediate(relic);
            Debug.Log("[BatteryMission] Cleaned LifeSupportStation (removed dock prompt + relic task).");
        }

        // ───────────────────────────────────────────────────────────── helpers

        private static Material BuildPbrMaterial(string name, Shader shader, string texBase, float emissionBoost)
        {
            string path = MatFolder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.shader = shader;

            var bc = AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_base_color.png");
            if (bc != null) mat.SetTexture("_BaseMap", bc);
            mat.SetColor("_BaseColor", Color.white);

            var n = AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_normal.png");
            if (n != null)
            {
                mat.SetTexture("_BumpMap", n);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }

            var m = AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_metallic.png");
            if (m != null)
            {
                mat.SetTexture("_MetallicGlossMap", m);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Metallic", 0.6f);
            mat.SetFloat("_Smoothness", 0.55f);

            var e = AssetDatabase.LoadAssetAtPath<Texture2D>(texBase + "_emission.png");
            if (e != null)
            {
                mat.SetTexture("_EmissionMap", e);
                mat.SetColor("_EmissionColor", Color.white * emissionBoost);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigureModelImporter(string fbxPath)
        {
            var imp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (imp == null) { Debug.LogWarning("[BatteryMission] No ModelImporter at " + fbxPath); return; }
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.SaveAndReimport();
        }

        private static void FixNormalMap(string pngPath)
        {
            var imp = AssetImporter.GetAtPath(pngPath) as TextureImporter;
            if (imp == null) return;
            if (imp.textureType != TextureImporterType.NormalMap)
            {
                imp.textureType = TextureImporterType.NormalMap;
                imp.SaveAndReimport();
            }
        }

        private static void ApplyMaterial(GameObject root, Material mat)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                int count = Mathf.Max(1, r.sharedMaterials.Length);
                var arr = new Material[count];
                for (int i = 0; i < count; i++) arr[i] = mat;
                r.sharedMaterials = arr;
            }
        }

        private static void NormalizeAndGround(GameObject visual, float targetHeight)
        {
            var rends = visual.GetComponentsInChildren<Renderer>(true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            if (b.size.y < 0.0001f) return;

            float scale = targetHeight / b.size.y;
            visual.transform.localScale *= scale;

            b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float groundY = visual.transform.parent != null ? visual.transform.parent.position.y : 0f;
            visual.transform.position += new Vector3(0f, groundY - b.min.y, 0f);
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
