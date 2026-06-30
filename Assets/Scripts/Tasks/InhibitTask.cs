using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Comms cognitive task: Go/No-Go inhibition with a SHAPE + COLOR conjunction.
/// One stimulus flashes per trial; only the exact target combination (a RED SQUARE)
/// is a GO. Every other stimulus — wrong colour, wrong shape, or both — is a NO-GO
/// the player must withhold on. The player identifies the target VISUALLY: the
/// presented shape is never labelled, so the only cue is what they see.
///
/// Presentation is deliberately clean: a single static console frame stays put for
/// the whole task, and ONLY the shape inside it appears, disappears and changes
/// between trials. Difficulty ramps with mission progress and within the run —
/// faster presentation, rarer targets, and occasional rapid bursts of non-targets
/// right before a target — to push inhibitory control without adding clutter.
///
///   GO  (target): RED SQUARE  -> press EXECUTE within the window.
///   NO-GO (everything else)    -> do NOT press; wait it out.
///
/// Metrics logged via SessionManager.LogCustomEvent:
///   INH_Spawned, INH_TrialStart, INH_Response, INH_Summary.
///
/// Resolve precedence (omission disengagement takes priority over commission):
///   Omission  if omissionRate  > 0.50
///   Commission if commissionRate > 0.30
///   Success   otherwise
/// </summary>
public class InhibitTask : CognitiveTaskBase
{
    private enum Phase { Idle, Countdown, ISI, Signal, Done }

    private const int   TotalTrials      = 24;
    private const int   MaxTargetStreak   = 2;       // no long runs of consecutive targets
    private const float CommissionThresh = 0.30f;
    private const float OmissionThresh   = 0.50f;

    private const int TargetShape = 0; // SQUARE
    private const int TargetColor = 0; // RED

    private static readonly string[] ShapeGlyphs = { "■", "●", "▲", "◆" };
    private static readonly string[] ShapeNames  = { "SQUARE", "CIRCLE", "TRIANGLE", "DIAMOND" };
    private static readonly Color[]  Palette =
    {
        new Color(0.93f, 0.27f, 0.24f), // RED
        new Color(0.34f, 0.58f, 1.00f), // BLUE
        new Color(0.32f, 0.82f, 0.42f), // GREEN
        new Color(0.97f, 0.82f, 0.24f), // YELLOW
    };
    private static readonly string[] ColorNames = { "RED", "BLUE", "GREEN", "YELLOW" };

    private static readonly Color BgDark     = new Color(0.06f, 0.07f, 0.10f, 1f);
    private static readonly Color ExecGreen  = new Color(0.20f, 0.75f, 0.35f);
    private static readonly Color FrameSteel = new Color(0.34f, 0.40f, 0.50f); // static neutral frame

    private struct TrialDef { public int Shape; public int Color; public bool IsTarget; }

    private Phase phase = Phase.Idle;
    private TrialDef[] schedule;
    private float[] signalDur;     // per-trial stimulus duration (ramps faster)
    private float[] isiDur;        // per-trial blank before the stimulus
    private int   targetCount;     // GO trials this run (rarer at higher difficulty)
    private bool[] wasCommission;  // true if trial i was a NO-GO commission
    private float baseDiff;        // mission difficulty captured at activation

    private int   trialIdx;
    private bool  started;
    private bool  responded;
    private float signalStartTime;
    private float curSignalDur;
    private Coroutine flowCo;

    private int commissionCount;
    private int omissionCount;
    private readonly List<float> hitRTs          = new List<float>();
    private readonly List<float> postErrorHitRTs = new List<float>();
    private readonly List<float> baselineHitRTs  = new List<float>();

    private TMP_Text   trialLabel;
    private TMP_Text   targetLegend;
    private GameObject signalIconGo;   // the ONLY thing that appears/disappears per trial
    private TMP_Text   signalIconText;
    private Coroutine  appearCo;

    private void Awake()
    {
        TaskName  = "Inhibit";
        priority  = TaskPriority.Critical;
        timeLimit = 130f; // generous; the run is shorter at higher difficulty
    }

    public override float MinResponseWindowSeconds => TotalTrials * 2.4f + 18f;

    private static string TargetDesc => ColorNames[TargetColor] + " " + ShapeNames[TargetShape];

    protected override string InstructionTitle => "COMMS - GO / NO-GO";
    protected override string[] InstructionBody => new[]
    {
        "Shapes flash one at a time in different colours.",
        "",
        "Press EXECUTE ONLY for a " + TargetDesc + " (" + ShapeGlyphs[TargetShape] + ").",
        "ANY other shape or colour = do NOT press, just wait.",
        "",
        "Watch both the shape AND the colour.",
    };

    public override void Activate()
    {
        base.Activate();
        baseDiff = GameManager.Instance != null ? Mathf.Clamp01(GameManager.Instance.CurrentDifficulty) : 0f;
        StationUI?.SetInstruction("INHIBIT TASK: dock to begin");
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));

        GenerateSchedule();
        BuildSignalFrame();
        BuildTrialLabel();
        BuildTargetLegend();

        SessionManager.Instance?.LogCustomEvent("INH_Spawned", "CommsStation",
            "trials=" + TotalTrials + ",targets=" + targetCount + ",target=" + TargetDesc +
            ",diff=" + baseDiff.ToString("F2"));
        AudioManager.Instance.PlayVoice("inhibit_intro");
    }

    protected override void OnDocked()
    {
        if (started) return;
        started = true;
        ShowReadyGate();
    }

    private void ShowReadyGate()
    {
        if (buttonsParent == null) return;
        ShowMessage("PRESS READY", new Color(0.9f, 0.95f, 1f));
        ClearButtons();
        SpawnButton(new Vector2(0f, -200f), new Vector2(280f, 92f), "READY",
            new Color(0.2f, 0.8f, 0.4f), OnStartReadyClicked);
    }

    private void OnStartReadyClicked()
    {
        if (flowCo != null) return;
        ClearButtons();
        // EXECUTE is always visible for the rest of the task. The handler only
        // accepts presses during the signal window; outside that, it no-ops.
        SpawnButton(new Vector2(0f, -200f), new Vector2(280f, 92f), "EXECUTE",
            ExecGreen, OnExecutePressed);
        flowCo = StartCoroutine(CoRunTrials());
    }

    // Builds targets (red squares) + distractors, shuffles with a max target streak,
    // then computes per-trial timing (speed ramp + occasional rapid bursts before a
    // target). Targets get rarer as mission difficulty rises.
    private void GenerateSchedule()
    {
        targetCount = Mathf.RoundToInt(Mathf.Lerp(9f, 6f, baseDiff)); // ~37% -> 25%
        targetCount = Mathf.Clamp(targetCount, 5, 10);

        var pool = new List<TrialDef>(TotalTrials);
        for (int i = 0; i < targetCount; i++)
            pool.Add(new TrialDef { Shape = TargetShape, Color = TargetColor, IsTarget = true });

        int distractors = TotalTrials - targetCount;
        for (int i = 0; i < distractors; i++)
        {
            int lure = i % 3;
            TrialDef d;
            if (lure == 0)      d = new TrialDef { Shape = TargetShape,  Color = OtherColor(), IsTarget = false }; // square, wrong colour
            else if (lure == 1) d = new TrialDef { Shape = OtherShape(), Color = TargetColor,  IsTarget = false }; // red, wrong shape
            else                d = new TrialDef { Shape = OtherShape(), Color = OtherColor(), IsTarget = false }; // neither
            pool.Add(d);
        }

        for (int attempt = 0; attempt < 60; attempt++)
        {
            ShuffleTrials(pool);
            if (TargetStreakOk(pool)) break;
        }
        schedule      = pool.ToArray();
        wasCommission = new bool[schedule.Length];

        ComputeTiming();
    }

    // Per-trial timing. Base speed comes from mission difficulty; a mild in-run ramp
    // makes later trials quicker; and a couple of targets get a "burst" of fast
    // non-targets right before them, raising the demand to hold back.
    private void ComputeTiming()
    {
        int n = schedule.Length;
        signalDur = new float[n];
        isiDur    = new float[n];

        float sigBase = Mathf.Lerp(1.25f, 0.90f, baseDiff);
        float isiMin  = Mathf.Lerp(0.70f, 0.45f, baseDiff);
        float isiMax  = Mathf.Lerp(1.10f, 0.70f, baseDiff);

        for (int i = 0; i < n; i++)
        {
            float ramp = n > 1 ? Mathf.Lerp(1f, 0.82f, i / (float)(n - 1)) : 1f;
            signalDur[i] = sigBase * ramp;
            isiDur[i]    = Random.Range(isiMin, isiMax) * ramp;
        }

        // Rapid bursts: for up to two targets, speed up the up-to-3 non-targets
        // immediately before them so the player faces a quick run of "hold" stimuli
        // and must not let momentum trigger a false start when the target lands.
        var targetPositions = new List<int>();
        for (int i = 0; i < n; i++) if (schedule[i].IsTarget) targetPositions.Add(i);
        ShuffleInts(targetPositions);
        int bursts = Mathf.Min(2, targetPositions.Count);
        for (int b = 0; b < bursts; b++)
        {
            int p = targetPositions[b];
            for (int k = 1; k <= 3; k++)
            {
                int j = p - k;
                if (j < 0 || schedule[j].IsTarget) break;
                isiDur[j]    = 0.30f;
                signalDur[j] = Mathf.Min(signalDur[j], 0.75f);
            }
        }
    }

    private static int OtherShape() => Random.Range(1, ShapeGlyphs.Length); // 1..3 (never the target shape)
    private static int OtherColor() => Random.Range(1, Palette.Length);     // 1..3 (never the target colour)

    private static void ShuffleTrials(List<TrialDef> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static void ShuffleInts(List<int> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private static bool TargetStreakOk(List<TrialDef> list)
    {
        int streak = 0;
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].IsTarget) { streak++; if (streak > MaxTargetStreak) return false; }
            else streak = 0;
        }
        return true;
    }

    // A single STATIC console frame. It never moves, recolours or toggles — only the
    // shape glyph inside it changes between trials. Centered, with clear spacing
    // above (rule + trial counter) and below (EXECUTE).
    private void BuildSignalFrame()
    {
        if (buttonsParent == null) return;

        GameObject frame = new GameObject("SignalFrame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(buttonsParent, false);
        RectTransform fr = frame.GetComponent<RectTransform>();
        fr.anchorMin = fr.anchorMax = new Vector2(0.5f, 0.5f);
        fr.pivot = new Vector2(0.5f, 0.5f);
        fr.anchoredPosition = new Vector2(0f, -8f);
        fr.sizeDelta = new Vector2(430f, 220f);
        Image frImg = frame.GetComponent<Image>();
        frImg.color = FrameSteel;            // static neutral ring
        frImg.raycastTarget = false;

        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(frame.transform, false);
        RectTransform ir = inner.GetComponent<RectTransform>();
        ir.anchorMin = Vector2.zero; ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(8f, 8f); ir.offsetMax = new Vector2(-8f, -8f);
        Image innerImg = inner.GetComponent<Image>();
        innerImg.color = BgDark;
        innerImg.raycastTarget = false;

        // The shape glyph — the ONLY per-trial element. Centered, hidden between trials.
        signalIconGo = new GameObject("Shape", typeof(RectTransform));
        signalIconGo.transform.SetParent(inner.transform, false);
        signalIconText = signalIconGo.AddComponent<TextMeshProUGUI>();
        signalIconText.alignment = TextAlignmentOptions.Center;
        signalIconText.fontSize = 150f;
        signalIconText.fontStyle = FontStyles.Bold;
        signalIconText.text = ShapeGlyphs[TargetShape];
        signalIconText.color = Palette[TargetColor];
        signalIconText.raycastTarget = false;
        RectTransform iconRt = signalIconGo.GetComponent<RectTransform>();
        iconRt.anchorMin = Vector2.zero; iconRt.anchorMax = Vector2.one;
        iconRt.offsetMin = Vector2.zero; iconRt.offsetMax = Vector2.zero;
        iconRt.pivot = new Vector2(0.5f, 0.5f);

        signalIconGo.SetActive(false); // empty frame until the first stimulus
    }

    private void BuildTrialLabel()
    {
        trialLabel = SpawnLabel(new Vector2(0f, 140f), new Vector2(360f, 28f),
            "Trial 0 / " + TotalTrials, new Color(0.62f, 0.70f, 0.82f), 22f);
    }

    // Always-visible RULE reminder (not the presented shape's name) so the player
    // never has to recall the target, but still has to read the stimulus visually.
    private void BuildTargetLegend()
    {
        targetLegend = SpawnLabel(new Vector2(0f, 182f), new Vector2(700f, 40f),
            "PRESS ONLY FOR   " + ShapeGlyphs[TargetShape] + "  " + TargetDesc,
            new Color(0.92f, 0.95f, 1f), 30f);
        if (targetLegend != null)
        {
            targetLegend.fontStyle = FontStyles.Bold;
            // Tint just the target swatch so the rule reads at a glance.
            targetLegend.text = "PRESS ONLY FOR   <color=#" + ColorUtility.ToHtmlStringRGB(Palette[TargetColor]) + ">"
                + ShapeGlyphs[TargetShape] + "  " + TargetDesc + "</color>";
        }
    }

    private void ShowShape(TrialDef trial)
    {
        if (signalIconText == null) return;
        signalIconText.text  = ShapeGlyphs[trial.Shape];
        signalIconText.color = Palette[trial.Color];
        signalIconGo.SetActive(true);
        if (appearCo != null) StopCoroutine(appearCo);
        appearCo = StartCoroutine(CoShapePop());
    }

    private void HideShape()
    {
        if (appearCo != null) { StopCoroutine(appearCo); appearCo = null; }
        if (signalIconGo != null) signalIconGo.SetActive(false);
    }

    // Quick pop on the SHAPE only (the frame stays put).
    private IEnumerator CoShapePop()
    {
        Transform t = signalIconGo.transform;
        const float inDur = 0.12f;
        for (float u = 0f; u < inDur; u += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0.7f, 1.05f, u / inDur);
            t.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        for (float u = 0f; u < 0.07f; u += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(1.05f, 1.0f, u / 0.07f);
            t.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        t.localScale = Vector3.one;
    }

    private IEnumerator CoRunTrials()
    {
        phase = Phase.Countdown;
        ShowMessage("READY?", new Color(0.9f, 0.95f, 1f));
        yield return new WaitForSeconds(0.7f);
        for (int n = 3; n >= 1 && IsActive; n--)
        {
            ShowMessage(n.ToString(), new Color(1f, 0.9f, 0.4f));
            yield return new WaitForSeconds(0.45f);
        }
        if (!IsActive) yield break;
        ShowMessage("WATCH FOR " + TargetDesc, new Color(0.4f, 1f, 0.5f));
        yield return new WaitForSeconds(0.45f);
        ShowMessage("", Color.white); // clear the header; the rule stays in the legend

        for (trialIdx = 0; trialIdx < schedule.Length && IsActive; trialIdx++)
        {
            phase = Phase.ISI;
            HideShape();
            yield return FrozenWait(isiDur[trialIdx]);
            if (!IsActive) yield break;

            TrialDef trial = schedule[trialIdx];
            bool isTarget = trial.IsTarget;
            string signalId = ColorNames[trial.Color] + "_" + ShapeNames[trial.Shape] + "_" + trialIdx;
            SessionManager.Instance?.LogCustomEvent("INH_TrialStart", "CommsStation",
                "trialIdx=" + trialIdx + ",isTarget=" + isTarget + ",signalId=" + signalId);

            phase = Phase.Signal;
            responded = false;
            signalStartTime = Time.time;
            curSignalDur = signalDur[trialIdx];
            ShowShape(trial);
            if (trialLabel != null)
                trialLabel.text = "Trial " + (trialIdx + 1) + " / " + schedule.Length;

            while (IsActive && phase == Phase.Signal && !responded
                   && (Time.time - signalStartTime) < curSignalDur)
            {
                // Debug freeze (F11 / assessor report): pause the response window
                // and keep the RT anchor honest.
                if (GameManager.IsDebugFrozen) signalStartTime += Time.deltaTime;
                yield return null;
            }

            HideShape();
            if (IsActive && !responded) HandleTimeout(isTarget);
        }

        phase = Phase.Done;
        HideShape();
        ResolveTask();
    }

    private void OnExecutePressed()
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (!IsActive) return;
        if (!IsDocked) return;
        if (phase != Phase.Signal) return;
        if (responded) return;

        responded = true;
        bool isTarget = schedule[trialIdx].IsTarget;
        float rtMs = (Time.time - signalStartTime) * 1000f;
        string result;
        if (isTarget)
        {
            hitRTs.Add(rtMs);
            if (trialIdx > 0 && wasCommission[trialIdx - 1]) postErrorHitRTs.Add(rtMs);
            else                                              baselineHitRTs.Add(rtMs);
            result = "correct";
        }
        else
        {
            commissionCount++;
            wasCommission[trialIdx] = true;
            result = "commission";
        }
        SessionManager.Instance?.LogCustomEvent("INH_Response", "CommsStation",
            "trialIdx=" + trialIdx + ",rtMs=" + rtMs.ToString("F0") + ",result=" + result);
    }

    private void HandleTimeout(bool isTarget)
    {
        string result;
        if (isTarget) { omissionCount++; result = "omission"; }
        else            result = "correct";
        SessionManager.Instance?.LogCustomEvent("INH_Response", "CommsStation",
            "trialIdx=" + trialIdx + ",rtMs=-1,result=" + result);
    }

    private void ResolveTask()
    {
        int goCount   = targetCount;                 // trials requiring a press
        int nogoCount = TotalTrials - targetCount;   // distractors requiring restraint

        float hitMean = 0f, hitSD = 0f;
        if (hitRTs.Count > 0)
        {
            float sum = 0f;
            for (int i = 0; i < hitRTs.Count; i++) sum += hitRTs[i];
            hitMean = sum / hitRTs.Count;
            if (hitRTs.Count > 1)
            {
                float varSum = 0f;
                for (int i = 0; i < hitRTs.Count; i++)
                {
                    float d = hitRTs[i] - hitMean;
                    varSum += d * d;
                }
                hitSD = Mathf.Sqrt(varSum / (hitRTs.Count - 1));
            }
        }

        string pesStr;
        if (commissionCount == 0 || postErrorHitRTs.Count == 0 || baselineHitRTs.Count == 0)
        {
            pesStr = "NA";
        }
        else
        {
            float pSum = 0f; for (int i = 0; i < postErrorHitRTs.Count; i++) pSum += postErrorHitRTs[i];
            float bSum = 0f; for (int i = 0; i < baselineHitRTs.Count; i++) bSum += baselineHitRTs[i];
            float pes = pSum / postErrorHitRTs.Count - bSum / baselineHitRTs.Count;
            pesStr = pes.ToString("F0");
        }

        // "NA", not 0: a zero would read as a real 0 ms RT downstream, and an
        // SD over a single observation is undefined.
        string hitMeanStr = hitRTs.Count > 0 ? Num.F0(hitMean) : "NA";
        string hitSdStr   = hitRTs.Count > 1 ? Num.F0(hitSD)   : "NA";

        SessionManager.Instance?.LogCustomEvent("INH_Summary", "CommsStation",
            "commission=" + commissionCount +
            ",omission=" + omissionCount +
            ",hitRT_mean=" + hitMeanStr +
            ",hitRT_sd=" + hitSdStr +
            ",postErrorSlowing=" + pesStr);
        AssessmentResults.Report(this,
            ("commission", commissionCount.ToString()),
            ("omission", omissionCount.ToString()),
            ("hitRtMeanMs", hitMeanStr),
            ("hitRtSdMs", hitSdStr),
            ("postErrorSlowingMs", pesStr));

        float commissionRate = nogoCount > 0 ? (float)commissionCount / nogoCount : 0f;
        float omissionRate   = goCount   > 0 ? (float)omissionCount   / goCount   : 0f;

        TaskResult result;
        if (omissionRate > OmissionThresh)         result = TaskResult.Omission;
        else if (commissionRate > CommissionThresh) result = TaskResult.Commission;
        else                                        result = TaskResult.Success;

        ResolutionPending = true; // outcome computed — base expiry must not overwrite it
        StartCoroutine(CoFinish(result, commissionCount, omissionCount, goCount, nogoCount));
    }

    // Mission-level timeout mid-run: keep the error counts accumulated so far.
    protected override void HandleExpiry()
    {
        if (Engaged)
            AssessmentResults.Report(this,
                ("commission", commissionCount.ToString()),
                ("omission", omissionCount.ToString()));
        base.HandleExpiry();
    }

    private IEnumerator CoFinish(TaskResult result, int comm, int omis, int goN, int nogoN)
    {
        ClearButtons();
        HideShape();
        if (result == TaskResult.Success)
        {
            AudioManager.Instance.PlaySfx("success_chime");
            AudioManager.Instance.PlayVoice("correct");
            ShowMessage("INHIBITION MAINTAINED", new Color(0.4f, 1f, 0.5f));
            ShowSplash("PASS", new Color(0.3f, 1f, 0.4f), 1.2f);
        }
        else if (result == TaskResult.Commission)
        {
            AudioManager.Instance.PlaySfx("fail_buzz");
            AudioManager.Instance.PlayVoice("incorrect");
            ShowMessage("FALSE STARTS  " + comm + " / " + nogoN, new Color(1f, 0.4f, 0.3f));
            ShowSplash("TOO MANY FALSE STARTS", new Color(1f, 0.3f, 0.3f), 1.2f, 60f);
        }
        else // Omission
        {
            AudioManager.Instance.PlaySfx("fail_buzz");
            AudioManager.Instance.PlayVoice("incorrect");
            ShowMessage("MISSED TARGETS  " + omis + " / " + goN, new Color(1f, 0.6f, 0.3f));
            ShowSplash("MISSED TOO MANY TARGETS", new Color(1f, 0.55f, 0.2f), 1.2f, 60f);
        }
        yield return new WaitForSeconds(1.2f);
        Resolve(result);
    }

    protected override void OnDestroy()
    {
        if (flowCo   != null) { StopCoroutine(flowCo);   flowCo   = null; }
        if (appearCo != null) { StopCoroutine(appearCo); appearCo = null; }
        base.OnDestroy();
    }
}
