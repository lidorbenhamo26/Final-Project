using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ambient distractors that ramp with mission difficulty (GameManager.CurrentDifficulty):
/// red-alert light flashes, an alarm siren, crew chatter, and decoy alert banners.
/// Nothing here is a real task — it's pure noise the player must learn to ignore.
/// Distractions only start after the calm intro (difficulty > 0) and get more
/// frequent as difficulty climbs. Audio hooks are silent until the matching clips
/// exist (AudioManager no-ops on missing files):
///   Resources/Audio/SFX/sfx_siren, Resources/Audio/Voice/voice_chatter
/// </summary>
[DisallowMultipleComponent]
public class DistractionDirector : MonoBehaviour
{
    [Header("Cadence (seconds between distractions)")]
    [SerializeField, Tooltip("Interval at difficulty 0 (rare).")]
    private float intervalCalm = 24f;
    [SerializeField, Tooltip("Interval at difficulty 1 (frequent).")]
    private float intervalIntense = 7f;

    [Header("Light flash")]
    [SerializeField, Tooltip("How many room lights flash per red-alert.")]
    private int lightsPerFlash = 3;
    [SerializeField, Tooltip("Duration of a red-alert flash (seconds).")]
    private float flashDuration = 1.4f;
    [SerializeField] private Color alarmColor = new Color(1f, 0.12f, 0.12f, 1f);

    private static readonly string[] DecoyAlerts =
    {
        "PROXIMITY ALERT",
        "MICROMETEOROID SHOWER",
        "SOLAR FLARE INBOUND",
        "HULL STRESS — MONITORING",
        "COOLANT PRESSURE FLUCTUATION",
        "RADIATION SPIKE — SECTOR 7",
        "DEBRIS FIELD AHEAD",
        "MAGNETIC INTERFERENCE",
    };

    private struct LightState
    {
        public Light light;
        public float baseIntensity;
        public Color baseColor;
    }

    private readonly List<LightState> roomLights = new();
    private float nextEventTime;
    private bool flashing;
    private int decoyIndex;

    private void Start()
    {
        GatherLights();
        nextEventTime = Time.time + intervalCalm; // first one no earlier than a calm gap
    }

    private void GatherLights()
    {
        var all = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var l in all)
        {
            if (l == null || l.type == LightType.Directional) continue; // leave the key light alone
            roomLights.Add(new LightState { light = l, baseIntensity = l.intensity, baseColor = l.color });
        }
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.MissionActive || GameManager.IsDebugFrozen) return;

        float diff = gm.CurrentDifficulty;
        if (diff <= 0.02f) return; // calm intro — no distractions yet

        if (Time.time >= nextEventTime)
        {
            TriggerDistraction(diff);
            float interval = Mathf.Lerp(intervalCalm, intervalIntense, diff);
            nextEventTime = Time.time + interval * Random.Range(0.8f, 1.25f);
        }
    }

    private void TriggerDistraction(float diff)
    {
        // Weighted pick. Decoy alerts are the most common (cheap, harmless noise);
        // light flashes and siren get more likely as it gets intense.
        float roll = Random.value;
        if (roll < 0.45f) DecoyAlert();
        else if (roll < 0.45f + 0.30f * Mathf.Lerp(0.5f, 1f, diff)) FlashAlarm();
        else if (roll < 0.9f) Siren();
        else Chatter();
    }

    private void DecoyAlert()
    {
        if (HUDManager.Instance == null) return;
        string msg = DecoyAlerts[decoyIndex % DecoyAlerts.Length];
        decoyIndex++;
        HUDManager.Instance.ShowAlertBanner(msg, 1.6f);
    }

    private void Siren()
    {
        AudioManager.Instance.PlaySfx("siren", 0.7f);
    }

    private void Chatter()
    {
        AudioManager.Instance.PlayVoice("chatter", 0.8f);
    }

    private void FlashAlarm()
    {
        if (flashing || roomLights.Count == 0) return;
        StartCoroutine(CoFlash());
    }

    private IEnumerator CoFlash()
    {
        flashing = true;

        // Pick a few random lights.
        var picked = new List<int>();
        int n = Mathf.Min(lightsPerFlash, roomLights.Count);
        while (picked.Count < n)
        {
            int idx = Random.Range(0, roomLights.Count);
            if (!picked.Contains(idx)) picked.Add(idx);
        }

        AudioManager.Instance.PlaySfx("alert_pulse", 0.6f);

        float t = 0f;
        while (t < flashDuration)
        {
            t += Time.deltaTime;
            // ~3 Hz pulse between dim and bright-red.
            float pulse = (Mathf.Sin(t * Mathf.PI * 2f * 3f) + 1f) * 0.5f;
            foreach (int idx in picked)
            {
                var ls = roomLights[idx];
                if (ls.light == null) continue;
                ls.light.color = Color.Lerp(ls.baseColor, alarmColor, 0.85f);
                ls.light.intensity = ls.baseIntensity * Mathf.Lerp(0.35f, 1.6f, pulse);
            }
            yield return null;
        }

        // Restore.
        foreach (int idx in picked)
        {
            var ls = roomLights[idx];
            if (ls.light == null) continue;
            ls.light.color = ls.baseColor;
            ls.light.intensity = ls.baseIntensity;
        }
        flashing = false;
    }
}
