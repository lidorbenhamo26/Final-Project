using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class CommsTask : MissionTask
{
    [SerializeField] private float displayDuration = 3f;
    [SerializeField] private float recallDelay = 5f;

    private string secretCode;
    private string enteredCode;
    private bool awaitingInput;
    private bool playerInZone;

    private void Awake()
    {
        TaskName = "Comms Authentication";
        priority = TaskPriority.Critical;
        timeLimit = 60f;
        enteredCode = "";
    }

    public override void Activate()
    {
        base.Activate();
        secretCode = Random.Range(1000, 9999).ToString();
        StartCoroutine(CommsSequence());
    }

    private IEnumerator CommsSequence()
    {
        StationUI?.SetInstruction("MEMORIZE CODE: " + secretCode);
        yield return new WaitForSeconds(displayDuration);
        StationUI?.SetInstruction("...code hidden — recall in 5s...");
        yield return new WaitForSeconds(recallDelay);
        StationUI?.SetInstruction("Enter the 4-digit code (number keys)");
        awaitingInput = true;
    }

    protected override void Update()
    {
        base.Update();
        if (!IsActive || !awaitingInput || !playerInZone) return;

        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        string digit = "";
        if (kb.digit0Key.wasPressedThisFrame) digit = "0";
        else if (kb.digit1Key.wasPressedThisFrame) digit = "1";
        else if (kb.digit2Key.wasPressedThisFrame) digit = "2";
        else if (kb.digit3Key.wasPressedThisFrame) digit = "3";
        else if (kb.digit4Key.wasPressedThisFrame) digit = "4";
        else if (kb.digit5Key.wasPressedThisFrame) digit = "5";
        else if (kb.digit6Key.wasPressedThisFrame) digit = "6";
        else if (kb.digit7Key.wasPressedThisFrame) digit = "7";
        else if (kb.digit8Key.wasPressedThisFrame) digit = "8";
        else if (kb.digit9Key.wasPressedThisFrame) digit = "9";

        if (digit == "") return;

        enteredCode += digit;
        StationUI?.SetInstruction("Entering: " + enteredCode);

        if (enteredCode.Length >= 4)
        {
            awaitingInput = false;
            TaskResult res = (enteredCode == secretCode) ? TaskResult.Success : TaskResult.Fail;
            Resolve(res);
        }
    }

    public override void OnPlayerEnter() { playerInZone = true; }

    public override void OnPlayerExit()
    {
        playerInZone = false;
        enteredCode = "";
    }
}
