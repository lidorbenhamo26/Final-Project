using UnityEngine;
using UnityEditor;
using Unity.AI.Navigation;

public class PlaceScifiEnvironment
{
    const string PROPS  = "Assets/Dzeruza/MinimalScifiPack/3DModels/Props/Prefabs/";
    const string FLOOR  = "Assets/Dzeruza/MinimalScifiPack/3DModels/Floor/Prefabs/";
    const string WALLS  = "Assets/Dzeruza/MinimalScifiPack/3DModels/Walls/Prefabs/";

    [MenuItem("Tools/Mission Focus/Place Scifi Environment")]
    public static void Run()
    {
        // ── 1. Remove old generic crates ─────────────────────────────────────
        foreach (var n in new[] { "Crate_A", "Crate_B", "Crate_C", "Crate_D", "Crate_E_Top" })
        {
            var go = GameObject.Find(n);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }

        // ── 2. Floor detail tiles (2×2 grid covering ~16×16 m) ───────────────
        Place(FLOOR + "SM_Trim_Floor_8x8M_A1.prefab", new Vector3(-4, 0.02f, -4), 0);
        Place(FLOOR + "SM_Trim_Floor_8x8M_A2.prefab", new Vector3( 4, 0.02f, -4), 0);
        Place(FLOOR + "SM_Trim_Floor_8x8M_A3.prefab", new Vector3(-4, 0.02f,  4), 0);
        Place(FLOOR + "SM_Trim_Floor_8x8M_A4.prefab", new Vector3( 4, 0.02f,  4), 0,
              StaticEditorFlags.ContributeGI);

        // ── 3. Central obstacle cluster (scattered, no perfect grid) ─────────
        PlaceStatic(PROPS + "SM_Trim_L_Container1.prefab", new Vector3(-1.5f, 0,  0.5f),  15f);
        PlaceStatic(PROPS + "SM_Trim_L_Container2.prefab", new Vector3( 1.0f, 0, -1.5f), -30f);
        PlaceStatic(PROPS + "SM_Box1.prefab",               new Vector3(-0.5f, 0, -2.8f),  45f);
        PlaceStatic(PROPS + "SM_Box1.prefab",               new Vector3( 2.6f, 0,  0.8f), -15f);
        PlaceStatic(PROPS + "SM_Box1.prefab",               new Vector3(-2.6f, 0,  1.8f),  20f);
        PlaceStatic(PROPS + "SM_Canister1.prefab",          new Vector3( 1.5f, 0,  2.2f),   0f);

        // ── 4. Wall trim on 4 cardinal station walls ─────────────────────────
        PlaceStatic(WALLS + "SM_Trim_Wall_3x8M_B1.prefab", new Vector3( 7.85f, 0,  0),  270f);
        PlaceStatic(WALLS + "SM_Trim_Wall_3x8M_B1.prefab", new Vector3(-7.85f, 0,  0),   90f);
        PlaceStatic(WALLS + "SM_Trim_Wall_3x8M_B1.prefab", new Vector3( 0,     0,  7.85f), 180f);
        PlaceStatic(WALLS + "SM_Trim_Wall_3x8M_B1.prefab", new Vector3( 0,     0, -7.85f),  0f);

        // ── 5. Station accent canisters (beside each console, outside trigger) 
        PlaceNamed(PROPS + "SM_Canister1.prefab", "Canister_Engine",      new Vector3( 5.8f, 0,  0.7f),   0f);
        PlaceNamed(PROPS + "SM_Canister1.prefab", "Canister_Navigation",  new Vector3(-0.7f, 0,  5.8f),   0f);
        PlaceNamed(PROPS + "SM_Canister1.prefab", "Canister_Comms",       new Vector3(-5.8f, 0, -0.7f),   0f);
        PlaceNamed(PROPS + "SM_Canister1.prefab", "Canister_LifeSupport", new Vector3( 0.7f, 0, -5.8f),   0f);

        // ── 6. Rebake NavMesh ────────────────────────────────────────────────
        var floor = GameObject.Find("Floor");
        if (floor != null)
        {
            var surface = floor.GetComponent<NavMeshSurface>();
            if (surface != null)
            {
                surface.BuildNavMesh();
                Debug.Log("[MissionFocus] NavMesh rebaked.");
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("[MissionFocus] Scifi environment placed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    static GameObject Place(string path, Vector3 pos, float rotY,
        StaticEditorFlags flags = StaticEditorFlags.ContributeGI)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null) { Debug.LogWarning("[MF] Missing prefab: " + path); return null; }
        var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(inst, "Place SciFi Prop");
        inst.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, rotY, 0));
        GameObjectUtility.SetStaticEditorFlags(inst, flags);
        return inst;
    }

    static GameObject PlaceStatic(string path, Vector3 pos, float rotY)
#pragma warning disable CS0618 // NavigationStatic still functions; full migration to NavMeshBuilder.CollectSources is out of scope here
        => Place(path, pos, rotY,
            StaticEditorFlags.NavigationStatic | StaticEditorFlags.ContributeGI);
#pragma warning restore CS0618

    static void PlaceNamed(string path, string name, Vector3 pos, float rotY)
    {
        var go = PlaceStatic(path, pos, rotY);
        if (go != null) go.name = name;
    }
}
