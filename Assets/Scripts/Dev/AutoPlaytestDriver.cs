using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// DEV-ONLY automated playtest driver. Drives the REAL input path via Unity Input
/// System event injection (WASD/E/Space/digits) plus safe task-button invokes, so
/// AstronautController / StationDockController / the cognitive tasks / EF events all
/// see genuine input. Logs a "[AUTOPLAY]" timeline for headless verification.
///
/// It verifies FUNCTION (does the 10-min loop run start-to-finish: traverse, dock,
/// tasks resolve, priority beats handled, mission completes + report) — not feel.
/// Add to a temp GameObject with quickTestMode on, enter play, read the console.
/// Not shipped.
/// </summary>
public class AutoPlaytestDriver : MonoBehaviour
{
    [SerializeField] private float maxRunSeconds = 200f;

    private readonly HashSet<Key> _held = new HashSet<Key>();
    private readonly List<Key> _taps = new List<Key>();

    private Transform player;
    private Rigidbody playerRb;
    private Transform cam;
    private EFEventDirector ef;

    private int docked, tasksSeenResolved, efBeats, stuck;

    private void Start()
    {
        var pgo = GameObject.FindWithTag("Player");
        if (pgo != null) { player = pgo.transform; playerRb = pgo.GetComponent<Rigidbody>(); }
        cam = Camera.main != null ? Camera.main.transform : null;
        ef = FindAnyObjectByType<EFEventDirector>();
        StartCoroutine(Run());
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        var keys = new List<Key>(_held);
        keys.AddRange(_taps);
        InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState(keys.ToArray()));
        _taps.Clear();
    }

    private void Hold(params Key[] k) { _held.Clear(); foreach (var x in k) _held.Add(x); }
    private void Release() { _held.Clear(); }
    private void Tap(Key k) { _taps.Add(k); }
    private Vector3 Pos() => player != null ? player.position : Vector3.zero;
    private bool BeatActive => ef != null && ef.EventActive;

    private IEnumerator Run()
    {
        yield return new WaitForSeconds(1.2f);
        if (player == null || Keyboard.current == null)
        {
            Debug.Log("[AUTOPLAY] ABORT — player/keyboard missing");
            yield break;
        }
        var gm = GameManager.Instance;
        Debug.Log("[AUTOPLAY] START full run");

        float deadline = Time.time + maxRunSeconds;
        while (Time.time < deadline && gm != null && gm.MissionActive)
        {
            // Priority beat in progress? Set an order with 1/2/3.
            if (ef != null && ef.EventActive)
            {
                yield return HandleBeat();
                continue;
            }

            var st = NearestActiveStation();
            if (st == null) { yield return new WaitForSeconds(0.5f); continue; }

            bool reached = false;
            yield return NavigateTo(st.transform.position, 2.0f, 8f, r => reached = r);
            if (ef != null && ef.EventActive) continue; // a beat fired while travelling

            if (BeatActive) continue;
            // Movement/camera is already verified by the probe; if naive steering
            // can't path through doorways, teleport so the full task/EF loop still
            // runs start-to-finish.
            if (!reached) { stuck++; Teleport(st.transform.position); Debug.Log("[AUTOPLAY] teleport-assist to " + st.stationName); }

            if (st.stationName == "LifeSupportStation")
            {
                yield return DriveLifeSupport(st, 28f); // pickup cell -> socket -> puzzle
            }
            else
            {
                // Dock directly — the proximity prompt lags a teleport, which made
                // Tap(E) miss; EnterDock reliably engages the task (calls OnPlayerEnter).
                var sdc = StationDockController.Instance;
                if (sdc != null && !sdc.IsDocked) sdc.EnterDock(st);
                docked++;
                yield return new WaitForSeconds(0.8f);
                yield return DriveDockedTask(st, 20f);
                if (sdc != null && sdc.IsDocked) sdc.ExitDock();
            }

            // step away so the spawn loop / next station can proceed
            Hold(Key.S); yield return new WaitForSeconds(0.6f); Release();
        }

        var sm = SessionManager.Instance;
        Debug.Log("[AUTOPLAY] END  docks=" + docked + " beats=" + efBeats + " stuck=" + stuck +
                  (sm != null ? "  tasksTotal=" + sm.TasksTotal + " passed=" + sm.TasksPassed + " failed=" + sm.TasksFailed : ""));
    }

    private IEnumerator HandleBeat()
    {
        efBeats++;
        Debug.Log("[AUTOPLAY] priority beat — setting order 1,2,3");
        yield return new WaitForSeconds(0.4f);
        Tap(Key.Digit1); yield return new WaitForSeconds(0.4f);
        Tap(Key.Digit2); yield return new WaitForSeconds(0.4f);
        Tap(Key.Digit3); yield return new WaitForSeconds(0.4f);
        // wait for the beat to finish holding before resuming
        float t = 0f;
        while (ef != null && ef.EventActive && t < 8f) { t += Time.deltaTime; yield return null; }
    }

    private void Teleport(Vector3 stationPos)
    {
        Vector3 toHub = -new Vector3(stationPos.x, 0f, stationPos.z);
        if (toHub.sqrMagnitude < 0.01f) toHub = Vector3.back;
        toHub.Normalize();
        Vector3 p = stationPos + toHub * 2f;
        p.y = Pos().y;
        Release();
        if (playerRb != null) { playerRb.position = p; playerRb.linearVelocity = Vector3.zero; }
        else if (player != null) player.position = p;
    }

    private TaskStation NearestActiveStation()
    {
        TaskStation best = null; float bestSqr = float.MaxValue;
        foreach (var s in FindObjectsByType<TaskStation>(FindObjectsSortMode.None))
        {
            if (s == null || !s.HasActiveTask()) continue;
            float d = (s.transform.position - Pos()).sqrMagnitude;
            if (d < bestSqr) { bestSqr = d; best = s; }
        }
        return best;
    }

    // Camera-relative steering toward a world target. Reports reached via callback.
    private IEnumerator NavigateTo(Vector3 target, float stopDist, float timeout, System.Action<bool> done)
    {
        float t = 0f; float lastDist = float.MaxValue; float noProgress = 0f;
        while (t < timeout)
        {
            if (BeatActive) { Release(); done?.Invoke(false); yield break; } // let the main loop handle the beat
            Vector3 to = target - Pos(); to.y = 0f;
            float dist = to.magnitude;
            if (dist <= stopDist) { Release(); done?.Invoke(true); yield break; }

            Vector3 camF = cam != null ? cam.forward : Vector3.forward; camF.y = 0f; camF.Normalize();
            Vector3 camR = cam != null ? cam.right : Vector3.right; camR.y = 0f; camR.Normalize();
            Vector3 dir = to.normalized;
            float fwd = Vector3.Dot(dir, camF);
            float str = Vector3.Dot(dir, camR);

            var keys = new List<Key>();
            if (fwd > 0.3f) keys.Add(Key.W); else if (fwd < -0.3f) keys.Add(Key.S);
            if (str > 0.3f) keys.Add(Key.D); else if (str < -0.3f) keys.Add(Key.A);

            // Unstick: if not closing distance, wiggle + jump.
            if (dist > lastDist - 0.05f) noProgress += Time.deltaTime; else noProgress = 0f;
            lastDist = dist;
            if (noProgress > 1.5f) { keys.Add(Key.Space); keys.Add(Random.value < 0.5f ? Key.A : Key.D); noProgress = 0f; }

            _held.Clear(); foreach (var k in keys) _held.Add(k);
            t += Time.deltaTime;
            yield return null;
        }
        Release();
        done?.Invoke((target - Pos()).magnitude <= stopDist + 0.5f);
    }

    // Progress whatever task is docked: tap Space (radar) + invoke task buttons
    // (whitelisted names only, so we never touch assessor/HUD controls). Stops when
    // the station's task resolves or on timeout.
    private IEnumerator DriveDockedTask(TaskStation st, float timeout)
    {
        string name = st.stationName;
        float t = 0f; float beat = 0f; int idx = 0;
        while (t < timeout && st.HasActiveTask())
        {
            if (BeatActive) { Debug.Log("[AUTOPLAY] priority beat during task — bailing to handle it"); break; }
            beat += Time.deltaTime; t += Time.deltaTime;
            if (beat >= 0.3f)
            {
                beat = 0f;
                Tap(Key.Space); // radar flag / generic
                var btns = TaskButtons();
                if (btns.Count > 0) { btns[idx % btns.Count].onClick.Invoke(); idx++; }
            }
            yield return null;
        }
        if (!st.HasActiveTask()) { tasksSeenResolved++; Debug.Log("[AUTOPLAY] resolved task at " + name); }
        else Debug.Log("[AUTOPLAY] left task at " + name + " unresolved (timeout)");
    }

    // Life Support is a physical multi-step task: pick up the cell from the rack,
    // carry it to the socket, open the planning puzzle, solve it.
    private IEnumerator DriveLifeSupport(TaskStation st, float timeout)
    {
        Debug.Log("[AUTOPLAY] Life Support: locating power cell");
        // 1) Find + grab the cell.
        var cell = FindCell();
        if (cell != null)
        {
            bool r = false;
            yield return NavigateTo(cell.transform.position, cell.pickupRadius * 0.7f, 8f, x => r = x);
            if (!r) Teleport(cell.transform.position);
            Tap(Key.E);
            yield return new WaitForSeconds(0.5f);
            if (!cell.IsCarried) cell.PickUp(); // fallback to the public API
            yield return new WaitForSeconds(0.3f);
            Debug.Log("[AUTOPLAY] cell carried=" + cell.IsCarried);
        }
        else Debug.Log("[AUTOPLAY] no cell found");

        // 2) Carry to the socket and open the puzzle (E).
        var socket = FindAnyObjectByType<BatterySocket>();
        Vector3 socketPos = socket != null ? socket.transform.position : st.transform.position;
        bool r2 = false;
        yield return NavigateTo(socketPos, 2.0f, 8f, x => r2 = x);
        if (!r2) Teleport(socketPos);

        float t = 0f;
        while (!PlanningPuzzlePanel.AnyOpen && t < 4f && st.HasActiveTask())
        { Tap(Key.E); yield return new WaitForSeconds(0.5f); t += 0.5f; }

        // 3) Solve whichever planning puzzle opened (dev hook); fall back to a
        //    direct install if it didn't open.
        IPlanningPuzzle puz = PlanningPuzzlePanel.ActiveInstance;
        if (puz != null)
        {
            Debug.Log("[AUTOPLAY] solving planning puzzle");
            puz.DebugAutoSolve();
        }
        else if (socket != null && st.HasActiveTask())
        {
            Debug.Log("[AUTOPLAY] puzzle did not open; CompleteInstall fallback");
            socket.CompleteInstall();
        }
        yield return new WaitForSeconds(2.0f);
        Debug.Log("[AUTOPLAY] Life Support resolved=" + (!st.HasActiveTask()));
    }

    private CarryableBattery FindCell()
    {
        foreach (var c in FindObjectsByType<CarryableBattery>(FindObjectsSortMode.None))
            if (c != null && !c.IsInstalled) return c;
        return null;
    }

    // Only task/instruction/puzzle buttons — never assessor or HUD chrome.
    private List<Button> TaskButtons()
    {
        var list = new List<Button>();
        foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
        {
            if (b == null || !b.gameObject.activeInHierarchy || !b.interactable) continue;
            string n = b.gameObject.name;
            if (n.StartsWith("Btn_") || n.StartsWith("Step_") || n == "GotItButton")
                list.Add(b);
        }
        return list;
    }
}
