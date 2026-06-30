using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes a wide per-task summary CSV (one row per administered task) next to
/// the raw event log and returns the file path. UTF-8 with BOM so Excel opens
/// it without an import wizard.
/// </summary>
public static class ReportCsvExporter
{
    // Fixed metric columns in a stable order; anything a task reports beyond
    // these lands in the trailing ExtraMetrics column as k=v;k=v.
    private static readonly string[] MetricColumns =
    {
        "correct", "typos", "totalTimeS", "recallTimeout",
        "rounds", "answered", "accuracy", "rtMeanMs", "rtSdMs",
        "hits", "misses", "falseAlarms", "correctRejects",
        "hitRate", "faRate", "dPrime", "vigilanceSlope", "deltaDPrime", "mode",
        "commission", "omission", "hitRtMeanMs", "hitRtSdMs", "postErrorSlowingMs",
        "pickupLatencyS", "deliveredTimeS",
        "wiresTotal", "wireErrorsCount", "puzzleTimeS", "panelOpens",
        "timeLimitS", "outcome",
    };

    public static string Export(ReportData data)
    {
        string fileName = "MissionFocus_TaskSummary_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        var sb = new StringBuilder(8 * 1024);

        sb.Append("ParticipantId,FullName,Age,SessionNumber,SessionGuid,StartedUtc,DurationMin,");
        sb.Append("TaskName,BriefScale,Station,Priority,Result,ReactionTimeS");
        foreach (var col in MetricColumns) sb.Append(',').Append(col);
        sb.AppendLine(",ExtraMetrics");

        string identity =
            Csv(data.ParticipantId) + "," +
            Csv(data.FullName) + "," +
            (data.Age.HasValue ? data.Age.Value.ToString() : "") + "," +
            (data.SessionNumber.HasValue ? data.SessionNumber.Value.ToString() : "") + "," +
            Csv(data.SessionGuid) + "," +
            (data.StartedUtc == default ? "" : data.StartedUtc.ToString("o")) + "," +
            (data.DurationMinutes > 0 ? data.DurationMinutes.ToString() : "");

        foreach (var section in data.Sections)
        {
            foreach (var r in section.Records)
            {
                sb.Append(identity);
                sb.Append(',').Append(Csv(r.TaskName));
                sb.Append(',').Append(r.Scale);
                sb.Append(',').Append(Csv(r.StationName));
                sb.Append(',').Append(r.Priority);
                sb.Append(',').Append(r.Result.HasValue ? r.Result.Value.ToString() : "InProgress");
                sb.Append(',').Append(r.Result.HasValue ? Num.F3(r.ReactionTimeS) : "");

                var extras = new List<KeyValuePair<string, string>>();
                foreach (var kv in r.Metrics)
                    if (Array.IndexOf(MetricColumns, kv.Key) < 0)
                        extras.Add(kv);

                foreach (var col in MetricColumns)
                {
                    string value = r.GetMetric(col);
                    sb.Append(',').Append(value != null ? Csv(value) : "");
                }

                if (extras.Count == 0)
                {
                    sb.AppendLine(",");
                }
                else
                {
                    var extraSb = new StringBuilder();
                    for (int i = 0; i < extras.Count; i++)
                    {
                        if (i > 0) extraSb.Append(';');
                        extraSb.Append(extras[i].Key).Append('=').Append(extras[i].Value);
                    }
                    sb.Append(',').AppendLine(Csv(extraSb.ToString()));
                }
            }
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));

        // Companion executive-function events CSV (behavioral data, kept separate
        // from the per-task cognitive summary above).
        WriteEfCsv(data);

        return path;
    }

    // One row per EF event (prioritization / switching behavior).
    private static void WriteEfCsv(ReportData data)
    {
        if (data.Executive == null || data.Executive.Events.Count == 0) return;

        string fileName = "MissionFocus_EFEvents_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        string path = Path.Combine(Application.persistentDataPath, fileName);

        var sb = new StringBuilder(2 * 1024);
        sb.Append("ParticipantId,FullName,SessionGuid,");
        sb.AppendLine("EventType,NOptions,Options,ChosenOrder,OptimalOrder,ChosenFirst," +
                      "FirstWasOptimal,WasOptimal,OptScore,DecisionLatencyS,ChangedMind,NoChoice,Perseveration,Resumed,CodeRecalledCorrect");

        string id = Csv(data.ParticipantId) + "," + Csv(data.FullName) + "," + Csv(data.SessionGuid);
        foreach (var e in data.Executive.Events)
        {
            sb.Append(id);
            sb.Append(',').Append(Csv(e.EventType));
            sb.Append(',').Append(e.NOptions);
            sb.Append(',').Append(Csv(e.OptionsDesc));
            sb.Append(',').Append(Csv(e.ChosenOrder));
            sb.Append(',').Append(Csv(e.OptimalOrder));
            sb.Append(',').Append(Csv(e.ChosenFirst));
            sb.Append(',').Append(e.FirstWasOptimal);
            sb.Append(',').Append(e.WasOptimal);
            sb.Append(',').Append(Num.F2(e.OptScore));
            sb.Append(',').Append(e.DecisionLatencyS < 0f ? "NA" : Num.F2(e.DecisionLatencyS));
            sb.Append(',').Append(e.ChangedMind);
            sb.Append(',').Append(e.NoChoice);
            sb.Append(',').Append(e.Perseveration);
            sb.Append(',').Append(e.Resumed);
            sb.Append(',').AppendLine(e.CodeRecalledCorrect < 0 ? "NA" :
                                      (e.CodeRecalledCorrect == 1 ? "True" : "False"));
        }

        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }
}
