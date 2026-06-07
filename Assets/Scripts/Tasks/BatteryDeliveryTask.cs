using System.Collections;
using UnityEngine;

/// <summary>
/// Life-Support mission: the player retrieves a power cell from the central
/// storage rack and installs it into the Life Support console socket.
///
/// Flow:
///   Activate    -> spawn a power cell at the storage rack, show the objective
///                  HUD, play the request voice; the soft power timer drains.
///   Pick up (E) -> objective updates to "carry to Life Support".
///   Install (E) -> Success.
///   Timer ends  -> Fail.
///
/// Physical task: there is no docked canvas — the player walks, carries, installs.
/// Resolving still fires MissionTask.OnTaskResolved so SessionManager logs it.
/// </summary>
public class BatteryDeliveryTask : MissionTask
{
    private const float PowerDrainSeconds = 100f;

    private GameObject battery;
    private CarryableBattery carryable;
    private BatterySocket socket;
    private BatteryMissionHUD hud;
    private bool pickedUp;
    private bool finished;
    private bool lowWarningPlayed;

    private void Awake()
    {
        TaskName = "Power Cell";
        priority = TaskPriority.Critical;
        timeLimit = PowerDrainSeconds;
    }

    public override void Activate()
    {
        base.Activate();

        socket = FindAnyObjectByType<BatterySocket>();
        var rack = FindAnyObjectByType<BatteryStorageRack>();

        Vector3 spawnPos = rack != null ? rack.SpawnPosition : new Vector3(0f, 1f, 5f);
        Quaternion spawnRot = rack != null ? rack.SpawnRotation : Quaternion.identity;

        GameObject prefab = Resources.Load<GameObject>("BatteryCell");
        if (prefab != null)
        {
            battery = Instantiate(prefab, spawnPos, spawnRot);
            battery.name = "PowerCell_Active";
            carryable = battery.GetComponent<CarryableBattery>();
            if (carryable == null) carryable = battery.AddComponent<CarryableBattery>();
            carryable.OnPickedUp += HandlePickedUp;
        }
        else
        {
            Debug.LogWarning("[BatteryDeliveryTask] BatteryCell prefab missing from Resources.");
        }

        if (socket != null)
        {
            socket.Arm(carryable);
            socket.OnBatteryInstalled += HandleInstalled;
        }
        else
        {
            Debug.LogWarning("[BatteryDeliveryTask] No BatterySocket found in the scene.");
        }

        var hudGO = new GameObject("BatteryMissionHUD");
        hud = hudGO.AddComponent<BatteryMissionHUD>();
        hud.SetObjective("LIFE SUPPORT FAILING - TAKE A POWER CELL FROM STORAGE");
        hud.SetPower(1f);

        StationUI?.SetInstruction("LIFE SUPPORT: awaiting power cell");

        AudioManager.Instance.PlaySfx("battery_alarm");
        AudioManager.Instance.PlayVoice("battery_request");

        SessionManager.Instance?.LogCustomEvent("Battery_Spawned", StationName,
            "rack=" + (rack != null));
    }

    // Physical task — no docked canvas, so these are intentionally inert.
    public override void OnPlayerEnter() { }
    public override void OnPlayerExit() { }

    protected override void Update()
    {
        base.Update(); // base handles timeLimit -> HandleExpiry
        if (!IsActive || finished) return;

        float frac = Mathf.Clamp01(1f - (Time.time - SpawnTime) / PowerDrainSeconds);
        if (hud != null) hud.SetPower(frac);

        if (!lowWarningPlayed && frac <= 0.25f)
        {
            lowWarningPlayed = true;
            AudioManager.Instance.PlaySfx("battery_alarm");
        }
    }

    private void HandlePickedUp(CarryableBattery b)
    {
        if (pickedUp) return;
        pickedUp = true;
        if (hud != null)
            hud.SetObjective("CARRY THE CELL TO LIFE SUPPORT  -  [E] AT THE CONSOLE TO INSTALL");
        SessionManager.Instance?.LogCustomEvent("Battery_PickedUp", StationName,
            "t=" + (Time.time - SpawnTime).ToString("F2"));
    }

    private void HandleInstalled(CarryableBattery b)
    {
        if (finished) return;
        finished = true;
        StartCoroutine(CoSucceed());
    }

    private IEnumerator CoSucceed()
    {
        if (hud != null)
        {
            hud.SetPower(1f);
            hud.Flash("POWER CELL INSTALLED - LIFE SUPPORT STABLE", new Color(0.35f, 1f, 0.5f));
        }
        AudioManager.Instance.PlaySfx("power_restore");
        AudioManager.Instance.PlaySfx("success_chime");
        AudioManager.Instance.PlayVoice("battery_installed");
        StationUI?.SetInstruction("LIFE SUPPORT: nominal");
        yield return new WaitForSeconds(2.4f);
        Resolve(TaskResult.Success);
    }

    protected override void HandleExpiry()
    {
        if (finished) return;
        finished = true;
        StartCoroutine(CoFail());
    }

    private IEnumerator CoFail()
    {
        if (hud != null)
        {
            hud.SetPower(0f);
            hud.Flash("LIFE SUPPORT FAILURE - CELL NOT INSTALLED IN TIME", new Color(1f, 0.35f, 0.3f));
        }
        AudioManager.Instance.PlaySfx("fail_buzz");
        AudioManager.Instance.PlayVoice("battery_failed");
        yield return new WaitForSeconds(2.4f);
        Resolve(TaskResult.Fail);
    }

    private void OnDestroy()
    {
        if (carryable != null) carryable.OnPickedUp -= HandlePickedUp;
        if (socket != null)
        {
            socket.OnBatteryInstalled -= HandleInstalled;
            socket.Disarm();
        }
        if (hud != null) Destroy(hud.gameObject);
        // An installed cell is owned by the socket and cleared when the next
        // mission arms it; only destroy a cell that never reached the socket.
        if (battery != null && (carryable == null || !carryable.IsInstalled))
            Destroy(battery);
    }
}
