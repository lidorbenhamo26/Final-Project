using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Base class for the Life-Support Plan/Organize puzzles. Holds everything the
/// variants share — the dimmed screen-space overlay, the player lock/cursor +
/// PhysicsRaycaster suspend, the power-drain mirror strip, freeze-aware active
/// time, ESC/ValidWhile auto-close, metrics, and the solved/error plumbing — so
/// each variant only implements its own goal, content, and solve logic.
///
/// The owning <see cref="BatteryDeliveryTask"/> drives every variant through
/// <see cref="IPlanningPuzzle"/>, and rotates a different one each delivery so the
/// player plans rather than memorizes a single puzzle.
/// </summary>
public abstract class PlanningPuzzlePanel : MonoBehaviour, IPlanningPuzzle
{
    // Unified "a planning puzzle is open" check (used by docking + the dev driver),
    // independent of which variant is active.
    public static PlanningPuzzlePanel ActiveInstance { get; private set; }
    public static bool AnyOpen => ActiveInstance != null && ActiveInstance.isOpen;

    public Func<bool> ValidWhile { get; set; }
    public event Action OnSolved;
    public event Action<bool> OnClosed;
    /// <summary>Fired on each wrong action — lets the task log plan errors.</summary>
    public event Action OnError;

    public int ErrorCount { get; protected set; }
    public float ActiveTimeS { get; private set; }
    public int OpenCount { get; private set; }
    public int StepCount { get; protected set; }
    public bool IsOpen => isOpen;
    public bool Solved { get; private set; }

    protected const float PanelW = 1100f;
    protected const float PanelH = 640f;
    private const float PowerInnerWidth = 352f;

    protected static readonly Color PanelBgCol = new Color(0.04f, 0.06f, 0.09f, 0.96f);
    protected static readonly Color Accent     = new Color(0.55f, 0.95f, 1f,   1f);
    protected static readonly Color OkColor    = new Color(0.22f, 0.80f, 0.36f, 1f);
    protected static readonly Color ErrColor   = new Color(0.92f, 0.28f, 0.24f, 1f);
    protected static readonly Color IdleCol    = new Color(0.20f, 0.26f, 0.34f, 1f);
    protected static readonly Color ReadyCol   = new Color(0.16f, 0.33f, 0.45f, 1f);
    protected static readonly Color TextDim    = new Color(0.70f, 0.80f, 0.88f, 1f);

    protected RectTransform panelRT;
    private GameObject canvasGO;
    private TMP_Text headerText;
    private RectTransform powerFillRT;
    private Image powerFillImg;
    private TMP_Text powerLabel;

    private bool built, isOpen, solving, suppressClosedEvent;

    private AstronautController player;
    private ThirdPersonCamera tpCam;
    private PhysicsRaycaster suspendedRaycaster;

    // ---- variant hooks -----------------------------------------------------
    protected abstract string Title { get; }
    protected abstract string Instructions { get; }
    protected abstract string SolvedHeader { get; }
    /// <summary>Build the interactive content under <see cref="panelRT"/>.</summary>
    protected abstract void BuildContent();
    /// <summary>Variant cleanup when the panel hides (optional).</summary>
    protected virtual void OnHide() { }

    /// <summary>True when the variant may process input (open, not mid-solve, no
    /// assessor report on top).</summary>
    protected bool CanInteract =>
        isOpen && !solving &&
        !(AssessmentReportController.Instance != null && AssessmentReportController.Instance.IsVisible);

    // ---- lifecycle ---------------------------------------------------------
    private void Update()
    {
        if (!isOpen) return;
        if (!GameManager.IsDebugFrozen) ActiveTimeS += Time.deltaTime;
        if (solving) return;
        if (AssessmentReportController.Instance != null && AssessmentReportController.Instance.IsVisible) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Hide(true); return; }
        if (ValidWhile != null && !ValidWhile()) Hide(false);
    }

    private void OnDestroy()
    {
        if (isOpen) RestorePlayerControl();
        if (ActiveInstance == this) ActiveInstance = null;
    }

    public void Show()
    {
        if (isOpen || Solved) return;
        if (!built) BuildUi();
        ActiveInstance = this;
        isOpen = true;
        OpenCount++;
        canvasGO.SetActive(true);
        LockPlayerControl();
        AudioManager.Instance?.PlaySfx("wiring_open");
    }

    public void Hide(bool userInitiated)
    {
        if (!isOpen) return;
        OnHide();
        isOpen = false;
        if (canvasGO != null) canvasGO.SetActive(false);
        RestorePlayerControl();
        if (!Solved && !suppressClosedEvent) OnClosed?.Invoke(!userInitiated);
    }

    public void ForceClose()
    {
        if (!isOpen) return;
        suppressClosedEvent = true;
        Hide(false);
        suppressClosedEvent = false;
    }

    public void DebugAutoSolve()
    {
        if (Solved) return;
        Solved = true;
        if (isOpen) Hide(false);
        OnSolved?.Invoke();
    }

    public void SetPower(float frac01)
    {
        if (!built) return;
        frac01 = Mathf.Clamp01(frac01);
        if (powerFillRT != null)
            powerFillRT.sizeDelta = new Vector2(PowerInnerWidth * frac01, powerFillRT.sizeDelta.y);
        if (powerFillImg != null)
            powerFillImg.color = frac01 > 0.5f ? new Color(0.3f, 1f, 0.5f)
                : frac01 > 0.25f ? new Color(1f, 0.8f, 0.2f) : new Color(1f, 0.32f, 0.26f);
        if (powerLabel != null) powerLabel.text = "POWER  " + Mathf.CeilToInt(frac01 * 100f) + "%";
    }

    // ---- variant-facing helpers -------------------------------------------

    /// <summary>Variant signals all steps complete.</summary>
    protected void Complete()
    {
        if (Solved) return;
        Solved = true;
        StartCoroutine(CoSolved());
    }

    /// <summary>Variant signals a wrong action (telemetry + buzz).</summary>
    protected void RegisterError()
    {
        ErrorCount++;
        AudioManager.Instance?.PlaySfx("wire_error");
        OnError?.Invoke();
    }

    protected IEnumerator FlashColor(Image img, Color flash)
    {
        if (img == null) yield break;
        Color baseColor = img.color;
        float t = 0f;
        while (t < 0.35f && img != null)
        {
            t += Time.deltaTime;
            img.color = Color.Lerp(baseColor, flash, Mathf.PingPong(t * 2f / 0.35f, 1f));
            yield return null;
        }
        if (img != null) img.color = baseColor;
    }

    private IEnumerator CoSolved()
    {
        solving = true;
        if (headerText != null) { headerText.text = SolvedHeader; headerText.color = OkColor; }
        AudioManager.Instance?.PlaySfx("power_restore");
        yield return new WaitForSeconds(0.75f);
        solving = false;
        Hide(false); // Solved == true, so Hide won't fire OnClosed
        OnSolved?.Invoke();
    }

    // ---- shell build -------------------------------------------------------

    private void BuildUi()
    {
        built = true;

        canvasGO = new GameObject(GetType().Name + "Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300; // above HUD, below assessor report (1000)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var dim = MakeImage(canvasGO.transform, "Dim", new Color(0f, 0f, 0f, 0.62f), true);
        var dimRT = (RectTransform)dim.transform;
        dimRT.anchorMin = Vector2.zero; dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero; dimRT.offsetMax = Vector2.zero;

        var panelImg = MakeImage(canvasGO.transform, "Panel", PanelBgCol, true);
        panelRT = (RectTransform)panelImg.transform;
        panelRT.sizeDelta = new Vector2(PanelW, PanelH);
        var outline = panelImg.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.85f, 1f, 0.9f);
        outline.effectDistance = new Vector2(3f, 3f);

        headerText = MakeLabel(panelRT, "Header", Title, 34f, Accent,
            new Vector2(0f, PanelH * 0.5f - 36f), new Vector2(PanelW - 40f, 44f));
        headerText.fontStyle = FontStyles.Bold;

        MakeLabel(panelRT, "Sub", Instructions, 18f, TextDim,
            new Vector2(0f, PanelH * 0.5f - 74f), new Vector2(PanelW - 60f, 26f));

        BuildPowerStrip();
        BuildContent();

        canvasGO.SetActive(false);
    }

    private void BuildPowerStrip()
    {
        var bg = MakeImage(panelRT, "PowerBg", new Color(0.1f, 0.12f, 0.16f, 1f), false);
        var bgRT = (RectTransform)bg.transform;
        bgRT.anchoredPosition = new Vector2(0f, PanelH * 0.5f - 108f);
        bgRT.sizeDelta = new Vector2(360f, 18f);
        var fill = MakeImage(bgRT, "PowerFill", new Color(0.3f, 1f, 0.5f), false);
        powerFillImg = fill;
        powerFillRT = (RectTransform)fill.transform;
        powerFillRT.anchorMin = powerFillRT.anchorMax = new Vector2(0f, 0.5f);
        powerFillRT.pivot = new Vector2(0f, 0.5f);
        powerFillRT.anchoredPosition = new Vector2(4f, 0f);
        powerFillRT.sizeDelta = new Vector2(PowerInnerWidth, 12f);
        powerLabel = MakeLabel(bgRT, "PowerLabel", "POWER  100%", 13f,
            new Color(0.95f, 0.98f, 1f), Vector2.zero, new Vector2(360f, 18f));
        powerLabel.fontStyle = FontStyles.Bold;
        var o = powerLabel.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0f, 0f, 0f, 0.85f); o.effectDistance = new Vector2(1.1f, 1.1f);
    }

    // ---- shared UI factory -------------------------------------------------

    protected static Image MakeImage(Transform parent, string name, Color color, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        return img;
    }

    protected static TMP_Text MakeLabel(Transform parent, string name, string text, float fontSize,
        Color color, Vector2 anchoredPos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return label;
    }

    protected Button MakeButton(Transform parent, string name, Vector2 pos, Vector2 size,
        Color color, UnityEngine.Events.UnityAction onClick)
    {
        var img = MakeImage(parent, name, color, true);
        var rt = (RectTransform)img.transform;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var btn = img.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        if (onClick != null) btn.onClick.AddListener(onClick);
        return btn;
    }

    protected static Image ButtonImage(Button b) => (Image)b.targetGraphic;

    protected static void Shuffle<T>(IList<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // ---- player lock (shared with the dock controller pattern) -------------

    private void LockPlayerControl()
    {
        if (player == null) player = FindAnyObjectByType<AstronautController>();
        if (tpCam == null) tpCam = FindAnyObjectByType<ThirdPersonCamera>();
        if (player != null) player.ControlsEnabled = false;
        if (tpCam != null) tpCam.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Mouse.current != null)
            Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        suspendedRaycaster = null;
        Camera cam = Camera.main;
        if (cam != null)
        {
            var pr = cam.GetComponent<PhysicsRaycaster>();
            if (pr != null && pr.enabled) { pr.enabled = false; suspendedRaycaster = pr; }
        }
        EnsureEventSystem();
    }

    private void RestorePlayerControl()
    {
        if (player != null) player.ControlsEnabled = true;
        if (tpCam != null) tpCam.enabled = true;
        if (suspendedRaycaster != null) { suspendedRaycaster.enabled = true; suspendedRaycaster = null; }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            var es = EventSystem.current;
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            return;
        }
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }
}
