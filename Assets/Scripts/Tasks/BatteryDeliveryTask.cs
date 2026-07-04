using System.Collections;
using UnityEngine;

/// <summary>
/// Life-Support mission: the player retrieves a power cell from the central
/// storage rack and installs it into the Life Support console socket.
///
/// Flow:
///   Activate    -> spawn a power cell at the storage rack, show the objective
///                  HUD, play the request voice; the soft power timer drains.
///   Pick up (E) -> objective updates to "carry to Life Support".
///   Console (E) -> opens a rotating Plan/Organize puzzle (PlanningPuzzlePanel).
///   Puzzle done -> cell installs -> Success.
///   Timer ends  -> Fail (the puzzle force-closes if open).
///
/// Physical task: there is no docked canvas — the player walks, carries, then
/// plans the power-up in a screen-space overlay (one of several variants). Plan
/// errors, panel time and open count are reported as Plan/Organize metrics
/// alongside delivery time.
/// Resolving still fires MissionTask.OnTaskResolved so SessionManager logs it.
/// </summary>
public class BatteryDeliveryTask : MissionTask
{
    private const float PowerDrainSeconds = 100f;

    private GameObject battery;
    private CarryableBattery carryable;
    private BatterySocket socket;
    private BatteryMissionHUD hud;
    private IPlanningPuzzle puzzle;     // rotating Plan/Organize variant
    private GameObject puzzleGo;
    private string puzzleVariant = "";
    private bool pickedUp;
    private bool finished;
    private bool lowWarningPlayed;

    // The delivery's minimum window IS its power drain — EF hand-back must
    // never clamp the task below the drain it visibly promises the player.
    public override float MinResponseWindowSeconds => PowerDrainSeconds;

    private void Awake()
    {
        TaskName = "Power Cell";
        priority = TaskPriority.Critical;
        timeLimit = PowerDrainSeconds;
    }

    public override void Activate()
    {
        base.Activate();

        socket = FindAnyObjectByType<BatterySocket>();
        var rack = FindAnyObjectByType<BatteryStorageRack>();

        Vector3 spawnPos = rack != null ? rack.SpawnPosition : new Vector3(0f, 1f, 5f);
        Quaternion spawnRot = rack != null ? rack.SpawnRotation : Quaternion.identity;

        GameObject prefab = Resources.Load<GameObject>("BatteryCell");
        if (prefab != null)
        {
            battery = Instantiate(prefab, spawnPos, spawnRot);
            battery.name = "PowerCell_Active";
            carryable = battery.GetComponent<CarryableBattery>();
            if (carryable == null) carryable = battery.AddComponent<CarryableBattery>();
            carryable.OnPickedUp += HandlePickedUp;
        }
        else
        {
            Debug.LogWarning("[BatteryDeliveryTask] BatteryCell prefab missing from Resources.");
        }

        if (socket != null)
        {
            socket.Arm(carryable, HandleInstallRequested);
            socket.SetHintText("[E]  INSTALL CELL & ROUTE POWER");
            socket.OnBatteryInstalled += HandleInstalled;
        }
        else
        {
            Debug.LogWarning("[BatteryDeliveryTask] No BatterySocket found in the scene.");
        }

        var hudGO = new GameObject("BatteryMissionHUD");
        hud = hudGO.AddComponent<BatteryMissionHUD>();
        hud.SetObjective("LIFE SUPPORT CRITICAL  —  STEP 1 OF 3:  GO TO STORAGE, PRESS [E] TO TAKE A POWER CELL");
        hud.SetPower(1f);

        StationUI?.SetInstruction("LIFE SUPPORT: awaiting power cell");

        AudioManager.Instance.PlaySfx("battery_alarm");
        AudioManager.Instance.PlayVoice("battery_request");

        SessionManager.Instance?.LogCustomEvent("Battery_Spawned", StationName,
            "rack=" + (rack != null));
    }

    // Physical task — no docked canvas, so these are intentionally inert.
    public override void OnPlayerEnter() { }
    public override void OnPlayerExit() { }

    protected override void Update()
    {
        base.Update(); // base handles timeLimit -> HandleExpiry
        if (!IsActive || finished) return;

        float frac = Mathf.Clamp01(1f - (Time.time - SpawnTime) / PowerDrainSeconds);
        if (hud != null) hud.SetPower(frac);
        // The puzzle's dim layer covers the HUD bar, so the drain is mirrored
        // inside the panel — the time pressure stays visible while wiring.
        if (puzzle != null) puzzle.SetPower(frac);

        if (!lowWarningPlayed && frac <= 0.25f)
        {
            lowWarningPlayed = true;
            AudioManager.Instance.PlaySfx("battery_alarm");
        }
    }

    private void HandlePickedUp(CarryableBattery b)
    {
        if (pickedUp) return;
        pickedUp = true;
        MarkEngaged(); // picking up the cell is the engagement point for this task
        float latency = Time.time - SpawnTime;
        if (hud != null)
            hud.SetObjective("STEP 2 OF 3:  CARRY THE CELL TO THE LIFE SUPPORT CONSOLE  —  PRESS [E] THERE");
        SessionManager.Instance?.LogCustomEvent("Battery_PickedUp", StationName,
            "t=" + latency.ToString("F2"));
        AssessmentResults.Report(this, ("pickupLatencyS", Num.F2(latency)));
    }

    /// <summary>E at the socket while carrying: open the wiring puzzle instead
    /// of installing. The install happens in HandlePuzzleSolved.</summary>
    private void HandleInstallRequested(CarryableBattery b)
    {
        if (finished) return;

        if (hud != null)
            hud.SetObjective("STEP 3 OF 3:  FOLLOW THE PLAN TO ROUTE POWER AND RESTORE LIFE SUPPORT");

        if (puzzle == null)
        {
            puzzleGo = new GameObject("PlanningPuzzle");
            // Rotate a different Plan/Organize puzzle each delivery (weighted so
            // Sequence-with-dependencies is the primary one) — the player plans,
            // never memorizes one layout.
            float r = UnityEngine.Random.value;
            string voice;
            if (r < 0.5f)
            {
                puzzle = puzzleGo.AddComponent<SequenceDependencyPuzzle>();
                puzzleVariant = "SequenceDependencies";
                voice = "plan_sequence";
            }
            else if (r < 0.75f)
            {
                puzzle = puzzleGo.AddComponent<ResourceAllocationPuzzle>();
                puzzleVariant = "ResourceAllocation";
                voice = "plan_allocate";
            }
            else
            {
                puzzle = puzzleGo.AddComponent<RoutePowerPuzzle>();
                puzzleVariant = "RoutePower";
                voice = "plan_route";
            }
            puzzle.ValidWhile = () => !finished && carryable != null && carryable.IsCarried;
            puzzle.OnSolved += HandlePuzzleSolved;
            puzzle.OnClosed += HandlePanelClosed;
            puzzle.OnError += HandlePlanError;
            // VO must match the CURRENT objective — each variant has its own line
            // (silent until a clip is recorded), never the stale "connect wires".
            AudioManager.Instance.PlayVoice(voice);
            SessionManager.Instance?.LogCustomEvent("Battery_PanelOpened", StationName, "first=true variant=" + puzzleVariant);
        }
        else
        {
            SessionManager.Instance?.LogCustomEvent("Battery_PanelOpened", StationName, "reopen=true");
        }

        puzzle.Show();
    }

    private void HandlePlanError()
    {
        SessionManager.Instance?.LogCustomEvent("Battery_PlanError", StationName,
            "n=" + (puzzle != null ? puzzle.ErrorCount : 0));
    }

    private void HandlePanelClosed(bool autoClosed)
    {
        SessionManager.Instance?.LogCustomEvent("Battery_PanelClosed", StationName,
            autoClosed ? "auto" : "esc");
    }

    private void HandlePuzzleSolved()
    {
        if (finished) return;
        SessionManager.Instance?.LogCustomEvent("Battery_PlanSolved", StationName,
            "variant=" + puzzleVariant + " errors=" + puzzle.ErrorCount + " t=" + Num.F2(puzzle.ActiveTimeS) +
            " moves=" + puzzle.MoveCount + " backtracks=" + puzzle.BacktrackCount +
            " firstMove=" + (puzzle.FirstMoveLatencyS >= 0f ? Num.F2(puzzle.FirstMoveLatencyS) : "NA"));
        socket.CompleteInstall(); // -> OnBatteryInstalled -> HandleInstalled -> CoSucceed
    }

    private void HandleInstalled(CarryableBattery b)
    {
        if (finished) return;
        finished = true;
        StartCoroutine(CoSucceed());
    }

    private IEnumerator CoSucceed()
    {
        AssessmentResults.Report(this,
            ("outcome", "Delivered"),
            ("deliveredTimeS", Num.F2(Time.time - SpawnTime)),
            ("timeLimitS", Num.F0(timeLimit)));
        ReportWiringMetrics();
        if (hud != null)
        {
            hud.SetPower(1f);
            hud.Flash("POWER CELL INSTALLED - LIFE SUPPORT STABLE", new Color(0.35f, 1f, 0.5f));
        }
        AudioManager.Instance.PlaySfx("power_restore");
        AudioManager.Instance.PlaySfx("success_chime");
        AudioManager.Instance.PlayVoice("battery_installed");
        StationUI?.SetInstruction("LIFE SUPPORT: nominal");
        yield return new WaitForSeconds(2.4f);
        Resolve(TaskResult.Success);
    }

    protected override void HandleExpiry()
    {
        if (finished) return;
        // Never picked up = never engaged -> neutral NotInitiated, not a Fail.
        if (!pickedUp) { base.HandleExpiry(); return; }
        finished = true;
        // Restore controls/cursor before the fail flow if the player timed out
        // with the wiring panel still up. ForceClose skips OnClosed reentry.
        if (puzzle != null && puzzle.IsOpen) puzzle.ForceClose();
        StartCoroutine(CoFail());
    }

    private IEnumerator CoFail()
    {
        AssessmentResults.Report(this,
            ("outcome", "Timed out"),
            ("timeLimitS", Num.F0(timeLimit)));
        ReportWiringMetrics();
        if (hud != null)
        {
            hud.SetPower(0f);
            hud.Flash("LIFE SUPPORT FAILURE - CELL NOT INSTALLED IN TIME", new Color(1f, 0.35f, 0.3f));
        }
        AudioManager.Instance.PlaySfx("fail_buzz");
        AudioManager.Instance.PlayVoice("battery_failed");
        yield return new WaitForSeconds(2.4f);
        Resolve(TaskResult.Fail);
    }

    // Planning-puzzle metrics ride along on both outcomes — the panel may have been
    // opened (and errors made) even when the task ultimately timed out.
    private void ReportWiringMetrics()
    {
        AssessmentResults.Report(this,
            ("puzzleVariant", puzzleVariant),
            ("wiresTotal", puzzle != null ? puzzle.StepCount.ToString() : "0"),
            ("wireErrorsCount", puzzle != null ? puzzle.ErrorCount.ToString() : "0"),
            ("puzzleTimeS", Num.F2(puzzle != null ? puzzle.ActiveTimeS : 0f)),
            ("panelOpens", puzzle != null ? puzzle.OpenCount.ToString() : "0"),
            // Granular planning metrics: attempted placements, undone/changed
            // actions (uncertainty), and open->first-click latency (proactive
            // planning vs impulsive trial-and-error). NA = panel never interacted.
            ("moveCount", puzzle != null ? puzzle.MoveCount.ToString() : "0"),
            ("backtrackCount", puzzle != null ? puzzle.BacktrackCount.ToString() : "0"),
            ("firstMoveLatencyS", puzzle != null && puzzle.FirstMoveLatencyS >= 0f
                ? Num.F2(puzzle.FirstMoveLatencyS) : "NA"));
    }

    private void OnDestroy()
    {
        if (carryable != null) carryable.OnPickedUp -= HandlePickedUp;
        if (socket != null)
        {
            socket.OnBatteryInstalled -= HandleInstalled;
            socket.Disarm();
        }
        // The panel's own OnDestroy restores the player if it is somehow
        // still open when the task is torn down.
        if (puzzleGo != null) Destroy(puzzleGo);
        if (hud != null) Destroy(hud.gameObject);
        // An installed cell is owned by the socket and cleared when the next
        // mission arms it; only destroy a cell that never reached the socket.
        if (battery != null && (carryable == null || !carryable.IsInstalled))
            Destroy(battery);
    }
}
