using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Engine-room cognitive task: Sequence Recall (Simon-says style).
/// Flow (only begins after the player docks for the first time):
///   1. "READY?" -> "3" -> "2" -> "1" -> "GO!" countdown.
///   2. Show 5 colored squares one-at-a-time (Showing).
///   3. Player presses READY (Waiting).
///   4. Player reproduces the sequence in 12s (Recalling).
///   5. Splash CORRECT/WRONG, then Resolve.
/// </summary>
public class CodeMemoryTask : CognitiveTaskBase
{
    private enum Phase { Idle, Countdown, Showing, Waiting, Recalling, Done }

    private static readonly Color[] Palette =
    {
        new Color(0.90f, 0.20f, 0.20f), // red
        new Color(0.95f, 0.85f, 0.15f), // yellow
        new Color(0.20f, 0.45f, 0.95f), // blue
        new Color(0.25f, 0.80f, 0.35f), // green
    };
    private static readonly string[] PaletteNames = { "RED", "YELLOW", "BLUE", "GREEN" };

    private const int SequenceLength = 5;
    private const float RecallDeadline = 12f;

    private Phase phase = Phase.Idle;
    private readonly List<int> sequence = new List<int>(SequenceLength);
    private readonly List<int> input = new List<int>(SequenceLength);

    private GameObject showSquare;
    private float recallStartTime = -1f;
    private float recallTimeUsedWhileUndocked;
    private Coroutine flowCo;
    private bool started;

    private void Awake()
    {
        TaskName = "Code Memory";
        priority = TaskPriority.NonCritical;
        timeLimit = 45f;
    }

    public override void Activate()
    {
        base.Activate();
        for (int i = 0; i < SequenceLength; i++)
            sequence.Add(Random.Range(0, Palette.Length));
        StationUI?.SetInstruction("CODE MEMORY: dock to begin");
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));
    }

    protected override void OnDocked()
    {
        // Start the flow on first dock. Subsequent re-docks do nothing here.
        if (!started)
        {
            started = true;
            ShowReadyGate();
        }
        // Resume recall countdown.
        if (phase == Phase.Recalling && recallStartTime > 0f)
        {
            recallStartTime = Time.time - recallTimeUsedWhileUndocked;
        }
    }

    private void ShowReadyGate()
    {
        if (buttonsParent == null) return;
        ShowMessage("PRESS READY", new Color(0.9f, 0.95f, 1f));
        ClearButtons();
        SpawnButton(Vector2.zero, new Vector2(280f, 100f), "READY",
            new Color(0.2f, 0.8f, 0.4f), OnStartReadyClicked);
    }

    private void OnStartReadyClicked()
    {
        if (started == false || flowCo != null) return;
        ClearButtons();
        flowCo = StartCoroutine(CoFullFlow());
    }

    protected override void OnUndocked()
    {
        if (phase == Phase.Recalling && recallStartTime > 0f)
        {
            recallTimeUsedWhileUndocked = Time.time - recallStartTime;
        }
    }

    private IEnumerator CoFullFlow()
    {
        if (buttonsParent == null) yield break;
        // 1) Ready/Go countdown.
        phase = Phase.Countdown;
        ShowMessage("READY?", new Color(0.9f, 0.95f, 1f));
        yield return new WaitForSeconds(0.8f);
        for (int n = 3; n >= 1 && IsActive; n--)
        {
            ShowMessage(n.ToString(), new Color(1f, 0.9f, 0.4f));
            yield return new WaitForSeconds(0.5f);
        }
        if (!IsActive) yield break;
        ShowMessage("GO!", new Color(0.4f, 1f, 0.5f));
        yield return new WaitForSeconds(0.4f);

        // 2) Show sequence.
        phase = Phase.Showing;
        ShowMessage("MEMORIZE THE SEQUENCE", Color.white);
        StationUI?.SetInstruction("Memorize the colored sequence");

        showSquare = new GameObject("ShowSquare", typeof(RectTransform), typeof(Image));
        showSquare.transform.SetParent(buttonsParent, false);
        RectTransform rt = showSquare.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(220f, 220f);
        rt.anchoredPosition = Vector2.zero;
        Image img = showSquare.GetComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        TMPro.TMP_Text caption = SpawnLabel(new Vector2(0f, -160f), new Vector2(400f, 60f),
            "", new Color(0.85f, 0.9f, 1f), 36f);

        float onTime = 0.5f;
        float offTime = 0.12f;
        for (int i = 0; i < sequence.Count && IsActive; i++)
        {
            img.color = Palette[sequence[i]];
            if (caption != null) caption.text = (i + 1) + " / " + sequence.Count;
            yield return new WaitForSeconds(onTime);
            img.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            yield return new WaitForSeconds(offTime);
        }
        if (showSquare != null) Destroy(showSquare);
        if (caption != null && caption.gameObject != null) Destroy(caption.gameObject);
        if (!IsActive) yield break;

        // 3) READY click.
        phase = Phase.Waiting;
        ShowMessage("PRESS WHEN READY", new Color(0.9f, 0.95f, 1f));
        StationUI?.SetInstruction("Recall the colored sequence");
        ClearButtons();
        SpawnButton(Vector2.zero, new Vector2(260f, 90f), "READY",
            new Color(0.2f, 0.8f, 0.4f), OnReadyClicked);
    }

    private void OnReadyClicked()
    {
        if (phase != Phase.Waiting) return;
        phase = Phase.Recalling;
        recallStartTime = Time.time;
        recallTimeUsedWhileUndocked = 0f;
        ShowMessage("REPEAT THE SEQUENCE", Color.white);
        ClearButtons();

        // Larger, more visible buttons.
        float btnW = 150f, btnH = 130f, gap = 24f;
        float totalW = Palette.Length * btnW + (Palette.Length - 1) * gap;
        float startX = -totalW * 0.5f + btnW * 0.5f;
        for (int i = 0; i < Palette.Length; i++)
        {
            int colorIdx = i;
            Vector2 pos = new Vector2(startX + i * (btnW + gap), -10f);
            SpawnButton(pos, new Vector2(btnW, btnH), PaletteNames[i], Palette[i],
                () => OnColorPressed(colorIdx));
        }

    }

    private void OnColorPressed(int colorIdx)
    {
        if (phase != Phase.Recalling || !IsActive) return;
        if (!IsDocked) return;
        input.Add(colorIdx);

        // Update progress label (the most recent label is the count one).
        ShowMessage("REPEAT THE SEQUENCE  (" + input.Count + " / " + sequence.Count + ")", Color.white);

        // Early-fail: wrong press.
        if (input[input.Count - 1] != sequence[input.Count - 1])
        {
            phase = Phase.Done;
            StartCoroutine(CoFinish(TaskResult.Fail));
            return;
        }
        if (input.Count >= sequence.Count)
        {
            phase = Phase.Done;
            StartCoroutine(CoFinish(TaskResult.Success));
        }
    }

    private IEnumerator CoFinish(TaskResult result)
    {
        ClearButtons();
        if (result == TaskResult.Success)
            ShowSplash("CORRECT!", new Color(0.3f, 1f, 0.4f), 1.0f);
        else if (result == TaskResult.Omission)
            ShowSplash("TIMEOUT", new Color(1f, 0.6f, 0.2f), 1.0f);
        else
            ShowSplash("WRONG!", new Color(1f, 0.3f, 0.3f), 1.0f);
        yield return new WaitForSeconds(1.0f);
        Resolve(result);
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive) return;
        if (phase != Phase.Recalling) return;
        if (!IsDocked) return;

        float elapsed = Time.time - recallStartTime;
        if (elapsed >= RecallDeadline)
        {
            phase = Phase.Done;
            StartCoroutine(CoFinish(TaskResult.Omission));
        }
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        base.OnDestroy();
    }
}
