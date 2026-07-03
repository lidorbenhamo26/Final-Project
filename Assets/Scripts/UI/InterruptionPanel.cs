using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Decision card for the Interruption EF event. When a new task barges in while
/// the player is working, the MISSION PAUSES and the card asks for the explicit
/// executive decision — YOUR CALL: STAY OR SWITCH? — as two clickable options:
///   STAY   — the current task (station + tier + reason + "keep working here"),
///   SWITCH — the incoming task (station + tier + reason + what to do next).
/// The player clicks one inside the choice window; no response defaults to
/// STAY (the director records it as such). The Priority Protocol legend rides
/// along so the rule is visible AT the decision moment (no memory required).
/// The card still never says which response is correct for THIS instance —
/// applying the rule is the measure.
///
/// Also owns the one-time INTRO screen shown (held) before the player's first
/// interruption, mirroring PriorityBeatPanel's intro pattern.
///
/// Glyph note: the project's static TMP atlas only covers ASCII plus ▲ and ●,
/// so those are the only non-ASCII characters used here.
/// </summary>
[DisallowMultipleComponent]
public class InterruptionPanel : MonoBehaviour
{
    public static InterruptionPanel Instance { get; private set; }

    // Values handed to the director via TakePendingChoice().
    public const int ChoiceStay = 1;
    public const int ChoiceSwitch = 2;

    private static readonly Color PanelBg  = new Color(0.05f, 0.07f, 0.11f, 0.96f);
    private static readonly Color BlockBg  = new Color(0.09f, 0.12f, 0.18f, 1f);
    private static readonly Color DimCol   = new Color(0f, 0f, 0f, 0.80f);
    private static readonly Color TextPri  = new Color(0.96f, 0.98f, 1f, 1f);
    private static readonly Color TextSec  = new Color(0.78f, 0.82f, 0.90f, 1f);

    private const float PANEL_W = 1100f;
    private const float PANEL_H = 252f;
    private const float BLOCK_W = 520f;
    private const float BLOCK_H = 138f;

    // Entrance salience: the player is heads-down in a console task when the
    // card appears, so it scales in and double-flashes its tier strip instead
    // of dimming/blocking the console underneath.
    private const float ScaleInSeconds = 0.25f;
    private const float StripFlashSeconds = 0.7f;

    private class Block
    {
        public GameObject Root;
        public Image Border;
        public Button Button;
        public TMP_Text Title, Station, Reason, Action;
    }

    private GameObject root;
    private RectTransform rootRT;
    private GameObject choiceDim;   // full-screen dim while the held choice is open
    private Image tierStrip;
    private TMP_Text headerLabel;
    private TMP_Text ruleLabel;
    private Block stayBlock;
    private Block switchBlock;
    private TMP_Text confirmLabel;
    private TMP_Text hintLabel;
    private Image countdownFill;

    private GameObject introRoot;     // separate one-time intro canvas
    public bool IntroActive { get; private set; }

    private bool counting;
    private float window;
    private float startTime;
    private float shownAt;
    private float hideAt = -1f;
    private Color headerColor;
    private int pendingChoice;      // ChoiceStay / ChoiceSwitch, 0 = none yet

    /// <summary>Consume the player's click, if any (0 = no choice yet).</summary>
    public int TakePendingChoice()
    {
        int c = pendingChoice;
        pendingChoice = 0;
        return c;
    }

    public static InterruptionPanel GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("InterruptionPanel");
        Instance = go.AddComponent<InterruptionPanel>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureEventSystem(); // the intro's I UNDERSTAND button needs one
        Build();
        BuildIntro();
        root.SetActive(false);
        introRoot.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>
    /// Present the interruption: <paramref name="incoming"/> barged in while the
    /// player works on <paramref name="current"/>. The countdown bar runs for
    /// <paramref name="windowSeconds"/> (the held choice window — the director
    /// pauses the mission and polls TakePendingChoice); the card auto-hides
    /// after <paramref name="holdSeconds"/> unless resolved earlier.
    /// </summary>
    public void Show(MissionTask current, MissionTask incoming, float windowSeconds, float holdSeconds)
    {
        if (root == null) return;

        string newStation = TaskListHUD.PrettyStation(incoming.StationName).ToUpperInvariant();
        string curStation = TaskListHUD.PrettyStation(current.StationName).ToUpperInvariant();

        headerColor = PriorityBeatPanel.TierColor(incoming.EfTier);
        if (headerLabel != null)
        {
            headerLabel.text = "▲ INTERRUPTION — YOUR CALL: STAY OR SWITCH?";
            headerLabel.color = headerColor;
        }
        if (ruleLabel != null) ruleLabel.text = PriorityBeatPanel.LEGEND;

        FillBlock(stayBlock, "STAY — CURRENT TASK", curStation, current.EfTier,
            current.EfReason, "CLICK — keep working here");
        FillBlock(switchBlock, "SWITCH — NEW TASK", newStation, incoming.EfTier,
            incoming.EfReason, "CLICK or E — undock and go to " + newStation);

        pendingChoice = 0;
        SetChoiceInteractable(true);
        if (stayBlock != null && stayBlock.Root != null) stayBlock.Root.SetActive(true);
        if (switchBlock != null && switchBlock.Root != null) switchBlock.Root.SetActive(true);
        if (hintLabel != null) hintLabel.gameObject.SetActive(true);
        if (confirmLabel != null) confirmLabel.gameObject.SetActive(false);

        window = Mathf.Max(0.5f, windowSeconds);
        startTime = Time.time;
        shownAt = Time.time;
        counting = true;
        hideAt = Time.time + Mathf.Max(holdSeconds, windowSeconds);
        if (countdownFill != null) { countdownFill.fillAmount = 1f; countdownFill.color = headerColor; }
        if (tierStrip != null) tierStrip.color = headerColor;
        if (rootRT != null) rootRT.localScale = new Vector3(0.92f, 0.92f, 1f);
        root.SetActive(true);
    }

    // The dim swallows every click that isn't one of the two options while the
    // held choice is open, so the console UI behind the card can't be hit.
    private void SetChoiceInteractable(bool on)
    {
        if (choiceDim != null) choiceDim.SetActive(on);
        if (stayBlock != null && stayBlock.Button != null) stayBlock.Button.interactable = on;
        if (switchBlock != null && switchBlock.Button != null) switchBlock.Button.interactable = on;
    }

    private void OnChoiceClicked(int choice)
    {
        if (pendingChoice != 0) return;
        pendingChoice = choice;
        AudioManager.Instance?.PlaySfx("button_click");
    }

    private void FillBlock(Block b, string title, string station, int tier, string reason, string action)
    {
        if (b == null) return;
        if (b.Border != null) b.Border.color = PriorityBeatPanel.TierColor(tier);
        if (b.Title != null) b.Title.text = title;
        if (b.Station != null)
            b.Station.text = station + "   <color=#5A6478>·</color>   " + TierBadge(tier);
        if (b.Reason != null) b.Reason.text = string.IsNullOrEmpty(reason) ? "" : "\"" + reason + "\"";
        if (b.Action != null) b.Action.text = action;
    }

    /// <summary>
    /// The player's call was made (or the window expired and STAY was applied by
    /// default). Swaps the option blocks for a short confirmation of the call —
    /// no judgement of whether it was optimal — plus what that means for the
    /// other task, then fades.
    /// </summary>
    public void ResolveChoice(bool switched, string targetStationPretty, string otherStationPretty,
        bool noResponse = false)
    {
        if (root == null || !root.activeSelf) return;
        counting = false;
        SetChoiceInteractable(false);
        if (countdownFill != null) countdownFill.fillAmount = 0f;
        if (stayBlock != null && stayBlock.Root != null) stayBlock.Root.SetActive(false);
        if (switchBlock != null && switchBlock.Root != null) switchBlock.Root.SetActive(false);
        if (hintLabel != null) hintLabel.gameObject.SetActive(false);
        if (confirmLabel != null)
        {
            string text;
            if (switched)
            {
                text = "SWITCHING — GO TO " + targetStationPretty;
                if (!string.IsNullOrEmpty(otherStationPretty))
                    text += "   <color=#5A6478>·</color>   " + otherStationPretty + " STAYS ON YOUR LIST";
            }
            else
            {
                text = noResponse ? "NO RESPONSE — STAYING ON CURRENT TASK" : "STAYING ON CURRENT TASK";
                if (!string.IsNullOrEmpty(otherStationPretty))
                    text += "   <color=#5A6478>·</color>   " + otherStationPretty + " IS NEXT ON YOUR LIST";
            }
            confirmLabel.text = text;
            confirmLabel.gameObject.SetActive(true);
        }
        hideAt = Time.time + 2.6f;
    }

    public void Hide()
    {
        counting = false;
        hideAt = -1f;
        SetChoiceInteractable(false);
        if (root != null) root.SetActive(false);
    }

    // ---------------------------------------------------------------- intro ----

    public void ShowIntro() { if (introRoot != null) { introRoot.SetActive(true); IntroActive = true; } }
    public void HideIntro() { if (introRoot != null) introRoot.SetActive(false); IntroActive = false; }
    private void OnIntroContinue() { AudioManager.Instance?.PlaySfx("button_click"); HideIntro(); }

    private void Update()
    {
        if (root == null || !root.activeSelf) return;

        float dt = Time.time - shownAt;

        if (rootRT != null)
        {
            float s = dt >= ScaleInSeconds ? 1f : Mathf.Lerp(0.92f, 1f, dt / ScaleInSeconds);
            rootRT.localScale = new Vector3(s, s, 1f);
        }
        if (tierStrip != null)
        {
            float a = 1f;
            if (dt < StripFlashSeconds)
                a = Mathf.FloorToInt(dt / (StripFlashSeconds * 0.25f)) % 2 == 0 ? 1f : 0.25f;
            tierStrip.color = new Color(headerColor.r, headerColor.g, headerColor.b, a);
        }

        if (counting && countdownFill != null)
        {
            float rem = Mathf.Max(0f, window - (Time.time - startTime));
            countdownFill.fillAmount = window > 0f ? rem / window : 0f;
        }

        // Attention pulse on the header (same rhythm as the old banner).
        if (headerLabel != null)
        {
            float t = Mathf.PingPong(Time.time * 3f, 1f);
            headerLabel.color = new Color(headerColor.r, headerColor.g, headerColor.b, 0.65f + 0.35f * t);
        }

        if (hideAt > 0f && Time.time >= hideAt) Hide();
    }

    private static string TierBadge(int tier)
    {
        string hex = ColorUtility.ToHtmlStringRGB(PriorityBeatPanel.TierColor(tier));
        return "<color=#" + hex + ">● " + PriorityBeatPanel.TierName(tier) + "</color>";
    }

    // --------------------------------------------------------------- build -----

    private void Build()
    {
        var canvasGO = new GameObject("Interruption_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 118; // above the HUD (100), below the Priority Beat (130)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>(); // the STAY/SWITCH options are buttons

        // Behind the card: dims the paused game and swallows stray clicks while
        // the held choice is open (active only between Show and the choice).
        choiceDim = new GameObject("ChoiceDim", typeof(RectTransform), typeof(Image));
        choiceDim.transform.SetParent(canvasGO.transform, false);
        var cdrt = (RectTransform)choiceDim.transform;
        cdrt.anchorMin = Vector2.zero; cdrt.anchorMax = Vector2.one;
        cdrt.offsetMin = Vector2.zero; cdrt.offsetMax = Vector2.zero;
        var cdImg = choiceDim.GetComponent<Image>();
        cdImg.color = new Color(0f, 0f, 0f, 0.45f);
        cdImg.raycastTarget = true;
        choiceDim.SetActive(false);

        root = new GameObject("Card", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(canvasGO.transform, false);
        rootRT = (RectTransform)root.transform;
        rootRT.anchorMin = new Vector2(0.5f, 1f);
        rootRT.anchorMax = new Vector2(0.5f, 1f);
        rootRT.pivot = new Vector2(0.5f, 1f);
        // Clears the top-left stats panel (ends x≈396) and the top-right station
        // status panel (starts x≈1660): (1920-1100)/2 = 410 of side margin.
        rootRT.anchoredPosition = new Vector2(0f, -8f);
        rootRT.sizeDelta = new Vector2(PANEL_W, PANEL_H);
        var bg = root.GetComponent<Image>();
        bg.color = PanelBg;
        bg.raycastTarget = false;

        var outline = root.AddComponent<Outline>();
        outline.effectDistance = new Vector2(2f, 2f);

        var strip = new GameObject("TierStrip", typeof(RectTransform), typeof(Image));
        strip.transform.SetParent(root.transform, false);
        var srt = (RectTransform)strip.transform;
        srt.anchorMin = new Vector2(0f, 1f); srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = Vector2.zero;
        srt.sizeDelta = new Vector2(0f, 4f);
        tierStrip = strip.GetComponent<Image>();
        tierStrip.raycastTarget = false;

        headerLabel = MakeLine(root.transform, "Header", -10f, 32f, 26,
            FontStyles.Bold, TextAlignmentOptions.Center);
        EnableAutoFit(headerLabel, 18f, 26f);

        // The rule, visible at the decision moment (single source: the shared
        // Priority Protocol legend), so choosing tests the rule, not recall.
        ruleLabel = MakeLine(root.transform, "Rule", -44f, 20f, 15,
            FontStyles.Bold, TextAlignmentOptions.Center);
        ruleLabel.color = TextSec;
        EnableAutoFit(ruleLabel, 11f, 15f);

        stayBlock = BuildBlock("StayBlock", -(BLOCK_W * 0.5f + 12f), ChoiceStay);
        switchBlock = BuildBlock("SwitchBlock", BLOCK_W * 0.5f + 12f, ChoiceSwitch);

        confirmLabel = MakeLine(root.transform, "Confirm", -128f, 40f, 26,
            FontStyles.Bold, TextAlignmentOptions.Center);
        confirmLabel.color = TextPri;
        EnableAutoFit(confirmLabel, 16f, 26f);
        confirmLabel.gameObject.SetActive(false);

        // How-to-answer line: the card itself says what a valid response is and
        // what silence does, so no one has to remember the intro.
        hintLabel = MakeLine(root.transform, "Hint", -210f, 20f, 13,
            FontStyles.Normal, TextAlignmentOptions.Center);
        hintLabel.color = TextSec;
        EnableAutoFit(hintLabel, 10f, 13f);
        hintLabel.text = "CLICK YOUR CALL   <color=#5A6478>·</color>   E = SWITCH   <color=#5A6478>·</color>   NO RESPONSE = STAY";

        var barBg = new GameObject("CountdownBg", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(root.transform, false);
        var brt = (RectTransform)barBg.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0.5f, 0f);
        brt.anchoredPosition = new Vector2(0f, 12f);
        brt.sizeDelta = new Vector2(-48f, 8f);
        var barBgImg = barBg.GetComponent<Image>();
        barBgImg.color = new Color(1f, 1f, 1f, 0.12f);
        barBgImg.raycastTarget = false;

        var fillGO = new GameObject("CountdownFill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barBg.transform, false);
        var frt = (RectTransform)fillGO.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = Vector2.zero; frt.offsetMax = Vector2.zero;
        countdownFill = fillGO.GetComponent<Image>();
        countdownFill.raycastTarget = false;
        countdownFill.type = Image.Type.Filled;
        countdownFill.fillMethod = Image.FillMethod.Horizontal;
        countdownFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        countdownFill.fillAmount = 1f;
    }

    private Block BuildBlock(string name, float x, int choice)
    {
        var b = new Block();
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(root.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(x, -70f);
        rt.sizeDelta = new Vector2(BLOCK_W, BLOCK_H);
        var img = go.GetComponent<Image>();
        img.color = BlockBg;
        img.raycastTarget = true; // the whole block is the click target
        b.Root = go;

        b.Button = go.AddComponent<Button>();
        b.Button.targetGraphic = img;
        b.Button.onClick.AddListener(() => OnChoiceClicked(choice));

        var border = new GameObject("Border", typeof(RectTransform), typeof(Image));
        border.transform.SetParent(go.transform, false);
        var brt = (RectTransform)border.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(0f, 1f);
        brt.pivot = new Vector2(0f, 0.5f);
        brt.anchoredPosition = Vector2.zero;
        brt.sizeDelta = new Vector2(6f, 0f);
        b.Border = border.GetComponent<Image>();
        b.Border.raycastTarget = false;

        b.Title   = MakeLine(go.transform, "Title",   -6f,  26f, 20, FontStyles.Bold,   TextAlignmentOptions.Center, 24f);
        b.Station = MakeLine(go.transform, "Station", -36f, 24f, 19, FontStyles.Bold,   TextAlignmentOptions.Center, 24f);
        b.Reason  = MakeLine(go.transform, "Reason",  -64f, 24f, 16, FontStyles.Italic, TextAlignmentOptions.Center, 24f);
        b.Action  = MakeLine(go.transform, "Action",  -96f, 30f, 15, FontStyles.Normal, TextAlignmentOptions.Center, 24f);
        b.Title.color = TextPri;
        b.Station.color = TextPri;
        b.Reason.color = TextSec;
        b.Action.color = TextSec;
        EnableAutoFit(b.Title, 14f, 20f);
        EnableAutoFit(b.Station, 13f, 19f);
        EnableAutoFit(b.Reason, 12f, 16f);
        EnableAutoFit(b.Action, 11f, 15f);
        return b;
    }

    private void BuildIntro()
    {
        var canvasGO = new GameObject("InterruptionIntro_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 132; // held explainer — above the beat panel (130)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();
        introRoot = canvasGO;

        var dim = new GameObject("Dim", typeof(RectTransform), typeof(Image));
        dim.transform.SetParent(canvasGO.transform, false);
        var drt = (RectTransform)dim.transform;
        drt.anchorMin = Vector2.zero; drt.anchorMax = Vector2.one;
        drt.offsetMin = Vector2.zero; drt.offsetMax = Vector2.zero;
        var dimImg = dim.GetComponent<Image>();
        dimImg.color = DimCol;
        dimImg.raycastTarget = true;

        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.pivot = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(1020f, 560f);
        panel.GetComponent<Image>().color = PanelBg;

        MakeIntroText(prt, "INTERRUPTIONS", 48, FontStyles.Bold, TextPri,
            new Vector2(0f, 218f), new Vector2(940f, 60f));

        var body = MakeIntroText(prt,
            "While you work, a new task may barge in. The mission PAUSES and a ▲ card " +
            "compares the NEW task's priority against your CURRENT one.\n\n" +
            "You have a few seconds to make the call: click STAY or SWITCH " +
            "(pressing E switches too). No response counts as STAY.\n\n" +
            "After choosing, act on it — anything you leave unfinished stays on your " +
            "task list, so you can return to it.",
            25, FontStyles.Normal, TextSec, new Vector2(0f, 30f), new Vector2(880f, 230f));
        body.textWrappingMode = TextWrappingModes.Normal;

        MakeIntroText(prt, PriorityBeatPanel.LEGEND, 22, FontStyles.Bold, TextPri,
            new Vector2(0f, -122f), new Vector2(940f, 32f));

        MakeIntroText(prt, "The mission is paused while you choose.", 20,
            FontStyles.Italic, TextSec, new Vector2(0f, -162f), new Vector2(940f, 28f));

        BuildIntroButton(prt, new Vector2(0f, -218f), new Vector2(320f, 62f), "I UNDERSTAND");

        introRoot.SetActive(false);
    }

    private void BuildIntroButton(RectTransform parent, Vector2 pos, Vector2 size, string label)
    {
        var go = new GameObject("Btn", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = go.GetComponent<Image>();
        img.color = new Color(0.18f, 0.45f, 0.62f, 1f);
        var btn = go.GetComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OnIntroContinue);
        MakeIntroText(rt, label, 22, FontStyles.Bold, new Color(1f, 1f, 1f, 0.95f),
            Vector2.zero, size);
    }

    private TMP_Text MakeIntroText(RectTransform parent, string text, int size, FontStyles style,
        Color color, Vector2 pos, Vector2 sizeDelta)
    {
        var go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.raycastTarget = false;
        lbl.richText = true;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        return lbl;
    }

    private static void EnsureEventSystem()
    {
        if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var es = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        Object.DontDestroyOnLoad(es);
    }

    private static void EnableAutoFit(TMP_Text lbl, float min, float max)
    {
        if (lbl == null) return;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = min;
        lbl.fontSizeMax = max;
    }

    private TMP_Text MakeLine(Transform parent, string name, float y, float height, int fontSize,
        FontStyles style, TextAlignmentOptions align, float sideMargin = 40f)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(-sideMargin, height);
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.fontSize = fontSize;
        lbl.fontStyle = style;
        lbl.alignment = align;
        lbl.richText = true;
        lbl.raycastTarget = false;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        lbl.overflowMode = TextOverflowModes.Ellipsis;
        lbl.text = "";
        return lbl;
    }
}
