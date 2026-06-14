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

    /// <summary>
    /// Minimum response window (seconds) the difficulty spawner must never scale
    /// the time limit below — e.g. a task with a fixed internal duration that
    /// would otherwise become impossible to finish in time. 0 = no per-task floor.
    /// </summary>
    public virtual float MinResponseWindowSeconds => 0f;

    public string TaskName { get; protected set; }
    public string StationName { get; set; }
    public TaskPriority Priority { get { return priority; } }
    public bool IsActive { get; private set; }
    public float SpawnTime { get; private set; }

    protected StationUI StationUI { get; private set; }

    // Set by a task the moment its outcome is computed (metrics reported,
    // celebration UI pending). Blocks the base time-limit expiry from racing
    // the deferred Resolve and overwriting a real result with Omission.
    protected bool ResolutionPending;

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
        if (GameManager.IsDebugFrozen)
        {
            // Slide the start point forward at real time so elapsed (and any
            // derived drain that reads Time.time - SpawnTime) stays frozen.
            SpawnTime += Time.deltaTime;
            return;
        }
        if (ResolutionPending) return;
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
