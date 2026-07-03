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
        public Vector2 WorldXZ;
    }

    private static readonly Station[] Stations = new Station[]
    {
        new Station {
            Name = "ENGINE", Task = "Working Memory",
            Body = "Memorize the code shown on the HUD, then enter it at the engine console.",
            Accent = new Color(1.00f, 0.55f, 0.20f, 1f),
            WorldXZ = new Vector2(0f, 16f),
        },
        new Station {
            Name = "NAVIGATION", Task = "Radar Scan",
            Body = "Flag asteroid contacts and ignore harmless returns.",
            Accent = new Color(0.30f, 0.70f, 0.95f, 1f),
            WorldXZ = new Vector2(16f, 0f),
        },
        new Station {
            Name = "COMMS", Task = "Stroop",
            Body = "React to the color of the text, not what it says.",
            Accent = new Color(0.45f, 0.90f, 0.55f, 1f),
            WorldXZ = new Vector2(-16f, 0f),
        },
        new Station {
            Name = "LIFE SUPPORT", Task = "Battery Delivery",
            Body = "Carry a power cell from storage and restore life support.",
            Accent = new Color(0.95f, 0.45f, 0.40f, 1f),
            WorldXZ = new Vector2(0f, -16f),
        },
    };

    private const float PixelsPerMeter = 10f;

    private static readonly Color HullColor   = new Color(0.37f, 0.78f, 0.88f, 0.45f);
    private static readonly Color HullDim     = new Color(0.37f, 0.78f, 0.88f, 0.25f);
    private static readonly Color CoreColor   = new Color(0.92f, 0.94f, 0.97f, 0.85f);
    private static readonly Color CardBg      = new Color(0.06f, 0.09f, 0.14f, 0.85f);
    private static readonly Color OkColor     = new Color(0.20f, 0.40f, 0.85f, 1f);
    private static readonly Color BackColor   = new Color(0.30f, 0.34f, 0.40f, 1f);
    private static readonly Color BodyColor   = new Color(0.85f, 0.88f, 0.92f, 1f);
    private static readonly Color TaskColor   = new Color(0.65f, 0.70f, 0.78f, 1f);
    private static readonly Color SubColor    = new Color(0.70f, 0.78f, 0.88f, 1f);
    private static readonly Color CardinalLbl = new Color(0.55f, 0.62f, 0.72f, 0.7f);

    public Action OnBegin;
    public Action OnBack;

    private RectTransform panel;
    private Image[] markerHaloes;
    private int selectedIndex;

    private TMP_Text infoNameLbl;
    private TMP_Text infoTaskLbl;
    private TMP_Text infoBodyLbl;
    private TMP_Text infoCoordLbl;
    private Image infoAccentBar;

    public RectTransform BuildUI(Transform parent)
    {
        panel = UIChrome.BuildScifiPanel(parent, new Vector2(1100f, 700f), Vector2.zero);
        panel.gameObject.name = "ShipBriefingPanel";

        SpawnLabel(panel, "SHIP BRIEFING", 36, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 290f), new Vector2(1040f, 50f), Color.white);
        SpawnLabel(panel, "Top-down overview  ·  click a station to inspect", 16,
            FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0f, 245f), new Vector2(1040f, 22f), SubColor);

        BuildMap(panel, new Vector2(-260f, 30f), new Vector2(460f, 400f));
        BuildInfoCard(panel, new Vector2(250f, 30f), new Vector2(380f, 400f));

        // The Priority Protocol, stated before the mission ever asks for it
        // (shares the legend with the in-mission beat/interruption panels).
        SpawnLabel(panel, "PRIORITY PROTOCOL   —   " + PriorityBeatPanel.LEGEND, 16,
            FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, -215f), new Vector2(1040f, 24f), SubColor);

        BuildButton(panel, "BACK", new Vector2(-420f, -290f), new Vector2(200f, 56f),
            BackColor, () => OnBack?.Invoke());
        BuildButton(panel, "BEGIN MISSION", new Vector2(380f, -290f), new Vector2(280f, 56f),
            OkColor, () => OnBegin?.Invoke());

        SetSelected(0);
        return panel;
    }

    public void Show()
    {
        panel.gameObject.SetActive(true);
        SessionContext.Instance.ShipMapViewed = true;
    }

    public void Hide() => panel.gameObject.SetActive(false);

    private void BuildMap(Transform parent, Vector2 center, Vector2 size)
    {
        var map = NewRect("Map", parent, size, center);

        AddRect(map, "MapBg", size, Vector2.zero, new Color(0.03f, 0.06f, 0.10f, 0.7f));

        AddRect(map, "HullV", new Vector2(10f, 360f), Vector2.zero, HullColor);
        AddRect(map, "HullH", new Vector2(360f, 10f), Vector2.zero, HullColor);

        float frameSize = 380f;
        float frameThick = 2f;
        AddRect(map, "FrameTop",    new Vector2(frameSize, frameThick), new Vector2(0f,  frameSize / 2f), HullDim);
        AddRect(map, "FrameBottom", new Vector2(frameSize, frameThick), new Vector2(0f, -frameSize / 2f), HullDim);
        AddRect(map, "FrameLeft",   new Vector2(frameThick, frameSize), new Vector2(-frameSize / 2f, 0f), HullDim);
        AddRect(map, "FrameRight",  new Vector2(frameThick, frameSize), new Vector2( frameSize / 2f, 0f), HullDim);

        AddRect(map, "Core", new Vector2(10f, 10f), Vector2.zero, CoreColor);

        SpawnLabel(map, "N", 14, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f,  size.y / 2f - 12f), new Vector2(20f, 18f), CardinalLbl);
        SpawnLabel(map, "S", 14, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, -size.y / 2f + 12f), new Vector2(20f, 18f), CardinalLbl);
        SpawnLabel(map, "E", 14, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2( size.x / 2f - 12f, 0f), new Vector2(18f, 18f), CardinalLbl);
        SpawnLabel(map, "W", 14, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(-size.x / 2f + 12f, 0f), new Vector2(18f, 18f), CardinalLbl);

        markerHaloes = new Image[Stations.Length];
        for (int i = 0; i < Stations.Length; i++)
        {
            var s = Stations[i];
            Vector2 markerPos = new Vector2(s.WorldXZ.x, s.WorldXZ.y) * PixelsPerMeter;
            BuildMarker(map, i, s, markerPos);
        }
    }

    private void BuildMarker(Transform mapParent, int index, Station station, Vector2 pos)
    {
        var rt = NewRect("Marker_" + station.Name, mapParent, new Vector2(48f, 48f), pos);
        var halo = rt.gameObject.AddComponent<Image>();
        halo.color = new Color(station.Accent.r, station.Accent.g, station.Accent.b, 0.30f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = halo;
        int capturedIndex = index;
        btn.onClick.AddListener(() => SetSelected(capturedIndex));
        markerHaloes[index] = halo;

        AddRect(rt, "Core", new Vector2(20f, 20f), Vector2.zero, station.Accent);

        Vector2 labelOffset = pos.y >= 0f ? new Vector2(0f, -36f) : new Vector2(0f, 36f);
        SpawnLabel(rt, station.Name, 12, FontStyles.Bold, TextAlignmentOptions.Center,
            labelOffset, new Vector2(140f, 16f), station.Accent);
    }

    private void BuildInfoCard(Transform parent, Vector2 center, Vector2 size)
    {
        var card = NewRect("InfoCard", parent, size, center);
        var bg = card.gameObject.AddComponent<Image>();
        bg.color = CardBg;
        bg.raycastTarget = false;

        infoAccentBar = AddRect(card, "AccentBar", new Vector2(size.x, 6f),
            new Vector2(0f, size.y / 2f - 3f), Stations[0].Accent);

        infoNameLbl = SpawnLabel(card, Stations[0].Name, 30, FontStyles.Bold,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, size.y / 2f - 50f),
            new Vector2(size.x - 40f, 36f), Stations[0].Accent);

        infoTaskLbl = SpawnLabel(card, Stations[0].Task, 16, FontStyles.Italic,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, size.y / 2f - 86f),
            new Vector2(size.x - 40f, 22f), TaskColor);

        AddRect(card, "Sep", new Vector2(size.x - 40f, 1f),
            new Vector2(0f, size.y / 2f - 110f), HullDim);

        infoBodyLbl = SpawnLabel(card, Stations[0].Body, 16, FontStyles.Normal,
            TextAlignmentOptions.TopLeft, new Vector2(0f, size.y / 2f - 200f),
            new Vector2(size.x - 40f, 160f), BodyColor);
        infoBodyLbl.enableWordWrapping = true;

        infoCoordLbl = SpawnLabel(card, "", 13, FontStyles.Normal,
            TextAlignmentOptions.MidlineLeft, new Vector2(0f, -size.y / 2f + 30f),
            new Vector2(size.x - 40f, 18f), TaskColor);
    }

    private void SetSelected(int index)
    {
        if (index < 0 || index >= Stations.Length) return;
        selectedIndex = index;

        for (int i = 0; i < markerHaloes.Length; i++)
        {
            if (markerHaloes[i] == null) continue;
            var c = Stations[i].Accent;
            float alpha = i == index ? 0.85f : 0.30f;
            markerHaloes[i].color = new Color(c.r, c.g, c.b, alpha);
        }

        var s = Stations[index];
        infoAccentBar.color = s.Accent;
        infoNameLbl.text = s.Name;
        infoNameLbl.color = s.Accent;
        infoTaskLbl.text = s.Task;
        infoBodyLbl.text = s.Body;
        infoCoordLbl.text = "Position  X " + s.WorldXZ.x.ToString("+0;-0;0")
                         + "   Z " + s.WorldXZ.y.ToString("+0;-0;0") + "   (meters)";
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

    private static Image AddRect(Transform parent, string name, Vector2 size, Vector2 pos, Color color)
    {
        var rt = NewRect(name, parent, size, pos);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
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
        lbl.raycastTarget = false;
        return lbl;
    }
}
