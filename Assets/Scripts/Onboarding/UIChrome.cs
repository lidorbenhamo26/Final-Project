using UnityEngine;
using UnityEngine.UI;

public static class UIChrome
{
    private static readonly Color PanelFill = new Color(0.04f, 0.07f, 0.11f, 0.92f);
    private static readonly Color Accent    = new Color(0.37f, 0.78f, 0.88f, 1f);
    private static readonly Color Star      = new Color(1f, 1f, 1f, 1f);

    private const float BracketArm = 24f;
    private const float BracketThickness = 3f;
    private const float BracketMargin = 14f;

    public static RectTransform BuildScifiPanel(Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        var panel = NewRect("Panel", parent, size, anchoredPos);
        var img = panel.gameObject.AddComponent<Image>();
        img.color = PanelFill;
        img.raycastTarget = false;
        AddCornerBrackets(panel, size);
        return panel;
    }

    public static void BuildStarfield(Transform parent, int count = 80, int seed = 12345)
    {
        var container = new GameObject("Starfield");
        container.transform.SetParent(parent, false);
        var crt = container.AddComponent<RectTransform>();
        crt.anchorMin = Vector2.zero; crt.anchorMax = Vector2.one;
        crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;

        var rand = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Star");
            go.transform.SetParent(container.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float sz = 2f + (float)rand.NextDouble() * 1.6f;
            rt.sizeDelta = new Vector2(sz, sz);
            rt.anchoredPosition = new Vector2(
                ((float)rand.NextDouble() - 0.5f) * 1920f,
                ((float)rand.NextDouble() - 0.5f) * 1080f);
            var img = go.AddComponent<Image>();
            float alpha = 0.25f + (float)rand.NextDouble() * 0.6f;
            img.color = new Color(Star.r, Star.g, Star.b, alpha);
            img.raycastTarget = false;
        }
    }

    private static void AddCornerBrackets(RectTransform panel, Vector2 size)
    {
        float hx = size.x / 2f;
        float hy = size.y / 2f;
        float m = BracketMargin;
        float a = BracketArm;
        float t = BracketThickness;

        AddBracketArm(panel, new Vector2(a, t), new Vector2(-hx + m + a / 2f,  hy - m - t / 2f));
        AddBracketArm(panel, new Vector2(t, a), new Vector2(-hx + m + t / 2f,  hy - m - a / 2f));

        AddBracketArm(panel, new Vector2(a, t), new Vector2( hx - m - a / 2f,  hy - m - t / 2f));
        AddBracketArm(panel, new Vector2(t, a), new Vector2( hx - m - t / 2f,  hy - m - a / 2f));

        AddBracketArm(panel, new Vector2(a, t), new Vector2(-hx + m + a / 2f, -hy + m + t / 2f));
        AddBracketArm(panel, new Vector2(t, a), new Vector2(-hx + m + t / 2f, -hy + m + a / 2f));

        AddBracketArm(panel, new Vector2(a, t), new Vector2( hx - m - a / 2f, -hy + m + t / 2f));
        AddBracketArm(panel, new Vector2(t, a), new Vector2( hx - m - t / 2f, -hy + m + a / 2f));
    }

    private static void AddBracketArm(Transform parent, Vector2 size, Vector2 pos)
    {
        var rt = NewRect("Bracket", parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = Accent;
        img.raycastTarget = false;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }
}
