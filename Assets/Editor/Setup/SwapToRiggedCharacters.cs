using UnityEditor;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Setup/13 — Swap the static AlienBuddy / RobotPet props in MainScene
    /// for the new V2 rigged + textured characters from Meshy:
    ///   • AlienBuddyV2 — generated, refined, and rigged (walking + running)
    ///   • RobotPetV2   — generated and refined with PBR textures
    ///
    /// Configures the FBX importers (Generic rig, looping walk clip), removes
    /// the old GameObjects under MeshyProps_Root, instantiates the new FBX
    /// prefabs, and wires WanderingAI on the alien + HoverBob on the robot.
    /// </summary>
    public static class SwapToRiggedCharacters
    {
        private const string AlienWalkingFbx = "Assets/Models/MeshyProps/AlienBuddyV2/AlienBuddyV2_Walking.fbx";
        private const string RobotRefinedFbx = "Assets/Models/MeshyProps/RobotPetV2/RobotPetV2_Refined.fbx";

        [MenuItem("Setup/13 - Swap to Rigged Characters")]
        public static void Swap()
        {
            int npcLayer = EnsureNpcLayer();

            ConfigureRiggedFbx(AlienWalkingFbx, isHumanoid: false, animLoop: true);
            ConfigureStaticFbx(RobotRefinedFbx);
            AssetDatabase.Refresh();
            AssetDatabase.SaveAssets();

            var alienPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AlienWalkingFbx);
            var robotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RobotRefinedFbx);
            if (alienPrefab == null) { Debug.LogError($"[Swap] Alien FBX not found at {AlienWalkingFbx}"); return; }
            if (robotPrefab == null) { Debug.LogError($"[Swap] Robot FBX not found at {RobotRefinedFbx}"); return; }

            var root = GameObject.Find("MeshyProps_Root");
            if (root == null) { Debug.LogError("[Swap] MeshyProps_Root not found in scene."); return; }

            // Remove the old static AlienBuddy and RobotPet
            DestroyChild(root.transform, "AlienBuddy");
            DestroyChild(root.transform, "RobotPet");

            // ── Alien — full body T-pose with looping walk animation, wanders the hub
            var alien = (GameObject)PrefabUtility.InstantiatePrefab(alienPrefab, root.transform);
            alien.name = "AlienBuddy";
            alien.transform.position = new Vector3(-2.0f, 0f, -1.0f);
            alien.transform.rotation = Quaternion.Euler(0f, 35f, 0f);
            alien.transform.localScale = Vector3.one * 1.2f;  // ~1.6m height after Meshy rig at 1.6m
            GroundRenderer(alien);

            // Capsule blocks the player from walking through. Kinematic Rigidbody
            // so the dynamic player Rigidbody can't shove the alien around (which
            // would fight WanderingAI's transform translation).
            var alienCap = alien.AddComponent<CapsuleCollider>();
            alienCap.radius = 0.30f;
            alienCap.height = 1.5f;
            alienCap.center = new Vector3(0f, 0.75f, 0f);
            alienCap.direction = 1; // Y-axis
            var alienRb = alien.AddComponent<Rigidbody>();
            alienRb.isKinematic = true;
            alienRb.useGravity = false;
            SetLayerRecursive(alien, npcLayer);

            var wander = alien.AddComponent<WanderingAI>();
            wander.areaCenter = new Vector3(0f, 0f, 0f);
            wander.areaRadius = 4.5f;
            wander.walkSpeed = 0.9f;
            wander.idleTime = 1.2f;

            // Mischievous-pestering reactive layer — sits next to WanderingAI.
            var curiosity = alien.AddComponent<AlienCuriosity>();
            curiosity.losBlockers = ~(1 << npcLayer);
            curiosity.chirpInterval = new Vector2(2.5f, 4.0f);
            curiosity.pesterChirpVolume = 0.35f;
            curiosity.noticeChirpVolume = 0.6f;

            // ── Robot — refined PBR mesh, hovers + bobs
            var robot = (GameObject)PrefabUtility.InstantiatePrefab(robotPrefab, root.transform);
            robot.name = "RobotPet";
            robot.transform.position = new Vector3(2.5f, 0.6f, -1.0f);
            robot.transform.rotation = Quaternion.Euler(0f, 200f, 0f);
            robot.transform.localScale = Vector3.one * 1.0f;
            GroundRenderer(robot);
            robot.transform.position += new Vector3(0f, 0.6f, 0f);  // raise so it floats off ground

            var robotCap = robot.AddComponent<CapsuleCollider>();
            robotCap.radius = 0.25f;
            robotCap.height = 0.55f;
            robotCap.center = Vector3.zero;
            robotCap.direction = 1;
            var robotRb = robot.AddComponent<Rigidbody>();
            robotRb.isKinematic = true;
            robotRb.useGravity = false;
            SetLayerRecursive(robot, npcLayer);

            var bob = robot.AddComponent<HoverBob>();
            bob.bobAmplitude = 0.15f;
            bob.bobSpeed = 1.6f;
            bob.yawSpin = 6f;

            // The rigged walking FBX ships without textures (Meshy keeps them with
            // the refine task), so chain into Setup/14 — otherwise the alien/robot
            // would appear as pure white silhouettes until the user manually runs it.
            ApplyCharacterTextures.Apply();

            // Setup/16: builds the AnimatorController and wires it onto the alien.
            // Without this the alien T-poses (or slides) instead of playing its walk
            // clip when WanderingAI / AlienCuriosity move it.
            CleanupAndAnimate.WireAlienAnimator();

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("[Swap] Replaced AlienBuddy + RobotPet with V2 rigged/refined characters.");
        }

        /// <summary>Make sure a user layer named "NPC" exists. Returns its index.</summary>
        private static int EnsureNpcLayer()
        {
            int existing = LayerMask.NameToLayer("NPC");
            if (existing >= 0) return existing;

            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            if (layers == null || !layers.isArray)
            {
                Debug.LogWarning("[Swap] Could not read TagManager layers; falling back to Default layer.");
                return 0;
            }
            // Slots 0-7 are reserved Unity built-ins; user layers start at 8.
            for (int i = 8; i < layers.arraySize; i++)
            {
                var slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = "NPC";
                    tagManager.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssets();
                    Debug.Log($"[Swap] Created user layer 'NPC' at index {i}.");
                    return i;
                }
            }
            Debug.LogWarning("[Swap] No empty user-layer slot available; falling back to Default layer.");
            return 0;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (go == null) return;
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private static void ConfigureRiggedFbx(string path, bool isHumanoid, bool animLoop)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[Swap] No ModelImporter at {path}"); return; }

            imp.animationType = isHumanoid ? ModelImporterAnimationType.Human : ModelImporterAnimationType.Generic;
            imp.importAnimation = true;
            imp.optimizeGameObjects = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

            // Make the embedded clip loop forever
            var clips = imp.defaultClipAnimations;
            if (clips == null || clips.Length == 0) clips = imp.clipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int i = 0; i < clips.Length; i++)
                {
                    clips[i].loopTime = animLoop;
                    clips[i].loop = animLoop;
                }
                imp.clipAnimations = clips;
            }
            imp.SaveAndReimport();
        }

        private static void ConfigureStaticFbx(string path)
        {
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogWarning($"[Swap] No ModelImporter at {path}"); return; }
            imp.animationType = ModelImporterAnimationType.None;
            imp.importAnimation = false;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            imp.SaveAndReimport();
        }

        private static void DestroyChild(Transform parent, string name)
        {
            var t = parent.Find(name);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }

        private static void GroundRenderer(GameObject go)
        {
            var rends = go.GetComponentsInChildren<Renderer>(includeInactive: true);
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float deltaY = -b.min.y;  // shift so the bottom is at Y=0
            go.transform.position += new Vector3(0f, deltaY, 0f);
        }
    }
}
