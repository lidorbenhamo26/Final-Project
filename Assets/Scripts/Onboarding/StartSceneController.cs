using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartSceneController : MonoBehaviour
{
    private RectTransform formPanel;
    private ShipBriefingPanelController briefing;

    private void Awake()
    {
        var _ = SessionContext.Instance;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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

        var bg = new GameObject("Bg");
        bg.transform.SetParent(canvasGO.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0.03f, 0.05f, 0.09f, 1f);
        bgImg.raycastTarget = false;
        var bgRT = bg.GetComponent<RectTransform>();
        bgRT.anchorMin = Vector2.zero; bgRT.anchorMax = Vector2.one;
        bgRT.offsetMin = Vector2.zero; bgRT.offsetMax = Vector2.zero;

        UIChrome.BuildStarfield(canvasGO.transform);

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
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = TextAlignmentOptions.Center;
    }
}
