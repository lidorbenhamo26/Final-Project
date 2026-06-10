using System.Collections;
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
    private const float RoundLimit = 4f;

    private Phase phase = Phase.Idle;
    private int round;
    private int correct;
    private int currentWordIdx;
    private int currentInkIdx;
    private bool matchInk;
    private bool answered;
    private float roundStartTime;

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
        BuildStimulus();
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
            StartRound(round);
            float start = Time.time;
            while (IsActive && phase == Phase.Trial && !answered && Time.time - start < RoundLimit)
                yield return null;

            // Unanswered round while docked = an attention/inhibition lapse the
            // player witnessed; undocked rounds are captured as task omission.
            yield return new WaitForSeconds(0.4f);
        }

        if (!IsActive) yield break;
        phase = Phase.Done;
        Resolve(correct >= 4 ? TaskResult.Success : TaskResult.Fail);
    }

    private void StartRound(int idx)
    {
        phase = Phase.Trial;
        answered = false;
        roundStartTime = Time.time;

        currentWordIdx = Random.Range(0, WordNames.Length);
        currentInkIdx = Random.value < 0.5f
            ? currentWordIdx
            : (currentWordIdx + Random.Range(1, WordNames.Length)) % WordNames.Length;

        matchInk = idx % 2 == 0;
        ShowMessage(matchInk ? "MATCH THE INK COLOR" : "MATCH THE WORD MEANING", Color.white);

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
    }
}
