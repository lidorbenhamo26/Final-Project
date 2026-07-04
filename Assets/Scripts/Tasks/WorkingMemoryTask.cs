using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Engine-station Working Memory task.
/// Flow:
///   1. Activate -> generate 4-digit code, immediately start CoFullFlow.
///   2. Alert (1.5s): HUD banner + MainScreen alert pulse + audio.
///   3. DisplayCode (4s): MainScreenDisplay shows the code in big cyan digits.
///   4. HiddenWaitingForDock: code is hidden, player must navigate to engine.
///   5. OnDocked while waiting -> Recall: numpad appears; player types 4 digits.
///   6. Submit -> Success/Fail; CLEAR resets input; timeout (overall or recall) -> Omission.
///
/// Metrics logged via SessionManager.LogCustomEvent:
///   WM_Spawned, WM_CodeShown, WM_CodeHidden, WM_TypoMistake, WM_Submit.
/// Reaction time + Success/Fail/Omission auto-logged by MissionTask.Resolve.
/// </summary>
public class WorkingMemoryTask : CognitiveTaskBase
{
    private enum Phase { Idle, Alert, DisplayCode, HiddenWaitingForDock, Distractor, Recall, Done }

    // Two retention modes (alternated by the catalog): Ambient = code flashes
    // during free roam and the walk to Engine IS the retention delay; DockDistractor
    // = dock, see the code, do a short distractor task, THEN recall (interference).
    public enum Mode { Ambient, DockDistractor }

    private const float AlertDuration   = 2.5f;
    private const float DisplayDuration = 4.5f; // shorter look at the code -> harder retention
    private const float RecallDeadline  = 25f;
    // Locked at exactly 4 digits in BOTH modes: 5-digit codes proved too hard /
    // frustrating in playtests and made spans incomparable across participants.
    private const int CodeDigits = 4;
    private int CodeLength = CodeDigits; // still tracks an external EF code's length
    // Interference between seeing the code and recalling it: quick equation
    // verifications (YES/NO under time pressure) that tax working-memory gating
    // without fully erasing the code — replaces the old "tap the lit pad".
    private const int   DistractorRounds   = 3;
    private const float DistractorPerRound = 3f;
    private const float DistractorSettle   = 0.3f;
    // Penalized re-read after an EF SWITCH pulled the player off this task: the
    // code re-displays briefly on return, so a later miss is attributable to the
    // switch decision rather than scored as an isolated WM failure.
    private const float ReReadSeconds = 2.5f;

    private string code;
    private string input = "";
    private int wrongDigits = 0;
    private Phase phase = Phase.Idle;
    private Coroutine flowCo;
    private float recallStartTime = -1f;
    private TMP_Text inputLabel;
    private bool flowStarted;
    private Mode mode = Mode.Ambient;
    private bool distractorAnswered;
    private int distractorHits;
    private TMP_Text distractorLabel; // per-round equation text (not tracked by ClearButtons)

    // EF-switch suspension state (see SuspendForSwitch / CoReRead).
    private bool suspendedBySwitch;
    private bool switchedAwayEver;
    private int reReads;
    private bool reReadRunning;

    /// <summary>True while any Ambient-mode WM task is in flight (code flashed or
    /// about to flash, not yet resolved) — i.e. the player is retaining a code in
    /// their head. GameManager reads this to hold EF beats (mutual exclusion).</summary>
    public static bool AmbientRetentionActive => ambientRetentionCount > 0;
    private static int ambientRetentionCount;
    private bool countedAmbientRetention;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { ambientRetentionCount = 0; }

    public void SetMode(Mode m) { mode = m; }

    private void Awake()
    {
        TaskName = "Working Memory";
        priority = TaskPriority.NonCritical;
        // Headroom for alert + (shorter) display + the longer distractor + recall.
        timeLimit = 70f;
    }

    // Set by the WM+Prioritization EF event: the code was already shown up front,
    // so skip the alert/display and go straight to recall on dock.
    private bool externalCode;
    /// <summary>True/false once recall is submitted; null until then. Read by the
    /// EF director to score WM-under-load.</summary>
    public bool? CodeCorrect { get; private set; }

    public void SetExternalCode(string c)
    {
        if (string.IsNullOrEmpty(c)) return;
        code = c;
        CodeLength = c.Length;
        externalCode = true;
        ShowMessage("DOCK TO ENTER CODE", new Color(0.7f, 0.85f, 1f));
    }

    protected override string InstructionTitle => "ENGINE - WORKING MEMORY";
    protected override string[] InstructionBody => new[]
    {
        "A short access code will flash on screen.",
        "Watch closely and memorize it - it shows once.",
        "",
        "When it disappears, type it back on the keypad.",
    };

    public override void Activate()
    {
        base.Activate();

        // Code: each digit independent 0-9, always exactly CodeDigits long.
        CodeLength = CodeDigits;
        char[] digits = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
            digits[i] = (char)('0' + Random.Range(0, 10));
        code = new string(digits);

        SessionManager.Instance?.LogCustomEvent("WM_Spawned", "EngineStation", "code=" + code);
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));
        StationUI?.SetInstruction("WORKING MEMORY: dock to begin");
        StartCoroutine(CoDeferredStart());
    }

    // Wait one frame so an EF event's SetExternalCode (called right after Activate)
    // can take effect first. Then: Ambient mode flashes the code NOW during free
    // roam (the walk to Engine is the retention delay); DockDistractor and external
    // code reveal on dock instead.
    private System.Collections.IEnumerator CoDeferredStart()
    {
        yield return null;
        if (externalCode || flowStarted || !IsActive || mode != Mode.Ambient) yield break;

        // Never flash the code under an EF beat / decision card (or a freeze):
        // queue the deployment until the beat clears, so a Triage popping at the
        // same moment can't collide with the memorize window and corrupt both.
        while (IsActive && (EFEventDirector.AnyEventActive || GameManager.IsDebugFrozen))
            yield return null;
        if (externalCode || flowStarted || !IsActive) yield break;

        flowStarted = true;
        ambientRetentionCount++;
        countedAmbientRetention = true;
        AudioManager.Instance.PlayVoice("wm_memorize");
        ShowMessage("MEMORIZE THE CODE — ENTER IT AT ENGINE", new Color(0.7f, 0.85f, 1f));
        StationUI?.SetInstruction("WORKING MEMORY: memorize it, then enter at Engine");
        flowCo = StartCoroutine(CoFullFlow());
    }

    // DockDistractor mode: show the code on the console, run a short distractor
    // task (interference), THEN ask for recall — a true working-memory test.
    private IEnumerator CoDistractorFlow()
    {
        phase = Phase.Alert;
        AudioManager.Instance.PlayVoice("wm_memorize");
        HUDManager.Instance?.ShowAlertBanner("MEMORIZE CODE — QUICK CHECK FIRST, THEN ENTER IT", AlertDuration);
        yield return new WaitForSeconds(AlertDuration);
        if (!IsActive) yield break;

        phase = Phase.DisplayCode;
        HUDManager.Instance?.ShowCodeBanner(code, DisplayDuration);
        SessionManager.Instance?.LogCustomEvent("WM_CodeShown", "EngineStation", code);
        yield return new WaitForSeconds(DisplayDuration);
        HUDManager.Instance?.HideCodeBanner();
        if (!IsActive) yield break;

        yield return CoDistractor();
        if (!IsActive) yield break;

        StartRecall();
    }

    // Quick Math verification between seeing the code and recalling it: simple
    // equations shown one at a time, YES/NO within DistractorPerRound each. This
    // taxes working-memory gating (numbers compete with the held code) far more
    // than the old "tap the lit pad", without fully erasing the code.
    private IEnumerator CoDistractor()
    {
        phase = Phase.Distractor;
        distractorHits = 0;
        int answered = 0;
        for (int r = 0; r < DistractorRounds && IsActive; r++)
        {
            ClearButtons();
            if (distractorLabel != null) Destroy(distractorLabel.gameObject);
            ShowMessage("HOLD THE CODE — QUICK CHECK " + (r + 1) + "/" + DistractorRounds,
                new Color(1f, 0.85f, 0.3f));

            // Simple a±b equation; half the time the shown result is off by 1-2.
            int a = Random.Range(1, 10);
            int b = Random.Range(1, 10);
            bool addition = Random.value < 0.5f;
            if (!addition && b > a) (a, b) = (b, a); // keep subtraction non-negative
            int trueResult = addition ? a + b : a - b;
            int shown = trueResult;
            if (Random.value < 0.5f)
            {
                int off = Random.Range(1, 3) * (Random.value < 0.5f ? -1 : 1);
                if (shown + off < 0) off = -off;
                shown += off;
            }
            bool isTrue = shown == trueResult;

            distractorLabel = SpawnLabel(new Vector2(0f, 70f), new Vector2(560f, 90f),
                a + (addition ? " + " : " - ") + b + " = " + shown + " ?", Color.white, 60f);
            SpawnButton(new Vector2(-110f, -60f), new Vector2(170f, 90f), "YES",
                new Color(0.25f, 0.62f, 0.38f), () => OnDistractorAnswer(true, isTrue));
            SpawnButton(new Vector2(110f, -60f), new Vector2(170f, 90f), "NO",
                new Color(0.62f, 0.28f, 0.26f), () => OnDistractorAnswer(false, isTrue));

            distractorAnswered = false;
            float t = 0f;
            while (IsActive && !distractorAnswered && t < DistractorPerRound)
            {
                // Rounds only burn while the player is actually at the console
                // (mirrors Stroop: undocked time doesn't consume trials).
                if (!GameManager.IsDebugFrozen && IsDocked) t += Time.deltaTime;
                yield return null;
            }
            if (distractorAnswered) answered++;
            // Brief settle after an answer so the interference period doesn't
            // collapse when the player answers instantly.
            if (IsActive) yield return FrozenWait(DistractorSettle);
        }
        ClearButtons();
        if (distractorLabel != null) { Destroy(distractorLabel.gameObject); distractorLabel = null; }
        SessionManager.Instance?.LogCustomEvent("WM_Distractor", "EngineStation",
            "correct=" + distractorHits + "/" + DistractorRounds + " answered=" + answered);
    }

    private void OnDistractorAnswer(bool saidYes, bool isTrue)
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (phase != Phase.Distractor || distractorAnswered) return;
        if (saidYes == isTrue) distractorHits++;
        distractorAnswered = true;
        AudioManager.Instance.PlaySfx("button_click");
    }

    private IEnumerator CoFullFlow()
    {
        // Phase 1: Alert
        phase = Phase.Alert;
        HUDManager.Instance?.ShowAlertBanner("INCOMING CODE — MEMORIZE, THEN ENTER IT AT ENGINE", AlertDuration);
        yield return new WaitForSeconds(AlertDuration);
        if (!IsActive) yield break;

        // Phase 2: Show code on the HUD overlay (always centered, can't be missed).
        phase = Phase.DisplayCode;
        HUDManager.Instance?.ShowCodeBanner(code, DisplayDuration);
        SessionManager.Instance?.LogCustomEvent("WM_CodeShown", "EngineStation", code);
        yield return new WaitForSeconds(DisplayDuration);
        if (!IsActive) yield break;

        // Phase 3: Hide. The flow normally runs while docked, so go straight to
        // recall; only fall back to "wait for dock" if the player wandered off
        // mid-display.
        HUDManager.Instance?.HideCodeBanner();
        SessionManager.Instance?.LogCustomEvent("WM_CodeHidden", "EngineStation", "");
        if (IsDocked) StartRecall();
        else
        {
            phase = Phase.HiddenWaitingForDock;
            ShowMessage("ENTER CODE HERE", new Color(0.4f, 1f, 0.5f));
            // Numpad is built when player re-docks (OnDocked).
        }
    }

    /// <summary>Called by the EF director when the player chose SWITCH away from
    /// this task while it holds a code. Cleanly suspends the retention: the recall
    /// deadline pauses while they are away, and on return the code re-displays
    /// for a short, penalized re-read (logged + reported), so a later miss reads
    /// as an expected consequence of the switch decision rather than an isolated
    /// working-memory failure.</summary>
    public void SuspendForSwitch()
    {
        if (!IsActive || phase == Phase.Done) return;
        switchedAwayEver = true;
        // Phases where the code is already hidden and being held: just flag the
        // suspension — the deadline pauses and the re-read fires on return.
        if (phase == Phase.HiddenWaitingForDock || phase == Phase.Recall)
        {
            suspendedBySwitch = true;
        }
        // Mid-reveal or mid-distractor: abort the console flow cleanly so it
        // can't tick on behind the player's back; the code re-displays
        // (penalized) on return and recall starts fresh from there.
        else if (phase == Phase.Alert || phase == Phase.DisplayCode || phase == Phase.Distractor)
        {
            if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
            ClearButtons();
            if (distractorLabel != null) { Destroy(distractorLabel.gameObject); distractorLabel = null; }
            HUDManager.Instance?.HideCodeBanner();
            phase = Phase.HiddenWaitingForDock;
            suspendedBySwitch = true;
        }
        SessionManager.Instance?.LogCustomEvent("WM_SuspendedBySwitch", "EngineStation",
            "phase=" + phase);
        ShowMessage("SUSPENDED — CODE RE-DISPLAYS ON RETURN", new Color(1f, 0.85f, 0.3f));
    }

    // Penalized re-read on return after a SWITCH: brief code re-display, then
    // recall resumes/starts. The re-read seconds run on the task clock — that IS
    // the penalty — and reReads/switchedAway are reported for attribution.
    private IEnumerator CoReRead()
    {
        reReadRunning = true;
        reReads++;
        SessionManager.Instance?.LogCustomEvent("WM_ReRead", "EngineStation",
            "n=" + reReads + " phase=" + phase);
        HUDManager.Instance?.ShowCodeBanner(code, ReReadSeconds);
        ShowMessage("CODE RE-DISPLAY — RESUME WHEN IT HIDES", new Color(1f, 0.85f, 0.3f));
        yield return FrozenWait(ReReadSeconds);
        reReadRunning = false;
        suspendedBySwitch = false;
        if (!IsActive) yield break;
        if (phase == Phase.HiddenWaitingForDock) StartRecall();
        else if (phase == Phase.Recall)
            ShowMessage("REPEAT THE " + CodeLength + "-DIGIT CODE  —  TYPE IT OR TAP", Color.white);
    }

    protected override void OnDocked()
    {
        // Returning after a SWITCH suspension: penalized re-read before anything
        // else (recall resumes or starts once the code hides again).
        if (suspendedBySwitch && !reReadRunning)
        {
            StartCoroutine(CoReRead());
            return;
        }

        // First dock: kick off the alert -> code -> recall reveal now that the
        // player is at the console. (The one-time instruction card, if any, has
        // already been dismissed before this runs.)
        if (!flowStarted)
        {
            flowStarted = true;
            // External code (WM+Prioritization event): shown up front -> recall now.
            if (externalCode) { StartRecall(); return; }
            // DockDistractor mode: dock -> show code -> short distractor -> recall.
            // (Ambient mode already started its reveal at spawn, so it won't reach
            // here; it lands in the HiddenWaitingForDock branch below.)
            flowCo = StartCoroutine(CoDistractorFlow());
            return;
        }
        if (phase == Phase.HiddenWaitingForDock)
        {
            StartRecall();
            return;
        }
        if (phase == Phase.Recall)
        {
            // Resuming after a brief undock: do nothing (input persists).
        }
    }

    private void StartRecall()
    {
        phase = Phase.Recall;
        recallStartTime = Time.time;
        AudioManager.Instance.PlayVoice("wm_enter_code");
        BuildNumpad();
    }

    protected override void OnUndocked()
    {
        // Input is preserved; the recall deadline still ticks regardless of dock state.
    }

    private void BuildNumpad()
    {
        if (buttonsParent == null) return;
        ClearButtons();
        ShowMessage("REPEAT THE " + CodeLength + "-DIGIT CODE  —  TYPE IT OR TAP", Color.white);

        inputLabel = SpawnLabel(new Vector2(0f, 200f), new Vector2(500f, 80f),
            BuildInputDisplay(), new Color(0.3f, 1f, 1f), 64f);

        Color digitColor = new Color(0.22f, 0.26f, 0.34f);
        Color clearColor = new Color(0.85f, 0.30f, 0.30f);
        Color submitColor = new Color(0.25f, 0.80f, 0.40f);
        Vector2 btnSize = new Vector2(100f, 80f);
        float xL = -112f, xM = 0f, xR = 112f;

        SpawnButton(new Vector2(xL, 130f), btnSize, "1", digitColor, () => OnDigitPressed(1));
        SpawnButton(new Vector2(xM, 130f), btnSize, "2", digitColor, () => OnDigitPressed(2));
        SpawnButton(new Vector2(xR, 130f), btnSize, "3", digitColor, () => OnDigitPressed(3));
        SpawnButton(new Vector2(xL,  40f), btnSize, "4", digitColor, () => OnDigitPressed(4));
        SpawnButton(new Vector2(xM,  40f), btnSize, "5", digitColor, () => OnDigitPressed(5));
        SpawnButton(new Vector2(xR,  40f), btnSize, "6", digitColor, () => OnDigitPressed(6));
        SpawnButton(new Vector2(xL, -50f), btnSize, "7", digitColor, () => OnDigitPressed(7));
        SpawnButton(new Vector2(xM, -50f), btnSize, "8", digitColor, () => OnDigitPressed(8));
        SpawnButton(new Vector2(xR, -50f), btnSize, "9", digitColor, () => OnDigitPressed(9));
        SpawnButton(new Vector2(xL, -140f), btnSize, "CLEAR", clearColor, OnClear);
        SpawnButton(new Vector2(xM, -140f), btnSize, "0", digitColor, () => OnDigitPressed(0));
        SpawnButton(new Vector2(xR, -140f), btnSize, "SUBMIT", submitColor, OnSubmit);
    }

    private string BuildInputDisplay()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < CodeLength; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(i < input.Length ? input[i] : '_');
        }
        return sb.ToString();
    }

    private void UpdateInputLabel()
    {
        if (inputLabel != null) inputLabel.text = BuildInputDisplay();
    }

    private void OnDigitPressed(int d)
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (phase != Phase.Recall || !IsActive) return;
        if (!IsDocked) return;
        if (input.Length >= CodeLength) return;

        char expected = code[input.Length];
        char got = (char)('0' + d);
        if (got != expected)
        {
            wrongDigits++;
            SessionManager.Instance?.LogCustomEvent("WM_TypoMistake", "EngineStation",
                "expected=" + expected + " got=" + got + " pos=" + input.Length);
        }
        input += got;
        AudioManager.Instance.PlaySfx("digit_press");
        UpdateInputLabel();
    }

    private void OnClear()
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (phase != Phase.Recall) return;
        input = "";
        UpdateInputLabel();
    }

    // Backspace: remove the most recently entered digit (keyboard only).
    private void OnBackspace()
    {
        if (phase != Phase.Recall || input.Length == 0) return;
        input = input.Substring(0, input.Length - 1);
        UpdateInputLabel();
    }

    // Direct keyboard entry during recall, in addition to the on-screen keypad:
    // 0-9 (top row or numpad) enter digits, Backspace deletes, Enter submits.
    private void PollKeyboardEntry()
    {
        if (phase != Phase.Recall || !IsActive || !IsDocked) return;
        var k = Keyboard.current;
        if (k == null) return;

        for (int d = 0; d <= 9; d++)
        {
            if (DigitKeyPressed(k, d)) OnDigitPressed(d);
        }
        if (k.backspaceKey.wasPressedThisFrame || k.deleteKey.wasPressedThisFrame) OnBackspace();
        if (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame) OnSubmit();
    }

    private static bool DigitKeyPressed(Keyboard k, int d)
    {
        switch (d)
        {
            case 0: return k.digit0Key.wasPressedThisFrame || k.numpad0Key.wasPressedThisFrame;
            case 1: return k.digit1Key.wasPressedThisFrame || k.numpad1Key.wasPressedThisFrame;
            case 2: return k.digit2Key.wasPressedThisFrame || k.numpad2Key.wasPressedThisFrame;
            case 3: return k.digit3Key.wasPressedThisFrame || k.numpad3Key.wasPressedThisFrame;
            case 4: return k.digit4Key.wasPressedThisFrame || k.numpad4Key.wasPressedThisFrame;
            case 5: return k.digit5Key.wasPressedThisFrame || k.numpad5Key.wasPressedThisFrame;
            case 6: return k.digit6Key.wasPressedThisFrame || k.numpad6Key.wasPressedThisFrame;
            case 7: return k.digit7Key.wasPressedThisFrame || k.numpad7Key.wasPressedThisFrame;
            case 8: return k.digit8Key.wasPressedThisFrame || k.numpad8Key.wasPressedThisFrame;
            case 9: return k.digit9Key.wasPressedThisFrame || k.numpad9Key.wasPressedThisFrame;
        }
        return false;
    }

    private void OnSubmit()
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (phase != Phase.Recall || !IsActive) return;
        if (input.Length < CodeLength)
        {
            ShowSplash("ENTER " + CodeLength + " DIGITS", new Color(1f, 0.7f, 0.2f), 0.5f);
            return;
        }
        bool correct = input == code;
        CodeCorrect = correct;
        float totalTime = Time.time - SpawnTime;
        SessionManager.Instance?.LogCustomEvent("WM_Submit", "EngineStation",
            "input=" + input + " correct=" + correct + " typos=" + wrongDigits +
            " totalTime=" + Num.F2(totalTime));
        AssessmentResults.Report(this,
            ("correct", correct.ToString()),
            ("typos", wrongDigits.ToString()),
            ("totalTimeS", Num.F2(totalTime)),
            ("recallTimeout", "false"),
            ("switchedAway", switchedAwayEver ? "true" : "false"),
            ("reReads", reReads.ToString()));

        phase = Phase.Done;
        ResolutionPending = true; // outcome computed — base expiry must not overwrite it
        StartCoroutine(CoFinish(correct ? TaskResult.Success : TaskResult.Fail));
    }

    private IEnumerator CoFinish(TaskResult result)
    {
        ClearButtons();
        if (result == TaskResult.Success)
        {
            AudioManager.Instance.PlaySfx("success_chime");
            AudioManager.Instance.PlayVoice("correct");
            ShowSplash("CORRECT!", new Color(0.3f, 1f, 0.4f), 1.0f);
        }
        else if (result == TaskResult.Omission)
        {
            AudioManager.Instance.PlaySfx("timeout_alarm");
            AudioManager.Instance.PlayVoice("timeout");
            ShowSplash("TIMEOUT", new Color(1f, 0.6f, 0.2f), 1.0f);
        }
        else
        {
            AudioManager.Instance.PlaySfx("fail_buzz");
            AudioManager.Instance.PlayVoice("incorrect");
            ShowSplash("WRONG", new Color(1f, 0.3f, 0.3f), 1.0f);
        }
        yield return new WaitForSeconds(1.0f);
        Resolve(result);
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive) return;
        if (phase != Phase.Recall) return;
        // Debug freeze (F11 / assessor report): the recall deadline pauses with
        // the rest of the mission instead of expiring behind the overlay. The
        // deadline also pauses while cleanly suspended by an EF SWITCH (away from
        // the console) and during the penalized re-read itself.
        if (GameManager.IsDebugFrozen || (suspendedBySwitch && !IsDocked) || reReadRunning)
        {
            recallStartTime += Time.deltaTime;
            return;
        }
        PollKeyboardEntry();
        if (!IsActive || phase != Phase.Recall) return; // a keyboard submit may have resolved it
        if (Time.time - recallStartTime >= RecallDeadline)
        {
            phase = Phase.Done;
            ResolutionPending = true;
            SessionManager.Instance?.LogCustomEvent("WM_Submit", "EngineStation",
                "input=" + input + " correct=False typos=" + wrongDigits +
                " totalTime=" + Num.F2(Time.time - SpawnTime) + " recallTimeout=true");
            AssessmentResults.Report(this,
                ("correct", "False"),
                ("typos", wrongDigits.ToString()),
                ("totalTimeS", Num.F2(Time.time - SpawnTime)),
                ("recallTimeout", "true"),
                ("switchedAway", switchedAwayEver ? "true" : "false"),
                ("reReads", reReads.ToString()));
            StartCoroutine(CoFinish(TaskResult.Omission));
        }
    }

    // Overall mission time limit hit before submission — keep partial entry data.
    protected override void HandleExpiry()
    {
        if (Engaged)
            AssessmentResults.Report(this,
                ("correct", "False"),
                ("typos", wrongDigits.ToString()),
                ("phaseAtTimeout", phase.ToString()),
                ("switchedAway", switchedAwayEver ? "true" : "false"),
                ("reReads", reReads.ToString()));
        base.HandleExpiry();
    }

    protected override void OnDestroy()
    {
        if (countedAmbientRetention)
        {
            ambientRetentionCount = Mathf.Max(0, ambientRetentionCount - 1);
            countedAmbientRetention = false;
        }
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        HUDManager.Instance?.HideCodeBanner();
        base.OnDestroy();
    }
}
