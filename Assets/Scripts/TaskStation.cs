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

    private void Start() => stationUI?.SetIdle();

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

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        currentTask?.OnPlayerEnter();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        currentTask?.OnPlayerExit();
    }

    public bool HasActiveTask() => currentTask != null && currentTask.IsActive;
}
