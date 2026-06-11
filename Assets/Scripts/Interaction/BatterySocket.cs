using TMPro;
using UnityEngine;

/// <summary>
/// Install point for a power cell, mounted on the Life Support console. Builds
/// its own glowing charging-pad visual. When the player is in range carrying
/// the armed battery, pressing Interact (E) slots the cell in and fires
/// OnBatteryInstalled. The pad pulses cyan while empty, steady green once filled.
/// </summary>
[DisallowMultipleComponent]
public class BatterySocket : MonoBehaviour
{
    [Header("Interaction")]
    public float interactRadius = 2.6f;

    public bool IsFilled { get; private set; }
    public Transform Slot => slotPoint;

    public event System.Action<CarryableBattery> OnBatteryInstalled;

    private static readonly Color EmptyColor = new Color(0.3f, 0.85f, 1f);
    private static readonly Color FilledColor = new Color(0.35f, 1f, 0.5f);

    private CarryableBattery expected;
    private CarryableBattery installed;
    private bool armed;
    // When set, an E press hands off to the mission (e.g. to open the wiring
    // puzzle) instead of installing immediately; the mission confirms the
    // physical install later via CompleteInstall().
    private System.Action<CarryableBattery> onInstallRequested;

    private Transform player;
    private Transform slotPoint;
    private float nextPlayerLookup;

    private Material padMat;
    private Light padLight;

    private Canvas hintCanvas;
    private TMP_Text hintLabel;
    private float hintAlpha;

    private void Awake()
    {
        BuildPad();
        BuildHint();
        SetHintAlpha(0f);
    }

    /// <summary>Arms the socket for one mission battery, clearing any previous cell.</summary>
    public void Arm(CarryableBattery battery)
    {
        Arm(battery, null);
    }

    /// <summary>
    /// Arms the socket with an install interceptor: while set, pressing E hands
    /// the request to the mission instead of installing the cell directly.
    /// </summary>
    public void Arm(CarryableBattery battery, System.Action<CarryableBattery> installInterceptor)
    {
        if (installed != null) Destroy(installed.gameObject);
        installed = null;
        expected = battery;
        armed = true;
        IsFilled = false;
        onInstallRequested = installInterceptor;
    }

    public void Disarm()
    {
        expected = null;
        armed = false;
        onInstallRequested = null;
        SetHintAlpha(0f);
    }

    /// <summary>Physically installs the armed cell — the mission's confirm step
    /// after its install interceptor (the wiring puzzle) succeeds.</summary>
    public void CompleteInstall()
    {
        Install();
    }

    /// <summary>Replaces the floating "[E] ..." prompt text shown in range.</summary>
    public void SetHintText(string text)
    {
        if (hintLabel != null) hintLabel.text = text;
    }

    private void Update()
    {
        UpdatePadGlow();

        if (!armed || IsFilled || expected == null)
        {
            hintAlpha = Mathf.MoveTowards(hintAlpha, 0f, 6f * Time.deltaTime);
            SetHintAlpha(hintAlpha);
            return;
        }

        if (player == null && Time.unscaledTime >= nextPlayerLookup)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
            nextPlayerLookup = Time.unscaledTime + 1f;
        }

        bool inRange = player != null &&
            (player.position - transform.position).sqrMagnitude <= interactRadius * interactRadius;
        bool canInstall = inRange && expected.IsCarried;

        hintAlpha = Mathf.MoveTowards(hintAlpha, canInstall ? 1f : 0f, 6f * Time.deltaTime);
        SetHintAlpha(hintAlpha);
        BillboardHint();

        if (canInstall && InteractInputBinding.InteractPressedThisFrame())
        {
            if (onInstallRequested != null) onInstallRequested(expected);
            else Install();
        }
    }

    private void Install()
    {
        if (IsFilled || expected == null) return;
        IsFilled = true;
        installed = expected;
        SetHintAlpha(0f);
        expected.InstallInto(slotPoint);
        AudioManager.Instance.PlaySfx("battery_install");
        OnBatteryInstalled?.Invoke(expected);
    }

    private void UpdatePadGlow()
    {
        if (padMat == null) return;
        float pulse = IsFilled ? 1f : 0.5f + 0.5f * Mathf.Sin(Time.time * 3f);
        Color c = IsFilled ? FilledColor : EmptyColor;
        padMat.SetColor("_EmissionColor", c * (0.6f + pulse * 1.8f));
        if (padLight != null)
        {
            padLight.color = c;
            padLight.intensity = 1f + pulse * 2.2f;
        }
    }

    private void BuildPad()
    {
        slotPoint = new GameObject("Slot").transform;
        slotPoint.SetParent(transform, false);
        slotPoint.localPosition = new Vector3(0f, 0.045f, 0f);

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "ChargingPad";
        pad.transform.SetParent(transform, false);
        pad.transform.localPosition = Vector3.zero;
        pad.transform.localScale = new Vector3(0.42f, 0.022f, 0.42f);
        var col = pad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        padMat = new Material(urpLit != null ? urpLit : Shader.Find("Standard"));
        padMat.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.09f));
        padMat.EnableKeyword("_EMISSION");
        padMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        pad.GetComponent<Renderer>().material = padMat;

        var lightGO = new GameObject("PadLight");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        padLight = lightGO.AddComponent<Light>();
        padLight.type = LightType.Point;
        padLight.range = 2.4f;
        padLight.intensity = 2f;
        padLight.color = EmptyColor;
    }

    private void BuildHint()
    {
        var canvasGO = new GameObject("SocketHintCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * 0.5f;

        hintCanvas = canvasGO.AddComponent<Canvas>();
        hintCanvas.renderMode = RenderMode.WorldSpace;
        var rt = hintCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(3.0f, 0.6f);
        canvasGO.transform.localScale = Vector3.one * 0.01f;

        var labelGO = new GameObject("HintLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        hintLabel = labelGO.AddComponent<TextMeshProUGUI>();
        hintLabel.text = "[E] INSTALL CELL";
        hintLabel.alignment = TextAlignmentOptions.Center;
        hintLabel.fontSize = 34f;
        hintLabel.fontStyle = FontStyles.Bold;
        hintLabel.color = new Color(0.4f, 1f, 0.55f, 0f);
        hintLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void SetHintAlpha(float a)
    {
        if (hintLabel == null) return;
        var c = hintLabel.color;
        c.a = a;
        hintLabel.color = c;
    }

    private void BillboardHint()
    {
        if (hintCanvas == null || hintAlpha <= 0.01f) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 fwd = hintCanvas.transform.position - cam.transform.position;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
            hintCanvas.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
}
