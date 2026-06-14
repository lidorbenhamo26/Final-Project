using UnityEngine;

/// <summary>
/// Maps a station name to the cognitive MissionTask component that should be
/// attached to a freshly-created task GameObject. Keeps GameManager's spawn
/// loop free of direct references to specific task subclasses.
/// </summary>
public static class CognitiveTaskCatalog
{
    // Some stations alternate two task variants that measure the SAME BRIEF-A
    // scale (so the report is unaffected): Engine = Working Memory <-> Code Memory
    // (both WorkingMemory scale), Comms = Stroop <-> Go/No-Go (both Inhibit scale).
    // `variant` is an alternation counter from the spawner; odd = second variant.
    // variant 0 always yields the original task, so 2-arg callers are unchanged.
    public static MissionTask CreateTaskForStation(GameObject host, string stationName, int variant = 0)
    {
        bool alt = (variant % 2) == 1;
        switch (stationName)
        {
            case "EngineStation":      return alt ? host.AddComponent<CodeMemoryTask>() : host.AddComponent<WorkingMemoryTask>();
            case "NavigationStation":  return host.AddComponent<RadarScanTask>();
            case "CommsStation":       return alt ? host.AddComponent<InhibitTask>()    : host.AddComponent<StroopTask>();
            case "LifeSupportStation": return host.AddComponent<BatteryDeliveryTask>();
            default:                   return host.AddComponent<EngineTask>();
        }
    }
}
