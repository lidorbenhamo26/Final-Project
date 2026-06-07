using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;
using System.Collections.Generic;

public class OptimizeForAssessment
{
    [MenuItem("Tools/Mission Focus/Optimize Scene For Assessment")]
    public static void Run()
    {
        RepositionCrateIslands();
        RepositionObstacleSpots();
        FixAmbientAndFillLight();
        LightenCargoMaterial();
        RebakeNavMesh();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();
        Debug.Log("[MissionFocus] Scene optimized for assessment.");
    }

    // ── 1. Move obstacles into 3 diagonal islands, clear all 4 cardinal paths ─
    static void RepositionCrateIslands()
    {
        // Island NE (+x, +z) — between Engine (E) and Navigation (N)
        Move("SM_Trim_L_Container1",  0, new Vector3( 3.8f, 0f,  3.2f), 225f);

        // Island NW (-x, +z) — between Navigation (N) and Comms (W)
        Move("SM_Trim_L_Container2",  0, new Vector3(-3.8f, 0f,  3.2f), 135f);
        Move("SM_Canister1",          0, new Vector3(-3.0f, 0f,  4.5f),  20f);

        // Island SW (-x, -z) — between Comms (W) and LifeSupport (S)
        Move("SM_Box1", 0, new Vector3(-4.0f, 0f, -3.2f), 200f);
        Move("SM_Box1", 1, new Vector3(-3.0f, 0f, -4.5f),  30f);

        // One lone box NE cluster (small companion to Container1)
        Move("SM_Box1", 2, new Vector3( 3.0f, 0f,  4.5f),  15f);

        // Verify paths are clear (log only)
        Debug.Log("[MF] Crates repositioned. Cardinal paths (±X, ±Z through origin) are clear.");
    }

    // Move the Nth root-level object with the given name
    static void Move(string objName, int index, Vector3 pos, float rotY)
    {
        var list = FindAllRootNamed(objName);
        if (index >= list.Count)
        {
            Debug.LogWarning($"[MF] '{objName}' index {index} not found (found {list.Count}).");
            return;
        }
        var t = list[index].transform;
        Undo.RecordObject(t, "Move Crate");
        t.position = pos;
        t.rotation = Quaternion.Euler(0f, rotY, 0f);
    }

    static List<GameObject> FindAllRootNamed(string name)
    {
        var result = new List<GameObject>();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Exclude))
            if (go.transform.parent == null && go.name == name)
                result.Add(go);
        return result;
    }

    // ── 2. Aim obstacle spotlights at new island positions ────────────────────
    static void RepositionObstacleSpots()
    {
        // Remove old crate spots and recreate at island positions
        foreach (var n in new[] { "Spot_Crates_A", "Spot_Crates_B", "Spot_Crates_C" })
        {
            var old = GameObject.Find(n);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }

        var islandSpots = new (string name, Vector3 pos, Vector3 aimAt)[]
        {
            ("Spot_Crates_A", new Vector3( 3.5f, 5.5f,  3.5f), new Vector3( 3.5f, 0f,  3.5f)),
            ("Spot_Crates_B", new Vector3(-3.5f, 5.5f,  3.5f), new Vector3(-3.5f, 0f,  3.5f)),
            ("Spot_Crates_C", new Vector3(-3.5f, 5.5f, -3.5f), new Vector3(-3.5f, 0f, -3.5f)),
        };

        foreach (var s in islandSpots)
        {
            var go = new GameObject(s.name);
            Undo.RegisterCreatedObjectUndo(go, "Island Spotlight");
            go.transform.position = s.pos;
            go.transform.LookAt(s.aimAt);
            go.transform.Rotate(90f, 0f, 0f);

            var light = go.AddComponent<Light>();
            light.type           = LightType.Spot;
            light.color          = new Color(1.0f, 0.90f, 0.75f);  // warm white
            light.intensity      = 5f;
            light.range          = 8f;
            light.spotAngle      = 50f;
            light.innerSpotAngle = 22f;
            light.shadows        = LightShadows.Soft;
            light.shadowStrength = 0.6f;
        }
    }

    // ── 3. Ambient + center ceiling fill light ────────────────────────────────
    static void FixAmbientAndFillLight()
    {
        // Raise ambient from near-black to dim but visible
        RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight     = new Color(0.06f, 0.065f, 0.09f);
        RenderSettings.ambientIntensity = 1f;

        // Remove existing fill if re-running
        var oldFill = GameObject.Find("Fill_Center");
        if (oldFill != null) Undo.DestroyObjectImmediate(oldFill);

        // Center ceiling point light — large, soft, low intensity
        var go = new GameObject("Fill_Center");
        Undo.RegisterCreatedObjectUndo(go, "Fill Light");
        go.transform.position = new Vector3(0f, 6f, 0f);

        var light = go.AddComponent<Light>();
        light.type           = LightType.Point;
        light.color          = new Color(0.88f, 0.92f, 1.0f);   // cool white
        light.intensity      = 1.8f;
        light.range          = 18f;
        light.shadows        = LightShadows.Soft;
        light.shadowStrength = 0.25f;   // very soft — fill, not key light

        // Boost station spotlights' inner cone so approach areas are lit
        foreach (var n in new[] { "Spot_Engine", "Spot_Navigation", "Spot_Comms", "Spot_LifeSupport" })
        {
            var go2 = GameObject.Find(n);
            if (go2 == null) continue;
            var l = go2.GetComponent<Light>();
            if (l == null) continue;
            Undo.RecordObject(l, "Station Spot Tweak");
            l.spotAngle       = 55f;    // wider cone → lights the approach floor
            l.innerSpotAngle  = 28f;
            l.intensity       = 10f;    // slightly less aggressive than before
            l.range           = 11f;
        }
    }

    // ── 4. Lighten cargo material, add subtle warm emission ──────────────────
    static void LightenCargoMaterial()
    {
        string path = "Assets/Materials/Mat_Cargo.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { Debug.LogWarning("[MF] Mat_Cargo not found."); return; }

        // Lighter albedo so crates are visible in shadow
        mat.SetColor("_BaseColor", new Color(0.42f, 0.36f, 0.28f));

        // Subtle warm emission — acts as a faint rim/self-illumination
        mat.EnableKeyword("_EMISSION");
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        mat.SetColor("_EmissionColor", new Color(0.05f, 0.04f, 0.025f));

        EditorUtility.SetDirty(mat);
    }

    // ── 5. Rebake NavMesh ─────────────────────────────────────────────────────
    static void RebakeNavMesh()
    {
        var floor = GameObject.Find("Floor");
        if (floor == null) { Debug.LogWarning("[MF] Floor not found for NavMesh bake."); return; }
        var surface = floor.GetComponent<NavMeshSurface>();
        if (surface == null) { Debug.LogWarning("[MF] NavMeshSurface not found."); return; }
        surface.BuildNavMesh();
        Debug.Log("[MF] NavMesh rebaked.");
    }
}
