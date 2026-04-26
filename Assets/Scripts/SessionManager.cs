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

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStartTime = Time.time;
        string fileName = "MissionFocus_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        logFilePath = Path.Combine(Application.persistentDataPath, fileName);
        logBuffer = new StringBuilder();
        logBuffer.AppendLine("RealTime_Timestamp,Mission_Time,Event_Type,Station,Priority,Action_Taken,Reaction_Time");
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
        string row = DateTime.Now.ToString("HH:mm:ss.fff") + "," + (Time.time - sessionStartTime).ToString("F2") + ",Task_Spawned," + task.StationName + "," + task.Priority.ToString() + ",Pending,N/A";
        logBuffer.AppendLine(row);
        File.WriteAllText(logFilePath, logBuffer.ToString());
    }

    private void HandleTaskResolved(MissionTask task, TaskResult result, float reactionTime)
    {
        string row = DateTime.Now.ToString("HH:mm:ss.fff") + "," + (Time.time - sessionStartTime).ToString("F2") + ",Task_Resolved," + task.StationName + "," + task.Priority.ToString() + "," + result.ToString() + "," + reactionTime.ToString("F3");
        logBuffer.AppendLine(row);
        File.WriteAllText(logFilePath, logBuffer.ToString());
    }

    public void LogCustomEvent(string eventType, string station, string action)
    {
        string row = DateTime.Now.ToString("HH:mm:ss.fff") + "," + (Time.time - sessionStartTime).ToString("F2") + "," + eventType + "," + station + ",N/A," + action + ",N/A";
        logBuffer.AppendLine(row);
        File.WriteAllText(logFilePath, logBuffer.ToString());
    }

    private void OnApplicationQuit()
    {
        File.WriteAllText(logFilePath, logBuffer.ToString());
    }
}
