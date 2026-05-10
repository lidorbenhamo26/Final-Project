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

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
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
