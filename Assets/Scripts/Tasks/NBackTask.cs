using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Life-Support cognitive task: 2-Back vigilance.
/// 12 trials, one symbol every 1.4s. Exactly 4 trials are 2-back hits.
/// Player presses the big red ALERT button when current == symbol 2-back.
/// Pass: Hits >= 3 AND FalseAlarms <= 2 -> Success, else Fail.
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

    private void Awake()
    {
        TaskName = "NBack";
        priority = TaskPriority.NonCritical;
        timeLimit = 60f;
    }

    public override void Activate()
    {
        base.Activate();
        BuildSequence();

        StationUI?.SetInstruction("LIFE SUPPORT: 2-back vigilance");
        ShowMessage("ALERT WHEN CURRENT MATCHES 2 AGO", Color.white);

        // Big central symbol display.
        GameObject stim = new GameObject("Symbol", typeof(RectTransform));
        stim.transform.SetParent(buttonsParent, false);
        symbolText = stim.AddComponent<TMPro.TextMeshProUGUI>();
        symbolText.alignment = TMPro.TextAlignmentOptions.Center;
        symbolText.fontSize = 120f;
        symbolText.fontStyle = TMPro.FontStyles.Bold;
        symbolText.color = Color.white;
        symbolText.text = "--";
        RectTransform srt = stim.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, 60f);
        srt.sizeDelta = new Vector2(600f, 200f);

        // Big red ALERT button.
        SpawnButton(new Vector2(0f, -150f), new Vector2(280f, 100f), "ALERT",
            new Color(0.85f, 0.15f, 0.15f), OnAlert);

        StartCoroutine(CoRun());
    }

    /// <summary>Build a 12-trial sequence with exactly 4 two-back hits.</summary>
    private void BuildSequence()
    {
        trial = new int[TrialCount];
        // First two: random.
        trial[0] = Random.Range(0, Symbols.Length);
        trial[1] = Random.Range(0, Symbols.Length);

        // Choose which of indices [2..11] (10 slots) are hits. Exactly 4.
        List<int> candidateIndices = new List<int>();
        for (int i = 2; i < TrialCount; i++) candidateIndices.Add(i);
        // Shuffle.
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
                trial[i] = trial[i - 2]; // exact 2-back match
            }
            else
            {
                // Pick something that is NOT trial[i-2].
                int v = Random.Range(0, Symbols.Length - 1);
                if (v >= trial[i - 2]) v++;
                trial[i] = v;
            }
        }
    }

    private IEnumerator CoRun()
    {
        // Brief pause so the player reads the header.
        yield return new WaitForSeconds(0.6f);

        for (int i = 0; i < TrialCount && IsActive; i++)
        {
            currentTrial = i;
            alertPressedThisTrial = false;
            if (symbolText != null) symbolText.text = Symbols[trial[i]];

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
        }

        if (!IsActive) yield break;

        if (hits >= 3 && falseAlarms <= 2) Resolve(TaskResult.Success);
        else Resolve(TaskResult.Fail);
    }

    private void OnAlert()
    {
        if (!IsActive) return;
        if (!IsDocked) return;
        if (currentTrial < 0) return;
        // Only the first press in a trial counts toward scoring.
        alertPressedThisTrial = true;
    }
}
