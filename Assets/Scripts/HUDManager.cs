using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text taskListText;

    private MissionEndUI missionEndUI;
    private bool wasMissionActive;
    private TMP_Text alertBannerText;
    private Coroutine alertBannerCo;
    private GameObject codePanel;
    private TMP_Text codePanelStatus;
    private TMP_Text codePanelCode;
    private Coroutine codePanelCo;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        missionEndUI = gameObject.AddComponent<MissionEndUI>();
        EnsureLiveHud();
    }

    private void Update()
    {
        if (GameManager.Instance == null) return;

        bool active = GameManager.Instance.MissionActive;
        if (wasMissionActive && !active)
            missionEndUI.Show();
        wasMissionActive = active;

        if (timerText != null)
        {
            float t = GameManager.Instance.MissionTimeRemaining;
            int min = Mathf.FloorToInt(t / 60f);
            int sec = Mathf.FloorToInt(t % 60f);
            timerText.text = "MISSION  " + min.ToString("00") + ":" + sec.ToString("00");
        }

        if (taskListText != null && SessionManager.Instance != null)
        {
            var sm = SessionManager.Instance;
            taskListText.text =
                "<color=#4ade80>PASSED " + sm.TasksPassed + "</color>   " +
                "<color=#f87171>FAILED " + sm.TasksFailed + "</color>   " +
                "AVG " + sm.AverageReactionTime.ToString("F1") + "s";
        }
    }

    public void UpdateTaskList(string content)
    {
        if (taskListText != null) taskListText.text = content;
    }

    /// <summary>Flashes a centered top-of-screen banner for the given duration, then fades.</summary>
    public void ShowAlertBanner(string text, float seconds)
    {
        EnsureAlertBanner();
        if (alertBannerText == null) return;
        if (alertBannerCo != null) StopCoroutine(alertBannerCo);
        alertBannerText.text = text;
        AudioManager.Instance.PlaySfx("alert_pulse");
        alertBannerCo = StartCoroutine(CoBannerFade(seconds));
    }

    public void HideAlertBanner()
    {
        if (alertBannerCo != null) { StopCoroutine(alertBannerCo); alertBannerCo = null; }
        if (alertBannerText != null) alertBannerText.text = "";
    }

    /// <summary>
    /// Show a large centered code panel on the HUD overlay for the given duration,
    /// then auto-hide. Used by WorkingMemoryTask so the player can't miss the code.
    /// </summary>
    public void ShowCodeBanner(string code, float duration)
    {
        EnsureCodePanel();
        if (codePanel == null) return;
        if (codePanelCo != null) StopCoroutine(codePanelCo);
        codePanel.SetActive(true);
        if (codePanelStatus != null) codePanelStatus.text = "AUTH CODE — MEMORIZE";
        if (codePanelCode != null) codePanelCode.text = code;
        AudioManager.Instance.PlaySfx("code_banner_appear");
        codePanelCo = StartCoroutine(CoHideCodePanel(duration));
    }

    public void HideCodeBanner()
    {
        if (codePanelCo != null) { StopCoroutine(codePanelCo); codePanelCo = null; }
        if (codePanel != null) codePanel.SetActive(false);
    }

    private IEnumerator CoHideCodePanel(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (codePanel != null) codePanel.SetActive(false);
        codePanelCo = null;
    }

    private void EnsureCodePanel()
    {
        if (codePanel != null) return;
        Canvas canvas = GetComponentInChildren<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        codePanel = new GameObject("CodePanel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        codePanel.transform.SetParent(parent, false);
        RectTransform rt = (RectTransform)codePanel.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(900f, 380f);
        rt.anchoredPosition = new Vector2(0f, 60f);
        var bg = codePanel.GetComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.02f, 0.05f, 0.08f, 0.92f);
        bg.raycastTarget = false;

        // Cyan border (Outline component for cheap rim)
        var outline = codePanel.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(0.3f, 1f, 1f, 1f);
        outline.effectDistance = new Vector2(4f, 4f);

        // Status label (top)
        var statusGO = new GameObject("Status", typeof(RectTransform));
        statusGO.transform.SetParent(codePanel.transform, false);
        codePanelStatus = statusGO.AddComponent<TextMeshProUGUI>();
        codePanelStatus.alignment = TextAlignmentOptions.Center;
        codePanelStatus.fontSize = 42f;
        codePanelStatus.fontStyle = FontStyles.Bold;
        codePanelStatus.color = new Color(0.4f, 1f, 0.6f);
        codePanelStatus.text = "AUTH CODE — MEMORIZE";
        codePanelStatus.raycastTarget = false;
        RectTransform srt = (RectTransform)statusGO.transform;
        srt.anchorMin = new Vector2(0f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(0.5f, 1f);
        srt.anchoredPosition = new Vector2(0f, -20f);
        srt.sizeDelta = new Vector2(0f, 70f);

        // Big code text (center)
        var codeGO = new GameObject("Code", typeof(RectTransform));
        codeGO.transform.SetParent(codePanel.transform, false);
        codePanelCode = codeGO.AddComponent<TextMeshProUGUI>();
        codePanelCode.alignment = TextAlignmentOptions.Center;
        codePanelCode.fontSize = 220f;
        codePanelCode.fontStyle = FontStyles.Bold;
        codePanelCode.color = new Color(0.3f, 1f, 1f);
        codePanelCode.characterSpacing = 20f;
        codePanelCode.text = "";
        codePanelCode.raycastTarget = false;
        RectTransform crt = (RectTransform)codeGO.transform;
        crt.anchorMin = new Vector2(0f, 0f);
        crt.anchorMax = new Vector2(1f, 1f);
        crt.offsetMin = new Vector2(20f, 20f);
        crt.offsetMax = new Vector2(-20f, -90f);

        codePanel.SetActive(false);
    }

    private IEnumerator CoBannerFade(float seconds)
    {
        Color visible = new Color(1f, 0.85f, 0.3f, 1f);
        Color hidden  = new Color(1f, 0.85f, 0.3f, 0f);
        float pulseRate = 3f;
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            float t = Mathf.PingPong(elapsed * pulseRate, 1f);
            alertBannerText.color = Color.Lerp(hidden, visible, 0.6f + 0.4f * t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        // Final fade-out over 0.3s
        float fadeT = 0f;
        const float fadeDur = 0.3f;
        Color startCol = alertBannerText.color;
        while (fadeT < fadeDur)
        {
            alertBannerText.color = Color.Lerp(startCol, hidden, fadeT / fadeDur);
            fadeT += Time.deltaTime;
            yield return null;
        }
        alertBannerText.text = "";
        alertBannerText.color = hidden;
        alertBannerCo = null;
    }

    private void EnsureAlertBanner()
    {
        if (alertBannerText != null) return;
        Canvas canvas = GetComponentInChildren<Canvas>();
        Transform parent = canvas != null ? canvas.transform : transform;

        GameObject go = new GameObject("AlertBanner");
        go.transform.SetParent(parent, false);
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.sizeDelta = new Vector2(1400f, 80f);
        rt.anchoredPosition = new Vector2(0f, -28f);
        alertBannerText = go.AddComponent<TextMeshProUGUI>();
        alertBannerText.alignment = TextAlignmentOptions.Center;
        alertBannerText.fontSize = 48f;
        alertBannerText.fontStyle = FontStyles.Bold;
        alertBannerText.color = new Color(1f, 0.85f, 0.3f, 0f);
        alertBannerText.text = "";
    }

    // Builds a minimal top-left HUD canvas at runtime so timer + live scores are
    // visible without requiring a scene-wired Canvas. Skipped if the serialized
    // fields are already assigned in the scene.
    private void EnsureLiveHud()
    {
        if (timerText != null && taskListText != null) return;

        var canvasGO = new GameObject("HUD_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        if (timerText == null)
            timerText = SpawnHudLabel(canvasGO.transform, "Timer", new Vector2(24f, -24f), 36, FontStyles.Bold);
        if (taskListText == null)
            taskListText = SpawnHudLabel(canvasGO.transform, "Score", new Vector2(24f, -68f), 24, FontStyles.Normal);

        SpawnMiniMap(canvasGO.transform);
        SpawnTaskListHUD(canvasGO.transform);
        SpawnNotificationFeed(canvasGO.transform);
    }

    private void SpawnMiniMap(Transform canvasParent)
    {
        var minimapGO = new GameObject("MiniMapHUD", typeof(RectTransform));
        minimapGO.transform.SetParent(canvasParent, false);
        var rt = (RectTransform)minimapGO.transform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot     = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(24f, 24f);
        rt.sizeDelta = new Vector2(280f, 280f);
        minimapGO.AddComponent<MiniMapHUD>();
    }

    private void SpawnTaskListHUD(Transform canvasParent)
    {
        var go = new GameObject("TaskListHUD", typeof(RectTransform));
        go.transform.SetParent(canvasParent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -110f);
        rt.sizeDelta = new Vector2(300f, 340f);
        go.AddComponent<TaskListHUD>();
    }

    private void SpawnNotificationFeed(Transform canvasParent)
    {
        var go = new GameObject("NotificationFeed", typeof(RectTransform));
        go.transform.SetParent(canvasParent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(24f, -470f);
        rt.sizeDelta = new Vector2(280f, 290f);
        go.AddComponent<NotificationFeed>();
    }

    private TMP_Text SpawnHudLabel(Transform parent, string name, Vector2 anchoredPos, int size, FontStyles style)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(720f, 48f);
        rt.anchoredPosition = anchoredPos;
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.color = Color.white;
        lbl.richText = true;
        lbl.text = "";
        return lbl;
    }
}
