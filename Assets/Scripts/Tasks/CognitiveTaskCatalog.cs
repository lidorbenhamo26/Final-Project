using UnityEngine;

/// <summary>
/// Maps a station name to the cognitive MissionTask component that should be
/// attached to a freshly-created task GameObject. Keeps GameManager's spawn
/// loop free of direct references to specific task subclasses.
/// </summary>
public static class CognitiveTaskCatalog
{
    public static MissionTask CreateTaskForStation(GameObject host, string stationName)
    {
        return stationName switch
        {
            "EngineStation"      => host.AddComponent<WorkingMemoryTask>(),
            "NavigationStation"  => host.AddComponent<RadarScanTask>(),
            "CommsStation"       => host.AddComponent<InhibitTask>(),
            "LifeSupportStation" => host.AddComponent<NBackTask>(),
            _                    => host.AddComponent<EngineTask>(),
        };
    }
}
