using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Navigation cognitive task: Star Constellation Match.
/// Flow (begins after the player docks):
///   1. "READY?" -> 3-2-1 -> "GO!"
///   2. Show 5-dot reference pattern for 1.8s
///   3. Show 3 candidates (1 identical, 2 with one star moved). User picks
///      within 8s.
///   4. Splash CORRECT!/WRONG!, then Resolve.
/// </summary>
public class PatternMatchTask : CognitiveTaskBase
{
    private enum Phase { Idle, Countdown, Showing, Choosing, Done }

    private const int GridSize = 4;
    private const int DotCount = 5;
    private const float ShowDuration = 1.8f;
    private const float ChooseDuration = 8f;

    private List<Vector2Int> reference;
    private List<List<Vector2Int>> candidates;
    private int correctIndex;

    private Phase phase = Phase.Idle;
    private GameObject referencePanel;
    private float chooseStartTime = -1f;
    private bool started;
    private Coroutine flowCo;

    private void Awake()
    {
        TaskName = "Pattern Match";
        priority = TaskPriority.NonCritical;
        timeLimit = 45f;
    }

    public override void Activate()
    {
        base.Activate();
        BuildPatterns();
        ShowMessage("DOCK TO BEGIN", new Color(0.7f, 0.85f, 1f));
        StationUI?.SetInstruction("PATTERN MATCH: dock to begin");
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
        SpawnButton(Vector2.zero, new Vector2(280f, 100f), "READY",
            new Color(0.2f, 0.8f, 0.4f), OnStartReadyClicked);
    }

    private void OnStartReadyClicked()
    {
        if (flowCo != null) return;
        ClearButtons();
        flowCo = StartCoroutine(CoFlow());
    }

    private void BuildPatterns()
    {
        reference = RandomDistinctPositions(DotCount);

        candidates = new List<List<Vector2Int>>();
        correctIndex = Random.Range(0, 3);

        for (int i = 0; i < 3; i++)
        {
            if (i == correctIndex)
            {
                candidates.Add(new List<Vector2Int>(reference));
            }
            else
            {
                candidates.Add(MakeWrongVariant(reference));
            }
        }
    }

    private static List<Vector2Int> RandomDistinctPositions(int count)
    {
        HashSet<Vector2Int> taken = new HashSet<Vector2Int>();
        List<Vector2Int> result = new List<Vector2Int>(count);
        int safety = 0;
        while (result.Count < count && safety++ < 1000)
        {
            Vector2Int p = new Vector2Int(Random.Range(0, GridSize), Random.Range(0, GridSize));
            if (taken.Add(p)) result.Add(p);
        }
        return result;
    }

    private static List<Vector2Int> MakeWrongVariant(List<Vector2Int> src)
    {
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(src);
        List<Vector2Int> copy = new List<Vector2Int>(src);

        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };
        for (int safety = 0; safety < 64; safety++)
        {
            int idx = Random.Range(0, copy.Count);
            Vector2Int original = copy[idx];
            for (int s = 0; s < 4; s++)
            {
                Vector2Int d = dirs[Random.Range(0, dirs.Length)];
                Vector2Int next = original + d;
                if (next.x < 0 || next.x >= GridSize || next.y < 0 || next.y >= GridSize) continue;
                if (set.Contains(next)) continue;
                set.Remove(original);
                set.Add(next);
                copy[idx] = next;
                return copy;
            }
        }
        copy[0] = new Vector2Int((copy[0].x + 1) % GridSize, copy[0].y);
        return copy;
    }

    private IEnumerator CoFlow()
    {
        if (buttonsParent == null) yield break;
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

        // Phase 1: show reference centered.
        ShowMessage("MEMORIZE THE STAR PATTERN", Color.white);
        referencePanel = BuildPatternPanel(reference, Vector2.zero, 260f);
        phase = Phase.Showing;
        yield return new WaitForSeconds(ShowDuration);
        if (!IsActive) yield break;

        if (referencePanel != null) Destroy(referencePanel);
        referencePanel = null;

        // Phase 2: candidates.
        ShowMessage("WHICH ONE MATCHES?", new Color(0.9f, 0.95f, 1f));
        StationUI?.SetInstruction("Pick the matching pattern");
        ClearButtons();
        phase = Phase.Choosing;
        chooseStartTime = Time.time;

        float panelSize = 220f;
        float gap = 36f;
        float totalW = 3 * panelSize + 2 * gap;
        float startX = -totalW * 0.5f + panelSize * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i;
            Vector2 pos = new Vector2(startX + i * (panelSize + gap), -20f);
            BuildCandidateButton(candidates[i], pos, panelSize, () => OnCandidatePicked(idx));
        }
    }

    private GameObject BuildPatternPanel(List<Vector2Int> dots, Vector2 anchorPos, float size)
    {
        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(buttonsParent, false);
        RectTransform rt = panel.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchorPos;
        rt.sizeDelta = new Vector2(size, size);
        Image img = panel.GetComponent<Image>();
        img.color = new Color(0.05f, 0.10f, 0.30f, 1f);

        float cell = size / GridSize;
        foreach (Vector2Int d in dots)
        {
            GameObject dot = new GameObject("Dot", typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(panel.transform, false);
            RectTransform drt = dot.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = new Vector2(0f, 0f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            float px = (d.x + 0.5f) * cell;
            float py = (d.y + 0.5f) * cell;
            drt.anchoredPosition = new Vector2(px, py);
            drt.sizeDelta = new Vector2(34f, 34f);
            Image dImg = dot.GetComponent<Image>();
            dImg.color = Color.white;
        }
        return panel;
    }

    private void BuildCandidateButton(List<Vector2Int> dots, Vector2 anchorPos, float size, System.Action onClick)
    {
        GameObject panel = BuildPatternPanel(dots, anchorPos, size);
        Button btn = panel.AddComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(() => onClick());
    }

    private void OnCandidatePicked(int idx)
    {
        if (phase != Phase.Choosing || !IsActive) return;
        if (!IsDocked) return;
        phase = Phase.Done;
        ClearButtons();
        if (idx == correctIndex)
            StartCoroutine(CoFinish(TaskResult.Success));
        else
            StartCoroutine(CoFinish(TaskResult.Commission));
    }

    private IEnumerator CoFinish(TaskResult result)
    {
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
        if (phase != Phase.Choosing) return;

        if (Time.time - chooseStartTime >= ChooseDuration)
        {
            phase = Phase.Done;
            ClearButtons();
            StartCoroutine(CoFinish(TaskResult.Omission));
        }
    }

    protected override void OnDestroy()
    {
        if (flowCo != null) { StopCoroutine(flowCo); flowCo = null; }
        base.OnDestroy();
    }
}
