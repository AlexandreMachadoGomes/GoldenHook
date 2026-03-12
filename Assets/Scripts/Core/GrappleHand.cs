/// <summary>Identifies which hand owns a grapple system component.</summary>
public enum Hand { Left, Right }

/// <summary>Player intent state for one grapple hand.</summary>
public enum GrappleState
{
    Idle,       // Hook not deployed
    Fired,      // Hook projectile in flight
    Attached,   // Hook embedded in target, chain live
    Retracting  // Chain being pulled in (subset of Attached)
}
