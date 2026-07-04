using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plan/Organize variant — SEQUENCE WITH DEPENDENCIES (the primary one). Each
/// system can only be brought online after ALL of its prerequisites are online.
/// The systems are laid out in a scrambled order and every offline cell looks
/// IDENTICAL — there is deliberately no "ready" glow or highlight, so the player
/// must read each unit's requirements, trace the dependency network (some units
/// need two prerequisites) and plan a valid activation order in their head.
/// Clicking a system whose prerequisites aren't all online is an error.
/// </summary>
public class SequenceDependencyPuzzle : PlanningPuzzlePanel
{
    private static readonly string[] Pool =
        { "REACTOR", "COOLANT", "OXYGEN", "LIGHTS", "COMMS", "GRAVITY", "SHIELDS" };

    private const int SystemCount = 5;
    // Chance a later node picks up a SECOND prerequisite (branching, not a
    // plain chain) — the branch is what forces real planning over left-to-right
    // elimination.
    private const float SecondPrereqChance = 0.45f;

    private int n = SystemCount;
    private string[] names;      // node i -> system name
    private List<int>[] prereqs; // node i -> prerequisite nodes (empty = root)
    private bool[] active;
    private Image[] cellImg;
    private TMP_Text[] reqLbl;

    protected override string Title => "LIFE SUPPORT — STARTUP SEQUENCE";
    protected override string Instructions =>
        "READ EACH UNIT'S REQUIREMENTS — BRING THEM ONLINE IN A VALID ORDER   [ESC] CLOSE";
    protected override string SolvedHeader => "ALL SYSTEMS ONLINE";

    protected override void BuildContent()
    {
        n = SystemCount;
        StepCount = n;

        var pool = new List<string>(Pool);
        Shuffle(pool);
        names = new string[n];
        for (int i = 0; i < n; i++) names[i] = pool[i];

        // Dependency DAG over a random topological order: order[0] is the root;
        // each later node needs the previous one, and may branch onto a second,
        // earlier prerequisite. Always solvable (the topological order itself),
        // but the valid order must be DERIVED, not spotted.
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        Shuffle(order);
        prereqs = new List<int>[n];
        for (int i = 0; i < n; i++) prereqs[i] = new List<int>();
        for (int k = 1; k < n; k++)
        {
            prereqs[order[k]].Add(order[k - 1]);
            if (k >= 2 && Random.value < SecondPrereqChance)
            {
                int extra = order[Random.Range(0, k - 1)];
                if (!prereqs[order[k]].Contains(extra)) prereqs[order[k]].Add(extra);
            }
        }

        active = new bool[n];
        cellImg = new Image[n];
        reqLbl = new TMP_Text[n];

        // Scrambled left-to-right placement so position != dependency order.
        var slots = new List<int>();
        for (int i = 0; i < n; i++) slots.Add(i);
        Shuffle(slots);
        float spacing = (PanelW - 220f) / Mathf.Max(1, n - 1);
        float startX = -(PanelW - 220f) * 0.5f;

        for (int i = 0; i < n; i++)
        {
            float x = startX + slots[i] * spacing;
            int captured = i;
            var btn = MakeButton(panelRT, "Step_" + i, new Vector2(x, -30f),
                new Vector2(170f, 170f), IdleCol, () => OnCell(captured));
            cellImg[i] = ButtonImage(btn);

            MakeLabel((RectTransform)btn.transform, "Name", names[i], 22f, Color.white,
                new Vector2(0f, 40f), new Vector2(160f, 36f)).fontStyle = FontStyles.Bold;
            reqLbl[i] = MakeLabel((RectTransform)btn.transform, "Req", "", 14f, TextDim,
                new Vector2(0f, -34f), new Vector2(158f, 58f));
            reqLbl[i].textWrappingMode = TextWrappingModes.Normal;
        }
        Refresh();
    }

    private bool AllPrereqsOnline(int i)
    {
        for (int k = 0; k < prereqs[i].Count; k++)
            if (!active[prereqs[i][k]]) return false;
        return true;
    }

    private void OnCell(int i)
    {
        if (!CanInteract || active[i]) return;
        RegisterMove(); // every attempted activation counts, valid or not
        if (!AllPrereqsOnline(i))
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

    // Only two visual states: ONLINE (green) and OFFLINE (identical dim cells).
    // No readiness cue — deciding which unit is safe to start IS the puzzle.
    private void Refresh()
    {
        for (int i = 0; i < n; i++)
        {
            cellImg[i].color = active[i] ? OkColor : IdleCol;
            if (reqLbl[i] == null) continue;
            if (active[i])
            {
                reqLbl[i].text = "ONLINE";
                reqLbl[i].color = OkColor;
            }
            else
            {
                reqLbl[i].text = prereqs[i].Count == 0
                    ? "INDEPENDENT SYSTEM"
                    : "NEEDS: " + ReqNames(i);
                reqLbl[i].color = TextDim;
            }
        }
    }

    private string ReqNames(int i)
    {
        var sb = new System.Text.StringBuilder();
        for (int k = 0; k < prereqs[i].Count; k++)
        {
            if (k > 0) sb.Append(" + ");
            sb.Append(names[prereqs[i][k]]);
        }
        return sb.ToString();
    }
}
