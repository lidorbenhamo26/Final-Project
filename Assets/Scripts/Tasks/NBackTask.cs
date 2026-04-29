using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Life-Support cognitive task: 2-Back vigilance.
/// Flow (begins after the player docks):
///   - "READY?" -> 3-2-1 -> "GO!"
///   - 12 trials, one symbol every 1.4s. Exactly 4 trials are 2-back hits.
///   - Big red ALERT button is always visible and clickable; non-presses
///     count as correct rejections for non-hit trials.
///   - Visible round counter and live hit/false-alarm score.
///   - Pass: Hits >= 3 AND FalseAlarms <= 2 -> Success, else Fail.
/// </summary>
public class NBackTask : CognitiveTaskBase
{
    private static readonly string[] Symbols = { "O2", "CO2", "H2O", "N2", "HE" };
    private const int TrialCount = 12;
    private const int TargetHits = 4;
    private const float TrialInterval = 1.4f;

    private int[] trial;
    private int currentTrial = -1;
    private bool alertPressedThisTrial;
    private int hits, misses, falseAlarms, correctRejections;

    private TMPro.TextMeshProUGUI symbolText;
    private TMPro.TMP_Text trialLabel;
    private TMPro.TMP_Text scoreLabel;
    private Image alertFlash;
    private bool started;
    private Coroutine flowCo;

    private void Awake()
    {
        TaskName = "NBack";
        priority = TaskPriority.NonCritical;
        timeLimit = 90f;
    }

    public override void Activate()
    {
        base.Activate();
        BuildSequence();

        StationUI?.SetInstruction("LIFE SUPPORT: 2-back vigilance");
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));

        // Big central symbol display.
        GameObject stim = new GameObject("Symbol", typeof(RectTransform));
        stim.transform.SetParent(buttonsParent, false);
        symbolText = stim.AddComponent<TMPro.TextMeshProUGUI>();
        symbolText.alignment = TMPro.TextAlignmentOptions.Center;
        symbolText.fontSize = 130f;
        symbolText.fontStyle = TMPro.FontStyles.Bold;
        symbolText.color = Color.white;
        symbolText.text = "--";
        RectTransform srt = stim.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, 50f);
        srt.sizeDelta = new Vector2(700f, 220f);

        // HUD labels (top corners).
        trialLabel = SpawnLabel(new Vector2(-280f, 170f), new Vector2(240f, 50f),
            "Trial 0 / " + TrialCount, new Color(0.85f, 0.9f, 1f), 28f);
        scoreLabel = SpawnLabel(new Vector2(280f, 170f), new Vector2(240f, 50f),
            "Hits 0  FA 0", new Color(0.85f, 0.9f, 1f), 28f);

        // Big red ALERT button — always present.
        Button alertBtn = SpawnButton(new Vector2(0f, -150f), new Vector2(320f, 110f), "ALERT",
            new Color(0.85f, 0.15f, 0.15f), OnAlert);
        if (alertBtn != null) alertFlash = alertBtn.GetComponent<Image>();
    }

    protected override void OnDocked()
    {
        if (started) return;
        started = true;
        flowCo = StartCoroutine(CoRun());
    }

    /// <summary>Build a 12-trial sequence with exactly 4 two-back hits.</summary>
    private void BuildSequence()
    {
        trial = new int[TrialCount];
        trial[0] = Random.Range(0, Symbols.Length);
        trial[1] = Random.Range(0, Symbols.Length);

        List<int> candidateIndices = new List<int>();
        for (int i = 2; i < TrialCount; i++) candidateIndices.Add(i);
        for (int i = candidateIndices.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (candidateIndices[i], candidateIndices[j]) = (candidateIndices[j], candidateIndices[i]);
        }
        HashSet<int> hitSet = new HashSet<int>();
        for (int k = 0; k < TargetHits; k++) hitSet.Add(candidateIndices[k]);

        for (int i = 2; i < TrialCount; i++)
        {
            if (hitSet.Contains(i))
            {
                trial[i] = trial[i - 2];
            }
            else
            {
                int v = Random.Range(0, Symbols.Length - 1);
                if (v >= trial[i - 2]) v++;
                trial[i] = v;
            }
        }
    }

    private IEnumerator CoRun()
    {
        // Countdown.
        ShowMessage("READY?", new Color(0.9f, 0.95f, 1f));
        yield return new WaitForSeconds(0.7f);
        for (int n = 3; n >= 1 && IsActive; n--)
        {
            ShowMessage(n.ToString(), new Color(1f, 0.9f, 0.4f));
            yield return new WaitForSeconds(0.45f);
        }
        if (!IsActive) yield break;
        ShowMessage("GO!", new Color(0.4f, 1f, 0.5f));
        yield return new WaitForSeconds(0.35f);

        ShowMessage("ALERT WHEN CURRENT MATCHES 2 AGO", Color.white);

        for (int i = 0; i < TrialCount && IsActive; i++)
        {
            currentTrial = i;
            alertPressedThisTrial = false;
            if (symbolText != null) symbolText.text = Symbols[trial[i]];
            UpdateHud();

            float t0 = Time.time;
            while (IsActive && (Time.time - t0) < TrialInterval)
            {
                yield return null;
            }

            // Score this trial.
            bool isHitTrial = (i >= 2) && (trial[i] == trial[i - 2]);
            if (isHitTrial && alertPressedThisTrial) hits++;
            else if (isHitTrial && !alertPressedThisTrial) misses++;
            else if (!isHitTrial && alertPressedThisTrial) falseAlarms++;
            else correctRejections++;

            UpdateHud();
        }

        if (!IsActive) yield break;

        // Final.
        if (symbolText != null) symbolText.text = "--";
        bool pass = (hits >= 3 && falseAlarms <= 2);
        if (pass)
        {
            ShowMessage("HITS " + hits + " / " + TargetHits + "   FALSE ALARMS " + falseAlarms,
                new Color(0.4f, 1f, 0.5f));
            ShowSplash("PASS!", new Color(0.3f, 1f, 0.4f), 1.2f);
            yield return new WaitForSeconds(1.2f);
            Resolve(TaskResult.Success);
        }
        else
        {
            ShowMessage("HITS " + hits + " / " + TargetHits + "   FALSE ALARMS " + falseAlarms,
                new Color(1f, 0.5f, 0.3f));
            ShowSplash("FAIL", new Color(1f, 0.3f, 0.3f), 1.2f);
            yield return new WaitForSeconds(1.2f);
            Resolve(TaskResult.Fail);
        }
    }

    private void UpdateHud()
    {
        if (trialLabel != null)
            trialLabel.text = "Trial " + Mathf.Min(currentTrial + 1, TrialCount) + " / " + TrialCount;
        if (scoreLabel != null)
            scoreLabel.text = "Hits " + hits + "  FA " + falseAlarms;
    }

    private void OnAlert()
    {
        if (!IsActive) return;
        if (!IsDocked) return;
        if (currentTrial < 0) return;
        if (alertPressedThisTrial) return; // only first press counts
        alertPressedThisTrial = true;

        // Brief visual flash so the player knows the click registered.
        if (alertFlash != null) StartCoroutine(CoAlertFlash());
    }

    private IEnumerator CoAlertFlash()
    {
        if (alertFlash == null) yield break;
        Color baseColor = new Color(0.85f, 0.15f, 0.15f);
        Color hot = new Color(1f, 0.95f, 0.4f);
        alertFlash.color = hot;
        yield return new WaitForSeconds(0.18f);
        if (alertFlash != null) alertFlash.color = baseColor;
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        base.OnDestroy();
    }
}
