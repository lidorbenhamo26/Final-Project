using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Button wiring for the report screen; provided by the controller.</summary>
public class ReportCallbacks
{
    public Action NewParticipant;
    public Action RunAgain;
    public Action ExportHtml;
    public Action ExportCsv;
    public Action OpenDataFolder;
    public Action Close;
    public bool ShowClose;
}

/// <summary>
/// Builds the assessor report visual tree (UI Toolkit) from a ReportData
/// snapshot. Pure view: no state, no data access — styling lives in
/// Resources/UI/Report/AssessmentReport.uss.
/// </summary>
public static class AssessmentReportView
{
    public static void Build(VisualElement root, ReportData data, ReportCallbacks cb)
    {
        root.Clear();

        var overlay = new VisualElement();
        overlay.AddToClassList("report-overlay");
        root.Add(overlay);

        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("report-scroll");
        overlay.Add(scroll);

        var page = new VisualElement();
        page.AddToClassList("report-page");
        scroll.Add(page);

        BuildHeader(page, data);
        if (data.MissionActive) BuildInProgressBanner(page);
        BuildSummaryStrip(page, data);
        foreach (var section in data.Sections)
            BuildScaleCard(page, section);
        BuildFooter(page, data, cb);
    }

    // ---------------------------------------------------------------- header

    private static void BuildHeader(VisualElement page, ReportData data)
    {
        var topRow = new VisualElement();
        topRow.AddToClassList("header-top-row");
        page.Add(topRow);

        var titleBlock = new VisualElement();
        titleBlock.AddToClassList("header-title-block");
        topRow.Add(titleBlock);

        var title = new Label("EXECUTIVE FUNCTION ASSESSMENT REPORT");
        title.AddToClassList("report-title");
        titleBlock.Add(title);

        var subtitle = new Label("Space-Station Cognitive Task Battery  ·  BRIEF-A–aligned scales");
        subtitle.AddToClassList("report-subtitle");
        titleBlock.Add(subtitle);

        var badge = new Label("RESEARCH PROTOTYPE");
        badge.AddToClassList("report-badge");
        topRow.Add(badge);

        page.Add(Divider());

        var grid = new VisualElement();
        grid.AddToClassList("demo-grid");
        page.Add(grid);

        AddDemoItem(grid, "PARTICIPANT ID", Safe(data.ParticipantId));
        AddDemoItem(grid, "FULL NAME", Safe(data.FullName));
        AddDemoItem(grid, "AGE", data.Age.HasValue ? data.Age.Value.ToString() : "—");
        AddDemoItem(grid, "SESSION #", data.SessionNumber.HasValue ? data.SessionNumber.Value.ToString() : "—");
        AddDemoItem(grid, "SESSION DATE", data.StartedUtc == default
            ? data.GeneratedLocal.ToString("yyyy-MM-dd HH:mm")
            : data.StartedUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"));
        AddDemoItem(grid, "SESSION GUID", Safe(data.SessionGuid));

        if (!string.IsNullOrWhiteSpace(data.Notes))
        {
            var notes = new VisualElement();
            notes.AddToClassList("demo-notes");
            var nl = new Label("NOTES");
            nl.AddToClassList("demo-label");
            notes.Add(nl);
            var nv = new Label(data.Notes);
            nv.AddToClassList("demo-value");
            nv.AddToClassList("demo-notes-value");
            notes.Add(nv);
            page.Add(notes);
        }
    }

    private static void AddDemoItem(VisualElement grid, string label, string value)
    {
        var item = new VisualElement();
        item.AddToClassList("demo-item");
        var l = new Label(label);
        l.AddToClassList("demo-label");
        item.Add(l);
        var v = new Label(value);
        v.AddToClassList("demo-value");
        item.Add(v);
        grid.Add(item);
    }

    private static void BuildInProgressBanner(VisualElement page)
    {
        var banner = new Label("SESSION IN PROGRESS — results below are partial");
        banner.AddToClassList("report-banner");
        page.Add(banner);
    }

    // --------------------------------------------------------------- summary

    private static void BuildSummaryStrip(VisualElement page, ReportData data)
    {
        var row = new VisualElement();
        row.AddToClassList("summary-row");
        page.Add(row);

        float accuracy = data.TasksTotal > 0 ? data.TasksPassed * 100f / data.TasksTotal : 0f;

        AddSummaryItem(row, data.TasksTotal.ToString(), "TASKS ADMINISTERED");
        AddSummaryItem(row, data.TasksPassed.ToString(), "PASSED", "summary-value--good");
        AddSummaryItem(row, data.TasksFailed.ToString(), "FAILED", data.TasksFailed > 0 ? "summary-value--bad" : null);
        AddSummaryItem(row, accuracy.ToString("F0") + "%", "OVERALL ACCURACY");
        AddSummaryItem(row, data.AverageReactionTime.ToString("F1") + "s", "MEAN COMPLETION TIME");
        AddSummaryItem(row, ReportFormat.FormatMissionStatus(data), "MISSION STATUS");
    }

    private static void AddSummaryItem(VisualElement row, string value, string caption, string extraClass = null)
    {
        var item = new VisualElement();
        item.AddToClassList("summary-item");
        var v = new Label(value);
        v.AddToClassList("summary-value");
        if (extraClass != null) v.AddToClassList(extraClass);
        item.Add(v);
        var c = new Label(caption);
        c.AddToClassList("summary-caption");
        item.Add(c);
        row.Add(item);
    }

    // ------------------------------------------------------------ scale card

    private static void BuildScaleCard(VisualElement page, ReportData.ScaleSection section)
    {
        var card = new VisualElement();
        card.AddToClassList("scale-card");
        if (!section.Administered) card.AddToClassList("scale-card--empty");
        page.Add(card);

        var header = new VisualElement();
        header.AddToClassList("scale-header");
        card.Add(header);

        var title = new Label(section.Title);
        title.AddToClassList("scale-title");
        header.Add(title);

        var spacer = new VisualElement();
        spacer.AddToClassList("flex-spacer");
        header.Add(spacer);

        if (section.Administered)
        {
            // Header pill summarizes the most recent attempt.
            var last = section.Records[section.Records.Count - 1];
            header.Add(MakePill(last.Result));
        }

        var desc = new Label(section.Description);
        desc.AddToClassList("scale-desc");
        card.Add(desc);

        if (!string.IsNullOrEmpty(section.TaskCaption))
        {
            var caption = new Label("Task: " + section.TaskCaption);
            caption.AddToClassList("scale-caption");
            card.Add(caption);
        }

        if (!section.Administered)
        {
            var empty = new Label("Not administered in this session.");
            empty.AddToClassList("scale-empty");
            card.Add(empty);
            return;
        }

        for (int i = 0; i < section.Records.Count; i++)
            BuildAttempt(card, section.Records[i], i, section.Records.Count);
    }

    private static void BuildAttempt(VisualElement card, TaskRecord record, int index, int totalAttempts)
    {
        var attempt = new VisualElement();
        attempt.AddToClassList("attempt");
        card.Add(attempt);

        var header = new VisualElement();
        header.AddToClassList("attempt-header");
        attempt.Add(header);

        string titleText = totalAttempts > 1 ? "ATTEMPT " + (index + 1) : "RESULT";
        var title = new Label(titleText);
        title.AddToClassList("attempt-title");
        header.Add(title);

        header.Add(MakePill(record.Result));

        if (record.Result.HasValue)
        {
            var rt = new Label("Completed in " + record.ReactionTimeS.ToString("F1") + "s");
            rt.AddToClassList("attempt-rt");
            header.Add(rt);
        }

        if (record.Metrics.Count > 0)
        {
            var grid = new VisualElement();
            grid.AddToClassList("metric-grid");
            attempt.Add(grid);

            foreach (var kv in record.Metrics)
            {
                var row = new VisualElement();
                row.AddToClassList("metric-row");
                var l = new Label(ReportFormat.LabelFor(kv.Key));
                l.AddToClassList("metric-label");
                row.Add(l);
                var v = new Label(ReportFormat.ValueFor(kv.Key, kv.Value));
                v.AddToClassList("metric-value");
                row.Add(v);
                grid.Add(row);
            }
        }

        AddPerformanceBars(attempt, record);
    }

    private static void AddPerformanceBars(VisualElement attempt, TaskRecord record)
    {
        if (record.TryGetMetricFloat("accuracy", out float acc))
            AddBar(attempt, "Accuracy", Mathf.Clamp01(acc), "16A34A");
        if (record.TryGetMetricFloat("hitRate", out float hr))
            AddBar(attempt, "Hit rate", Mathf.Clamp01(hr), "16A34A");
        if (record.TryGetMetricFloat("faRate", out float fa))
            AddBar(attempt, "False-alarm rate", Mathf.Clamp01(fa), "DC2626");
        if (record.TryGetMetricFloat("deliveredTimeS", out float used)
            && record.TryGetMetricFloat("timeLimitS", out float limit) && limit > 0f)
            AddBar(attempt, "Time used", Mathf.Clamp01(used / limit), "2563EB");
    }

    private static void AddBar(VisualElement parent, string label, float fraction, string fillHex)
    {
        var block = new VisualElement();
        block.AddToClassList("bar-block");
        parent.Add(block);

        var head = new VisualElement();
        head.AddToClassList("bar-head");
        block.Add(head);

        var l = new Label(label);
        l.AddToClassList("bar-label");
        head.Add(l);

        var pct = new Label(Mathf.RoundToInt(fraction * 100f) + "%");
        pct.AddToClassList("bar-pct");
        head.Add(pct);

        var track = new VisualElement();
        track.AddToClassList("bar-track");
        block.Add(track);

        var fill = new VisualElement();
        fill.AddToClassList("bar-fill");
        fill.style.width = Length.Percent(Mathf.Clamp01(fraction) * 100f);
        fill.style.backgroundColor = new StyleColor(ParseHex(fillHex));
        track.Add(fill);
    }

    // ---------------------------------------------------------------- footer

    private static void BuildFooter(VisualElement page, ReportData data, ReportCallbacks cb)
    {
        page.Add(Divider());

        var disclaimer = new Label(
            "Research prototype developed for an academic project. This is not a clinical " +
            "instrument; results are game-derived performance indicators and do not constitute " +
            "a diagnosis or a standardized BRIEF-A administration.");
        disclaimer.AddToClassList("disclaimer");
        page.Add(disclaimer);

        var meta = new Label(
            "Generated " + data.GeneratedLocal.ToString("yyyy-MM-dd HH:mm:ss") +
            (string.IsNullOrEmpty(data.LogFilePath) ? "" : "   ·   Raw event log: " + data.LogFilePath));
        meta.AddToClassList("footer-meta");
        page.Add(meta);

        var status = new Label("") { name = "export-status" };
        status.AddToClassList("export-status");
        page.Add(status);

        var row = new VisualElement();
        row.AddToClassList("btn-row");
        page.Add(row);

        row.Add(MakeButton("EXPORT HTML REPORT", cb.ExportHtml, "report-btn--primary"));
        row.Add(MakeButton("EXPORT TASK CSV", cb.ExportCsv, null));
        row.Add(MakeButton("OPEN DATA FOLDER", cb.OpenDataFolder, "report-btn--ghost"));

        var spacer = new VisualElement();
        spacer.AddToClassList("flex-spacer");
        row.Add(spacer);

        row.Add(MakeButton("NEW PARTICIPANT", cb.NewParticipant, "report-btn--ghost"));
        row.Add(MakeButton("RUN AGAIN", cb.RunAgain, "report-btn--primary"));
        if (cb.ShowClose)
            row.Add(MakeButton("CLOSE", cb.Close, null));
    }

    // --------------------------------------------------------------- helpers

    private static Button MakeButton(string text, Action onClick, string extraClass)
    {
        var btn = new Button(() => onClick?.Invoke()) { text = text };
        btn.AddToClassList("report-btn");
        if (extraClass != null) btn.AddToClassList(extraClass);
        return btn;
    }

    private static VisualElement MakePill(TaskResult? result)
    {
        var pill = new Label(ReportFormat.ResultLabel(result));
        pill.AddToClassList("pill");
        pill.style.backgroundColor = new StyleColor(ParseHex(ReportFormat.ResultColorHex(result)));
        return pill;
    }

    private static VisualElement Divider()
    {
        var d = new VisualElement();
        d.AddToClassList("divider");
        return d;
    }

    private static Color ParseHex(string hex)
    {
        return ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.gray;
    }

    private static string Safe(string s) => string.IsNullOrWhiteSpace(s) ? "—" : s;
}
