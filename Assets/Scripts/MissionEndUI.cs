using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MissionEndUI : MonoBehaviour
{
    private GameObject canvasRoot;
    private bool shown;

    public void Show()
    {
        if (shown) return;
        shown = true;
        Build();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Build()
    {
        var canvasGO = new GameObject("MissionEnd_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("Bg");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.85f);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        var panel = new GameObject("Panel");
        panel.transform.SetParent(canvasGO.transform, false);
        var panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.10f, 0.14f, 0.96f);
        var panelRT = panel.GetComponent<RectTransform>();
        panelRT.anchorMin = new Vector2(0.5f, 0.5f);
        panelRT.anchorMax = new Vector2(0.5f, 0.5f);
        panelRT.pivot = new Vector2(0.5f, 0.5f);
        panelRT.sizeDelta = new Vector2(640f, 460f);
        panelRT.anchoredPosition = Vector2.zero;

        var title = SpawnLabel(panel.transform, "MISSION COMPLETE", 48, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 1f);
        titleRT.anchorMax = new Vector2(1f, 1f);
        titleRT.pivot = new Vector2(0.5f, 1f);
        titleRT.sizeDelta = new Vector2(0f, 80f);
        titleRT.anchoredPosition = new Vector2(0f, -24f);

        int total = SessionManager.Instance != null ? SessionManager.Instance.TasksTotal : 0;
        int passed = SessionManager.Instance != null ? SessionManager.Instance.TasksPassed : 0;
        int failed = SessionManager.Instance != null ? SessionManager.Instance.TasksFailed : 0;
        float avgRT = SessionManager.Instance != null ? SessionManager.Instance.AverageReactionTime : 0f;
        float accuracy = total > 0 ? (passed * 100f / total) : 0f;

        string body =
            $"Total tasks      <b>{total}</b>\n" +
            $"Passed           <b><color=#4ade80>{passed}</color></b>\n" +
            $"Failed           <b><color=#f87171>{failed}</color></b>\n" +
            $"Accuracy         <b>{accuracy:F0}%</b>\n" +
            $"Avg reaction     <b>{avgRT:F2}s</b>";

        var stats = SpawnLabel(panel.transform, body, 28);
        stats.alignment = TextAlignmentOptions.MidlineLeft;
        var statsRT = stats.GetComponent<RectTransform>();
        statsRT.anchorMin = new Vector2(0f, 0f);
        statsRT.anchorMax = new Vector2(1f, 1f);
        statsRT.offsetMin = new Vector2(60f, 110f);
        statsRT.offsetMax = new Vector2(-60f, -120f);

        var btnGO = new GameObject("RestartButton");
        btnGO.transform.SetParent(panel.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.20f, 0.40f, 0.85f, 1f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(Restart);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0f);
        btnRT.anchorMax = new Vector2(0.5f, 0f);
        btnRT.pivot = new Vector2(0.5f, 0f);
        btnRT.sizeDelta = new Vector2(260f, 64f);
        btnRT.anchoredPosition = new Vector2(0f, 28f);

        var btnLabel = SpawnLabel(btnGO.transform, "RESTART", 28, FontStyles.Bold);
        btnLabel.alignment = TextAlignmentOptions.Center;
        var lblRT = btnLabel.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;

        canvasRoot = canvasGO;
    }

    private TMP_Text SpawnLabel(Transform parent, string text, int size, FontStyles style = FontStyles.Normal)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.color = Color.white;
        lbl.richText = true;
        return lbl;
    }

    private void Restart()
    {
        if (SessionManager.Instance != null) SessionManager.Instance.ResetForNewMission();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
