using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Engine-room cognitive task: Sequence Recall.
/// 1. Show 4 colored squares one-at-a-time (Showing).
/// 2. Wait for the player to press READY (Waiting).
/// 3. Player reproduces the sequence in 10s (Recalling).
/// </summary>
public class CodeMemoryTask : CognitiveTaskBase
{
    private enum Phase { Showing, Waiting, Recalling, Done }

    private static readonly Color[] Palette =
    {
        new Color(0.90f, 0.20f, 0.20f), // red
        new Color(0.95f, 0.85f, 0.15f), // yellow
        new Color(0.20f, 0.45f, 0.95f), // blue
        new Color(0.25f, 0.80f, 0.35f), // green
    };
    private static readonly string[] PaletteNames = { "RED", "YELLOW", "BLUE", "GREEN" };

    private const int SequenceLength = 4;

    private Phase phase = Phase.Showing;
    private readonly List<int> sequence = new List<int>(SequenceLength);
    private readonly List<int> input = new List<int>(SequenceLength);

    private GameObject showSquare;
    private float recallStartTime = -1f;
    private float recallTimeUsedWhileUndocked;
    private float recallDeadline = 10f;

    private void Awake()
    {
        TaskName = "Code Memory";
        priority = TaskPriority.NonCritical;
        timeLimit = 30f;
    }

    public override void Activate()
    {
        base.Activate();
        for (int i = 0; i < SequenceLength; i++)
            sequence.Add(Random.Range(0, Palette.Length));
        StationUI?.SetInstruction("CODE MEMORY: dock to begin");
        ShowMessage("MEMORIZE THIS SEQUENCE", Color.white);
        StartCoroutine(CoShowSequence());
    }

    private IEnumerator CoShowSequence()
    {
        // Build a single square placeholder.
        showSquare = new GameObject("ShowSquare", typeof(RectTransform), typeof(Image));
        showSquare.transform.SetParent(buttonsParent, false);
        RectTransform rt = showSquare.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(180f, 180f);
        rt.anchoredPosition = Vector2.zero;
        Image img = showSquare.GetComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 1f);

        // ~2.5s total: 4 flashes -> ~0.5s on, ~0.1s off
        float onTime = 0.5f;
        float offTime = 0.12f;
        for (int i = 0; i < sequence.Count && IsActive; i++)
        {
            img.color = Palette[sequence[i]];
            yield return new WaitForSeconds(onTime);
            img.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            yield return new WaitForSeconds(offTime);
        }
        if (showSquare != null) Destroy(showSquare);

        if (!IsActive) yield break;

        // Phase 2: Waiting for READY click.
        phase = Phase.Waiting;
        ShowMessage("PRESS WHEN READY", new Color(0.9f, 0.95f, 1f));
        StationUI?.SetInstruction("Recall the colored sequence");
        ClearButtons();
        SpawnButton(Vector2.zero, new Vector2(220f, 80f), "READY",
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

        float btnW = 110f, btnH = 110f, gap = 20f;
        float totalW = Palette.Length * btnW + (Palette.Length - 1) * gap;
        float startX = -totalW * 0.5f + btnW * 0.5f;
        for (int i = 0; i < Palette.Length; i++)
        {
            int colorIdx = i; // capture
            Vector2 pos = new Vector2(startX + i * (btnW + gap), 0f);
            SpawnButton(pos, new Vector2(btnW, btnH), PaletteNames[i], Palette[i],
                () => OnColorPressed(colorIdx));
        }
    }

    private void OnColorPressed(int colorIdx)
    {
        if (phase != Phase.Recalling || !IsActive) return;
        if (!IsDocked) return; // freeze input when undocked
        input.Add(colorIdx);

        if (input.Count >= sequence.Count)
        {
            // Compare.
            for (int i = 0; i < sequence.Count; i++)
            {
                if (input[i] != sequence[i])
                {
                    phase = Phase.Done;
                    Resolve(TaskResult.Fail);
                    return;
                }
            }
            phase = Phase.Done;
            Resolve(TaskResult.Success);
        }
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive) return;
        if (phase != Phase.Recalling) return;

        // Recall window only counts down while docked.
        if (!IsDocked) return;
        float elapsed = Time.time - recallStartTime;
        if (elapsed >= recallDeadline)
        {
            phase = Phase.Done;
            Resolve(TaskResult.Omission);
        }
    }

    protected override void OnUndocked()
    {
        // Pause the recall countdown by snapshotting elapsed and bumping start time on re-dock.
        if (phase == Phase.Recalling && recallStartTime > 0f)
        {
            recallTimeUsedWhileUndocked = Time.time - recallStartTime;
        }
    }

    protected override void OnDocked()
    {
        if (phase == Phase.Recalling && recallStartTime > 0f)
        {
            // Resume: shift start time so that "elapsed" stays where it was at undock.
            recallStartTime = Time.time - recallTimeUsedWhileUndocked;
        }
    }
}
