using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class ClickHandler : MonoBehaviour
{
    [SerializeField] private float maxRayDistance = 25f;
    private Camera cam;

    private void Awake() => cam = GetComponent<Camera>();

    private void Update()
    {
        if (Mouse.current == null) return;
        if (!Mouse.current.leftButton.wasPressedThisFrame) return;

        // When cursor is locked (fly mode) aim from screen center; otherwise use mouse pos
        Vector2 screenPos = Cursor.lockState == CursorLockMode.Locked
            ? new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)
            : Mouse.current.position.ReadValue();

        Ray ray = cam.ScreenPointToRay(new Vector3(screenPos.x, screenPos.y, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance))
            hit.collider.GetComponent<ClickTarget>()?.OnClicked();
    }
}
