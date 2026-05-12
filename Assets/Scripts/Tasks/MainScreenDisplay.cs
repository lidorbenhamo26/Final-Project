using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// World-space main display screen, used by tasks that need to present
/// information away from the station canvas (e.g. WorkingMemoryTask shows a
/// 4-digit code here before the player walks to the engine terminal).
///
/// Attach this component to a GameObject placed where the screen prop sits
/// (a wall-mounted monitor). The canvas is built procedurally as a child so
/// no scene wiring is needed beyond placing the parent at the right pose.
/// </summary>
public class MainScreenDisplay : MonoBehaviour
{
    public static MainScreenDisplay Instance { get; private set; }

    [Header("Canvas")]
    [SerializeField] private Vector2 canvasSize = new Vector2(1200f, 700f);
    [SerializeField] private float canvasScale = 0.003f;
    [SerializeField] private Vector3 canvasLocalOffset = new Vector3(0f, 0f, 0.3f);

    [Header("Audio (optional)")]
    [SerializeField] private AudioClip alertClip;

    private Canvas worldCanvas;
    private TMP_Text codeText;
    private TMP_Text statusText;
    private Image alertGlow;
    private AudioSource audioSrc;
    private Coroutine glowCo;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        BuildCanvas();
        audioSrc = gameObject.AddComponent<AudioSource>();
        audioSrc.playOnAwake = false;
        audioSrc.spatialBlend = 0f;
        SetIdleVisual();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void BuildCanvas()
    {
        GameObject canvasGO = new GameObject("MainScreenCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = canvasLocalOffset;
        canvasGO.transform.localRotation = Quaternion.identity;

        worldCanvas = canvasGO.AddComponent<Canvas>();
        worldCanvas.renderMode = RenderMode.WorldSpace;

        RectTransform crt = canvasGO.GetComponent<RectTransform>();
        crt.sizeDelta = canvasSize;
        crt.localScale = Vector3.one * canvasScale;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Background panel
        GameObject bg = new GameObject("BG", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(canvasGO.transform, false);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = new Color(0.02f, 0.04f, 0.08f, 0.95f);

        // Alert glow overlay (full-rect, additive feel by tinting bright)
        GameObject glow = new GameObject("AlertGlow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(canvasGO.transform, false);
        RectTransform glowRt = glow.GetComponent<RectTransform>();
        glowRt.anchorMin = Vector2.zero;
        glowRt.anchorMax = Vector2.one;
        glowRt.offsetMin = Vector2.zero;
        glowRt.offsetMax = Vector2.zero;
        alertGlow = glow.GetComponent<Image>();
        alertGlow.color = new Color(1f, 0.2f, 0.2f, 0f);
        alertGlow.raycastTarget = false;

        // Status text (top, smaller)
        GameObject status = new GameObject("Status", typeof(RectTransform));
        status.transform.SetParent(canvasGO.transform, false);
        statusText = status.AddComponent<TextMeshProUGUI>();
        statusText.alignment = TextAlignmentOptions.Center;
        statusText.fontSize = 56f;
        statusText.fontStyle = FontStyles.Bold;
        statusText.color = new Color(0.6f, 0.8f, 1f);
        statusText.text = "MAIN DISPLAY";
        RectTransform statusRt = status.GetComponent<RectTransform>();
        statusRt.anchorMin = new Vector2(0f, 1f);
        statusRt.anchorMax = new Vector2(1f, 1f);
        statusRt.pivot = new Vector2(0.5f, 1f);
        statusRt.anchoredPosition = new Vector2(0f, -40f);
        statusRt.sizeDelta = new Vector2(0f, 100f);

        // Big code text (center, very large)
        GameObject code = new GameObject("CodeText", typeof(RectTransform));
        code.transform.SetParent(canvasGO.transform, false);
        codeText = code.AddComponent<TextMeshProUGUI>();
        codeText.alignment = TextAlignmentOptions.Center;
        codeText.fontSize = 280f;
        codeText.fontStyle = FontStyles.Bold;
        codeText.color = new Color(0.3f, 1f, 1f);
        codeText.text = "";
        codeText.characterSpacing = 30f;
        RectTransform codeRt = code.GetComponent<RectTransform>();
        codeRt.anchorMin = new Vector2(0.5f, 0.5f);
        codeRt.anchorMax = new Vector2(0.5f, 0.5f);
        codeRt.pivot = new Vector2(0.5f, 0.5f);
        codeRt.anchoredPosition = new Vector2(0f, -20f);
        codeRt.sizeDelta = new Vector2(1100f, 400f);
    }

    private void SetIdleVisual()
    {
        if (codeText != null) codeText.text = "";
        if (statusText != null)
        {
            statusText.text = "MAIN DISPLAY";
            statusText.color = new Color(0.4f, 0.6f, 0.9f);
        }
        if (alertGlow != null) alertGlow.color = new Color(1f, 0.2f, 0.2f, 0f);
    }

    /// <summary>Pulse the screen with an alert (red glow + audio cue).</summary>
    public void ShowAlert(float seconds)
    {
        if (alertClip != null && audioSrc != null) audioSrc.PlayOneShot(alertClip);
        if (statusText != null)
        {
            statusText.text = "INCOMING TRANSMISSION";
            statusText.color = new Color(1f, 0.6f, 0.2f);
        }
        if (glowCo != null) StopCoroutine(glowCo);
        glowCo = StartCoroutine(CoPulseGlow(seconds));
    }

    /// <summary>Display the 4-digit code in the center of the screen.</summary>
    public void ShowCode(string code)
    {
        if (codeText != null)
        {
            codeText.text = code;
            codeText.color = new Color(0.3f, 1f, 1f);
        }
        if (statusText != null)
        {
            statusText.text = "AUTH CODE — MEMORIZE";
            statusText.color = new Color(0.4f, 1f, 0.6f);
        }
        Debug.Log("[MainScreenDisplay] ShowCode('" + code + "') at world pos " + transform.position +
                  " canvas active=" + (worldCanvas != null && worldCanvas.gameObject.activeInHierarchy));
    }

    /// <summary>Hide the code and return the screen to idle.</summary>
    public void HideCode()
    {
        SetIdleVisual();
    }

    private IEnumerator CoPulseGlow(float duration)
    {
        if (alertGlow == null) yield break;
        float elapsed = 0f;
        Color baseCol = new Color(1f, 0.2f, 0.2f, 0f);
        Color peakCol = new Color(1f, 0.2f, 0.2f, 0.45f);
        float pulseRate = 4f;
        while (elapsed < duration)
        {
            float t = Mathf.PingPong(elapsed * pulseRate, 1f);
            alertGlow.color = Color.Lerp(baseCol, peakCol, t);
            elapsed += Time.deltaTime;
            yield return null;
        }
        alertGlow.color = baseCol;
        glowCo = null;
    }
}
