using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives in-flow Executive-Function events. v1 / Phase 1 = the Triage event:
/// 2-3 tasks are offered at once on the normal HUD (priority color + deadline);
/// a short decision window lets the player set the execution order (number keys
/// 1-3); they then perform all of them in that order. The PRIORITIZATION decision
/// is logged separately from each task's cognition, so the two signals stay clean.
///
/// Priority Protocol (the transparent "optimal", taught in the tutorial):
///   1. Most critical first (red -> yellow -> green).
///   2. Ties broken by the nearer deadline.
/// Optimal order = sort by (tier asc, then deadline asc). Out-of-order ordering
/// only lowers the prioritization score (option a) — it never deletes a task.
///
/// Runs as a coroutine the GameManager spawn loop yields on, so the baseline
/// single-task flow is suspended for the duration of the event.
/// </summary>
[DisallowMultipleComponent]
public class EFEventDirector : MonoBehaviour
{
    // Generous, readable decision window for the held priority beat (code-driven so
    // it stays generous regardless of older serialized scene values).
    private const float BeatWindow = 9f;
    // Faster window for the lightweight 2-task "micro" prioritization beat — a quick
    // "which first?" call that adds many small prioritization data points.
    private const float MicroWindow = 5f;
    [SerializeField, Tooltip("Safety: if offered tasks are never engaged, end the event after this long so the mission resumes. Kept short so an abandoned beat can't eat the mission.")]
    private float eventSafetyTimeout = 60f;
    [Header("Interruption event")]
    [SerializeField, Tooltip("Window (seconds) after the interrupt appears to count an undock+switch as a deliberate switch. The current task's own clock keeps running during this window — the interruption never pauses it. A later switch still counts; this is just the 'fast/deliberate' threshold and how long the prompt holds.")]
    private float switchWindow = 6f;
    [SerializeField, Tooltip("Delay after the player engages the first task before the interrupt fires. Kept short so the interrupt reliably lands WHILE the first task is still in progress.")]
    private float interruptDelay = 1.5f;
    [SerializeField, Tooltip("How long to wait for the player to engage the first task before abandoning the interruption event.")]
    private float engageTimeout = 45f;

    public bool EventActive { get; private set; }

    private GameManager gm;
    private int typeCounter;
    private bool firstBeatExplained;

    private void Awake() { gm = GetComponent<GameManager>(); }

    // Round-robin across the enabled EF event types for balanced behavioral data.
    public IEnumerator RunNextEvent(List<TaskStation> eligible)
    {
        // One-time explanation before the player's very first beat (always a Triage,
        // since the counter starts at 0), so they know what to do.
        if (!firstBeatExplained) { firstBeatExplained = true; yield return ShowFirstBeatIntro(); }

        // Explicit ordering (not a plain N%k round-robin) so the variety is FRONT-
        // LOADED: even a short session sees Triage, then a mid-task Interruption,
        // then a Micro beat within the first few events. Interruption/Micro recur
        // often so prioritization stays central. 0=Triage 1=Interruption 2=WM+Prio 3=Micro
        int type = EventOrder[typeCounter % EventOrder.Length];
        typeCounter++;
        if (type == 0) yield return RunTriage(eligible);
        else if (type == 1) yield return RunInterruption(eligible);
        else if (type == 2) yield return RunWmPrioritization(eligible);
        else yield return RunMicroPriority(eligible);
    }

    private static readonly int[] EventOrder = { 0, 1, 3, 1, 2, 3, 1 };

    // Lightweight, fast 2-task prioritization beat: same "pick the most critical
    // first" decision as Triage, but always exactly two offers and a shorter window.
    // Adds frequent, small prioritization data points without the full ceremony.
    public IEnumerator RunMicroPriority(List<TaskStation> eligible)
    {
        yield return RunTriageCore(eligible, 2, 2, MicroWindow, "MicroTriage");
    }

    private class DecisionResult
    {
        public List<int> Order = new List<int>();
        public float Latency = -1f;
        public int ChangedMind;
        public bool NoChoice;
    }

    // The single executive decision: pick the ONE highest-priority task. The game is
    // HELD (debug-freeze) so it's deliberate, not a scramble. The player picks by
    // CLICKING a card or pressing its number key; the chosen task becomes the next
    // objective (EfOrder = 1). Fills `res` with the single chosen index.
    private IEnumerator RunSinglePick(List<MissionTask> offers, DecisionResult res, float window)
    {
        TaskListHUD.ClearEfClicks();
        bool wasFrozen = GameManager.IsDebugFrozen;
        if (!wasFrozen) GameManager.SetDebugFrozen(true); // hold mission timers/tasks for the beat

        // Hold the player and free the cursor so they can CLICK; this also stops the
        // camera spinning while choosing. All restored on commit.
        var playerGo = GameObject.FindWithTag("Player");
        var ac = playerGo != null ? playerGo.GetComponent<AstronautController>() : null;
        bool priorControls = ac == null || ac.ControlsEnabled;
        if (ac != null) ac.ControlsEnabled = false;
        CursorLockMode prevLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        float t0 = Time.time;
        int chosen = -1;
        while (Time.time - t0 < window && chosen < 0)
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
                for (int d = 0; d < offers.Count && d < 3; d++)
                    if (DigitPressed(kb, d + 1)) { chosen = d; break; }

            if (chosen < 0)
            {
                MissionTask clicked;
                while ((clicked = TaskListHUD.DequeueEfClick()) != null)
                {
                    int idx = offers.IndexOf(clicked);
                    if (idx >= 0) { chosen = idx; break; }
                }
            }
            yield return null;
        }

        res.NoChoice = chosen < 0;
        if (chosen >= 0)
        {
            res.Order.Add(chosen);
            res.Latency = Time.time - t0;
            offers[chosen].EfOrder = 1; // the next objective
        }

        if (ac != null) ac.ControlsEnabled = priorControls;
        Cursor.lockState = prevLock;
        Cursor.visible = prevLock != CursorLockMode.Locked;
        if (!wasFrozen) GameManager.SetDebugFrozen(false);
    }

    // One-time explanation shown before the player's first priority beat.
    private IEnumerator ShowFirstBeatIntro()
    {
        var panel = PriorityBeatPanel.GetOrCreate();
        bool wasFrozen = GameManager.IsDebugFrozen;
        if (!wasFrozen) GameManager.SetDebugFrozen(true);

        var playerGo = GameObject.FindWithTag("Player");
        var ac = playerGo != null ? playerGo.GetComponent<AstronautController>() : null;
        bool priorControls = ac == null || ac.ControlsEnabled;
        if (ac != null) ac.ControlsEnabled = false;
        CursorLockMode prevLock = Cursor.lockState;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        AudioManager.Instance?.PlaySfx("alert_pulse", 0.5f);
        AudioManager.Instance?.PlayVoice("priority_intro");
        panel.ShowIntro();
        float t0 = Time.time;
        while (panel.IntroActive && Time.time - t0 < 30f) yield return null;
        panel.HideIntro();

        if (ac != null) ac.ControlsEnabled = priorControls;
        Cursor.lockState = prevLock;
        Cursor.visible = prevLock != CursorLockMode.Locked;
        if (!wasFrozen) GameManager.SetDebugFrozen(false);
    }

    public IEnumerator RunTriage(List<TaskStation> eligible)
    {
        yield return RunTriageCore(eligible, 2, 3, BeatWindow, "Triage");
    }

    // Shared body for the full Triage beat and the lightweight Micro beat.
    // minOffers..maxOffers are INCLUSIVE; window is the decision time; label is the
    // EFResults event type ("Triage" / "MicroTriage").
    private IEnumerator RunTriageCore(List<TaskStation> eligible, int minOffers, int maxOffers, float window, string label)
    {
        EventActive = true;

        // 1) Choose N distinct eligible stations.
        var pick = new List<TaskStation>(eligible);
        Shuffle(pick);
        int n = Mathf.Min(pick.Count, Random.Range(minOffers, maxOffers + 1));
        pick = pick.GetRange(0, n);

        // 2) Spawn the offers with distinct deadlines so the protocol's tie-break
        //    is meaningful, keeping each offer's station for the consequence.
        float[] deadlines = MakeDistinctDeadlines(n);
        var offers = new List<MissionTask>(n);
        var offerStations = new List<TaskStation>(n);
        for (int i = 0; i < n; i++)
        {
            var t = gm.SpawnEfOffer(pick[i], deadlines[i]);
            if (t != null) { offers.Add(t); offerStations.Add(pick[i]); }
        }
        if (offers.Count < 2) { EventActive = false; yield break; }

        // Learnable 3-tier rule: distinct tiers + a stated reason per offer, so the
        // order is an INFORMED decision (read the reason), not a color guess.
        AssignTiersAndReasons(offers, offerStations);

        // 3) The signature beat: centered, held, dimmed panel + alert + voice.
        AudioManager.Instance?.PlaySfx("alert_pulse", 0.7f);
        AudioManager.Instance?.PlayVoice("priority_event");
        var panel = PriorityBeatPanel.GetOrCreate();
        panel.ShowBeat(offers, window);
        SessionManager.Instance?.LogCustomEvent("EF_TriageOffer", "System",
            "label=" + label + " n=" + offers.Count + " " + DescribeOffers(offers));

        // 4) The decision: pick the single HIGHEST-priority task.
        int[] optimal = OptimalOrder(offers);
        var dec = new DecisionResult();
        yield return RunSinglePick(offers, dec, window);
        panel.Confirm("OBJECTIVE SET");

        int chosenIdx = dec.Order.Count > 0 ? dec.Order[0] : -1;
        int optimalIdx = optimal[0];
        bool firstWasOptimal = chosenIdx == optimalIdx;
        float optScore = firstWasOptimal ? 1f : 0f;
        string chosenStr = chosenIdx >= 0 ? offers[chosenIdx].StationName : "none";
        string optimalStr = offers[optimalIdx].StationName;

        // 5) Log the prioritization decision (separate from each task's cognition).
        SessionManager.Instance?.LogCustomEvent("EF_TriageDecision", "System",
            "chosen=" + chosenStr +
            " optimal=" + optimalStr +
            " firstWasOptimal=" + firstWasOptimal +
            " optScore=" + Num.F2(optScore) +
            " latency=" + (dec.Latency < 0f ? "NA" : Num.F2(dec.Latency)) +
            " noChoice=" + dec.NoChoice);

        // Behavioral record for the report's executive-function section.
        EFResults.Instance?.Add(new EFEventRecord
        {
            EventType = label,
            NOptions = offers.Count,
            OptionsDesc = DescribeOffers(offers),
            ChosenOrder = chosenStr,
            OptimalOrder = optimalStr,
            ChosenFirst = chosenStr,
            FirstWasOptimal = firstWasOptimal,
            WasOptimal = firstWasOptimal,
            OptScore = optScore,
            DecisionLatencyS = dec.Latency,
            NoChoice = dec.NoChoice,
        });

        RewardGoodCall(firstWasOptimal, offers, chosenIdx);

        // 6) Execute — the player performs the offered tasks. If they did NOT pick
        //    the critical one as highest priority, its station visibly goes CRITICAL
        //    (crew alarm) and recovers when that task resolves. No deletion.
        yield return new WaitForSeconds(0.8f);
        panel.HideBeat();

        // If the player did NOT pick the critical task as highest priority, its
        // station visibly goes CRITICAL (alarm); it recovers via ShipState when its
        // task resolves.
        int critIdx = CriticalIndex(offers);
        bool deprioritizedCritical = critIdx >= 0 && chosenIdx != critIdx;
        if (deprioritizedCritical) TriggerCritical(offerStations[critIdx], offers[critIdx]);

        // Hand the tasks back as normal active tasks and END the event immediately,
        // so beats stay frequent (no waiting for everything to resolve). Keep the
        // chosen objective (and any deprioritized critical, to rescue) with a fair
        // window; drop the rest so the deck clears and the next beat can schedule.
        HandBackOffers(offers, chosenIdx, deprioritizedCritical ? critIdx : -1);

        SessionManager.Instance?.LogCustomEvent("EF_TriageEnd", "System", "chosen=" + chosenStr);
        EventActive = false;
    }

    // After the single pick, return offered tasks to normal play: keep the chosen
    // objective (+ optional critical-to-rescue) with a fair window; expire the rest
    // so the deck clears quickly and the next beat can schedule.
    private static void HandBackOffers(List<MissionTask> offers, int keepA, int keepB)
    {
        for (int i = 0; i < offers.Count; i++)
        {
            var t = offers[i];
            if (t == null || !t.IsActive) continue;
            t.timeLimit = (i == keepA || i == keepB) ? 60f : 0.05f;
        }
    }

    // ------------------------------------------------------- Interruption event
    // A task is in progress; a second task barges in. Switching is OPTIMAL only if
    // the interrupt is strictly more critical. Diegetic: "switch" = undock the
    // first task and engage the interrupt (no special key). Measures Shift.
    public IEnumerator RunInterruption(List<TaskStation> eligible)
    {
        EventActive = true;
        var pick = new List<TaskStation>(eligible);
        Shuffle(pick);
        if (pick.Count < 2) { EventActive = false; yield break; }
        var stationA = pick[0];
        var stationB = pick[1];

        // 1) The in-progress task A (Important/Routine — leaves room for a more
        //    critical interrupt).
        var A = gm.SpawnEfOffer(stationA, 60f);
        if (A == null) { EventActive = false; yield break; }
        A.EfOrder = 1;
        AssignTierReason(A, stationA, Random.Range(1, 3)); // 1 or 2

        // 2) Wait for the player to engage A (else abandon — no interruption to make).
        float t0 = Time.time;
        while (A != null && A.IsActive && !A.Engaged && Time.time - t0 < engageTimeout) yield return null;
        if (A == null || !A.Engaged)
        {
            while (A != null && A.IsActive && Time.time - t0 < eventSafetyTimeout) yield return null;
            SessionManager.Instance?.LogCustomEvent("EF_InterruptionAbort", "System",
                "reason=A_not_engaged station=" + stationA.stationName);
            EventActive = false; yield break;
        }

        // 3) Beat, then fire the interrupt B (if A is still going).
        yield return new WaitForSeconds(interruptDelay);
        if (A == null || !A.IsActive) { EventActive = false; yield break; }
        var B = gm.SpawnEfOffer(stationB, 40f);
        if (B == null) { EventActive = false; yield break; }
        B.EfOrder = 2;
        AssignTierReason(B, stationB, Random.Range(0, 3)); // 0,1,2 — sometimes more critical than A
        AudioManager.Instance?.PlaySfx("alert_pulse", 0.7f);
        AudioManager.Instance?.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Beep, 2), AudioBus.Sfx, 0.6f);
        AudioManager.Instance?.PlayVoice("priority_event");
        string stationPretty = TaskListHUD.PrettyStation(stationB.stationName).ToUpperInvariant();
        string reasonB = string.IsNullOrEmpty(B.EfReason) ? "priority task" : B.EfReason;
        // Explicit either/or, framed as the player's call. The current task's clock
        // keeps draining underneath — switching costs time, so the decision matters.
        // Held a little past the switch window so the prompt is still up while the
        // player reacts and (optionally) walks over to switch.
        HUDManager.Instance?.ShowAlertBanner(
            "⚠ INTERRUPTION — " + stationPretty + ": " + reasonB +
            "   |   STAY on current task, or GO to " + stationPretty + " to switch?",
            switchWindow + 2.5f);
        NotificationFeed.Instance?.Push("⚠ " + stationPretty + " — " + reasonB + ". Stay on task, or switch?");

        bool optimalSwitch = B.EfTier < A.EfTier; // B strictly more critical -> switch is optimal
        string optionsDesc = "A=" + Tag(A) + " | B=" + Tag(B);
        SessionManager.Instance?.LogCustomEvent("EF_InterruptionOffer", "System",
            optionsDesc + " optimalSwitch=" + optimalSwitch);

        // 4) Decision window: did they undock A and engage B?
        float ti = Time.time;
        bool switched = false;
        float switchLatency = -1f;
        while (Time.time - ti < switchWindow)
        {
            if (B != null && B.Engaged) { switched = true; switchLatency = Time.time - ti; break; }
            if (A == null || !A.IsActive) break; // finished A first -> continued
            yield return null;
        }

        // 5) Wait for both to resolve; allow a late switch to still count for resume.
        float te = Time.time;
        while (((A != null && A.IsActive) || (B != null && B.IsActive)) && Time.time - te < eventSafetyTimeout)
        {
            if (!switched && B != null && B.Engaged) { switched = true; switchLatency = Time.time - ti; }
            yield return null;
        }
        ReleaseAbandoned(new[] { A, B });

        bool resumed = switched && (A == null || !A.IsActive); // came back / A finished after switching
        bool wasOptimal = optimalSwitch ? switched : !switched;
        bool perseveration = optimalSwitch && !switched;

        SessionManager.Instance?.LogCustomEvent("EF_InterruptionDecision", "System",
            "decision=" + (switched ? "switch" : "stay") +
            " optimalSwitch=" + optimalSwitch +
            " wasOptimal=" + wasOptimal +
            " switchLatency=" + (switchLatency < 0f ? "NA" : Num.F2(switchLatency)) +
            " resumedFirst=" + resumed +
            " perseveration=" + perseveration);

        EFResults.Instance?.Add(new EFEventRecord
        {
            EventType = "Interruption",
            NOptions = 2,
            OptionsDesc = optionsDesc,
            ChosenOrder = switched ? ("SWITCHED" + (switchLatency >= 0f ? " @" + Num.F2(switchLatency) + "s" : "")) : "STAYED",
            OptimalOrder = optimalSwitch ? "switch" : "stay",
            ChosenFirst = switched ? stationB.stationName : stationA.stationName,
            FirstWasOptimal = wasOptimal,
            WasOptimal = wasOptimal,
            OptScore = wasOptimal ? 1f : 0f,
            DecisionLatencyS = switchLatency,
            NoChoice = false,
            Perseveration = perseveration,
            Resumed = resumed,
        });

        EventActive = false;
    }

    private static string Tag(MissionTask t)
    {
        string tier = TierWord(t.EfTier);
        return t.StationName + ":" + tier;
    }

    private static string TierWord(int tier) => tier == 0 ? "critical" : tier == 1 ? "important" : "routine";

    // ----------------------------------------- Priority tiers, reasons, stakes

    // Assign distinct tiers (always including one Critical) so the optimal order is
    // unambiguous and the rule is learnable; shuffled so the critical isn't always
    // the first card. Each offer also gets a stated, station-themed reason.
    private static void AssignTiersAndReasons(List<MissionTask> offers, List<TaskStation> stations)
    {
        var tiers = TierSpread(offers.Count);
        for (int i = 0; i < offers.Count; i++)
        {
            offers[i].EfTier = tiers[i];
            offers[i].EfReason = ReasonFor(stations[i] != null ? stations[i].stationName : offers[i].StationName, tiers[i]);
        }
    }

    private static void AssignTierReason(MissionTask t, TaskStation s, int tier)
    {
        if (t == null) return;
        t.EfTier = tier;
        t.EfReason = ReasonFor(s != null ? s.stationName : t.StationName, tier);
    }

    private static int[] TierSpread(int n)
    {
        if (n <= 1) return new[] { 0 };
        var tiers = new List<int> { 0 };                 // always one Critical
        if (n >= 3) { tiers.Add(1); tiers.Add(2); }       // Critical/Important/Routine
        else tiers.Add(Random.value < 0.5f ? 1 : 2);     // n==2: Critical + one other
        while (tiers.Count < n) tiers.Add(2);
        var arr = tiers.GetRange(0, n);
        for (int i = arr.Count - 1; i > 0; i--) { int j = Random.Range(0, i + 1); (arr[i], arr[j]) = (arr[j], arr[i]); }
        return arr.ToArray();
    }

    private static int CriticalIndex(List<MissionTask> offers)
    {
        for (int i = 0; i < offers.Count; i++) if (offers[i].EfTier == 0) return i;
        return -1;
    }

    // Station-themed, tier-appropriate reasons (the WHY shown on each beat card).
    private static string ReasonFor(string station, int tier)
    {
        var pool = ReasonPool(station, tier);
        return pool[Random.Range(0, pool.Length)];
    }

    private static string[] ReasonPool(string station, int tier)
    {
        switch (station)
        {
            case "EngineStation":
                if (tier == 0) return new[] { "Reactor overheating", "Reactor core unstable" };
                if (tier == 1) return new[] { "Reactor output dropping", "Power efficiency degrading" };
                return new[] { "Routine reactor calibration", "Engine diagnostics due" };
            case "NavigationStation":
                if (tier == 0) return new[] { "Collision course detected", "Drifting toward debris" };
                if (tier == 1) return new[] { "Navigation accuracy decreasing", "Course drift detected" };
                return new[] { "Routine course calibration", "Nav system diagnostics" };
            case "CommsStation":
                if (tier == 0) return new[] { "Mayday signal incoming", "Comms blackout imminent" };
                if (tier == 1) return new[] { "Comms signal weakening", "Comm link degrading" };
                return new[] { "Routine comms check", "Signal diagnostics" };
            case "LifeSupportStation":
                if (tier == 0) return new[] { "Crew oxygen dropping", "Hull pressure falling" };
                if (tier == 1) return new[] { "Air quality degrading", "Cabin pressure slipping" };
                return new[] { "Routine filter calibration", "Life support diagnostics" };
        }
        if (tier == 0) return new[] { "Critical system failure" };
        if (tier == 1) return new[] { "Performance degrading" };
        return new[] { "Routine maintenance" };
    }

    // Positive reinforcement that closes the prioritization loop: when the player
    // picks the most critical task first, the crew acknowledges the good call.
    private void RewardGoodCall(bool firstWasOptimal, List<MissionTask> offers, int chosenIdx)
    {
        if (!firstWasOptimal || chosenIdx < 0) return;
        NotificationFeed.Instance?.Push(
            "Good call — " + TaskListHUD.PrettyStation(offers[chosenIdx].StationName).ToUpperInvariant() + " first");
        AudioManager.Instance?.PlayVoice("priority_good");
    }

    // Visible, recoverable stakes: the deprioritized critical station raises a crew
    // alarm (and a banner). It "recovers" — stabilizes — once its task resolves.
    private void TriggerCritical(TaskStation s, MissionTask t)
    {
        if (s == null) return;
        string pretty = TaskListHUD.PrettyStation(s.stationName).ToUpperInvariant();
        string reason = t != null && !string.IsNullOrEmpty(t.EfReason) ? t.EfReason : "System critical";
        ShipState.Instance?.SetCritical(s.stationName);
        AudioManager.Instance?.PlaySfx("fail_buzz", 0.7f);
        NotificationFeed.Instance?.Push("! " + pretty + " — " + reason);
        HUDManager.Instance?.ShowAlertBanner("! " + pretty + " CRITICAL — " + reason, 4f);
        SessionManager.Instance?.LogCustomEvent("EF_Consequence", s.stationName, "wentCritical reason=" + reason);
    }


    // ------------------------------------------------ WM + Prioritization event
    // A code is shown UP FRONT; the player must hold it while triaging + doing the
    // offered tasks, then enter it at Engine. Measures working memory under
    // prioritization interference. Falls back to Triage if Engine isn't eligible.
    public IEnumerator RunWmPrioritization(List<TaskStation> eligible)
    {
        var engine = gm.EngineStation;
        if (engine == null || !eligible.Contains(engine)) { yield return RunTriage(eligible); yield break; }

        EventActive = true;

        // 1) Show the code to memorize (the interference target).
        string code = MakeCode();
        AudioManager.Instance?.PlaySfx("button_click");
        HUDManager.Instance?.ShowCodeBanner(code, 5f);
        HUDManager.Instance?.ShowAlertBanner("MEMORIZE CODE — clear the tasks, then enter it at ENGINE", 5f);
        SessionManager.Instance?.LogCustomEvent("EF_WMPrioOffer", "System", "code=" + code);
        yield return new WaitForSeconds(5f);
        HUDManager.Instance?.HideCodeBanner();

        // 2) Offer the WM task (forced variant 0) + one other eligible task.
        var others = new List<TaskStation>();
        foreach (var s in eligible) if (s != engine) others.Add(s);
        Shuffle(others);

        float[] deadlines = MakeDistinctDeadlines(2);
        var offers = new List<MissionTask>();
        var offerStations = new List<TaskStation>();
        var wmTask = gm.SpawnEfOffer(engine, deadlines[0], 0) as WorkingMemoryTask;
        if (wmTask != null) { wmTask.SetExternalCode(code); offers.Add(wmTask); offerStations.Add(engine); }
        if (others.Count > 0)
        {
            var t = gm.SpawnEfOffer(others[0], deadlines[1]);
            if (t != null) { offers.Add(t); offerStations.Add(others[0]); }
        }
        if (offers.Count < 1) { EventActive = false; yield break; }

        // Learnable 3-tier rule (same as Triage).
        AssignTiersAndReasons(offers, offerStations);

        AudioManager.Instance?.PlaySfx("alert_pulse", 0.7f);
        AudioManager.Instance?.PlayVoice("priority_event");
        var panel = PriorityBeatPanel.GetOrCreate();
        panel.ShowBeat(offers, BeatWindow);
        SessionManager.Instance?.LogCustomEvent("EF_WMPrioTasks", "System", DescribeOffers(offers));

        // 3) The decision: pick the single highest-priority task (shared with Triage).
        int[] optimal = OptimalOrder(offers);
        var dec = new DecisionResult();
        yield return RunSinglePick(offers, dec, BeatWindow);
        panel.Confirm("OBJECTIVE SET");
        int chosenIdx = dec.Order.Count > 0 ? dec.Order[0] : -1;
        int optimalIdx = optimal[0];
        bool firstWasOptimal = chosenIdx == optimalIdx;
        float optScore = firstWasOptimal ? 1f : 0f;
        string chosenStr = chosenIdx >= 0 ? offers[chosenIdx].StationName : "none";
        string optimalStr = offers[optimalIdx].StationName;

        RewardGoodCall(firstWasOptimal, offers, chosenIdx);

        yield return new WaitForSeconds(0.8f);
        panel.HideBeat();

        // 4) Deprioritized-critical consequence (same as Triage), then wait only on
        //    the WM recall — the core measure — before reading it.
        int critIdx = CriticalIndex(offers);
        bool deprioritizedCritical = critIdx >= 0 && chosenIdx != critIdx;
        if (deprioritizedCritical) TriggerCritical(offerStations[critIdx], offers[critIdx]);

        // Only two offers here; keep both (one IS the WM task) with a fair window.
        foreach (var t in offers) if (t != null && t.IsActive) t.timeLimit = 60f;

        float execStart = Time.time;
        while (wmTask != null && wmTask.IsActive && Time.time - execStart < eventSafetyTimeout) yield return null;

        int codeCorrect = (wmTask == null || wmTask.CodeCorrect == null)
            ? -1 : (wmTask.CodeCorrect.Value ? 1 : 0);

        SessionManager.Instance?.LogCustomEvent("EF_WMPrioDecision", "System",
            "chosen=" + chosenStr +
            " optimal=" + optimalStr +
            " firstWasOptimal=" + firstWasOptimal +
            " optScore=" + Num.F2(optScore) +
            " latency=" + (dec.Latency < 0f ? "NA" : Num.F2(dec.Latency)) +
            " codeCorrect=" + (codeCorrect < 0 ? "NA" : (codeCorrect == 1 ? "True" : "False")));

        EFResults.Instance?.Add(new EFEventRecord
        {
            EventType = "WM+Prioritization",
            NOptions = offers.Count,
            OptionsDesc = DescribeOffers(offers),
            ChosenOrder = chosenStr,
            OptimalOrder = optimalStr,
            ChosenFirst = chosenStr,
            FirstWasOptimal = firstWasOptimal,
            WasOptimal = firstWasOptimal,
            OptScore = optScore,
            DecisionLatencyS = dec.Latency,
            NoChoice = dec.NoChoice,
            CodeRecalledCorrect = codeCorrect,
        });

        EventActive = false;
    }

    private static string MakeCode()
    {
        char[] d = new char[4];
        for (int i = 0; i < 4; i++) d[i] = (char)('0' + Random.Range(0, 10));
        return new string(d);
    }

    // --------------------------------------------------------------- helpers

    private static float[] MakeDistinctDeadlines(int n)
    {
        var pool = new List<float> { 25f, 40f, 55f, 70f };
        Shuffle(pool);
        var r = new float[n];
        for (int i = 0; i < n; i++) r[i] = pool[i];
        return r;
    }

    private static int[] OptimalOrder(List<MissionTask> offers)
    {
        var idx = new List<int>();
        for (int i = 0; i < offers.Count; i++) idx.Add(i);
        idx.Sort((a, b) =>
        {
            int t = offers[a].EfTier.CompareTo(offers[b].EfTier);   // most critical first
            if (t != 0) return t;
            return offers[a].EfDeadline.CompareTo(offers[b].EfDeadline); // then nearest deadline
        });
        return idx.ToArray();
    }

    private static bool DigitPressed(UnityEngine.InputSystem.Keyboard kb, int n)
    {
        switch (n)
        {
            case 1: return kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame;
            case 2: return kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame;
            case 3: return kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame;
        }
        return false;
    }

    // Pairwise concordance vs the optimal order, 0..1 (1 = identical order).
    private static float OrderSimilarity(List<int> chosen, int[] optimal)
    {
        int n = chosen.Count;
        var posO = new Dictionary<int, int>();
        for (int i = 0; i < optimal.Length; i++) posO[optimal[i]] = i;
        int conc = 0, total = 0;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
            {
                // chosen has 'a' before 'b' (i<j); concordant if optimal agrees.
                if (posO[chosen[i]] < posO[chosen[j]]) conc++;
                total++;
            }
        return total == 0 ? 1f : (float)conc / total;
    }

    private static bool SameOrder(List<int> chosen, int[] optimal)
    {
        if (chosen.Count != optimal.Length) return false;
        for (int i = 0; i < optimal.Length; i++) if (chosen[i] != optimal[i]) return false;
        return true;
    }

    private static string DescribeOffers(List<MissionTask> offers)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < offers.Count; i++)
        {
            if (i > 0) sb.Append('|');
            sb.Append(offers[i].StationName).Append(':').Append(TierWord(offers[i].EfTier))
              .Append(':').Append(Num.F0(offers[i].EfDeadline)).Append('s');
        }
        return sb.ToString();
    }

    private static string OrderNames(List<MissionTask> offers, List<int> order)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < order.Count; i++)
        {
            if (i > 0) sb.Append('>');
            sb.Append(offers[order[i]].StationName);
        }
        return sb.ToString();
    }

    private static List<int> ToList(int[] a)
    {
        var l = new List<int>(a.Length);
        foreach (var x in a) l.Add(x);
        return l;
    }

    private static bool AnyActive(List<MissionTask> offers)
    {
        foreach (var t in offers) if (t != null && t.IsActive) return true;
        return false;
    }

    // When an event ends (incl. via the safety timeout), force any offer the player
    // never engaged to expire (-> NotInitiated, neutral) so the deck clears and the
    // baseline loop resumes. Engaged-but-unfinished tasks are left to resolve.
    private static void ReleaseAbandoned(System.Collections.Generic.IEnumerable<MissionTask> offers)
    {
        foreach (var t in offers)
            if (t != null && t.IsActive && !t.Engaged) t.timeLimit = 0.01f;
    }

    private static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
