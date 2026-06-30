using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central "operating a ship" status layer. Tracks each station's system state
/// (STABLE / ACTIVE / CRITICAL) and an overall alert level, so the HUD station
/// strip and the end-of-mission summary can present the mission as running a ship
/// rather than isolated cognitive tests.
///
/// State is driven automatically from task spawn/resolve, plus an explicit
/// <see cref="SetCritical"/> raised by the Priority Beat consequence (a
/// deprioritized critical system). A station recovers to STABLE when its task
/// resolves. Pure model + events — no UI here.
/// </summary>
public enum ShipSystemState { Stable, Active, Critical }

[DisallowMultipleComponent]
public class ShipState : MonoBehaviour
{
    public static ShipState Instance { get; private set; }

    public event Action Changed;

    private static readonly string[] Stations =
        { "EngineStation", "NavigationStation", "CommsStation", "LifeSupportStation" };

    private readonly Dictionary<string, ShipSystemState> state = new Dictionary<string, ShipSystemState>();

    /// <summary>How many systems went critical this mission, and how many of those
    /// the player stabilized — feeds the player-facing end summary.</summary>
    public int CriticalsRaised { get; private set; }
    public int CriticalsStabilized { get; private set; }

    public static ShipState GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("ShipState");
        Instance = go.AddComponent<ShipState>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        foreach (var s in Stations) state[s] = ShipSystemState.Stable;
    }

    private void OnEnable()
    {
        MissionTask.OnTaskSpawned += HandleSpawn;
        MissionTask.OnTaskResolved += HandleResolved;
    }

    private void OnDisable()
    {
        MissionTask.OnTaskSpawned -= HandleSpawn;
        MissionTask.OnTaskResolved -= HandleResolved;
        if (Instance == this) Instance = null;
    }

    private void HandleSpawn(MissionTask task)
    {
        if (task == null || string.IsNullOrEmpty(task.StationName)) return;
        // A spawn lights the system ACTIVE (unless it's already CRITICAL).
        if (Get(task.StationName) != ShipSystemState.Critical)
            Set(task.StationName, ShipSystemState.Active);
    }

    private void HandleResolved(MissionTask task, TaskResult result, float rt)
    {
        if (task == null || string.IsNullOrEmpty(task.StationName)) return;
        if (Get(task.StationName) == ShipSystemState.Critical && result != TaskResult.NotInitiated)
        {
            CriticalsStabilized++;
            if (result == TaskResult.Success)
                NotificationFeed.Instance?.Push(
                    TaskListHUD.PrettyStation(task.StationName).ToUpperInvariant() + " stabilized — crew safe");
        }
        Set(task.StationName, ShipSystemState.Stable);
    }

    /// <summary>Raise a system to CRITICAL (the Priority Beat consequence).</summary>
    public void SetCritical(string stationName)
    {
        if (string.IsNullOrEmpty(stationName)) return;
        if (Get(stationName) == ShipSystemState.Critical) return;
        CriticalsRaised++;
        Set(stationName, ShipSystemState.Critical);
        if (CriticalCount() >= 2)
            NotificationFeed.Instance?.Push("! Multiple systems critical — prioritize!");
    }

    public ShipSystemState Get(string stationName)
        => state.TryGetValue(stationName, out var s) ? s : ShipSystemState.Stable;

    public int CriticalCount()
    {
        int n = 0;
        foreach (var kv in state) if (kv.Value == ShipSystemState.Critical) n++;
        return n;
    }

    private void Set(string stationName, ShipSystemState s)
    {
        if (Get(stationName) == s) return;
        state[stationName] = s;
        Changed?.Invoke();
    }

    /// <summary>Overall alert level — escalates with mission difficulty, overridden
    /// to CRITICAL while any system is critical. Makes the load ramp read as
    /// intentional escalation.</summary>
    public string AlertLevel
    {
        get
        {
            if (CriticalCount() > 0) return "CRITICAL";
            float d = GameManager.Instance != null ? GameManager.Instance.CurrentDifficulty : 0f;
            if (d >= 0.60f) return "HIGH";
            if (d >= 0.25f) return "ELEVATED";
            return "NOMINAL";
        }
    }

    public Color AlertColor
    {
        get
        {
            switch (AlertLevel)
            {
                case "CRITICAL": return new Color(0.937f, 0.267f, 0.267f, 1f);
                case "HIGH":     return new Color(0.98f, 0.55f, 0.20f, 1f);
                case "ELEVATED": return new Color(0.98f, 0.80f, 0.10f, 1f);
                default:         return new Color(0.30f, 0.80f, 0.45f, 1f);
            }
        }
    }

    public static Color StateColor(ShipSystemState s)
    {
        switch (s)
        {
            case ShipSystemState.Critical: return new Color(0.937f, 0.267f, 0.267f, 1f);
            case ShipSystemState.Active:   return new Color(0.22f, 0.74f, 0.97f, 1f);
            default:                       return new Color(0.30f, 0.70f, 0.45f, 1f);
        }
    }

    public static string StateWord(ShipSystemState s)
    {
        switch (s)
        {
            case ShipSystemState.Critical: return "CRITICAL";
            case ShipSystemState.Active:   return "ACTIVE";
            default:                       return "STABLE";
        }
    }
}
