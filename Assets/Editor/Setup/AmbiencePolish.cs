using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Agent 4 — Audio/FX/Polish layer.
/// Builds a URP VolumeProfile, ambient particle FX, fog, and ambient audio sources.
/// Idempotent: tear down existing roots and rebuild on every run.
/// Menu: Setup/4 - Add Ambience & FX
/// </summary>
public static class AmbiencePolish
{
    const string SETTINGS_DIR  = "Assets/Settings";
    const string PROFILE_PATH  = "Assets/Settings/SpaceStation_VolumeProfile.asset";

    const string FX_VOLUME_ROOT = "FX_VolumeRoot";
    const string FX_ROOT        = "FX_Root";
    const string AMBIENCE_ROOT  = "Ambience_Root";

    [MenuItem("Setup/4 - Add Ambience & FX")]
    public static void Run()
    {
        EnsureSettingsFolder();
        var profile = EnsureVolumeProfile();

        TearDown();

        BuildVolume(profile);
        BuildParticles();
        BuildFog();
        BuildAmbientAudio();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[AmbiencePolish] Volume profile, particles, fog, and ambient audio set up.");
    }

    // ─── Volume Profile ───────────────────────────────────────────────────────
    static void EnsureSettingsFolder()
    {
        if (!AssetDatabase.IsValidFolder(SETTINGS_DIR))
            AssetDatabase.CreateFolder("Assets", "Settings");
    }

    static VolumeProfile EnsureVolumeProfile()
    {
        var existing = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PROFILE_PATH);
        if (existing != null)
        {
            ApplyOverrides(existing);
            EditorUtility.SetDirty(existing);
            AssetDatabase.SaveAssets();
            return existing;
        }

        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, PROFILE_PATH);
        ApplyOverrides(profile);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log($"[AmbiencePolish] Created VolumeProfile at {PROFILE_PATH}");
        return profile;
    }

    static void ApplyOverrides(VolumeProfile profile)
    {
        // Bloom
        if (!profile.TryGet<Bloom>(out var bloom))
            bloom = profile.Add<Bloom>(true);
        bloom.intensity.Override(1.5f);
        bloom.threshold.Override(0.9f);
        bloom.scatter.Override(0.6f);
        bloom.tint.Override(HexColor("#A8D8FF"));
        bloom.highQualityFiltering.Override(true);

        // Vignette
        if (!profile.TryGet<Vignette>(out var vignette))
            vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.3f);
        vignette.smoothness.Override(0.4f);
        vignette.color.Override(HexColor("#000000"));

        // Chromatic Aberration
        if (!profile.TryGet<ChromaticAberration>(out var chroma))
            chroma = profile.Add<ChromaticAberration>(true);
        chroma.intensity.Override(0.2f);

        // Color Adjustments
        if (!profile.TryGet<ColorAdjustments>(out var color))
            color = profile.Add<ColorAdjustments>(true);
        color.postExposure.Override(0.0f);
        color.contrast.Override(10f);
        color.saturation.Override(-15f);

        // Tonemapping
        if (!profile.TryGet<Tonemapping>(out var tone))
            tone = profile.Add<Tonemapping>(true);
        tone.mode.Override(TonemappingMode.Neutral);
    }

    // ─── Idempotency ──────────────────────────────────────────────────────────
    static void TearDown()
    {
        DestroyByName(FX_VOLUME_ROOT);
        DestroyByName(FX_ROOT);
        DestroyByName(AMBIENCE_ROOT);
    }

    static void DestroyByName(string name)
    {
        var go = GameObject.Find(name);
        if (go != null) Object.DestroyImmediate(go);
    }

    // ─── Volume GameObject ────────────────────────────────────────────────────
    static void BuildVolume(VolumeProfile profile)
    {
        var go = new GameObject(FX_VOLUME_ROOT);
        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.weight = 1f;
        vol.priority = 0f;
        vol.sharedProfile = profile;
    }

    // ─── Particle Systems ─────────────────────────────────────────────────────
    static void BuildParticles()
    {
        var fxRoot = new GameObject(FX_ROOT);
        fxRoot.transform.position = Vector3.zero;

        BuildDustMotes(fxRoot.transform);
        BuildEngineSteam(fxRoot.transform);
    }

    static void BuildDustMotes(Transform parent)
    {
        var go = new GameObject("DustMotes");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 3f, 0f);

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 12f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.startColor = new Color(0.85f, 0.92f, 1f, 0.6f);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        var emission = ps.emission;
        emission.rateOverTime = 30f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(20f, 4f, 20f);

        var velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        velocity.y = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        velocity.z = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);

        // Twinkle: size over lifetime curve (rises then falls)
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var twinkle = new AnimationCurve();
        twinkle.AddKey(0f, 0.2f);
        twinkle.AddKey(0.25f, 1.0f);
        twinkle.AddKey(0.5f, 0.6f);
        twinkle.AddKey(0.75f, 1.0f);
        twinkle.AddKey(1f, 0.0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, twinkle);

        // Fade in/out via color over lifetime
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(new Color(0.85f, 0.92f, 1f), 0f),
                new GradientColorKey(new Color(1f, 1f, 1f),       0.5f),
                new GradientColorKey(new Color(0.85f, 0.92f, 1f), 1f),
            },
            new[] {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(0.7f, 0.2f),
                new GradientAlphaKey(0.7f, 0.8f),
                new GradientAlphaKey(0f,   1f),
            });
        colorOverLife.color = grad;

        // Additive shader
        renderer.material = MakeAdditiveParticleMaterial("Mat_DustMotes");
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static void BuildEngineSteam(Transform parent)
    {
        var go = new GameObject("EngineSteam");
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(0f, 0f, 16f);

        var ps = go.AddComponent<ParticleSystem>();
        var renderer = go.GetComponent<ParticleSystemRenderer>();

        var main = ps.main;
        main.duration = 5f;
        main.loop = true;
        main.startLifetime = 2f;
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startColor = new Color(1f, 1f, 1f, 0.9f);
        main.gravityModifier = -0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.rateOverTime = 5f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 12f;
        shape.radius = 0.15f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // point upward (+Y)

        // Size grows over lifetime (puffs expand)
        var sizeOverLife = ps.sizeOverLifetime;
        sizeOverLife.enabled = true;
        var growth = new AnimationCurve();
        growth.AddKey(0f, 0.5f);
        growth.AddKey(1f, 2.0f);
        sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, growth);

        // White → transparent
        var colorOverLife = ps.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f),
            },
            new[] {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(0.6f, 0.2f),
                new GradientAlphaKey(0f,   1f),
            });
        colorOverLife.color = grad;

        renderer.material = MakeAdditiveParticleMaterial("Mat_EngineSteam");
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
    }

    static Material MakeAdditiveParticleMaterial(string name)
    {
        // Try URP particle additive, fall back to legacy.
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        var mat = new Material(shader) { name = name };

        // Configure additive blend on URP particle shader if available
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);   // Transparent
        if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend", 1f);     // Additive
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        if (mat.HasProperty("_ZWrite"))   mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;

        return mat;
    }

    // ─── Fog ──────────────────────────────────────────────────────────────────
    static void BuildFog()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogColor = HexColor("#0E1A2B");
        RenderSettings.fogDensity = 0.015f;
    }

    // ─── Ambient Audio ────────────────────────────────────────────────────────
    static void BuildAmbientAudio()
    {
        var root = new GameObject(AMBIENCE_ROOT);
        root.tag = "Untagged";

        AudioClip hum = Resources.Load<AudioClip>("Audio/SpaceStationHum");

        // Primary ambient hum
        var hum2D = new GameObject("Ambience_Hum");
        hum2D.transform.SetParent(root.transform, false);
        var sourceHum = hum2D.AddComponent<AudioSource>();
        sourceHum.loop = true;
        sourceHum.playOnAwake = true;
        sourceHum.spatialBlend = 0f;
        sourceHum.volume = 0.4f;
        sourceHum.pitch = 1f;
        sourceHum.clip = hum;

        // Secondary low-frequency rumble
        var rumble = new GameObject("Ambience_Rumble");
        rumble.transform.SetParent(root.transform, false);
        var sourceRumble = rumble.AddComponent<AudioSource>();
        sourceRumble.loop = true;
        sourceRumble.playOnAwake = true;
        sourceRumble.spatialBlend = 0f;
        sourceRumble.volume = 0.2f;
        sourceRumble.pitch = 0.6f;
        sourceRumble.clip = hum; // re-uses same clip if available; otherwise null placeholder

        if (hum == null)
            Debug.LogWarning("[AmbiencePolish] No AudioClip at Resources/Audio/SpaceStationHum — sources left clip-less for later wiring.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    static Color HexColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        return Color.white;
    }
}
