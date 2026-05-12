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
