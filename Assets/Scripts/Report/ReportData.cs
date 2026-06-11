using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable snapshot of everything the assessor report shows — consumed by
/// the on-screen view and by both exporters so all three always agree.
/// </summary>
public class ReportData
{
    public class ScaleSection
    {
        public BriefScale Scale;
        public string Title;            // BRIEF-A scale name, e.g. "INHIBIT"
        public string Description;      // one-line clinical description
        public string TaskCaption;      // in-game task + station, e.g. "Stroop Color-Word Task — Comms Station"
        public List<TaskRecord> Records = new List<TaskRecord>();
        public bool Administered => Records.Count > 0;
    }

    // Participant
    public string ParticipantId;
    public string FullName;
    public int? Age;
    public int? SessionNumber;
    public string Notes;
    public string SessionGuid;
    public DateTime StartedUtc;

    // Session
    public int TasksTotal;
    public int TasksPassed;
    public int TasksFailed;
    public float AverageReactionTime;
    public string LogFilePath;
    public bool MissionActive;
    public float MissionTimeRemaining;
    public DateTime GeneratedLocal;

    public List<ScaleSection> Sections = new List<ScaleSection>();

    private class ScaleMeta
    {
        public BriefScale Scale;
        public string Title;
        public string Description;
        public string TaskCaption;
    }

    // Fixed clinical display order; Unclassified is appended only when populated.
    private static readonly ScaleMeta[] ScaleOrder =
    {
        new ScaleMeta
        {
            Scale = BriefScale.Inhibit,
            Title = "INHIBIT",
            Description = "Response inhibition — the ability to resist impulses and stop a prepotent response.",
            TaskCaption = "Stroop Color-Word Task — Comms Station",
        },
        new ScaleMeta
        {
            Scale = BriefScale.WorkingMemory,
            Title = "WORKING MEMORY",
            Description = "Holding information in mind to complete a task — encoding and recalling a 4-digit code under delay.",
            TaskCaption = "4-Digit Code Recall — Engine Station",
        },
        new ScaleMeta
        {
            Scale = BriefScale.PlanOrganize,
            Title = "PLAN / ORGANIZE",
            Description = "Planning and sequencing multi-step action toward a goal under time pressure.",
            TaskCaption = "Power Cell Delivery — Life Support Station",
        },
        new ScaleMeta
        {
            Scale = BriefScale.TaskMonitor,
            Title = "TASK MONITOR",
            Description = "Sustained attention and performance monitoring — detecting rare targets over time (CPT).",
            TaskCaption = "Radar Scan Vigilance Task — Navigation Station",
        },
        new ScaleMeta
        {
            Scale = BriefScale.Unclassified,
            Title = "OTHER TASKS",
            Description = "Tasks without a BRIEF-A scale mapping.",
            TaskCaption = "",
        },
    };

    public static ReportData Capture()
    {
        var data = new ReportData { GeneratedLocal = DateTime.Now };

        var ctx = SessionContext.Instance;
        if (ctx != null)
        {
            data.ParticipantId = ctx.ParticipantId;
            data.FullName = ctx.FullName;
            data.Age = ctx.Age;
            data.SessionNumber = ctx.SessionNumber;
            data.Notes = ctx.Notes;
            data.SessionGuid = ctx.SessionGuid;
            data.StartedUtc = ctx.StartedUtc;
        }

        var sm = SessionManager.Instance;
        if (sm != null)
        {
            data.TasksTotal = sm.TasksTotal;
            data.TasksPassed = sm.TasksPassed;
            data.TasksFailed = sm.TasksFailed;
            data.AverageReactionTime = sm.AverageReactionTime;
            data.LogFilePath = sm.LogFilePath;
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            data.MissionActive = gm.MissionActive;
            data.MissionTimeRemaining = gm.MissionTimeRemaining;
        }

        var records = AssessmentResults.Instance.Records;
        foreach (var meta in ScaleOrder)
        {
            var section = new ScaleSection
            {
                Scale = meta.Scale,
                Title = meta.Title,
                Description = meta.Description,
                TaskCaption = meta.TaskCaption,
            };
            foreach (var r in records)
                if (r.Scale == meta.Scale)
                    section.Records.Add(r);

            // Unclassified only appears when something actually landed there.
            if (meta.Scale == BriefScale.Unclassified && !section.Administered) continue;

            // For unmapped tasks the caption comes from the records themselves.
            if (meta.Scale == BriefScale.Unclassified && section.Records.Count > 0)
                section.TaskCaption = section.Records[0].TaskName + " — " + section.Records[0].StationName;

            data.Sections.Add(section);
        }

        return data;
    }
}

/// <summary>
/// Shared display formatting for the on-screen view and the HTML exporter:
/// metric-key → human label, value prettifiers, and result colors.
/// </summary>
public static class ReportFormat
{
    private static readonly Dictionary<string, string> MetricLabels = new Dictionary<string, string>
    {
        { "correct",            "Correct" },
        { "typos",              "Wrong key presses" },
        { "totalTimeS",         "Total time (s)" },
        { "recallTimeout",      "Recall timed out" },
        { "phaseAtTimeout",     "Phase at timeout" },
        { "rounds",             "Trials" },
        { "answered",           "Trials answered" },
        { "accuracy",           "Accuracy" },
        { "rtMeanMs",           "Mean RT (ms)" },
        { "rtSdMs",             "RT variability (SD, ms)" },
        { "hits",               "Hits" },
        { "misses",             "Misses" },
        { "falseAlarms",        "False alarms" },
        { "correctRejects",     "Correct rejections" },
        { "hitRate",            "Hit rate" },
        { "faRate",             "False-alarm rate" },
        { "dPrime",             "d′ (sensitivity)" },
        { "vigilanceSlope",     "Vigilance decrement slope" },
        { "deltaDPrime",        "Δ d′ (block 2 − block 1)" },
        { "mode",               "Protocol" },
        { "commission",         "Commission errors (false starts)" },
        { "omission",           "Omission errors (missed GO)" },
        { "hitRtMeanMs",        "Mean GO RT (ms)" },
        { "hitRtSdMs",          "GO RT variability (SD, ms)" },
        { "postErrorSlowingMs", "Post-error slowing (ms)" },
        { "pickupLatencyS",     "Time to locate cell (s)" },
        { "deliveredTimeS",     "Total delivery time (s)" },
        { "wiresTotal",         "Wires to route" },
        { "wireErrorsCount",    "Wiring errors (wrong socket)" },
        { "puzzleTimeS",        "Wiring panel time (s)" },
        { "panelOpens",         "Panel openings" },
        { "outcome",            "Outcome" },
    };

    public static string LabelFor(string metricKey)
    {
        return MetricLabels.TryGetValue(metricKey, out var label) ? label : metricKey;
    }

    /// <summary>Render rate-like metrics (0..1) as percentages; pass through everything else.</summary>
    public static string ValueFor(string metricKey, string raw)
    {
        if (raw == null) return "";
        if ((metricKey == "accuracy" || metricKey == "hitRate" || metricKey == "faRate")
            && float.TryParse(raw, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f))
            return Mathf.RoundToInt(f * 100f) + "%";
        if (metricKey == "correct" || metricKey == "recallTimeout")
        {
            if (raw == "True" || raw == "true") return "Yes";
            if (raw == "False" || raw == "false") return "No";
        }
        return raw;
    }

    public static string ResultLabel(TaskResult? result)
    {
        if (!result.HasValue) return "IN PROGRESS";
        switch (result.Value)
        {
            case TaskResult.Success:    return "PASS";
            case TaskResult.Fail:       return "FAIL";
            case TaskResult.Omission:   return "OMISSION";
            case TaskResult.Commission: return "COMMISSION";
            default:                    return result.Value.ToString().ToUpperInvariant();
        }
    }

    /// <summary>Hex color (no #) shared by the USS pills and the HTML pills.</summary>
    public static string ResultColorHex(TaskResult? result)
    {
        if (!result.HasValue) return "64748B";          // slate — in progress
        switch (result.Value)
        {
            case TaskResult.Success:    return "16A34A"; // green
            case TaskResult.Fail:       return "DC2626"; // red
            case TaskResult.Omission:   return "D97706"; // amber
            case TaskResult.Commission: return "D97706"; // amber
            default:                    return "64748B";
        }
    }

    public static string FormatMissionStatus(ReportData data)
    {
        if (!data.MissionActive) return "Complete";
        int min = Mathf.FloorToInt(data.MissionTimeRemaining / 60f);
        int sec = Mathf.FloorToInt(data.MissionTimeRemaining % 60f);
        return "In progress — " + min.ToString("00") + ":" + sec.ToString("00") + " remaining";
    }
}
