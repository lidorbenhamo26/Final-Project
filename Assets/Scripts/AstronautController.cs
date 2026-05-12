using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(PlayerInput))]
public class AstronautController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float sprintSpeed = 6f;
    [SerializeField] private float rotationSpeed = 12f;
    [SerializeField, Tooltip("How quickly horizontal velocity reaches the target speed (1/seconds)")]
    private float accelLerp = 14f;
    [SerializeField, Tooltip("Air control multiplier (0 = no air steering, 1 = full)")]
    private float airControl = 0.35f;
    [SerializeField, Tooltip("Max horizontal speed the astronaut can have while airborne. Stops sprint-jumps from carrying full Run speed forward.")]
    private float airSpeedMax = 2.5f;

    [Header("Lock")]
    [Tooltip("When false, movement input is ignored (used by station dock).")]
    public bool ControlsEnabled = true;

    [Header("Jump")]
    [SerializeField] private float jumpForce = 6.5f;
    [SerializeField, Tooltip("Time after pressing jump that the input stays valid (seconds)")]
    private float jumpBuffer = 0.15f;
    [SerializeField, Tooltip("Time after leaving the ground that you can still jump (coyote time, seconds)")]
    private float coyoteTime = 0.1f;

    [Header("Ground Check")]
    [SerializeField] private LayerMask groundLayer = ~0;
    [SerializeField] private float groundCheckRadius = 0.25f;
    [SerializeField] private Vector3 groundCheckOffset = new Vector3(0f, 0.05f, 0f);

    [Header("Animation")]
    [SerializeField, Tooltip("Damping time for the Speed animator parameter (seconds). Smooths Idle->Walk->Run blending so sprint->walk feels natural instead of popping.")]
    private float speedDamp = 0.2f;

    [Header("Camera (auto-resolved if empty)")]
    [SerializeField] private Transform cameraTransform;

    private Rigidbody rb;
    private Animator animator;
    private PlayerInput playerInput;

    // Polled actions — more reliable than SendMessages OnSprint, which sometimes
    // misses the button release for "Button" actions and leaves sprint stuck on.
    private InputAction moveAction;
    private InputAction sprintAction;
    private InputAction jumpAction;

    private Vector2 moveInput;
    private bool sprintHeld;
    private float jumpBufferTimer;
    private float coyoteTimer;
    private bool grounded;
    private float footstepTimer;
    private const float FootstepWalkInterval = 0.5f;
    private const float FootstepRunInterval = 0.3f;
    private const float FootstepSpeedThreshold = 0.5f;
    private const float FootstepVolume = 0.7f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int JumpHash = Animator.StringToHash("Jump");
    private static readonly int WaveHash = Animator.StringToHash("Wave");
    private static readonly int AlertHash = Animator.StringToHash("Alert");

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        animator = GetComponent<Animator>();
        playerInput = GetComponent<PlayerInput>();
        if (rb != null)
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
        ResolveActions();
    }

    private void OnEnable() { ResolveActions(); }

    private void ResolveActions()
    {
        if (playerInput == null || playerInput.actions == null) return;
        moveAction   = playerInput.actions.FindAction("Move",   throwIfNotFound: false);
        sprintAction = playerInput.actions.FindAction("Sprint", throwIfNotFound: false);
        jumpAction   = playerInput.actions.FindAction("Jump",   throwIfNotFound: false);
    }

    private void Update()
    {
        // Poll inputs every frame — this is the source of truth.
        if (moveAction   != null) moveInput  = moveAction.ReadValue<Vector2>();
        if (sprintAction != null) sprintHeld = sprintAction.IsPressed();
        if (jumpAction != null && jumpAction.WasPressedThisFrame()) jumpBufferTimer = jumpBuffer;

        if (!ControlsEnabled) { moveInput = Vector2.zero; sprintHeld = false; jumpBufferTimer = 0f; }

        if (jumpBufferTimer > 0f) jumpBufferTimer -= Time.deltaTime;
        if (coyoteTimer    > 0f) coyoteTimer    -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        bool wasGrounded = grounded;
        grounded = Physics.CheckSphere(transform.position + groundCheckOffset, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
        if (grounded) coyoteTimer = coyoteTime;
        else if (wasGrounded) coyoteTimer = coyoteTime; // just left ground
        if (grounded && !wasGrounded) AudioManager.Instance.PlaySfx("land");

        Vector3 camForward = cameraTransform != null ? cameraTransform.forward : Vector3.forward;
        Vector3 camRight   = cameraTransform != null ? cameraTransform.right   : Vector3.right;
        camForward.y = 0f; camRight.y = 0f;
        camForward.Normalize(); camRight.Normalize();

        Vector3 wishDir = camForward * moveInput.y + camRight * moveInput.x;
        float wishMag = Mathf.Clamp01(wishDir.magnitude);
        if (wishMag > 0.001f) wishDir /= wishMag;

        // In air, cap the target speed so a sprint-jump doesn't carry full Run velocity forward
        float baseSpeed = sprintHeld ? sprintSpeed : moveSpeed;
        if (!grounded) baseSpeed = Mathf.Min(baseSpeed, airSpeedMax);
        float targetSpeed = baseSpeed * wishMag;
        Vector3 desiredHoriz = wishDir * targetSpeed;

        Vector3 v = rb.linearVelocity;
        // In air, blend the player's wishes more slowly into existing velocity
        float lerpRate = (grounded ? accelLerp : accelLerp * airControl) * Time.fixedDeltaTime;
        Vector2 currentHoriz = new Vector2(v.x, v.z);
        Vector2 targetHoriz  = new Vector2(desiredHoriz.x, desiredHoriz.z);
        Vector2 newHoriz = Vector2.Lerp(currentHoriz, targetHoriz, Mathf.Clamp01(lerpRate));
        v.x = newHoriz.x;
        v.z = newHoriz.y;

        // Jump: consume buffered jump if grounded (or within coyote window)
        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            v.y = jumpForce;
            // Clamp horizontal velocity AT the moment of jump so a sprint-jump
            // doesn't carry full Run velocity forward through the entire arc.
            Vector2 horiz = new Vector2(v.x, v.z);
            if (horiz.magnitude > airSpeedMax)
            {
                horiz = horiz.normalized * airSpeedMax;
                v.x = horiz.x;
                v.z = horiz.y;
            }
            animator.SetTrigger(JumpHash);
            AudioManager.Instance.PlaySfx("jump", 0.85f);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
        }

        rb.linearVelocity = v;

        if (wishMag > 0.05f && grounded)
        {
            Quaternion targetRot = Quaternion.LookRotation(wishDir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
        }

        // Damped Speed parameter so Idle/Walk/Run blends look smooth (no popping)
        float horizSpeed = new Vector2(v.x, v.z).magnitude;
        animator.SetFloat(SpeedHash, horizSpeed, speedDamp, Time.fixedDeltaTime);
        animator.SetBool(GroundedHash, grounded);

        if (grounded && horizSpeed > FootstepSpeedThreshold)
        {
            footstepTimer -= Time.fixedDeltaTime;
            if (footstepTimer <= 0f)
            {
                AudioManager.Instance.PlaySfx("footstep_metal", FootstepVolume);
                footstepTimer = sprintHeld ? FootstepRunInterval : FootstepWalkInterval;
            }
        }
        else
        {
            footstepTimer = 0f;
        }
    }

    public void TriggerWave()  => animator.SetTrigger(WaveHash);
    public void TriggerAlert() => animator.SetTrigger(AlertHash);

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = grounded ? Color.green : Color.red;
        Gizmos.DrawWireSphere(transform.position + groundCheckOffset, groundCheckRadius);
    }
}
