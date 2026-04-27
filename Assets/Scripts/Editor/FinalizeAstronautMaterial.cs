#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class FinalizeAstronautMaterial
{
    [MenuItem("Tools/Astronaut/Finalize Material")]
    public static void Run()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Characters/Astronaut/Materials/M_Astronaut.mat");
        if (mat == null) { Debug.LogError("[AstroMat] material not found"); return; }

        // Enable URP/Lit keywords required so the shader actually samples our textures
        mat.EnableKeyword("_NORMALMAP");
        mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_WorkflowMode", 1f); // 1 = Metallic
        mat.SetFloat("_SmoothnessTextureChannel", 0f); // metallic alpha (we set scalar smoothness anyway)
        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssetIfDirty(mat);
        Debug.Log("[AstroMat] keywords enabled: _NORMALMAP, _METALLICSPECGLOSSMAP. baseMap=" + (mat.GetTexture("_BaseMap")!=null) + " bump=" + (mat.GetTexture("_BumpMap")!=null) + " met=" + (mat.GetTexture("_MetallicGlossMap")!=null));
    }
}
#endif
