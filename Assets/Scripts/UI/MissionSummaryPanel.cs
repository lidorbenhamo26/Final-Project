using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Player-facing "MISSION COMPLETE" screen, shown the moment the mission ends —
/// BEFORE the clinical assessor report. Deliberately minimal: a title, a short
/// congratulatory line, and a single button to open the detailed report. All the
/// numbers live in the assessor report; this screen is just a satisfying beat of
/// closure. Freezes the mission while up so in-flight tasks can't expire behind it.
/// </summary>
[DisallowMultipleComponent]
public class MissionSummaryPanel : MonoBehaviour
{
    public static MissionSummaryPanel Instance { get; private set; }

    private static readonly Color DimCol  = new Color(0f, 0f, 0f, 0.85f);
    private static readonly Color PanelBg = new Color(0.05f, 0.07f, 0.11f, 0.99f);
    private static readonly Color TextPri = new Color(0.96f, 0.98f, 1f, 1f);
    private static readonly Color TextSec = new Color(0.80f, 0.85f, 0.92f, 1f);

    private GameObject root;
    private TMP_Text flavor;
    private Action onContinue;

    public static MissionSummaryPanel GetOrCreate()
    {
        if (Instance != null) return Instance;
        var go = new GameObject("MissionSummaryPanel");
        Instance = go.AddComponent<MissionSummaryPanel>();
        return Instance;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        EnsureEventSystem();
        Build();
        root.SetActive(false);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    public void Show(Action continueCallback)
    {
        onContinue = continueCallback;

        // A small, warm sign-off; nudges to the crew-safe variant when no system
        // was lost, but stays celebratory either way.
        var ship = ShipState.Instance;
        bool allSafe = ship == null || ship.CriticalsRaised == 0 || ship.CriticalsStabilized >= ship.CriticalsRaised;
        if (flavor != null)
            flavor.text = allSafe
                ? "Well done, Commander — the mission was completed successfully."
                : "Mission complete. The crew made it through.";

        GameManager.SetDebugFrozen(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        AudioManager.Instance?.PlaySfx("success_chime");
        root.SetActive(true);
    }

    private void Continue()
    {
        AudioManager.Instance?.PlaySfx("button_click");
        root.SetActive(false);
        var cb = onContinue; onContinue = null;
        cb?.Invoke();
    }

    // ---------------------------------------------------------------- build ----

    private void Build()
    {
        var canvasGO = new GameObject("MissionSummary_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 140;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();
        root = canvasGO;

        var dim = NewFull("Dim", canvasGO.transform);
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = DimCol; dimImg.raycastTarget = true;

        var panel = NewRect("Panel", canvasGO.transform, new Vector2(760f, 380f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var panelImg = panel.gameObject.AddComponent<Image>();
        panelImg.color = PanelBg; panelImg.raycastTarget = true;

        MakeText(panel, "MISSION COMPLETE", 56, FontStyles.Bold, TextPri,
            new Vector2(0f, 100f), new Vector2(700f, 70f), TextAlignmentOptions.Center);
        flavor = MakeText(panel, "Well done, Commander.", 26, FontStyles.Normal, TextSec,
            new Vector2(0f, 10f), new Vector2(660f, 70f), TextAlignmentOptions.Center);
        flavor.textWrappingMode = TextWrappingModes.Normal;

        BuildButton(panel, new Vector2(0f, -110f), new Vector2(380f, 64f),
            "VIEW ASSESSOR REPORT", new Color(0.18f, 0.45f, 0.62f, 1f), Continue);
    }

    private void BuildButton(RectTransform parent, Vector2 pos, Vector2 size, string label, Color color, Action onClick)
    {
        var rt = NewRect("Btn", parent, size, pos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(() => onClick());
        var lbl = MakeText(rt, label, 22, FontStyles.Bold, new Color(1f, 1f, 1f, 0.95f),
            Vector2.zero, size, TextAlignmentOptions.Center);
        lbl.raycastTarget = false;
    }

    // -------------------------------------------------------------- helpers ----

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
        return rt;
    }

    private TMP_Text MakeText(RectTransform parent, string text, int size, FontStyles style,
        Color color, Vector2 pos, Vector2 sizeDelta, TextAlignmentOptions align)
    {
        var rt = NewRect("Label", parent, sizeDelta, pos,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text; lbl.fontSize = size; lbl.fontStyle = style; lbl.color = color;
        lbl.alignment = align; lbl.raycastTarget = false; lbl.richText = true;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        return lbl;
    }
}
