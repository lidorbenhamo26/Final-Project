using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class DebugCamera : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float sprintMultiplier = 3f;

    [Header("Teleport — assign station transforms in Inspector")]
    [Tooltip("1 = Engine")] [SerializeField] private Transform station1;
    [Tooltip("2 = Navigation")] [SerializeField] private Transform station2;
    [Tooltip("3 = Comms")] [SerializeField] private Transform station3;
    [Tooltip("4 = Life Support")] [SerializeField] private Transform station4;

    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float approachOffset = 2f;

    private float yaw;
    private float pitch;
    private bool cursorLocked;

    private void Start()
    {
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
        SetCursorLock(true);
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        Mouse mouse = Mouse.current;
        if (kb == null || mouse == null) return;

        if (kb.digit1Key.wasPressedThisFrame && station1 != null) TeleportTo(station1);
        if (kb.digit2Key.wasPressedThisFrame && station2 != null) TeleportTo(station2);
        if (kb.digit3Key.wasPressedThisFrame && station3 != null) TeleportTo(station3);
        if (kb.digit4Key.wasPressedThisFrame && station4 != null) TeleportTo(station4);
        if (kb.escapeKey.wasPressedThisFrame) SetCursorLock(!cursorLocked);

        if (!cursorLocked) return;

        Vector2 delta = mouse.delta.ReadValue();
        yaw += delta.x * lookSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -80f, 80f);
        transform.eulerAngles = new Vector3(pitch, yaw, 0f);

        float speed = moveSpeed * (kb.leftShiftKey.isPressed ? sprintMultiplier : 1f);
        float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
        float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
        float y = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
        transform.Translate(new Vector3(x, y, z) * speed * Time.deltaTime, Space.Self);
    }

    private void TeleportTo(Transform target)
    {
        transform.position = target.position + target.forward * approachOffset + Vector3.up * eyeHeight;
        transform.LookAt(target.position + Vector3.up);
        yaw = transform.eulerAngles.y;
        pitch = transform.eulerAngles.x;
    }

    private void SetCursorLock(bool locked)
    {
        cursorLocked = locked;
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}
