using UnityEngine;
using UnityEditor;

public static class UpgradeVintageControlsMaterials
{
    [MenuItem("Tools/Mission Focus/Upgrade Vintage Controls to URP")]
    public static void Run()
    {
        var lit   = Shader.Find("Universal Render Pipeline/Lit");
        var unlit = Shader.Find("Universal Render Pipeline/Unlit");
        string root = "Assets/Megapoly.Art/Vintage Controls/Materials/";
        int count = 0;

        string[] litNames = { "Main", "Main Offset 1", "Main Offset 2", "Main Offset 3", "Scene White" };
        string[] unlitNames = { "Screen", "Scene Emission", "Emission Red", "Emission Green", "Emission Blue", "Emission Yellow" };

        foreach (var n in litNames)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(root + n + ".mat");
            if (mat == null) continue;
            mat.shader = lit;
            EditorUtility.SetDirty(mat);
            count++;
        }

        foreach (var n in unlitNames)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(root + n + ".mat");
            if (mat == null) continue;
            mat.shader = unlit;
            EditorUtility.SetDirty(mat);
            count++;
        }

        // Retro CRT green on the screen face
        var screen = AssetDatabase.LoadAssetAtPath<Material>(root + "Screen.mat");
        if (screen != null) screen.color = new Color(0.1f, 0.85f, 0.2f);

        // Amber on yellow emission
        var amber = AssetDatabase.LoadAssetAtPath<Material>(root + "Emission Yellow.mat");
        if (amber != null) amber.color = new Color(1f, 0.65f, 0f);

        // Bright green emission
        var green = AssetDatabase.LoadAssetAtPath<Material>(root + "Emission Green.mat");
        if (green != null) green.color = new Color(0f, 1f, 0.3f);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[MissionFocus] Upgraded " + count + " Vintage Controls materials to URP.");
    }
}
