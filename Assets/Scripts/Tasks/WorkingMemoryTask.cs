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
    private int CodeLength = 4; // varies 4-5 per instance for less rote repetition
    // Longer interference between seeing the code and recalling it (more rounds +
    // a brief settle after each tap) so the code must genuinely be HELD, not rehearsed.
    private const int   DistractorRounds  = 5;
    private const float DistractorPerRound = 2f;
    private const float DistractorSettle  = 0.3f;

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

        // Vary the code length (4-5) per instance for variety / less practice effect.
        CodeLength = Random.Range(0, 2) == 0 ? 4 : 5;
        // Code: each digit independent 0-9.
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
        if (externalCode || flowStarted || !IsActive) yield break;
        if (mode == Mode.Ambient)
        {
            flowStarted = true;
            AudioManager.Instance.PlayVoice("wm_memorize");
            ShowMessage("MEMORIZE THE CODE — ENTER IT AT ENGINE", new Color(0.7f, 0.85f, 1f));
            StationUI?.SetInstruction("WORKING MEMORY: memorize it, then enter at Engine");
            flowCo = StartCoroutine(CoFullFlow());
        }
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

    // A brief attention task between seeing the code and recalling it: tap the lit
    // pad a few times. Occupies attention so the code must be HELD, not rehearsed.
    private IEnumerator CoDistractor()
    {
        phase = Phase.Distractor;
        distractorHits = 0;
        for (int r = 0; r < DistractorRounds && IsActive; r++)
        {
            ClearButtons();
            ShowMessage("HOLD THE CODE — quick check", new Color(1f, 0.85f, 0.3f));
            int targetIdx = Random.Range(0, 3);
            for (int i = 0; i < 3; i++)
            {
                bool isTarget = i == targetIdx;
                Color col = isTarget ? new Color(0.30f, 0.85f, 0.45f) : new Color(0.28f, 0.32f, 0.40f);
                SpawnButton(new Vector2(-130f + i * 130f, 0f), new Vector2(110f, 90f),
                    isTarget ? "TAP" : "", col, () => OnDistractorTap(isTarget));
            }
            distractorAnswered = false;
            float t = 0f;
            while (IsActive && !distractorAnswered && t < DistractorPerRound)
            {
                if (!GameManager.IsDebugFrozen) t += Time.deltaTime;
                yield return null;
            }
            // Brief settle after a tap so the interference period doesn't collapse
            // when the player answers instantly — keeps the retention delay honest.
            if (IsActive) yield return FrozenWait(DistractorSettle);
        }
        ClearButtons();
        SessionManager.Instance?.LogCustomEvent("WM_Distractor", "EngineStation", "hits=" + distractorHits);
    }

    private void OnDistractorTap(bool correct)
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (phase != Phase.Distractor) return;
        if (correct) distractorHits++;
        distractorAnswered = true;
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

    protected override void OnDocked()
    {
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
            ("recallTimeout", "false"));

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
        // the rest of the mission instead of expiring behind the overlay.
        if (GameManager.IsDebugFrozen)
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
                ("recallTimeout", "true"));
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
                ("phaseAtTimeout", phase.ToString()));
        base.HandleExpiry();
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        HUDManager.Instance?.HideCodeBanner();
        base.OnDestroy();
    }
}
