using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mission Settings")]
    [SerializeField] private float missionDuration = 600f;

    [Header("Task Spawn — Difficulty Ramp")]
    [SerializeField, Tooltip("Opening calm window: no task pressure (difficulty 0) for this many seconds so the player can settle in.")]
    private float calmIntroSeconds = 75f;
    [SerializeField, Tooltip("Maps mission progress AFTER the calm intro (0..1) to difficulty (0..1). Ease-in keeps it gentle then ramps.")]
    private AnimationCurve difficultyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField, Tooltip("Spawn interval (min,max seconds) at difficulty 0 (calm).")]
    private Vector2 spawnIntervalCalm = new Vector2(20f, 28f);
    [SerializeField, Tooltip("Spawn interval (min,max seconds) at difficulty 1 (intense) — the cap, so it never becomes impossible.")]
    private Vector2 spawnIntervalIntense = new Vector2(6f, 10f);
    [SerializeField, Tooltip("Max concurrent tasks at difficulty 0 (one at a time).")]
    private int maxConcurrentCalm = 1;
    [SerializeField, Tooltip("Max concurrent tasks at difficulty 1 (the cap).")]
    private int maxConcurrentIntense = 3;
    [SerializeField, Tooltip("Multiplier on each task's response window (time limit) at difficulty 0 — a little extra time.")]
    private float responseFactorCalm = 1.15f;
    [SerializeField, Tooltip("Multiplier on each task's response window at difficulty 1 — tighter, but never below the floor.")]
    private float responseFactorIntense = 0.65f;
    [SerializeField, Tooltip("Never shorten a task's response window below this many seconds (keeps it fair).")]
    private float minResponseWindow = 7f;
    [SerializeField, Tooltip("After a task resolves at a station, wait this many seconds before that station can receive a new task.")]
    private float stationCooldownAfterResolve = 15f;
    [SerializeField, Tooltip("How often (seconds) the spawner re-checks when it's blocked (max concurrent reached or no eligible station). Keep small.")]
    private float spawnRecheckInterval = 1.5f;

    /// <summary>Current mission difficulty in 0..1 (0 during the calm intro). Read by the HUD and DistractionDirector.</summary>
    public float CurrentDifficulty { get; private set; }

    [Header("Tutorial")]
    [SerializeField, Tooltip("If checked, this GameManager sets up (Instance + station refs) but does NOT start the mission timer or task spawning. The TutorialDirector drives the flow instead. Used by TutorialScene.")]
    private bool tutorialMode = false;

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

    // F11 toggles this. When true: mission timer, task spawning, and every
    // MissionTask's internal timer freeze — so you can carry the cell and inspect
    // the grip indefinitely with no game pressure.
    public static bool IsDebugFrozen { get; private set; }

    /// <summary>Set the debug-freeze state directly (used by F11 and the assessor report overlay).</summary>
    public static void SetDebugFrozen(bool value)
    {
        IsDebugFrozen = value;
    }

    /// <summary>True while the assessor has paused the session (distinct from F11 debug freeze).</summary>
    public bool MissionPaused { get; private set; }

    /// <summary>
    /// Assessor pause/resume. Reuses the freeze plumbing so the mission timer,
    /// task spawning, and every task's internal timer halt together — and
    /// MissionTask slides its SpawnTime forward while frozen, so reaction times
    /// are not corrupted by the paused interval. Resume continues exactly.
    /// </summary>
    public void SetPaused(bool paused)
    {
        if (!MissionActive || MissionPaused == paused) return;
        MissionPaused = paused;
        SetDebugFrozen(paused);
        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent(paused ? "Mission_Paused" : "Mission_Resumed",
                "Assessor", "t=" + Mathf.RoundToInt(MissionTimeRemaining) + "s");
    }

    /// <summary>
    /// Assessor "Stop": end the session early. The mission loops finalize on their
    /// next tick and HUDManager shows the report with whatever data was collected,
    /// so the session is never lost.
    /// </summary>
    public void EndMissionEarly()
    {
        if (!MissionActive) return;
        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent("Mission_Stopped_Early", "Assessor",
                "t=" + Mathf.RoundToInt(MissionTimeRemaining) + "s diff=" + CurrentDifficulty.ToString("F2"));
        MissionPaused = false;
        SetDebugFrozen(false);
        MissionActive = false; // MissionCountdown/TaskSpawnLoop exit; HUD shows the report
    }

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
        // Log difficulty alongside the outcome so the report can plot performance
        // against how hard the mission was at that moment.
        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent("Difficulty", task.StationName,
                result + " diff=" + CurrentDifficulty.ToString("F2"));
    }

    private float ComputeDifficulty()
    {
        float total = quickTestMode ? quickTestDuration : missionDuration;
        float elapsed = total - MissionTimeRemaining;
        if (elapsed <= calmIntroSeconds) return 0f;
        float denom = Mathf.Max(1f, total - calmIntroSeconds);
        float p = Mathf.Clamp01((elapsed - calmIntroSeconds) / denom);
        return Mathf.Clamp01(difficultyCurve.Evaluate(p));
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
        // Fresh mission = fresh session stats. SessionManager/AssessmentResults
        // are DontDestroyOnLoad, so without this the tutorial practice task (and
        // any record stuck mid-flight on a scene change) would leak into this
        // participant's report and CSV exports.
        if (SessionManager.Instance != null) SessionManager.Instance.ResetForNewMission();

        // Tutorial: keep Instance + station bindings live (so docking, the HUD and
        // proximity prompts work) but don't run the real mission — the
        // TutorialDirector spawns its own practice task and drives the flow.
        if (tutorialMode)
        {
            AudioManager.Instance.PlayAmbient("station_hum");
            return;
        }

        // Assessor-chosen length (from the intake form) overrides the serialized
        // default, so both the mission timer and the difficulty ramp (which reads
        // missionDuration) scale to it.
        if (SessionContext.Instance != null && SessionContext.Instance.MissionMinutes > 0)
            missionDuration = SessionContext.Instance.MissionMinutes * 60f;

        if (quickTestMode) Application.runInBackground = true;
        MissionTimeRemaining = quickTestMode ? quickTestDuration : missionDuration;
        MissionActive = true;
        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent("Mission_Start", "System", "Begin");
        // Music is station-scoped: it starts on dock and fades out on exit
        // (StationDockController), so free-roam is ambient-only — no global music here.
        AudioManager.Instance.PlayAmbient("station_hum");
        AudioManager.Instance.PlayVoice("mission_start");
        StartCoroutine(MissionCountdown());
        StartCoroutine(TaskSpawnLoop());
    }

    private void Update()
    {
        if (MissionActive) CurrentDifficulty = ComputeDifficulty();

        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null) return;

        // F9: skip random spawn, immediately start the full Working Memory task.
        if (kb.f9Key.wasPressedThisFrame) ForceSpawnEngineTask();

        // F7: skip random spawn, immediately start the full Inhibit task at Comms.
        if (kb.f7Key.wasPressedThisFrame) ForceSpawnCommsTask();

        // F6: skip random spawn, immediately start Radar Scan in quick mode (fast dev iteration).
        if (kb.f6Key.wasPressedThisFrame) ForceSpawnNavigationTask();

        // F10: skip random spawn, immediately start the Battery Delivery task at Life Support.
        if (kb.f10Key.wasPressedThisFrame) ForceSpawnLifeSupportTask();

        // F11: debug freeze — pauses mission timer, task spawns, and all task drains.
        // Ignored while the assessor report is open: the report owns the freeze
        // then, and a hidden toggle would silently desync its restore snapshot.
        if (kb.f11Key.wasPressedThisFrame
            && (AssessmentReportController.Instance == null || !AssessmentReportController.Instance.IsVisible))
        {
            SetDebugFrozen(!IsDebugFrozen);
            Debug.Log("[GameManager] Debug freeze: " + (IsDebugFrozen ? "ON" : "OFF"));
            HUDManager.Instance?.ShowAlertBanner(IsDebugFrozen ? "DEBUG FREEZE: ON" : "DEBUG FREEZE: OFF", 1.4f);
        }

        // F12: toggle the assessor report overlay (freezes the mission while open).
        if (kb.f12Key.wasPressedThisFrame)
            AssessmentReportController.Instance?.ToggleDebug();

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

    [ContextMenu("Debug: Force-spawn Life Support Task")]
    public void ForceSpawnLifeSupportTask()
    {
        if (lifeSupportStation == null)
        {
            Debug.LogWarning("[GameManager] lifeSupportStation is null — run Tools/Mission Focus/Wire GameManager Stations.");
            return;
        }
        if (lifeSupportStation.HasActiveTask())
        {
            Debug.Log("[GameManager] LifeSupportStation already has an active task.");
            return;
        }
        GameObject go = new GameObject("LifeSupportStationTask");
        lifeSupportStation.AssignTask(CognitiveTaskCatalog.CreateTaskForStation(go, lifeSupportStation.stationName));
        Debug.Log("[GameManager] Force-spawned Life Support battery task via F10.");
    }

    private IEnumerator MissionCountdown()
    {
        while (MissionTimeRemaining > 0f && MissionActive)
        {
            yield return new WaitForSeconds(1f);
            if (!IsDebugFrozen) MissionTimeRemaining -= 1f;
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
            if (IsDebugFrozen)
            {
                yield return new WaitForSeconds(spawnRecheckInterval);
                continue;
            }

            CurrentDifficulty = ComputeDifficulty();
            int maxConcurrent = Mathf.Max(1, Mathf.RoundToInt(
                Mathf.Lerp(maxConcurrentCalm, maxConcurrentIntense, CurrentDifficulty)));

            if (CountActiveTasks() >= maxConcurrent)
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

            // Spawn cadence tightens as difficulty climbs (capped by the intense range).
            float min = Mathf.Lerp(spawnIntervalCalm.x, spawnIntervalIntense.x, CurrentDifficulty);
            float max = Mathf.Lerp(spawnIntervalCalm.y, spawnIntervalIntense.y, CurrentDifficulty);
            yield return new WaitForSeconds(Random.Range(min, max));
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
        var task = CognitiveTaskCatalog.CreateTaskForStation(go, station.stationName);
        station.AssignTask(task);

        // Tighten the response window as difficulty climbs, but never below the
        // fairness floor. Applied AFTER Activate so it scales the task's own limit.
        float factor = Mathf.Lerp(responseFactorCalm, responseFactorIntense, CurrentDifficulty);
        task.timeLimit = Mathf.Max(minResponseWindow, task.timeLimit * factor);

        if (SessionManager.Instance != null)
            SessionManager.Instance.LogCustomEvent("Task_Spawn", station.stationName,
                "diff=" + CurrentDifficulty.ToString("F2"));
    }
}
