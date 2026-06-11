using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneController : MonoBehaviour
{
    private const float VideoAspectRatio = 16f / 9f;

    private RectTransform formPanel;
    private ShipBriefingPanelController briefing;

    private void Awake()
    {
        var _ = SessionContext.Instance;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (Camera.main != null)
        {
            Camera.main.clearFlags = CameraClearFlags.SolidColor;
            Camera.main.backgroundColor = new Color(0.015f, 0.025f, 0.045f, 1f);
        }
    }

    private void Start()
    {
        BuildUI();
        if (SessionContext.Instance.TutorialCompleted)
        {
            if (formPanel != null) formPanel.gameObject.SetActive(false);
            briefing.Show();
        }
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

        BuildVideoBackground(canvasGO.transform);

        SpawnHeading(canvasGO.transform, "MISSION FOCUS", 56, FontStyles.Bold,
            new Vector2(0f, 480f), new Vector2(1200f, 64f), Color.white);

        var formGO = new GameObject("ParticipantForm");
        formGO.transform.SetParent(transform, false);
        var form = formGO.AddComponent<ParticipantFormController>();
        form.OnSubmit = OnFormSubmitted;
        formPanel = form.BuildUI(canvasGO.transform);

        var briefGO = new GameObject("ShipBriefingPanel");
        briefGO.transform.SetParent(transform, false);
        briefing = briefGO.AddComponent<ShipBriefingPanelController>();
        briefing.OnBegin = OnBriefingBegin;
        briefing.OnBack = OnBriefingBack;
        briefing.BuildUI(canvasGO.transform);
        briefing.Hide();
    }

    private static void BuildVideoBackground(Transform parent)
    {
        var fallback = NewFullScreenRect("BgFallback", parent);
        var fallbackImg = fallback.gameObject.AddComponent<Image>();
        fallbackImg.color = new Color(0.015f, 0.025f, 0.045f, 1f);
        fallbackImg.raycastTarget = false;

        var video = new GameObject("VideoBackground");
        video.transform.SetParent(parent, false);
        var videoRT = video.AddComponent<RectTransform>();
        videoRT.anchorMin = new Vector2(0.5f, 0.5f);
        videoRT.anchorMax = new Vector2(0.5f, 0.5f);
        videoRT.pivot = new Vector2(0.5f, 0.5f);
        videoRT.anchoredPosition = Vector2.zero;
        videoRT.sizeDelta = new Vector2(1920f, 1080f);

        var rawImage = video.AddComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        var fitter = video.AddComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        fitter.aspectRatio = VideoAspectRatio;

        video.AddComponent<LoopingVideoBackground>();

        var overlay = NewFullScreenRect("VideoReadabilityOverlay", parent);
        var overlayImg = overlay.gameObject.AddComponent<Image>();
        overlayImg.color = new Color(0f, 0f, 0f, 0.45f);
        overlayImg.raycastTarget = false;
    }

    private void OnFormSubmitted()
    {
        SceneTransition.LoadTutorial();
    }

    private void OnBriefingBack()
    {
        SceneTransition.LoadTutorial();
    }

    private void OnBriefingBegin()
    {
        SceneTransition.LoadMain();
    }

    private static void SpawnHeading(Transform parent, string text, int size, FontStyles style,
        Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var go = new GameObject("Heading");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = pos;
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.enableAutoSizing = true;
        lbl.fontSizeMin = Mathf.Max(18f, size * 0.55f);
        lbl.fontSizeMax = size;
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = TextAlignmentOptions.Center;
    }

    private static RectTransform NewFullScreenRect(string name, Transform parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return rt;
    }
}
