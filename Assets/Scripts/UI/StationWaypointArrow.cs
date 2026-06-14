using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A HUD arrow that points toward the nearest station with an active task, with a
/// "ENGINE  14m" label, so the player wastes less time hunting for where to go.
/// Camera-relative: the arrow rotates to "turn left / right / ahead / behind".
/// Hidden when no task is active or while docked. Builds its own overlay canvas;
/// add to a GameObject in the mission scene.
/// </summary>
[DisallowMultipleComponent]
public class StationWaypointArrow : MonoBehaviour
{
    private RectTransform arrowRt;
    private Image arrowImg;
    private TMP_Text label;
    private GameObject root;

    private Transform player;
    private Camera cam;
    private TaskStation[] stations;
    private float nextRescan;

    private static Color Accent(string s)
    {
        switch (s)
        {
            case "EngineStation":      return new Color(1f, 0.30f, 0.30f);
            case "NavigationStation":  return new Color(0.30f, 0.62f, 1f);
            case "CommsStation":       return new Color(1f, 0.90f, 0.30f);
            case "LifeSupportStation": return new Color(0.30f, 1f, 0.45f);
        }
        return new Color(0.55f, 0.95f, 1f);
    }

    private void Start()
    {
        BuildUI();
        cam = Camera.main;
        RescanStations();
        SetVisible(false);
    }

    private void Update()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (cam == null) cam = Camera.main;
        if (Time.unscaledTime >= nextRescan) { RescanStations(); nextRescan = Time.unscaledTime + 2f; }

        // Hide while docked (you're already at a console) or with nothing to do.
        bool docked = StationDockController.Instance != null && StationDockController.Instance.IsDocked;
        if (player == null || cam == null || docked) { SetVisible(false); return; }

        TaskStation target = NearestActive();
        if (target == null) { SetVisible(false); return; }

        SetVisible(true);
        Vector3 to = target.transform.position - player.position; to.y = 0f;
        float dist = to.magnitude;
        Vector3 fwd = cam.transform.forward; fwd.y = 0f;
        float signed = Vector3.SignedAngle(fwd, to, Vector3.up); // +ve = target is to the right
        arrowRt.localEulerAngles = new Vector3(0f, 0f, -signed);

        Color c = Accent(target.stationName);
        arrowImg.color = c;
        label.text = TaskListHUD.PrettyStation(target.stationName).ToUpperInvariant() + "  " + Mathf.RoundToInt(dist) + "m";
        label.color = c;
    }

    private TaskStation NearestActive()
    {
        TaskStation best = null;
        float bestD = float.MaxValue;
        if (stations == null) return null;
        foreach (var s in stations)
        {
            if (s == null || !s.HasActiveTask()) continue;
            float d = (s.transform.position - player.position).sqrMagnitude;
            if (d < bestD) { bestD = d; best = s; }
        }
        return best;
    }

    private void RescanStations()
    {
        stations = FindObjectsByType<TaskStation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
    }

    private void SetVisible(bool v)
    {
        if (root != null && root.activeSelf != v) root.SetActive(v);
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("Waypoint_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95; // below assessor controls/HUD chrome, above scene
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root = new GameObject("WaypointRoot", typeof(RectTransform));
        root.transform.SetParent(canvasGO.transform, false);
        var rRt = (RectTransform)root.transform;
        rRt.anchorMin = rRt.anchorMax = new Vector2(0.5f, 0f);
        rRt.pivot = new Vector2(0.5f, 0f);
        rRt.anchoredPosition = new Vector2(0f, 150f);
        rRt.sizeDelta = new Vector2(220f, 110f);

        var arrowGO = new GameObject("Arrow", typeof(RectTransform), typeof(Image));
        arrowGO.transform.SetParent(root.transform, false);
        arrowRt = (RectTransform)arrowGO.transform;
        arrowRt.anchorMin = arrowRt.anchorMax = new Vector2(0.5f, 0f);
        arrowRt.pivot = new Vector2(0.5f, 0.5f);
        arrowRt.anchoredPosition = new Vector2(0f, 64f);
        arrowRt.sizeDelta = new Vector2(56f, 56f);
        arrowImg = arrowGO.GetComponent<Image>();
        arrowImg.sprite = BuildTriangleSprite();
        arrowImg.raycastTarget = false;

        var lblGO = new GameObject("Label", typeof(RectTransform));
        lblGO.transform.SetParent(root.transform, false);
        var lRt = (RectTransform)lblGO.transform;
        lRt.anchorMin = lRt.anchorMax = new Vector2(0.5f, 0f);
        lRt.pivot = new Vector2(0.5f, 0f);
        lRt.anchoredPosition = new Vector2(0f, 0f);
        lRt.sizeDelta = new Vector2(220f, 28f);
        label = lblGO.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.text = "";
    }

    // 64x64 upward-pointing solid triangle so we don't depend on any art asset.
    private static Sprite BuildTriangleSprite()
    {
        const int N = 64;
        var px = new Color[N * N];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(1, 1, 1, 0);
        for (int y = 0; y < N; y++)
        {
            // Width grows from apex (top) to base (bottom).
            float t = 1f - (y / (float)(N - 1)); // 0 at bottom, 1 at top
            int halfW = Mathf.RoundToInt((1f - t) * (N * 0.5f));
            int cx = N / 2;
            for (int x = cx - halfW; x <= cx + halfW; x++)
                if (x >= 0 && x < N) px[y * N + x] = Color.white;
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }
}
