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
    [SerializeField] private Vector3 localPosition = new Vector3(0f, -0.58f, 0.85f);
    [SerializeField] private Vector3 localEulerAngles = new Vector3(4f, 0f, 0f);
    [SerializeField] private Vector3 localScale = Vector3.one * 0.35f;

    private GameObject instance;

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
        ApplyLocalPose();
        instance.SetActive(true);
    }

    public void Hide()
    {
        if (instance != null) instance.SetActive(false);
    }

    private bool EnsureInstance()
    {
        if (instance != null) return true;
        if (handsPrefab == null) return false;

        instance = Instantiate(handsPrefab, transform);
        instance.name = "FirstPersonHands_View";
        DisableInteractionColliders(instance);
        ApplyLocalPose();
        instance.SetActive(false);
        return true;
    }

    private void ApplyLocalPose()
    {
        if (instance == null) return;
        Transform t = instance.transform;
        t.localPosition = localPosition;
        t.localRotation = Quaternion.Euler(localEulerAngles);
        t.localScale = localScale;
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
