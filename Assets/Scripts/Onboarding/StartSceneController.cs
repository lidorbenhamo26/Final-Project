using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneController : MonoBehaviour
{
    private void Awake()
    {
        var _ = SessionContext.Instance;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Start()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        var canvasGO = new GameObject("Start_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        var bg = new GameObject("Bg");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.05f, 0.07f, 0.10f, 1f);
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        var title = SpawnLabel(canvasGO.transform, "MISSION FOCUS", 96, FontStyles.Bold);
        title.alignment = TextAlignmentOptions.Center;
        var titleRT = title.GetComponent<RectTransform>();
        titleRT.anchorMin = new Vector2(0f, 0.5f);
        titleRT.anchorMax = new Vector2(1f, 0.5f);
        titleRT.pivot = new Vector2(0.5f, 0.5f);
        titleRT.sizeDelta = new Vector2(0f, 140f);
        titleRT.anchoredPosition = new Vector2(0f, 160f);

        var subtitle = SpawnLabel(canvasGO.transform, "10-minute cognitive load mission", 32);
        subtitle.alignment = TextAlignmentOptions.Center;
        subtitle.color = new Color(0.7f, 0.78f, 0.88f, 1f);
        var subRT = subtitle.GetComponent<RectTransform>();
        subRT.anchorMin = new Vector2(0f, 0.5f);
        subRT.anchorMax = new Vector2(1f, 0.5f);
        subRT.pivot = new Vector2(0.5f, 0.5f);
        subRT.sizeDelta = new Vector2(0f, 60f);
        subRT.anchoredPosition = new Vector2(0f, 60f);

        var btnGO = new GameObject("BeginButton");
        btnGO.transform.SetParent(canvasGO.transform, false);
        var btnImg = btnGO.AddComponent<Image>();
        btnImg.color = new Color(0.20f, 0.40f, 0.85f, 1f);
        var btn = btnGO.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(SceneTransition.LoadMain);
        var btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(0.5f, 0.5f);
        btnRT.anchorMax = new Vector2(0.5f, 0.5f);
        btnRT.pivot = new Vector2(0.5f, 0.5f);
        btnRT.sizeDelta = new Vector2(360f, 80f);
        btnRT.anchoredPosition = new Vector2(0f, -80f);

        var btnLabel = SpawnLabel(btnGO.transform, "BEGIN MISSION", 32, FontStyles.Bold);
        btnLabel.alignment = TextAlignmentOptions.Center;
        var lblRT = btnLabel.GetComponent<RectTransform>();
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
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
}
