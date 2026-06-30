using System;
using System.Collections.Generic;
using UnityEngine;

public class SessionContext : MonoBehaviour
{
    private static SessionContext _instance;

    public static SessionContext Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("SessionContext");
                _instance = go.AddComponent<SessionContext>();
            }
            return _instance;
        }
    }

    public string ParticipantId;
    public string FullName;
    public int? Age;
    public int? SessionNumber;
    public string Notes;
    public string SessionGuid;
    public DateTime StartedUtc;
    public bool TutorialCompleted;
    public bool ShipMapViewed;

    // Assessor-chosen assessment length in minutes (set on the intake form).
    public int MissionMinutes = 10;

    // Task types whose one-time instruction card has already been shown this
    // session, so each card appears only the first time that task type docks.
    private readonly HashSet<string> seenTaskTypes = new HashSet<string>();
    public bool HasSeenTask(string typeKey) => seenTaskTypes.Contains(typeKey);
    public void MarkTaskSeen(string typeKey)
    {
        if (!string.IsNullOrEmpty(typeKey)) seenTaskTypes.Add(typeKey);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        if (string.IsNullOrEmpty(SessionGuid))
            SessionGuid = Guid.NewGuid().ToString("N").Substring(0, 12);
    }

    public bool IsValid() =>
        !string.IsNullOrWhiteSpace(ParticipantId) && !string.IsNullOrWhiteSpace(FullName);

    public void Reset()
    {
        ParticipantId = null;
        FullName = null;
        Age = null;
        SessionNumber = null;
        Notes = null;
        SessionGuid = Guid.NewGuid().ToString("N").Substring(0, 12);
        StartedUtc = default;
        TutorialCompleted = false;
        ShipMapViewed = false;
        seenTaskTypes.Clear(); // a new participant should see every card again
        // MissionMinutes is intentionally NOT reset — the assessor's chosen length
        // persists across the reset that happens when a report is dismissed.
    }
}
