using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The signature "Priority Beat" overlay. During an EF event it presents the 2-3
/// incoming tasks as numbered cards and asks the single question that defines the
/// executive-function decision: <b>which task is the highest priority right now?</b>
/// The player picks ONE (click or number key); that becomes their next objective.
/// Each card states WHY it has its tier (the learnable Priority Protocol), so the
/// choice is informed, not a color guess.
///
/// Also owns the one-time INTRO screen shown before the player's first beat,
/// explaining the mechanic with an "I understand" gate.
///
/// Presentation only — the pick capture + scoring live in <see cref="EFEventDirector"/>.
/// Card clicks feed <see cref="TaskListHUD"/>'s EF click queue (the same channel the
/// director consumes), so clicks and number keys stay unified.
/// </summary>
[DisallowMultipleComponent]
public class PriorityBeatPanel : MonoBehaviour
{
    public static PriorityBeatPanel Instance { get; private set; }

    private static readonly Color TierCritical  = new Color(0.937f, 0.267f, 0.267f, 1f);
    private static readonly Color TierImportant = new Color(0.98f,  0.80f,  0.10f,  1f);
    private static readonly Color TierRoutine   = new Color(0.133f, 0.773f, 0.369f, 1f);
    private static readonly Color DimCol        = new Color(0f, 0f, 0f, 0.80f);
    private static readonly Color PanelBg       = new Color(0.05f, 0.07f, 0.11f, 0.98f);
    private static readonly Color CardBg        = new Color(0.09f, 0.12f, 0.18f, 1f);
    private static readonly Color TextPri       = new Color(0.96f, 0.98f, 1f, 1f);
    private static readonly Color TextSec       = new Color(0.78f, 0.82f, 0.90f, 1f);

    private const float CARD_W = 360f;
    private const float CARD_H = 230f;
    private const float CARD_GAP = 28f;

    private const string LEGEND =
        "<color=#EF4444>● CRITICAL</color>   >   " +
        "<color=#FACC1A>● IMPORTANT</color>   >   " +
        "<color=#22C55E>● ROUTINE</color>      then soonest deadline";

    private class Card
    {
        public MissionTask Task;
        public int Key;
        public TMP_Text Badge;
    }

    private GameObject root;          // beat canvas
    private RectTransform cardArea;
    private Image countdownFill;
    private GameObject confirmBox;
    private TMP_Text confirmLabel;
    private readonly List<Card> cards = new List<Card>();
    private readonly List<GameObject> cardObjects = new List<GameObject>();

    private GameObject introRoot;     // separate one-time intro canvas
    public bool IntroActive { get; private set; }

    private bool active;
    private float window;
    private float startTime;

    public static Color TierColor(int tier) => tier == 0 ? TierCritical : tier == 1 ? TierImportant : TierRoutine;
    public static string TierName(int tier) => tier == 0 ? "CRITICAL" : tier == 1 ? "IMPORTANT" : "ROUTINE";

    public static PriorityBeatPanel GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("PriorityBeatPanel");
        Instance = go.AddComponent<PriorityBeatPanel>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureEventSystem();
        BuildBeat();
        BuildIntro();
        root.SetActive(false);
        introRoot.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ---------------------------------------------------------------- beat -----

    public void ShowBeat(List<MissionTask> offers, float windowSeconds)
    {
        BuildCards(offers);
        window = Mathf.Max(0.5f, windowSeconds);
        startTime = Time.time;
        active = true;
        if (confirmBox != null) confirmBox.SetActive(false);
        if (countdownFill != null) countdownFill.fillAmount = 1f;
        root.SetActive(true);
    }

    public void Confirm(string message)
    {
        active = false;
        if (countdownFill != null) countdownFill.fillAmount = 0f;
        if (confirmBox != null) confirmBox.SetActive(true);
        if (confirmLabel != null) confirmLabel.text = message;
    }

    public void HideBeat()
    {
        active = false;
        if (root != null) root.SetActive(false);
        ClearCards();
    }

    private void Update()
    {
        if (!active) return;
        float rem = Mathf.Max(0f, window - (Time.time - startTime));
        if (countdownFill != null) countdownFill.fillAmount = window > 0f ? rem / window : 0f;
        foreach (var c in cards)
        {
            if (c.Task == null || c.Badge == null) continue;
            if (c.Task.EfOrder > 0) { c.Badge.text = "> SELECTED <"; c.Badge.color = TierColor(c.Task.EfTier); }
            else { c.Badge.text = "TAP / " + c.Key; c.Badge.color = TextSec; }
        }
    }

    // ---------------------------------------------------------------- intro ----

    public void ShowIntro() { if (introRoot != null) { introRoot.SetActive(true); IntroActive = true; } }
    public void HideIntro() { if (introRoot != null) introRoot.SetActive(false); IntroActive = false; }
    private void OnIntroContinue() { AudioManager.Instance?.PlaySfx("button_click"); HideIntro(); }

    // --------------------------------------------------------------- build -----

    private void BuildBeat()
    {
        root = NewCanvas("PriorityBeat_Canvas", 130);

        var dim = NewFull("Dim", root.transform);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = DimCol; dimImg.raycastTarget = true;

        var panel = NewRect("Panel", root.transform, new Vector2(1320f, 640f), Vector2.zero,
            Half, Half, Half);
        panel.gameObject.AddComponent<Image>().color = PanelBg;

        // Title block pinned to the top with spacing; cards sit below, then the
        // footer hint and countdown — a clear top-to-bottom read.
        MakeText(panel, "!  PRIORITY EVENT", 48, FontStyles.Bold, TextPri,
            new Vector2(0f, 268f), new Vector2(1240f, 58f), TextAlignmentOptions.Center);
        MakeText(panel, "Which task is the HIGHEST priority right now?",
            28, FontStyles.Bold, TextPri, new Vector2(0f, 214f),
            new Vector2(1240f, 38f), TextAlignmentOptions.Center);
        MakeText(panel, LEGEND, 22, FontStyles.Bold, TextPri,
            new Vector2(0f, 172f), new Vector2(1240f, 32f), TextAlignmentOptions.Center);

        cardArea = NewRect("CardArea", panel, new Vector2(1280f, 250f), new Vector2(0f, -6f),
            Half, Half, Half);

        MakeText(panel, "Click a task, or press its number key",
            20, FontStyles.Italic, TextSec, new Vector2(0f, -158f),
            new Vector2(1240f, 30f), TextAlignmentOptions.Center);

        var barBg = NewRect("CountdownBg", panel, new Vector2(1180f, 10f), new Vector2(0f, -206f),
            Half, Half, Half);
        barBg.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.12f);
        var fill = NewRect("CountdownFill", barBg, Vector2.zero, Vector2.zero,
            new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0f, 0.5f));
        fill.offsetMin = Vector2.zero; fill.offsetMax = Vector2.zero;
        countdownFill = fill.gameObject.AddComponent<Image>();
        countdownFill.color = new Color(0.55f, 0.95f, 1f, 0.9f);
        countdownFill.type = Image.Type.Filled;
        countdownFill.fillMethod = Image.FillMethod.Horizontal;
        countdownFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        countdownFill.fillAmount = 1f;

        confirmBox = NewFull("ConfirmBox", panel).gameObject;
        confirmLabel = MakeText((RectTransform)confirmBox.transform, "OBJECTIVE SET", 60,
            FontStyles.Bold, new Color(0.4f, 1f, 0.65f, 1f), Vector2.zero,
            new Vector2(900f, 100f), TextAlignmentOptions.Center);
        confirmBox.SetActive(false);
    }

    private void BuildIntro()
    {
        introRoot = NewCanvas("PriorityBeatIntro_Canvas", 132);

        var dim = NewFull("Dim", introRoot.transform);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = DimCol; dimImg.raycastTarget = true;

        var panel = NewRect("Panel", introRoot.transform, new Vector2(1020f, 560f), Vector2.zero,
            Half, Half, Half);
        panel.gameObject.AddComponent<Image>().color = PanelBg;

        // Title pinned to the top with clear spacing; instructions sit below it.
        MakeText(panel, "PRIORITY EVENT", 48, FontStyles.Bold, TextPri,
            new Vector2(0f, 218f), new Vector2(940f, 60f), TextAlignmentOptions.Center);

        var body = MakeText(panel,
            "Every so often, several tasks will demand attention at once.\n\n" +
            "Your job: choose which one to handle FIRST — the most CRITICAL " +
            "(an immediate risk to the crew or the mission).\n\n" +
            "Read each card's reason, then click a task or press its number key.",
            25, FontStyles.Normal, TextSec, new Vector2(0f, 28f),
            new Vector2(880f, 200f), TextAlignmentOptions.Center);
        body.textWrappingMode = TextWrappingModes.Normal;

        MakeText(panel, LEGEND, 22, FontStyles.Bold, TextPri,
            new Vector2(0f, -132f), new Vector2(940f, 32f), TextAlignmentOptions.Center);

        BuildButton(panel, new Vector2(0f, -218f), new Vector2(320f, 62f),
            "I UNDERSTAND", new Color(0.18f, 0.45f, 0.62f, 1f), OnIntroContinue);

        introRoot.SetActive(false);
    }

    private void BuildCards(List<MissionTask> offers)
    {
        ClearCards();
        if (offers == null || offers.Count == 0) return;
        int n = offers.Count;
        float totalW = n * CARD_W + (n - 1) * CARD_GAP;
        float startX = -totalW * 0.5f + CARD_W * 0.5f;

        for (int i = 0; i < n; i++)
        {
            var task = offers[i];
            if (task == null) continue;
            float x = startX + i * (CARD_W + CARD_GAP);
            cards.Add(BuildCard(task, i + 1, x));
        }
    }

    private Card BuildCard(MissionTask task, int key, float x)
    {
        Color tc = TierColor(task.EfTier);

        var cardRt = NewRect("Card" + key, cardArea, new Vector2(CARD_W, CARD_H),
            new Vector2(x, 0f), Half, Half, Half);
        var cardImg = cardRt.gameObject.AddComponent<Image>();
        cardImg.color = CardBg; cardImg.raycastTarget = true;
        var btn = cardRt.gameObject.AddComponent<Button>();
        btn.targetGraphic = cardImg;
        var captured = task;
        btn.onClick.AddListener(() => TaskListHUD.EnqueueEfClick(captured));

        var border = NewRect("Border", cardRt, new Vector2(10f, 0f), Vector2.zero,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f));
        border.offsetMin = new Vector2(0f, 0f); border.offsetMax = new Vector2(10f, 0f);
        var borderImg = border.gameObject.AddComponent<Image>();
        borderImg.color = tc; borderImg.raycastTarget = false;

        var badge = NewRect("KeyBadge", cardRt, new Vector2(48f, 48f), new Vector2(34f, -34f),
            new Vector2(0f, 1f), new Vector2(0f, 1f), Half);
        badge.gameObject.AddComponent<Image>().color = tc;
        var keyLbl = MakeText(badge, key.ToString(), 28, FontStyles.Bold, new Color(0.04f, 0.06f, 0.1f, 1f),
            Vector2.zero, new Vector2(48f, 48f), TextAlignmentOptions.Center);
        keyLbl.raycastTarget = false;

        var tier = MakeText(cardRt, "● " + TierName(task.EfTier), 20, FontStyles.Bold, tc,
            new Vector2(-16f, -34f), new Vector2(220f, 28f), TextAlignmentOptions.Right);
        SetAnchor((RectTransform)tier.transform, new Vector2(1f, 1f), new Vector2(-16f, -34f));

        MakeText(cardRt, TaskListHUD.PrettyStation(task.StationName).ToUpperInvariant(),
            30, FontStyles.Bold, TextPri, new Vector2(0f, 26f), new Vector2(CARD_W - 40f, 40f),
            TextAlignmentOptions.Center);

        string reason = string.IsNullOrEmpty(task.EfReason) ? "" : "\"" + task.EfReason + "\"";
        var reasonT = MakeText(cardRt, reason, 22, FontStyles.Italic, TextSec,
            new Vector2(0f, -26f), new Vector2(CARD_W - 48f, 56f), TextAlignmentOptions.Center);
        reasonT.textWrappingMode = TextWrappingModes.Normal;

        var deadline = MakeText(cardRt, "DUE IN " + Mathf.RoundToInt(task.EfDeadline) + "s",
            20, FontStyles.Bold, TextSec, new Vector2(20f, 20f), new Vector2(160f, 28f),
            TextAlignmentOptions.Left);
        SetAnchor((RectTransform)deadline.transform, new Vector2(0f, 0f), new Vector2(20f, 20f));

        var badgeLbl = MakeText(cardRt, "TAP / " + key, 22, FontStyles.Bold, TextSec,
            new Vector2(-16f, 18f), new Vector2(180f, 30f), TextAlignmentOptions.Right);
        SetAnchor((RectTransform)badgeLbl.transform, new Vector2(1f, 0f), new Vector2(-16f, 18f));

        return new Card { Task = task, Key = key, Badge = badgeLbl };
    }

    private void ClearCards()
    {
        foreach (var go in cardObjects) if (go != null) Destroy(go);
        cardObjects.Clear();
        cards.Clear();
    }

    // ------------------------------------------------------------- helpers -----

    private static readonly Vector2 Half = new Vector2(0.5f, 0.5f);

    private GameObject NewCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        go.AddComponent<GraphicRaycaster>();
        return go;
    }

    private void BuildButton(RectTransform parent, Vector2 pos, Vector2 size, string label, Color color, Action onClick)
    {
        var rt = NewRect("Btn", parent, size, pos, Half, Half, Half);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        var lbl = MakeText(rt, label, 22, FontStyles.Bold, new Color(1f, 1f, 1f, 0.95f),
            Vector2.zero, size, TextAlignmentOptions.Center);
        lbl.raycastTarget = false;
    }

    private static void SetAnchor(RectTransform rt, Vector2 anchor, Vector2 pos)
    {
        rt.anchorMin = anchor; rt.anchorMax = anchor; rt.pivot = anchor;
        rt.anchoredPosition = pos;
    }

    private static void EnsureEventSystem()
    {
        if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        UnityEngine.Object.DontDestroyOnLoad(es);
    }

    private RectTransform NewFull(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    private RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        if (parent == cardArea) cardObjects.Add(go);
        return rt;
    }

    private TMP_Text MakeText(RectTransform parent, string text, int size, FontStyles style,
        Color color, Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align)
    {
        var rt = NewRect("Label", parent, sizeDelta, pos, Half, Half, Half);
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text; lbl.fontSize = size; lbl.fontStyle = style; lbl.color = color;
        lbl.alignment = align; lbl.raycastTarget = false; lbl.richText = true;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        return lbl;
    }
}
