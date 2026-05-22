#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceStation.EditorSetup
{
    /// <summary>
    /// After the astronaut FBX was re-rigged in Blender with RightFingers1/RightFingers2
    /// bones, the standalone scene object and prefab still carry the old 24-bone skeleton.
    /// This wires the two new bones into the Armature and rebuilds the SkinnedMeshRenderer
    /// bone array to match the re-imported mesh's 26 bindposes.
    /// </summary>
    public static class InstallFingerBones
    {
        const string FbxPath = "Assets/Characters/Astronaut/Models/Astronaut_Character.fbx";
        const string PrefabPath = "Assets/Characters/Astronaut/Prefabs/Astronaut.prefab";

        [MenuItem("Setup/30 - Install Finger Bones On Astronaut")]
        public static void Run()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null) { Debug.LogError("[FingerBones] FBX not found: " + FbxPath); return; }

            var fbxSmr = fbx.GetComponentInChildren<SkinnedMeshRenderer>();
            if (fbxSmr == null) { Debug.LogError("[FingerBones] FBX has no SkinnedMeshRenderer"); return; }

            Transform[] refBones = fbxSmr.bones;
            Transform refF1 = refBones.FirstOrDefault(b => b.name == "RightFingers1");
            Transform refF2 = refBones.FirstOrDefault(b => b.name == "RightFingers2");
            if (refF1 == null || refF2 == null)
            {
                Debug.LogError("[FingerBones] FBX is missing RightFingers bones — re-import the FBX first.");
                return;
            }

            GameObject sceneAstro = GameObject.FindGameObjectWithTag("Player");
            if (sceneAstro == null) { Debug.LogError("[FingerBones] No Player-tagged astronaut in the scene."); return; }

            bool isInstance = PrefabUtility.IsPartOfPrefabInstance(sceneAstro);
            if (isInstance)
            {
                string p = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(sceneAstro);
                EditPrefabAsset(p, refBones, refF1, refF2);
            }
            else
            {
                ApplyTo(sceneAstro, refBones, refF1, refF2, "scene");
                EditorSceneManager.MarkSceneDirty(sceneAstro.scene);
                EditPrefabAsset(PrefabPath, refBones, refF1, refF2);
            }

            EditorSceneManager.SaveOpenScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[FingerBones] Done. sceneAstroIsPrefabInstance=" + isInstance);
        }

        static void EditPrefabAsset(string path, Transform[] refBones, Transform refF1, Transform refF2)
        {
            if (string.IsNullOrEmpty(path)) { Debug.LogWarning("[FingerBones] no prefab path to edit"); return; }
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            ApplyTo(root, refBones, refF1, refF2, "prefab:" + path);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
        }

        static void ApplyTo(GameObject astro, Transform[] refBones, Transform refF1, Transform refF2, string label)
        {
            Transform rightHand = FindDeep(astro.transform, "RightHand");
            if (rightHand == null) { Debug.LogError("[FingerBones] " + label + ": RightHand not found"); return; }

            Transform f1 = FindDeep(astro.transform, "RightFingers1");
            if (f1 == null)
            {
                f1 = new GameObject("RightFingers1").transform;
                f1.SetParent(rightHand, false);
            }
            f1.localPosition = refF1.localPosition;
            f1.localRotation = refF1.localRotation;
            f1.localScale = refF1.localScale;

            Transform f2 = FindDeep(astro.transform, "RightFingers2");
            if (f2 == null)
            {
                f2 = new GameObject("RightFingers2").transform;
                f2.SetParent(f1, false);
            }
            f2.localPosition = refF2.localPosition;
            f2.localRotation = refF2.localRotation;
            f2.localScale = refF2.localScale;

            var smr = astro.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null) { Debug.LogError("[FingerBones] " + label + ": no SkinnedMeshRenderer"); return; }

            Transform[] newBones = new Transform[refBones.Length];
            int missing = 0;
            for (int i = 0; i < refBones.Length; i++)
            {
                newBones[i] = FindDeep(astro.transform, refBones[i].name);
                if (newBones[i] == null) { Debug.LogError("[FingerBones] " + label + ": missing bone " + refBones[i].name); missing++; }
            }
            if (missing > 0) return;

            smr.bones = newBones;
            EditorUtility.SetDirty(smr);
            Debug.Log("[FingerBones] " + label + ": bone array rebuilt -> " + newBones.Length + " bones");
        }

        static Transform FindDeep(Transform root, string n)
        {
            if (root.name == n) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), n);
                if (r != null) return r;
            }
            return null;
        }
    }
}
#endif
