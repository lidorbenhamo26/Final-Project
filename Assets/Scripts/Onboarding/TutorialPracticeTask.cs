using System.Collections;
using UnityEngine;

/// <summary>
/// A trivial practice task used ONLY by the tutorial — it is not in
/// CognitiveTaskCatalog, so it never appears in the real mission and can't spoil
/// the real tasks. It just teaches the dock/interact loop: dock, do one tiny
/// action, undock. Two modes for a little variety across the two practice
/// stations:
///   mode 0 — press the single highlighted button.
///   mode 1 — press the matching number among three buttons.
/// Never auto-expires (no time pressure during onboarding).
/// </summary>
public class TutorialPracticeTask : CognitiveTaskBase
{
    [Tooltip("0 = press the button, 1 = press the matching number.")]
    public int practiceMode = 0;

    private bool done;

    public override void Activate()
    {
        TaskName = "Practice";
        timeLimit = 99999f; // no pressure in the tutorial
        base.Activate();
    }

    protected override void OnDocked()
    {
        if (done) return;
        ClearButtons();

        if (practiceMode == 1)
        {
            ShowMessage("PRACTICE  —  PRESS THE NUMBER  7", Color.white);
            SpawnButton(new Vector2(-180f, -40f), new Vector2(150f, 150f), "3", new Color(0.22f, 0.30f, 0.42f), () => OnWrong());
            SpawnButton(new Vector2(0f, -40f),    new Vector2(150f, 150f), "7", new Color(0.20f, 0.65f, 1.00f), () => OnSolved());
            SpawnButton(new Vector2(180f, -40f),  new Vector2(150f, 150f), "9", new Color(0.22f, 0.30f, 0.42f), () => OnWrong());
        }
        else
        {
            ShowMessage("PRACTICE  —  PRESS THE BUTTON", Color.white);
            SpawnButton(new Vector2(0f, -40f), new Vector2(320f, 140f), "PRESS", new Color(0.20f, 0.65f, 1.00f), () => OnSolved());
        }
    }

    private void OnWrong()
    {
        if (done) return;
        ShowSplash("TRY AGAIN", new Color(1f, 0.55f, 0.4f), 0.6f, 70f);
    }

    private void OnSolved()
    {
        if (done) return;
        done = true;
        ClearButtons();
        ShowMessage("NICE!", new Color(0.4f, 1f, 0.6f));
        ShowSplash("DONE", new Color(0.4f, 1f, 0.6f), 0.9f);
        ResolutionPending = true;          // block base time-limit from racing the resolve
        StartCoroutine(ResolveAfter(0.9f));
    }

    private IEnumerator ResolveAfter(float t)
    {
        yield return FrozenWait(t);
        Resolve(TaskResult.Success);
    }
}
