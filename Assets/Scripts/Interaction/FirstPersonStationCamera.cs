using UnityEngine;

/// <summary>
/// Lives on the same GameObject as the main Camera. When enabled, takes over
/// the camera transform each LateUpdate, lerping toward a static "dock pose"
/// relative to the docked station, then holding that pose. The mouse is left
/// entirely to the UI cursor so the player can click worldspace task buttons
/// without the view sliding underneath their pointer. When disabled, leaves
/// the camera untouched so ThirdPersonCamera can drive it.
/// </summary>
[DisallowMultipleComponent]
public class FirstPersonStationCamera : MonoBehaviour
{
    [Header("Dock Pose")]
    public Transform stationTransform;
    [Tooltip("Position offset relative to the station forward axis (used if no DockPoint child).")]
    public Vector3 dockOffset = new Vector3(0f, 1.6f, -0.7f);
    [Tooltip("Look-at offset relative to station origin.")]
    public Vector3 lookOffset = new Vector3(0f, 1.2f, 0f);

    [Header("Smoothing")]
    [Tooltip("Position/rotation lerp rate. Higher = snappier.")]
    public float lerpRate = 12f;

    private Vector3 _basePos;
    private Quaternion _baseRot;
    private bool _hasPose;

    /// <summary>Snap & set the dock pose. Call when entering dock.</summary>
    public void SetDockTarget(Transform t)
    {
        stationTransform = t;
        if (t == null) { _hasPose = false; return; }

        Transform dockPoint = t.Find("DockPoint");
        if (dockPoint != null)
        {
            _basePos = dockPoint.position;
            _baseRot = dockPoint.rotation;
        }
        else
        {
            _basePos = t.position + t.forward * dockOffset.z + Vector3.up * dockOffset.y + t.right * dockOffset.x;
            Vector3 lookAt = t.position + Vector3.up * lookOffset.y + t.forward * lookOffset.z + t.right * lookOffset.x;
            Vector3 dir = (lookAt - _basePos);
            if (dir.sqrMagnitude < 0.0001f) dir = t.forward;
            _baseRot = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        _hasPose = true;
    }

    private void LateUpdate()
    {
        if (!_hasPose || stationTransform == null) return;

        float t = 1f - Mathf.Exp(-lerpRate * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, _basePos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, _baseRot, t);
    }
}
