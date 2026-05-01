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
    // While docked, we disable the console's solid BoxCollider so the docked
    // player's clicks pass through to the cognitive Canvas's GraphicRaycaster
    // instead of hitting the console mesh. Restored on undock.
    private Collider _suspendedConsoleCollider;
    // The scene's PhysicsRaycaster on Camera.main intercepts UI clicks because
    // its eventMask includes the station's mesh colliders (FBX import adds
    // them automatically). Disable while docked, re-enable on undock.
    private PhysicsRaycaster _suspendedPhysicsRaycaster;

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
                    if (station != null)
                    {
                        // If the random TaskSpawnLoop hasn't yet picked this station,
                        // spawn a cognitive task on demand so docking always shows
                        // a working minigame instead of an empty console.
                        if (!station.HasActiveTask())
                        {
                            var taskGO = new GameObject(station.stationName + "_Task");
                            var task = CognitiveTaskCatalog.CreateTaskForStation(taskGO, station.stationName);
                            station.AssignTask(task);
                        }
                        EnterDock(station);
                    }
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

        station.UI?.Hide();

        var fpCamera = fpCam != null ? fpCam.GetComponent<Camera>() : null;
        (station.CurrentTask as CognitiveTaskBase)?.SetCanvasCamera(fpCamera);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EnsureEventSystem();

        _suspendedPhysicsRaycaster = null;
        if (mainCamera != null)
        {
            var pr = mainCamera.GetComponent<PhysicsRaycaster>();
            if (pr != null && pr.enabled)
            {
                pr.enabled = false;
                _suspendedPhysicsRaycaster = pr;
            }
        }

        // Disable the console's solid BoxCollider so it doesn't block UI clicks.
        // The astronaut is also frozen via ControlsEnabled=false, so collision
        // doesn't matter for the duration of the dock.
        _suspendedConsoleCollider = null;
        var visualT = station.transform.Find("Visual");
        if (visualT != null)
        {
            var col = visualT.GetComponent<BoxCollider>();
            if (col != null && col.enabled && !col.isTrigger)
            {
                col.enabled = false;
                _suspendedConsoleCollider = col;
            }
        }

        station.CurrentTask?.OnPlayerEnter();
    }

    /// <summary>Release the player from the station and restore normal play.</summary>
    public void ExitDock()
    {
        if (_state != State.Docked) return;

        var station = _currentStation;
        if (station != null && station.CurrentTask != null)
            station.CurrentTask.OnPlayerExit();

        _currentStation = null;
        _state = State.Free;

        if (player != null) player.ControlsEnabled = true;
        if (tpCam != null) tpCam.enabled = true;
        if (fpCam != null) fpCam.enabled = false;

        station?.UI?.Show();

        if (_suspendedPhysicsRaycaster != null)
        {
            _suspendedPhysicsRaycaster.enabled = true;
            _suspendedPhysicsRaycaster = null;
        }

        // Restore the console's solid collider so the player can't walk through it.
        if (_suspendedConsoleCollider != null)
        {
            _suspendedConsoleCollider.enabled = true;
            _suspendedConsoleCollider = null;
        }

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
}
