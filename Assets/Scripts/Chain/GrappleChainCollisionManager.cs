using UnityEngine;

/// <summary>
/// Per-chain-instance collision filtering.
/// The global layer collision matrix (ChainLink ignores Player, ChainLink ignores ChainLink,
/// Throwable ignores ChainLink) covers most cases. This component handles edge cases:
/// chain links must not collide with the specific target object they are attached to.
/// </summary>
public class GrappleChainCollisionManager : MonoBehaviour
{
    /// <summary>
    /// Called by GrappleChainFactory after all links are created.
    /// Ignores collisions between every link and the target object's colliders.
    /// </summary>
    public void IgnoreTargetColliders(GrappleChainLink[] links, GameObject targetGO)
    {
        if (targetGO == null) return;

        var targetColliders = targetGO.GetComponentsInChildren<Collider>(includeInactive: true);
        if (targetColliders.Length == 0) return;

        foreach (var link in links)
        {
            var linkCol = link.GetComponent<Collider>();
            if (linkCol == null) continue;

            foreach (var tc in targetColliders)
                Physics.IgnoreCollision(linkCol, tc, true);
        }
    }
}
