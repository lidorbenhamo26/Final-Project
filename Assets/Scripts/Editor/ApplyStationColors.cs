using UnityEngine;
using UnityEditor;
using TMPro;

public class ApplyStationColors
{
    const string MAT = "Assets/Materials/";

    struct Station
    {
        public string name, cardinal, labelKey;
        public Color baseColor, emitHDR, lightColor;
    }

    static readonly Station[] Stations =
    {
        new Station { name="Engine",      cardinal="E", labelKey="ENGINE_TERMINAL",
            baseColor = new Color(0.85f, 0.44f, 0.00f),
            emitHDR   = new Color(3.0f,  1.5f,  0.00f),
            lightColor= new Color(1.0f,  0.55f, 0.00f) },

        new Station { name="Navigation",  cardinal="N", labelKey="NAVIGATION_DECK",
            baseColor = new Color(0.04f, 0.18f, 0.82f),
            emitHDR   = new Color(0.0f,  0.60f, 4.00f),
            lightColor= new Color(0.1f,  0.35f, 1.00f) },

        new Station { name="Comms",       cardinal="W", labelKey="COMMS_HUB",
            baseColor = new Color(0.04f, 0.72f, 0.22f),
            emitHDR   = new Color(0.0f,  4.00f, 1.00f),
            lightColor= new Color(0.1f,  1.00f, 0.30f) },

        new Station { name="LifeSupport", cardinal="S", labelKey="LIFE_SUPPORT",
            baseColor = new Color(0.82f, 0.04f, 0.04f),
            emitHDR   = new Color(4.0f,  0.00f, 0.00f),
            lightColor= new Color(1.0f,  0.10f, 0.10f) },
    };

    [MenuItem("Tools/Mission Focus/Apply Station Colors & Materials")]
    public static void Run()
    {
        if (!System.IO.Directory.Exists(MAT))
            System.IO.Directory.CreateDirectory(MAT);

        // Shared environment & obstacle materials
        var matCargo      = MakeLit("Mat_Cargo",      new Color(0.22f, 0.18f, 0.14f), 0.25f, 0.30f, null);
        var matScifiWall  = MakeLit("Mat_ScifiWall",  new Color(0.62f, 0.65f, 0.68f), 0.55f, 0.65f, null);
        var matScifiFloor = MakeLit("Mat_ScifiFloor", new Color(0.15f, 0.17f, 0.20f), 0.85f, 0.85f, null);

        // Sci-fi pack surfaces
        PaintPrefix("SM_Trim_Wall_3x8M",  matScifiWall);
        PaintPrefix("SM_Trim_Floor_8x8M", matScifiFloor);

        // Cargo obstacles
        PaintRoot("SM_Trim_L_Container1", matCargo);
        PaintRoot("SM_Trim_L_Container2", matCargo);
        PaintPrefix("SM_Box1",      matCargo);
        PaintPrefix("Canister_",   matCargo);
        PaintPrefix("SM_Canister", matCargo);  // obstacle-cluster canister (not station-named)

        // Per-station color themes
        foreach (var s in Stations)
        {
            var consoleMat = MakeLit($"Mat_Console_{s.name}", s.baseColor, 0.5f, 0.45f, s.baseColor * 0.35f);
            var screenMat  = MakeEmissive($"Mat_Screen_{s.name}", new Color(0.02f, 0.02f, 0.03f), s.emitHDR);

            PaintHierarchy($"VintageConsole_{s.name}", consoleMat);
            PaintHierarchy($"Console_{s.name}",        consoleMat);
            PaintHierarchy($"Screen_{s.name}",         screenMat);
            PaintHierarchy($"Monitor_{s.name}",        screenMat);

            SetLabel($"Label_{s.labelKey}", s.baseColor);
            SetLight($"Light_{s.cardinal}", s.lightColor);
        }

        AssetDatabase.SaveAssets();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[MissionFocus] Station colors applied.");
    }

    // ── Material factories ────────────────────────────────────────────────────

    static Material MakeLit(string assetName, Color baseColor, float metallic, float smoothness, Color? emit)
    {
        string path = MAT + assetName + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material mat;
        if (existing != null)
        {
            mat = existing;
        }
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic",   metallic);
        mat.SetFloat("_Smoothness", smoothness);
        if (emit.HasValue && emit.Value.maxColorComponent > 0.001f)
        {
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            mat.SetColor("_EmissionColor", emit.Value);
        }
        else
        {
            mat.DisableKeyword("_EMISSION");
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static Material MakeEmissive(string assetName, Color baseColor, Color emitHDR)
    {
        string path = MAT + assetName + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        Material mat;
        if (existing != null)
        {
            mat = existing;
        }
        else
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(mat, path);
        }
        mat.shader = Shader.Find("Universal Render Pipeline/Lit");
        mat.SetColor("_BaseColor", baseColor);
        mat.SetFloat("_Metallic",   0f);
        mat.SetFloat("_Smoothness", 0.9f);
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        mat.SetColor("_EmissionColor", emitHDR);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ── Scene applicators ─────────────────────────────────────────────────────

    static void PaintHierarchy(string rootName, Material mat)
    {
        var go = GameObject.Find(rootName);
        if (go == null) return;
        foreach (var r in go.GetComponentsInChildren<MeshRenderer>(true))
            ApplyToRenderer(r, mat);
    }

    static void PaintRoot(string exactName, Material mat)
    {
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            if (r.transform.root.gameObject.name == exactName)
                ApplyToRenderer(r, mat);
    }

    static void PaintPrefix(string prefix, Material mat)
    {
        foreach (var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Exclude))
            if (r.transform.root.gameObject.name.StartsWith(prefix))
                ApplyToRenderer(r, mat);
    }

    static void ApplyToRenderer(MeshRenderer r, Material mat)
    {
        Undo.RecordObject(r, "Apply Material");
        var slots = r.sharedMaterials;
        for (int i = 0; i < slots.Length; i++) slots[i] = mat;
        r.sharedMaterials = slots;
    }

    // ── TMP label & light ─────────────────────────────────────────────────────

    static void SetLabel(string goName, Color color)
    {
        var go = GameObject.Find(goName);
        if (go == null) return;
        var tmp = go.GetComponent<TextMeshPro>();
        if (tmp == null) return;
        Undo.RecordObject(tmp, "Label Color");
        tmp.color = color;
        if (tmp.fontSharedMaterial != null)
        {
            var inst = new Material(tmp.fontSharedMaterial);
            inst.EnableKeyword("GLOW_ON");
            inst.SetFloat("_GlowPower", 0.5f);
            inst.SetFloat("_GlowOuter", 0.4f);
            inst.SetColor("_GlowColor", new Color(color.r, color.g, color.b, 0.85f));
            tmp.fontMaterial = inst;
        }
    }

    static void SetLight(string goName, Color color)
    {
        var go = GameObject.Find(goName);
        if (go == null) return;
        var light = go.GetComponent<Light>();
        if (light == null) return;
        Undo.RecordObject(light, "Light Color");
        light.color = color;
    }
}
