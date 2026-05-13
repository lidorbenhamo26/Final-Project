using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Comms cognitive task: Go/No-Go inhibition.
/// 20 trials. Each shows a signal panel for 1.2s, then a 0.6-1.0s blank ISI.
///   - GO trial (70%): red border + alert icon + "CRITICAL ALERT". Click EXECUTE within window.
///   - NO-GO trial (30%): yellow border + prohibit icon + "DRILL MODE". DO NOT click; wait it out.
/// Schedule is pseudo-random with max 3 consecutive NO-GO trials.
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

    private const int   TotalTrials      = 20;
    private const int   NoGoCount        = 6;       // 30% NO-GO
    private const int   MaxNoGoStreak    = 3;
    private const float SignalDuration   = 1.2f;
    private const float IsiMin           = 0.6f;
    private const float IsiMax           = 1.0f;
    private const float CommissionThresh = 0.30f;
    private const float OmissionThresh   = 0.50f;

    private static readonly Color GoBorder   = new Color(0.92f, 0.18f, 0.18f);
    private static readonly Color NoGoBorder = new Color(0.98f, 0.80f, 0.12f);
    private static readonly Color BgDark    = new Color(0.06f, 0.07f, 0.10f, 1f);
    private static readonly Color ExecGreen = new Color(0.20f, 0.75f, 0.35f);

    // Iconic glyphs in the basic Unicode plane so the default TMP atlas renders them.
    private const string GoIconGlyph   = "▲"; // warning triangle
    private const string NoGoIconGlyph = "Ø"; // slashed O (prohibit)

    private Phase phase = Phase.Idle;
    private bool[] schedule;       // true = GO trial
    private bool[] wasCommission;  // true if trial i was a NO-GO commission

    private int   trialIdx;
    private bool  started;
    private bool  responded;
    private float signalStartTime;
    private Coroutine flowCo;

    private int commissionCount;
    private int omissionCount;
    private readonly List<float> hitRTs          = new List<float>();
    private readonly List<float> postErrorHitRTs = new List<float>();
    private readonly List<float> baselineHitRTs  = new List<float>();

    private TMP_Text   trialLabel;
    private GameObject signalPanel;       // invisible container (animates scale)
    private Image      signalGlowImg;     // outer halo
    private Image      signalBorderImg;   // colored ring
    private TMP_Text   signalIconText;
    private TMP_Text   signalHeaderText;
    private Coroutine  appearCo;

    private void Awake()
    {
        TaskName  = "Inhibit";
        priority  = TaskPriority.Critical;
        timeLimit = 120f; // 20 trials * ~2s + intro/outro buffer
    }

    public override void Activate()
    {
        base.Activate();
        StationUI?.SetInstruction("INHIBIT TASK: dock to begin");
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));

        GenerateSchedule();
        BuildSignalPanel();
        BuildTrialLabel();

        SessionManager.Instance?.LogCustomEvent("INH_Spawned", "CommsStation",
            "trials=" + TotalTrials + ",nogo=" + NoGoCount);
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
        SpawnButton(new Vector2(0f, -180f), new Vector2(280f, 100f), "READY",
            new Color(0.2f, 0.8f, 0.4f), OnStartReadyClicked);
    }

    private void OnStartReadyClicked()
    {
        if (flowCo != null) return;
        ClearButtons();
        // EXECUTE is always visible for the rest of the task. The handler only
        // accepts presses during the 1.2s signal window; outside that, it no-ops.
        SpawnButton(new Vector2(0f, -180f), new Vector2(280f, 90f), "EXECUTE",
            ExecGreen, OnExecutePressed);
        flowCo = StartCoroutine(CoRunTrials());
    }

    private void GenerateSchedule()
    {
        schedule      = new bool[TotalTrials];
        wasCommission = new bool[TotalTrials];

        // Random permutation with max-3 NO-GO streak. Retry on violation.
        var indices = new List<int>();
        for (int attempt = 0; attempt < 50; attempt++)
        {
            for (int i = 0; i < TotalTrials; i++) schedule[i] = true;
            indices.Clear();
            for (int i = 0; i < TotalTrials; i++) indices.Add(i);
            for (int i = 0; i < NoGoCount; i++)
            {
                int j = Random.Range(i, indices.Count);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
                schedule[indices[i]] = false; // mark as NO-GO
            }
            int streak = 0; bool ok = true;
            for (int i = 0; i < TotalTrials; i++)
            {
                if (!schedule[i]) { streak++; if (streak > MaxNoGoStreak) { ok = false; break; } }
                else streak = 0;
            }
            if (ok) return;
        }
        // Deterministic fallback: 6 NO-GO at positions 2,5,8,11,14,17 (max streak 1).
        for (int i = 0; i < TotalTrials; i++) schedule[i] = true;
        int placed = 0;
        for (int i = 2; i < TotalTrials && placed < NoGoCount; i += 3)
        {
            schedule[i] = false; placed++;
        }
    }

    private void BuildSignalPanel()
    {
        if (buttonsParent == null) return;

        // Outer wrapper (invisible) — used to animate scale on appearance.
        signalPanel = new GameObject("SignalPanel", typeof(RectTransform));
        signalPanel.transform.SetParent(buttonsParent, false);
        RectTransform pr = signalPanel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.pivot = new Vector2(0.5f, 0.5f);
        pr.anchoredPosition = new Vector2(0f, 30f);
        pr.sizeDelta = new Vector2(560f, 340f); // room for outer glow

        // Outer halo glow (larger than the colored border, faded color).
        GameObject glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(signalPanel.transform, false);
        RectTransform gr = glow.GetComponent<RectTransform>();
        gr.anchorMin = gr.anchorMax = new Vector2(0.5f, 0.5f);
        gr.pivot = new Vector2(0.5f, 0.5f);
        gr.anchoredPosition = Vector2.zero;
        gr.sizeDelta = new Vector2(560f, 340f);
        signalGlowImg = glow.GetComponent<Image>();
        signalGlowImg.color = new Color(GoBorder.r, GoBorder.g, GoBorder.b, 0.18f);
        signalGlowImg.raycastTarget = false;

        // Colored ring (the bordered panel).
        GameObject border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(signalPanel.transform, false);
        RectTransform br = border.GetComponent<RectTransform>();
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
        br.pivot = new Vector2(0.5f, 0.5f);
        br.anchoredPosition = Vector2.zero;
        br.sizeDelta = new Vector2(480f, 260f);
        signalBorderImg = border.GetComponent<Image>();
        signalBorderImg.color = GoBorder;
        signalBorderImg.raycastTarget = false;

        // Dark inner fill leaves a 10px colored ring around the border image.
        GameObject inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
        inner.transform.SetParent(border.transform, false);
        RectTransform ir = inner.GetComponent<RectTransform>();
        ir.anchorMin = Vector2.zero;
        ir.anchorMax = Vector2.one;
        ir.offsetMin = new Vector2(10f, 10f);
        ir.offsetMax = new Vector2(-10f, -10f);
        Image innerImg = inner.GetComponent<Image>();
        innerImg.color = BgDark;
        innerImg.raycastTarget = false;

        GameObject icon = new GameObject("Icon", typeof(RectTransform));
        icon.transform.SetParent(inner.transform, false);
        signalIconText = icon.AddComponent<TextMeshProUGUI>();
        signalIconText.alignment = TextAlignmentOptions.Center;
        signalIconText.fontSize = 180f;
        signalIconText.fontStyle = FontStyles.Bold;
        signalIconText.text = GoIconGlyph;
        signalIconText.color = GoBorder;
        signalIconText.raycastTarget = false;
        RectTransform iconRt = icon.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.28f);
        iconRt.anchorMax = new Vector2(1f, 1f);
        iconRt.offsetMin = Vector2.zero;
        iconRt.offsetMax = Vector2.zero;

        GameObject hdr = new GameObject("Header", typeof(RectTransform));
        hdr.transform.SetParent(inner.transform, false);
        signalHeaderText = hdr.AddComponent<TextMeshProUGUI>();
        signalHeaderText.alignment = TextAlignmentOptions.Center;
        signalHeaderText.fontSize = 40f;
        signalHeaderText.fontStyle = FontStyles.Bold;
        signalHeaderText.characterSpacing = 8f;
        signalHeaderText.text = "CRITICAL ALERT";
        signalHeaderText.color = GoBorder;
        signalHeaderText.raycastTarget = false;
        RectTransform hRt = hdr.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0f, 0f);
        hRt.anchorMax = new Vector2(1f, 0.28f);
        hRt.offsetMin = Vector2.zero;
        hRt.offsetMax = Vector2.zero;

        signalPanel.SetActive(false);
    }

    private void BuildTrialLabel()
    {
        trialLabel = SpawnLabel(new Vector2(0f, 200f), new Vector2(400f, 40f),
            "Trial 0 / " + TotalTrials, new Color(0.85f, 0.9f, 1f), 28f);
    }

    private void SetSignalForTrial(bool isGo)
    {
        Color c = isGo ? GoBorder : NoGoBorder;
        signalBorderImg.color = c;
        signalGlowImg.color   = new Color(c.r, c.g, c.b, 0.22f);
        signalIconText.text   = isGo ? GoIconGlyph   : NoGoIconGlyph;
        signalIconText.color  = c;
        signalHeaderText.text = isGo ? "CRITICAL ALERT" : "DRILL MODE";
        signalHeaderText.color = c;
    }

    private IEnumerator CoAppearPulse()
    {
        // Snap-in scale + a single subtle pulse during the 1.2s lifespan.
        Transform t = signalPanel.transform;
        const float inDur = 0.16f;
        for (float u = 0f; u < inDur; u += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0.88f, 1.04f, u / inDur);
            t.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        for (float u = 0f; u < 0.08f; u += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(1.04f, 1.0f, u / 0.08f);
            t.localScale = new Vector3(k, k, 1f);
            yield return null;
        }
        t.localScale = Vector3.one;
        // Hold the glow pulse subtly for the rest of the signal window.
        Color baseGlow = signalGlowImg.color;
        float t0 = Time.time;
        while (Time.time - t0 < SignalDuration - inDur - 0.08f)
        {
            float s = 0.18f + 0.12f * Mathf.Abs(Mathf.Sin((Time.time - t0) * 6f));
            signalGlowImg.color = new Color(baseGlow.r, baseGlow.g, baseGlow.b, s);
            yield return null;
        }
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
        ShowMessage("MONITOR ALERTS", new Color(0.4f, 1f, 0.5f));
        yield return new WaitForSeconds(0.35f);

        for (trialIdx = 0; trialIdx < TotalTrials && IsActive; trialIdx++)
        {
            phase = Phase.ISI;
            signalPanel.SetActive(false);
            yield return new WaitForSeconds(Random.Range(IsiMin, IsiMax));
            if (!IsActive) yield break;

            bool isGo = schedule[trialIdx];
            string signalId = (isGo ? "GO_" : "NOGO_") + trialIdx;
            SessionManager.Instance?.LogCustomEvent("INH_TrialStart", "CommsStation",
                "trialIdx=" + trialIdx + ",isGo=" + isGo + ",signalId=" + signalId);

            phase = Phase.Signal;
            responded = false;
            signalStartTime = Time.time;
            SetSignalForTrial(isGo);
            signalPanel.transform.localScale = new Vector3(0.88f, 0.88f, 1f);
            signalPanel.SetActive(true);
            if (appearCo != null) StopCoroutine(appearCo);
            appearCo = StartCoroutine(CoAppearPulse());
            if (trialLabel != null)
                trialLabel.text = "Trial " + (trialIdx + 1) + " / " + TotalTrials;

            while (IsActive && phase == Phase.Signal && !responded
                   && (Time.time - signalStartTime) < SignalDuration)
            {
                yield return null;
            }

            signalPanel.SetActive(false);
            if (IsActive && !responded) HandleTimeout(isGo);
        }

        phase = Phase.Done;
        signalPanel.SetActive(false);
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
        bool isGo = schedule[trialIdx];
        float rtMs = (Time.time - signalStartTime) * 1000f;
        string result;
        if (isGo)
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

    private void HandleTimeout(bool isGo)
    {
        string result;
        if (isGo) { omissionCount++; result = "omission"; }
        else       result = "correct";
        SessionManager.Instance?.LogCustomEvent("INH_Response", "CommsStation",
            "trialIdx=" + trialIdx + ",rtMs=-1,result=" + result);
    }

    private void ResolveTask()
    {
        int goCount   = TotalTrials - NoGoCount; // 14
        int nogoCount = NoGoCount;               // 6

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

        SessionManager.Instance?.LogCustomEvent("INH_Summary", "CommsStation",
            "commission=" + commissionCount +
            ",omission=" + omissionCount +
            ",hitRT_mean=" + hitMean.ToString("F0") +
            ",hitRT_sd=" + hitSD.ToString("F0") +
            ",postErrorSlowing=" + pesStr);

        float commissionRate = nogoCount > 0 ? (float)commissionCount / nogoCount : 0f;
        float omissionRate   = goCount   > 0 ? (float)omissionCount   / goCount   : 0f;

        TaskResult result;
        if (omissionRate > OmissionThresh)         result = TaskResult.Omission;
        else if (commissionRate > CommissionThresh) result = TaskResult.Commission;
        else                                        result = TaskResult.Success;

        StartCoroutine(CoFinish(result, commissionCount, omissionCount, goCount, nogoCount));
    }

    private IEnumerator CoFinish(TaskResult result, int comm, int omis, int goN, int nogoN)
    {
        ClearButtons();
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
            ShowMessage("MISSED ALERTS  " + omis + " / " + goN, new Color(1f, 0.6f, 0.3f));
            ShowSplash("MISSED TOO MANY ALERTS", new Color(1f, 0.55f, 0.2f), 1.2f, 60f);
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
