using UnityEngine;

/// <summary>
/// Stateless resolver: converts a collision into a GrappleAttachResult.
///
/// OWNERSHIP: This class decides WHETHER and HOW a hit becomes an attachment.
/// It does not know about chains, launchers, or state machines.
/// </summary>
public static class GrappleAttachResolver
{
    /// <summary>
    /// Evaluate a collision and return an attach result.
    /// IsValid = false means the hook hit something it cannot attach to.
    /// </summary>
    public static GrappleAttachResult Resolve(Collision collision)
    {
        var result = new GrappleAttachResult();

        // Walk up from the hit collider to find a GrappleTarget.
        // Checking GetComponentInParent covers both the collider's own GO
        // and compound-collider setups where the target is on a parent.
        var target = collision.collider.GetComponentInParent<GrappleTarget>();
        if (target == null || !target.IsGrappleable)
            return result; // IsValid stays false

        Vector3 contactPoint = collision.GetContact(0).point;

        result.IsValid          = true;
        result.Target           = target;
        result.TargetBody       = target.GetComponentInParent<Rigidbody>();
        result.WorldAttachPoint = target.GetAttachPoint(contactPoint);

        return result;
    }
}
