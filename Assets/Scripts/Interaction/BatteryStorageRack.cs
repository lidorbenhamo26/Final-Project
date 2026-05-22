using UnityEngine;

/// <summary>
/// Marks where a fresh power cell appears in central storage. BatteryDeliveryTask
/// finds this at runtime and spawns the battery at SpawnPosition (the geometric
/// centre of the rack mesh, so the cell sits visually centred inside the box).
/// </summary>
[DisallowMultipleComponent]
public class BatteryStorageRack : MonoBehaviour
{
    [Tooltip("Optional override. If unset, the cell spawns at the rack mesh's geometric centre.")]
    public Transform spawnPoint;

    public Vector3 SpawnPosition
    {
        get
        {
            // Centre on the rack mesh so the cell sits in the middle of the box.
            if (TryGetComponent(out MeshRenderer rend)) return rend.bounds.center;
            if (spawnPoint != null) return spawnPoint.position;
            return transform.position + Vector3.up;
        }
    }

    public Quaternion SpawnRotation =>
        spawnPoint != null ? spawnPoint.rotation : transform.rotation;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.85f, 1f, 0.6f);
        Gizmos.DrawWireSphere(SpawnPosition, 0.3f);
    }
}
