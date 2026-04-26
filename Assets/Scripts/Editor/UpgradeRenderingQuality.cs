using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class UpgradeRenderingQuality
{
    const string PACK_TEX = "Assets/Dzeruza/MinimalScifiPack/Textures&Materials/";
    const string MAT      = "Assets/Materials/";

    [MenuItem("Tools/Mission Focus/Upgrade Rendering Quality")]
    public static void Run()
    {
        FixPackTextures();
        FixPackMaterials();
        SetAmbientLight();
        PlaceSpotlights();
        UpgradePostProcessing();
        UpgradeShadows();
        BoostMetallicSmoothness();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[MissionFocus] Rendering quality upgraded.");
    }

    // ── 1. Ensure normal map is imported correctly ────────────────────────────
    static void FixPackTextures()
    {
        string normalPath = PACK_TEX + "T_Trim_Walls_Normal.png";
        var imp = AssetImporter.GetAtPath(normalPath) as TextureImporter;
        if (imp != null && imp.textureType != TextureImporterType.NormalMap)
        {
            imp.textureType = TextureImporterType.NormalMap;
            imp.SaveAndReimport();
            Debug.Log("[MF] Re-imported normal map.");
        }
    }

    // ── 2. Plug textures into ScifiWall & ScifiFloor materials ────────────────
    static void FixPackMaterials()
    {
        var albedo  = AssetDatabase.LoadAssetAtPath<Texture2D>(PACK_TEX + "T_Trim_Walls_Color.png");
        var normal  = AssetDatabase.LoadAssetAtPath<Texture2D>(PACK_TEX + "T_Trim_Walls_Normal.png");
        var maskMap = AssetDatabase.LoadAssetAtPath<Texture2D>(PACK_TEX + "T_Trim_Walls_MaskMap.png");

        if (albedo == null)  { Debug.LogWarning("[MF] Missing albedo texture.");  return; }
        if (normal == null)  { Debug.LogWarning("[MF] Missing normal texture.");  return; }
        if (maskMap == null) { Debug.LogWarning("[MF] Missing mask map texture."); return; }

        // Wall trim: light gray tint, full texture detail
        ApplyPackTex(MAT + "Mat_ScifiWall.mat",  albedo, normal, maskMap,
            new Color(0.75f, 0.77f, 0.80f), metallic: 0.9f, smoothness: 0.82f,
            tiling: new Vector2(1f, 1f));

        // Floor tiles: darker tint
        ApplyPackTex(MAT + "Mat_ScifiFloor.mat", albedo, normal, maskMap,
            new Color(0.30f, 0.32f, 0.35f), metallic: 1.0f, smoothness: 0.88f,
            tiling: new Vector2(1f, 1f));

        // Boost smoothness on cargo (subtle sheen)
        UpdateLitMat(MAT + "Mat_Cargo.mat", metallic: 0.35f, smoothness: 0.50f);
    }

    static void ApplyPackTex(string matPath, Texture2D albedo, Texture2D normal,
        Texture2D maskMap, Color tint, float metallic, float smoothness, Vector2 tiling)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) { Debug.LogWarning("[MF] Mat not found: " + matPath); return; }

        mat.SetTexture("_BaseMap",          albedo);
        mat.SetColor  ("_BaseColor",        tint);
        mat.SetTexture("_BumpMap",          normal);
        mat.SetFloat  ("_BumpScale",        1.2f);
        mat.EnableKeyword("_NORMALMAP");

        mat.SetTexture("_MetallicGlossMap", maskMap);
        mat.SetFloat  ("_Metallic",         metallic);
        mat.SetFloat  ("_Smoothness",       smoothness);

        mat.SetTextureScale("_BaseMap", tiling);
        mat.SetTextureScale("_BumpMap", tiling);
        mat.SetTextureScale("_MetallicGlossMap", tiling);

        EditorUtility.SetDirty(mat);
    }

    static void UpdateLitMat(string matPath, float metallic, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null) return;
        mat.SetFloat("_Metallic",   metallic);
        mat.SetFloat("_Smoothness", smoothness);
        EditorUtility.SetDirty(mat);
    }

    // ── 3. Ambient light → near-black for moody base ──────────────────────────
    static void SetAmbientLight()
    {
        Undo.RecordObject(RenderSettings.skybox, "Ambient Light");
        RenderSettings.ambientMode  = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.015f, 0.015f, 0.022f);
        RenderSettings.ambientIntensity = 0f;
        Debug.Log("[MF] Ambient light set to near-black.");
    }

    // ── 4. Dramatic spotlights above consoles & obstacle cluster ─────────────
    static void PlaceSpotlights()
    {
        // Station spots (tinted to match station color)
        var spots = new (string name, Vector3 pos, Vector3 aim, Color col)[]
        {
            ("Spot_Engine",      new Vector3( 7.2f, 5.5f,  0f),    new Vector3( 7.2f, 0f,  0f),    new Color(1.0f, 0.70f, 0.20f)),
            ("Spot_Navigation",  new Vector3( 0f,   5.5f,  7.2f),  new Vector3( 0f,   0f,  7.2f),  new Color(0.40f, 0.60f, 1.0f)),
            ("Spot_Comms",       new Vector3(-7.2f, 5.5f,  0f),    new Vector3(-7.2f, 0f,  0f),    new Color(0.30f, 1.0f, 0.45f)),
            ("Spot_LifeSupport", new Vector3( 0f,   5.5f, -7.2f),  new Vector3( 0f,   0f, -7.2f),  new Color(1.0f, 0.25f, 0.20f)),
            // Obstacle cluster
            ("Spot_Crates_A",    new Vector3(-1.5f, 5.5f, -0.5f),  new Vector3(-1.5f, 0f, -0.5f),  Color.white),
            ("Spot_Crates_B",    new Vector3( 1.5f, 5.5f,  1.0f),  new Vector3( 1.5f, 0f,  1.0f),  Color.white),
        };

        foreach (var s in spots)
        {
            // Remove old if re-running
            var old = GameObject.Find(s.name);
            if (old != null) Undo.DestroyObjectImmediate(old);

            var go   = new GameObject(s.name);
            Undo.RegisterCreatedObjectUndo(go, "Spotlight");
            go.transform.position = s.pos;
            go.transform.LookAt(s.aim);
            go.transform.Rotate(90f, 0f, 0f);   // Unity spot points down local -Z

            var light = go.AddComponent<Light>();
            light.type        = LightType.Spot;
            light.color       = s.col;
            light.intensity   = 12f;
            light.range       = 9f;
            light.spotAngle   = 38f;
            light.innerSpotAngle = 18f;
            light.shadows     = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias  = 0.04f;
            light.shadowNormalBias = 0.4f;
        }
    }

    // ── 5. Post-processing: bloom, tonemapping, vignette, color grading ───────
    static void UpgradePostProcessing()
    {
        var volGo = GameObject.Find("Global Volume");
        if (volGo == null) { Debug.LogWarning("[MF] Global Volume not found."); return; }
        var vol = volGo.GetComponent<Volume>();
        if (vol == null)    { Debug.LogWarning("[MF] Volume component not found."); return; }

        var profile = vol.sharedProfile;
        if (profile == null) { Debug.LogWarning("[MF] No shared profile."); return; }

        // Bloom — bright, tight, high-quality
        if (profile.TryGet<Bloom>(out var bloom))
        {
            bloom.intensity.Override(1.4f);
            bloom.threshold.Override(0.8f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(true);
        }

        // Tonemapping → ACES for cinematic contrast
        if (!profile.TryGet<Tonemapping>(out var tone))
            tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.ACES);

        // Color Adjustments
        if (!profile.TryGet<ColorAdjustments>(out var ca))
            ca = profile.Add<ColorAdjustments>(true);
        ca.contrast.Override(18f);
        ca.saturation.Override(12f);
        ca.colorFilter.Override(new Color(0.98f, 0.97f, 1.00f));  // slight cool tint

        // Vignette
        if (!profile.TryGet<Vignette>(out var vig))
            vig = profile.Add<Vignette>(true);
        vig.intensity.Override(0.38f);
        vig.smoothness.Override(0.55f);
        vig.rounded.Override(true);

        // Chromatic Aberration (subtle)
        if (!profile.TryGet<ChromaticAberration>(out var ca2))
            ca2 = profile.Add<ChromaticAberration>(true);
        ca2.intensity.Override(0.08f);

        EditorUtility.SetDirty(profile);
        Debug.Log("[MF] Post-processing updated.");
    }

    // ── 6. Soft shadows on all lights ─────────────────────────────────────────
    static void UpgradeShadows()
    {
        foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.shadows == LightShadows.None) continue;
            Undo.RecordObject(light, "Soft Shadows");
            light.shadows = LightShadows.Soft;
        }

        // Also darken the directional light a touch for drama
        var dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
        if (dirLight != null)
        {
            Undo.RecordObject(dirLight, "Directional Intensity");
            dirLight.intensity = 0.4f;
            dirLight.shadows   = LightShadows.Soft;
        }
    }

    // ── 7. Boost smoothness on station consoles for metallic reflections ───────
    static void BoostMetallicSmoothness()
    {
        foreach (var suffix in new[] { "Engine", "Navigation", "Comms", "LifeSupport" })
        {
            UpdateLitMat($"{MAT}Mat_Console_{suffix}.mat", metallic: 0.65f, smoothness: 0.62f);
        }
    }
}
