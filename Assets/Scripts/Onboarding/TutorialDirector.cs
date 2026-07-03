using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Drives the onboarding tutorial inside a copy of the real ship (TutorialScene
/// is duplicated from MainScene). Teaches the controls by making the player
/// actually use each one (walking/sprinting a real distance, jumping), teaches
/// the ship layout and station names, then has the player dock and complete a
/// trivial practice task at TWO stations, and finally return to the central hub.
/// Nothing auto-passes — each step must be demonstrated. A Skip button lets
/// returning users jump straight to the briefing.
///
/// Flow: walk Xm -> sprint Ym -> jump xN -> tour (names all 4) ->
///       walk to Station 1 -> dock -> practice -> walk to Station 2 -> dock ->
///       practice -> return to central hub -> finish.
/// </summary>
public class TutorialDirector : MonoBehaviour
{
    [Header("Movement gates (metres / count)")]
    [SerializeField, Tooltip("Accumulated walking distance required to clear the walk step.")]
    private float walkDistance = 3.5f;
    [SerializeField, Tooltip("Accumulated sprinting distance required to clear the sprint step.")]
    private float sprintDistance = 5.5f;
    [SerializeField, Tooltip("Horizontal speed above which movement counts as sprinting for the sprint gate.")]
    private float sprintSpeedThreshold = 3.5f;
    [SerializeField, Tooltip("Number of jumps required to clear the jump step.")]
    private int requiredJumps = 3;

    [Header("Arrival radii (metres)")]
    [SerializeField, Tooltip("How close the player must get to a station to count as 'reached'.")]
    private float stationArriveRadius = 2.6f;
    [SerializeField, Tooltip("How close to the hub centre counts as 'returned'.")]
    private float hubReturnRadius = 3.5f;

    [Header("Stations (auto-found if empty)")]
    [SerializeField] private TaskStation station1Override; // defaults to Engine
    [SerializeField] private TaskStation station2Override; // defaults to Comms

    private static readonly Color PromptColor    = Color.white;
    private static readonly Color HelperColor     = new Color(0.70f, 0.78f, 0.88f, 1f);
    private static readonly Color ProgressColor   = new Color(0.55f, 0.62f, 0.72f, 0.9f);
    private static readonly Color SkipColor        = new Color(0.30f, 0.34f, 0.40f, 1f);
    private static readonly Color CheckDoneColor   = new Color(0.35f, 0.95f, 0.55f, 1f);
    private static readonly Color CheckPendColor   = new Color(0.55f, 0.62f, 0.72f, 0.7f);

    private const float TourSecondsEach = 1.8f;

    private struct Step
    {
        public string Prompt;
        public string Helper;
        public Func<TutorialDirector, bool> IsComplete;
    }

    // Step indices (keep in sync with BuildSteps()).
    private const int StepMove        = 0;
    private const int StepSprint      = 1;
    private const int StepJump        = 2;
    private const int StepTour        = 3;
    private const int StepBatteryWalk = 4;
    private const int StepBatteryGrab = 5;
    private const int StepWalk1       = 6;
    private const int StepDock1       = 7;
    private const int StepTask1       = 8;
    private const int StepWalk2       = 9;
    private const int StepDock2       = 10;
    private const int StepTask2       = 11;
    private const int StepReturn      = 12;

    private AstronautController player;
    private Rigidbody playerRb;
    private Transform playerT;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private TaskStation station1;
    private TaskStation station2;
    private string station1Label = "ENGINE";
    private string station2Label = "COMMS";
    private readonly List<(TaskStation station, string label)> tourStations = new();
    private Transform hubMarker;

    private TMP_Text promptLbl;
    private TMP_Text helperLbl;
    private TMP_Text progressLbl;
    private TMP_Text[] checklistLbls;
    private readonly bool[] checklistDone = new bool[7];
    private static readonly string[] ChecklistNames = { "WALK", "SPRINT", "JUMP", "BATTERY", "STATION 1", "STATION 2", "RETURN" };
    private TutorialHighlight highlight;

    private List<Step> steps;
    private int stepIndex = -1;

    private float walkAccum;
    private float sprintAccum;
    private Vector3 lastPos;
    private bool hasLastPos;
    private int jumpCount;
    private float jumpCooldown;
    private bool taskResolvedThisStep;
    private bool tourComplete;
    private float tourTimer;
    private int tourIndex = -1;
    private bool finished;
    private GameObject introRoot; // full-screen "get ready" card; destroyed on START
    private ThirdPersonCamera tpCam; // third-person camera; its mouse-look is paused while the card is up

    // Battery teach-step state.
    private Transform batteryMarker;     // marks the storage rack / cell location
    private GameObject batteryCell;      // spawned practice cell
    private CarryableBattery batteryCarryable;
    private bool batteryGrabbed;
    private bool batterySetupFailed;     // scene lacks rack/prefab -> auto-pass the steps
    private float batteryArriveRadius = 2.6f;

    private void Start()
    {
        BuildOverlay();
        BuildHighlight();
        ResolveRefs();
        DiscoverStations();
        EnsurePlayerAnimator();
        BuildSteps();
        MissionTask.OnTaskResolved += HandleTaskResolved;
        ShowIntroCard();
    }

    private void OnDestroy()
    {
        MissionTask.OnTaskResolved -= HandleTaskResolved;
        if (batteryCarryable != null) batteryCarryable.OnPickedUp -= HandleBatteryGrabbed;
    }

    private void EnsurePlayerAnimator()
    {
        if (player == null) return;
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.enabled = true;
    }

    private void Update()
    {
        if (finished || steps == null || stepIndex < 0 || stepIndex >= steps.Count) return;

        TrackMovement();
        if (stepIndex == StepJump) TickJumpCount();
        if (stepIndex == StepTour) TickTour();
        if (stepIndex == StepBatteryWalk) EnsureBatteryCell();

        if (steps[stepIndex].IsComplete(this)) Advance();
    }

    private void HandleTaskResolved(MissionTask t, TaskResult r, float rt) => taskResolvedThisStep = true;

    // ---------------------------------------------------------------- refs ---
    private void ResolveRefs()
    {
        player = FindAnyObjectByType<AstronautController>();
        if (player != null)
        {
            playerRb = player.GetComponent<Rigidbody>();
            playerT = player.transform;
            var input = player.GetComponent<PlayerInput>();
            if (input != null && input.actions != null)
            {
                moveAction   = input.actions.FindAction("Move",   throwIfNotFound: false);
                sprintAction = input.actions.FindAction("Sprint", throwIfNotFound: false);
                jumpAction   = input.actions.FindAction("Jump",   throwIfNotFound: false);
            }
        }

        var cam = Camera.main;
        if (cam != null && playerT != null)
        {
            var tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc != null) tpc.SetTarget(playerT);
        }

        // Marker at the hub centre (world origin) for the return waypoint.
        var markerGo = new GameObject("TutorialHubMarker");
        markerGo.transform.SetParent(transform, false);
        markerGo.transform.position = Vector3.zero;
        hubMarker = markerGo.transform;
    }

    private void DiscoverStations()
    {
        var all = FindObjectsByType<TaskStation>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        TaskStation engine = null, nav = null, comms = null, life = null;
        foreach (var s in all)
        {
            string n = (s.stationName ?? s.name).ToLowerInvariant();
            if (engine == null && n.Contains("engine")) engine = s;
            else if (nav == null && n.Contains("nav")) nav = s;
            else if (comms == null && n.Contains("comm")) comms = s;
            else if (life == null && (n.Contains("life") || n.Contains("support"))) life = s;
        }

        AddTour(engine, "ENGINE");
        AddTour(nav, "NAVIGATION");
        AddTour(comms, "COMMS");
        AddTour(life, "LIFE SUPPORT");

        station1 = station1Override != null ? station1Override : engine;
        station2 = station2Override != null ? station2Override : comms;
        // Fallbacks so the tutorial still works if a station is missing.
        if (station1 == null && all.Length > 0) station1 = all[0];
        if (station2 == null && all.Length > 1) station2 = all[1];
        else if (station2 == null) station2 = station1;

        // Use the spaced/proper display name (PrettyStation), not the raw
        // serialized id, so labels read "ENGINE" not "ENGINESTATION".
        if (station1 != null) station1Label = TaskListHUD.PrettyStation(station1.stationName).ToUpperInvariant();
        if (station2 != null) station2Label = TaskListHUD.PrettyStation(station2.stationName).ToUpperInvariant();
    }

    private void AddTour(TaskStation s, string label)
    {
        if (s != null) tourStations.Add((s, label));
    }

    // --------------------------------------------------------------- steps ---
    private void BuildSteps()
    {
        steps = new List<Step>
        {
            new Step {
                Prompt = "MOVE  —  PRESS  W A S D  OR  ARROW KEYS",
                Helper = "Objective: walk around the hub to get used to the controls.",
                IsComplete = d => d.walkAccum >= d.walkDistance,
            },
            new Step {
                Prompt = "SPRINT  —  HOLD  SHIFT  WHILE MOVING",
                Helper = "Objective: run across the hub.",
                IsComplete = d => d.sprintAccum >= d.sprintDistance,
            },
            new Step {
                Prompt = "JUMP  —  PRESS  SPACE",
                Helper = "Objective: jump a few times.",
                IsComplete = d => d.jumpCount >= d.requiredJumps,
            },
            new Step {
                Prompt = "SHIP TOUR  —  LEARN YOUR FOUR STATIONS",
                Helper = "Watch each doorway light up. These are the four stations tasks will route to.",
                IsComplete = d => d.tourComplete,
            },
            new Step {
                Prompt = "WALK TO THE LIFE SUPPORT POWER CELL",
                Helper = "This is the Life Support battery, in central storage. Follow the marker to it.",
                IsComplete = d => d.batterySetupFailed || d.DistanceToPoint(d.batteryMarker) < d.batteryArriveRadius,
            },
            new Step {
                Prompt = "PRESS  E  TO TAKE THE POWER CELL",
                Helper = "Grab a cell so you know how. In the mission, carry one here to Life Support when power runs low.",
                IsComplete = d => d.batterySetupFailed || d.batteryGrabbed,
            },
            new Step {
                Prompt = "WALK TO THE " + " STATION", // patched in Advance with station1Label
                Helper = "Objective: follow the marker through the doorway to the station.",
                IsComplete = d => d.DistanceTo(d.station1) < d.stationArriveRadius,
            },
            new Step {
                Prompt = "PRESS  E  TO DOCK AT THE CONSOLE",
                Helper = "Stand at the console and press E to engage.",
                IsComplete = d => d.EnsurePractice(d.station1, 0) && d.IsDockedAt(d.station1),
            },
            new Step {
                Prompt = "COMPLETE THE PRACTICE TASK",
                Helper = "Do the quick action shown on the console.",
                IsComplete = d => d.taskResolvedThisStep,
            },
            new Step {
                Prompt = "WALK TO THE " + " STATION", // patched with station2Label
                Helper = "Objective: now head to the next station.",
                IsComplete = d => d.DistanceTo(d.station2) < d.stationArriveRadius,
            },
            new Step {
                Prompt = "PRESS  E  TO DOCK AT THE CONSOLE",
                Helper = "Engage the second console with E.",
                IsComplete = d => d.EnsurePractice(d.station2, 1) && d.IsDockedAt(d.station2),
            },
            new Step {
                Prompt = "COMPLETE THE PRACTICE TASK",
                Helper = "One more quick action.",
                IsComplete = d => d.taskResolvedThisStep,
            },
            new Step {
                Prompt = "RETURN TO THE CENTRAL HUB TO FINISH",
                Helper = "Objective: head back to the room in the middle of the ship.",
                IsComplete = d => d.DistanceToHub() < d.hubReturnRadius,
            },
        };
    }

    private void Advance()
    {
        MarkChecklistForCompletedStep(stepIndex);

        stepIndex++;
        walkAccum = 0f;
        sprintAccum = 0f;
        jumpCount = 0;
        taskResolvedThisStep = false;
        tourComplete = false;
        tourTimer = 0f;
        tourIndex = -1;

        if (stepIndex >= steps.Count) { Finish(); return; }

        promptLbl.text = ResolvePrompt(stepIndex);
        helperLbl.text = steps[stepIndex].Helper;
        progressLbl.text = (stepIndex + 1) + " / " + steps.Count;
        UpdateHighlight();
    }

    // Fill in the station names for the walk-to-station prompts.
    private string ResolvePrompt(int i)
    {
        if (i == StepWalk1) return "WALK TO THE " + station1Label + " STATION";
        if (i == StepWalk2) return "WALK TO THE " + station2Label + " STATION";
        return steps[i].Prompt;
    }

    private void MarkChecklistForCompletedStep(int completed)
    {
        switch (completed)
        {
            case StepMove:        SetCheck(0); break;
            case StepSprint:      SetCheck(1); break;
            case StepJump:        SetCheck(2); break;
            case StepBatteryGrab: SetCheck(3); break;
            case StepTask1:       SetCheck(4); break;
            case StepTask2:       SetCheck(5); break;
            case StepReturn:      SetCheck(6); break;
        }
    }

    private void SetCheck(int i)
    {
        if (i < 0 || i >= checklistDone.Length) return;
        checklistDone[i] = true;
        RefreshChecklist();
    }

    private void RefreshChecklist()
    {
        if (checklistLbls == null) return;
        for (int i = 0; i < checklistLbls.Length; i++)
        {
            if (checklistLbls[i] == null) continue;
            bool done = checklistDone[i];
            checklistLbls[i].text = (done ? "[X]  " : "[  ]  ") + ChecklistNames[i];
            checklistLbls[i].color = done ? CheckDoneColor : CheckPendColor;
        }
    }

    private void UpdateHighlight()
    {
        if (highlight == null) return;
        switch (stepIndex)
        {
            case StepBatteryWalk:
                EnsureBatteryCell();
                if (batteryMarker != null) highlight.SetTarget(batteryMarker, "POWER CELL", "WALK HERE"); else highlight.Hide();
                break;
            case StepBatteryGrab:
                if (batteryMarker != null) highlight.SetTarget(batteryMarker, "POWER CELL", "PRESS  E"); else highlight.Hide();
                break;
            case StepWalk1:
                if (station1 != null) highlight.SetTarget(station1.transform, station1Label, "WALK HERE"); else highlight.Hide();
                break;
            case StepDock1:
                if (station1 != null) highlight.SetTarget(station1.transform, station1Label, "DOCK HERE"); else highlight.Hide();
                break;
            case StepWalk2:
                if (station2 != null) highlight.SetTarget(station2.transform, station2Label, "WALK HERE"); else highlight.Hide();
                break;
            case StepDock2:
                if (station2 != null) highlight.SetTarget(station2.transform, station2Label, "DOCK HERE"); else highlight.Hide();
                break;
            case StepReturn:
                if (hubMarker != null) highlight.SetTarget(hubMarker, "CENTRAL HUB", "RETURN HERE"); else highlight.Hide();
                break;
            case StepTour:
                break; // managed in TickTour
            default:
                highlight.Hide();
                break;
        }
    }

    // --------------------------------------------------------------- ticks ---
    private void TrackMovement()
    {
        if (playerT == null) return;
        float frameDist = 0f;
        if (hasLastPos)
        {
            Vector3 cur = playerT.position, prev = lastPos;
            cur.y = 0f; prev.y = 0f;
            frameDist = Vector3.Distance(cur, prev);
        }
        lastPos = playerT.position;
        hasLastPos = true;

        if (stepIndex == StepMove)
        {
            walkAccum += frameDist;
            if (helperLbl != null) helperLbl.text = "Walk  " + Mathf.Min(walkAccum, walkDistance).ToString("0.0") + " / " + walkDistance.ToString("0") + " m";
            UpdateProgressOnChecklist(0, walkAccum, walkDistance);
        }
        else if (stepIndex == StepSprint)
        {
            if (HorizSpeed() >= sprintSpeedThreshold) sprintAccum += frameDist;
            if (helperLbl != null) helperLbl.text = "Sprint  " + Mathf.Min(sprintAccum, sprintDistance).ToString("0.0") + " / " + sprintDistance.ToString("0") + " m";
            UpdateProgressOnChecklist(1, sprintAccum, sprintDistance);
        }
    }

    private void UpdateProgressOnChecklist(int i, float value, float target)
    {
        if (checklistLbls == null || i >= checklistLbls.Length || checklistLbls[i] == null) return;
        if (checklistDone[i]) return;
        checklistLbls[i].text = "[  ]  " + ChecklistNames[i] + "  " + Mathf.Min(value, target).ToString("0.0") + "/" + target.ToString("0") + "m";
    }

    private void TickJumpCount()
    {
        if (jumpCooldown > 0f) jumpCooldown -= Time.deltaTime;
        bool grounded = playerRb != null && Mathf.Abs(playerRb.linearVelocity.y) < 0.6f;
        if (JumpPressedThisFrame() && grounded && jumpCooldown <= 0f)
        {
            jumpCount++;
            jumpCooldown = 0.25f;
            if (helperLbl != null) helperLbl.text = "Jumps  " + Mathf.Min(jumpCount, requiredJumps) + " / " + requiredJumps;
        }
    }

    private void TickTour()
    {
        if (tourStations.Count == 0) { tourComplete = true; return; }
        tourTimer += Time.deltaTime;
        int idx = Mathf.FloorToInt(tourTimer / TourSecondsEach);
        if (idx >= tourStations.Count) { highlight.Hide(); tourComplete = true; return; }
        if (idx != tourIndex)
        {
            tourIndex = idx;
            var entry = tourStations[idx];
            highlight.SetTarget(entry.station.transform, entry.label, "STATION");
            if (helperLbl != null) helperLbl.text = entry.label + "  —  " + TourPurpose(entry.label);
        }
    }

    // One-line purpose per station so the tour teaches what each is for, not just
    // where it is.
    private static string TourPurpose(string label)
    {
        switch (label)
        {
            case "ENGINE":        return "reactor & memory tasks";
            case "NAVIGATION":    return "scanning & course tasks";
            case "COMMS":         return "signal & attention tasks";
            case "LIFE SUPPORT":  return "keep the crew's air & power up";
        }
        return "a mission station";
    }

    // Spawns the practice power cell at the central storage rack the first time the
    // battery step is reached. If the scene has no rack or the cell prefab is
    // missing, flags setupFailed so the two battery steps auto-pass (no soft-lock).
    private void EnsureBatteryCell()
    {
        if (batteryCell != null || batterySetupFailed || batteryMarker != null) return;

        var rack = FindAnyObjectByType<BatteryStorageRack>();
        if (rack == null) { batterySetupFailed = true; return; }

        Vector3 spawnPos = rack.SpawnPosition;
        Quaternion spawnRot = rack.SpawnRotation;

        var markerGo = new GameObject("TutorialBatteryMarker");
        markerGo.transform.SetParent(transform, false);
        markerGo.transform.position = spawnPos;
        batteryMarker = markerGo.transform;

        var prefab = Resources.Load<GameObject>("BatteryCell");
        if (prefab == null) { batterySetupFailed = true; return; }

        batteryCell = Instantiate(prefab, spawnPos, spawnRot);
        batteryCell.name = "TutorialPowerCell";
        batteryCarryable = batteryCell.GetComponent<CarryableBattery>();
        if (batteryCarryable == null) batteryCarryable = batteryCell.AddComponent<CarryableBattery>();
        batteryCarryable.OnPickedUp += HandleBatteryGrabbed;
    }

    private void HandleBatteryGrabbed(CarryableBattery b)
    {
        batteryGrabbed = true;
        if (helperLbl != null) helperLbl.text = "Got it — that's the Life Support cell. Now back to the stations.";
    }

    // Create a trivial practice task on the station if it has none yet. Returns
    // true once the station has an active task (so docking is permitted).
    private bool EnsurePractice(TaskStation s, int mode)
    {
        if (s == null) return false;
        if (s.HasActiveTask()) return true;
        var go = new GameObject("TutorialPractice");
        var task = go.AddComponent<TutorialPracticeTask>();
        task.practiceMode = mode;
        s.AssignTask(task);
        return true;
    }

    private bool IsDockedAt(TaskStation s)
    {
        var dock = StationDockController.Instance;
        return dock != null && dock.IsDocked && (s == null || dock.CurrentStation == s);
    }

    private void Finish()
    {
        if (finished) return;
        finished = true;
        SessionContext.Instance.TutorialCompleted = true;
        if (highlight != null) highlight.Hide();
        promptLbl.text = "TUTORIAL COMPLETE";
        helperLbl.text = "Returning to briefing...";
        progressLbl.text = "";
        Invoke(nameof(ReturnToBriefing), 1.4f);
    }

    private void Skip()
    {
        if (finished) return;
        finished = true;
        SessionContext.Instance.TutorialCompleted = true;
        ReturnToBriefing();
    }

    private void ReturnToBriefing() => SceneTransition.LoadStart();

    // --------------------------------------------------------------- input ---
    private bool JumpPressedThisFrame()
    {
        if (jumpAction != null && jumpAction.WasPressedThisFrame()) return true;
        var k = Keyboard.current;
        return k != null && k.spaceKey.wasPressedThisFrame;
    }

    private float HorizSpeed()
    {
        if (playerRb == null) return 0f;
        var v = playerRb.linearVelocity;
        return new Vector2(v.x, v.z).magnitude;
    }

    private float DistanceTo(TaskStation s)
    {
        if (s == null || playerT == null) return float.MaxValue;
        Vector3 a = playerT.position, b = s.transform.position;
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    private float DistanceToHub()
    {
        if (playerT == null) return float.MaxValue;
        Vector3 a = playerT.position; a.y = 0f;
        return a.magnitude; // hub centre is world origin
    }

    private float DistanceToPoint(Transform t)
    {
        if (t == null || playerT == null) return float.MaxValue;
        Vector3 a = playerT.position, b = t.position;
        a.y = 0f; b.y = 0f;
        return Vector3.Distance(a, b);
    }

    // -------------------------------------------------------------- overlay ---
    private void BuildHighlight()
    {
        var hGO = new GameObject("TutorialHighlight");
        hGO.transform.SetParent(transform, false);
        highlight = hGO.AddComponent<TutorialHighlight>();
    }

    private void BuildOverlay()
    {
        var canvasGO = new GameObject("Tutorial_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();

        var panel = UIChrome.BuildScifiPanel(canvasGO.transform, new Vector2(1100f, 140f), new Vector2(0f, 400f));
        panel.gameObject.name = "TutorialPrompt";

        promptLbl = SpawnLabel(panel, "", 32, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 22f), new Vector2(1040f, 44f), PromptColor);
        helperLbl = SpawnLabel(panel, "", 18, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0f, -16f), new Vector2(1040f, 24f), HelperColor);
        progressLbl = SpawnLabel(panel, "", 14, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, -52f), new Vector2(200f, 20f), ProgressColor);

        BuildChecklist(canvasGO.transform);
        BuildSkipButton(canvasGO.transform);
    }

    // ---------------------------------------------------------------- intro ---
    // One-time "get ready" card shown the moment TutorialScene loads, so the jump
    // from the registration form into hands-on onboarding isn't abrupt. The tutorial
    // itself doesn't begin (Advance) until the player presses START TUTORIAL.
    private void ShowIntroCard()
    {
        EnsureEventSystem(); // without this, START (and the Skip button) can't be clicked
        var canvasGO = new GameObject("TutorialIntro_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120; // above the tutorial overlay (50) and its Skip button
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasGO.AddComponent<GraphicRaycaster>();
        introRoot = canvasGO;

        // Full-screen dim that also blocks the Skip button while the card is up.
        var dim = NewRect("Dim", canvasGO.transform, Vector2.zero, Vector2.zero);
        dim.anchorMin = Vector2.zero; dim.anchorMax = Vector2.one;
        dim.offsetMin = Vector2.zero; dim.offsetMax = Vector2.zero;
        var dimImg = dim.gameObject.AddComponent<Image>();
        dimImg.color = new Color(0.015f, 0.03f, 0.05f, 0.92f);
        dimImg.raycastTarget = true;

        var panel = UIChrome.BuildScifiPanel(canvasGO.transform, new Vector2(1080f, 460f), Vector2.zero);
        panel.gameObject.name = "TutorialIntroCard";

        SpawnLabel(panel, "TUTORIAL", 46, FontStyles.Bold, TextAlignmentOptions.Center,
            new Vector2(0f, 150f), new Vector2(1000f, 64f), PromptColor);
        SpawnLabel(panel,
            "Before your mission, a short hands-on tutorial will teach you the controls and your four stations.",
            24, FontStyles.Normal, TextAlignmentOptions.Center,
            new Vector2(0f, 36f), new Vector2(940f, 130f), HelperColor);
        SpawnLabel(panel, "Take your time — nothing here is timed.",
            22, FontStyles.Italic, TextAlignmentOptions.Center,
            new Vector2(0f, -66f), new Vector2(940f, 40f), ProgressColor);

        BuildIntroStartButton(panel);

        // Freeze roaming and free the cursor so START is clickable. The scene otherwise
        // locks the mouse for camera look, which is why clicking first needed an ESC.
        SetGameplayFrozen(true);
    }

    private void BuildIntroStartButton(Transform parent)
    {
        var rt = NewRect("StartButton", parent, new Vector2(340f, 64f), new Vector2(0f, -160f));
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.20f, 0.65f, 1.00f, 1f);
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(BeginTutorial);

        var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero);
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = "START TUTORIAL";
        lbl.fontSize = 24;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
    }

    private void BeginTutorial()
    {
        if (introRoot != null) { Destroy(introRoot); introRoot = null; }
        SetGameplayFrozen(false);
        Advance(); // start step 0
    }

    // Freeze/unfreeze roaming while the intro card is up: stop the astronaut, pause the
    // third-person camera's mouse-look, and free the cursor so the START button is clickable.
    // Mirrors how StationDockController hands the cursor to UI while the player is docked.
    private void SetGameplayFrozen(bool frozen)
    {
        if (tpCam == null && Camera.main != null) tpCam = Camera.main.GetComponent<ThirdPersonCamera>();
        if (tpCam == null) tpCam = FindAnyObjectByType<ThirdPersonCamera>();

        if (player != null) player.ControlsEnabled = !frozen;
        if (tpCam != null) tpCam.enabled = !frozen; // disabled component can't run mouse-look or re-lock the cursor

        if (frozen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            // Drop the freed cursor at screen centre so it lands on the card, not a screen edge.
            if (Mouse.current != null)
                Mouse.current.WarpCursorPosition(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    // UGUI buttons (this card's START and the Skip button) only receive clicks if the
    // scene has an EventSystem wired to the new Input System. TutorialScene (copied from
    // MainScene) has none by default, so create/repair one — same pattern the task panels use.
    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            var es = EventSystem.current;
            if (es.GetComponent<InputSystemUIInputModule>() == null)
            {
                var legacy = es.GetComponent<StandaloneInputModule>();
                if (legacy != null) Destroy(legacy);
                es.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            return;
        }
        var go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();
        go.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildChecklist(Transform parent)
    {
        var rt = NewRect("TutorialChecklist", parent, new Vector2(320f, 250f), Vector2.zero);
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(36f, -36f);

        var bg = rt.gameObject.AddComponent<Image>();
        bg.color = new Color(0.04f, 0.06f, 0.10f, 0.55f);
        bg.raycastTarget = false;

        var title = SpawnLabel(rt, "CHECKLIST", 18, FontStyles.Bold, TextAlignmentOptions.Left,
            Vector2.zero, new Vector2(280f, 26f), new Color(0.85f, 0.9f, 1f, 0.9f));
        var titleRT = title.rectTransform;
        titleRT.anchorMin = new Vector2(0f, 1f); titleRT.anchorMax = new Vector2(0f, 1f);
        titleRT.pivot = new Vector2(0f, 1f); titleRT.anchoredPosition = new Vector2(16f, -12f);

        checklistLbls = new TMP_Text[ChecklistNames.Length];
        for (int i = 0; i < ChecklistNames.Length; i++)
        {
            var lbl = SpawnLabel(rt, "", 20, FontStyles.Bold, TextAlignmentOptions.Left,
                Vector2.zero, new Vector2(290f, 26f), CheckPendColor);
            var lrt = lbl.rectTransform;
            lrt.anchorMin = new Vector2(0f, 1f); lrt.anchorMax = new Vector2(0f, 1f);
            lrt.pivot = new Vector2(0f, 1f); lrt.anchoredPosition = new Vector2(16f, -44f - i * 32f);
            checklistLbls[i] = lbl;
        }
        RefreshChecklist();
    }

    private void BuildSkipButton(Transform parent)
    {
        var rt = NewRect("SkipButton", parent, new Vector2(160f, 44f), Vector2.zero);
        rt.anchorMin = new Vector2(1f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-32f, -32f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = SkipColor;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(Skip);

        var lblRT = NewRect("Label", rt, Vector2.zero, Vector2.zero);
        lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one;
        lblRT.offsetMin = Vector2.zero; lblRT.offsetMax = Vector2.zero;
        var lbl = lblRT.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = "SKIP TUTORIAL";
        lbl.fontSize = 16;
        lbl.fontStyle = FontStyles.Bold;
        lbl.color = Color.white;
        lbl.alignment = TextAlignmentOptions.Center;
    }

    private static RectTransform NewRect(string name, Transform parent, Vector2 size, Vector2 anchoredPos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = anchoredPos;
        return rt;
    }

    private static TMP_Text SpawnLabel(Transform parent, string text, int size, FontStyles style,
        TextAlignmentOptions align, Vector2 pos, Vector2 sizeDelta, Color color)
    {
        var rt = NewRect("Label", parent, sizeDelta, pos);
        var lbl = rt.gameObject.AddComponent<TextMeshProUGUI>();
        lbl.text = text;
        lbl.fontSize = size;
        lbl.fontStyle = style;
        lbl.color = color;
        lbl.alignment = align;
        lbl.raycastTarget = false;
        return lbl;
    }
}
