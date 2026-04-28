using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// Setup/15 — Cleanup unused/low-quality Meshy props from MeshyProps_Root.
    /// Setup/16 — Build a tiny AnimatorController for the rigged alien so the
    ///            walking animation actually plays in scene + Play mode.
    /// </summary>
    public static class CleanupAndAnimate
    {
        // Props removed (the "lousy" earlier batch)
        private static readonly string[] PropsToRemove = new[]
        {
            "OxygenTank",
            "ToolsCaddy",
            "RobotHelper",
            "StarMapStand",
            "CoffeeMug",
            "PlanetGlobe",
            "HelmetStand",
        };

        private const string AlienWalkingFbx = "Assets/Models/MeshyProps/AlienBuddyV2/AlienBuddyV2_Walking.fbx";
        private const string AlienControllerPath = "Assets/Animations/AlienBuddy_Controller.controller";

        [MenuItem("Setup/15 - Cleanup Meshy Props (remove low-quality)")]
        public static void Cleanup()
        {
            var root = GameObject.Find("MeshyProps_Root");
            if (root == null) { Debug.LogError("[Cleanup] MeshyProps_Root not found."); return; }

            int removed = 0;
            foreach (var name in PropsToRemove)
            {
                var t = root.transform.Find(name);
                if (t != null) { Object.DestroyImmediate(t.gameObject); removed++; }
            }
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[Cleanup] Removed {removed}/{PropsToRemove.Length} low-quality props. Kept: AlienBuddy, RobotPet, StarCrystal + celestial bodies.");
        }

        [MenuItem("Setup/16 - Wire Alien Walking Animator")]
        public static void WireAlienAnimator()
        {
            // 1. Find walking animation clip embedded in the FBX
            AnimationClip clip = null;
            var subAssets = AssetDatabase.LoadAllAssetsAtPath(AlienWalkingFbx);
            foreach (var a in subAssets)
            {
                if (a is AnimationClip c && !c.name.StartsWith("__preview__"))
                {
                    clip = c; break;
                }
            }
            if (clip == null)
            {
                Debug.LogError($"[AlienAnim] No AnimationClip found inside {AlienWalkingFbx}. Is the FBX import set to Generic with Import Animation enabled?");
                return;
            }

            // 2. Create / reuse a tiny controller that always plays the walk clip
            EnsureFolder("Assets/Animations");
            var ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(AlienControllerPath);
            if (ctrl == null)
            {
                ctrl = AnimatorController.CreateAnimatorControllerAtPath(AlienControllerPath);
            }

            // Wipe states & re-create one always-walking state for predictability
            var sm = ctrl.layers[0].stateMachine;
            foreach (var st in sm.states) sm.RemoveState(st.state);
            var walkState = sm.AddState("Walk");
            walkState.motion = clip;
            sm.defaultState = walkState;

            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();

            // 3. Assign to the alien in scene
            var alien = GameObject.Find("AlienBuddy");
            if (alien == null) { Debug.LogError("[AlienAnim] AlienBuddy GameObject not found."); return; }
            var anim = alien.GetComponentInChildren<Animator>();
            if (anim == null) anim = alien.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl;
            anim.applyRootMotion = false;  // WanderingAI moves the transform; root motion would double-up

            // Hook the animator into WanderingAI
            var wander = alien.GetComponent<WanderingAI>();
            if (wander != null) wander.animator = anim;

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log($"[AlienAnim] Wired AnimatorController '{AlienControllerPath}' on AlienBuddy with clip '{clip.name}'.");
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
