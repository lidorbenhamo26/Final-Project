using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class TaskListHUD : MonoBehaviour
{
    private struct Row
    {
        public RectTransform Root;
        public Image Pill;
        public Image Icon;
        public TextMeshProUGUI StationLabel;
        public TextMeshProUGUI ResultLabel;
        public Image TimeBar;
    }

    private static readonly Color ColorIdle      = new Color(0.612f, 0.639f, 0.686f, 1f); // #9CA3AF
    private static readonly Color ColorActive    = new Color(0.133f, 0.773f, 0.369f, 1f); // #22C55E
    private static readonly Color ColorUrgent    = new Color(0.937f, 0.267f, 0.267f, 1f); // #EF4444
    private static readonly Color ColorPanelBg   = new Color(0.04f, 0.06f, 0.09f, 0.55f);
    private static readonly Color ColorRowBg     = new Color(0.08f, 0.11f, 0.16f, 0.7f);
    private static readonly Color ColorTextPri   = new Color(0.95f, 0.97f, 1f, 1f);
    private static readonly Color ColorTextSec   = new Color(0.78f, 0.82f, 0.88f, 0.95f);

    private const float ROW_W      = 300f;
    private const float ACTIVE_H   = 72f;
    private const float RECENT_H   = 56f;
    private const float HEADER_GAP = 8f;
    private const float HEADER_H   = 20f;
    private const float ROW_GAP    = 4f;

    private Row activeRow;
    private Row[] recentRows = new Row[2];
    private MissionTask currentTask;
    private Dictionary<string, Sprite> iconByStation;
    private Sprite fallbackIcon;
    private Sprite panelFrameSprite;
    private Image activeFrameImg;
    private bool _urgencyActive;
    private bool _hasRecent;

    private void Awake()
    {
        if (GetComponent<RectTransform>() == null) gameObject.AddComponent<RectTransform>();

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(transform, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = ColorPanelBg;

        panelFrameSprite = Resources.Load<Sprite>("UI/panel_frame_corners");

        LoadIcons();
        BuildActiveRow();
        BuildRecentHeader();
        BuildRecentRows();
        ClearActiveRow();
        for (int i = 0; i < recentRows.Length; i++) ClearRecentRow(i);
        ShowEmptyRecentState();
    }

    private void OnEnable()
    {
        MissionTask.OnTaskSpawned += HandleSpawn;
        MissionTask.OnTaskResolved += HandleResolved;
    }

    private void OnDisable()
    {
        MissionTask.OnTaskSpawned -= HandleSpawn;
        MissionTask.OnTaskResolved -= HandleResolved;
    }

    private void LoadIcons()
    {
        fallbackIcon = BuildFallbackSprite();
        iconByStation = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        iconByStation["EngineStation"]      = LoadOr("working_memory", BuildMemoryIcon);
        iconByStation["NavigationStation"]  = LoadOr("pattern_match",  BuildGridIcon);
        iconByStation["CommsStation"]       = LoadOr("stroop",         BuildStroopIcon);
        iconByStation["LifeSupportStation"] = LoadOr("nback",          BuildLayersIcon);
    }

    private Sprite LoadOr(string key, Func<Sprite> builder)
    {
        var s = Resources.Load<Sprite>("TaskIcons/" + key);
        return s != null ? s : builder();
    }

    private Sprite BuildFallbackSprite()
    {
        return MakeSprite(8, FillSolid(8, new Color(0.6f, 0.65f, 0.72f, 1f)));
    }

    private Sprite BuildMemoryIcon()
    {
        const int N = 64;
        var px = FillClear(N);
        Color cyan       = new Color(0.157f, 0.808f, 0.957f, 1f);
        Color cyanBright = new Color(0.55f, 0.95f, 1f, 1f);
        float cx = N * 0.5f, cy = N * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float dx = x - cx, dy = y - cy;
            float r = Mathf.Sqrt(dx * dx + dy * dy);
            if (r <= 25f)
            {
                float wave = Mathf.Sin(x * 0.55f + Mathf.Sin(y * 0.3f) * 2f);
                if (wave > 0.4f) px[y * N + x] = cyanBright;
                else px[y * N + x] = Color.Lerp(cyan, cyanBright, Mathf.Clamp01(1f - r / 25f));
            }
            else if (r <= 27.5f)
            {
                float t = (r - 25f) / 2.5f;
                px[y * N + x] = new Color(cyan.r, cyan.g, cyan.b, 1f - t);
            }
        }
        return MakeSprite(N, px);
    }

    private Sprite BuildGridIcon()
    {
        const int N = 64;
        var px = FillClear(N);
        Color tile = new Color(0.231f, 0.604f, 0.957f, 1f);
        Color hot  = new Color(0.45f, 0.85f, 1f, 1f);
        int tileSize = 14, gap = 4;
        int side = 3 * tileSize + 2 * gap;
        int start = (N - side) / 2;
        for (int gy = 0; gy < 3; gy++)
        for (int gx = 0; gx < 3; gx++)
        {
            int x0 = start + gx * (tileSize + gap);
            int y0 = start + gy * (tileSize + gap);
            Color c = ((gx == 0 && gy == 2) || (gx == 2 && gy == 0)) ? hot : tile;
            for (int dy = 0; dy < tileSize; dy++)
            for (int dx = 0; dx < tileSize; dx++)
                px[(y0 + dy) * N + (x0 + dx)] = c;
        }
        return MakeSprite(N, px);
    }

    private Sprite BuildStroopIcon()
    {
        const int N = 64;
        var px = FillClear(N);
        Color magenta = new Color(0.957f, 0.286f, 0.745f, 1f);
        Color cyan    = new Color(0.157f, 0.808f, 0.957f, 1f);
        int barW = 48, barH = 18, gap = 4;
        int x0 = (N - barW) / 2;
        int yTop = N / 2 + gap / 2;
        int yBot = N / 2 - gap / 2 - barH;
        for (int dy = 0; dy < barH; dy++)
        for (int dx = 0; dx < barW; dx++)
        {
            int corner = Mathf.Min(Mathf.Min(dx, barW - 1 - dx), Mathf.Min(dy, barH - 1 - dy));
            if (corner < 1) continue;
            px[(yTop + dy) * N + (x0 + dx)] = magenta;
            px[(yBot + dy) * N + (x0 + dx)] = cyan;
        }
        int boxW = 14, boxH = 8;
        int bx = x0 + 6;
        for (int dy = 0; dy < boxH; dy++)
        for (int dx = 0; dx < boxW; dx++)
        {
            px[(yTop + 5 + dy) * N + (bx + dx)] = cyan;
            px[(yBot + 5 + dy) * N + (bx + dx + barW - boxW - 12)] = magenta;
        }
        return MakeSprite(N, px);
    }

    private Sprite BuildLayersIcon()
    {
        const int N = 64;
        var px = FillClear(N);
        Color a1    = new Color(0.957f, 0.671f, 0.196f, 0.6f);
        Color a2    = new Color(0.957f, 0.671f, 0.196f, 0.85f);
        Color a3    = new Color(1f, 0.85f, 0.4f, 1f);
        Color arrow = new Color(1f, 0.93f, 0.65f, 1f);
        int barW = 46, barH = 10, gap = 5;
        int x0 = (N - barW) / 2;
        int baseY = 8;
        Color[] cols = { a1, a2, a3 };
        for (int c = 0; c < 3; c++)
        {
            int y0 = baseY + c * (barH + gap);
            for (int dy = 0; dy < barH; dy++)
            for (int dx = 0; dx < barW; dx++)
            {
                int corner = Mathf.Min(Mathf.Min(dx, barW - 1 - dx), Mathf.Min(dy, barH - 1 - dy));
                if (corner < 1) continue;
                px[(y0 + dy) * N + (x0 + dx)] = cols[c];
            }
        }
        int ax = N - 14, ay = N - 14;
        for (int i = 0; i < 8; i++)
        {
            int xx = ax - i;
            int yy = ay - i;
            if (xx >= 0 && yy >= 0)       px[yy * N + xx] = arrow;
            if (xx >= 0 && yy + 1 < N)   px[(yy + 1) * N + xx] = arrow;
        }
        return MakeSprite(N, px);
    }

    private static Color[] FillClear(int n)
    {
        var px = new Color[n * n];
        Color c = new Color(0, 0, 0, 0);
        for (int i = 0; i < px.Length; i++) px[i] = c;
        return px;
    }

    private static Color[] FillSolid(int n, Color c)
    {
        var px = new Color[n * n];
        for (int i = 0; i < px.Length; i++) px[i] = c;
        return px;
    }

    private static Sprite MakeSprite(int n, Color[] px)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f);
    }

    private void BuildActiveRow()
    {
        activeRow = BuildRow("ActiveRow", 0f, ACTIVE_H, 64f, 20, 16);

        // Corner-bracket frame overlay (sci-fi cockpit feel). If the Meshy sprite is
        // present at Resources/UI/panel_frame_corners we use it as a 9-sliced image;
        // otherwise fall back to a thin outline rectangle so the row still reads
        // as a contained panel.
        var frame = new GameObject("Frame", typeof(RectTransform), typeof(Image));
        frame.transform.SetParent(activeRow.Root, false);
        var frt = (RectTransform)frame.transform;
        frt.anchorMin = Vector2.zero; frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(2f, 2f); frt.offsetMax = new Vector2(-2f, -2f);
        activeFrameImg = frame.GetComponent<Image>();
        activeFrameImg.raycastTarget = false;
        if (panelFrameSprite != null)
        {
            activeFrameImg.sprite = panelFrameSprite;
            activeFrameImg.type = Image.Type.Sliced;
            activeFrameImg.pixelsPerUnitMultiplier = 1f;
        }
        else
        {
            activeFrameImg.sprite = BuildHollowRectSprite();
            activeFrameImg.type = Image.Type.Sliced;
            activeFrameImg.pixelsPerUnitMultiplier = 1f;
        }
        activeFrameImg.color = new Color(ColorIdle.r, ColorIdle.g, ColorIdle.b, 0.4f);

        var bar = new GameObject("TimeBar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(activeRow.Root, false);
        var brt = (RectTransform)bar.transform;
        brt.anchorMin = new Vector2(0f, 0f); brt.anchorMax = new Vector2(1f, 0f);
        brt.pivot = new Vector2(0f, 0f);
        brt.anchoredPosition = new Vector2(8f, 6f);
        brt.sizeDelta = new Vector2(-16f, 4f);
        var img = bar.GetComponent<Image>();
        img.color = ColorActive;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        activeRow.TimeBar = img;
    }

    // Procedural fallback when panel_frame_corners sprite is missing: a 32x32
    // hollow rect with a 4px cyan border, sliced 9-ways so it stretches cleanly.
    private static Sprite BuildHollowRectSprite()
    {
        const int N = 32;
        const int border = 4;
        var px = new Color[N * N];
        var clear = new Color(0f, 0f, 0f, 0f);
        var stroke = new Color(0.55f, 0.95f, 1f, 1f);
        for (int i = 0; i < px.Length; i++) px[i] = clear;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            bool onEdge = x < border || x >= N - border || y < border || y >= N - border;
            // Carve out the center so only the border + corners draw
            bool inCorner =
                (x < border * 2 && y < border * 2) ||
                (x >= N - border * 2 && y < border * 2) ||
                (x < border * 2 && y >= N - border * 2) ||
                (x >= N - border * 2 && y >= N - border * 2);
            if (onEdge && inCorner) px[y * N + x] = stroke;
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(border * 2, border * 2, border * 2, border * 2));
    }

    private void BuildRecentHeader()
    {
        var go = new GameObject("RecentHeader", typeof(RectTransform));
        go.transform.SetParent(transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, -(ACTIVE_H + HEADER_GAP));
        rt.sizeDelta = new Vector2(ROW_W, HEADER_H);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = "RECENT";
        tmp.fontSize = 12f;
        tmp.fontStyle = FontStyles.Bold;
        tmp.color = ColorTextSec;
        tmp.alignment = TextAlignmentOptions.Left;
        tmp.margin = new Vector4(8f, 0f, 0f, 0f);
    }

    private void BuildRecentRows()
    {
        float baseY = -(ACTIVE_H + HEADER_GAP + HEADER_H + HEADER_GAP);
        for (int i = 0; i < recentRows.Length; i++)
        {
            float y = baseY - i * (RECENT_H + ROW_GAP);
            recentRows[i] = BuildRow("RecentRow" + i, y, RECENT_H, 44f, 14, 12);
        }
    }

    private Row BuildRow(string name, float y, float height, float iconSize, int stationFontSize, int taskFontSize)
    {
        var row = new GameObject(name, typeof(RectTransform));
        row.transform.SetParent(transform, false);
        var rt = (RectTransform)row.transform;
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, y);
        rt.sizeDelta = new Vector2(ROW_W, height);

        var bg = new GameObject("Bg", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(row.transform, false);
        var bgRt = (RectTransform)bg.transform;
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(4f, 4f); bgRt.offsetMax = new Vector2(-4f, -4f);
        bg.GetComponent<Image>().color = ColorRowBg;

        var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(row.transform, false);
        var pillRt = (RectTransform)pill.transform;
        pillRt.anchorMin = new Vector2(0f, 0.5f); pillRt.anchorMax = new Vector2(0f, 0.5f);
        pillRt.pivot = new Vector2(0f, 0.5f);
        pillRt.anchoredPosition = new Vector2(6f, 0f);
        pillRt.sizeDelta = new Vector2(4f, height - 16f);
        var pillImg = pill.GetComponent<Image>();
        pillImg.color = ColorIdle;

        var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        icon.transform.SetParent(row.transform, false);
        var iconRt = (RectTransform)icon.transform;
        iconRt.anchorMin = new Vector2(0f, 0.5f); iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.anchoredPosition = new Vector2(16f, 0f);
        iconRt.sizeDelta = new Vector2(iconSize, iconSize);
        var iconImg = icon.GetComponent<Image>();
        iconImg.preserveAspect = true;
        iconImg.raycastTarget = false;

        float textLeft = 16f + iconSize + 8f;

        var st = new GameObject("Station", typeof(RectTransform));
        st.transform.SetParent(row.transform, false);
        var stRt = (RectTransform)st.transform;
        stRt.anchorMin = new Vector2(0f, 0.5f); stRt.anchorMax = new Vector2(1f, 1f);
        stRt.pivot = new Vector2(0f, 1f);
        stRt.offsetMin = new Vector2(textLeft, 0f);
        stRt.offsetMax = new Vector2(-8f, -4f);
        var stTmp = st.AddComponent<TextMeshProUGUI>();
        stTmp.fontSize = stationFontSize;
        stTmp.fontStyle = FontStyles.Bold;
        stTmp.color = ColorTextPri;
        stTmp.alignment = TextAlignmentOptions.TopLeft;
        stTmp.enableWordWrapping = false;
        stTmp.overflowMode = TextOverflowModes.Ellipsis;
        stTmp.text = "";
        stTmp.raycastTarget = false;

        var tk = new GameObject("Task", typeof(RectTransform));
        tk.transform.SetParent(row.transform, false);
        var tkRt = (RectTransform)tk.transform;
        tkRt.anchorMin = new Vector2(0f, 0f); tkRt.anchorMax = new Vector2(1f, 0.5f);
        tkRt.pivot = new Vector2(0f, 0f);
        tkRt.offsetMin = new Vector2(textLeft, 8f);
        tkRt.offsetMax = new Vector2(-8f, 0f);
        var tkTmp = tk.AddComponent<TextMeshProUGUI>();
        tkTmp.fontSize = taskFontSize;
        tkTmp.color = ColorTextSec;
        tkTmp.alignment = TextAlignmentOptions.BottomLeft;
        tkTmp.enableWordWrapping = false;
        tkTmp.overflowMode = TextOverflowModes.Ellipsis;
        tkTmp.richText = true;
        tkTmp.text = "";
        tkTmp.raycastTarget = false;

        return new Row { Root = rt, Pill = pillImg, Icon = iconImg, StationLabel = stTmp, ResultLabel = tkTmp, TimeBar = null };
    }

    private void ClearActiveRow()
    {
        currentTask = null;
        if (activeRow.Pill != null) activeRow.Pill.color = ColorIdle;
        if (activeRow.Icon != null) { activeRow.Icon.sprite = null; activeRow.Icon.color = new Color(1f, 1f, 1f, 0.18f); }
        if (activeRow.StationLabel != null) { activeRow.StationLabel.text = "STANDBY"; activeRow.StationLabel.color = ColorTextSec; }
        if (activeRow.ResultLabel != null) activeRow.ResultLabel.text = "Awaiting task...";
        if (activeRow.TimeBar != null) { activeRow.TimeBar.fillAmount = 0f; activeRow.TimeBar.color = ColorIdle; }
        if (activeFrameImg != null) activeFrameImg.color = new Color(ColorIdle.r, ColorIdle.g, ColorIdle.b, 0.4f);
    }

    private void ClearRecentRow(int slot)
    {
        if (slot < 0 || slot >= recentRows.Length) return;
        var r = recentRows[slot];
        if (r.Pill != null) r.Pill.color = new Color(ColorIdle.r, ColorIdle.g, ColorIdle.b, 0.35f);
        if (r.Icon != null) { r.Icon.sprite = null; r.Icon.color = new Color(1f, 1f, 1f, 0f); }
        if (r.StationLabel != null) { r.StationLabel.text = ""; r.StationLabel.color = ColorTextPri; }
        if (r.ResultLabel != null) { r.ResultLabel.text = ""; r.ResultLabel.color = ColorTextSec; }
    }

    private void ShowEmptyRecentState()
    {
        if (recentRows.Length == 0) return;
        var r = recentRows[0];
        if (r.Pill != null) r.Pill.color = new Color(ColorIdle.r, ColorIdle.g, ColorIdle.b, 0.18f);
        if (r.StationLabel != null)
        {
            r.StationLabel.text = "AWAITING";
            r.StationLabel.color = new Color(ColorTextSec.r, ColorTextSec.g, ColorTextSec.b, 0.55f);
        }
        if (r.ResultLabel != null)
        {
            r.ResultLabel.text = "No tasks completed yet";
            r.ResultLabel.color = new Color(ColorTextSec.r, ColorTextSec.g, ColorTextSec.b, 0.4f);
        }
        _hasRecent = false;
    }

    private void HandleSpawn(MissionTask task)
    {
        if (task == null) return;
        currentTask = task;
        Sprite icon = LookupIcon(task.StationName);
        if (activeRow.Icon != null) { activeRow.Icon.sprite = icon; activeRow.Icon.color = Color.white; }
        if (activeRow.Pill != null) activeRow.Pill.color = ColorActive;
        if (activeRow.StationLabel != null) { activeRow.StationLabel.text = PrettyStation(task.StationName).ToUpperInvariant(); activeRow.StationLabel.color = ColorTextPri; }
        if (activeRow.ResultLabel != null) activeRow.ResultLabel.text = task.TaskName != null ? task.TaskName : "Task";
        if (activeRow.TimeBar != null) { activeRow.TimeBar.color = ColorActive; activeRow.TimeBar.fillAmount = 1f; }
        if (activeFrameImg != null) activeFrameImg.color = new Color(ColorActive.r, ColorActive.g, ColorActive.b, 1f);
    }

    private void HandleResolved(MissionTask task, TaskResult result, float reactionTime)
    {
        if (task == null) return;
        bool isCurrent = ReferenceEquals(task, currentTask);
        if (!_hasRecent)
        {
            // First resolved task: replace the empty-state placeholder before shifting.
            ClearRecentRow(0);
            _hasRecent = true;
        }
        ShiftRecentsDown();
        WriteRecent(0, task, result, reactionTime);
        if (isCurrent) { ClearActiveRow(); _urgencyActive = false; }
    }

    private void ShiftRecentsDown()
    {
        for (int i = recentRows.Length - 1; i >= 1; i--)
        {
            var src = recentRows[i - 1];
            var dst = recentRows[i];
            if (dst.Icon != null && src.Icon != null) { dst.Icon.sprite = src.Icon.sprite; dst.Icon.color = src.Icon.color; }
            if (dst.Pill != null && src.Pill != null) dst.Pill.color = src.Pill.color;
            if (dst.StationLabel != null && src.StationLabel != null) { dst.StationLabel.text = src.StationLabel.text; dst.StationLabel.color = src.StationLabel.color; }
            if (dst.ResultLabel != null && src.ResultLabel != null) { dst.ResultLabel.text = src.ResultLabel.text; dst.ResultLabel.color = src.ResultLabel.color; }
        }
    }

    private void WriteRecent(int slot, MissionTask task, TaskResult result, float rt)
    {
        if (slot < 0 || slot >= recentRows.Length) return;
        var r = recentRows[slot];
        bool ok = result == TaskResult.Success;
        Color tint = ok ? ColorActive : ColorUrgent;
        Sprite icon = LookupIcon(task.StationName);
        if (r.Icon != null) { r.Icon.sprite = icon; r.Icon.color = new Color(1f, 1f, 1f, 0.9f); }
        if (r.Pill != null) r.Pill.color = tint;
        if (r.StationLabel != null) { r.StationLabel.text = PrettyStation(task.StationName).ToUpperInvariant(); r.StationLabel.color = ColorTextPri; }
        if (r.ResultLabel != null)
        {
            string tag = ResultLabel(result);
            string hex = ColorUtility.ToHtmlStringRGB(tint);
            string taskName = task.TaskName != null ? task.TaskName : "Task";
            r.ResultLabel.text = taskName + " <color=#" + hex + ">" + tag + "</color> " + rt.ToString("F1") + "s";
            r.ResultLabel.color = ColorTextSec;
        }
    }

    private static string ResultLabel(TaskResult r)
    {
        switch (r)
        {
            case TaskResult.Success:    return "PASS";
            case TaskResult.Fail:       return "FAIL";
            case TaskResult.Omission:   return "MISS";
            case TaskResult.Commission: return "ERR";
        }
        return r.ToString().ToUpperInvariant();
    }

    private Sprite LookupIcon(string stationName)
    {
        if (string.IsNullOrEmpty(stationName)) return fallbackIcon;
        if (iconByStation != null && iconByStation.TryGetValue(stationName, out var s) && s != null) return s;
        return fallbackIcon;
    }

    public static string PrettyStation(string stationName)
    {
        if (string.IsNullOrEmpty(stationName)) return "Station";
        switch (stationName)
        {
            case "EngineStation":      return "Engine";
            case "NavigationStation":  return "Navigation";
            case "CommsStation":       return "Comms";
            case "LifeSupportStation": return "Life Support";
        }
        return stationName;
    }

    private void Update()
    {
        if (currentTask == null || !currentTask.IsActive)
        {
            if (_urgencyActive) _urgencyActive = false;
            return;
        }
        float elapsed = Time.time - currentTask.SpawnTime;
        float remaining = Mathf.Max(0f, currentTask.timeLimit - elapsed);
        float fill = Mathf.Clamp01(remaining / Mathf.Max(0.0001f, currentTask.timeLimit));
        if (activeRow.TimeBar != null) activeRow.TimeBar.fillAmount = fill;
        bool urgent = remaining < 5f;
        if (urgent && !_urgencyActive)
        {
            _urgencyActive = true;
            AudioManager.Instance.PlayMusic("urgency");
        }
        else if (!urgent && _urgencyActive)
        {
            _urgencyActive = false;
        }
        Color c = urgent ? ColorUrgent : ColorActive;
        if (activeRow.Pill != null) activeRow.Pill.color = c;
        if (activeRow.TimeBar != null) activeRow.TimeBar.color = c;
        if (activeFrameImg != null)
        {
            // Pulse the frame alpha while urgent so the panel reads as 'in danger'.
            float a = urgent ? 0.55f + 0.45f * Mathf.PingPong(Time.time * 2f, 1f) : 1f;
            activeFrameImg.color = new Color(c.r, c.g, c.b, a);
        }
    }
}
