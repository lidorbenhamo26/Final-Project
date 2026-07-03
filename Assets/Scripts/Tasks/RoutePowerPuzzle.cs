using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Plan/Organize variant — POWER ROUTING. Every system must be routed to one of
/// two reactors without exceeding that reactor's capacity (a small bin-packing
/// plan). Click a system to cycle its reactor (A → B → unrouted). Overloading a
/// reactor flashes it red and counts as an error. Solved when every system is
/// routed and no reactor is over capacity. A clearer successor to the old wiring
/// match — it's about capacity reasoning, not color matching.
/// </summary>
public class RoutePowerPuzzle : PlanningPuzzlePanel
{
    private static readonly string[] Pool =
        { "OXYGEN", "HEATING", "LIGHTS", "WATER", "COMMS", "GRAVITY" };
    private static readonly string[] SourceNames = { "REACTOR A", "REACTOR B" };

    private const int Sources = 2;
    private int n = 4;
    private int[] load;        // system i -> power load
    private int[] assigned;    // system i -> source index, or -1
    private int[] capacity;    // source s -> capacity
    private TMP_Text[] routeLbl;
    private Image[] srcImg;
    private TMP_Text[] srcLbl;

    protected override string Title => "LIFE SUPPORT — POWER ROUTING";
    protected override string Instructions =>
        "ROUTE EVERY SYSTEM TO A REACTOR WITHOUT OVERLOADING IT   [ESC] CLOSE";
    protected override string SolvedHeader => "POWER ROUTED — SYSTEMS ONLINE";

    protected override void BuildContent()
    {
        n = 4;
        StepCount = n;

        var pool = new List<string>(Pool);
        Shuffle(pool);
        var sysNames = new string[n];
        load = new int[n];
        assigned = new int[n];
        for (int i = 0; i < n; i++) { sysNames[i] = pool[i]; load[i] = Random.Range(1, 4); assigned[i] = -1; }

        // Build a guaranteed-solvable layout: assign each system to a random
        // reactor, then set each reactor's capacity to exactly that sum (tight, so
        // the player must find a valid partition). Reset assignments for play.
        capacity = new int[Sources];
        for (int i = 0; i < n; i++) capacity[Random.Range(0, Sources)] += load[i];
        // Guard: if a reactor ended at 0 capacity, push one unit so both are usable.
        for (int s = 0; s < Sources; s++) if (capacity[s] == 0) capacity[s] = 1;

        srcImg = new Image[Sources];
        srcLbl = new TMP_Text[Sources];
        for (int s = 0; s < Sources; s++)
        {
            float y = 70f - s * 170f;
            var box = MakeImage(panelRT, "Src_" + s, new Color(0.10f, 0.14f, 0.20f, 1f), false);
            var brt = (RectTransform)box.transform;
            brt.anchoredPosition = new Vector2(-320f, y);
            brt.sizeDelta = new Vector2(300f, 150f);
            srcImg[s] = box;
            MakeLabel(brt, "Name", SourceNames[s], 24f, Accent,
                new Vector2(0f, 40f), new Vector2(280f, 36f)).fontStyle = FontStyles.Bold;
            srcLbl[s] = MakeLabel(brt, "Used", "", 22f, Color.white,
                new Vector2(0f, -24f), new Vector2(280f, 36f));
        }

        routeLbl = new TMP_Text[n];
        float topY = 150f;
        for (int i = 0; i < n; i++)
        {
            float y = topY - i * 82f;
            int captured = i;
            var btn = MakeButton(panelRT, "Sys_" + i, new Vector2(220f, y),
                new Vector2(380f, 70f), IdleCol, () => OnSystem(captured));
            MakeLabel((RectTransform)btn.transform, "Name", sysNames[i] + "   (" + load[i] + " kW)",
                22f, Color.white, new Vector2(-70f, 0f), new Vector2(240f, 40f)).fontStyle = FontStyles.Bold;
            routeLbl[i] = MakeLabel((RectTransform)btn.transform, "Route", "— UNROUTED",
                20f, TextDim, new Vector2(120f, 0f), new Vector2(140f, 36f));
        }

        Refresh();
    }

    private void OnSystem(int i)
    {
        if (!CanInteract) return;
        int next = assigned[i] + 1;        // -1 -> 0 -> 1 -> (2 => -1)
        if (next >= Sources) next = -1;
        assigned[i] = next;
        AudioManager.Instance?.PlaySfx("button_click");
        Refresh();

        bool overload = false;
        var used = Used();
        for (int s = 0; s < Sources; s++)
            if (used[s] > capacity[s]) { overload = true; StartCoroutine(FlashColor(srcImg[s], ErrColor)); }
        if (overload) { RegisterError(); return; }

        for (int k = 0; k < n; k++) if (assigned[k] < 0) return; // not all routed
        Complete();
    }

    private int[] Used()
    {
        var used = new int[Sources];
        for (int i = 0; i < n; i++) if (assigned[i] >= 0) used[assigned[i]] += load[i];
        return used;
    }

    private void Refresh()
    {
        var used = Used();
        for (int s = 0; s < Sources; s++)
        {
            if (srcLbl[s] != null) srcLbl[s].text = "LOAD  " + used[s] + " / " + capacity[s];
            if (srcImg[s] != null)
                srcImg[s].color = used[s] > capacity[s] ? new Color(0.30f, 0.12f, 0.13f, 1f)
                    : new Color(0.10f, 0.14f, 0.20f, 1f);
        }
        for (int i = 0; i < n; i++)
        {
            if (routeLbl[i] == null) continue;
            // ">" not "→": the static TMP atlas has no arrow glyph (renders as □).
            routeLbl[i].text = assigned[i] < 0 ? "— UNROUTED" : "> " + SourceNames[assigned[i]];
            routeLbl[i].color = assigned[i] < 0 ? TextDim : OkColor;
        }
    }
}
