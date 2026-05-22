using TMPro;
using UnityEngine;

/// <summary>
/// A power cell the player picks up by pressing Interact (E) while in range,
/// then carries. While carried it tracks the astronaut's hand bone every
/// LateUpdate (after the Animator poses the skeleton) so it genuinely sits in
/// the hand. While in storage it gently hovers and spins, with a floating
/// "[E] PICK UP" hint that fades in when the player is close.
/// </summary>
[DisallowMultipleComponent]
public class CarryableBattery : MonoBehaviour
{
    [Header("Pickup")]
    public float pickupRadius = 2.2f;

    [Header("Carry — held in hand")]
    [Tooltip("Fine nudge of the cell away from the grip bone, in the player's local axes (right, up, forward).")]
    public Vector3 carryGripOffset = Vector3.zero;
    [Tooltip("Uniform scale applied to the cell while carried, so it fits the astronaut's small hand.")]
    [Range(0.2f, 1f)] public float carriedScale = 0.45f;

    [Header("Idle motion while in storage")]
    public float hoverAmplitude = 0.06f;
    public float hoverFrequency = 0.75f;
    public float spinSpeed = 28f;

    [Header("Knock-down")]
    [Tooltip("Random tumble spin (rad/s) imparted when the alien knocks the cell loose.")]
    public float knockTumbleSpin = 5f;

    public bool IsCarried { get; private set; }
    public bool IsInstalled { get; private set; }
    public bool IsDropped { get; private set; }

    public event System.Action<CarryableBattery> OnPickedUp;

    private Transform player;
    private Transform handBone;
    private Transform gripBone;
    private AstronautHandGrip handGrip;
    private Vector3 restPosition;
    private Vector3 baseScale = Vector3.one;
    private float bobSeed;
    private float nextPlayerLookup;

    private Canvas hintCanvas;
    private TMP_Text hintLabel;
    private float hintAlpha;

    private Rigidbody body;
    private BoxCollider dropCollider;

    private void Awake()
    {
        baseScale = transform.localScale;
        BuildHint();
        SetHintAlpha(0f);
    }

    private void Start()
    {
        // Captured in Start so the spawning task has already placed the cell.
        restPosition = transform.position;
        bobSeed = Random.value * 10f;
        EnsurePhysics();
    }

    private void Update()
    {
        if (IsInstalled || IsCarried) return;

        if (!IsDropped)
        {
            // Idle hover + spin while sitting in storage.
            float t = Time.time * hoverFrequency * Mathf.PI * 2f + bobSeed;
            transform.position = restPosition + Vector3.up * Mathf.Sin(t) * hoverAmplitude;
            transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
        }

        // Pickup hint + Interact — active in storage and once knocked to the floor.
        if (player == null && Time.unscaledTime >= nextPlayerLookup)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
            nextPlayerLookup = Time.unscaledTime + 1f;
        }

        bool inRange = player != null &&
            (player.position - transform.position).sqrMagnitude <= pickupRadius * pickupRadius;

        hintAlpha = Mathf.MoveTowards(hintAlpha, inRange ? 1f : 0f, 6f * Time.deltaTime);
        SetHintAlpha(hintAlpha);
        BillboardHint();

        if (inRange && InteractInputBinding.InteractPressedThisFrame())
            PickUp();
    }

    // Runs after the Animator has posed the skeleton this frame, so the cell
    // tracks the live hand position. Kept upright (yaw follows the player).
    private void LateUpdate()
    {
        if (!IsCarried || player == null) return;

        // Anchor on the knuckle bone — that point sits inside the closed grip.
        Vector3 grip = gripBone != null
            ? gripBone.position
            : (handBone != null ? handBone.position
                                : player.position + Vector3.up * 1.0f + player.right * 0.25f);
        grip += player.right * carryGripOffset.x
              + Vector3.up * carryGripOffset.y
              + player.forward * carryGripOffset.z;

        transform.rotation = Quaternion.Euler(0f, player.eulerAngles.y, 0f);

        // Shift the pivot so the cell's visible centre lands on the grip point.
        Vector3 centreLocal = dropCollider != null ? dropCollider.center : new Vector3(0f, 0.21f, 0f);
        transform.position = grip - transform.rotation * Vector3.Scale(centreLocal, transform.lossyScale);
    }

    public void PickUp()
    {
        if (IsCarried || IsInstalled) return;
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
        if (player == null) return;

        IsCarried = true;
        IsDropped = false;
        handBone = FindHandBone(player);
        gripBone = FindBoneByName(player, "RightFingers1");
        if (gripBone == null) gripBone = handBone;
        handGrip = player.GetComponentInChildren<AstronautHandGrip>();
        if (handGrip != null) handGrip.SetGripping(true);

        // Carried cells are kinematic + non-colliding; physics only runs while dropped.
        if (body != null)
        {
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
        }
        if (dropCollider != null) dropCollider.enabled = false;

        transform.SetParent(player, true);
        transform.localScale = baseScale * carriedScale;
        SetHintAlpha(0f);
        if (hintCanvas != null) hintCanvas.gameObject.SetActive(false);

        AudioManager.Instance.PlaySfx("battery_pickup");
        OnPickedUp?.Invoke(this);
    }

    /// <summary>Detach from the player and snap into a station socket slot.</summary>
    public void InstallInto(Transform slot)
    {
        IsCarried = false;
        IsInstalled = true;
        if (handGrip != null) handGrip.SetGripping(false);
        if (hintCanvas != null) hintCanvas.gameObject.SetActive(false);
        if (slot != null)
        {
            transform.SetParent(slot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }
        transform.localScale = baseScale;
    }

    /// <summary>
    /// Knocked out of the player's hands by the alien — detaches into world
    /// space and falls to the floor under physics. The pickup hint comes back
    /// so the cell can be recovered from the floor and still installed.
    /// </summary>
    public void Drop(Vector3 impulse)
    {
        if (!IsCarried) return;

        EnsurePhysics();
        IsCarried = false;
        IsDropped = true;
        handBone = null;
        gripBone = null;
        if (handGrip != null) handGrip.SetGripping(false);
        transform.SetParent(null, true);
        transform.localScale = baseScale;

        dropCollider.enabled = true;
        body.isKinematic = false;
        body.useGravity = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.AddForce(impulse, ForceMode.Impulse);
        body.angularVelocity = Random.onUnitSphere * knockTumbleSpin;

        hintAlpha = 0f;
        SetHintAlpha(0f);
        if (hintCanvas != null) hintCanvas.gameObject.SetActive(true);
    }

    // Adds (once) the Rigidbody + BoxCollider used for the knocked-loose fall.
    // Both stay kinematic / disabled until the cell is actually dropped.
    private void EnsurePhysics()
    {
        if (body == null)
        {
            body = GetComponent<Rigidbody>();
            if (body == null) body = gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.linearDamping = 0.4f;
            body.angularDamping = 3f;
        }
        if (dropCollider == null)
        {
            dropCollider = GetComponent<BoxCollider>();
            if (dropCollider == null) dropCollider = gameObject.AddComponent<BoxCollider>();
            FitColliderToModel();
            dropCollider.enabled = false;
        }
    }

    // Sizes the box collider to the visible cell mesh (the unrotated Model child).
    private void FitColliderToModel()
    {
        var mf = GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Transform mt = mf.transform;
            Bounds mb = mf.sharedMesh.bounds;
            dropCollider.center = mt.localPosition + Vector3.Scale(mb.center, mt.localScale);
            dropCollider.size = Vector3.Scale(mb.size, mt.localScale);
        }
        else
        {
            dropCollider.center = new Vector3(0f, 0.2f, 0f);
            dropCollider.size = new Vector3(0.3f, 0.4f, 0.3f);
        }
    }

    /// <summary>Finds the astronaut's right hand bone (generic rig, searched by name).</summary>
    private static Transform FindHandBone(Transform root)
    {
        Transform anyHand = null;
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
        {
            string n = tr.name.ToLowerInvariant();
            if (!n.Contains("hand")) continue;
            if (n.Contains("index") || n.Contains("thumb") || n.Contains("middle")
                || n.Contains("ring") || n.Contains("pinky") || n.Contains("pinkie")) continue;
            if (anyHand == null) anyHand = tr;
            if (n.Contains("right") || n.Contains("hand.r") || n.Contains("hand_r")
                || n.Contains("r_hand") || n.Contains("rhand"))
                return tr;
        }
        return anyHand;
    }

    /// <summary>Finds a descendant transform by exact name (case-insensitive).</summary>
    private static Transform FindBoneByName(Transform root, string boneName)
    {
        foreach (var tr in root.GetComponentsInChildren<Transform>(true))
            if (string.Equals(tr.name, boneName, System.StringComparison.OrdinalIgnoreCase))
                return tr;
        return null;
    }

    private void BuildHint()
    {
        var canvasGO = new GameObject("BatteryHintCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvasGO.transform.localPosition = Vector3.up * 0.55f;

        hintCanvas = canvasGO.AddComponent<Canvas>();
        hintCanvas.renderMode = RenderMode.WorldSpace;
        var rt = hintCanvas.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(2.6f, 0.6f);
        canvasGO.transform.localScale = Vector3.one * 0.01f;

        var labelGO = new GameObject("HintLabel");
        labelGO.transform.SetParent(canvasGO.transform, false);
        var lrt = labelGO.AddComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = Vector2.zero;
        lrt.offsetMax = Vector2.zero;

        hintLabel = labelGO.AddComponent<TextMeshProUGUI>();
        hintLabel.text = "[E] PICK UP";
        hintLabel.alignment = TextAlignmentOptions.Center;
        hintLabel.fontSize = 34f;
        hintLabel.fontStyle = FontStyles.Bold;
        hintLabel.color = new Color(0.55f, 0.95f, 1f, 0f);
        hintLabel.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void SetHintAlpha(float a)
    {
        if (hintLabel == null) return;
        var c = hintLabel.color;
        c.a = a;
        hintLabel.color = c;
    }

    private void BillboardHint()
    {
        if (hintCanvas == null || hintAlpha <= 0.01f) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 fwd = hintCanvas.transform.position - cam.transform.position;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
            hintCanvas.transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }
}
