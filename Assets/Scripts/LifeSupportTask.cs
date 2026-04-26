using UnityEngine;

public class LifeSupportTask : MissionTask
{
    private void Awake()
    {
        TaskName = "Life Support Override";
        priority = TaskPriority.Critical;
        timeLimit = 10f;
    }

    public override void Activate()
    {
        base.Activate();
        StationUI?.SetUrgent(true);
        StationUI?.SetInstruction("!! LIFE SUPPORT CRITICAL !!");
    }

    protected override void Update()
    {
        if (!IsActive) return;
        float remaining = timeLimit - (Time.time - SpawnTime);
        StationUI?.SetInstruction("!! LIFE SUPPORT: " + remaining.ToString("F1") + "s !!");
        base.Update();
    }

    protected override void HandleExpiry()
    {
        Debug.LogWarning("[MissionFocus] MISSED EVENT: Life Support task expired!");
        Resolve(TaskResult.Omission);
    }

    public override void OnPlayerEnter()
    {
        if (IsActive) Resolve(TaskResult.Success);
    }

    public override void OnPlayerExit() { }
}
