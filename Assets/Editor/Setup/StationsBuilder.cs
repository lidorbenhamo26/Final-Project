#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Agent 2 — Stations Designer.
/// Builds 4 mission-task station prefabs (Engine / Navigation / Comms / LifeSupport)
/// and provides a one-shot scene placement helper.
///
/// Menu items:
///   Setup/2a - Create Station Prefabs   → builds & saves the 4 prefabs (idempotent)
///   Setup/2b - Place Stations in Scene  → instantiates the prefabs at the 4 cardinals
///                                         under a Stations_Root (idempotent)
/// </summary>
public static class StationsBuilder
{
    // ── Paths ────────────────────────────────────────────────────────────────
    const string PrefabFolder   = "Assets/Prefabs/Stations";
    const string ConsolePrefab  = "Assets/Megapoly.Art/Vintage Controls/Prefabs/ControlTable_1.prefab";

    // ── Station data ─────────────────────────────────────────────────────────
    struct StationDef
    {
        public string Name;        // prefab + root name (no suffix)
        public Color Color;        // theme color
        public Type TaskType;      // matching MissionTask subclass
        public Vector3 ScenePos;   // placement position for menu 2b
    }

    static readonly StationDef[] Stations = new StationDef[]
    {
        new StationDef {
            Name = "EngineStation",
            Color = HexToColor("#FF3B30"),
            TaskType = typeof(EngineTask),
            ScenePos = new Vector3(0f, 0f, 16f),    // north
        },
        new StationDef {
            Name = "NavigationStation",
            Color = HexToColor("#007AFF"),
            TaskType = typeof(NavigationTask),
            ScenePos = new Vector3(16f, 0f, 0f),    // east
        },
        new StationDef {
            Name = "CommsStation",
            Color = HexToColor("#FFCC00"),
            TaskType = typeof(CommsTask),
            ScenePos = new Vector3(-16f, 0f, 0f),   // west
        },
        new StationDef {
            Name = "LifeSupportStation",
            Color = HexToColor("#34C759"),
            TaskType = typeof(LifeSupportTask),
            ScenePos = new Vector3(0f, 0f, -16f),   // south
        },
    };

    // =========================================================================
    // Menu 2a — Build & save the prefabs
    // =========================================================================
    [MenuItem("Setup/2a - Create Station Prefabs")]
    public static void CreatePrefabs()
    {
        try
        {
            EnsureFolder(PrefabFolder);

            var consoleAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ConsolePrefab);
            if (consoleAsset == null)
            {
                Debug.LogError($"[StationsBuilder] Console prefab not found at: {ConsolePrefab}");
                return;
            }

            int built = 0;
            foreach (var def in Stations)
            {
                string prefabPath = $"{PrefabFolder}/{def.Name}.prefab";
                try
                {
                    BuildOnePrefab(def, consoleAsset, prefabPath);
                    built++;
                }
                catch (Exception inner)
                {
                    Debug.LogError($"[StationsBuilder] Failed to build prefab {prefabPath}: {inner}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[StationsBuilder] Created {built}/{Stations.Length} station prefabs in {PrefabFolder}/");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StationsBuilder] CreatePrefabs failed: {e}");
        }
    }

    static void BuildOnePrefab(StationDef def, GameObject consoleAsset, string prefabPath)
    {
        // Build a working hierarchy in the scene, save it as a prefab, then destroy it.
        var root = new GameObject(def.Name);
        try
        {
            // ── Visual: instance of the Vintage Controls console ─────────────
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(consoleAsset);
            visual.name = "Visual";
            visual.transform.SetParent(root.transform, false);

            // ── Trigger: 2x2x2 BoxCollider isTrigger ─────────────────────────
            var trigger = new GameObject("Trigger");
            trigger.transform.SetParent(root.transform, false);
            var box = trigger.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(2f, 2f, 2f);
            box.center = new Vector3(0f, 1f, 0f); // raise so player walks "into" it

            // The TaskStation script also requires a Collider on the ROOT
            // ([RequireComponent(typeof(Collider))]). Add one and force isTrigger.
            var rootBox = root.AddComponent<BoxCollider>();
            rootBox.isTrigger = true;
            rootBox.size = new Vector3(2f, 2f, 2f);
            rootBox.center = new Vector3(0f, 1f, 0f);

            // ── LED: Point light tinted with the station theme color ─────────
            var ledGO = new GameObject("LED");
            ledGO.transform.SetParent(root.transform, false);
            ledGO.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var led = ledGO.AddComponent<Light>();
            led.type = LightType.Point;
            led.color = def.Color;
            led.range = 4f;
            led.intensity = 2f;

            // ── WorldCanvas: world-space TMP UI showing "Press E" ────────────
            var canvasGO = new GameObject("WorldCanvas");
            canvasGO.transform.SetParent(root.transform, false);
            canvasGO.transform.localPosition = new Vector3(0f, 2.0f, 0f);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvasGO.AddComponent<CanvasScaler>();
            canvasGO.AddComponent<GraphicRaycaster>();

            var canvasRT = canvasGO.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(100f, 60f);             // 1m × 0.6m at scale 0.01
            canvasRT.localScale = Vector3.one * 0.01f;

            // Station name (top)
            var nameGO = new GameObject("StationName");
            nameGO.transform.SetParent(canvasGO.transform, false);
            var nameTMP = nameGO.AddComponent<TextMeshProUGUI>();
            nameTMP.text = def.Name.Replace("Station", "").ToUpper();
            nameTMP.alignment = TextAlignmentOptions.Center;
            nameTMP.fontSize = 14f;
            nameTMP.color = def.Color;
            var nameRT = nameTMP.rectTransform;
            nameRT.anchorMin = new Vector2(0f, 0.55f);
            nameRT.anchorMax = new Vector2(1f, 1f);
            nameRT.offsetMin = Vector2.zero;
            nameRT.offsetMax = Vector2.zero;

            // Instruction "Press E"
            var instrGO = new GameObject("Instruction");
            instrGO.transform.SetParent(canvasGO.transform, false);
            var instrTMP = instrGO.AddComponent<TextMeshProUGUI>();
            instrTMP.text = "Press E";
            instrTMP.alignment = TextAlignmentOptions.Center;
            instrTMP.fontSize = 12f;
            instrTMP.color = Color.white;
            var instrRT = instrTMP.rectTransform;
            instrRT.anchorMin = new Vector2(0f, 0.15f);
            instrRT.anchorMax = new Vector2(1f, 0.55f);
            instrRT.offsetMin = Vector2.zero;
            instrRT.offsetMax = Vector2.zero;

            // Status light dot (bottom, used by StationUI.statusLight)
            var statusGO = new GameObject("StatusLight");
            statusGO.transform.SetParent(canvasGO.transform, false);
            var statusImg = statusGO.AddComponent<Image>();
            statusImg.color = def.Color;
            var statusRT = statusImg.rectTransform;
            statusRT.anchorMin = new Vector2(0.4f, 0f);
            statusRT.anchorMax = new Vector2(0.6f, 0.15f);
            statusRT.offsetMin = Vector2.zero;
            statusRT.offsetMax = Vector2.zero;

            // Progress bar (used by StationUI.progressBar)
            var sliderGO = new GameObject("ProgressBar");
            sliderGO.transform.SetParent(canvasGO.transform, false);
            var slider = sliderGO.AddComponent<Slider>();
            var sliderRT = sliderGO.GetComponent<RectTransform>();
            sliderRT.anchorMin = new Vector2(0.05f, 0.02f);
            sliderRT.anchorMax = new Vector2(0.35f, 0.13f);
            sliderRT.offsetMin = Vector2.zero;
            sliderRT.offsetMax = Vector2.zero;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            // ── Components on root ───────────────────────────────────────────
            // StationUI (added before TaskStation so we can wire it)
            var ui = root.AddComponent<StationUI>();
            WireStationUI(ui, nameTMP, instrTMP, slider, statusImg, def.Color);

            // TaskStation (must be after StationUI exists for wiring)
            var taskStation = root.AddComponent<TaskStation>();
            WireTaskStation(taskStation, def.Name, ui);

            // The matching MissionTask subclass (added directly on the root).
            // MissionTask itself is an abstract MonoBehaviour; its concrete subclass
            // initialises priority + timeLimit in Awake().
            root.AddComponent(def.TaskType);

            // ── Save as prefab (idempotent: SaveAsPrefabAsset overwrites) ────
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    // =========================================================================
    // Reflection-based serialized-field wiring
    // =========================================================================
    static void WireTaskStation(TaskStation ts, string stationName, StationUI ui)
    {
        var so = new SerializedObject(ts);
        SetStringIfPresent(so, "stationName", stationName);
        SetObjectIfPresent(so, "stationUI", ui);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void WireStationUI(StationUI ui,
                              TextMeshProUGUI stationNameText,
                              TextMeshProUGUI instructionText,
                              Slider progressBar,
                              Image statusLight,
                              Color theme)
    {
        var so = new SerializedObject(ui);
        SetObjectIfPresent(so, "stationNameText", stationNameText);
        SetObjectIfPresent(so, "instructionText", instructionText);
        SetObjectIfPresent(so, "progressBar", progressBar);
        SetObjectIfPresent(so, "statusLight", statusLight);

        // Tint the active color toward the theme (used by SetInstruction / SetUrgent).
        var active = so.FindProperty("activeColor");
        if (active != null) active.colorValue = theme;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjectIfPresent(SerializedObject so, string fieldName, UnityEngine.Object value)
    {
        var p = so.FindProperty(fieldName);
        if (p == null)
        {
            Debug.LogWarning($"[StationsBuilder] Field '{fieldName}' not found on {so.targetObject.GetType().Name}");
            return;
        }
        p.objectReferenceValue = value;
    }

    static void SetStringIfPresent(SerializedObject so, string fieldName, string value)
    {
        var p = so.FindProperty(fieldName);
        if (p == null)
        {
            Debug.LogWarning($"[StationsBuilder] Field '{fieldName}' not found on {so.targetObject.GetType().Name}");
            return;
        }
        p.stringValue = value;
    }

    // =========================================================================
    // Menu 2b — Place the prefabs in the active scene
    // =========================================================================
    [MenuItem("Setup/2b - Place Stations in Scene")]
    public static void PlaceInScene()
    {
        try
        {
            // Idempotent: tear down any prior root.
            var existing = GameObject.Find("Stations_Root");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);

            var rootGO = new GameObject("Stations_Root");

            int placed = 0;
            foreach (var def in Stations)
            {
                string prefabPath = $"{PrefabFolder}/{def.Name}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null)
                {
                    Debug.LogError($"[StationsBuilder] Prefab missing at {prefabPath}. Run 'Setup/2a - Create Station Prefabs' first.");
                    continue;
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootGO.transform);
                instance.transform.position = def.ScenePos;

                // Face the world center so the console fronts the player.
                var lookAt = new Vector3(0f, def.ScenePos.y, 0f);
                if ((instance.transform.position - lookAt).sqrMagnitude > 0.0001f)
                {
                    instance.transform.LookAt(lookAt);
                    var e = instance.transform.eulerAngles;
                    instance.transform.eulerAngles = new Vector3(0f, e.y, 0f);
                }
                placed++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[StationsBuilder] Placed {placed}/{Stations.Length} stations under Stations_Root.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[StationsBuilder] PlaceInScene failed: {e}");
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================
    static void EnsureFolder(string assetPath)
    {
        if (AssetDatabase.IsValidFolder(assetPath)) return;

        string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
        string leaf   = Path.GetFileName(assetPath);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, leaf);
    }

    static Color HexToColor(string hex)
    {
        if (ColorUtility.TryParseHtmlString(hex, out var c)) return c;
        Debug.LogWarning($"[StationsBuilder] Bad hex color '{hex}' — falling back to white.");
        return Color.white;
    }
}
#endif
