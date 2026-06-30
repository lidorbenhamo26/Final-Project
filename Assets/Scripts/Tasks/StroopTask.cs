using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Comms cognitive task: color-word Stroop.
/// </summary>
public class StroopTask : CognitiveTaskBase
{
    private enum Phase { Idle, Trial, Done }

    private static readonly string[] WordNames = { "RED", "BLUE", "GREEN", "YELLOW" };
    private static readonly Color[] Inks =
    {
        new Color(0.92f, 0.18f, 0.18f),
        new Color(0.20f, 0.45f, 0.95f),
        new Color(0.25f, 0.80f, 0.35f),
        new Color(0.95f, 0.85f, 0.15f),
    };

    private const int RoundCount = 6;
    private const int RoundsPerBlock = 3; // 2 blocks => exactly ONE clearly-announced rule switch
    private const float RoundLimit = 4f;

    private static readonly Color InkRuleColor  = new Color(0.45f, 0.85f, 1f);
    private static readonly Color WordRuleColor = new Color(1f, 0.82f, 0.30f);

    private Phase phase = Phase.Idle;
    private int round;
    private int correct;
    private int currentWordIdx;
    private int currentInkIdx;
    private bool matchInk;
    private bool startMatchInk; // rule of the FIRST block (randomized per task); block 2 is the opposite
    private bool answered;
    private bool roundsStarted;
    private float roundStartTime;
    private int answeredCount;
    private readonly List<float> roundRtsMs = new List<float>();

    private TMPro.TextMeshProUGUI stimulusText;

    private void Awake()
    {
        TaskName = "Stroop";
        priority = TaskPriority.Critical;
        timeLimit = 60f;
    }

    protected override string InstructionTitle => "COMMS - STROOP";
    protected override string[] InstructionBody => new[]
    {
        "A color word appears in a colored ink.",
        "The banner at the top tells you the current rule:",
        "",
        "MATCH THE INK = tap the ink's color.",
        "MATCH THE WORD = tap what the word says.",
        "",
        "The rule changes ONCE, halfway through -",
        "watch for the big NEW RULE banner.",
    };

    public override void Activate()
    {
        base.Activate();
        // Randomize which rule the first block uses so order effects average out.
        startMatchInk = Random.value < 0.5f;
        StationUI?.SetInstruction("STROOP TASK: dock to respond");
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));
        BuildStimulus();
    }

    // First block uses startMatchInk; the second block uses the opposite rule.
    private bool RuleForRound(int idx) => (idx < RoundsPerBlock) ? startMatchInk : !startMatchInk;

    // Rounds start on first dock (like Inhibit/Radar) so every presented trial
    // was actually seen by the player; never docking ends as a clean Omission
    // via the base 60s time limit instead of a fake accuracy-0 Fail.
    protected override void OnDocked()
    {
        if (roundsStarted) return;
        roundsStarted = true;
        StartCoroutine(CoRunRounds());
    }

    private void BuildStimulus()
    {
        if (buttonsParent == null) return;

        var stim = new GameObject("Stimulus", typeof(RectTransform));
        stim.transform.SetParent(buttonsParent, false);
        stimulusText = stim.AddComponent<TMPro.TextMeshProUGUI>();
        stimulusText.alignment = TMPro.TextAlignmentOptions.Center;
        stimulusText.fontSize = 80f;
        stimulusText.fontStyle = TMPro.FontStyles.Bold;
        stimulusText.text = "";

        var rt = stim.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, 80f);
        rt.sizeDelta = new Vector2(600f, 160f);
    }

    private IEnumerator CoRunRounds()
    {
        yield return null;

        for (round = 0; round < RoundCount && IsActive; round++)
        {
            // Hold the next round until the player is docked again — undocked
            // time must not burn trials. The base time limit still gates this.
            while (IsActive && !IsDocked) yield return null;
            if (!IsActive) yield break;

            bool rule = RuleForRound(round);
            // Announce the rule at the start of each block: a plain intro for the
            // first, a big "NEW RULE" callout for the single mid-task switch.
            if (round % RoundsPerBlock == 0)
            {
                yield return CoAnnounceRule(rule, round == 0);
                while (IsActive && !IsDocked) yield return null; // may have undocked during the banner
                if (!IsActive) yield break;
            }

            StartRound(round, rule);
            float elapsed = 0f;
            while (IsActive && phase == Phase.Trial && !answered && elapsed < RoundLimit)
            {
                // Debug freeze (F11 / assessor report): pause the round clock
                // and slide the RT anchor so reaction times stay correct.
                if (GameManager.IsDebugFrozen) roundStartTime += Time.deltaTime;
                else elapsed += Time.deltaTime;
                yield return null;
            }

            // Unanswered round while docked = an attention/inhibition lapse.
            yield return FrozenWait(0.4f);
        }

        if (!IsActive) yield break;
        phase = Phase.Done;

        float rtMean = 0f;
        for (int i = 0; i < roundRtsMs.Count; i++) rtMean += roundRtsMs[i];
        if (roundRtsMs.Count > 0) rtMean /= roundRtsMs.Count;
        float accuracy = (float)correct / RoundCount;
        // "NA", not 0: a zero would read as a real 0 ms reaction time downstream.
        string rtMeanStr = roundRtsMs.Count > 0 ? Num.F0(rtMean) : "NA";

        SessionManager.Instance?.LogCustomEvent("STROOP_Summary", "CommsStation",
            "correct=" + correct + " rounds=" + RoundCount + " answered=" + answeredCount +
            " accuracy=" + Num.F3(accuracy) + " rtMeanMs=" + rtMeanStr);
        AssessmentResults.Report(this,
            ("correct", correct.ToString()),
            ("rounds", RoundCount.ToString()),
            ("answered", answeredCount.ToString()),
            ("accuracy", Num.F3(accuracy)),
            ("rtMeanMs", rtMeanStr));

        Resolve(correct >= 4 ? TaskResult.Success : TaskResult.Fail);
    }

    // Big block-rule announcement: plain "RULE" intro for block 1, a loud
    // "NEW RULE" callout (console splash + HUD banner + sound) for the single
    // mid-task switch so the player is never surprised by a silent flip.
    private IEnumerator CoAnnounceRule(bool ink, bool first)
    {
        string body = ink ? "MATCH THE INK COLOR" : "MATCH THE WORD";
        Color color = ink ? InkRuleColor : WordRuleColor;
        ShowMessage("RULE: " + body, color);
        ShowSplash((first ? "RULE" : "NEW RULE!") + "\n" + body, color, 1.8f, 54f);
        if (!first)
        {
            HUDManager.Instance?.ShowAlertBanner("NEW RULE - " + body, 1.8f);
            AudioManager.Instance?.PlaySfx("fail_buzz", 0.6f); // distinct attention cue
        }
        AudioManager.Instance?.PlaySfx("button_click");
        SessionManager.Instance?.LogCustomEvent("STROOP_RuleBlock", "CommsStation",
            "block=" + (first ? 1 : 2) + " rule=" + (ink ? "ink" : "word"));
        yield return FrozenWait(1.8f);
    }

    private void StartRound(int idx, bool rule)
    {
        phase = Phase.Trial;
        answered = false;
        roundStartTime = Time.time;

        currentWordIdx = Random.Range(0, WordNames.Length);
        currentInkIdx = Random.value < 0.5f
            ? currentWordIdx
            : (currentWordIdx + Random.Range(1, WordNames.Length)) % WordNames.Length;

        matchInk = rule;
        // Persistent, color-coded rule banner on every trial — the player can
        // always re-check the rule mid-round.
        ShowMessage(matchInk ? "RULE: MATCH THE INK COLOR" : "RULE: MATCH THE WORD",
                    matchInk ? InkRuleColor : WordRuleColor);

        if (stimulusText != null)
        {
            stimulusText.text = WordNames[currentWordIdx];
            stimulusText.color = Inks[currentInkIdx];
        }

        ClearButtons();
        const float btnW = 130f;
        const float btnH = 80f;
        const float gap = 16f;
        float totalW = WordNames.Length * btnW + (WordNames.Length - 1) * gap;
        float startX = -totalW * 0.5f + btnW * 0.5f;

        for (int i = 0; i < WordNames.Length; i++)
        {
            int captured = i;
            Vector2 pos = new Vector2(startX + i * (btnW + gap), -120f);
            SpawnButton(pos, new Vector2(btnW, btnH), WordNames[i], Inks[i],
                () => OnAnswer(captured));
        }
    }

    private void OnAnswer(int answerIdx)
    {
        StationDockController.Instance?.handsView?.TriggerPress();
        if (!IsActive || phase != Phase.Trial || answered) return;
        if (!IsDocked) return;

        answered = true;
        int target = matchInk ? currentInkIdx : currentWordIdx;
        bool ok = answerIdx == target;
        if (ok) correct++;

        answeredCount++;
        float rtMs = (Time.time - roundStartTime) * 1000f;
        roundRtsMs.Add(rtMs);
        SessionManager.Instance?.LogCustomEvent("STROOP_Round", "CommsStation",
            "round=" + round + " rule=" + (matchInk ? "ink" : "word") +
            " word=" + WordNames[currentWordIdx] + " ink=" + WordNames[currentInkIdx] +
            " congruent=" + (currentWordIdx == currentInkIdx) +
            " choice=" + WordNames[answerIdx] + " correct=" + ok +
            " rtMs=" + Num.F0(rtMs));
    }

    // Mission-level timeout (e.g. the player never docked): record whatever
    // was answered so the Omission row isn't empty.
    protected override void HandleExpiry()
    {
        if (Engaged)
            AssessmentResults.Report(this,
                ("correct", correct.ToString()),
                ("rounds", RoundCount.ToString()),
                ("answered", answeredCount.ToString()));
        base.HandleExpiry();
    }
}
