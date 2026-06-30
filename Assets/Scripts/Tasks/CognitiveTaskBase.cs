using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared base class for all cognitive-load mission tasks.
/// Builds a world-space UI Canvas in front of the parent station and exposes
/// helpers for spawning labels and buttons procedurally (no prefabs).
///
/// Lifecycle:
///   Activate()        -> creates world-space canvas as child of station.
///   OnPlayerEnter()   -> shows canvas, marks dock-time, calls OnDocked().
///   OnPlayerExit()    -> hides canvas, calls OnUndocked().
///   Resolve(...)      -> base MissionTask destroys the GameObject; OnDestroy
///                        cleans the canvas if it was orphaned.
/// </summary>
public abstract class CognitiveTaskBase : MissionTask
{
    // World-space canvas root (a separate GameObject, parented under the station).
    protected GameObject taskCanvasRoot;
    protected RectTransform canvasRect;
    protected TMP_Text headerText;
    protected Transform buttonsParent;

    // Time the player first docked. -1 means "never docked yet".
    protected float DockTime { get; private set; } = -1f;
    protected bool IsDocked { get; private set; }

    // Track buttons we created so ClearButtons can remove only those.
    private readonly List<GameObject> spawnedButtons = new List<GameObject>();

    public void SetCanvasCamera(Camera cam)
    {
        if (taskCanvasRoot == null) return;
        var canvas = taskCanvasRoot.GetComponent<Canvas>();
        if (canvas != null) canvas.worldCamera = cam;
    }

    public override void Activate()
    {
        base.Activate();
        BuildCanvas();
        // Hidden until the player docks.
        HideCanvas();
    }

    public override void OnPlayerEnter()
    {
        if (!IsActive) return;
        IsDocked = true;
        ShowCanvas();

        // The first time this task type is ever docked in the session, show a
        // one-time instruction card and hold the task (and its trial clock, via
        // the debug freeze) until the player taps "GOT IT". Reading is never
        // scored. Subsequent docks (this run or later tasks of the same type)
        // skip straight to the task.
        if (DockTime < 0f && instructionCard == null
            && InstructionBody != null && InstructionBody.Length > 0
            && (SessionContext.Instance == null || !SessionContext.Instance.HasSeenTask(TaskTypeKey)))
        {
            ShowInstructionCard();
            return;
        }

        BeginDock();
    }

    // Sets the first-dock marker and runs the task's OnDocked hook. Split out so
    // the instruction card can defer it until dismissed.
    private void BeginDock()
    {
        if (DockTime < 0f) DockTime = Time.time;
        MarkEngaged(); // the player is now actually doing the task
        OnDocked();
    }

    public override void OnPlayerExit()
    {
        IsDocked = false;
        // Undocking before dismissing the card leaves it unread: tear it down and
        // unfreeze so the card shows again on the next dock (still not "seen").
        if (instructionCard != null) CleanupInstructionCard();
        HideCanvas();
        OnUndocked();
    }

    /// <summary>
    /// WaitForSeconds replacement that stops accumulating while the debug
    /// freeze (F11 / assessor report) is active, so trial pacing pauses with
    /// the rest of the mission instead of expiring behind the overlay.
    /// </summary>
    protected static System.Collections.IEnumerator FrozenWait(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (!GameManager.IsDebugFrozen) t += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>Hook called the first time (and every time) the player docks.</summary>
    protected virtual void OnDocked() { }

    /// <summary>Hook called when the player un-docks. Task remains alive.</summary>
    protected virtual void OnUndocked() { }

    // ----------------------------------------------- one-time instruction card

    private GameObject instructionCard;
    private bool cardFroze;

    /// <summary>Key used to remember "this type's card was shown". Defaults to
    /// the concrete class name so each task type shows its card once.</summary>
    protected virtual string TaskTypeKey => GetType().Name;

    /// <summary>Heading on the first-time instruction card.</summary>
    protected virtual string InstructionTitle => TaskName;

    /// <summary>Body lines for the first-time instruction card (one per line).
    /// Return null/empty to skip the card for this task type.</summary>
    protected virtual string[] InstructionBody => null;

    private void ShowInstructionCard()
    {
        if (taskCanvasRoot == null) { BeginDock(); return; }

        // Freeze trial/mission clocks while reading (same flag as F11/report) so
        // nothing is scored behind the card. Only un-freeze what we froze.
        if (!GameManager.IsDebugFrozen) { GameManager.SetDebugFrozen(true); cardFroze = true; }

        instructionCard = new GameObject("InstructionCard", typeof(RectTransform), typeof(Image));
        instructionCard.transform.SetParent(taskCanvasRoot.transform, false);
        RectTransform cardRt = instructionCard.GetComponent<RectTransform>();
        cardRt.anchorMin = Vector2.zero; cardRt.anchorMax = Vector2.one;
        cardRt.offsetMin = Vector2.zero; cardRt.offsetMax = Vector2.zero;
        instructionCard.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.09f, 0.98f);

        GameObject title = new GameObject("Title", typeof(RectTransform));
        title.transform.SetParent(instructionCard.transform, false);
        TextMeshProUGUI titleTmp = title.AddComponent<TextMeshProUGUI>();
        titleTmp.text = InstructionTitle;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.fontSize = 44f;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.color = new Color(0.6f, 0.85f, 1f);
        RectTransform trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0f, 1f); trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.anchoredPosition = new Vector2(0f, -36f);
        trt.sizeDelta = new Vector2(-60f, 80f);

        GameObject body = new GameObject("Body", typeof(RectTransform));
        body.transform.SetParent(instructionCard.transform, false);
        TextMeshProUGUI bodyTmp = body.AddComponent<TextMeshProUGUI>();
        bodyTmp.text = string.Join("\n", InstructionBody);
        bodyTmp.alignment = TextAlignmentOptions.Center;
        bodyTmp.fontSize = 30f;
        bodyTmp.color = Color.white;
        RectTransform brt = body.GetComponent<RectTransform>();
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 0.5f);
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(0f, 30f);
        brt.sizeDelta = new Vector2(720f, 300f);

        GameObject btnGo = new GameObject("GotItButton", typeof(RectTransform), typeof(Image), typeof(Button));
        btnGo.transform.SetParent(instructionCard.transform, false);
        RectTransform btnRt = btnGo.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(0.5f, 0f); btnRt.anchorMax = new Vector2(0.5f, 0f);
        btnRt.pivot = new Vector2(0.5f, 0f);
        btnRt.anchoredPosition = new Vector2(0f, 44f);
        btnRt.sizeDelta = new Vector2(320f, 92f);
        btnGo.GetComponent<Image>().color = new Color(0.2f, 0.72f, 0.42f);
        btnGo.GetComponent<Button>().onClick.AddListener(DismissInstructionCard);

        GameObject lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(btnGo.transform, false);
        TextMeshProUGUI lblTmp = lbl.AddComponent<TextMeshProUGUI>();
        lblTmp.text = "GOT IT";
        lblTmp.alignment = TextAlignmentOptions.Center;
        lblTmp.fontSize = 34f;
        lblTmp.fontStyle = FontStyles.Bold;
        lblTmp.color = Color.white;
        RectTransform lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;

        SessionManager.Instance?.LogCustomEvent("Instruction_Shown", StationName, "type=" + TaskTypeKey);
    }

    private void DismissInstructionCard()
    {
        SessionContext.Instance?.MarkTaskSeen(TaskTypeKey);
        AudioManager.Instance?.PlaySfx("button_click");
        SessionManager.Instance?.LogCustomEvent("Instruction_Dismissed", StationName, "type=" + TaskTypeKey);
        CleanupInstructionCard();
        if (IsDocked) BeginDock();
    }

    private void CleanupInstructionCard()
    {
        if (cardFroze) { GameManager.SetDebugFrozen(false); cardFroze = false; }
        if (instructionCard != null) { Destroy(instructionCard); instructionCard = null; }
    }

    // ---------------------------------------------------------------- canvas

    private void BuildCanvas()
    {
        Transform parent = transform.parent != null ? transform.parent : transform;

        taskCanvasRoot = new GameObject("CognitiveCanvas");
        taskCanvasRoot.transform.SetParent(parent, false);

        // The docked first-person camera sits between the station and the world
        // hub (world origin) and looks BACK toward the station. Unity world-space
        // UI reads correctly only when the canvas's +forward points AWAY from
        // the camera (i.e. toward the station/wall, the OPPOSITE of toHub), so
        // the camera sits on the canvas's +Z side and sees non-mirrored text.
        // This matches StationProximityPrompt.cs (fwd = canvas - cam). Compute
        // toHub in world space so the station's arbitrary world rotation does
        // not flip the text.
        Vector3 stationPos = parent.position;
        Vector3 toHub = -new Vector3(stationPos.x, 0f, stationPos.z);
        if (toHub.sqrMagnitude < 0.0001f) toHub = -parent.forward; // fallback
        toHub.y = 0f;
        toHub.Normalize();

        // Position the canvas slightly toward the hub from the console at
        // mid-console height so it sits between the docked camera and the
        // console body (clearly visible, not clipped by the console mesh).
        Vector3 pos = stationPos + toHub * 0.4f + Vector3.up * 1.3f;
        taskCanvasRoot.transform.position = pos;

        // Set rotation in WORLD space: canvas forward = -toHub (toward the
        // station/wall, AWAY from the docked camera at the hub side) so the
        // camera, sitting on the +Z side of the canvas, sees readable text.
        taskCanvasRoot.transform.rotation = Quaternion.LookRotation(-toHub, Vector3.up);

        Canvas canvas = taskCanvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        canvasRect = taskCanvasRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(800f, 500f); // 1.6m x 1.0m at scale 0.002
        canvasRect.localScale = Vector3.one * 0.002f;

        taskCanvasRoot.AddComponent<CanvasScaler>();
        taskCanvasRoot.AddComponent<GraphicRaycaster>();

        // Background panel.
        GameObject bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(taskCanvasRoot.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        Image bgImg = bg.GetComponent<Image>();
        bgImg.color = new Color(0.04f, 0.05f, 0.08f, 0.92f);

        // Header text (top of canvas).
        GameObject header = new GameObject("Header", typeof(RectTransform));
        header.transform.SetParent(taskCanvasRoot.transform, false);
        headerText = header.AddComponent<TextMeshProUGUI>();
        headerText.alignment = TextAlignmentOptions.Center;
        headerText.fontSize = 36f;
        headerText.color = Color.white;
        headerText.text = "";
        RectTransform headerRt = header.GetComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0f, 1f);
        headerRt.anchorMax = new Vector2(1f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -20f);
        headerRt.sizeDelta = new Vector2(0f, 80f);

        // Button area parent (everything below the header).
        GameObject area = new GameObject("ButtonArea", typeof(RectTransform));
        area.transform.SetParent(taskCanvasRoot.transform, false);
        RectTransform areaRt = area.GetComponent<RectTransform>();
        areaRt.anchorMin = new Vector2(0f, 0f);
        areaRt.anchorMax = new Vector2(1f, 1f);
        areaRt.offsetMin = new Vector2(0f, 0f);
        areaRt.offsetMax = new Vector2(0f, -100f);
        buttonsParent = area.transform;
    }

    protected void ShowMessage(string text, Color color)
    {
        if (headerText == null) return;
        headerText.text = text;
        headerText.color = color;
    }

    /// <summary>Spawns a UI Button inside the canvas's button area.</summary>
    protected Button SpawnButton(Vector2 anchorPos, Vector2 size, string label, Color color, Action onClick)
    {
        if (buttonsParent == null) return null;

        GameObject go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(buttonsParent, false);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchorPos;
        rt.sizeDelta = size;

        Image img = go.GetComponent<Image>();
        img.color = color;

        Button btn = go.GetComponent<Button>();
        if (onClick != null) btn.onClick.AddListener(() => onClick());

        // Label child.
        GameObject lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(go.transform, false);
        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 28f;
        tmp.color = PickReadableTextColor(color);
        RectTransform lrt = lbl.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        spawnedButtons.Add(go);
        return btn;
    }

    private static Color PickReadableTextColor(Color bg)
    {
        float lum = 0.299f * bg.r + 0.587f * bg.g + 0.114f * bg.b;
        return lum > 0.55f ? Color.black : Color.white;
    }

    protected void ClearButtons()
    {
        for (int i = spawnedButtons.Count - 1; i >= 0; i--)
        {
            if (spawnedButtons[i] != null) Destroy(spawnedButtons[i]);
        }
        spawnedButtons.Clear();
    }

    protected void HideCanvas()
    {
        if (taskCanvasRoot != null) taskCanvasRoot.SetActive(false);
    }

    protected void ShowCanvas()
    {
        if (taskCanvasRoot != null) taskCanvasRoot.SetActive(true);
    }

    protected virtual void OnDestroy()
    {
        // If the task is torn down with its card still up (e.g. mission ended),
        // make sure we don't leave the global freeze flag stuck on.
        CleanupInstructionCard();
        if (taskCanvasRoot != null) Destroy(taskCanvasRoot);
    }

    // --------------------------------------------------------------- helpers

    /// <summary>
    /// Spawns a transient overlay text that auto-destroys after `duration`
    /// seconds. Useful for "CORRECT!" / "WRONG!" splashes between rounds.
    /// Safe even if the canvas isn't built yet (returns null).
    /// </summary>
    protected GameObject ShowSplash(string text, Color color, float duration = 0.9f, float fontSize = 90f)
    {
        if (taskCanvasRoot == null || buttonsParent == null) return null;

        GameObject splash = new GameObject("Splash", typeof(RectTransform));
        splash.transform.SetParent(buttonsParent, false);
        TextMeshProUGUI tmp = splash.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.fontStyle = FontStyles.Bold;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rt = splash.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(700f, 200f);

        if (duration > 0f) Destroy(splash, duration);
        return splash;
    }

    /// <summary>
    /// Spawns a small label (non-interactive text) attached to buttonsParent.
    /// Returns the TextMeshProUGUI for further mutation. Safe to call before
    /// canvas is ready (returns null).
    /// </summary>
    protected TMP_Text SpawnLabel(Vector2 anchorPos, Vector2 size, string text, Color color, float fontSize = 32f)
    {
        if (buttonsParent == null) return null;

        GameObject lbl = new GameObject("Label", typeof(RectTransform));
        lbl.transform.SetParent(buttonsParent, false);
        TextMeshProUGUI tmp = lbl.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.color = color;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;

        RectTransform rt = lbl.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchorPos;
        rt.sizeDelta = size;

        return tmp;
    }
}
