using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One executive-function event the player went through (e.g. a Triage). This is
/// the BEHAVIORAL record — how the player prioritized/decided — kept SEPARATE from
/// the cognitive task records in <see cref="AssessmentResults"/>.
/// </summary>
public class EFEventRecord
{
    public string EventType;        // "Triage" (Interruption / WM-Prioritization later)
    public int    NOptions;
    public string OptionsDesc;      // "Nav:low:55s|LifeSupport:critical:40s|..."
    public string ChosenOrder;      // "LifeSupport>Comms>Nav"
    public string OptimalOrder;
    public string ChosenFirst;
    public bool   FirstWasOptimal;  // chosen first == optimal first
    public bool   WasOptimal;       // whole order matched optimal
    public float  OptScore;         // 0..1 pairwise concordance vs optimal
    public float  DecisionLatencyS; // < 0 = NA (no choice made)
    public int    ChangedMind;
    public bool   NoChoice;
    public DateTime When;

    // Interruption (Shift) specifics. For Triage events these stay at defaults.
    public bool   Switched;         // the player chose SWITCH (stays default to STAY); latency alone
                                    // can't tell — a stay decision has a latency too
    public bool   Perseveration;    // a more-critical task interrupted but the player didn't switch
    public bool   Resumed;          // after switching, the player came back and finished the first task

    // WM+Prioritization specific: was the up-front code entered correctly after the
    // prioritization interference? 1 = yes, 0 = no, -1 = NA / not applicable.
    public int    CodeRecalledCorrect = -1;
}

/// <summary>
/// Session-scoped store of executive-function event records, mirrored on the
/// <see cref="AssessmentResults"/> pattern. Feeds the report's EF-behavior section.
/// Cleared per mission alongside the cognitive results.
/// </summary>
public class EFResults : MonoBehaviour
{
    private static EFResults _instance;

    public static EFResults Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("EFResults");
                _instance = go.AddComponent<EFResults>();
            }
            return _instance;
        }
    }

    public static EFResults InstanceOrNull => _instance;

    public event Action Changed;

    private readonly List<EFEventRecord> events = new List<EFEventRecord>();
    public IReadOnlyList<EFEventRecord> Events => events;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Add(EFEventRecord record)
    {
        if (record == null) return;
        events.Add(record);
        Changed?.Invoke();
    }

    public void Clear()
    {
        events.Clear();
        Changed?.Invoke();
    }

    // ----------------------------------------------------------- aggregates

    public int Count => events.Count;

    /// <summary>Fraction of events where the player's FIRST choice was optimal.</summary>
    public float OptimalFirstRate => Mean(e => e.FirstWasOptimal ? 1f : 0f);

    /// <summary>Mean order-quality score (0..1) across events.</summary>
    public float MeanOptScore => Mean(e => e.OptScore);

    /// <summary>Mean decision latency over events where a choice was actually made.</summary>
    public float MeanDecisionLatencyS
    {
        get
        {
            float sum = 0f; int n = 0;
            foreach (var e in events)
                if (!e.NoChoice && e.DecisionLatencyS >= 0f) { sum += e.DecisionLatencyS; n++; }
            return n == 0 ? -1f : sum / n;
        }
    }

    /// <summary>Fraction of events the player never made a choice in (let it default).</summary>
    public float NoChoiceRate => Mean(e => e.NoChoice ? 1f : 0f);

    // ---- Shift (task-switching) aggregates, over Interruption events only ----

    public int InterruptionCount => CountType("Interruption");

    /// <summary>Fraction of interruptions the player handled optimally (switched
    /// when the interrupt was more critical, stayed otherwise).</summary>
    public float SwitchCorrectRate => MeanOverType("Interruption", e => e.FirstWasOptimal ? 1f : 0f);

    /// <summary>Mean switch latency over interruptions where the player switched.</summary>
    public float MeanSwitchLatencyS
    {
        get
        {
            float sum = 0f; int n = 0;
            foreach (var e in events)
                if (e.EventType == "Interruption" && e.Switched && e.DecisionLatencyS >= 0f) { sum += e.DecisionLatencyS; n++; }
            return n == 0 ? -1f : sum / n;
        }
    }

    /// <summary>Number of interruptions the player failed to switch on when they should have.</summary>
    public int PerseverationCount
    {
        get { int n = 0; foreach (var e in events) if (e.EventType == "Interruption" && e.Perseveration) n++; return n; }
    }

    /// <summary>Of the times the player switched, fraction where they returned to finish the first task.</summary>
    public float ResumeRate
    {
        get
        {
            int switched = 0, resumed = 0;
            foreach (var e in events)
                if (e.EventType == "Interruption" && e.Switched) { switched++; if (e.Resumed) resumed++; }
            return switched == 0 ? -1f : (float)resumed / switched;
        }
    }

    // ---- Working memory under load, over WM+Prioritization events ----

    public int WmPrioritizationCount => CountType("WM+Prioritization");

    /// <summary>Fraction of WM+Prioritization events where the up-front code was
    /// recalled correctly after the interference. -1 = NA (none entered).</summary>
    public float CodeRecallRate
    {
        get
        {
            int n = 0, ok = 0;
            foreach (var e in events)
                if (e.EventType == "WM+Prioritization" && e.CodeRecalledCorrect >= 0)
                { n++; if (e.CodeRecalledCorrect == 1) ok++; }
            return n == 0 ? -1f : (float)ok / n;
        }
    }

    private int CountType(string type)
    {
        int n = 0; foreach (var e in events) if (e.EventType == type) n++; return n;
    }

    private float MeanOverType(string type, Func<EFEventRecord, float> sel)
    {
        float sum = 0f; int n = 0;
        foreach (var e in events) if (e.EventType == type) { sum += sel(e); n++; }
        return n == 0 ? 0f : sum / n;
    }

    private float Mean(Func<EFEventRecord, float> sel)
    {
        if (events.Count == 0) return 0f;
        float sum = 0f;
        foreach (var e in events) sum += sel(e);
        return sum / events.Count;
    }
}
