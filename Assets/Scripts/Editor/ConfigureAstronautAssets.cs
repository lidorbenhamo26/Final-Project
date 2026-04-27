#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Text;

public static class ConfigureAstronautAssets
{
    const string CharPath = "Assets/Characters/Astronaut/Models/Astronaut_Character.fbx";
    static readonly string[] AnimPaths = {
        "Assets/Characters/Astronaut/Models/Astronaut_Anim_Idle.fbx",
        "Assets/Characters/Astronaut/Models/Astronaut_Anim_Walk.fbx",
        "Assets/Characters/Astronaut/Models/Astronaut_Anim_Run.fbx",
        "Assets/Characters/Astronaut/Models/Astronaut_Anim_Jump.fbx",
        "Assets/Characters/Astronaut/Models/Astronaut_Anim_Wave.fbx",
        "Assets/Characters/Astronaut/Models/Astronaut_Anim_Alert.fbx",
    };
    static readonly bool[] Loops = { true, true, true, false, false, false };
    static readonly string[] CleanNames = { "Idle", "Walk", "Run", "Jump", "Wave", "Alert" };

    [MenuItem("Tools/Astronaut/Configure Importers")]
    public static void Run()
    {
        var sb = new StringBuilder();

        // Character FBX
        var charImp = (ModelImporter)AssetImporter.GetAtPath(CharPath);
        charImp.animationType = ModelImporterAnimationType.Generic;
        charImp.materialImportMode = ModelImporterMaterialImportMode.None;
        charImp.optimizeGameObjects = false;
        charImp.importAnimation = false;
        charImp.SaveAndReimport();

        Avatar charAvatar = null;
        foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(CharPath))
            if (sub is Avatar a) { charAvatar = a; break; }
        sb.AppendLine("character avatar: " + (charAvatar != null ? charAvatar.name : "NULL"));

        // Animation FBXs
        for (int i = 0; i < AnimPaths.Length; i++)
        {
            string p = AnimPaths[i];
            var imp = (ModelImporter)AssetImporter.GetAtPath(p);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.materialImportMode = ModelImporterMaterialImportMode.None;
            imp.importAnimation = true;
            imp.optimizeGameObjects = false;
            if (charAvatar != null) imp.sourceAvatar = charAvatar;

            var clips = imp.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                for (int c = 0; c < clips.Length; c++)
                {
                    clips[c].name = CleanNames[i];
                    clips[c].loopTime = Loops[i];
                }
                imp.clipAnimations = clips;
            }
            imp.SaveAndReimport();

            AnimationClip clip = null;
            foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(p))
                if (sub is AnimationClip ac && !ac.name.StartsWith("__preview__")) { clip = ac; break; }
            sb.AppendLine($"{Path.GetFileName(p)} -> clip='{(clip != null ? clip.name : "NONE")}' loop={Loops[i]} avatar={(imp.sourceAvatar != null ? imp.sourceAvatar.name : "NULL")}");
        }

        // Textures
        Configure("Assets/Characters/Astronaut/Materials/Textures/Astronaut_Normal.png",   t => { t.textureType = TextureImporterType.NormalMap; }, "normal: NormalMap", sb);
        Configure("Assets/Characters/Astronaut/Materials/Textures/Astronaut_Metallic.png", t => { t.sRGBTexture = false; },                          "metallic: linear",   sb);
        Configure("Assets/Characters/Astronaut/Materials/Textures/Astronaut_Roughness.png",t => { t.sRGBTexture = false; },                          "roughness: linear",  sb);

        Debug.Log("[Astronaut Config]\n" + sb);
    }

    static void Configure(string path, System.Action<TextureImporter> setup, string label, StringBuilder sb)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null) { sb.AppendLine(label + " (importer NOT FOUND at " + path + ")"); return; }
        setup(imp);
        imp.SaveAndReimport();
        sb.AppendLine(label);
    }
}
#endif
