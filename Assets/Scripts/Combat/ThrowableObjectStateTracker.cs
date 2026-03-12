using UnityEngine;

/// <summary>
/// Companion to ThrowablePhysicsObject. Records when each state was entered
/// and how many full lifecycle trips the object has made through the pool.
///
/// Keeps ThrowablePhysicsObject clean by extracting observability concerns.
/// Used by GrappleDebugGizmos and GrappleTelemetry (Batch 6).
/// </summary>
public class ThrowableObjectStateTracker : MonoBehaviour
{
    public ThrowablePhysicsObject.ThrowableState LastState { get; private set; }
    public float StateEnteredTime  { get; private set; }
    public float TimeInCurrentState => Time.time - StateEnteredTime;
    public int   LifecycleCount    { get; private set; }

    ThrowablePhysicsObject _throwable;

    void Awake()
    {
        _throwable = GetComponent<ThrowablePhysicsObject>();
        LastState  = _throwable.State;
    }

    void Update()
    {
        if (_throwable == null) return;

        var current = _throwable.State;
        if (current == LastState) return;

        if (current == ThrowablePhysicsObject.ThrowableState.ThrownByMonster)
            LifecycleCount++;

        StateEnteredTime = Time.time;
        LastState        = current;
    }
}
