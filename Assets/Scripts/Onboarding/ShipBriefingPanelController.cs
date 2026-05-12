using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ShipBriefingPanelController : MonoBehaviour
{
    private struct Station
    {
        public string Name;
        public string Task;
        public string Body;
        public Color Accent;
    }

    private static readonly Station[] Stations = new Station[]
    {
        new Station {
            Name = "ENGINE",
            Task = "Code Memory",
            Body = "Recall short sequences shown briefly on the console.",
            Accent = new Color(1.00f, 0.55f, 0.20f, 1f),
        },
        new Station {
            Name = "NAVIGATION",
            Task = "Pattern Match",
            Body = "Pick the matching pattern from a row of options.",
            Accent = new Color(0.30f, 0.70f, 0.95f, 1f),
        },
        new Station {
            Name = "COMMS",
            Task = "Stroop",
            Body = "React to the color of the text, not what it says.",
            Accent = new Color(0.45f, 0.90f, 0.55f, 1f),
        },
        new Station {
            Name = "LIFE SUPPORT",
            Task = "N-Back",
            Body = "Match the symbol shown N steps earlier.",
            Accent = new Color(0.95f, 0.45f, 0.40f, 1f),
        },
    };

    private static readonly Color PanelColor = new Color(0.08f, 0.10f, 0.14f, 0.92f);
    private static readonly Color CardColor  = new Color(0.12f, 0.14f, 0.18f, 1f);
    private static readonly Color OkColor    = new Color(0.20f, 0.40f, 0.85f, 1f);
    private static readonly Color BackColor  = new Color(0.30f, 0.34f, 0.40f, 1f);
    private static readonly Color BodyColor  = new Color(0.85f, 0.88f, 0.92f, 1f);
    private static readonly Color TaskColor  = new Color(0.65f, 0.70f, 0.78f, 1f);
    private static readonly Color SubColor   = new Color(0.70f, 0.78f, 0.88f, 1f);

    public Action OnBegin;
    public Action OnBack;

    private RectTransform panel;

    public RectTransform BuildUI(Transform parent)
    {
        panel = NewRect("ShipBriefingPanel", parent, new Vector2(960f, 640f), Vector2.zero);
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = PanelColor;

        SpawnLabel(panel, "SHIP BRIEFING", 36, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 260f), new Vector2(900f, 50f), Color.white);

        SpawnLabel(panel, "Four stations need your attention", 18, FontStyles.Normal,
            TextAlignmentOptions.Center, new Vector2(0f, 205f), new Vector2(900f, 24f), SubColor);

        BuildCard(panel, Stations[0], new Vector2(-160f,  70f));
        BuildCard(panel, Stations[1], new Vector2( 160f,  70f));
        BuildCard(panel, Stations[2], new Vector2(-160f, -130f));
        BuildCard(panel, Stations[3], new Vector2( 160f, -130f));

        BuildButton(panel, "BACK", new Vector2(-360f, -270f), new Vector2(200f, 56f),
            BackColor, () => OnBack?.Invoke());
        BuildButton(panel, "BEGIN MISSION", new Vector2(310f, -270f), new Vector2(280f, 56f),
            OkColor, OnBeginClicked);

        return panel;
    }

    public void Show()
    {
        panel.gameObject.SetActive(true);
        SessionContext.Instance.ShipMapViewed = true;
    }

    public void Hide() => panel.gameObject.SetActive(false);

    private void OnBeginClicked() => OnBegin?.Invoke();

    private void BuildCard(Transform parent, Station station, Vector2 center)
    {
        var card = NewRect("Card_" + station.Name, parent, new Vector2(260f, 180f), center);
        var cardImg = card.gameObject.AddComponent<Image>();
        cardImg.color = CardColor;

        var bar = NewRect("Accent", card, new Vector2(260f, 8f), new Vector2(0f, 86f));
        var barImg = bar.gameObject.AddComponent<Image>();
        barImg.color = station.Accent;

        SpawnLabel(card, station.Name, 22, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 54f), new Vector2(240f, 28f), station.Accent);

        SpawnLabel(card, station.Task, 14, FontStyles.Italic, TextAlignmentOptions.Center,
            new Vector2(0f, 24f), new Vector2(240f, 20f), TaskColor);

        var body = SpawnLabel(card, station.Body, 15, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0f, -28f), new Vector2(232f, 80f), BodyColor);
        body.enableWordWrapping = true;
    }

    private void BuildButton(Transform parent, string text, Vector2 pos, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        var rt = NewRect("Btn_" + text, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(onClick);

        var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero);
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = 24;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
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

    private static TMP_Text SpawnLabel(Transform parent, string text, int size, FontStyles style,
        TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var rt = NewRect("Label", parent, sizeDelta, pos);
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = align;
        lbl.richText = true;
        return lbl;
    }
}
