using UnityEngine;

public class MovingPlatform : MonoBehaviour
{
    [SerializeField] private Vector3 moveAxis = Vector3.right;
    [SerializeField] private float distance = 3f;
    [SerializeField] private float speed = 1.5f;

    private Vector3 origin;
    private void Start() => origin = transform.position;

    private void Update()
    {
        float t = Mathf.PingPong(Time.time * speed, distance);
        transform.position = origin + moveAxis.normalized * t;
    }
}
