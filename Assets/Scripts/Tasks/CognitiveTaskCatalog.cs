using UnityEngine;

/// <summary>
/// Maps a station name to the cognitive MissionTask component that should be
/// attached to a freshly-created task GameObject. Keeps GameManager's spawn
/// loop free of direct references to specific task subclasses.
/// </summary>
public static class CognitiveTaskCatalog
{
    // Stations alternate two variants that measure the SAME BRIEF-A scale (so the
    // report is unaffected): Engine = Working Memory in two retention modes
    // (Ambient code->walk->enter / Dock->distractor->recall); Comms = Stroop <->
    // Go/No-Go (both Inhibit). `variant` is an alternation counter from the spawner;
    // odd = second variant. variant 0 yields the original, so 2-arg callers are
    // unchanged (and the WM+Prioritization EF event forces variant 0 = Working Memory).
    public static MissionTask CreateTaskForStation(GameObject host, string stationName, int variant = 0)
    {
        bool alt = (variant % 2) == 1;
        switch (stationName)
        {
            case "EngineStation":
            {
                var wm = host.AddComponent<WorkingMemoryTask>();
                wm.SetMode(alt ? WorkingMemoryTask.Mode.DockDistractor : WorkingMemoryTask.Mode.Ambient);
                return wm;
            }
            case "NavigationStation":  return host.AddComponent<RadarScanTask>();
            case "CommsStation":       return alt ? host.AddComponent<InhibitTask>()    : host.AddComponent<StroopTask>();
            case "LifeSupportStation": return host.AddComponent<BatteryDeliveryTask>();
            default:                   return host.AddComponent<EngineTask>();
        }
    }
}
