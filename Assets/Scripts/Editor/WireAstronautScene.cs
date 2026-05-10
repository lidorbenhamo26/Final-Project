#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

public static class WireAstronautScene
{
    const string CharFbx        = "Assets/Characters/Astronaut/Models/Astronaut_Character.fbx";
    const string MaterialPath   = "Assets/Characters/Astronaut/Materials/M_Astronaut.mat";
    const string ControllerPath = "Assets/Characters/Astronaut/Animations/AC_Astronaut.controller";
    const string PrefabPath     = "Assets/Characters/Astronaut/Prefabs/Astronaut.prefab";
    const string InputActions   = "Assets/InputSystem_Actions.inputactions";

    [MenuItem("Tools/Astronaut/Wire Scene")]
    public static void Run()
    {
        var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CharFbx);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        var rac = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
        var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActions);

        if (fbx == null) { Debug.LogError("[AstroWire] character FBX not found"); return; }
        if (mat == null) { Debug.LogError("[AstroWire] material not found"); return; }
        if (rac == null) { Debug.LogError("[AstroWire] animator controller not found"); return; }
        if (actions == null) { Debug.LogError("[AstroWire] InputActionAsset not found"); return; }

        // 1. Remove any pre-existing Astronaut to keep this idempotent
        var existing = GameObject.Find("Astronaut");
        if (existing != null) Object.DestroyImmediate(existing);

        // 2. Instantiate the model in the scene
        var go = (GameObject)PrefabUtility.InstantiatePrefab(fbx);
        go.name = "Astronaut";
        go.tag = "Player";
        go.transform.position = new Vector3(0f, 0f, 0f);
        // Unpack so we can add components / save as new prefab
        PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        // 3. Override the SkinnedMeshRenderer material(s)
        foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mats = new Material[smr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            smr.sharedMaterials = mats;
        }
        foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
        {
            var mats = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = mat;
            mr.sharedMaterials = mats;
        }

        // 4. Wire animator FIRST — must exist before AstronautController is added
        // (AstronautController has [RequireComponent(typeof(Animator))] and its Awake reads animator)
        var animator = go.GetComponent<Animator>();
        if (animator == null) animator = go.AddComponent<Animator>();
        animator.runtimeAnimatorController = rac;
        animator.applyRootMotion = false;
        // Generic rigs need the avatar — try to find one or let Animator pick up the bone hierarchy
        var charFbx = AssetDatabase.LoadAllAssetsAtPath(CharFbx);
        foreach (var sub in charFbx) if (sub is Avatar a) { animator.avatar = a; break; }

        // 5. Physics
        var rb = go.GetComponent<Rigidbody>();
        if (rb == null) rb = go.AddComponent<Rigidbody>();
        rb.mass = 70f;
        rb.linearDamping = 0f;
        rb.angularDamping = 0.05f;
        rb.useGravity = true;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;

        // Determine astronaut height from the SkinnedMeshRenderer bounds so the capsule fits
        float heightApprox = 1.8f, radiusApprox = 0.3f;
        var skinned = go.GetComponentInChildren<SkinnedMeshRenderer>();
        if (skinned != null)
        {
            var b = skinned.bounds;
            heightApprox = Mathf.Max(0.5f, b.size.y);
            radiusApprox = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.5f, 0.2f, 0.6f);
        }

        var cap = go.GetComponent<CapsuleCollider>();
        if (cap == null) cap = go.AddComponent<CapsuleCollider>();
        cap.height = heightApprox;
        cap.radius = radiusApprox;
        cap.center = new Vector3(0f, heightApprox * 0.5f, 0f);

        // 6. PlayerInput (Send Messages so OnMove/OnJump/OnSprint hit AstronautController)
        var pi = go.GetComponent<PlayerInput>();
        if (pi == null) pi = go.AddComponent<PlayerInput>();
        pi.actions = actions;
        pi.defaultActionMap = "Player";
        pi.notificationBehavior = PlayerNotifications.SendMessages;
        pi.neverAutoSwitchControlSchemes = false;

        // 7. AstronautController
        if (go.GetComponent<AstronautController>() == null) go.AddComponent<AstronautController>();

        // 8. Save as prefab
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
        var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, PrefabPath, InteractionMode.AutomatedAction);
        Debug.Log("[AstroWire] prefab saved: " + (prefab != null ? prefab.name : "NULL") + $" capsule h={heightApprox:F2} r={radiusApprox:F2}");

        // 9. Re-target Main Camera: drop old player-camera role, install ThirdPersonCamera
        var cam = GameObject.Find("Main Camera");
        if (cam != null)
        {
            // The camera was acting as the trigger-collider "Player"; that role moves to the astronaut
            var camRb = cam.GetComponent<Rigidbody>();
            if (camRb != null) Object.DestroyImmediate(camRb);
            var camCol = cam.GetComponent<SphereCollider>();
            if (camCol != null) Object.DestroyImmediate(camCol);
            cam.tag = "MainCamera";

            var follow = cam.GetComponent<ThirdPersonCamera>();
            if (follow == null) follow = cam.AddComponent<ThirdPersonCamera>();
            // Use SerializedObject so we can hit private serialized fields
            var so = new SerializedObject(follow);
            so.FindProperty("target").objectReferenceValue = go.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[AstroWire] Main Camera reconfigured → ThirdPersonCamera target=Astronaut");
        }
        else
        {
            Debug.LogWarning("[AstroWire] Main Camera not found — couldn't install follow camera");
        }

        // 10. Save the scene
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Selection.activeGameObject = go;
        Debug.Log("[AstroWire] DONE — Astronaut spawned at origin, Main Camera follows.");
    }
}
#endif
