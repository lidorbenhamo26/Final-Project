using System;
using UnityEngine;

public enum TaskPriority { Critical, NonCritical }
// NotInitiated is a NEUTRAL behavioral outcome (the player never engaged the task),
// not a cognitive failure. It is logged for executive-function analysis but does
// not produce a cognitive record / scale entry. Appended last to keep the
// serialized values of the existing members stable.
public enum TaskResult { Success, Fail, Omission, Commission, NotInitiated }

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

    // True once the player actually starts the task (docks / picks up the cell).
    // Drives the expiry outcome: an un-engaged task that times out is NotInitiated
    // (neutral) rather than a cognitive Omission failure.
    public bool Engaged { get; private set; }
    protected void MarkEngaged() { Engaged = true; }

    // --- Executive-function offer fields (set by EFEventDirector during an EF
    // event). They change only how the task is PRESENTED/scored for prioritization;
    // the task's own cognition is unchanged. EfTier: 0=critical(red), 1=medium
    // (yellow), 2=low(green). EfDeadline: urgency seconds for the Priority Protocol
    // + HUD countdown. EfOrder: player-assigned execution order (0 = unset).
    public bool EfOffered;
    public int EfTier;
    public float EfDeadline;
    public int EfOrder;
    // True while this task is the un-answered interrupt of an Interruption event:
    // the task list tags it "NEW!" instead of an order number, so the player sees
    // an unresolved demand, not a pre-assigned rank. Cleared once they switch.
    public bool EfInterrupt;
    // Stated in-fiction reason for this task's tier (e.g. "Crew oxygen dropping"),
    // shown on the Priority Beat card so the player's ordering is an INFORMED
    // executive decision (the learnable Priority Protocol), not a color guess.
    public string EfReason;

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
        // Never engaged -> neutral NotInitiated; engaged but ran out of time -> a
        // real cognitive Omission.
        Resolve(Engaged ? TaskResult.Omission : TaskResult.NotInitiated);
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
