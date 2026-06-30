using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plan/Organize variant — POWER ALLOCATION. A fixed power budget must be
/// distributed so each system gets exactly the amount it needs. The budget equals
/// the sum of the needs, so there is no slack: over-feeding an early system
/// strands a later one. Adjust with +/-, then CONFIRM. A confirm that doesn't
/// match every need is an error (the mismatched rows flash).
/// </summary>
public class ResourceAllocationPuzzle : PlanningPuzzlePanel
{
    private static readonly string[] Pool =
        { "OXYGEN", "HEATING", "LIGHTING", "WATER", "GRAVITY" };

    private int n = 3;
    private int budget;
    private int[] need;
    private int[] alloc;
    private Image[] rowImg;
    private TMP_Text[] allocLbl;
    private TMP_Text remainLbl;

    protected override string Title => "LIFE SUPPORT — POWER ALLOCATION";
    protected override string Instructions =>
        "GIVE EACH SYSTEM EXACTLY THE POWER IT NEEDS — NO SLACK   [ESC] CLOSE";
    protected override string SolvedHeader => "POWER BALANCED — SYSTEMS ONLINE";

    protected override void BuildContent()
    {
        n = 3;
        StepCount = n;

        var pool = new List<string>(Pool);
        Shuffle(pool);
        var sysNames = new string[n];
        need = new int[n];
        alloc = new int[n];
        budget = 0;
        for (int i = 0; i < n; i++) { sysNames[i] = pool[i]; need[i] = Random.Range(2, 5); budget += need[i]; }

        rowImg = new Image[n];
        allocLbl = new TMP_Text[n];

        float topY = 90f;
        for (int i = 0; i < n; i++)
        {
            float y = topY - i * 86f;
            int captured = i;

            var row = MakeImage(panelRT, "Row_" + i, new Color(0.09f, 0.12f, 0.18f, 1f), false);
            var rrt = (RectTransform)row.transform;
            rrt.anchoredPosition = new Vector2(0f, y);
            rrt.sizeDelta = new Vector2(PanelW - 160f, 72f);
            rowImg[i] = row;

            MakeLabel(rrt, "Name", sysNames[i], 24f, Color.white,
                new Vector2(-340f, 0f), new Vector2(220f, 40f)).fontStyle = FontStyles.Bold;
            MakeLabel(rrt, "Need", "NEEDS " + need[i], 20f, TextDim,
                new Vector2(-110f, 0f), new Vector2(160f, 36f));

            MakeButton(rrt, "Minus_" + i, new Vector2(60f, 0f), new Vector2(54f, 54f),
                new Color(0.30f, 0.20f, 0.22f, 1f), () => Adjust(captured, -1));
            MakeLabel(rrt, "MinusGlyph", "-", 34f, Color.white, new Vector2(60f, 0f), new Vector2(54f, 54f));

            allocLbl[i] = MakeLabel(rrt, "Alloc", "0", 30f, Accent,
                new Vector2(150f, 0f), new Vector2(80f, 50f));
            allocLbl[i].fontStyle = FontStyles.Bold;

            MakeButton(rrt, "Plus_" + i, new Vector2(240f, 0f), new Vector2(54f, 54f),
                new Color(0.20f, 0.30f, 0.24f, 1f), () => Adjust(captured, +1));
            MakeLabel(rrt, "PlusGlyph", "+", 34f, Color.white, new Vector2(240f, 0f), new Vector2(54f, 54f));
        }

        remainLbl = MakeLabel(panelRT, "Remain", "", 24f, Accent,
            new Vector2(0f, -150f), new Vector2(PanelW - 120f, 36f));
        remainLbl.fontStyle = FontStyles.Bold;

        MakeButton(panelRT, "Confirm", new Vector2(0f, -220f), new Vector2(280f, 60f),
            new Color(0.18f, 0.45f, 0.62f, 1f), OnConfirm);
        MakeLabel(panelRT, "ConfirmLbl", "CONFIRM", 24f, Color.white,
            new Vector2(0f, -220f), new Vector2(280f, 40f)).fontStyle = FontStyles.Bold;

        UpdateLabels();
    }

    private int Used()
    {
        int u = 0;
        for (int i = 0; i < n; i++) u += alloc[i];
        return u;
    }

    private void Adjust(int i, int delta)
    {
        if (!CanInteract) return;
        if (delta > 0 && Used() >= budget) return;          // can't exceed the budget
        if (delta < 0 && alloc[i] == 0) return;
        alloc[i] = Mathf.Clamp(alloc[i] + delta, 0, budget);
        AudioManager.Instance?.PlaySfx("button_click");
        UpdateLabels();
    }

    private void OnConfirm()
    {
        if (!CanInteract) return;
        bool ok = true;
        for (int i = 0; i < n; i++)
        {
            if (alloc[i] != need[i]) { ok = false; StartCoroutine(FlashColor(rowImg[i], ErrColor)); }
        }
        if (ok) Complete();
        else RegisterError();
    }

    private void UpdateLabels()
    {
        for (int i = 0; i < n; i++) if (allocLbl[i] != null) allocLbl[i].text = alloc[i].ToString();
        int remain = budget - Used();
        if (remainLbl != null)
            remainLbl.text = "POWER REMAINING:  " + remain + " / " + budget;
    }
}
