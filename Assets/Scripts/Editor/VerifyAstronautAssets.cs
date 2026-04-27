#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Text;

public static class VerifyAstronautAssets
{
    [MenuItem("Tools/Astronaut/Verify Importers")]
    public static void Run()
    {
        var sb = new StringBuilder();
        string[] paths = {
            "Assets/Characters/Astronaut/Models/Astronaut_Character.fbx",
            "Assets/Characters/Astronaut/Models/Astronaut_Anim_Idle.fbx",
            "Assets/Characters/Astronaut/Models/Astronaut_Anim_Walk.fbx",
            "Assets/Characters/Astronaut/Models/Astronaut_Anim_Run.fbx",
            "Assets/Characters/Astronaut/Models/Astronaut_Anim_Jump.fbx",
            "Assets/Characters/Astronaut/Models/Astronaut_Anim_Wave.fbx",
            "Assets/Characters/Astronaut/Models/Astronaut_Anim_Alert.fbx",
        };
        foreach (var p in paths)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            string clipName = "NONE";
            bool loop = false;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(p))
                if (sub is AnimationClip c && !c.name.StartsWith("__preview__")) { clipName = c.name; loop = c.isLooping; break; }
            string avatarName = "NULL";
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(p))
                if (sub is Avatar a) { avatarName = a.name; break; }
            string srcAv = imp.sourceAvatar != null ? imp.sourceAvatar.name : "(none)";
            sb.Append(System.IO.Path.GetFileNameWithoutExtension(p))
              .Append(" rig=").Append(imp.animationType)
              .Append(" mat=").Append(imp.materialImportMode)
              .Append(" srcAvatar=").Append(srcAv)
              .Append(" embeddedAvatar=").Append(avatarName)
              .Append(" clip=").Append(clipName)
              .Append(" loop=").Append(loop)
              .Append(" | ");
        }
        Debug.Log("[AstroVerify] " + sb);
    }
}
#endif
