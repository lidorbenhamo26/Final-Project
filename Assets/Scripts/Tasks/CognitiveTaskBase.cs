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
        if (DockTime < 0f) DockTime = Time.time;
        ShowCanvas();
        OnDocked();
    }

    public override void OnPlayerExit()
    {
        IsDocked = false;
        HideCanvas();
        OnUndocked();
    }

    /// <summary>Hook called the first time (and every time) the player docks.</summary>
    protected virtual void OnDocked() { }

    /// <summary>Hook called when the player un-docks. Task remains alive.</summary>
    protected virtual void OnUndocked() { }

    // ---------------------------------------------------------------- canvas

    private void BuildCanvas()
    {
        Transform parent = transform.parent != null ? transform.parent : transform;

        taskCanvasRoot = new GameObject("CognitiveCanvas");
        taskCanvasRoot.transform.SetParent(parent, false);

        // Position: 0.6m forward, 1.4m up from station origin.
        Vector3 pos = parent.position + parent.forward * 0.6f + parent.up * 1.4f;
        taskCanvasRoot.transform.position = pos;

        // Face away from the station's forward (so the canvas reads when
        // the player walks up to the station from the front).
        taskCanvasRoot.transform.rotation = Quaternion.LookRotation(parent.forward, parent.up);

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
        if (taskCanvasRoot != null) Destroy(taskCanvasRoot);
    }
}
