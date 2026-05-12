using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mission Settings")]
    [SerializeField] private float missionDuration = 600f;
    [SerializeField, Tooltip("Seconds between task spawns — lower = harder")]
    private float eventFrequency = 15f;

    [Header("Debug / Quick Test")]
    [SerializeField, Tooltip("If checked, mission uses Quick Test Duration instead of Mission Duration. Leave OFF for normal 10-min runs.")]
    private bool quickTestMode = false;
    [SerializeField, Tooltip("Mission length when Quick Test Mode is enabled.")]
    private float quickTestDuration = 30f;

    [Header("Stations")]
    [SerializeField] private TaskStation engineStation;
    [SerializeField] private TaskStation navigationStation;
    [SerializeField] private TaskStation commsStation;
    [SerializeField] private TaskStation lifeSupportStation;

    public float MissionTimeRemaining { get; private set; }
    public bool MissionActive { get; private set; }

    public TaskStation ActiveTaskStation
    {
        get
        {
            if (engineStation      != null && engineStation.HasActiveTask())      return engineStation;
            if (navigationStation  != null && navigationStation.HasActiveTask())  return navigationStation;
            if (commsStation       != null && commsStation.HasActiveTask())       return commsStation;
            if (lifeSupportStation != null && lifeSupportStation.HasActiveTask()) return lifeSupportStation;
            return null;
        }
    }

    public TaskStation EngineStation      => engineStation;
    public TaskStation NavigationStation  => navigationStation;
    public TaskStation CommsStation       => commsStation;
    public TaskStation LifeSupportStation => lifeSupportStation;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        AutoBindStations();
    }

    private void AutoBindStations()
    {
        if (engineStation != null && navigationStation != null
            && commsStation != null && lifeSupportStation != null) return;

        var all = Object.FindObjectsByType<TaskStation>(FindObjectsSortMode.None);
        foreach (var s in all)
        {
            string n = s.stationName != null ? s.stationName.ToLowerInvariant() : "";
            if (engineStation == null && n.Contains("engine"))
                engineStation = s;
            else if (navigationStation == null && n.Contains("nav"))
                navigationStation = s;
            else if (commsStation == null && n.Contains("comm"))
                commsStation = s;
            else if (lifeSupportStation == null && (n.Contains("life") || n.Contains("support")))
                lifeSupportStation = s;
        }
    }

    private void Start()
    {
        if (quickTestMode) Application.runInBackground = true;
        MissionTimeRemaining = quickTestMode ? quickTestDuration : missionDuration;
        MissionActive = true;
        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent("Mission_Start", "System", "Begin");
        StartCoroutine(MissionCountdown());
        StartCoroutine(TaskSpawnLoop());
    }

    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        // F9: skip random spawn, immediately start the full Working Memory task.
        if (kb.f9Key.wasPressedThisFrame) ForceSpawnEngineTask();

        // F8: debug — show the HUD code banner directly with "1234".
        if (kb.f8Key.wasPressedThisFrame)
        {
            HUDManager.Instance?.ShowAlertBanner("INCOMING CODE", 1.5f);
            StartCoroutine(F8ShowCodeAfter(1.5f));
        }
    }

    private System.Collections.IEnumerator F8ShowCodeAfter(float delay)
    {
        yield return new WaitForSeconds(delay);
        HUDManager.Instance?.ShowCodeBanner("1234", 4f);
    }

    [ContextMenu("Debug: Force-spawn Engine Task")]
    public void ForceSpawnEngineTask()
    {
        if (engineStation == null)
        {
            Debug.LogWarning("[GameManager] engineStation is null — run Tools/Mission Focus/Wire GameManager Stations.");
            return;
        }
        if (engineStation.HasActiveTask())
        {
            Debug.Log("[GameManager] EngineStation already has an active task.");
            return;
        }
        GameObject go = new GameObject("EngineTask");
        engineStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, engineStation.stationName));
        Debug.Log("[GameManager] Force-spawned Engine task via F9.");
    }

    private IEnumerator MissionCountdown()
    {
        while (MissionTimeRemaining > 0f && MissionActive)
        {
            yield return new WaitForSeconds(1f);
            MissionTimeRemaining -= 1f;
        }
        MissionActive = false;
        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent("Mission_End", "System", "Complete");
        Debug.Log("[GameManager] Mission complete. Logs at: " + Application.persistentDataPath);
    }

    private IEnumerator TaskSpawnLoop()
    {
        yield return new WaitForSeconds(3f);
        while (MissionActive)
        {
            int pick = Random.Range(0, 4);

            if (pick == 0 && engineStation != null && !engineStation.HasActiveTask())
            {
                GameObject go = new GameObject("EngineTask");
                engineStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, engineStation.stationName));
            }
            else if (pick == 1 && navigationStation != null && !navigationStation.HasActiveTask())
            {
                GameObject go = new GameObject("NavigationTask");
                navigationStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, navigationStation.stationName));
            }
            else if (pick == 2 && commsStation != null && !commsStation.HasActiveTask())
            {
                GameObject go = new GameObject("CommsTask");
                commsStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, commsStation.stationName));
            }
            else if (pick == 3 && lifeSupportStation != null && !lifeSupportStation.HasActiveTask())
            {
                GameObject go = new GameObject("LifeSupportTask");
                lifeSupportStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, lifeSupportStation.stationName));
            }

            yield return new WaitForSeconds(eventFrequency);
        }
    }
}
