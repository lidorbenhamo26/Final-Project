using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Writes a self-contained, print-friendly HTML version of the assessor
/// report to Application.persistentDataPath and returns the file path.
/// Opens cleanly in any browser; Print → Save as PDF gives an A4 report.
/// </summary>
public static class ReportHtmlExporter
{
    public static string Export(ReportData data)
    {
        string idSafe = SanitizeForFileName(data.ParticipantId);
        string fileName = "MissionFocus_Report_" + idSafe + "_" +
                          DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".html";
        string path = Path.Combine(Application.persistentDataPath, fileName);
        File.WriteAllText(path, BuildHtml(data), new UTF8Encoding(false));
        return path;
    }

    private static string BuildHtml(ReportData data)
    {
        var sb = new StringBuilder(16 * 1024);
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("<meta charset=\"utf-8\">");
        sb.AppendLine("<title>Executive Function Assessment Report — " + Esc(data.ParticipantId) + "</title>");
        sb.AppendLine("<style>");
        sb.AppendLine(Css);
        sb.AppendLine("</style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("<div class=\"page\">");

        BuildHeader(sb, data);
        if (data.MissionActive)
            sb.AppendLine("<div class=\"banner\">SESSION IN PROGRESS — results below are partial</div>");
        BuildSummary(sb, data);
        foreach (var section in data.Sections)
            BuildScaleCard(sb, section);
        BuildExecutiveCard(sb, data);
        BuildFooter(sb, data);

        sb.AppendLine("</div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static void BuildHeader(StringBuilder sb, ReportData data)
    {
        sb.AppendLine("<div class=\"header\">");
        sb.AppendLine("  <div>");
        sb.AppendLine("    <h1>EXECUTIVE FUNCTION ASSESSMENT REPORT</h1>");
        sb.AppendLine("    <div class=\"subtitle\">Space-Station Cognitive Task Battery &middot; BRIEF-A&ndash;aligned scales</div>");
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"header-right\">");
        sb.AppendLine("    <span class=\"badge\">RESEARCH PROTOTYPE</span>");
        sb.AppendLine("    <button class=\"no-print print-btn\" onclick=\"window.print()\">Print this report</button>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</div>");

        string sessionDate = data.StartedUtc == default
            ? data.GeneratedLocal.ToString("yyyy-MM-dd HH:mm")
            : data.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

        sb.AppendLine("<table class=\"demo\">");
        sb.AppendLine("  <tr>");
        DemoCell(sb, "Participant ID", data.ParticipantId);
        DemoCell(sb, "Full name", data.FullName);
        DemoCell(sb, "Age", data.Age.HasValue ? data.Age.Value.ToString() : "—");
        sb.AppendLine("  </tr><tr>");
        DemoCell(sb, "Session #", data.SessionNumber.HasValue ? data.SessionNumber.Value.ToString() : "—");
        DemoCell(sb, "Session date", sessionDate);
        DemoCell(sb, "Duration", data.DurationMinutes > 0 ? data.DurationMinutes + " min" : "—");
        sb.AppendLine("  </tr><tr>");
        DemoCell(sb, "Session GUID", data.SessionGuid);
        sb.AppendLine("  </tr>");
        if (!string.IsNullOrWhiteSpace(data.Notes))
        {
            sb.AppendLine("  <tr><td colspan=\"3\"><span class=\"demo-label\">Notes</span><br>" +
                          Esc(data.Notes) + "</td></tr>");
        }
        sb.AppendLine("</table>");
    }

    private static void DemoCell(StringBuilder sb, string label, string value)
    {
        sb.AppendLine("    <td><span class=\"demo-label\">" + Esc(label) + "</span><br>" +
                      Esc(string.IsNullOrWhiteSpace(value) ? "—" : value) + "</td>");
    }

    private static void BuildSummary(StringBuilder sb, ReportData data)
    {
        float accuracy = data.TasksTotal > 0 ? data.TasksPassed * 100f / data.TasksTotal : 0f;
        sb.AppendLine("<div class=\"summary\">");
        SummaryItem(sb, data.TasksTotal.ToString(), "Tasks administered", null);
        SummaryItem(sb, data.TasksPassed.ToString(), "Passed", "good");
        SummaryItem(sb, data.TasksFailed.ToString(), "Failed", data.TasksFailed > 0 ? "bad" : null);
        SummaryItem(sb, accuracy.ToString("F0") + "%", "Overall accuracy", null);
        SummaryItem(sb, data.AverageReactionTime.ToString("F1") + "s", "Mean completion time", null);
        SummaryItem(sb, ReportFormat.FormatMissionStatus(data), "Mission status", null);
        sb.AppendLine("</div>");
    }

    private static void SummaryItem(StringBuilder sb, string value, string caption, string extraClass)
    {
        string cls = extraClass == null ? "summary-value" : "summary-value " + extraClass;
        sb.AppendLine("  <div class=\"summary-item\"><div class=\"" + cls + "\">" + Esc(value) +
                      "</div><div class=\"summary-caption\">" + Esc(caption) + "</div></div>");
    }

    private static void BuildScaleCard(StringBuilder sb, ReportData.ScaleSection section)
    {
        sb.AppendLine("<div class=\"card" + (section.Administered ? "" : " card-empty") + "\">");
        sb.AppendLine("  <div class=\"card-header\"><h2>" + Esc(section.Title) + "</h2>");
        if (section.Administered)
        {
            var last = section.Records[section.Records.Count - 1];
            sb.AppendLine("    " + Pill(last.Result));
        }
        sb.AppendLine("  </div>");
        sb.AppendLine("  <div class=\"card-desc\">" + Esc(section.Description) + "</div>");
        if (!string.IsNullOrEmpty(section.TaskCaption))
            sb.AppendLine("  <div class=\"card-caption\">Task: " + Esc(section.TaskCaption) + "</div>");

        if (!section.Administered)
        {
            sb.AppendLine("  <div class=\"card-na\">Not administered in this session.</div>");
            sb.AppendLine("</div>");
            return;
        }

        for (int i = 0; i < section.Records.Count; i++)
        {
            var r = section.Records[i];
            string title = section.Records.Count > 1 ? "ATTEMPT " + (i + 1) : "RESULT";
            sb.AppendLine("  <div class=\"attempt\">");
            sb.AppendLine("    <div class=\"attempt-header\"><span class=\"attempt-title\">" + title + "</span> " + Pill(r.Result) +
                          (r.Result.HasValue
                              ? " <span class=\"attempt-rt\">Completed in " + r.ReactionTimeS.ToString("F1") + "s</span>"
                              : "") + "</div>");

            if (r.Metrics.Count > 0)
            {
                sb.AppendLine("    <table class=\"metrics\">");
                foreach (var kv in r.Metrics)
                {
                    sb.AppendLine("      <tr><td class=\"metric-label\">" + Esc(ReportFormat.LabelFor(kv.Key)) +
                                  "</td><td class=\"metric-value\">" + Esc(ReportFormat.ValueFor(kv.Key, kv.Value)) + "</td></tr>");
                }
                sb.AppendLine("    </table>");
            }

            AppendBars(sb, r);
            sb.AppendLine("  </div>");
        }
        sb.AppendLine("</div>");
    }

    private static void AppendBars(StringBuilder sb, TaskRecord r)
    {
        if (r.TryGetMetricFloat("accuracy", out float acc)) Bar(sb, "Accuracy", acc, "#16A34A");
        if (r.TryGetMetricFloat("hitRate", out float hr)) Bar(sb, "Hit rate", hr, "#16A34A");
        if (r.TryGetMetricFloat("faRate", out float fa)) Bar(sb, "False-alarm rate", fa, "#DC2626");
        if (r.TryGetMetricFloat("deliveredTimeS", out float used)
            && r.TryGetMetricFloat("timeLimitS", out float limit) && limit > 0f)
            Bar(sb, "Time used", used / limit, "#2563EB");
    }

    private static void Bar(StringBuilder sb, string label, float fraction, string color)
    {
        fraction = Mathf.Clamp01(fraction);
        sb.AppendLine("    <div class=\"bar-block\">");
        sb.AppendLine("      <div class=\"bar-head\"><span>" + Esc(label) + "</span><span>" +
                      Mathf.RoundToInt(fraction * 100f) + "%</span></div>");
        sb.AppendLine("      <div class=\"bar-track\"><div class=\"bar-fill\" style=\"width:" +
                      (fraction * 100f).ToString("F0") + "%;background:" + color + "\"></div></div>");
        sb.AppendLine("    </div>");
    }

    // Executive-function behavior — prioritization decisions during EF events,
    // reported separately from the cognitive scales above.
    private static void BuildExecutiveCard(StringBuilder sb, ReportData data)
    {
        var ef = data.Executive;
        if (ef == null || ef.EventCount == 0) return;

        sb.AppendLine("<div class=\"card\">");
        sb.AppendLine("  <div class=\"card-header\"><h2>EXECUTIVE FUNCTION &mdash; PRIORITIZATION</h2></div>");
        sb.AppendLine("  <div class=\"card-desc\">How the participant triaged competing tasks during high-workload " +
                      "events: which task they did first, how close their order was to the optimal Priority Protocol " +
                      "(most critical first, then nearest deadline), and how quickly they decided.</div>");

        sb.AppendLine("  <table class=\"metrics\">");
        sb.AppendLine("    <tr><td class=\"metric-label\">EF events</td><td class=\"metric-value\">" + ef.EventCount + "</td></tr>");
        sb.AppendLine("    <tr><td class=\"metric-label\">Optimal first choice</td><td class=\"metric-value\">" +
                      Mathf.RoundToInt(ef.OptimalFirstRate * 100f) + "%</td></tr>");
        sb.AppendLine("    <tr><td class=\"metric-label\">Order quality (mean)</td><td class=\"metric-value\">" +
                      Mathf.RoundToInt(ef.MeanOptScore * 100f) + "%</td></tr>");
        sb.AppendLine("    <tr><td class=\"metric-label\">Mean decision latency</td><td class=\"metric-value\">" +
                      (ef.MeanDecisionLatencyS < 0f ? "&mdash;" : ef.MeanDecisionLatencyS.ToString("F1") + "s") + "</td></tr>");
        sb.AppendLine("    <tr><td class=\"metric-label\">No decision made</td><td class=\"metric-value\">" +
                      Mathf.RoundToInt(ef.NoChoiceRate * 100f) + "%</td></tr>");
        sb.AppendLine("  </table>");

        // Shift / task-switching sub-block (from Interruption events).
        if (ef.InterruptionCount > 0)
        {
            sb.AppendLine("  <div class=\"card-caption\">Shift &mdash; task-switching (interruption events)</div>");
            sb.AppendLine("  <table class=\"metrics\">");
            sb.AppendLine("    <tr><td class=\"metric-label\">Interruptions</td><td class=\"metric-value\">" + ef.InterruptionCount + "</td></tr>");
            sb.AppendLine("    <tr><td class=\"metric-label\">Correct switch decisions</td><td class=\"metric-value\">" +
                          Mathf.RoundToInt(ef.SwitchCorrectRate * 100f) + "%</td></tr>");
            sb.AppendLine("    <tr><td class=\"metric-label\">Mean switch latency</td><td class=\"metric-value\">" +
                          (ef.MeanSwitchLatencyS < 0f ? "&mdash;" : ef.MeanSwitchLatencyS.ToString("F1") + "s") + "</td></tr>");
            sb.AppendLine("    <tr><td class=\"metric-label\">Perseveration (missed switches)</td><td class=\"metric-value\">" + ef.PerseverationCount + "</td></tr>");
            sb.AppendLine("    <tr><td class=\"metric-label\">Resumed after switch</td><td class=\"metric-value\">" +
                          (ef.ResumeRate < 0f ? "&mdash;" : Mathf.RoundToInt(ef.ResumeRate * 100f) + "%") + "</td></tr>");
            sb.AppendLine("  </table>");
        }

        // Working memory under load sub-block (from WM+Prioritization events).
        if (ef.WmPrioritizationCount > 0)
        {
            sb.AppendLine("  <div class=\"card-caption\">Working memory under load (WM + prioritization events)</div>");
            sb.AppendLine("  <table class=\"metrics\">");
            sb.AppendLine("    <tr><td class=\"metric-label\">WM+Prioritization events</td><td class=\"metric-value\">" + ef.WmPrioritizationCount + "</td></tr>");
            sb.AppendLine("    <tr><td class=\"metric-label\">Code recalled after interference</td><td class=\"metric-value\">" +
                          (ef.CodeRecallRate < 0f ? "&mdash;" : Mathf.RoundToInt(ef.CodeRecallRate * 100f) + "%") + "</td></tr>");
            sb.AppendLine("  </table>");
        }

        // Per-event detail.
        for (int i = 0; i < ef.Events.Count; i++)
        {
            var e = ef.Events[i];
            sb.AppendLine("  <div class=\"attempt\">");
            sb.AppendLine("    <div class=\"attempt-header\"><span class=\"attempt-title\">" +
                          Esc(e.EventType.ToUpperInvariant()) + " " + (i + 1) + "</span> " +
                          "<span class=\"pill\" style=\"background:#" + (e.FirstWasOptimal ? "16A34A" : "D97706") + "\">" +
                          (e.FirstWasOptimal ? "OPTIMAL FIRST" : "SUBOPTIMAL") + "</span></div>");
            sb.AppendLine("    <table class=\"metrics\">");
            sb.AppendLine("      <tr><td class=\"metric-label\">Offered</td><td class=\"metric-value\">" + Esc(ReportFormat.PrettyStationsInline(e.OptionsDesc)) + "</td></tr>");
            sb.AppendLine("      <tr><td class=\"metric-label\">Player choice</td><td class=\"metric-value\">" + Esc(ReportFormat.PrettyStationsInline(e.ChosenOrder)) + "</td></tr>");
            sb.AppendLine("      <tr><td class=\"metric-label\">Optimal choice</td><td class=\"metric-value\">" + Esc(ReportFormat.PrettyStationsInline(e.OptimalOrder)) + "</td></tr>");
            sb.AppendLine("      <tr><td class=\"metric-label\">Decision quality</td><td class=\"metric-value\">" + Mathf.RoundToInt(e.OptScore * 100f) + "%</td></tr>");
            sb.AppendLine("      <tr><td class=\"metric-label\">Decision latency</td><td class=\"metric-value\">" +
                          (e.DecisionLatencyS < 0f ? "no choice" : e.DecisionLatencyS.ToString("F1") + "s") + "</td></tr>");
            if (e.CodeRecalledCorrect >= 0)
                sb.AppendLine("      <tr><td class=\"metric-label\">Code recalled</td><td class=\"metric-value\">" +
                              (e.CodeRecalledCorrect == 1 ? "Yes" : "No") + "</td></tr>");
            sb.AppendLine("    </table>");
            sb.AppendLine("  </div>");
        }

        sb.AppendLine("</div>");
    }

    private static void BuildFooter(StringBuilder sb, ReportData data)
    {
        sb.AppendLine("<div class=\"footer\">");
        sb.AppendLine("  <div class=\"disclaimer\">Research prototype developed for an academic project. " +
                      "This is not a clinical instrument; results are game-derived performance indicators " +
                      "and do not constitute a diagnosis or a standardized BRIEF-A administration.</div>");
        sb.AppendLine("  <div class=\"footer-meta\">Generated " + data.GeneratedLocal.ToString("yyyy-MM-dd HH:mm:ss") +
                      (string.IsNullOrEmpty(data.LogFilePath) ? "" : " &middot; Raw event log: " + Esc(data.LogFilePath)) +
                      "</div>");
        sb.AppendLine("</div>");
    }

    private static string Pill(TaskResult? result)
    {
        return "<span class=\"pill\" style=\"background:#" + ReportFormat.ResultColorHex(result) + "\">" +
               Esc(ReportFormat.ResultLabel(result)) + "</span>";
    }

    private static string Esc(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&#39;");
    }

    private static string SanitizeForFileName(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "unknown";
        var sb = new StringBuilder(s.Length);
        foreach (char c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
        return sb.ToString();
    }

    private const string Css = @"
:root { color-scheme: light; }
* { box-sizing: border-box; }
body { margin: 0; padding: 24px; background: #E5E9EF; font-family: 'Segoe UI', Arial, sans-serif; color: #1E293B; }
.page { max-width: 900px; margin: 0 auto; background: #F7F9FB; border: 1px solid #D7DEE7; border-radius: 10px; padding: 36px 44px; }
.header { display: flex; justify-content: space-between; align-items: flex-start; gap: 16px; }
h1 { font-size: 22px; letter-spacing: 1px; margin: 0 0 4px 0; color: #0F172A; }
.subtitle { font-size: 13px; color: #64748B; }
.header-right { text-align: right; }
.badge { display: inline-block; background: #2563EB; color: #fff; font-size: 11px; font-weight: 700; letter-spacing: 1px; padding: 4px 10px; border-radius: 4px; }
.print-btn { display: block; margin: 10px 0 0 auto; padding: 6px 14px; border: 1px solid #2563EB; background: #fff; color: #2563EB; border-radius: 6px; cursor: pointer; font-weight: 600; }
.print-btn:hover { background: #EFF4FF; }
.demo { width: 100%; border-collapse: collapse; margin: 18px 0 6px 0; }
.demo td { border: 1px solid #E2E8F0; padding: 8px 12px; font-size: 14px; background: #fff; }
.demo-label { font-size: 11px; font-weight: 700; letter-spacing: 0.8px; color: #64748B; text-transform: uppercase; }
.banner { margin: 14px 0 0 0; background: #FEF3C7; border: 1px solid #D97706; color: #92400E; font-weight: 700; font-size: 13px; padding: 8px 14px; border-radius: 6px; text-align: center; }
.summary { display: flex; gap: 10px; margin: 18px 0; flex-wrap: wrap; }
.summary-item { flex: 1 1 120px; background: #fff; border: 1px solid #E2E8F0; border-radius: 8px; padding: 10px 12px; text-align: center; }
.summary-value { font-size: 20px; font-weight: 700; color: #0F172A; }
.summary-value.good { color: #16A34A; }
.summary-value.bad { color: #DC2626; }
.summary-caption { font-size: 10px; font-weight: 700; letter-spacing: 0.6px; color: #64748B; text-transform: uppercase; margin-top: 2px; }
.card { background: #fff; border: 1px solid #E2E8F0; border-left: 4px solid #2563EB; border-radius: 8px; padding: 16px 20px; margin: 14px 0; page-break-inside: avoid; }
.card-empty { border-left-color: #CBD5E1; opacity: 0.8; }
.card-header { display: flex; align-items: center; gap: 12px; }
h2 { font-size: 16px; letter-spacing: 1.2px; margin: 0; color: #0F172A; flex: 1; }
.card-desc { font-size: 13px; color: #475569; margin-top: 6px; }
.card-caption { font-size: 12px; color: #64748B; font-style: italic; margin-top: 2px; }
.card-na { font-size: 13px; color: #94A3B8; margin-top: 10px; }
.attempt { border-top: 1px solid #EEF2F7; margin-top: 12px; padding-top: 10px; }
.attempt-header { display: flex; align-items: center; gap: 10px; }
.attempt-title { font-size: 11px; font-weight: 700; letter-spacing: 1px; color: #64748B; }
.attempt-rt { font-size: 12px; color: #64748B; }
.pill { display: inline-block; color: #fff; font-size: 11px; font-weight: 700; letter-spacing: 0.6px; padding: 3px 10px; border-radius: 99px; }
.metrics { border-collapse: collapse; margin: 10px 0 4px 0; width: 100%; max-width: 520px; }
.metrics td { font-size: 13px; padding: 3px 10px 3px 0; }
.metric-label { color: #64748B; width: 60%; }
.metric-value { color: #0F172A; font-weight: 600; }
.bar-block { max-width: 420px; margin: 8px 0; }
.bar-head { display: flex; justify-content: space-between; font-size: 12px; color: #475569; margin-bottom: 3px; }
.bar-track { height: 10px; background: #E2E8F0; border-radius: 5px; overflow: hidden; }
.bar-fill { height: 100%; border-radius: 5px; }
.footer { margin-top: 22px; border-top: 1px solid #D7DEE7; padding-top: 14px; }
.disclaimer { font-size: 11px; color: #64748B; }
.footer-meta { font-size: 11px; color: #94A3B8; margin-top: 6px; word-break: break-all; }
@media print {
  .no-print { display: none !important; }
  body { background: #fff; padding: 0; }
  .page { border: none; border-radius: 0; max-width: none; padding: 0; }
}
@page { margin: 18mm; }
";
}
