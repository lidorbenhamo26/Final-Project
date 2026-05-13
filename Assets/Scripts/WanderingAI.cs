using UnityEngine;

/// <summary>
/// Simple wandering AI for non-player characters (alien, pet) that walks around
/// the central hub picking random nearby points. Uses an Animator if present —
/// expects either a "Speed" float parameter (will set to current speed) or a
/// boolean "IsWalking" parameter as a fallback. Falls back to no animation.
///
/// Movement is direct transform translation with smooth turning. No NavMesh
/// dependency — for a simple cognitive-load station scene this is enough and
/// avoids extra setup.
/// </summary>
[DisallowMultipleComponent]
public class WanderingAI : MonoBehaviour
{
    [Header("Wander Area")]
    [Tooltip("Center of the area the character will wander within.")]
    public Vector3 areaCenter = Vector3.zero;
    [Tooltip("Radius (meters) around areaCenter the character will roam.")]
    public float areaRadius = 5f;

    [Header("Movement")]
    public float walkSpeed = 0.9f;
    public float turnSpeed = 4f;
    [Tooltip("Time to wait at each point before picking a new one (seconds).")]
    public float idleTime = 1.5f;
    [Tooltip("How close to the target counts as 'arrived'.")]
    public float arriveThreshold = 0.25f;

    [Header("Animator")]
    [Tooltip("Optional. If null, GetComponentInChildren<Animator>() is used.")]
    public Animator animator;
    [Tooltip("Float parameter on the animator to set to current speed.")]
    public string speedParam = "Speed";
    [Tooltip("Bool parameter as a fallback if Speed is not present.")]
    public string isWalkingParam = "IsWalking";

    private Vector3 _target;
    private float _idleTimer;
    private bool _isIdle;
    private bool _hasSpeedParam;
    private bool _hasWalkingParam;

    private void Awake()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == speedParam     && p.type == AnimatorControllerParameterType.Float) _hasSpeedParam = true;
                if (p.name == isWalkingParam && p.type == AnimatorControllerParameterType.Bool)  _hasWalkingParam = true;
            }
        }
        PickNewTarget();
    }

    private void Update()
    {
        if (_isIdle)
        {
            _idleTimer -= Time.deltaTime;
            SetAnimatorSpeed(0f);
            if (_idleTimer <= 0f)
            {
                PickNewTarget();
                _isIdle = false;
            }
            return;
        }

        Vector3 to = _target - transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < arriveThreshold * arriveThreshold)
        {
            _isIdle = true;
            _idleTimer = idleTime;
            return;
        }

        // Smooth rotate to face the target
        Quaternion want = Quaternion.LookRotation(to.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, turnSpeed * Time.deltaTime);

        // Walk forward at walkSpeed
        transform.position += transform.forward * walkSpeed * Time.deltaTime;
        SetAnimatorSpeed(walkSpeed);
    }

    private void PickNewTarget()
    {
        // Try up to 5 random points in the area, rejecting any that would
        // require walking through a wall. Without a NavMesh, a raycast from
        // current position to the candidate is enough — every Wall/Door has
        // a MeshCollider, so any blocked path returns a hit.
        for (int attempt = 0; attempt < 5; attempt++)
        {
            Vector2 r = Random.insideUnitCircle * areaRadius;
            Vector3 candidate = new Vector3(areaCenter.x + r.x, transform.position.y, areaCenter.z + r.y);
            Vector3 dir = candidate - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) continue;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int npc = LayerMask.NameToLayer("NPC");
            int wallMask = npc >= 0 ? ~(1 << npc) : ~0;
            if (!Physics.Raycast(origin, dir.normalized, dir.magnitude, wallMask, QueryTriggerInteraction.Ignore))
            {
                _target = candidate;
                return;
            }
        }
        _target = transform.position;
    }

    private void SetAnimatorSpeed(float s)
    {
        if (animator == null) return;
        if (_hasSpeedParam)        animator.SetFloat(speedParam, s);
        else if (_hasWalkingParam) animator.SetBool(isWalkingParam, s > 0.05f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.2f);
        Gizmos.DrawWireSphere(areaCenter, areaRadius);
        if (Application.isPlaying)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(_target, 0.15f);
        }
    }
}
