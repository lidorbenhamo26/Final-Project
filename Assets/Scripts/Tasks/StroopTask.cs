using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Comms cognitive task: Color-Word Stroop.
/// Flow (begins after the player docks):
///   - 6 rounds. Each round shows a colored word and the header alternates
///     between "MATCH THE INK COLOR" and "MATCH THE WORD MEANING".
///   - 4s per round. A small round counter and live score are visible.
///   - Round-end splash flashes CORRECT! / WRONG! / TIME!
///   - Pass threshold: >= 4/6 correct -> Success, else Fail.
/// </summary>
public class StroopTask : CognitiveTaskBase
{
    private enum Phase { Idle, Countdown, Trial, Done }

    private static readonly string[] WordNames = { "RED", "BLUE", "GREEN", "YELLOW" };
    private static readonly Color[] Inks =
    {
        new Color(0.92f, 0.18f, 0.18f),
        new Color(0.20f, 0.45f, 0.95f),
        new Color(0.25f, 0.80f, 0.35f),
        new Color(0.95f, 0.85f, 0.15f),
    };

    private const int RoundCount = 6;
    private const int PassThreshold = 4;
    private const float RoundLimit = 4f;

    private Phase phase = Phase.Idle;
    private int round;
    private int correct;
    private int currentWordIdx;
    private int currentInkIdx;
    private bool matchInk;
    private bool answered;
    private bool wasCorrect;

    private TMPro.TextMeshProUGUI stimulusText;
    private TMPro.TMP_Text roundLabel;
    private TMPro.TMP_Text scoreLabel;
    private bool started;
    private Coroutine flowCo;

    private void Awake()
    {
        TaskName = "Stroop";
        priority = TaskPriority.Critical;
        timeLimit = 90f;
    }

    public override void Activate()
    {
        base.Activate();
        StationUI?.SetInstruction("STROOP TASK: dock to respond");
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));

        // Build central stimulus text once; mutate it per round.
        GameObject stim = new GameObject("Stimulus", typeof(RectTransform));
        stim.transform.SetParent(buttonsParent, false);
        stimulusText = stim.AddComponent<TMPro.TextMeshProUGUI>();
        stimulusText.alignment = TMPro.TextAlignmentOptions.Center;
        stimulusText.fontSize = 90f;
        stimulusText.fontStyle = TMPro.FontStyles.Bold;
        stimulusText.text = "";
        RectTransform srt = stim.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, 60f);
        srt.sizeDelta = new Vector2(700f, 180f);

        // Round / score HUD labels (top corners of button area).
        roundLabel = SpawnLabel(new Vector2(-280f, 170f), new Vector2(220f, 50f),
            "Round 0 / " + RoundCount, new Color(0.85f, 0.9f, 1f), 28f);
        scoreLabel = SpawnLabel(new Vector2(280f, 170f), new Vector2(220f, 50f),
            "Score 0", new Color(0.85f, 0.9f, 1f), 28f);
    }

    protected override void OnDocked()
    {
        if (started) return;
        started = true;
        flowCo = StartCoroutine(CoRunRounds());
    }

    private IEnumerator CoRunRounds()
    {
        // Countdown.
        phase = Phase.Countdown;
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

        for (round = 0; round < RoundCount && IsActive; round++)
        {
            StartRound(round);

            float t0 = Time.time;
            while (IsActive && phase == Phase.Trial && !answered && (Time.time - t0) < RoundLimit)
            {
                yield return null;
            }

            // Round-end feedback splash.
            ClearTrialButtons();
            if (!answered)
            {
                ShowSplash("TIME!", new Color(1f, 0.6f, 0.2f), 0.7f);
            }
            else if (wasCorrect)
            {
                ShowSplash("CORRECT!", new Color(0.3f, 1f, 0.4f), 0.7f);
            }
            else
            {
                ShowSplash("WRONG!", new Color(1f, 0.3f, 0.3f), 0.7f);
            }
            UpdateHud();
            yield return new WaitForSeconds(0.75f);
        }

        if (!IsActive) yield break;

        phase = Phase.Done;
        if (stimulusText != null) stimulusText.text = "";
        if (correct >= PassThreshold)
        {
            ShowMessage("MISSION COMPLETE  " + correct + " / " + RoundCount,
                new Color(0.4f, 1f, 0.5f));
            ShowSplash("PASS!", new Color(0.3f, 1f, 0.4f), 1.2f);
            yield return new WaitForSeconds(1.2f);
            Resolve(TaskResult.Success);
        }
        else
        {
            ShowMessage("INSUFFICIENT  " + correct + " / " + RoundCount,
                new Color(1f, 0.5f, 0.3f));
            ShowSplash("FAIL", new Color(1f, 0.3f, 0.3f), 1.2f);
            yield return new WaitForSeconds(1.2f);
            Resolve(TaskResult.Fail);
        }
    }

    private void StartRound(int idx)
    {
        phase = Phase.Trial;
        answered = false;
        wasCorrect = false;

        currentWordIdx = Random.Range(0, WordNames.Length);
        if (Random.value < 0.5f) currentInkIdx = currentWordIdx;
        else currentInkIdx = (currentWordIdx + Random.Range(1, WordNames.Length)) % WordNames.Length;

        matchInk = (idx % 2 == 0);
        ShowMessage(matchInk ? "MATCH THE INK COLOR" : "MATCH THE WORD MEANING", Color.white);

        if (stimulusText != null)
        {
            stimulusText.text = WordNames[currentWordIdx];
            stimulusText.color = Inks[currentInkIdx];
        }

        UpdateHud();

        // Rebuild only the answer buttons, not the HUD labels.
        ClearTrialButtons();
        float btnW = 150f, btnH = 90f, gap = 18f;
        float totalW = WordNames.Length * btnW + (WordNames.Length - 1) * gap;
        float startX = -totalW * 0.5f + btnW * 0.5f;
        for (int i = 0; i < WordNames.Length; i++)
        {
            int captured = i;
            Vector2 pos = new Vector2(startX + i * (btnW + gap), -130f);
            SpawnButton(pos, new Vector2(btnW, btnH), WordNames[i], Inks[i],
                () => OnAnswer(captured));
        }
    }

    private void ClearTrialButtons()
    {
        // ClearButtons() removes everything spawned via SpawnButton, which is
        // exactly what we want — the HUD/Stimulus were created via
        // SpawnLabel/AddComponent and aren't tracked.
        ClearButtons();
    }

    private void UpdateHud()
    {
        if (roundLabel != null)
            roundLabel.text = "Round " + Mathf.Min(round + 1, RoundCount) + " / " + RoundCount;
        if (scoreLabel != null)
            scoreLabel.text = "Score " + correct;
    }

    private void OnAnswer(int answerIdx)
    {
        if (!IsActive || phase != Phase.Trial || answered) return;
        if (!IsDocked) return;
        answered = true;
        int target = matchInk ? currentInkIdx : currentWordIdx;
        wasCorrect = (answerIdx == target);
        if (wasCorrect) correct++;
        UpdateHud();
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        base.OnDestroy();
    }
}
