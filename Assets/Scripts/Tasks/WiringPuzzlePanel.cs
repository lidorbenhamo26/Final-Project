using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Among-Us-style wire matching overlay for the battery delivery mission.
/// Pressing E at the Life Support socket opens this panel instead of
/// instant-installing the cell: colored wire stubs on the left, the same
/// colors shuffled as sockets on the right. The player drags each wire
/// handle to its matching socket; wrong-color drops buzz, flash the socket
/// red and count as errors (a Plan/Organize metric). When every wire is
/// routed the panel closes and fires OnSolved so the task completes the
/// physical install.
///
/// While open the player is frozen and the cursor freed, mirroring
/// StationDockController's dock lock. ESC closes (progress kept, E re-opens);
/// ValidWhile lets the owning task auto-close it (cell knocked loose, task
/// over). The life-support drain keeps running — SetPower mirrors the HUD bar
/// inside the panel since the dim layer covers the screen-space HUD.
/// </summary>
public class WiringPuzzlePanel : MonoBehaviour
{
    public static WiringPuzzlePanel ActiveInstance { get; private set; }
    public static bool IsOpen => ActiveInstance != null && ActiveInstance.isOpen;

    [Range(2, 5)] public int WireCount = 4;

    /// <summary>Polled while open; returning false auto-closes the panel.</summary>
    public Func<bool> ValidWhile;

    /// <summary>All wires routed correctly (fired after the panel hides).</summary>
    public event Action OnSolved;
    /// <summary>(wireColorIndex, socketColorIndex) for a wrong-color drop.</summary>
    public event Action<int, int> OnWireError;
    /// <summary>Closed without solving; true = auto-closed, false = ESC.</summary>
    public event Action<bool> OnClosed;

    public int ErrorCount { get; private set; }
    public float ActiveTimeS { get; private set; }
    public int OpenCount { get; private set; }
    public bool Solved { get; private set; }

    // Same palette as CodeMemoryTask so color language stays consistent.
    private static readonly Color[] Palette =
    {
        new Color(0.90f, 0.20f, 0.20f), // red
        new Color(0.95f, 0.85f, 0.15f), // yellow
        new Color(0.20f, 0.45f, 0.95f), // blue
        new Color(0.25f, 0.80f, 0.35f), // green
        new Color(0.80f, 0.25f, 0.80f), // magenta (5-wire variant)
    };

    private const float PanelW = 1100f;
    private const float PanelH = 640f;
    private const float RowSpacing = 110f;
    private const float StubX = -440f;
    private const float HandleHomeX = -370f;
    private const float SocketX = 440f;
    private const float SnapRadiusPx = 70f;
    private const float LineThickness = 10f;
    private const float PowerInnerWidth = 352f;

    private bool built;
    private bool isOpen;
    private bool solving;
    private bool suppressClosedEvent;

    private GameObject canvasGO;
    private RectTransform panelRT;
    private TMP_Text headerText;
    private Color headerBaseColor;
    private TMP_Text subtitleText;
    private RectTransform powerFillRT;
    private Image powerFillImg;
    private TMP_Text powerLabel;

    private int[] rightOrder;        // socket row j -> wire color index it accepts
    private bool[] connected;        // per wire color index
    private Vector2[] handleHome;    // per wire color index
    private Vector2[] socketPos;     // per socket row j
    private RectTransform[] handles;
    private Image[] handleImages;
    private RectTransform[] lines;
    private Image[] socketImages;
    private Color[] socketBaseColors;
    private int draggingIndex = -1;

    private AstronautController player;
    private ThirdPersonCamera tpCam;
    private PhysicsRaycaster suspendedRaycaster;

    private void Update()
    {
        if (!isOpen) return;

        // Freeze-aware active time: F11 / the assessor report pause the clock.
        if (!GameManager.IsDebugFrozen) ActiveTimeS += Time.deltaTime;

        if (solving) return;

        // The assessor report overlay (F12) sits above us and owns input.
        if (AssessmentReportController.Instance != null && AssessmentReportController.Instance.IsVisible)
            return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Hide(userInitiated: true);
            return;
        }

        if (ValidWhile != null && !ValidWhile())
            Hide(userInitiated: false);
    }

    private void OnDestroy()
    {
        // Never strand a frozen player with a free cursor if the task tears
        // the panel down mid-puzzle.
        if (isOpen) RestorePlayerControl();
        if (ActiveInstance == this) ActiveInstance = null;
    }

    /// <summary>Opens the panel (builds the UI on first call). No-op while open.</summary>
    public void Show()
    {
        if (isOpen || Solved) return;
        if (!built) BuildUi();

        ActiveInstance = this;
        isOpen = true;
        OpenCount++;
        canvasGO.SetActive(true);
        LockPlayerControl();
        AudioManager.Instance.PlaySfx("wiring_open");
    }

    /// <summary>Closes the panel, restoring the player. Wire progress is kept.</summary>
    public void Hide(bool userInitiated)
    {
        if (!isOpen) return;

        CancelDrag();
        isOpen = false;
        canvasGO.SetActive(false);
        RestorePlayerControl();

        if (!Solved && !suppressClosedEvent)
            OnClosed?.Invoke(!userInitiated);
    }

    /// <summary>Hide without firing OnClosed — for task expiry/teardown paths.</summary>
    public void ForceClose()
    {
        if (!isOpen) return;
        suppressClosedEvent = true;
        Hide(userInitiated: false);
        suppressClosedEvent = false;
    }

    /// <summary>Mirrors the HUD life-support bar inside the panel.</summary>
    public void SetPower(float frac01)
    {
        if (!built) return;
        frac01 = Mathf.Clamp01(frac01);
        if (powerFillRT != null)
            powerFillRT.sizeDelta = new Vector2(PowerInnerWidth * frac01, powerFillRT.sizeDelta.y);
        if (powerFillImg != null)
        {
            powerFillImg.color = frac01 > 0.5f ? new Color(0.3f, 1f, 0.5f)
                : frac01 > 0.25f ? new Color(1f, 0.8f, 0.2f)
                : new Color(1f, 0.32f, 0.26f);
        }
        if (powerLabel != null)
            powerLabel.text = "POWER  " + Mathf.CeilToInt(frac01 * 100f) + "%";
    }

    // ------------------------------------------------------------------ drag

    public void BeginWireDrag(int wireIndex, PointerEventData eventData)
    {
        if (!isOpen || solving || connected[wireIndex] || draggingIndex >= 0) return;
        draggingIndex = wireIndex;
        AudioManager.Instance.PlaySfx("wire_grab");
        DragWire(wireIndex, eventData);
    }

    public void DragWire(int wireIndex, PointerEventData eventData)
    {
        if (draggingIndex != wireIndex) return;
        Vector2 lp = PointerToPanel(eventData);
        handles[wireIndex].anchoredPosition = lp;
        UpdateLine(wireIndex, lp);
    }

    public void EndWireDrag(int wireIndex, PointerEventData eventData)
    {
        if (draggingIndex != wireIndex) return;
        draggingIndex = -1;

        Vector2 lp = PointerToPanel(eventData);
        int socketRow = FindSocketNear(lp);
        if (socketRow < 0)
        {
            SnapBack(wireIndex); // released over nothing — not an error
            return;
        }

        if (rightOrder[socketRow] == wireIndex)
        {
            ConnectWire(wireIndex, socketRow);
        }
        else
        {
            ErrorCount++;
            SnapBack(wireIndex);
            AudioManager.Instance.PlaySfx("wire_error");
            StartCoroutine(CoFlashSocket(socketRow));
            OnWireError?.Invoke(wireIndex, rightOrder[socketRow]);
        }
    }

    private void ConnectWire(int wireIndex, int socketRow)
    {
        connected[wireIndex] = true;
        handles[wireIndex].anchoredPosition = socketPos[socketRow];
        handleImages[wireIndex].raycastTarget = false;
        UpdateLine(wireIndex, socketPos[socketRow]);
        AudioManager.Instance.PlaySfx("wire_connect");

        for (int i = 0; i < WireCount; i++)
            if (!connected[i]) return;

        Solved = true;
        StartCoroutine(CoSolved());
    }

    private IEnumerator CoSolved()
    {
        solving = true;
        headerText.text = "POWER ROUTED";
        headerText.color = new Color(0.35f, 1f, 0.5f);
        subtitleText.text = "ALL WIRES CONNECTED";
        yield return new WaitForSeconds(0.75f);
        solving = false;
        Hide(userInitiated: false); // Solved == true, so no OnClosed
        OnSolved?.Invoke();
    }

    private IEnumerator CoFlashSocket(int socketRow)
    {
        Image img = socketImages[socketRow];
        Color baseColor = socketBaseColors[socketRow];
        Color errColor = new Color(1f, 0.3f, 0.25f);
        float t = 0f;
        const float duration = 0.35f;
        while (t < duration && img != null)
        {
            t += Time.deltaTime;
            float k = Mathf.PingPong(t * 2f / duration, 1f);
            img.color = Color.Lerp(baseColor, errColor, k);
            yield return null;
        }
        if (img != null) img.color = baseColor;
    }

    private void CancelDrag()
    {
        if (draggingIndex < 0) return;
        SnapBack(draggingIndex); // silent — ESC mid-drag is not an error
        draggingIndex = -1;
    }

    private void SnapBack(int wireIndex)
    {
        handles[wireIndex].anchoredPosition = handleHome[wireIndex];
        lines[wireIndex].sizeDelta = new Vector2(0f, LineThickness);
    }

    private void UpdateLine(int wireIndex, Vector2 end)
    {
        Vector2 d = end - handleHome[wireIndex];
        lines[wireIndex].sizeDelta = new Vector2(d.magnitude, LineThickness);
        lines[wireIndex].localEulerAngles = new Vector3(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    private Vector2 PointerToPanel(PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            panelRT, eventData.position, null, out Vector2 lp); // null camera: ScreenSpaceOverlay
        lp.x = Mathf.Clamp(lp.x, -PanelW * 0.5f + 15f, PanelW * 0.5f - 15f);
        lp.y = Mathf.Clamp(lp.y, -PanelH * 0.5f + 15f, PanelH * 0.5f - 15f);
        return lp;
    }

    private int FindSocketNear(Vector2 panelPoint)
    {
        int best = -1;
        float bestSqr = SnapRadiusPx * SnapRadiusPx;
        for (int j = 0; j < WireCount; j++)
        {
            float sqr = (socketPos[j] - panelPoint).sqrMagnitude;
            if (sqr <= bestSqr)
            {
                bestSqr = sqr;
                best = j;
            }
        }
        return best;
    }

    // ------------------------------------------------------- player lock

    private void LockPlayerControl()
    {
        if (player == null) player = FindAnyObjectByType<AstronautController>();
        if (tpCam == null) tpCam = FindAnyObjectByType<ThirdPersonCamera>();

        if (player != null) player.ControlsEnabled = false;
        // Disabling the third-person camera stops mouse-look and its ESC
        // cursor toggle (both live in its LateUpdate), same as docking.
        if (tpCam != null) tpCam.enabled = false;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (Mouse.current != null)
            Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));

        // The scene PhysicsRaycaster would let clicks hit 3D ClickTargets
        // through the panel; suspend it like StationDockController does.
        suspendedRaycaster = null;
        Camera cam = Camera.main;
        if (cam != null)
        {
            var pr = cam.GetComponent<PhysicsRaycaster>();
            if (pr != null && pr.enabled)
            {
                pr.enabled = false;
                suspendedRaycaster = pr;
            }
        }

        EnsureEventSystem();
    }

    private void RestorePlayerControl()
    {
        if (player != null) player.ControlsEnabled = true;
        if (tpCam != null) tpCam.enabled = true;
        if (suspendedRaycaster != null)
        {
            suspendedRaycaster.enabled = true;
            suspendedRaycaster = null;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    // The project runs the new Input System only (activeInputHandler: 1), so a
    // StandaloneInputModule would silently dead-end UI input — always make
    // sure the EventSystem drives uGUI through InputSystemUIInputModule.
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

    // ------------------------------------------------------------ build

    private void BuildUi()
    {
        built = true;
        WireCount = Mathf.Clamp(WireCount, 2, Palette.Length);

        connected = new bool[WireCount];
        handleHome = new Vector2[WireCount];
        socketPos = new Vector2[WireCount];
        handles = new RectTransform[WireCount];
        handleImages = new Image[WireCount];
        lines = new RectTransform[WireCount];
        socketImages = new Image[WireCount];
        socketBaseColors = new Color[WireCount];
        rightOrder = ShuffledOrder(WireCount);

        canvasGO = new GameObject("WiringPuzzleCanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300; // above HUD (100) / battery HUD (96), below assessor report (1000)
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        canvasGO.AddComponent<GraphicRaycaster>();

        // Full-screen dim layer; raycast target so stray clicks go nowhere.
        var dim = MakeImage(canvasGO.transform, "Dim", new Color(0f, 0f, 0f, 0.62f), true);
        var dimRT = (RectTransform)dim.transform;
        dimRT.anchorMin = Vector2.zero;
        dimRT.anchorMax = Vector2.one;
        dimRT.offsetMin = Vector2.zero;
        dimRT.offsetMax = Vector2.zero;

        var panelImg = MakeImage(canvasGO.transform, "Panel", new Color(0.04f, 0.06f, 0.09f, 0.96f), true);
        panelRT = (RectTransform)panelImg.transform;
        panelRT.sizeDelta = new Vector2(PanelW, PanelH);
        var outline = panelImg.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.85f, 1f, 0.9f);
        outline.effectDistance = new Vector2(3f, 3f);

        headerText = MakeLabel(panelRT, "Header", "LIFE SUPPORT - WIRE PANEL", 34f,
            new Color(0.55f, 0.95f, 1f), new Vector2(0f, PanelH * 0.5f - 36f), new Vector2(PanelW - 40f, 44f));
        headerText.fontStyle = FontStyles.Bold;
        headerBaseColor = headerText.color;

        subtitleText = MakeLabel(panelRT, "Subtitle", "DRAG EACH WIRE TO ITS MATCHING SOCKET   [ESC] CLOSE", 18f,
            new Color(0.65f, 0.78f, 0.85f), new Vector2(0f, PanelH * 0.5f - 72f), new Vector2(PanelW - 40f, 26f));

        BuildPowerStrip();

        // Row layout, top to bottom, centered on the panel.
        float topY = (WireCount - 1) * RowSpacing * 0.5f - 24f; // nudged down clear of the header block
        var stubSize = new Vector2(110f, 50f);

        // Left stubs + right sockets first so wires render above them...
        for (int i = 0; i < WireCount; i++)
        {
            float y = topY - i * RowSpacing;
            handleHome[i] = new Vector2(HandleHomeX, y);

            var stub = MakeImage(panelRT, "Stub_" + i, Palette[i], false);
            var stubRT = (RectTransform)stub.transform;
            stubRT.anchoredPosition = new Vector2(StubX, y);
            stubRT.sizeDelta = stubSize;

            socketPos[i] = new Vector2(SocketX, topY - i * RowSpacing);
            int colorIdx = rightOrder[i];
            var socket = MakeImage(panelRT, "Socket_" + i, Palette[colorIdx], false);
            var socketRT = (RectTransform)socket.transform;
            socketRT.anchoredPosition = socketPos[i];
            socketRT.sizeDelta = stubSize;
            socketImages[i] = socket;
            socketBaseColors[i] = socket.color;

            var hole = MakeImage(socketRT, "Hole", Palette[colorIdx] * 0.35f, false);
            var holeRT = (RectTransform)hole.transform;
            holeRT.sizeDelta = new Vector2(44f, 28f);
            holeRT.anchoredPosition = new Vector2(-22f, 0f);
        }

        // ...then the wire lines, then the handles on top of everything.
        var wiresGO = new GameObject("Wires", typeof(RectTransform));
        wiresGO.transform.SetParent(panelRT, false);
        var wiresRT = (RectTransform)wiresGO.transform;
        wiresRT.anchorMin = wiresRT.anchorMax = new Vector2(0.5f, 0.5f);
        wiresRT.sizeDelta = Vector2.zero;

        for (int i = 0; i < WireCount; i++)
        {
            var line = MakeImage(wiresRT, "Wire_" + i, Palette[i], false);
            lines[i] = (RectTransform)line.transform;
            lines[i].pivot = new Vector2(0f, 0.5f);
            lines[i].anchoredPosition = handleHome[i];
            lines[i].sizeDelta = new Vector2(0f, LineThickness);
        }

        for (int i = 0; i < WireCount; i++)
        {
            var handle = MakeImage(panelRT, "Handle_" + i, Color.Lerp(Palette[i], Color.white, 0.3f), true);
            handles[i] = (RectTransform)handle.transform;
            handles[i].anchoredPosition = handleHome[i];
            handles[i].sizeDelta = new Vector2(44f, 44f);
            handleImages[i] = handle;

            var grab = handle.gameObject.AddComponent<WiringWireHandle>();
            grab.panel = this;
            grab.wireIndex = i;
        }

        canvasGO.SetActive(false);
    }

    private void BuildPowerStrip()
    {
        var bg = MakeImage(panelRT, "PowerBg", new Color(0.1f, 0.12f, 0.16f, 1f), false);
        var bgRT = (RectTransform)bg.transform;
        bgRT.anchoredPosition = new Vector2(0f, PanelH * 0.5f - 102f);
        bgRT.sizeDelta = new Vector2(360f, 18f);

        var fill = MakeImage(bgRT, "PowerFill", new Color(0.3f, 1f, 0.5f), false);
        powerFillImg = fill;
        powerFillRT = (RectTransform)fill.transform;
        powerFillRT.anchorMin = new Vector2(0f, 0.5f);
        powerFillRT.anchorMax = new Vector2(0f, 0.5f);
        powerFillRT.pivot = new Vector2(0f, 0.5f);
        powerFillRT.anchoredPosition = new Vector2(4f, 0f);
        powerFillRT.sizeDelta = new Vector2(PowerInnerWidth, 12f);

        powerLabel = MakeLabel(bgRT, "PowerLabel", "POWER  100%", 13f,
            new Color(0.95f, 0.98f, 1f), Vector2.zero, new Vector2(360f, 18f));
        powerLabel.fontStyle = FontStyles.Bold;
        var lblOutline = powerLabel.gameObject.AddComponent<Outline>();
        lblOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        lblOutline.effectDistance = new Vector2(1.1f, 1.1f);
    }

    private static Image MakeImage(Transform parent, string name, Color color, bool raycast)
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

    private static TMP_Text MakeLabel(Transform parent, string name, string text, float fontSize,
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
        label.textWrappingMode = TextWrappingModes.NoWrap;
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
        return label;
    }

    private static int[] ShuffledOrder(int n)
    {
        var order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;

        bool identity = true;
        while (identity)
        {
            for (int i = n - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (order[i], order[j]) = (order[j], order[i]);
            }
            identity = true;
            for (int i = 0; i < n; i++)
                if (order[i] != i) { identity = false; break; }
        }
        return order;
    }
}

/// <summary>
/// Runtime-attached drag relay for one wire handle; forwards uGUI drag events
/// to the owning WiringPuzzlePanel.
/// </summary>
public class WiringWireHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [NonSerialized] public WiringPuzzlePanel panel;
    [NonSerialized] public int wireIndex;

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (panel != null) panel.BeginWireDrag(wireIndex, eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (panel != null) panel.DragWire(wireIndex, eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (panel != null) panel.EndWireDrag(wireIndex, eventData);
    }
}
