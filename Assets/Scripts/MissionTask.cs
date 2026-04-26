using System;
using UnityEngine;

public enum TaskPriority { Critical, NonCritical }
public enum TaskResult { Success, Fail, Omission, Commission }

public abstract class MissionTask : MonoBehaviour
{
    public static event Action<MissionTask> OnTaskSpawned;
    public static event Action<MissionTask, TaskResult, float> OnTaskResolved;

    [SerializeField] public TaskPriority priority = TaskPriority.NonCritical;
    [SerializeField] public float timeLimit = 30f;

    public string TaskName { get; protected set; }
    public string StationName { get; set; }
    public TaskPriority Priority { get { return priority; } }
    public bool IsActive { get; private set; }
    public float SpawnTime { get; private set; }

    protected StationUI StationUI { get; private set; }

    public void SetStationUI(StationUI ui)
    {
        StationUI = ui;
    }

    public virtual void Activate()
    {
        IsActive = true;
        SpawnTime = Time.time;
        OnTaskSpawned?.Invoke(this);
    }

    public abstract void OnPlayerEnter();
    public abstract void OnPlayerExit();

    protected virtual void Update()
    {
        if (!IsActive) return;
        if (Time.time - SpawnTime >= timeLimit)
        {
            HandleExpiry();
        }
    }

    protected virtual void HandleExpiry()
    {
        Resolve(TaskResult.Omission);
    }

    protected void Resolve(TaskResult result)
    {
        if (!IsActive) return;
        IsActive = false;
        float reactionTime = Time.time - SpawnTime;
        OnTaskResolved?.Invoke(this, result, reactionTime);
        Destroy(gameObject, 0.1f);
    }
}
