using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// On-screen + keyboard controls for the assessor running a session:
///   PAUSE / RESUME  — freezes the mission timer and every active task together
///                     (via GameManager.SetPaused). Reaction times are not
///                     corrupted by the paused interval. Shortcut: P.
///   STOP            — ends the session early (with a confirm so a participant
///                     can't end it by accident) and still generates the report
///                     from the data collected so far.
/// A dimmed "PAUSED" overlay makes the held state obvious. Builds its own
/// screen-space canvas; lives only in the real mission scene.
/// </summary>
[DisallowMultipleComponent]
public class AssessorControls : MonoBehaviour
{
    private static readonly Color BarBg      = new Color(0.04f, 0.06f, 0.10f, 0.85f);
    private static readonly Color PauseCol   = new Color(0.18f, 0.45f, 0.62f, 1f);
    private static readonly Color StopCol    = new Color(0.62f, 0.20f, 0.20f, 1f);
    private static readonly Color NeutralCol = new Color(0.28f, 0.32f, 0.38f, 1f);

    private GameManager gm;
    private GameObject bar;
    private TMP_Text pauseLabel;
    private GameObject pausedOverlay;
    private GameObject confirmOverlay;

    private bool confirmShown;
    private bool pausedForConfirm;
    private bool cursorFreed;
    private CursorLockMode prevLock;
    private bool prevVisible;

    private void Start()
    {
        BuildUI();
        SetOverlay(pausedOverlay, false);
        SetOverlay(confirmOverlay, false);
    }

    private void Update()
    {
        if (gm == null) gm = GameManager.Instance;
        if (gm == null) return;

        bool show = gm.MissionActive;
        if (bar != null && bar.activeSelf != show) bar.SetActive(show);
        if (!show)
        {
            SetOverlay(pausedOverlay, false);
            if (confirmShown) { SetOverlay(confirmOverlay, false); confirmShown = false; }
            return;
        }

        var kb = Keyboard.current;
        if (kb != null && kb.pKey.wasPressedThisFrame && !confirmShown) TogglePause();
    }

    // ----------------------------------------------------------------- logic ---
    private void TogglePause()
    {
        if (gm == null || !gm.MissionActive || confirmShown) return;
        if (gm.MissionPaused) Resume(); else Pause();
    }

    private void Pause()
    {
        gm.SetPaused(true);
        SetOverlay(pausedOverlay, true);
        FreeCursor();
        UpdatePauseLabel();
    }

    private void Resume()
    {
        gm.SetPaused(false);
        SetOverlay(pausedOverlay, false);
        if (!confirmShown) RestoreCursor();
        UpdatePauseLabel();
    }

    private void RequestStop()
    {
        if (gm == null || !gm.MissionActive) return;
        // Freeze while the assessor decides so nothing expires behind the dialog.
        pausedForConfirm = !gm.MissionPaused;
        if (pausedForConfirm) gm.SetPaused(true);
        confirmShown = true;
        SetOverlay(confirmOverlay, true);
        FreeCursor();
    }

    private void CancelStop()
    {
        confirmShown = false;
        SetOverlay(confirmOverlay, false);
        if (pausedForConfirm) { gm.SetPaused(false); pausedForConfirm = false; }
        bool stillPaused = gm.MissionPaused;
        SetOverlay(pausedOverlay, stillPaused);
        if (!stillPaused) RestoreCursor();
        UpdatePauseLabel();
    }

    private void ConfirmStop()
    {
        confirmShown = false;
        SetOverlay(confirmOverlay, false);
        SetOverlay(pausedOverlay, false);
        RestoreCursor();
        gm.EndMissionEarly(); // report takes over (it manages its own cursor)
    }

    private void UpdatePauseLabel()
    {
        if (pauseLabel != null)
            pauseLabel.text = (gm != null && gm.MissionPaused) ? "RESUME  (P)" : "PAUSE  (P)";
    }

    // --------------------------------------------------------------- cursor ---
    private void FreeCursor()
    {
        if (!cursorFreed)
        {
            prevLock = Cursor.lockState;
            prevVisible = Cursor.visible;
            cursorFreed = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void RestoreCursor()
    {
        if (!cursorFreed) return;
        Cursor.lockState = prevLock;
        Cursor.visible = prevVisible;
        cursorFreed = false;
    }

    // ----------------------------------------------------------------- UI ------
    private void BuildUI()
    {
        var canvasGO = new GameObject("AssessorControls_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120; // above the HUD (100)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        // Overlays first (drawn under the bar).
        pausedOverlay = BuildPausedOverlay(canvasGO.transform);
        confirmOverlay = BuildConfirmOverlay(canvasGO.transform);

        // Control bar on top.
        bar = BuildBar(canvasGO.transform);
        UpdatePauseLabel();
    }

    private GameObject BuildBar(Transform parent)
    {
        // Small, subtle bar tucked into the top-right corner so it stays out of
        // the participant's way.
        var barRt = NewRect("Bar", parent, new Vector2(224f, 40f), new Vector2(-12f, -12f),
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f));
        barRt.gameObject.AddComponent<Image>().color = new Color(0.04f, 0.06f, 0.10f, 0.30f);

        Color pauseMuted = new Color(0.18f, 0.45f, 0.62f, 0.50f);
        Color stopMuted  = new Color(0.62f, 0.20f, 0.20f, 0.50f);

        var pauseBtn = BuildButton(barRt, new Vector2(-56f, 0f), new Vector2(104f, 30f),
            "PAUSE (P)", pauseMuted, TogglePause, 13f);
        pauseLabel = pauseBtn.GetComponentInChildren<TMP_Text>();

        BuildButton(barRt, new Vector2(56f, 0f), new Vector2(104f, 30f), "STOP", stopMuted, RequestStop, 13f);
        return barRt.gameObject;
    }

    private GameObject BuildPausedOverlay(Transform parent)
    {
        var rt = NewFull("PausedOverlay", parent);
        rt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        SpawnText(rt, "PAUSED", 90, FontStyles.Bold, new Vector2(0f, 40f),
            new Vector2(900f, 120f), Color.white);
        SpawnText(rt, "Mission frozen — press  P  or RESUME to continue", 26,
            FontStyles.Normal, new Vector2(0f, -40f), new Vector2(1100f, 40f),
            new Color(0.8f, 0.86f, 0.95f, 1f));
        return rt.gameObject;
    }

    private GameObject BuildConfirmOverlay(Transform parent)
    {
        var rt = NewFull("StopConfirmOverlay", parent);
        rt.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.7f);

        var panel = NewRect("Panel", rt, new Vector2(720f, 280f), Vector2.zero,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        panel.gameObject.AddComponent<Image>().color = new Color(0.06f, 0.08f, 0.12f, 0.98f);

        SpawnText(panel, "END SESSION EARLY?", 38, FontStyles.Bold, new Vector2(0f, 90f),
            new Vector2(680f, 50f), Color.white);
        var body = SpawnText(panel, "The report will be generated from the data collected so far. This cannot be undone.",
            22, FontStyles.Normal, new Vector2(0f, 20f), new Vector2(620f, 80f),
            new Color(0.82f, 0.86f, 0.92f, 1f));
        body.textWrappingMode = TextWrappingModes.Normal;

        BuildButton(panel, new Vector2(-160f, -90f), new Vector2(260f, 56f), "YES, END SESSION", StopCol, ConfirmStop);
        BuildButton(panel, new Vector2(160f, -90f), new Vector2(260f, 56f), "NO, CONTINUE", NeutralCol, CancelStop);
        return rt.gameObject;
    }

    // --------------------------------------------------------------- helpers ---
    private Button BuildButton(RectTransform parent, Vector2 pos, Vector2 size, string label, Color color, UnityEngine.Events.UnityAction onClick, float fontSize = 22f)
    {
        var rt = NewRect("Btn", parent, size, pos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);

        var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f));
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = label;
        lbl.fontSize = fontSize;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = new Color(1f, 1f, 1f, 0.92f);
        lbl.alignment = TextAlignmentOptions.Center;
        return btn;
    }

    private static void SetOverlay(GameObject go, bool on)
    {
        if (go != null && go.activeSelf != on) go.SetActive(on);
    }

    private static RectTransform NewFull(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return rt;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 pos,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.sizeDelta = size; rt.anchoredPosition = pos;
        return rt;
    }

    private static TMP_Text SpawnText(RectTransform parent, string text, int size, FontStyles style,
        Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var rt = NewRect("Label", parent, sizeDelta, pos, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text; lbl.fontSize = size; lbl.fontStyle = style; lbl.color = color;
        lbl.alignment = TextAlignmentOptions.Center; lbl.raycastTarget = false;
        return lbl;
    }
}
