using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mission Settings")]
    [SerializeField] private float missionDuration = 600f;

    [Header("Task Spawn Pacing")]
    [SerializeField, Tooltip("Maximum number of tasks that can be active at the same time across all stations.")]
    private int maxConcurrentTasks = 3;
    [SerializeField, Tooltip("Minimum seconds between consecutive task spawns.")]
    private float minSpawnInterval = 15f;
    [SerializeField, Tooltip("Maximum seconds between consecutive task spawns. Spawn delay is randomized between min and max.")]
    private float maxSpawnInterval = 25f;
    [SerializeField, Tooltip("After a task resolves at a station, wait this many seconds before that station can receive a new task.")]
    private float stationCooldownAfterResolve = 15f;
    [SerializeField, Tooltip("How often (seconds) the spawner re-checks when it's blocked (max concurrent reached or no eligible station). Keep small.")]
    private float spawnRecheckInterval = 1.5f;

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

    private readonly Dictionary<string, float> lastResolvedAt = new Dictionary<string, float>();
    private string lastSpawnedStationName = null;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        AutoBindStations();
    }

    private void OnEnable()
    {
        MissionTask.OnTaskResolved += HandleTaskResolvedForCooldown;
    }

    private void OnDisable()
    {
        MissionTask.OnTaskResolved -= HandleTaskResolvedForCooldown;
    }

    private void HandleTaskResolvedForCooldown(MissionTask task, TaskResult result, float rt)
    {
        if (task == null || string.IsNullOrEmpty(task.StationName)) return;
        lastResolvedAt[task.StationName] = Time.time;
    }

    private void AutoBindStations()
    {
        if (engineStation != null && navigationStation != null
            && commsStation != null && lifeSupportStation != null) return;

        var all = Object.FindObjectsByType<TaskStation>(FindObjectsInactive.Exclude);
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
        AudioManager.Instance.PlayMusic("gameplay_loop");
        AudioManager.Instance.PlayAmbient("station_hum");
        AudioManager.Instance.PlayVoice("mission_start");
        StartCoroutine(MissionCountdown());
        StartCoroutine(TaskSpawnLoop());
    }

    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        // F9: skip random spawn, immediately start the full Working Memory task.
        if (kb.f9Key.wasPressedThisFrame) ForceSpawnEngineTask();

        // F7: skip random spawn, immediately start the full Inhibit task at Comms.
        if (kb.f7Key.wasPressedThisFrame) ForceSpawnCommsTask();

        // F6: skip random spawn, immediately start Radar Scan in quick mode (fast dev iteration).
        if (kb.f6Key.wasPressedThisFrame) ForceSpawnNavigationTask();

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

    [ContextMenu("Debug: Force-spawn Comms Task")]
    public void ForceSpawnCommsTask()
    {
        if (commsStation == null)
        {
            Debug.LogWarning("[GameManager] commsStation is null — run Tools/Mission Focus/Wire GameManager Stations.");
            return;
        }
        if (commsStation.HasActiveTask())
        {
            Debug.Log("[GameManager] CommsStation already has an active task.");
            return;
        }
        GameObject go = new GameObject("CommsTask");
        commsStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, commsStation.stationName));
        Debug.Log("[GameManager] Force-spawned Comms task via F7.");
    }

    [ContextMenu("Debug: Force-spawn Navigation Task (quick mode)")]
    public void ForceSpawnNavigationTask()
    {
        if (navigationStation == null)
        {
            Debug.LogWarning("[GameManager] navigationStation is null — run Tools/Mission Focus/Wire GameManager Stations.");
            return;
        }
        if (navigationStation.HasActiveTask())
        {
            Debug.Log("[GameManager] NavigationStation already has an active task.");
            return;
        }
        GameObject go = new GameObject("NavigationTask");
        var task = CognitiveTaskCatalog.CreateTaskForStation(go, navigationStation.stationName);
        if (task is RadarScanTask radar) radar.quickMode = true;
        navigationStation.AssignTask(task);
        Debug.Log("[GameManager] Force-spawned Radar Scan (quick mode) via F6.");
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
        AudioManager.Instance.StopMusic();
        AudioManager.Instance.StopAmbient();
        Debug.Log("[GameManager] Mission complete. Logs at: " + Application.persistentDataPath);
    }

    private IEnumerator TaskSpawnLoop()
    {
        yield return new WaitForSeconds(3f);
        while (MissionActive)
        {
            if (CountActiveTasks() >= maxConcurrentTasks)
            {
                yield return new WaitForSeconds(spawnRecheckInterval);
                continue;
            }

            var eligible = BuildEligibleStationList();
            if (eligible.Count == 0)
            {
                yield return new WaitForSeconds(spawnRecheckInterval);
                continue;
            }

            TaskStation chosen = PickStation(eligible);
            SpawnTaskAt(chosen);
            lastSpawnedStationName = chosen.stationName;

            float wait = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(wait);
        }
    }

    private int CountActiveTasks()
    {
        int n = 0;
        if (engineStation      != null && engineStation.HasActiveTask())      n++;
        if (navigationStation  != null && navigationStation.HasActiveTask())  n++;
        if (commsStation       != null && commsStation.HasActiveTask())       n++;
        if (lifeSupportStation != null && lifeSupportStation.HasActiveTask()) n++;
        return n;
    }

    private List<TaskStation> BuildEligibleStationList()
    {
        var list = new List<TaskStation>(4);
        TryAddEligible(list, engineStation);
        TryAddEligible(list, navigationStation);
        TryAddEligible(list, commsStation);
        TryAddEligible(list, lifeSupportStation);
        return list;
    }

    private void TryAddEligible(List<TaskStation> list, TaskStation s)
    {
        if (s == null) return;
        if (s.HasActiveTask()) return;
        if (lastResolvedAt.TryGetValue(s.stationName, out float t)
            && Time.time - t < stationCooldownAfterResolve) return;
        list.Add(s);
    }

    private TaskStation PickStation(List<TaskStation> eligible)
    {
        if (eligible.Count > 1 && lastSpawnedStationName != null)
        {
            var alt = eligible.FindAll(s => s.stationName != lastSpawnedStationName);
            if (alt.Count > 0) return alt[Random.Range(0, alt.Count)];
        }
        return eligible[Random.Range(0, eligible.Count)];
    }

    private void SpawnTaskAt(TaskStation station)
    {
        var go = new GameObject(station.stationName + "Task");
        station.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, station.stationName));
    }
}
