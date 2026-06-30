using TMPro;
using UnityEngine;

public class TutorialHighlight : MonoBehaviour
{
    [SerializeField] private float heightAboveTarget = 2.4f;
    [SerializeField] private Color pulseColor = new Color(0.37f, 0.78f, 0.88f, 1f);
    [SerializeField] private float pulseSpeed = 3.2f;

    private Canvas canvas;
    private GameObject canvasGO;
    private TMP_Text mainText;
    private TMP_Text subText;
    private Transform target;

    private void Awake()
    {
        BuildCanvas();
        SetVisible(false);
    }

    /// <summary>
    /// Point the marker at <paramref name="t"/>. <paramref name="title"/> is the
    /// big pulsing line (e.g. the station name) and <paramref name="action"/> the
    /// smaller prompt under it (e.g. "DOCK HERE"). Replaces the old generic
    /// "OBJECTIVE" placeholder with a real, contextual prompt.
    /// </summary>
    public void SetTarget(Transform t, string title, string action = "")
    {
        target = t;
        if (mainText != null) mainText.text = title ?? "";
        if (subText != null) subText.text = action ?? "";
        SetVisible(t != null);
    }

    public void Hide() => SetTarget(null, null);

    private void LateUpdate()
    {
        if (target == null || canvasGO == null) return;

        canvasGO.transform.position = target.position + Vector3.up * heightAboveTarget;

        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 fwd = canvasGO.transform.position - cam.transform.position;
            fwd.y = 0f;
            if (fwd.sqrMagnitude > 0.0001f)
                canvasGO.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
        float alpha = Mathf.Lerp(0.35f, 1f, pulse);
        if (mainText != null)
        {
            var c = pulseColor; c.a = alpha;
            mainText.color = c;
        }
    }

    private void SetVisible(bool v)
    {
        if (canvasGO != null) canvasGO.SetActive(v);
    }

    private void BuildCanvas()
    {
        canvasGO = new GameObject("HighlightCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var rt = canvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(4f, 1.6f);
        canvasGO.transform.localScale = Vector3.one * 0.01f;

        mainText = SpawnText(canvasGO.transform, "", 80, FontStyles.Bold,
            new Vector2(0f, 28f), new Vector2(400f, 80f));
        subText = SpawnText(canvasGO.transform, "", 36, FontStyles.Bold,
            new Vector2(0f, -36f), new Vector2(400f, 40f));
        subText.color = new Color(0.95f, 0.97f, 1f, 0.9f);
    }

    private static TMP_Text SpawnText(Transform parent, string text, int size, FontStyles style,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sizeDelta;
        rt.anchoredPosition = anchoredPos;
        var lbl = go.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.alignment = TextAlignmentOptions.Center;
        lbl.textWrappingMode = TextWrappingModes.NoWrap;
        return lbl;
    }
}
