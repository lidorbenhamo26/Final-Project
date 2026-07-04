using System;

/// <summary>
/// A Life-Support planning puzzle (Plan/Organize). Several variants implement this
/// so the station can rotate a different one each time, keeping the task fresh and
/// measuring planning rather than memorizing one puzzle. The owning
/// BatteryDeliveryTask drives them all through this interface.
/// </summary>
public interface IPlanningPuzzle
{
    /// <summary>Polled while open; returning false auto-closes the puzzle.</summary>
    Func<bool> ValidWhile { get; set; }

    /// <summary>All steps completed correctly (fired after the panel hides).</summary>
    event Action OnSolved;
    /// <summary>Closed without solving; true = auto-closed, false = player (ESC).</summary>
    event Action<bool> OnClosed;
    /// <summary>Fired on each wrong action (for plan-error telemetry).</summary>
    event Action OnError;

    int ErrorCount { get; }     // wrong actions (a Plan/Organize metric)
    float ActiveTimeS { get; }  // freeze-aware time the panel was open
    int OpenCount { get; }      // how many times the player (re)opened it
    int StepCount { get; }      // wires / sequence steps (for the report)
    int MoveCount { get; }      // attempted placements / adjustments (incl. wrong ones)
    int BacktrackCount { get; } // undone or changed actions (planning uncertainty)
    float FirstMoveLatencyS { get; } // open -> first interaction (<0 = never moved);
                                     // proactive planning vs impulsive trial-and-error
    bool IsOpen { get; }

    void Show();
    void ForceClose();          // hide without firing OnClosed (task teardown)
    void SetPower(float frac01); // mirror the life-support drain inside the panel
    void DebugAutoSolve();      // DEV/auto-playtest: complete the puzzle programmatically
}
