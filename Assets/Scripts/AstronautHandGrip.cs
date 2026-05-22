using UnityEngine;

/// <summary>
/// Curls the astronaut's right-hand finger bones into a grip while carrying an
/// object, and relaxes them open otherwise. The locomotion clips never key
/// RightFingers1/2, so these bones are ours to drive; we still apply the pose in
/// LateUpdate so it lands after the Animator has posed the arm each frame.
/// </summary>
[DisallowMultipleComponent]
public class AstronautHandGrip : MonoBehaviour
{
    [Header("Finger bones (auto-found by name when left empty)")]
    [SerializeField] private Transform fingers1;
    [SerializeField] private Transform fingers2;

    [Header("Grip pose — local-space curl layered on top of the rest pose")]
    [SerializeField] private Vector3 curlProximal = new Vector3(-55f, 0f, 0f);
    [SerializeField] private Vector3 curlDistal = new Vector3(-45f, 0f, 0f);

    [Header("State")]
    [Tooltip("Target grip state — driven by CarryableBattery; also exposed for tuning.")]
    [SerializeField] private bool gripping;

    [Tooltip("How fast the grip opens/closes (grip fraction per second).")]
    [SerializeField] private float gripSpeed = 8f;

    private Quaternion restProximal;
    private Quaternion restDistal;
    private bool haveBones;
    private float grip;

    /// <summary>Drive the grip open (false) or closed (true).</summary>
    public void SetGripping(bool on) => gripping = on;

    private void Awake()
    {
        if (fingers1 == null) fingers1 = FindDeep(transform, "RightFingers1");
        if (fingers2 == null) fingers2 = FindDeep(transform, "RightFingers2");
        haveBones = fingers1 != null && fingers2 != null;

        if (haveBones)
        {
            restProximal = fingers1.localRotation;
            restDistal = fingers2.localRotation;
        }
        else
        {
            Debug.LogWarning("[AstronautHandGrip] RightFingers1/RightFingers2 bones not found — grip disabled.");
        }
    }

    private void LateUpdate()
    {
        if (!haveBones) return;

        grip = Mathf.MoveTowards(grip, gripping ? 1f : 0f, gripSpeed * Time.deltaTime);
        fingers1.localRotation = restProximal * Quaternion.Euler(curlProximal * grip);
        fingers2.localRotation = restDistal * Quaternion.Euler(curlDistal * grip);
    }

    private static Transform FindDeep(Transform root, string boneName)
    {
        if (root.name == boneName) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), boneName);
            if (found != null) return found;
        }
        return null;
    }
}
