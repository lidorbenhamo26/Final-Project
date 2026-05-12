using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor menu: places the Working Memory main screen in the active scene.
///
/// Source order (first found wins):
///   1. Meshy-generated:  Assets/Models/MeshyProps/MainScreen/MainScreen.fbx
///   2. Vintage Controls: Assets/Megapoly.Art/Vintage Controls/Prefabs/Monitor_Big.prefab
///
/// Position: north wall (+Z), mounted high, facing the room center.
/// The MainScreenDisplay component is added so WorkingMemoryTask can drive it.
/// </summary>
public class PlaceMainScreen
{
    private const string MeshyPath   = "Assets/Models/MeshyProps/MainScreen/MainScreen.fbx";
    private const string FallbackPath = "Assets/Megapoly.Art/Vintage Controls/Prefabs/Monitor_Big.prefab";
    private const string ScreenName = "MainScreen";

    [MenuItem("Tools/Mission Focus/Place Main Screen")]
    public static void Place()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Place Main Screen",
                "Cannot place the main screen while Unity is in Play mode.\n\n" +
                "Exit Play mode first, run this menu, then re-enter Play.",
                "OK");
            return;
        }

        // Remove any existing main screen first so re-running is idempotent.
        GameObject existing = GameObject.Find(ScreenName);
        if (existing != null)
            Undo.DestroyObjectImmediate(existing);

        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(MeshyPath);
        string usedPath = MeshyPath;
        if (source == null)
        {
            source = AssetDatabase.LoadAssetAtPath<GameObject>(FallbackPath);
            usedPath = FallbackPath;
        }
        if (source == null)
        {
            EditorUtility.DisplayDialog("Place Main Screen",
                "Neither MainScreen.fbx (Meshy) nor Monitor_Big.prefab were found.\n\n" +
                "Expected one of:\n• " + MeshyPath + "\n• " + FallbackPath,
                "OK");
            return;
        }

        GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        Undo.RegisterCreatedObjectUndo(go, "Place MainScreen");
        go.name = ScreenName;

        // Free-standing in the room, north of the player spawn (~z=-2), high
        // enough to read clearly, facing south toward the room center.
        go.transform.position = new Vector3(0f, 3f, 3.5f);
        go.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
        go.transform.localScale = new Vector3(2f, 2f, 2f);

        // Attach the display driver.
        if (go.GetComponent<MainScreenDisplay>() == null)
            go.AddComponent<MainScreenDisplay>();

        Selection.activeGameObject = go;
        SceneView.lastActiveSceneView?.FrameSelected();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();

        Debug.Log("[PlaceMainScreen] Placed '" + ScreenName + "' using: " + usedPath +
                  " at " + go.transform.position + ". MainScreenDisplay attached.");
    }

    [MenuItem("Tools/Mission Focus/Remove Main Screen")]
    public static void Remove()
    {
        GameObject existing = GameObject.Find(ScreenName);
        if (existing == null)
        {
            Debug.Log("[PlaceMainScreen] No '" + ScreenName + "' to remove.");
            return;
        }
        Undo.DestroyObjectImmediate(existing);

        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log("[PlaceMainScreen] Removed '" + ScreenName + "' and saved scene.");
        }
        else
        {
            Debug.LogWarning("[PlaceMainScreen] Removed '" + ScreenName + "' from the running session, " +
                             "but the saved scene still contains it. Exit Play mode and re-run this menu " +
                             "to make the removal permanent.");
        }
    }
}
