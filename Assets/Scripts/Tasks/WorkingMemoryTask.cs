using System.Collections;
using TMPro;
using UnityEngine;
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
    private enum Phase { Idle, Alert, DisplayCode, HiddenWaitingForDock, Recall, Done }

    private const float AlertDuration   = 1.5f;
    private const float DisplayDuration = 4f;
    private const float RecallDeadline  = 25f;
    private const int CodeLength = 4;

    private string code;
    private string input = "";
    private int wrongDigits = 0;
    private Phase phase = Phase.Idle;
    private Coroutine flowCo;
    private float recallStartTime = -1f;
    private TMP_Text inputLabel;

    private void Awake()
    {
        TaskName = "Working Memory";
        priority = TaskPriority.NonCritical;
        timeLimit = 60f;
    }

    public override void Activate()
    {
        base.Activate();

        // 4-digit code: each digit independent 0-9.
        char[] digits = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
            digits[i] = (char)('0' + Random.Range(0, 10));
        code = new string(digits);

        SessionManager.Instance?.LogCustomEvent("WM_Spawned", "EngineStation", "code=" + code);
        StationUI?.SetInstruction("WORKING MEMORY: read code on main screen, then enter here");
        ShowMessage("AWAIT CODE…", new Color(0.7f, 0.85f, 1f));
        AudioManager.Instance.PlayVoice("wm_memorize");
        flowCo = StartCoroutine(CoFullFlow());
    }

    private IEnumerator CoFullFlow()
    {
        // Phase 1: Alert
        phase = Phase.Alert;
        HUDManager.Instance?.ShowAlertBanner("INCOMING CODE — MEMORIZE", AlertDuration);
        yield return new WaitForSeconds(AlertDuration);
        if (!IsActive) yield break;

        // Phase 2: Show code on the HUD overlay (always centered, can't be missed).
        phase = Phase.DisplayCode;
        HUDManager.Instance?.ShowCodeBanner(code, DisplayDuration);
        SessionManager.Instance?.LogCustomEvent("WM_CodeShown", "EngineStation", code);
        yield return new WaitForSeconds(DisplayDuration);
        if (!IsActive) yield break;

        // Phase 3: Hide and wait for dock.
        HUDManager.Instance?.HideCodeBanner();
        SessionManager.Instance?.LogCustomEvent("WM_CodeHidden", "EngineStation", "");
        phase = Phase.HiddenWaitingForDock;
        ShowMessage("ENTER CODE HERE", new Color(0.4f, 1f, 0.5f));
        // Numpad is built when player docks (OnDocked).
    }

    protected override void OnDocked()
    {
        if (phase == Phase.HiddenWaitingForDock)
        {
            phase = Phase.Recall;
            recallStartTime = Time.time;
            AudioManager.Instance.PlayVoice("wm_enter_code");
            BuildNumpad();
            return;
        }
        if (phase == Phase.Recall)
        {
            // Resuming after a brief undock: do nothing (input persists).
        }
    }

    protected override void OnUndocked()
    {
        // Input is preserved; the recall deadline still ticks regardless of dock state.
    }

    private void BuildNumpad()
    {
        if (buttonsParent == null) return;
        ClearButtons();
        ShowMessage("REPEAT THE 4-DIGIT CODE", Color.white);

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
        if (string.IsNullOrEmpty(input)) return "_ _ _ _";
        char[] cells = new char[CodeLength];
        for (int i = 0; i < CodeLength; i++)
            cells[i] = i < input.Length ? input[i] : '_';
        return cells[0] + " " + cells[1] + " " + cells[2] + " " + cells[3];
    }

    private void UpdateInputLabel()
    {
        if (inputLabel != null) inputLabel.text = BuildInputDisplay();
    }

    private void OnDigitPressed(int d)
    {
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
        if (phase != Phase.Recall) return;
        input = "";
        UpdateInputLabel();
    }

    private void OnSubmit()
    {
        if (phase != Phase.Recall || !IsActive) return;
        if (input.Length < CodeLength)
        {
            ShowSplash("ENTER 4 DIGITS", new Color(1f, 0.7f, 0.2f), 0.5f);
            return;
        }
        bool correct = input == code;
        float totalTime = Time.time - SpawnTime;
        SessionManager.Instance?.LogCustomEvent("WM_Submit", "EngineStation",
            "input=" + input + " correct=" + correct + " typos=" + wrongDigits +
            " totalTime=" + totalTime.ToString("F2"));

        phase = Phase.Done;
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
        if (Time.time - recallStartTime >= RecallDeadline)
        {
            phase = Phase.Done;
            SessionManager.Instance?.LogCustomEvent("WM_Submit", "EngineStation",
                "input=" + input + " correct=False typos=" + wrongDigits +
                " totalTime=" + (Time.time - SpawnTime).ToString("F2") + " recallTimeout=true");
            StartCoroutine(CoFinish(TaskResult.Omission));
        }
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        HUDManager.Instance?.HideCodeBanner();
        base.OnDestroy();
    }
}
