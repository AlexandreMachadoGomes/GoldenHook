using UnityEngine;

/// <summary>
/// Data returned by GrappleAttachResolver. Passed through the hook attach event chain
/// so all downstream systems (chain factory, telemetry) get the same picture.
/// </summary>
public class GrappleAttachResult
{
    /// <summary>False if the hook hit something non-grappleable.</summary>
    public bool IsValid;

    /// <summary>The Rigidbody the hook is embedded in. Null if static geometry.</summary>
    public Rigidbody TargetBody;

    /// <summary>World-space point where the hook made contact.</summary>
    public Vector3 WorldAttachPoint;

    /// <summary>GrappleTarget component on the hit object, if present.</summary>
    public GrappleTarget Target;
}
