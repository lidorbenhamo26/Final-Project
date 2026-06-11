using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Culture-invariant float formatting for metric values: exports and parsers
/// must always see dot-decimal numbers regardless of the OS locale.
/// </summary>
public static class Num
{
    private static readonly System.Globalization.CultureInfo Inv =
        System.Globalization.CultureInfo.InvariantCulture;

    public static string F0(float v) => v.ToString("F0", Inv);
    public static string F2(float v) => v.ToString("F2", Inv);
    public static string F3(float v) => v.ToString("F3", Inv);
    public static string F4(float v) => v.ToString("F4", Inv);
}

/// <summary>
/// BRIEF-A executive-function scales the in-game tasks map onto.
/// Order here is not the display order; ReportData defines the clinical order.
/// </summary>
public enum BriefScale
{
    Inhibit,
    WorkingMemory,
    PlanOrganize,
    TaskMonitor,
    Unclassified
}

/// <summary>
/// One administered task instance with its outcome and domain-specific metrics.
/// Metrics are ordered key/value strings so the report and exporters render
/// them in the order the task reported them.
/// </summary>
public class TaskRecord
{
    public string TaskName;
    public string StationName;
    public BriefScale Scale;
    public TaskPriority Priority;
    public TaskResult? Result;          // null = still in progress (F12 partial view)
    public float ReactionTimeS;
    public DateTime SpawnedLocal;
    public DateTime ResolvedLocal;

    private readonly List<KeyValuePair<string, string>> metrics = new List<KeyValuePair<string, string>>();

    public IReadOnlyList<KeyValuePair<string, string>> Metrics => metrics;

    public void SetMetric(string key, string value)
    {
        for (int i = 0; i < metrics.Count; i++)
        {
            if (metrics[i].Key == key)
            {
                metrics[i] = new KeyValuePair<string, string>(key, value);
                return;
            }
        }
        metrics.Add(new KeyValuePair<string, string>(key, value));
    }

    public string GetMetric(string key)
    {
        for (int i = 0; i < metrics.Count; i++)
            if (metrics[i].Key == key) return metrics[i].Value;
        return null;
    }

    public bool TryGetMetricFloat(string key, out float value)
    {
        value = 0f;
        string s = GetMetric(key);
        if (s == null) return false;
        // Metrics are formatted with the current culture (project convention);
        // try invariant first, then the current culture, so bars never vanish
        // over a decimal-separator mismatch.
        return float.TryParse(s, System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out value)
               || float.TryParse(s, out value);
    }
}

/// <summary>
/// Structured per-task results store feeding the assessor report screen and
/// its exporters. Listens to the same MissionTask events SessionManager uses;
/// tasks push domain metrics via AssessmentResults.Report(...) just before
/// they Resolve. The raw event CSV remains the research ground truth — this
/// store only holds what the report needs, for the current session.
/// </summary>
public class AssessmentResults : MonoBehaviour
{
    private static AssessmentResults _instance;

    public static AssessmentResults Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("AssessmentResults");
                _instance = go.AddComponent<AssessmentResults>();
            }
            return _instance;
        }
    }

    /// <summary>Non-creating accessor for teardown paths (OnDestroy/app quit must not spawn objects).</summary>
    public static AssessmentResults InstanceOrNull => _instance;

    /// <summary>Create the singleton early so it is subscribed before the first task spawns.</summary>
    public static void EnsureExists()
    {
        var _ = Instance;
    }

    /// <summary>Raised whenever a record is opened, updated, or resolved.</summary>
    public event Action Changed;

    private readonly List<TaskRecord> records = new List<TaskRecord>();
    private readonly Dictionary<MissionTask, TaskRecord> openRecords = new Dictionary<MissionTask, TaskRecord>();

    /// <summary>All records for the current session, in spawn order (unresolved ones included).</summary>
    public IReadOnlyList<TaskRecord> Records => records;

    private static readonly Dictionary<string, BriefScale> ScaleMap = new Dictionary<string, BriefScale>
    {
        { "Working Memory", BriefScale.WorkingMemory },
        { "Code Memory",    BriefScale.WorkingMemory },
        { "Stroop",         BriefScale.Inhibit },
        { "Inhibit",        BriefScale.Inhibit },
        { "Radar Scan",     BriefScale.TaskMonitor },
        { "Power Cell",     BriefScale.PlanOrganize },
    };

    public static BriefScale ScaleFor(string taskName)
    {
        if (!string.IsNullOrEmpty(taskName) && ScaleMap.TryGetValue(taskName, out var scale))
            return scale;
        return BriefScale.Unclassified;
    }

    /// <summary>
    /// Merge domain metrics into the open record for this task instance.
    /// Null-safe and order-preserving; call just before Resolve.
    /// </summary>
    public static void Report(MissionTask task, params (string key, string value)[] metrics)
    {
        if (task == null) return;
        var inst = Instance;
        var record = inst.GetOrOpenRecord(task);
        if (metrics != null)
            foreach (var (key, value) in metrics)
                record.SetMetric(key, value);
        inst.Changed?.Invoke();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        MissionTask.OnTaskSpawned += HandleTaskSpawned;
        MissionTask.OnTaskResolved += HandleTaskResolved;
    }

    private void OnDisable()
    {
        MissionTask.OnTaskSpawned -= HandleTaskSpawned;
        MissionTask.OnTaskResolved -= HandleTaskResolved;
    }

    public void Clear()
    {
        records.Clear();
        openRecords.Clear();
        Changed?.Invoke();
    }

    private void HandleTaskSpawned(MissionTask task)
    {
        GetOrOpenRecord(task);
        Changed?.Invoke();
    }

    private void HandleTaskResolved(MissionTask task, TaskResult result, float reactionTime)
    {
        var record = GetOrOpenRecord(task);
        record.Result = result;
        record.ReactionTimeS = reactionTime;
        record.ResolvedLocal = DateTime.Now;
        openRecords.Remove(task);
        Changed?.Invoke();
    }

    private TaskRecord GetOrOpenRecord(MissionTask task)
    {
        if (openRecords.TryGetValue(task, out var existing)) return existing;

        var record = new TaskRecord
        {
            TaskName = task.TaskName,
            StationName = task.StationName,
            Scale = ScaleFor(task.TaskName),
            Priority = task.Priority,
            Result = null,
            SpawnedLocal = DateTime.Now,
        };
        records.Add(record);
        openRecords[task] = record;
        return record;
    }
}
