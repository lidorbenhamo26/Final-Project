using UnityEngine;
using UnityEditor;

public class UpgradeScifiPackMaterials
{
    [MenuItem("Tools/Mission Focus/Upgrade Scifi Pack Materials to URP")]
    public static void Run()
    {
        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) { Debug.LogError("[MF] URP/Lit shader not found."); return; }

        string[] guids = AssetDatabase.FindAssets("t:Material",
            new[] { "Assets/Dzeruza/MinimalScifiPack" });

        int count = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            if (mat.shader.name.StartsWith("Universal Render Pipeline")) continue;

            // Preserve albedo color and texture before swapping shader
            Color albedo = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
            Texture mainTex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;

            mat.shader = urpLit;
            mat.SetColor("_BaseColor", albedo);
            if (mainTex != null) mat.SetTexture("_BaseMap", mainTex);

            EditorUtility.SetDirty(mat);
            count++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[MissionFocus] Upgraded {count} Scifi Pack materials to URP/Lit.");
    }
}
