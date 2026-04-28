using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Comms cognitive task: Color-Word Stroop.
/// 6 rounds of conflict trials. Header alternates between "MATCH THE INK COLOR"
/// and "MATCH THE WORD MEANING". Answer within 4s/round.
/// Pass threshold: >= 4/6 correct -> Success, else Fail.
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
    private const float RoundLimit = 4f;

    private Phase phase = Phase.Idle;
    private int round;
    private int correct;
    private int currentWordIdx;
    private int currentInkIdx;
    private bool matchInk; // true: answer = ink color, false: answer = word meaning
    private float roundStartTime;
    private bool answered;

    private TMPro.TextMeshProUGUI stimulusText;

    private void Awake()
    {
        TaskName = "Stroop";
        priority = TaskPriority.Critical;
        timeLimit = 60f;
    }

    public override void Activate()
    {
        base.Activate();
        StationUI?.SetInstruction("STROOP TASK: dock to respond");
        ShowMessage("COMMS CALIBRATION", Color.white);

        // Build the central stimulus text once; we'll mutate it per round.
        GameObject stim = new GameObject("Stimulus", typeof(RectTransform));
        stim.transform.SetParent(buttonsParent, false);
        stimulusText = stim.AddComponent<TMPro.TextMeshProUGUI>();
        stimulusText.alignment = TMPro.TextAlignmentOptions.Center;
        stimulusText.fontSize = 80f;
        stimulusText.fontStyle = TMPro.FontStyles.Bold;
        stimulusText.text = "";
        RectTransform srt = stim.GetComponent<RectTransform>();
        srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
        srt.pivot = new Vector2(0.5f, 0.5f);
        srt.anchoredPosition = new Vector2(0f, 80f);
        srt.sizeDelta = new Vector2(600f, 160f);

        StartCoroutine(CoRunRounds());
    }

    private IEnumerator CoRunRounds()
    {
        // Wait one frame so canvas is laid out.
        yield return null;
        for (round = 0; round < RoundCount && IsActive; round++)
        {
            StartRound(round);
            // Wait for the round to end (answered or 4s timeout).
            float t0 = Time.time;
            while (IsActive && phase == Phase.Trial && !answered && (Time.time - t0) < RoundLimit)
            {
                yield return null;
            }
            if (!answered)
            {
                // Treated as miss -> contributes to fail count (Omission contribution).
                // Nothing additional to record beyond "not correct".
            }
            // Brief pause between rounds.
            yield return new WaitForSeconds(0.4f);
        }

        if (!IsActive) yield break;

        phase = Phase.Done;
        if (correct >= 4) Resolve(TaskResult.Success);
        else Resolve(TaskResult.Fail);
    }

    private void StartRound(int idx)
    {
        phase = Phase.Trial;
        answered = false;
        roundStartTime = Time.time;

        currentWordIdx = Random.Range(0, WordNames.Length);
        // 50% same / 50% conflict.
        if (Random.value < 0.5f) currentInkIdx = currentWordIdx;
        else currentInkIdx = (currentWordIdx + Random.Range(1, WordNames.Length)) % WordNames.Length;

        matchInk = (idx % 2 == 0); // alternate
        ShowMessage(matchInk ? "MATCH THE INK COLOR" : "MATCH THE WORD MEANING", Color.white);

        if (stimulusText != null)
        {
            stimulusText.text = WordNames[currentWordIdx];
            stimulusText.color = Inks[currentInkIdx];
        }

        // Rebuild the 4 answer buttons.
        ClearButtons();
        float btnW = 130f, btnH = 80f, gap = 16f;
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
        if (!IsActive || phase != Phase.Trial || answered) return;
        if (!IsDocked) return;
        answered = true;
        int target = matchInk ? currentInkIdx : currentWordIdx;
        if (answerIdx == target) correct++;
    }
}
