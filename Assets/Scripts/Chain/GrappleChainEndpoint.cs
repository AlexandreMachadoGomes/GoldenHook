using UnityEngine;

/// <summary>
/// Added to the target object's GameObject when a chain attaches to it.
/// Marks the object as "currently hooked" and stores the attach metadata.
/// Cleaned up by GrappleChainRuntime.Release().
///
/// Dual-hook support (Batch future): a second hook on the same object
/// will find this component and know the object already has a chain.
/// </summary>
public class GrappleChainEndpoint : MonoBehaviour
{
    public GrappleChainRuntime AttachedChain  { get; private set; }

    /// <summary>Attach point in the target body's local space.</summary>
    public Vector3 LocalAttachPoint { get; private set; }

    public bool IsAttached => AttachedChain != null;

    public void Attach(GrappleChainRuntime chain, Vector3 localAttachPoint)
    {
        AttachedChain    = chain;
        LocalAttachPoint = localAttachPoint;
    }

    public void Detach()
    {
        AttachedChain = null;
    }
}
