using UnityEngine;

/// <summary>
/// Gives a transform a gentle hover (bob up/down) and idle yaw spin.
/// Use on non-rigged props that should look "alive" (e.g. the service robot
/// pet that floats next to the player).
/// </summary>
[DisallowMultipleComponent]
public class HoverBob : MonoBehaviour
{
    [Header("Bob")]
    public float bobAmplitude = 0.12f;
    public float bobSpeed = 1.4f;

    [Header("Spin")]
    [Tooltip("Idle yaw spin (deg/sec). 0 disables.")]
    public float yawSpin = 8f;

    [Header("Phase")]
    [Tooltip("Random phase offset on enable so multiple props don't bob in sync.")]
    public bool randomizePhase = true;

    private Vector3 _basePosition;
    private float _phase;

    private void OnEnable()
    {
        _basePosition = transform.localPosition;
        _phase = randomizePhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
    }

    private void Update()
    {
        float y = Mathf.Sin(Time.time * bobSpeed + _phase) * bobAmplitude;
        transform.localPosition = _basePosition + new Vector3(0f, y, 0f);
        if (Mathf.Abs(yawSpin) > 0.001f)
            transform.Rotate(0f, yawSpin * Time.deltaTime, 0f, Space.Self);
    }
}
