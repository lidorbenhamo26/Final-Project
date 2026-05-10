using System;
using System.IO;
using System.Text;
using UnityEngine;

public class SessionManager : MonoBehaviour
{
    public static SessionManager Instance { get; private set; }

    private string logFilePath;
    private float sessionStartTime;
    private StringBuilder logBuffer;

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
        StartNewLog();
        Debug.Log("[SessionManager] Logging to: " + logFilePath);
    }

    private void StartNewLog()
    {
        sessionStartTime = Time.time;
        string fileName = "MissionFocus_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        logFilePath = Path.Combine(Application.persistentDataPath, fileName);
        logBuffer = new StringBuilder();
        logBuffer.AppendLine("RealTime_Timestamp,Mission_Time,Event_Type,Station,Priority,Action_Taken,Reaction_Time");
    }

    public void ResetForNewMission()
    {
        TasksTotal = 0;
        TasksPassed = 0;
        TasksFailed = 0;
        TotalReactionTime = 0f;
        StartNewLog();
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
        string row = DateTime.Now.ToString("HH:mm:ss.fff") + "," + (Time.time - sessionStartTime).ToString("F2") + ",Task_Spawned," + task.StationName + "," + task.Priority.ToString() + ",Pending,N/A";
        logBuffer.AppendLine(row);
        FlushLog();
    }

    private void HandleTaskResolved(MissionTask task, TaskResult result, float reactionTime)
    {
        TasksTotal++;
        if (result == TaskResult.Success) TasksPassed++;
        else TasksFailed++;
        TotalReactionTime += reactionTime;

        string row = DateTime.Now.ToString("HH:mm:ss.fff") + "," + (Time.time - sessionStartTime).ToString("F2") + ",Task_Resolved," + task.StationName + "," + task.Priority.ToString() + "," + result.ToString() + "," + reactionTime.ToString("F3");
        logBuffer.AppendLine(row);
        FlushLog();
    }

    public void LogCustomEvent(string eventType, string station, string action)
    {
        string row = DateTime.Now.ToString("HH:mm:ss.fff") + "," + (Time.time - sessionStartTime).ToString("F2") + "," + eventType + "," + station + ",N/A," + action + ",N/A";
        logBuffer.AppendLine(row);
        FlushLog();
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

    private void OnApplicationQuit()
    {
        FlushLog();
    }
}
