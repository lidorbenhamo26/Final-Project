using UnityEngine;

public class EngineTask : MissionTask
{
    [SerializeField] private float holdDuration = 5f;

    private bool playerPresent;
    private float holdTimer;

    private void Awake()
    {
        TaskName = "Engine Activation";
        priority = TaskPriority.Critical;
        timeLimit = 60f;
    }

    public override void Activate()
    {
        base.Activate();
        StationUI?.SetInstruction("HOLD at console to activate engine (5s)");
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive || !playerPresent) return;

        holdTimer += Time.deltaTime;
        StationUI?.SetProgress(holdTimer / holdDuration);

        if (holdTimer >= holdDuration)
        {
            Resolve(TaskResult.Success);
        }
    }

    public override void OnPlayerEnter()
    {
        if (!IsActive) return;
        playerPresent = true;
        StationUI?.SetInstruction("HOLD... activating engine");
    }

    public override void OnPlayerExit()
    {
        playerPresent = false;
        holdTimer = 0f;
        StationUI?.SetProgress(0f);
        StationUI?.SetInstruction("HOLD at console to activate engine (5s)");
    }
}
