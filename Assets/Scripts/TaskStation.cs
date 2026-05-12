using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TaskStation : MonoBehaviour
{
    [SerializeField] public string stationName = "Station";
    [SerializeField] private StationUI stationUI;

    private MissionTask currentTask;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Start()
    {
        stationUI?.SetIdle();
        // Hide the world-space station info panel permanently — its decorative
        // squares (status light, LED, progress-bar background) read as floating
        // white shapes on the console, and every piece of info it carries is
        // already shown on the top-left HUD.
        stationUI?.Hide();
    }

    public void AssignTask(MissionTask task)
    {
        currentTask = task;
        task.StationName = stationName;
        task.SetStationUI(stationUI);
        task.transform.SetParent(transform);
        MissionTask.OnTaskResolved += OnTaskResolved;
        task.Activate();
    }

    private void OnTaskResolved(MissionTask task, TaskResult result, float rt)
    {
        if (task != currentTask) return;
        MissionTask.OnTaskResolved -= OnTaskResolved;
        currentTask = null;
        stationUI?.SetIdle();
    }

    public bool HasActiveTask() => currentTask != null && currentTask.IsActive;

    public MissionTask CurrentTask => currentTask;

    public StationUI UI => stationUI;
}
