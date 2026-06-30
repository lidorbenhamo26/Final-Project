using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Light environmental pressure so the station feels alive without distracting from
/// the cognitive assessment:
///   - periodic quiet crew radio chatter (NotificationFeed line + soft VO),
///   - escalation warnings when the alert level rises to HIGH / CRITICAL,
///   - a pulsing red-alert edge vignette + periodic alarm while any system is critical.
/// All VO/SFX ids are optional (AudioManager no-ops if a clip is missing). Reads
/// <see cref="ShipState"/> for alert/critical status. Spawned by GameManager for the
/// real mission only (not the tutorial). The alien's own proximity chirps
/// (AlienCuriosity) already cover the "alien reacts when you're near" idea.
/// </summary>
[DisallowMultipleComponent]
public class AmbienceDirector : MonoBehaviour
{
    // Kind drives both the on-screen prefix and the procedural sound paired with the
    // line, so radio crew / automated robot / alien interference / status / caution
    // each read and sound distinct. Background distraction, never mission-critical.
    private enum Kind { Crew, Robot, Alien, Status, Warning }

    private struct Line { public string Text; public Kind Kind; public Line(string t, Kind k) { Text = t; Kind = k; } }

    private static readonly Line[] Chatter =
    {
        // Crew radio
        new Line("Engineering: pressure holding steady, over.", Kind.Crew),
        new Line("Medbay: crew vitals nominal.", Kind.Crew),
        new Line("Bridge: stay sharp out there, Commander.", Kind.Crew),
        new Line("Cargo: containment secure, over.", Kind.Crew),
        new Line("Hydroponics: oxygen output steady.", Kind.Crew),
        new Line("Galley: rationing on schedule, no complaints.", Kind.Crew),
        new Line("Deck two: just passing through, all clear.", Kind.Crew),
        // Automated / robot announcements
        new Line("AUTO-SYS: routine diagnostic complete.", Kind.Robot),
        new Line("AUTO-SYS: corridor lighting recalibrated.", Kind.Robot),
        new Line("AUTO-SYS: airlock seals verified.", Kind.Robot),
        new Line("AUTO-SYS: coolant cycle nominal.", Kind.Robot),
        new Line("AUTO-SYS: gravity plating stable.", Kind.Robot),
        new Line("AUTO-SYS: waste reclamation at sixty percent.", Kind.Robot),
        // Alien interference
        new Line("≈≈ unknown signal on the long-range array ≈≈", Kind.Alien),
        new Line("≈≈ ...kzzt... no translation available ≈≈", Kind.Alien),
        new Line("≈≈ proximity contact — bearing unclear ≈≈", Kind.Alien),
        new Line("≈≈ static bursts across channel seven ≈≈", Kind.Alien),
        // Status updates
        new Line("Status: sector sweep progressing.", Kind.Status),
        new Line("Status: reactor output within limits.", Kind.Status),
        new Line("Status: navigation drift within tolerance.", Kind.Status),
        new Line("Status: comms relay synced.", Kind.Status),
        // Non-critical cautions
        new Line("Caution: minor power fluctuation, deck three.", Kind.Warning),
        new Line("Caution: debris field ahead, keep watch.", Kind.Warning),
        new Line("Caution: thermal load creeping up.", Kind.Warning),
    };

    private float _nextChatter;
    private float _nextAmbientSfx;
    private int _chatterIdx;
    private string _lastAlert = "NOMINAL";
    private float _alarmCooldown;
    private int _lastSector = 1;
    private Image _vignette;

    private void Start()
    {
        _nextChatter = Time.time + Random.Range(4f, 8f);
        _nextAmbientSfx = Time.time + Random.Range(3f, 7f);
        // Randomize the start point so runs don't always open with the same line.
        _chatterIdx = Random.Range(0, Chatter.Length);
        BuildVignette();
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || !gm.MissionActive) { SetVignette(0f); return; }
        if (GameManager.IsDebugFrozen) return;

        TickSector();
        TickChatter();
        TickAmbientSfx();
        TickAlert();
        TickCritical();
    }

    // Announce sector transitions — a light "progress toward rescue" spine.
    private void TickSector()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        int s = gm.CurrentSector;
        if (s <= _lastSector) return;
        _lastSector = s;
        if (s >= GameManager.TotalSectors)
            NotificationFeed.Instance?.Push("Final sector — rescue is close. Hold together.");
        else
            NotificationFeed.Instance?.Push("Entering Sector " + s + " of " + GameManager.TotalSectors + ".");
        AudioManager.Instance?.PlayVoice("sector_change");
    }

    private void TickChatter()
    {
        if (Time.time < _nextChatter) return;
        float diff = GameManager.Instance != null ? GameManager.Instance.CurrentDifficulty : 0f;
        // Much busier than before: ~9-16s when calm, tightening toward ~6-10s when intense.
        _nextChatter = Time.time + Random.Range(Mathf.Lerp(9f, 6f, diff), Mathf.Lerp(16f, 10f, diff));

        // Don't chatter over a critical situation — alarms own that moment.
        if (ShipState.Instance != null && ShipState.Instance.CriticalCount() > 0) return;

        var line = NextLine();
        NotificationFeed.Instance?.Push(line.Text);
        // Procedural sound matched to the source of the line (no recorded VO needed).
        PlayKindSfx(line.Kind);
        // The crew-chatter VO id still fires if a clip is ever added; silent until then.
        if (line.Kind == Kind.Crew) AudioManager.Instance?.PlayVoice("crew_chatter", 0.5f);
    }

    // Step through the lines with a small random jump so the same line never repeats
    // back-to-back but every line is still heard over the mission.
    private Line NextLine()
    {
        _chatterIdx = (_chatterIdx + Random.Range(1, 4)) % Chatter.Length;
        return Chatter[_chatterIdx];
    }

    // An independent layer of subtle non-speech blips/pings/hums so the ship is never
    // silent between chatter lines.
    private void TickAmbientSfx()
    {
        if (Time.time < _nextAmbientSfx) return;
        float diff = GameManager.Instance != null ? GameManager.Instance.CurrentDifficulty : 0f;
        _nextAmbientSfx = Time.time + Random.Range(Mathf.Lerp(7f, 4f, diff), Mathf.Lerp(13f, 8f, diff));
        if (ShipState.Instance != null && ShipState.Instance.CriticalCount() > 0) return;

        int roll = Random.Range(0, 5);
        ProceduralSfx.Kind k = roll == 0 ? ProceduralSfx.Kind.Ping
                             : roll == 1 ? ProceduralSfx.Kind.Hum
                             : roll == 2 ? ProceduralSfx.Kind.Beep
                             : ProceduralSfx.Kind.Blip;
        AudioManager.Instance?.PlayClip(ProceduralSfx.Get(k, Random.Range(0, 3)), AudioBus.Sfx, 0.25f);
    }

    private void PlayKindSfx(Kind kind)
    {
        var am = AudioManager.Instance;
        if (am == null) return;
        switch (kind)
        {
            case Kind.Crew:    am.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Static, Random.Range(0, 3)), AudioBus.Sfx, 0.30f); break;
            case Kind.Robot:   am.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Blip,   Random.Range(0, 3)), AudioBus.Sfx, 0.35f); break;
            case Kind.Alien:   am.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Chirp,  Random.Range(0, 3)), AudioBus.Sfx, 0.30f); break;
            case Kind.Status:  am.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Ping,   Random.Range(0, 3)), AudioBus.Sfx, 0.28f); break;
            case Kind.Warning: am.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Beep,   Random.Range(0, 3)), AudioBus.Sfx, 0.32f); break;
        }
    }

    private void TickAlert()
    {
        var ship = ShipState.Instance;
        if (ship == null) return;
        string lvl = ship.AlertLevel;
        if (lvl == _lastAlert) return;

        if (lvl == "CRITICAL")
            AudioManager.Instance?.PlayVoice("alert_critical");
        else if (lvl == "HIGH" && _lastAlert != "CRITICAL")
        {
            NotificationFeed.Instance?.Push("Warning: system load rising.");
            AudioManager.Instance?.PlayVoice("alert_high");
        }
        _lastAlert = lvl;
    }

    private void TickCritical()
    {
        var ship = ShipState.Instance;
        bool critical = ship != null && ship.CriticalCount() > 0;
        if (!critical) { SetVignette(0f); return; }

        SetVignette(0.10f + 0.08f * Mathf.Abs(Mathf.Sin(Time.time * 4f)));
        _alarmCooldown -= Time.deltaTime;
        if (_alarmCooldown <= 0f)
        {
            // Recorded "alarm" SFX no-ops if missing — back it with a procedural
            // double-beep so red alert is always audible.
            AudioManager.Instance?.PlaySfx("alarm", 0.5f);
            AudioManager.Instance?.PlayClip(ProceduralSfx.Get(ProceduralSfx.Kind.Beep, 1), AudioBus.Sfx, 0.5f);
            _alarmCooldown = 2.5f;
        }
    }

    private void SetVignette(float alpha)
    {
        if (_vignette == null) return;
        var c = _vignette.color; c.a = alpha; _vignette.color = c;
    }

    private void BuildVignette()
    {
        var canvasGO = new GameObject("Ambience_Canvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110; // above HUD, below the beat/summary panels
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        var go = new GameObject("RedAlertVignette", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(canvasGO.transform, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        _vignette = go.GetComponent<Image>();
        _vignette.raycastTarget = false;
        _vignette.sprite = BuildVignetteSprite();
        _vignette.type = Image.Type.Simple;
        _vignette.color = new Color(0.85f, 0.05f, 0.05f, 0f); // red, alpha driven by SetVignette
    }

    // Radial sprite: transparent center fading to opaque edges, so the red reads as
    // an alarm vignette rather than tinting the whole screen.
    private static Sprite BuildVignetteSprite()
    {
        const int N = 128;
        var px = new Color[N * N];
        Vector2 c = new Vector2((N - 1) * 0.5f, (N - 1) * 0.5f);
        float maxR = N * 0.5f;
        for (int y = 0; y < N; y++)
        for (int x = 0; x < N; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c) / maxR; // 0 center .. ~1 corner
            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.55f) / 0.45f));
            px[y * N + x] = new Color(1f, 1f, 1f, a);
        }
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }
}
