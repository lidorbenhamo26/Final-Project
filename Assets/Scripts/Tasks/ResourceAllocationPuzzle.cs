using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plan/Organize variant — POWER ALLOCATION with IMPLICIT constraints. The needs
/// are never shown as numbers: the player gets two relational rules (e.g.
/// "OXYGEN must get exactly TWICE the power of GRAVITY", "HEATING must get 1
/// MORE than OXYGEN") plus a zero-slack budget, and must derive the unique
/// allocation in their head before committing (Working Memory + Planning).
/// Adjust with +/-, then CONFIRM. A confirm that violates the rules is an error
/// (the offending rows flash). The chained rules rise strictly with the anchor
/// value, so the shown budget always pins exactly one solution.
/// </summary>
public class ResourceAllocationPuzzle : PlanningPuzzlePanel
{
    private static readonly string[] Pool =
        { "OXYGEN", "HEATING", "LIGHTING", "WATER", "GRAVITY" };

    private const int MaxBudget = 12;
    private const int MaxTarget = 6;

    private int n = 3;
    private int budget;
    private int[] target;      // hidden unique solution, derived from the rules
    private int[] alloc;
    private string[] sysNames;
    private string[] ruleText; // the two relational rules shown to the player
    private Image[] rowImg;
    private TMP_Text[] allocLbl;
    private TMP_Text remainLbl;
    private Outline confirmGlow; // lit once the whole budget is assigned

    protected override string Title => "LIFE SUPPORT — POWER ALLOCATION";
    protected override string Instructions =>
        "WORK OUT EACH SYSTEM'S POWER FROM THE RULES — USE THE WHOLE BUDGET   [ESC] CLOSE";
    protected override string SolvedHeader => "POWER BALANCED — SYSTEMS ONLINE";

    protected override void BuildContent()
    {
        n = 3;
        StepCount = n;

        var pool = new List<string>(Pool);
        Shuffle(pool);
        sysNames = new string[n];
        for (int i = 0; i < n; i++) sysNames[i] = pool[i];

        GenerateRules();
        alloc = new int[n];

        rowImg = new Image[n];
        allocLbl = new TMP_Text[n];

        // The rules ARE the puzzle — give them the top of the panel.
        var rules1 = MakeLabel(panelRT, "Rule1", "•  " + ruleText[0], 20f, Accent,
            new Vector2(0f, 176f), new Vector2(PanelW - 120f, 28f));
        rules1.fontStyle = FontStyles.Bold;
        var rules2 = MakeLabel(panelRT, "Rule2", "•  " + ruleText[1], 20f, Accent,
            new Vector2(0f, 146f), new Vector2(PanelW - 120f, 28f));
        rules2.fontStyle = FontStyles.Bold;

        float topY = 64f;
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
                new Vector2(-300f, 0f), new Vector2(300f, 40f)).fontStyle = FontStyles.Bold;

            // Outlined +/- pads so the clickable controls read instantly against
            // the (non-interactive) row background.
            var minus = MakeButton(rrt, "Minus_" + i, new Vector2(60f, 0f), new Vector2(54f, 54f),
                new Color(0.30f, 0.20f, 0.22f, 1f), () => Adjust(captured, -1));
            AddGlow(minus.gameObject);
            MakeLabel(rrt, "MinusGlyph", "-", 34f, Color.white, new Vector2(60f, 0f), new Vector2(54f, 54f));

            allocLbl[i] = MakeLabel(rrt, "Alloc", "0", 30f, Accent,
                new Vector2(150f, 0f), new Vector2(80f, 50f));
            allocLbl[i].fontStyle = FontStyles.Bold;

            var plus = MakeButton(rrt, "Plus_" + i, new Vector2(240f, 0f), new Vector2(54f, 54f),
                new Color(0.20f, 0.30f, 0.24f, 1f), () => Adjust(captured, +1));
            AddGlow(plus.gameObject);
            MakeLabel(rrt, "PlusGlyph", "+", 34f, Color.white, new Vector2(240f, 0f), new Vector2(54f, 54f));
        }

        remainLbl = MakeLabel(panelRT, "Remain", "", 24f, Accent,
            new Vector2(0f, -164f), new Vector2(PanelW - 120f, 36f));
        remainLbl.fontStyle = FontStyles.Bold;

        var confirm = MakeButton(panelRT, "Confirm", new Vector2(0f, -224f), new Vector2(280f, 60f),
            new Color(0.18f, 0.45f, 0.62f, 1f), OnConfirm);
        confirmGlow = AddGlow(confirm.gameObject);
        confirmGlow.enabled = false;
        MakeLabel(panelRT, "ConfirmLbl", "CONFIRM", 24f, Color.white,
            new Vector2(0f, -224f), new Vector2(280f, 40f)).fontStyle = FontStyles.Bold;

        UpdateLabels();
    }

    // Chained relational rules: sys0 derives from an anchor (sys2), sys1 derives
    // from sys0. Every target rises strictly with the anchor value, so the total
    // (the shown budget) pins a unique solution the player must reason out.
    private void GenerateRules()
    {
        target = new int[n];
        ruleText = new string[2];
        for (int attempt = 0; attempt < 64; attempt++)
        {
            int anchor = Random.Range(1, 4);          // sys2's hidden value: 1..3
            int op0 = Random.Range(0, 3);             // sys0 from sys2
            int op1 = Random.Range(0, 3);             // sys1 from sys0
            target[2] = anchor;
            target[0] = Apply(op0, target[2]);
            target[1] = Apply(op1, target[0]);
            budget = target[0] + target[1] + target[2];
            if (budget <= MaxBudget
                && target[0] <= MaxTarget && target[1] <= MaxTarget && target[2] <= MaxTarget)
            {
                ruleText[0] = RuleLine(sysNames[0], op0, sysNames[2]);
                ruleText[1] = RuleLine(sysNames[1], op1, sysNames[0]);
                return;
            }
        }
        // Fallback (never expected): smallest instance of the same shape.
        target[2] = 1; target[0] = 2; target[1] = 3;
        budget = 6;
        ruleText[0] = RuleLine(sysNames[0], 0, sysNames[2]);
        ruleText[1] = RuleLine(sysNames[1], 1, sysNames[0]);
    }

    // op 0 = exactly twice, 1 = one more, 2 = two more. All strictly increasing
    // in the source value, which is what guarantees a unique solution.
    private static int Apply(int op, int src) => op == 0 ? src * 2 : op == 1 ? src + 1 : src + 2;

    private static string RuleLine(string sys, int op, string src)
    {
        switch (op)
        {
            case 0:  return sys + " must get exactly TWICE the power of " + src;
            case 1:  return sys + " must get 1 MORE than " + src;
            default: return sys + " must get 2 MORE than " + src;
        }
    }

    private Outline AddGlow(GameObject go)
    {
        var glow = go.AddComponent<Outline>();
        glow.effectColor = Accent;
        glow.effectDistance = new Vector2(2f, 2f);
        return glow;
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
        RegisterMove();
        if (delta < 0) RegisterBacktrack(); // removing assigned power = undoing a placement
        alloc[i] = Mathf.Clamp(alloc[i] + delta, 0, budget);
        AudioManager.Instance?.PlaySfx("button_click");
        UpdateLabels();
    }

    private void OnConfirm()
    {
        if (!CanInteract) return;
        RegisterMove(); // an attempted commit counts as a placement attempt
        bool ok = true;
        for (int i = 0; i < n; i++)
        {
            if (alloc[i] != target[i]) { ok = false; StartCoroutine(FlashColor(rowImg[i], ErrColor)); }
        }
        if (ok) Complete();
        else RegisterError();
    }

    private void UpdateLabels()
    {
        // No per-row correctness feedback before CONFIRM — the solution is hidden
        // behind the rules, and coloring a matched row would leak it.
        for (int i = 0; i < n; i++) if (allocLbl[i] != null) allocLbl[i].text = alloc[i].ToString();
        int remain = budget - Used();
        if (remainLbl != null)
        {
            remainLbl.text = remain == 0
                ? "ALL POWER ASSIGNED — PRESS CONFIRM"
                : "POWER REMAINING:  " + remain + " / " + budget;
            remainLbl.color = remain == 0 ? OkColor : Accent;
        }
        if (confirmGlow != null) confirmGlow.enabled = remain == 0;
    }
}
