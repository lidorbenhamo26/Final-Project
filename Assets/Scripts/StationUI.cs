using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StationUI : MonoBehaviour
{
    [SerializeField] private TMP_Text stationNameText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Image statusLight;

    [Header("Colors")]
    [SerializeField] private Color idleColor = new Color(0.4f, 0.4f, 0.4f);
    [SerializeField] private Color activeColor = Color.green;
    [SerializeField] private Color urgentColor = Color.red;

    public void SetStationName(string n) { if (stationNameText) stationNameText.text = n; }

    public void SetInstruction(string msg)
    {
        if (instructionText) instructionText.text = msg;
        if (statusLight) statusLight.color = activeColor;
    }

    public void SetProgress(float t) { if (progressBar) progressBar.value = Mathf.Clamp01(t); }

    public void SetUrgent(bool urgent) { if (statusLight) statusLight.color = urgent ? urgentColor : activeColor; }

    public void SetIdle()
    {
        if (instructionText) instructionText.text = "STANDBY";
        if (statusLight) statusLight.color = idleColor;
        if (progressBar) progressBar.value = 0f;
    }
}
