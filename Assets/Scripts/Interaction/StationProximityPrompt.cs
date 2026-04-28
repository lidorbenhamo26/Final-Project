using TMPro;
using UnityEngine;

/// <summary>
/// Lives on the same GameObject as a TaskStation. Uses a sphere check (default 2.5m)
/// to detect when the local Player is in range. When in range AND no station is currently
/// docked AND the station has an active task, registers itself as the global "current"
/// prompt that the StationDockController polls. Optionally renders a floating
/// world-space "[E] INTERACT" hint above the station that fades in/out.
/// </summary>
[DisallowMultipleComponent]
public class StationProximityPrompt : MonoBehaviour
{
    [Header("Proximity")]
    [Tooltip("Radius (m) within which the [E] hint shows and the player can dock.")]
    public float promptRadius = 2.5f;

    [Tooltip("Vertical offset above the station for the floating hint.")]
    public float hintHeight = 1.8f;

    [Tooltip("How fast the hint label fades in/out (alpha units per second).")]
    public float fadeRate = 6f;

    [Header("Hint (optional — auto-created if null)")]
    [SerializeField] private TMP_Text hintLabel;
    [SerializeField] private Canvas hintCanvas;

    [Tooltip("Text shown when in range. Leave default unless re-binding the key.")]
    public string hintText = "[E] INTERACT";

    private static StationProximityPrompt CurrentPrompt;

    private TaskStation _station;
    private Transform _player;
    private float _alpha;
    private bool _inRangeCached;

    /// <summary>Returns the prompt the player is currently in range of (may be null).</summary>
    public static StationProximityPrompt GetCurrent() => CurrentPrompt;

    private void Awake()
    {
        _station = GetComponent<TaskStation>();
        if (hintLabel == null) BuildHint();
        SetHintAlpha(0f);
    }

    private void OnDisable()
    {
        if (CurrentPrompt == this) CurrentPrompt = null;
    }

    private void OnDestroy()
    {
        if (CurrentPrompt == this) CurrentPrompt = null;
    }

    private void Update()
    {
        if (_player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) _player = go.transform;
        }

        bool inRange = IsPlayerInRange();
        bool stationDocked = StationDockController.Instance != null && StationDockController.Instance.IsDocked;
        bool hasTask = _station != null && _station.HasActiveTask();

        bool shouldPrompt = inRange && !stationDocked && hasTask;

        if (shouldPrompt)
        {
            CurrentPrompt = this;
        }
        else if (CurrentPrompt == this)
        {
            CurrentPrompt = null;
        }

        // Fade label alpha toward target
        float target = shouldPrompt ? 1f : 0f;
        _alpha = Mathf.MoveTowards(_alpha, target, fadeRate * Time.deltaTime);
        SetHintAlpha(_alpha);

        // Billboard the hint canvas toward the camera
        if (hintCanvas != null && _alpha > 0.01f)
        {
            hintCanvas.transform.position = transform.position + Vector3.up * hintHeight;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 fwd = hintCanvas.transform.position - cam.transform.position;
                fwd.y = 0f;
                if (fwd.sqrMagnitude > 0.0001f)
                    hintCanvas.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
            }
        }

        _inRangeCached = inRange;
    }

    /// <summary>True if the player is within promptRadius of this station.</summary>
    public bool IsPlayerInRange()
    {
        if (_player == null) return false;
        float r2 = promptRadius * promptRadius;
        return (_player.position - transform.position).sqrMagnitude <= r2;
    }

    private void BuildHint()
    {
        var canvasGO = new GameObject("ProximityHintCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * hintHeight;

        hintCanvas = canvasGO.AddComponent<Canvas>();
        hintCanvas.renderMode = RenderMode.WorldSpace;
        var rt = hintCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2.5f, 0.6f);
        canvasGO.transform.localScale = Vector3.one * 0.01f;

        var labelGO = new GameObject("HintLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var labelRT = labelGO.AddComponent<RectTransform>();
        labelRT.anchorMin = Vector2.zero;
        labelRT.anchorMax = Vector2.one;
        labelRT.offsetMin = Vector2.zero;
        labelRT.offsetMax = Vector2.zero;

        hintLabel = labelGO.AddComponent<TextMeshProUGUI>();
        hintLabel.text = hintText;
        hintLabel.alignment = TextAlignmentOptions.Center;
        hintLabel.fontSize = 36f;
        hintLabel.color = new Color(1f, 1f, 1f, 0f);
        hintLabel.enableWordWrapping = false;
    }

    private void SetHintAlpha(float a)
    {
        if (hintLabel == null) return;
        var c = hintLabel.color;
        c.a = a;
        hintLabel.color = c;
        if (hintLabel.text != hintText) hintLabel.text = hintText;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, promptRadius);
    }
}
