using TMPro;
using UnityEngine;

public class HUDManager : MonoBehaviour
{
    public static HUDManager Instance { get; private set; }

    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text taskListText;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (GameManager.Instance == null || timerText == null) return;
        float t = GameManager.Instance.MissionTimeRemaining;
        int min = Mathf.FloorToInt(t / 60f);
        int sec = Mathf.FloorToInt(t % 60f);
        timerText.text = "MISSION  " + min.ToString("00") + ":" + sec.ToString("00");
    }

    public void UpdateTaskList(string content)
    {
        if (taskListText != null) taskListText.text = content;
    }
}
