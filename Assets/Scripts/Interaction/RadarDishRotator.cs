using UnityEngine;

/// <summary>Spins a GameObject around its Y axis at a fixed degrees-per-second rate.
/// Used to animate the decorative radar dish above NavigationStation.</summary>
public class RadarDishRotator : MonoBehaviour
{
    [SerializeField] private float degreesPerSecond = 30f;

    private void Update()
    {
        transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f, Space.Self);
    }
}
