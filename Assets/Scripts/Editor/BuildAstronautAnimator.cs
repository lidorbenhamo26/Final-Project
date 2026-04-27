#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using System.IO;

public static class BuildAstronautAnimator
{
    const string ControllerPath = "Assets/Characters/Astronaut/Animations/AC_Astronaut.controller";

    [MenuItem("Tools/Astronaut/Build Animator")]
    public static void Run()
    {
        var idle  = LoadClip("Assets/Characters/Astronaut/Models/Astronaut_Anim_Idle.fbx",  "Idle");
        var walk  = LoadClip("Assets/Characters/Astronaut/Models/Astronaut_Anim_Walk.fbx",  "Walk");
        var run   = LoadClip("Assets/Characters/Astronaut/Models/Astronaut_Anim_Run.fbx",   "Run");
        var jump  = LoadClip("Assets/Characters/Astronaut/Models/Astronaut_Anim_Jump.fbx",  "Jump");
        var wave  = LoadClip("Assets/Characters/Astronaut/Models/Astronaut_Anim_Wave.fbx",  "Wave");
        var alert = LoadClip("Assets/Characters/Astronaut/Models/Astronaut_Anim_Alert.fbx", "Alert");
        if (idle == null || walk == null || run == null || jump == null || wave == null || alert == null)
        {
            Debug.LogError($"[AstroAnim] missing clip(s): idle={idle} walk={walk} run={run} jump={jump} wave={wave} alert={alert}");
            return;
        }

        // Delete + recreate (so re-running rebuilds cleanly)
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null)
            AssetDatabase.DeleteAsset(ControllerPath);

        Directory.CreateDirectory(Path.GetDirectoryName(ControllerPath));
        var ac = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

        // Parameters
        ac.AddParameter("Speed", AnimatorControllerParameterType.Float);
        ac.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        ac.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Wave", AnimatorControllerParameterType.Trigger);
        ac.AddParameter("Alert", AnimatorControllerParameterType.Trigger);

        var sm = ac.layers[0].stateMachine;

        // Blend tree
        var blendTree = new BlendTree
        {
            name = "Locomotion",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false,
            hideFlags = HideFlags.HideInHierarchy,
        };
        AssetDatabase.AddObjectToAsset(blendTree, ac);
        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walk, 2f);
        blendTree.AddChild(run,  6f);

        var locomotionState = sm.AddState("Locomotion");
        locomotionState.motion = blendTree;
        locomotionState.writeDefaultValues = false;
        sm.defaultState = locomotionState;

        // Jump / Wave / Alert states (one-shot)
        var jumpState  = AddOneShot(sm, "Jump", jump);
        var waveState  = AddOneShot(sm, "Wave", wave);
        var alertState = AddOneShot(sm, "Alert", alert);

        // AnyState → Jump on trigger Jump (only if Grounded so we don't spam mid-air)
        // Slightly longer blends for a more natural feel
        var toJump = sm.AddAnyStateTransition(jumpState);
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Jump");
        toJump.AddCondition(AnimatorConditionMode.If, 0f, "Grounded");
        toJump.duration = 0.12f; toJump.hasExitTime = false; toJump.canTransitionToSelf = false;

        var toWave = sm.AddAnyStateTransition(waveState);
        toWave.AddCondition(AnimatorConditionMode.If, 0f, "Wave");
        toWave.duration = 0.18f; toWave.hasExitTime = false; toWave.canTransitionToSelf = false;

        var toAlert = sm.AddAnyStateTransition(alertState);
        toAlert.AddCondition(AnimatorConditionMode.If, 0f, "Alert");
        toAlert.duration = 0.18f; toAlert.hasExitTime = false; toAlert.canTransitionToSelf = false;

        // One-shots → back to Locomotion (longer blend out, exit a bit earlier so transitions overlap)
        AddExit(jumpState,  locomotionState, 0.7f,  0.2f);
        AddExit(waveState,  locomotionState, 0.9f,  0.25f);
        AddExit(alertState, locomotionState, 0.9f,  0.25f);

        EditorUtility.SetDirty(ac);
        AssetDatabase.SaveAssetIfDirty(ac);
        AssetDatabase.ImportAsset(ControllerPath);

        Debug.Log("[AstroAnim] AC_Astronaut built. states=" + sm.states.Length + " params=" + ac.parameters.Length);
    }

    static AnimatorState AddOneShot(AnimatorStateMachine sm, string name, AnimationClip clip)
    {
        var st = sm.AddState(name);
        st.motion = clip;
        st.writeDefaultValues = false;
        return st;
    }

    static void AddExit(AnimatorState from, AnimatorState to, float exitTime, float duration = 0.15f)
    {
        var t = from.AddTransition(to);
        t.hasExitTime = true;
        t.exitTime = exitTime;
        t.duration = duration;
        t.canTransitionToSelf = false;
    }

    static AnimationClip LoadClip(string path, string clipName)
    {
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            if (sub is AnimationClip c && !c.name.StartsWith("__preview__") && c.name == clipName)
                return c;
        // Fallback: first non-preview clip
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(path))
            if (sub is AnimationClip c && !c.name.StartsWith("__preview__"))
                return c;
        return null;
    }
}
#endif
