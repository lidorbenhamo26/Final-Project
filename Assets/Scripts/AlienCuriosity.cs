using UnityEngine;

/// <summary>
/// Playful pestering behavior for the alien. Sits alongside WanderingAI on the
/// same GameObject — when the player walks into its field of view it disables
/// the wanderer, pivots to face the player, then orbits at a close distance
/// while chirping. After a short pestering window it re-enables wandering and
/// goes on cooldown so the alien doesn't immediately re-trigger.
/// </summary>
[DisallowMultipleComponent]
public class AlienCuriosity : MonoBehaviour
{
    private enum State { Wander, Notice, Pester, Cooldown }

    [Header("Detection")]
    [Tooltip("Distance within which the alien can spot the player.")]
    public float detectionRadius = 5f;
    [Tooltip("Half-angle (degrees) of the alien's vision cone. 75 => 150-deg field of view.")]
    public float fovHalfAngle = 75f;
    [Tooltip("Layers that block line-of-sight. Walls, doors, etc.")]
    public LayerMask losBlockers = ~0;

    [Header("Movement")]
    [Tooltip("Distance the alien tries to maintain while pestering.")]
    public float pesterDistance = 1.2f;
    [Tooltip("Once the player leaves this radius, the alien gives up.")]
    public float disengageRadius = 7f;
    [Tooltip("Speed while approaching / orbiting (m/s).")]
    public float approachSpeed = 1.1f;
    [Tooltip("How fast the alien rotates toward its target direction.")]
    public float turnSpeed = 7f;
    [Tooltip("If true, the alien drifts sideways while close — playful circling.")]
    public bool orbitWhileClose = true;
    [Tooltip("Lateral drift speed during orbit (m/s).")]
    public float orbitSpeed = 0.6f;

    [Header("Timing")]
    [Tooltip("Random pester duration range (seconds).")]
    public Vector2 pesterDuration = new Vector2(4f, 6f);
    [Tooltip("Random cooldown range before alien can notice player again (seconds).")]
    public Vector2 cooldownDuration = new Vector2(8f, 12f);
    [Tooltip("Random interval between soft chirps during pester (seconds).")]
    public Vector2 chirpInterval = new Vector2(2.5f, 4.0f);
    [Tooltip("Volume of the quiet pester chirps (0..1).")]
    [Range(0f, 1f)] public float pesterChirpVolume = 0.35f;
    [Tooltip("Volume of the louder one-shot notice chirp (0..1).")]
    [Range(0f, 1f)] public float noticeChirpVolume = 0.6f;

    [Header("Animator")]
    [Tooltip("Optional override. If null, GetComponentInChildren<Animator>() is used.")]
    public Animator animator;
    public string speedParam = "Speed";
    public string isWalkingParam = "IsWalking";

    [Header("Audio (Resources/Audio/SFX/sfx_<id>)")]
    public string noticeChirpId = "alien_curious_chirp_a";
    public string softChirpIdB = "alien_curious_chirp_b";
    public string softChirpIdC = "alien_curious_chirp_c";

    [Header("UI")]
    public string firstSightNotification = "The alien is curious about you...";

    private static bool _firstSightNotified;

    private State _state = State.Wander;
    private Transform _player;
    private WanderingAI _wanderer;
    private float _stateTimer;
    private float _chirpTimer;
    private float _orbitDir = 1f;
    private bool _hasSpeedParam;
    private bool _hasWalkingParam;

    private void Awake()
    {
        _wanderer = GetComponent<WanderingAI>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == speedParam && p.type == AnimatorControllerParameterType.Float) _hasSpeedParam = true;
                if (p.name == isWalkingParam && p.type == AnimatorControllerParameterType.Bool) _hasWalkingParam = true;
            }
        }
    }

    private void Start()
    {
        ResolvePlayer();
    }

    private void ResolvePlayer()
    {
        var tagged = GameObject.FindWithTag("Player");
        if (tagged != null) { _player = tagged.transform; return; }
        var ctrl = FindAnyObjectByType<AstronautController>();
        if (ctrl != null) _player = ctrl.transform;
    }

    private void Update()
    {
        if (_player == null)
        {
            ResolvePlayer();
            if (_player == null) return;
        }

        switch (_state)
        {
            case State.Wander:   TickWander();   break;
            case State.Notice:   TickNotice();   break;
            case State.Pester:   TickPester();   break;
            case State.Cooldown: TickCooldown(); break;
        }
    }

    private void TickWander()
    {
        if (!CanSeePlayer(out float dist)) return;
        EnterNotice(dist);
    }

    private void TickNotice()
    {
        FacePlayer();
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) EnterPester();
    }

    private void TickPester()
    {
        float dist = Vector3.Distance(transform.position, _player.position);
        if (dist > disengageRadius) { EnterCooldown(); return; }

        FacePlayer();

        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        Vector3 dir = toPlayer.normalized;

        Vector3 move = Vector3.zero;
        if (dist > pesterDistance + 0.1f)
        {
            move += dir * approachSpeed;
        }
        else if (dist < pesterDistance - 0.1f)
        {
            move -= dir * approachSpeed * 0.6f;
        }

        if (orbitWhileClose && dist < pesterDistance + 0.5f)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir) * _orbitDir;
            move += side * orbitSpeed;
        }

        transform.position += move * Time.deltaTime;
        SetAnimatorSpeed(move.magnitude);

        _chirpTimer -= Time.deltaTime;
        if (_chirpTimer <= 0f)
        {
            string id = Random.value < 0.5f ? softChirpIdB : softChirpIdC;
            if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(id, pesterChirpVolume);
            _chirpTimer = Random.Range(chirpInterval.x, chirpInterval.y);
        }

        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f) EnterCooldown();
    }

    private void TickCooldown()
    {
        SetAnimatorSpeed(0f);
        _stateTimer -= Time.deltaTime;
        if (_stateTimer <= 0f)
        {
            _state = State.Wander;
            if (_wanderer != null) _wanderer.enabled = true;
        }
    }

    private void EnterNotice(float distance)
    {
        _state = State.Notice;
        if (_wanderer != null) _wanderer.enabled = false;
        _stateTimer = 0.6f;
        _orbitDir = Random.value < 0.5f ? -1f : 1f;
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySfx(noticeChirpId, noticeChirpVolume);
        if (!_firstSightNotified)
        {
            _firstSightNotified = true;
            var feed = FindAnyObjectByType<NotificationFeed>();
            if (feed != null) feed.Push(firstSightNotification);
        }
    }

    private void EnterPester()
    {
        _state = State.Pester;
        _stateTimer = Random.Range(pesterDuration.x, pesterDuration.y);
        _chirpTimer = Random.Range(chirpInterval.x, chirpInterval.y);
    }

    private void EnterCooldown()
    {
        _state = State.Cooldown;
        _stateTimer = Random.Range(cooldownDuration.x, cooldownDuration.y);
        SetAnimatorSpeed(0f);
        if (_wanderer != null) _wanderer.enabled = true;
    }

    private bool CanSeePlayer(out float distance)
    {
        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        distance = toPlayer.magnitude;
        if (distance > detectionRadius || distance < 0.01f) return false;

        Vector3 fwd = transform.forward; fwd.y = 0f;
        float angle = Vector3.Angle(fwd, toPlayer);
        if (angle > fovHalfAngle) return false;

        Vector3 origin = transform.position + Vector3.up * 1.2f;
        Vector3 target = _player.position + Vector3.up * 1.2f;
        Vector3 ray = target - origin;
        if (Physics.Raycast(origin, ray.normalized, out RaycastHit hit, ray.magnitude, losBlockers, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform != _player && !hit.transform.IsChildOf(_player)) return false;
        }
        return true;
    }

    private void FacePlayer()
    {
        Vector3 toPlayer = _player.position - transform.position;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude < 0.0001f) return;
        Quaternion want = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, turnSpeed * Time.deltaTime);
    }

    private void SetAnimatorSpeed(float s)
    {
        if (animator == null) return;
        if (_hasSpeedParam) animator.SetFloat(speedParam, s);
        else if (_hasWalkingParam) animator.SetBool(isWalkingParam, s > 0.05f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        Gizmos.color = new Color(1f, 0.3f, 0.1f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, pesterDistance);
        Vector3 fwd = transform.forward;
        Quaternion left = Quaternion.AngleAxis(-fovHalfAngle, Vector3.up);
        Quaternion right = Quaternion.AngleAxis(fovHalfAngle, Vector3.up);
        Gizmos.color = new Color(1f, 0.8f, 0.2f, 0.6f);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.2f, left * fwd * detectionRadius);
        Gizmos.DrawRay(transform.position + Vector3.up * 1.2f, right * fwd * detectionRadius);
    }
}
