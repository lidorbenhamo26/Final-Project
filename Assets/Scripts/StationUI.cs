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

    private void Start()
    {
        // The WorldCanvas (parent of stationNameText) was built with the
        // station's local forward, which faces AWAY from the hub. From the
        // docked first-person camera (sitting on the hub side, looking at the
        // station) the camera then sees the BACK of the canvas → mirrored
        // text. Force the canvas's forward to -toHub at runtime so its FRONT
        // face renders toward the camera and reads correctly. Same logic the
        // cognitive task canvas uses in CognitiveTaskBase.BuildCanvas.
        if (stationNameText != null && stationNameText.transform.parent != null)
        {
            Transform canvasT = stationNameText.transform.parent;
            Vector3 stationPos = transform.position;
            Vector3 toHub = -new Vector3(stationPos.x, 0f, stationPos.z);
            if (toHub.sqrMagnitude < 0.0001f) toHub = -transform.forward;
            toHub.y = 0f;
            toHub.Normalize();
            canvasT.rotation = Quaternion.LookRotation(-toHub, Vector3.up);
        }
    }

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
