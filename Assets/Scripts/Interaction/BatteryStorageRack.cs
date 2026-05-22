using UnityEngine;

/// <summary>
/// Marks where a fresh power cell appears in central storage. BatteryDeliveryTask
/// finds this at runtime and spawns the battery at SpawnPoint.
/// </summary>
[DisallowMultipleComponent]
public class BatteryStorageRack : MonoBehaviour
{
    [Tooltip("Where the cell spawns. Falls back to this object's position + 1m up.")]
    public Transform spawnPoint;

    public Vector3 SpawnPosition =>
        spawnPoint != null ? spawnPoint.position : transform.position + Vector3.up;

    public Quaternion SpawnRotation =>
        spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(SpawnPosition, 0.3f);
    }
}
