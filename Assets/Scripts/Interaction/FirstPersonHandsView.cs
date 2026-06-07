using UnityEngine;

/// <summary>
/// Runtime-only first-person hands presenter. It instantiates a visual prefab
/// under the camera and toggles it while the player is docked at a station.
/// </summary>
[DisallowMultipleComponent]
public class FirstPersonHandsView : MonoBehaviour
{
    [Header("Hands")]
    [SerializeField] private GameObject handsPrefab;
    [SerializeField] private Vector3 localPosition = new Vector3(0f, -0.54f, 0.92f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(14f, 0f, 0f);
    [SerializeField] private Vector3 localScale = Vector3.one * 26f;

    [Header("Idle Motion")]
    [SerializeField] private bool idleMotion = true;
    [SerializeField] private float motionFrequency = 0.55f;
    [SerializeField] private float bobAmplitude = 0.012f;
    [SerializeField] private float swayAmplitude = 0.016f;
    [SerializeField] private float rollAmplitude = 0.9f;

    [Header("Press Reaction")]
    [SerializeField] private float pressForwardAmount = 0.045f;
    [SerializeField] private float pressPitchDegrees = 6f;
    [SerializeField] private float pressDuration = 0.18f;

    private GameObject instance;
    private float motionStartTime;
    private float pressStartTime = float.NegativeInfinity;

    public GameObject HandsPrefab
    {
        get => handsPrefab;
        set
        {
            if (handsPrefab == value) return;
            handsPrefab = value;
            DestroyInstance();
        }
    }

    public bool HasPrefab => handsPrefab != null;
    public bool IsVisible => instance != null && instance.activeSelf;

    private void Awake()
    {
        Hide();
    }

    public void Show()
    {
        if (!EnsureInstance()) return;
        motionStartTime = Time.unscaledTime;
        ApplyLocalPose(true);
        instance.SetActive(true);
    }

    public void Hide()
    {
        if (instance != null) instance.SetActive(false);
    }

    /// <summary>Trigger a one-shot forward jab + slight pitch reaction. Called when the docked player
    /// clicks a keypad button so the hands feel connected to the input.</summary>
    public void TriggerPress()
    {
        pressStartTime = Time.unscaledTime;
    }

    private bool EnsureInstance()
    {
        if (instance != null) return true;
        if (handsPrefab == null) return false;

        instance = Instantiate(handsPrefab, transform);
        instance.name = "FirstPersonHands_View";
        DisableInteractionColliders(instance);
        ApplyLocalPose(false);
        instance.SetActive(false);
        return true;
    }

    private void LateUpdate()
    {
        if (instance != null && instance.activeSelf)
            ApplyLocalPose(true);
    }

    private void ApplyLocalPose(bool includeMotion)
    {
        if (instance == null) return;

        Vector3 position = localPosition;
        Vector3 eulerAngles = localEulerAngles;
        if (includeMotion && idleMotion)
        {
            float t = (Time.unscaledTime - motionStartTime) * motionFrequency * Mathf.PI * 2f;
            position.x += Mathf.Sin(t * 0.7f) * swayAmplitude;
            position.y += Mathf.Sin(t) * bobAmplitude;
            eulerAngles.x += Mathf.Cos(t * 0.5f) * 0.45f;
            eulerAngles.z += Mathf.Sin(t * 0.6f) * rollAmplitude;
        }

        if (includeMotion && pressDuration > 0f)
        {
            float pressElapsed = Time.unscaledTime - pressStartTime;
            if (pressElapsed >= 0f && pressElapsed <= pressDuration)
            {
                float u = pressElapsed / pressDuration;
                float bell = 4f * u * (1f - u);
                position.z += pressForwardAmount * bell;
                position.y -= pressForwardAmount * 0.35f * bell;
                eulerAngles.x += pressPitchDegrees * bell;
            }
        }

        Transform instanceTransform = instance.transform;
        instanceTransform.localPosition = position;
        instanceTransform.localRotation = Quaternion.Euler(eulerAngles);
        instanceTransform.localScale = localScale;
    }

    private static void DisableInteractionColliders(GameObject root)
    {
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;
    }

    private void DestroyInstance()
    {
        if (instance == null) return;

        if (Application.isPlaying)
            Destroy(instance);
        else
            DestroyImmediate(instance);

        instance = null;
    }
}
