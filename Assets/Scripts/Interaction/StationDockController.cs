using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Singleton state machine that handles docking the player at a TaskStation:
/// Free ↔ Docked. While Docked, the third-person controller and camera are
/// disabled, the cursor is freed for UI interaction, and a first-person
/// station camera is enabled. Auto-exits when the active task resolves.
/// </summary>
public class StationDockController : MonoBehaviour
{
    public enum State { Free, Docked }

    public static StationDockController Instance { get; private set; }

    [Header("Refs (auto-resolved if null)")]
    public Camera mainCamera;
    public ThirdPersonCamera tpCam;
    public FirstPersonStationCamera fpCam;
    public AstronautController player;

    private State _state = State.Free;
    private TaskStation _currentStation;
    private InputAction _interactAction;
    private bool _subscribed;

    public bool IsDocked => _state == State.Docked;
    public TaskStation CurrentStation => _currentStation;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_subscribed)
        {
            MissionTask.OnTaskResolved -= HandleTaskResolved;
            _subscribed = false;
        }
    }

    private void Start()
    {
        ResolveRefs();

        if (!_subscribed)
        {
            MissionTask.OnTaskResolved += HandleTaskResolved;
            _subscribed = true;
        }
    }

    private void ResolveRefs()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (tpCam == null) tpCam = FindObjectOfType<ThirdPersonCamera>();
        if (fpCam == null && mainCamera != null)
        {
            fpCam = mainCamera.GetComponent<FirstPersonStationCamera>();
            if (fpCam == null) fpCam = FindObjectOfType<FirstPersonStationCamera>(true);
        }
        if (player == null) player = FindObjectOfType<AstronautController>();

        // Resolve Interact action from the player's PlayerInput
        if (_interactAction == null && player != null)
        {
            var pi = player.GetComponent<PlayerInput>();
            if (pi != null && pi.actions != null)
                _interactAction = pi.actions.FindAction("Interact", throwIfNotFound: false);
        }

        // Disable fpCam at start so the third-person view is the default
        if (fpCam != null) fpCam.enabled = false;
    }

    private void Update()
    {
        if (_interactAction == null) ResolveRefs();

        bool interactPressed =
            (_interactAction != null && _interactAction.WasPressedThisFrame())
            || InteractInputBinding.InteractPressedThisFrame();

        if (_state == State.Free)
        {
            if (interactPressed)
            {
                var current = StationProximityPrompt.GetCurrent();
                if (current != null && current.IsPlayerInRange())
                {
                    var station = current.GetComponent<TaskStation>();
                    if (station != null && station.HasActiveTask())
                        EnterDock(station);
                }
            }
        }
        else // Docked
        {
            bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            if (interactPressed || escPressed)
            {
                ExitDock();
            }
        }
    }

    /// <summary>Lock the player into the given station's interaction view.</summary>
    public void EnterDock(TaskStation station)
    {
        if (station == null || _state == State.Docked) return;

        ResolveRefs();
        _currentStation = station;
        _state = State.Docked;

        if (player != null) player.ControlsEnabled = false;
        if (tpCam != null) tpCam.enabled = false;

        if (fpCam != null)
        {
            fpCam.enabled = true;
            fpCam.SetDockTarget(station.transform);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();
        EnsurePhysicsRaycaster();

        station.CurrentTask?.OnPlayerEnter();
    }

    /// <summary>Release the player from the station and restore normal play.</summary>
    public void ExitDock()
    {
        if (_state != State.Docked) return;

        if (_currentStation != null && _currentStation.CurrentTask != null)
            _currentStation.CurrentTask.OnPlayerExit();

        _currentStation = null;
        _state = State.Free;

        if (player != null) player.ControlsEnabled = true;
        if (tpCam != null) tpCam.enabled = true;
        if (fpCam != null) fpCam.enabled = false;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void HandleTaskResolved(MissionTask task, TaskResult result, float rt)
    {
        if (_state != State.Docked || _currentStation == null) return;
        // Only auto-exit if the resolved task belonged to the docked station.
        // Note: TaskStation.CurrentTask is cleared by the same event; use parent check.
        if (task != null && task.transform != null && task.transform.parent == _currentStation.transform)
        {
            ExitDock();
        }
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<StandaloneInputModule>();
    }

    private void EnsurePhysicsRaycaster()
    {
        if (mainCamera == null) return;
        var raycaster = mainCamera.GetComponent<PhysicsRaycaster>();
        if (raycaster == null) mainCamera.gameObject.AddComponent<PhysicsRaycaster>();
    }
}
