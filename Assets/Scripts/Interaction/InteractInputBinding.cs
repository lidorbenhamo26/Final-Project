using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Ensures the Player's PlayerInput has an "Interact" action bound to E.
/// If the action map doesn't already contain it, the action is added at
/// runtime so the dock flow works regardless of the project's
/// InputActionAsset state. Also exposes a static helper the
/// StationDockController can call as a fallback.
/// </summary>
[DisallowMultipleComponent]
public class InteractInputBinding : MonoBehaviour
{
    [Header("Binding")]
    [Tooltip("Name of the action map on the PlayerInput's actions asset.")]
    public string actionMapName = "Player";

    [Tooltip("Name of the Interact action.")]
    public string actionName = "Interact";

    [Tooltip("Input System binding path.")]
    public string bindingPath = "<Keyboard>/e";

    private static InputAction s_interactAction;

    private void Awake()
    {
        var go = GameObject.FindGameObjectWithTag("Player");
        if (go == null) return;

        var pi = go.GetComponent<PlayerInput>();
        if (pi == null || pi.actions == null) return;

        var map = pi.actions.FindActionMap(actionMapName, throwIfNotFound: false);
        if (map == null) return;

        var existing = map.FindAction(actionName);
        if (existing == null)
        {
            map.Disable();
            var act = map.AddAction(actionName, InputActionType.Button);
            act.AddBinding(bindingPath);
            map.Enable();
            existing = act;
        }

        s_interactAction = existing;
        if (!s_interactAction.enabled) s_interactAction.Enable();
    }

    private void OnDestroy()
    {
        // Keep the action live for other systems; just drop the static ref if it points at us.
        // (No per-instance state to clean up.)
    }

    /// <summary>
    /// True for one frame whenever the Interact action transitions to pressed.
    /// Falls back to a direct keyboard E check if the action wasn't resolved
    /// (e.g. this component isn't in the scene).
    /// </summary>
    public static bool InteractPressedThisFrame()
    {
        if (s_interactAction != null && s_interactAction.enabled)
            return s_interactAction.WasPressedThisFrame();

        var kb = Keyboard.current;
        return kb != null && kb.eKey.wasPressedThisFrame;
    }
}
