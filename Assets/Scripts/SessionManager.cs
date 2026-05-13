using System;
using System.IO;
using System.Text;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    private string logFilePath;
    private string metaFilePath;
    private float sessionStartTime;
    private StringBuilder logBuffer;
    private bool logOpen;

    private string participantIdCsv;
    private string participantNameCsv;
    private string sessionGuidCsv;
    private string sessionNumberCsv;

    public int TasksTotal { get; private set; }
    public int TasksPassed { get; private set; }
    public int TasksFailed { get; private set; }
    public float TotalReactionTime { get; private set; }
    public float AverageReactionTime => TasksTotal > 0 ? TotalReactionTime / TasksTotal : 0f;
    public string LogFilePath => logFilePath;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        logBuffer = new StringBuilder();
    }

    public void ResetForNewMission()
    {
        TasksTotal = 0;
        TasksPassed = 0;
        TasksFailed = 0;
        TotalReactionTime = 0f;
        logOpen = false;
    }

    private void EnsureLogOpen()
    {
        if (logOpen) return;
        logOpen = true;

        sessionStartTime = Time.time;
        var ctx = SessionContext.Instance;
        participantIdCsv   = CsvEscape(ctx.ParticipantId);
        participantNameCsv = CsvEscape(ctx.FullName);
        sessionGuidCsv     = CsvEscape(ctx.SessionGuid);
        sessionNumberCsv   = ctx.SessionNumber.HasValue ? ctx.SessionNumber.Value.ToString() : "";

        string stem = "MissionFocus_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
        logFilePath = Path.Combine(Application.persistentDataPath, stem + ".csv");
        metaFilePath = Path.Combine(Application.persistentDataPath, stem + ".meta.json");

        logBuffer.Length = 0;
        logBuffer.AppendLine("RealTime_Timestamp,Mission_Time,Participant_Id,Participant_Name,Session_Guid,Session_Number,Event_Type,Station,Priority,Action_Taken,Reaction_Time");

        WriteSidecar(ctx);
        Debug.Log("[SessionManager] Logging to: " + logFilePath);
    }

    private void OnEnable()
    {
        MissionTask.OnTaskSpawned += HandleTaskSpawned;
        MissionTask.OnTaskResolved += HandleTaskResolved;
    }

    private void OnDisable()
    {
        MissionTask.OnTaskSpawned -= HandleTaskSpawned;
        MissionTask.OnTaskResolved -= HandleTaskResolved;
    }

    private void HandleTaskSpawned(MissionTask task)
    {
        EnsureLogOpen();
        logBuffer.AppendLine(RowPrefix() + ",Task_Spawned," + CsvEscape(task.StationName) + "," + task.Priority + ",Pending,N/A");
        FlushLog();
    }

    private void HandleTaskResolved(MissionTask task, TaskResult result, float reactionTime)
    {
        EnsureLogOpen();
        TasksTotal++;
        if (result == TaskResult.Success) TasksPassed++;
        else TasksFailed++;
        TotalReactionTime += reactionTime;

        logBuffer.AppendLine(RowPrefix() + ",Task_Resolved," + CsvEscape(task.StationName) + "," + task.Priority + "," + result + "," + reactionTime.ToString("F3"));
        FlushLog();
    }

    public void LogCustomEvent(string eventType, string station, string action)
    {
        EnsureLogOpen();
        logBuffer.AppendLine(RowPrefix() + "," + CsvEscape(eventType) + "," + CsvEscape(station) + ",N/A," + CsvEscape(action) + ",N/A");
        FlushLog();
    }

    private string RowPrefix()
    {
        return DateTime.Now.ToString("HH:mm:ss.fff")
             + "," + (Time.time - sessionStartTime).ToString("F2")
             + "," + participantIdCsv
             + "," + participantNameCsv
             + "," + sessionGuidCsv
             + "," + sessionNumberCsv;
    }

    private void FlushLog()
    {
        try
        {
            File.WriteAllText(logFilePath, logBuffer.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SessionManager] Failed to write log: " + ex.Message);
        }
    }

    private void WriteSidecar(SessionContext ctx)
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"participantId\": " + JsonStr(ctx.ParticipantId) + ",");
        sb.AppendLine("  \"fullName\": " + JsonStr(ctx.FullName) + ",");
        sb.AppendLine("  \"age\": " + (ctx.Age.HasValue ? ctx.Age.Value.ToString() : "null") + ",");
        sb.AppendLine("  \"sessionNumber\": " + (ctx.SessionNumber.HasValue ? ctx.SessionNumber.Value.ToString() : "null") + ",");
        sb.AppendLine("  \"notes\": " + JsonStr(ctx.Notes) + ",");
        sb.AppendLine("  \"sessionGuid\": " + JsonStr(ctx.SessionGuid) + ",");
        sb.AppendLine("  \"startedUtc\": " + JsonStr(ctx.StartedUtc == default ? "" : ctx.StartedUtc.ToString("o")) + ",");
        sb.AppendLine("  \"missionLogOpenedUtc\": " + JsonStr(DateTime.UtcNow.ToString("o")) + ",");
        sb.AppendLine("  \"tutorialCompleted\": " + (ctx.TutorialCompleted ? "true" : "false") + ",");
        sb.AppendLine("  \"shipMapViewed\": " + (ctx.ShipMapViewed ? "true" : "false"));
        sb.AppendLine("}");
        try
        {
            File.WriteAllText(metaFilePath, sb.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[SessionManager] Failed to write meta: " + ex.Message);
        }
    }

    private static string CsvEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0) return s;
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string JsonStr(string s)
    {
        if (s == null) return "null";
        var sb = new StringBuilder(s.Length + 2);
        sb.Append('"');
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"':  sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 0x20) sb.AppendFormat("\\u{0:x4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private void OnApplicationQuit()
    {
        if (logOpen) FlushLog();
    }
}
