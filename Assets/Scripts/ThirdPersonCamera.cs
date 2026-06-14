using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.6f, 0f);

    [Header("Distance & Angles")]
    [SerializeField] private float distance = 4f;
    [SerializeField] private float minPitch = -25f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Look")]
    [SerializeField] private float yawSpeed = 0.18f;
    [SerializeField] private float pitchSpeed = 0.15f;
    [SerializeField] private float positionLerp = 18f;

    [Header("Auto-align (reduces manual camera correction)")]
    [SerializeField, Tooltip("Swing the camera behind the direction of travel when the player is moving and not using the mouse.")]
    private bool autoAlign = true;
    [SerializeField, Tooltip("Seconds of no mouse-look before auto-align engages.")]
    private float autoAlignDelay = 0.4f;
    [SerializeField, Tooltip("How fast the yaw catches up to the travel direction (higher = snappier). Smooth lerp, never snaps.")]
    private float autoAlignSpeed = 3f;
    [SerializeField, Tooltip("Minimum horizontal player speed (m/s) before auto-align engages.")]
    private float autoAlignMinSpeed = 1.2f;
    [SerializeField, Tooltip("Mouse delta magnitude treated as active manual aiming (defers auto-align).")]
    private float mouseActiveThreshold = 0.6f;

    [Header("Occlusion")]
    [SerializeField] private LayerMask occluderLayers = ~0;
    [SerializeField] private float occlusionPadding = 0.2f;

    private float yaw;
    private float pitch = 15f;
    private bool cursorLocked;
    private Rigidbody _targetRb;
    private float _lastManualLookTime = -999f;

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        if (target != null) _targetRb = target.GetComponent<Rigidbody>();
        SetCursorLock(true);
        SnapToTarget();
    }

    private void SnapToTarget()
    {
        if (target == null) return;
        Vector3 focus = target.position + targetOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = focus + rot * (Vector3.back * distance);
        transform.LookAt(focus);
    }

    public void SetTarget(Transform t)
    {
        target = t;
        _targetRb = t != null ? t.GetComponent<Rigidbody>() : null;
    }

    private void LateUpdate()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLock(!cursorLocked);

        // Only steer with the mouse while the cursor is actually locked. Anything
        // that frees the cursor (assessor pause, docking, report) stops the look.
        bool canLook = cursorLocked && Cursor.lockState == CursorLockMode.Locked && mouse != null;
        if (canLook)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * yawSpeed;
            pitch = Mathf.Clamp(pitch - d.y * pitchSpeed, minPitch, maxPitch);
            if (d.sqrMagnitude > mouseActiveThreshold * mouseActiveThreshold) _lastManualLookTime = Time.time;
        }

        if (target == null) return;

        // Auto-align: smoothly swing the yaw behind the player's travel direction
        // when they're moving and not actively aiming, so the view follows through
        // corridors and doorways without constant manual mouse correction. Holding
        // forward is stable (already aligned); mouse input always takes priority.
        if (autoAlign && Time.time - _lastManualLookTime > autoAlignDelay)
        {
            Vector3 vel = _targetRb != null ? _targetRb.linearVelocity : Vector3.zero;
            vel.y = 0f;
            if (vel.sqrMagnitude > autoAlignMinSpeed * autoAlignMinSpeed)
            {
                float desiredYaw = Mathf.Atan2(vel.x, vel.z) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, desiredYaw, autoAlignSpeed * Time.deltaTime);
            }
        }

        // Use Rigidbody interpolated position when available for jitter-free tracking.
        Vector3 targetPos = (_targetRb != null) ? _targetRb.position : target.position;
        Vector3 focus = targetPos + targetOffset;
        Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
        Vector3 desiredPos = focus + rot * (Vector3.back * distance);

        if (Physics.SphereCast(focus, occlusionPadding, desiredPos - focus, out RaycastHit hit, distance, occluderLayers, QueryTriggerInteraction.Ignore))
            desiredPos = focus + (desiredPos - focus).normalized * Mathf.Max(hit.distance - occlusionPadding, 0.5f);

        transform.position = Vector3.Lerp(transform.position, desiredPos, 1f - Mathf.Exp(-positionLerp * Time.deltaTime));
        transform.LookAt(focus);
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
