using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Screen-space HUD for the battery delivery mission: an objective line and a
/// draining life-support power meter. Created and driven by BatteryDeliveryTask;
/// destroy the GameObject to remove it.
/// </summary>
public class BatteryMissionHUD : MonoBehaviour
{
    private const float BarInnerWidth = 712f;

    private Canvas canvas;
    private TMP_Text objectiveText;
    private RectTransform powerFillRT;
    private Image powerFillImg;
    private TMP_Text powerLabel;

    private void Awake()
    {
        Build();
    }

    private void Update()
    {
        // Hide the power bar while docked at a station — it's a bottom-center
        // overlay that otherwise blocks the console view. The task keeps
        // draining underneath; only the rendering is toggled.
        if (canvas == null) return;
        bool docked = StationDockController.Instance != null && StationDockController.Instance.IsDocked;
        if (canvas.enabled == docked) canvas.enabled = !docked;
    }

    public void SetObjective(string text)
    {
        if (objectiveText == null) return;
        objectiveText.text = text;
        objectiveText.color = Color.white;
    }

    public void Flash(string text, Color color)
    {
        if (objectiveText == null) return;
        objectiveText.text = text;
        objectiveText.color = color;
    }

    public void SetPower(float frac01)
    {
        frac01 = Mathf.Clamp01(frac01);
        if (powerFillRT != null)
            powerFillRT.sizeDelta = new Vector2(BarInnerWidth * frac01, powerFillRT.sizeDelta.y);
        if (powerFillImg != null)
        {
            powerFillImg.color = frac01 > 0.5f ? new Color(0.3f, 1f, 0.5f)
                : frac01 > 0.25f ? new Color(1f, 0.8f, 0.2f)
                : new Color(1f, 0.32f, 0.26f);
        }
        if (powerLabel != null)
        {
            int pct = Mathf.CeilToInt(frac01 * 100f);
            powerLabel.text = frac01 <= 0.25f
                ? "LIFE SUPPORT POWER  " + pct + "%  —  RESTORE NOW"
                : "LIFE SUPPORT POWER  " + pct + "%";
        }
    }

    private void Build()
    {
        var canvasGO = new GameObject("BatteryMissionHUD_Canvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 96;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f; // match height so it fits on screens shorter than 16:9
        canvasGO.AddComponent<GraphicRaycaster>();

        // Objective panel, bottom-center (clear of the top-center alert banner).
        var panel = new GameObject("ObjectivePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvasGO.transform, false);
        var prt = (RectTransform)panel.transform;
        prt.anchorMin = new Vector2(0.5f, 0f);
        prt.anchorMax = new Vector2(0.5f, 0f);
        prt.pivot = new Vector2(0.5f, 0f);
        prt.anchoredPosition = new Vector2(0f, 132f);
        prt.sizeDelta = new Vector2(760f, 98f);
        var pbg = panel.GetComponent<Image>();
        pbg.color = new Color(0.04f, 0.06f, 0.09f, 0.84f);
        pbg.raycastTarget = false;
        var outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(0.4f, 0.85f, 1f, 0.7f);
        outline.effectDistance = new Vector2(2f, 2f);

        var objGO = new GameObject("Objective", typeof(RectTransform));
        objGO.transform.SetParent(panel.transform, false);
        objectiveText = objGO.AddComponent<TextMeshProUGUI>();
        objectiveText.alignment = TextAlignmentOptions.Center;
        objectiveText.enableAutoSizing = true;
        objectiveText.fontSizeMin = 15f;
        objectiveText.fontSizeMax = 26f;
        objectiveText.fontStyle = FontStyles.Bold;
        objectiveText.color = Color.white;
        objectiveText.raycastTarget = false;
        objectiveText.textWrappingMode = TextWrappingModes.NoWrap;
        objectiveText.text = "";
        var ort = (RectTransform)objGO.transform;
        ort.anchorMin = new Vector2(0f, 1f);
        ort.anchorMax = new Vector2(1f, 1f);
        ort.pivot = new Vector2(0.5f, 1f);
        ort.anchoredPosition = new Vector2(0f, -10f);
        ort.sizeDelta = new Vector2(-32f, 42f);

        var barBg = new GameObject("PowerBarBg", typeof(RectTransform), typeof(Image));
        barBg.transform.SetParent(panel.transform, false);
        var barBgImg = barBg.GetComponent<Image>();
        barBgImg.color = new Color(0.1f, 0.12f, 0.16f, 1f);
        barBgImg.raycastTarget = false;
        var bbrt = (RectTransform)barBg.transform;
        bbrt.anchorMin = new Vector2(0.5f, 0f);
        bbrt.anchorMax = new Vector2(0.5f, 0f);
        bbrt.pivot = new Vector2(0.5f, 0f);
        bbrt.anchoredPosition = new Vector2(0f, 14f);
        bbrt.sizeDelta = new Vector2(720f, 28f);

        var fillGO = new GameObject("PowerBarFill", typeof(RectTransform), typeof(Image));
        fillGO.transform.SetParent(barBg.transform, false);
        powerFillImg = fillGO.GetComponent<Image>();
        powerFillImg.color = new Color(0.3f, 1f, 0.5f);
        powerFillImg.raycastTarget = false;
        powerFillRT = (RectTransform)fillGO.transform;
        powerFillRT.anchorMin = new Vector2(0f, 0.5f);
        powerFillRT.anchorMax = new Vector2(0f, 0.5f);
        powerFillRT.pivot = new Vector2(0f, 0.5f);
        powerFillRT.anchoredPosition = new Vector2(4f, 0f);
        powerFillRT.sizeDelta = new Vector2(BarInnerWidth, 22f);

        var plGO = new GameObject("PowerLabel", typeof(RectTransform));
        plGO.transform.SetParent(barBg.transform, false);
        powerLabel = plGO.AddComponent<TextMeshProUGUI>();
        powerLabel.alignment = TextAlignmentOptions.Center;
        powerLabel.fontSize = 15f;
        powerLabel.fontStyle = FontStyles.Bold;
        powerLabel.color = new Color(0.95f, 0.98f, 1f);
        powerLabel.raycastTarget = false;
        powerLabel.textWrappingMode = TextWrappingModes.NoWrap;
        powerLabel.text = "LIFE SUPPORT POWER  100%";
        var plrt = (RectTransform)plGO.transform;
        plrt.anchorMin = Vector2.zero;
        plrt.anchorMax = Vector2.one;
        plrt.offsetMin = Vector2.zero;
        plrt.offsetMax = Vector2.zero;
        var plOutline = plGO.AddComponent<Outline>();
        plOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        plOutline.effectDistance = new Vector2(1.2f, 1.2f);
    }
}
