using UnityEngine;

/// <summary>
/// Trigger zone that recycles throwable objects that leave the arena
/// (fall through the floor, fly over walls, etc.).
///
/// Place a large trigger collider below and around the arena geometry.
/// </summary>
public class ArenaRecycleZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        var throwable = other.GetComponent<ThrowablePhysicsObject>();
        throwable?.Recycle();
    }
}
