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
///   Console (E) -> opens the wire-matching puzzle (WiringPuzzlePanel).
///   Puzzle done -> cell installs -> Success.
///   Timer ends  -> Fail (the puzzle force-closes if open).
///
/// Physical task: there is no docked canvas — the player walks, carries, then
/// routes wires in a screen-space overlay. Wiring errors, panel time and
/// open count are reported as Plan/Organize metrics alongside delivery time.
/// Resolving still fires MissionTask.OnTaskResolved so SessionManager logs it.
/// </summary>
public class BatteryDeliveryTask : MissionTask
{
    private const float PowerDrainSeconds = 100f;
    private const int WiringWireCount = 4;

    private GameObject battery;
    private CarryableBattery carryable;
    private BatterySocket socket;
    private BatteryMissionHUD hud;
    private WiringPuzzlePanel puzzle;
    private bool pickedUp;
    private bool finished;
    private bool lowWarningPlayed;

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
            socket.SetHintText("[E] OPEN WIRE PANEL");
            socket.OnBatteryInstalled += HandleInstalled;
        }
        else
        {
            Debug.LogWarning("[BatteryDeliveryTask] No BatterySocket found in the scene.");
        }

        var hudGO = new GameObject("BatteryMissionHUD");
        hud = hudGO.AddComponent<BatteryMissionHUD>();
        hud.SetObjective("LIFE SUPPORT FAILING - TAKE A POWER CELL FROM STORAGE");
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
            hud.SetObjective("CARRY THE CELL TO LIFE SUPPORT  -  [E] AT THE CONSOLE TO OPEN THE WIRE PANEL");
        SessionManager.Instance?.LogCustomEvent("Battery_PickedUp", StationName,
            "t=" + latency.ToString("F2"));
        AssessmentResults.Report(this, ("pickupLatencyS", Num.F2(latency)));
    }

    /// <summary>E at the socket while carrying: open the wiring puzzle instead
    /// of installing. The install happens in HandlePuzzleSolved.</summary>
    private void HandleInstallRequested(CarryableBattery b)
    {
        if (finished) return;

        if (puzzle == null)
        {
            var go = new GameObject("WiringPuzzlePanel");
            puzzle = go.AddComponent<WiringPuzzlePanel>();
            puzzle.WireCount = WiringWireCount;
            puzzle.ValidWhile = () => !finished && carryable != null && carryable.IsCarried;
            puzzle.OnSolved += HandlePuzzleSolved;
            puzzle.OnWireError += HandleWireError;
            puzzle.OnClosed += HandlePanelClosed;
            AudioManager.Instance.PlayVoice("battery_wiring");
            SessionManager.Instance?.LogCustomEvent("Battery_PanelOpened", StationName, "first=true");
        }
        else
        {
            SessionManager.Instance?.LogCustomEvent("Battery_PanelOpened", StationName, "reopen=true");
        }

        puzzle.Show();
    }

    private void HandleWireError(int wireIdx, int socketIdx)
    {
        SessionManager.Instance?.LogCustomEvent("Battery_WireError", StationName,
            "wire=" + wireIdx + " socket=" + socketIdx + " n=" + puzzle.ErrorCount);
    }

    private void HandlePanelClosed(bool autoClosed)
    {
        SessionManager.Instance?.LogCustomEvent("Battery_PanelClosed", StationName,
            autoClosed ? "auto" : "esc");
    }

    private void HandlePuzzleSolved()
    {
        if (finished) return;
        SessionManager.Instance?.LogCustomEvent("Battery_WiringSolved", StationName,
            "errors=" + puzzle.ErrorCount + " t=" + Num.F2(puzzle.ActiveTimeS));
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
        if (puzzle != null && WiringPuzzlePanel.IsOpen) puzzle.ForceClose();
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

    // Wiring metrics ride along on both outcomes — the panel may have been
    // opened (and errors made) even when the task ultimately timed out.
    private void ReportWiringMetrics()
    {
        AssessmentResults.Report(this,
            ("wiresTotal", puzzle != null ? puzzle.WireCount.ToString() : "0"),
            ("wireErrorsCount", puzzle != null ? puzzle.ErrorCount.ToString() : "0"),
            ("puzzleTimeS", Num.F2(puzzle != null ? puzzle.ActiveTimeS : 0f)),
            ("panelOpens", puzzle != null ? puzzle.OpenCount.ToString() : "0"));
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
        if (puzzle != null) Destroy(puzzle.gameObject);
        if (hud != null) Destroy(hud.gameObject);
        // An installed cell is owned by the socket and cleared when the next
        // mission arms it; only destroy a cell that never reached the socket.
        if (battery != null && (carryable == null || !carryable.IsInstalled))
            Destroy(battery);
    }
}
