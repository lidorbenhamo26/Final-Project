using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Lives on the same GameObject as the main Camera. When enabled, takes over
/// the camera transform each LateUpdate, lerping toward a "dock pose" relative
/// to the docked station. While docked, mouse delta lets the player look
/// around within a clamped yaw/pitch range. When disabled, leaves the camera
/// untouched so ThirdPersonCamera can drive it.
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

    [Header("Look")]
    [Tooltip("Mouse yaw sensitivity (degrees per pixel).")]
    public float yawSpeed = 0.18f;
    [Tooltip("Mouse pitch sensitivity (degrees per pixel).")]
    public float pitchSpeed = 0.15f;
    [Tooltip("Max ± yaw deflection from base look (degrees).")]
    public float yawClamp = 25f;
    [Tooltip("Max ± pitch deflection from base look (degrees).")]
    public float pitchClamp = 25f;

    private Vector3 _basePos;
    private Quaternion _baseRot;
    private float _yaw;
    private float _pitch;
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

        _yaw = 0f;
        _pitch = 0f;
        _hasPose = true;
    }

    private void OnEnable()
    {
        _yaw = 0f;
        _pitch = 0f;
    }

    private void LateUpdate()
    {
        if (!_hasPose || stationTransform == null) return;

        // Mouse delta → yaw/pitch within clamp
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();
            _yaw = Mathf.Clamp(_yaw + d.x * yawSpeed, -yawClamp, yawClamp);
            _pitch = Mathf.Clamp(_pitch - d.y * pitchSpeed, -pitchClamp, pitchClamp);
        }

        Quaternion offsetRot = Quaternion.Euler(_pitch, _yaw, 0f);
        Quaternion targetRot = _baseRot * offsetRot;

        float t = 1f - Mathf.Exp(-lerpRate * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, _basePos, t);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
    }
}
