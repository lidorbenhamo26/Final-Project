using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TutorialDirector : MonoBehaviour
{
    [SerializeField] private Transform launchPadOverride;
    [SerializeField] private TaskStation practiceStationOverride;

    private static readonly Color PromptColor  = Color.white;
    private static readonly Color HelperColor  = new Color(0.70f, 0.78f, 0.88f, 1f);
    private static readonly Color ProgressColor = new Color(0.55f, 0.62f, 0.72f, 0.9f);
    private static readonly Color SkipColor    = new Color(0.30f, 0.34f, 0.40f, 1f);

    private struct Step
    {
        public string Prompt;
        public string Helper;
        public Func<TutorialDirector, bool> IsComplete;
    }

    private AstronautController player;
    private Rigidbody playerRb;
    private Transform playerT;
    private TaskStation practiceStation;
    private Transform launchPad;
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private TMP_Text promptLbl;
    private TMP_Text helperLbl;
    private TMP_Text progressLbl;
    private TutorialHighlight highlight;

    private List<Step> steps;
    private int stepIndex = -1;
    private float speedHoldSeconds;
    private bool jumpPressedThisStep;
    private bool taskResolvedThisStep;
    private bool practiceTaskPrepared;
    private bool finished;

    private void Start()
    {
        BuildOverlay();
        BuildHighlight();
        ResolveRefs();
        EnsurePlayerAnimator();
        BuildSteps();
        MissionTask.OnTaskResolved += HandleTaskResolved;
        Advance();
    }

    // Make sure the Astronaut's Animator is enabled when entering the
    // tutorial. We previously disabled it as a workaround for a movement
    // bug; that bug was actually the practice station's huge BoxCollider,
    // not the Animator. The disable lives in the scene file as a relic
    // and would silently re-disable the animator on every play.
    private void EnsurePlayerAnimator()
    {
        if (player == null) return;
        var anim = player.GetComponent<Animator>();
        if (anim != null) anim.enabled = true;
    }

    private void BuildHighlight()
    {
        var hGO = new GameObject("TutorialHighlight");
        hGO.transform.SetParent(transform, false);
        highlight = hGO.AddComponent<TutorialHighlight>();
    }

    private void OnDestroy()
    {
        MissionTask.OnTaskResolved -= HandleTaskResolved;
    }

    private void Update()
    {
        if (finished || steps == null || stepIndex < 0 || stepIndex >= steps.Count) return;
        if (stepIndex == 2 && JumpPressedThisFrame()) jumpPressedThisStep = true;
        if (steps[stepIndex].IsComplete(this)) Advance();
    }

    private void HandleTaskResolved(MissionTask t, TaskResult r, float rt) => taskResolvedThisStep = true;

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
                moveAction = input.actions.FindAction("Move", throwIfNotFound: false);
                sprintAction = input.actions.FindAction("Sprint", throwIfNotFound: false);
                jumpAction = input.actions.FindAction("Jump", throwIfNotFound: false);
            }
        }

        practiceStation = practiceStationOverride != null
            ? practiceStationOverride
            : FindAnyObjectByType<TaskStation>();

        launchPad = launchPadOverride;
        if (launchPad == null)
        {
            var go = GameObject.Find("LaunchPad");
            if (go != null) launchPad = go.transform;
        }

        var cam = Camera.main;
        if (cam != null && playerT != null)
        {
            var tpc = cam.GetComponent<ThirdPersonCamera>();
            if (tpc != null) tpc.SetTarget(playerT);
        }
    }

    private void BuildSteps()
    {
        steps = new List<Step>
        {
            new Step {
                Prompt = "PRESS  W A S D  OR  ARROW KEYS  TO MOVE",
                Helper = "Walk around to get a feel for the controls.",
                IsComplete = d => d.HasMoveInput() && d.SustainedSpeed(1.0f, 0.4f),
            },
            new Step {
                Prompt = "HOLD  SHIFT  TO SPRINT",
                Helper = "Try running across the bay.",
                IsComplete = d => d.HasMoveInput() && d.HasSprintInput() && d.SustainedSpeed(3.5f, 0.4f),
            },
            new Step {
                Prompt = "PRESS  SPACE  TO JUMP",
                Helper = "Hop into the air.",
                IsComplete = d => d.jumpPressedThisStep && d.playerRb != null && d.playerRb.linearVelocity.y > 1.0f,
            },
            new Step {
                Prompt = "WALK TO THE PRACTICE STATION",
                Helper = "The console highlighted ahead of you.",
                IsComplete = d => d.DistanceToStation() < 2.5f,
            },
            new Step {
                Prompt = "PRESS  E  TO DOCK",
                Helper = "Engage with the station console.",
                IsComplete = d => d.EnsurePracticeTask() && StationDockController.Instance != null && StationDockController.Instance.IsDocked,
            },
            new Step {
                Prompt = "COMPLETE THE PRACTICE TASK",
                Helper = "You'll undock automatically when finished.",
                IsComplete = d => d.taskResolvedThisStep,
            },
            new Step {
                Prompt = "WALK TO THE LAUNCH PAD",
                Helper = "Highlighted on the floor — that's your ride to the real mission.",
                IsComplete = d => d.DistanceToLaunchPad() < 2.0f,
            },
        };
    }

    private void Advance()
    {
        stepIndex++;
        speedHoldSeconds = 0f;
        jumpPressedThisStep = false;
        taskResolvedThisStep = false;

        if (stepIndex >= steps.Count) { Finish(); return; }

        promptLbl.text = steps[stepIndex].Prompt;
        helperLbl.text = steps[stepIndex].Helper;
        progressLbl.text = (stepIndex + 1) + " / " + steps.Count;
        UpdateHighlight();
    }

    private void UpdateHighlight()
    {
        if (highlight == null) return;
        switch (stepIndex)
        {
            case 3:
                if (practiceStation != null)
                    highlight.SetTarget(practiceStation.transform, "PRACTICE STATION");
                else highlight.Hide();
                break;
            case 6:
                if (launchPad != null)
                    highlight.SetTarget(launchPad, "LAUNCH PAD");
                else highlight.Hide();
                break;
            default:
                highlight.Hide();
                break;
        }
    }

    private bool EnsurePracticeTask()
    {
        if (practiceTaskPrepared) return true;
        if (practiceStation == null) return false;
        if (practiceStation.HasActiveTask()) { practiceTaskPrepared = true; return true; }

        var taskGO = new GameObject("TutorialPractice_CodeMemoryTask");
        var task = taskGO.AddComponent<CodeMemoryTask>();
        practiceStation.AssignTask(task);
        practiceTaskPrepared = true;
        return true;
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
        Invoke(nameof(ReturnToBriefing), 1.2f);
    }

    private void Skip()
    {
        if (finished) return;
        finished = true;
        SessionContext.Instance.TutorialCompleted = true;
        ReturnToBriefing();
    }

    private void ReturnToBriefing() => SceneTransition.LoadStart();

    private bool SustainedSpeed(float threshold, float duration)
    {
        if (HorizSpeed() > threshold) { speedHoldSeconds += Time.deltaTime; return speedHoldSeconds >= duration; }
        speedHoldSeconds = 0f;
        return false;
    }

    private bool HasMoveInput()
    {
        if (moveAction != null && moveAction.ReadValue<Vector2>().sqrMagnitude > 0.04f) return true;
        var keyboard = Keyboard.current;
        return keyboard != null && (
            keyboard.wKey.isPressed || keyboard.aKey.isPressed || keyboard.sKey.isPressed || keyboard.dKey.isPressed ||
            keyboard.upArrowKey.isPressed || keyboard.leftArrowKey.isPressed ||
            keyboard.downArrowKey.isPressed || keyboard.rightArrowKey.isPressed);
    }

    private bool HasSprintInput()
    {
        if (sprintAction != null && sprintAction.IsPressed()) return true;
        var keyboard = Keyboard.current;
        return keyboard != null && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
    }

    private bool JumpPressedThisFrame()
    {
        if (jumpAction != null && jumpAction.WasPressedThisFrame()) return true;
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard.spaceKey.wasPressedThisFrame;
    }

    private float HorizSpeed()
    {
        if (playerRb == null) return 0f;
        var v = playerRb.linearVelocity;
        return new Vector2(v.x, v.z).magnitude;
    }

    private float DistanceToStation()
    {
        if (practiceStation == null || playerT == null) return float.MaxValue;
        return Vector3.Distance(playerT.position, practiceStation.transform.position);
    }

    private float DistanceToLaunchPad()
    {
        if (launchPad == null || playerT == null) return float.MaxValue;
        return Vector3.Distance(playerT.position, launchPad.position);
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

        BuildSkipButton(canvasGO.transform);
    }

    private void BuildSkipButton(Transform parent)
    {
        var rt = NewRect("SkipButton", parent, new Vector2(160f, 44f), new Vector2(0f, 0f));
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
