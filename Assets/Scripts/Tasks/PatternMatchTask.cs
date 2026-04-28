using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Navigation cognitive task: Star Constellation Match.
/// Show a 5-dot pattern on a 4x4 grid for 1.5s. Then show 3 candidates
/// (1 identical, 2 with one dot moved by a single grid cell). User picks
/// within 6s.
/// </summary>
public class PatternMatchTask : CognitiveTaskBase
{
    private enum Phase { Showing, Choosing, Done }

    private const int GridSize = 4;
    private const int DotCount = 5;
    private const float ShowDuration = 1.5f;
    private const float ChooseDuration = 6f;

    private List<Vector2Int> reference;
    private List<List<Vector2Int>> candidates;
    private int correctIndex;

    private Phase phase = Phase.Showing;
    private GameObject referencePanel;
    private float chooseStartTime = -1f;

    private void Awake()
    {
        TaskName = "Pattern Match";
        priority = TaskPriority.NonCritical;
        timeLimit = 30f;
    }

    public override void Activate()
    {
        base.Activate();
        BuildPatterns();
        ShowMessage("MEMORIZE THE STAR PATTERN", Color.white);
        StationUI?.SetInstruction("PATTERN MATCH: dock to begin");
        StartCoroutine(CoFlow());
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
        // Copy and move exactly one dot to a neighbouring (orthogonal) cell that
        // is in-bounds and not already occupied.
        HashSet<Vector2Int> set = new HashSet<Vector2Int>(src);
        List<Vector2Int> copy = new List<Vector2Int>(src);

        // Try a number of times to find a valid move.
        Vector2Int[] dirs =
        {
            new Vector2Int(1, 0), new Vector2Int(-1, 0),
            new Vector2Int(0, 1), new Vector2Int(0, -1),
        };
        for (int safety = 0; safety < 64; safety++)
        {
            int idx = Random.Range(0, copy.Count);
            Vector2Int original = copy[idx];
            // Shuffle directions.
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
        // Fallback: change first dot's column.
        copy[0] = new Vector2Int((copy[0].x + 1) % GridSize, copy[0].y);
        return copy;
    }

    private IEnumerator CoFlow()
    {
        // Phase 1: show reference centered.
        referencePanel = BuildPatternPanel(reference, Vector2.zero, 220f);
        phase = Phase.Showing;
        yield return new WaitForSeconds(ShowDuration);
        if (!IsActive) yield break;

        if (referencePanel != null) Destroy(referencePanel);
        referencePanel = null;

        // Phase 2: show 3 candidates.
        ShowMessage("WHICH ONE MATCHES?", new Color(0.9f, 0.95f, 1f));
        StationUI?.SetInstruction("Pick the matching pattern");
        ClearButtons();
        phase = Phase.Choosing;
        chooseStartTime = Time.time;

        float panelSize = 200f;
        float gap = 30f;
        float totalW = 3 * panelSize + 2 * gap;
        float startX = -totalW * 0.5f + panelSize * 0.5f;
        for (int i = 0; i < 3; i++)
        {
            int idx = i; // capture
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
        img.color = new Color(0.05f, 0.10f, 0.30f, 1f); // dark blue

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
            drt.sizeDelta = new Vector2(30f, 30f);
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
        phase = Phase.Done;
        if (idx == correctIndex) Resolve(TaskResult.Success);
        else Resolve(TaskResult.Commission);
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive) return;
        if (phase != Phase.Choosing) return;

        if (Time.time - chooseStartTime >= ChooseDuration)
        {
            phase = Phase.Done;
            Resolve(TaskResult.Omission);
        }
    }
}
