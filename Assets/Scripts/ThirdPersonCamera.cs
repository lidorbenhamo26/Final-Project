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

    [Header("Occlusion")]
    [SerializeField] private LayerMask occluderLayers = ~0;
    [SerializeField] private float occlusionPadding = 0.2f;

    private float yaw;
    private float pitch = 15f;
    private bool cursorLocked;

    private void Start()
    {
        yaw = transform.eulerAngles.y;
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

    public void SetTarget(Transform t) => target = t;

    private void LateUpdate()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame) SetCursorLock(!cursorLocked);

        if (cursorLocked && mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue();
            yaw += d.x * yawSpeed;
            pitch = Mathf.Clamp(pitch - d.y * pitchSpeed, minPitch, maxPitch);
        }

        if (target == null) return;

        Vector3 focus = target.position + targetOffset;
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
