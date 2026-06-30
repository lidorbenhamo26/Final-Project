using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plan/Organize variant — SEQUENCE WITH DEPENDENCIES (the primary one). Each
/// system can only be brought online after its prerequisite is online. The
/// systems are laid out in a scrambled order, each showing "NEEDS: X", so the
/// player must read the dependencies and plan a valid activation order. Clicking
/// a system whose prerequisite isn't online yet is an error (flash + buzz).
/// </summary>
public class SequenceDependencyPuzzle : PlanningPuzzlePanel
{
    private static readonly string[] Pool =
        { "REACTOR", "COOLANT", "OXYGEN", "LIGHTS", "COMMS", "GRAVITY", "SHIELDS" };

    private int n = 4;
    private string[] names;   // node i -> system name
    private int[] prereq;     // node i -> prerequisite node, or -1
    private bool[] active;
    private Image[] cellImg;

    protected override string Title => "LIFE SUPPORT — STARTUP SEQUENCE";
    protected override string Instructions =>
        "BRING EACH SYSTEM ONLINE ONLY AFTER ITS PREREQUISITE   [ESC] CLOSE";
    protected override string SolvedHeader => "ALL SYSTEMS ONLINE";

    protected override void BuildContent()
    {
        n = 4;
        StepCount = n;

        var pool = new List<string>(Pool);
        Shuffle(pool);
        names = new string[n];
        for (int i = 0; i < n; i++) names[i] = pool[i];

        // Dependency chain over a random order: order[0] is the root; each later
        // node requires the previous one. A clear, valid sequence to discover.
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Shuffle(order);
        prereq = new int[n];
        for (int i = 0; i < n; i++) prereq[i] = -1;
        for (int k = 1; k < n; k++) prereq[order[k]] = order[k - 1];

        active = new bool[n];
        cellImg = new Image[n];

        // Scrambled left-to-right placement so position != dependency order.
        var slots = new List<int>();
        for (int i = 0; i < n; i++) slots.Add(i);
        Shuffle(slots);
        float spacing = (PanelW - 260f) / Mathf.Max(1, n - 1);
        float startX = -(PanelW - 260f) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float x = startX + slots[i] * spacing;
            int captured = i;
            var btn = MakeButton(panelRT, "Step_" + i, new Vector2(x, -30f),
                new Vector2(180f, 160f), IdleCol, () => OnCell(captured));
            cellImg[i] = ButtonImage(btn);

            MakeLabel((RectTransform)btn.transform, "Name", names[i], 24f, Color.white,
                new Vector2(0f, 30f), new Vector2(170f, 40f)).fontStyle = FontStyles.Bold;
            string reqText = prereq[i] < 0 ? "READY TO START" : "NEEDS: " + names[prereq[i]];
            MakeLabel((RectTransform)btn.transform, "Req", reqText, 16f, TextDim,
                new Vector2(0f, -36f), new Vector2(170f, 30f));
        }
        Refresh();
    }

    private void OnCell(int i)
    {
        if (!CanInteract || active[i]) return;
        bool ready = prereq[i] < 0 || active[prereq[i]];
        if (!ready)
        {
            RegisterError();
            StartCoroutine(FlashColor(cellImg[i], ErrColor));
            return;
        }
        active[i] = true;
        AudioManager.Instance?.PlaySfx("wire_connect");
        Refresh();

        for (int k = 0; k < n; k++) if (!active[k]) return;
        Complete();
    }

    private void Refresh()
    {
        for (int i = 0; i < n; i++)
            cellImg[i].color = active[i] ? OkColor : IdleCol;
    }
}
