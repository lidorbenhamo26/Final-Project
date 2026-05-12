using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

/// <summary>
/// Optional 3D button for placing on a Cube/Cylinder mesh that's a child of a
/// console. Requires a PhysicsRaycaster on the active camera (StationDockController
/// adds one automatically). Animates the button down 0.02m on click and pulses
/// the renderer's emission. Cognitive tasks (Agent A) generally use UI Canvas
/// buttons instead — this is a complementary tactile-feeling option.
/// </summary>
[DisallowMultipleComponent]
public class StationButton3D : MonoBehaviour, IPointerClickHandler
{
    [Header("Click")]
    public UnityEvent onClick;

    [Header("Press Animation")]
    [Tooltip("Local-space depth the button travels when pressed (m).")]
    public float pressDepth = 0.02f;
    [Tooltip("Time to travel down + back up (seconds).")]
    public float pressDuration = 0.12f;

    [Header("Emission Flash")]
    [Tooltip("Emission color used during the click flash. Renderer must support _EmissionColor.")]
    public Color flashColor = new Color(0.6f, 1.2f, 1.6f, 1f);
    [Tooltip("Total flash duration (seconds).")]
    public float flashDuration = 0.18f;

    private Renderer _renderer;
    private Material _matInstance;
    private Color _baseEmission;
    private bool _hasEmission;
    private Vector3 _restLocalPos;
    private bool _animating;

    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    private void Awake()
    {
        _restLocalPos = transform.localPosition;
        _renderer = GetComponentInChildren<Renderer>();
        if (_renderer != null)
        {
            _matInstance = _renderer.material; // instance, not sharedMaterial
            if (_matInstance != null && _matInstance.HasProperty(EmissionColorId))
            {
                _hasEmission = true;
                _baseEmission = _matInstance.GetColor(EmissionColorId);
            }
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!enabled) return;
        AudioManager.Instance.PlaySfx("button_click");
        if (!_animating) StartCoroutine(PressRoutine());
        if (_hasEmission) StartCoroutine(FlashRoutine());
        onClick?.Invoke();
    }

    private IEnumerator PressRoutine()
    {
        _animating = true;
        Vector3 down = _restLocalPos + Vector3.down * pressDepth;
        float half = Mathf.Max(0.01f, pressDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(_restLocalPos, down, t / half);
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            transform.localPosition = Vector3.Lerp(down, _restLocalPos, t / half);
            yield return null;
        }
        transform.localPosition = _restLocalPos;
        _animating = false;
    }

    private IEnumerator FlashRoutine()
    {
        if (!_hasEmission || _matInstance == null) yield break;
        float half = Mathf.Max(0.01f, flashDuration * 0.5f);

        float t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            _matInstance.SetColor(EmissionColorId, Color.Lerp(_baseEmission, flashColor, t / half));
            yield return null;
        }
        t = 0f;
        while (t < half)
        {
            t += Time.deltaTime;
            _matInstance.SetColor(EmissionColorId, Color.Lerp(flashColor, _baseEmission, t / half));
            yield return null;
        }
        _matInstance.SetColor(EmissionColorId, _baseEmission);
    }

    private void OnDisable()
    {
        if (_animating)
        {
            transform.localPosition = _restLocalPos;
            _animating = false;
        }
        if (_hasEmission && _matInstance != null)
            _matInstance.SetColor(EmissionColorId, _baseEmission);
    }
}
