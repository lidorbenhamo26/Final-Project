using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Top-right "SHIP SYSTEMS" dashboard: one row per station with a status dot
/// (STABLE / ACTIVE / CRITICAL) plus an overall ALERT level. Reads <see cref="ShipState"/>
/// so the player always sees the ship they're operating — the backbone of the
/// progression/purpose layer. Builds its own children under the HUD canvas.
/// </summary>
[DisallowMultipleComponent]
public class StationStatusHUD : MonoBehaviour
{
    private static readonly Color PanelBg = new Color(0.04f, 0.06f, 0.09f, 0.78f);
    private static readonly Color TextPri = new Color(0.95f, 0.97f, 1f, 1f);
    private static readonly Color TextSec = new Color(0.70f, 0.75f, 0.83f, 1f);

    private static readonly (string station, string label)[] Rows =
    {
        ("EngineStation",      "ENGINE"),
        ("NavigationStation",  "NAV"),
        ("CommsStation",       "COMMS"),
        ("LifeSupportStation", "LIFE SUPPORT"),
    };

    private struct StatusRow { public Image Dot; public TMP_Text Label; public string Station; }

    private readonly StatusRow[] rows = new StatusRow[4];
    private TMP_Text alertLabel;
    private TMP_Text titleLabel;

    private void Awake()
    {
        ShipState.GetOrCreate();
        Build();
    }

    private void Update()
    {
        var ship = ShipState.Instance;
        if (ship == null) return;

        var gm = GameManager.Instance;
        if (titleLabel != null && gm != null)
            titleLabel.text = "SECTOR " + gm.CurrentSector + " / " + GameManager.TotalSectors;

        if (alertLabel != null)
        {
            alertLabel.text = "ALERT: " + ship.AlertLevel;
            alertLabel.color = ship.AlertColor;
        }
        for (int i = 0; i < rows.Length; i++)
        {
            var st = ship.Get(rows[i].Station);
            if (rows[i].Dot != null) rows[i].Dot.color = ShipState.StateColor(st);
            if (rows[i].Label != null)
                rows[i].Label.text = Rows[i].label + "   <color=#" +
                    ColorUtility.ToHtmlStringRGB(ShipState.StateColor(st)) + ">" +
                    ShipState.StateWord(st) + "</color>";
        }
    }

    private void Build()
    {
        var rt = (RectTransform)transform;
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-12f, -64f); // below the assessor control bar
        rt.sizeDelta = new Vector2(248f, 176f);

        var bg = gameObject.AddComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = false;

        titleLabel = MakeLabel("Title", "SECTOR 1 / 3", 15, FontStyles.Bold, TextSec,
            new Vector2(12f, -8f), new Vector2(224f, 20f), TextAlignmentOptions.Left);
        alertLabel = MakeLabel("Alert", "ALERT: NOMINAL", 15, FontStyles.Bold, TextPri,
            new Vector2(-12f, -8f), new Vector2(160f, 20f), TextAlignmentOptions.Right);
        ((RectTransform)alertLabel.transform).anchorMin = new Vector2(1f, 1f);
        ((RectTransform)alertLabel.transform).anchorMax = new Vector2(1f, 1f);
        ((RectTransform)alertLabel.transform).pivot = new Vector2(1f, 1f);
        ((RectTransform)alertLabel.transform).anchoredPosition = new Vector2(-12f, -8f);

        float y = -36f;
        for (int i = 0; i < Rows.Length; i++)
        {
            var dot = new GameObject("Dot" + i, typeof(RectTransform), typeof(Image));
            dot.transform.SetParent(transform, false);
            var drt = (RectTransform)dot.transform;
            drt.anchorMin = new Vector2(0f, 1f); drt.anchorMax = new Vector2(0f, 1f);
            drt.pivot = new Vector2(0f, 1f);
            drt.anchoredPosition = new Vector2(14f, y - 4f);
            drt.sizeDelta = new Vector2(12f, 12f);
            var dimg = dot.GetComponent<Image>();
            dimg.color = ShipState.StateColor(ShipSystemState.Stable);
            dimg.raycastTarget = false;

            var lbl = MakeLabel("Row" + i, Rows[i].label, 15, FontStyles.Bold, TextPri,
                new Vector2(36f, y), new Vector2(204f, 22f), TextAlignmentOptions.Left);
            rows[i] = new StatusRow { Dot = dimg, Label = lbl, Station = Rows[i].station };
            y -= 30f;
        }
    }

    private TMP_Text MakeLabel(string name, string text, int size, FontStyles style, Color color,
        Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = pos; rt.sizeDelta = sizeDelta;
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text; lbl.fontSize = size; lbl.fontStyle = style; lbl.color = color;
        lbl.alignment = align; lbl.raycastTarget = false; lbl.richText = true;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        return lbl;
    }
}
