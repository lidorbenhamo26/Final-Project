using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// One-shot editor tool: finds the four TaskStations in the active scene and
/// assigns them to GameManager's serialized fields, then saves the scene.
///
/// Safe to re-run — sets references to the current scene objects each time.
/// </summary>
public class WireGameManager
{
    [MenuItem("Tools/Mission Focus/Wire GameManager Stations")]
    public static void Wire()
    {
        var gmGO = GameObject.Find("GameManager");
        if (gmGO == null)
        {
            Debug.LogError("[WireGameManager] No GameManager GameObject found in scene.");
            return;
        }
        var gm = gmGO.GetComponent<GameManager>();
        if (gm == null)
        {
            Debug.LogError("[WireGameManager] GameManager component missing.");
            return;
        }

        TaskStation engine = FindStation("EngineStation");
        TaskStation nav    = FindStation("NavigationStation");
        TaskStation comms  = FindStation("CommsStation");
        TaskStation ls     = FindStation("LifeSupportStation");

        var so = new SerializedObject(gm);
        SetRef(so, "engineStation",      engine);
        SetRef(so, "navigationStation",  nav);
        SetRef(so, "commsStation",       comms);
        SetRef(so, "lifeSupportStation", ls);
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(gm);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[WireGameManager] Wired stations -> " +
                  "engine=" + (engine != null) + ", " +
                  "nav="    + (nav    != null) + ", " +
                  "comms="  + (comms  != null) + ", " +
                  "ls="     + (ls     != null));
    }

    private static TaskStation FindStation(string goName)
    {
        var go = GameObject.Find(goName);
        if (go == null)
        {
            Debug.LogWarning("[WireGameManager] Station GameObject not found: " + goName);
            return null;
        }
        var ts = go.GetComponent<TaskStation>();
        if (ts == null)
            Debug.LogWarning("[WireGameManager] " + goName + " has no TaskStation component.");
        return ts;
    }

    private static void SetRef(SerializedObject so, string fieldName, Object value)
    {
        var p = so.FindProperty(fieldName);
        if (p == null)
        {
            Debug.LogWarning("[WireGameManager] Field not found on GameManager: " + fieldName);
            return;
        }
        p.objectReferenceValue = value;
    }
}
