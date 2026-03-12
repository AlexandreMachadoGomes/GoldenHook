using UnityEngine;

/// <summary>
/// Tracks gameplay state for one throwable arena object across its full lifecycle:
/// spawned by pool → thrown by monster → hooked by player → released → hits monster → recycled.
///
/// OWNERSHIP: Is this hit on the monster valid? Was this under player control recently?
/// Does NOT know about chains, joints, or input. Reads GrappleChainEndpoint passively.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class ThrowablePhysicsObject : MonoBehaviour
{
    public enum ThrowableState
    {
        Pooled,             // Inactive, waiting in pool
        ThrownByMonster,    // In flight from monster
        HookedByPlayer,     // Chain attached — under player control
        ReleasedByPlayer,   // Player released — valid throw window open
        Spent               // Hit something or out of bounds — being recycled
    }

    public ThrowableState State { get; private set; } = ThrowableState.Pooled;

    [Tooltip("Seconds after release during which a monster hit counts as player-scored.")]
    [SerializeField] float validHitWindow = 6f;

    // ---- Internal ----
    ThrowableObjectPool     _pool;
    GrappleChainEndpoint    _endpoint;       // added by GrappleChainFactory on hook attach
    Rigidbody               _rb;
    float                   _releasedTime;
    bool                    _endpointWasAttached;

    void Awake() => _rb = GetComponent<Rigidbody>();

    // -----------------------------------------------------------------------
    // Pool API

    /// <summary>Called by MonsterThrower after Get() from pool.</summary>
    public void Launch(ThrowableObjectPool pool, Vector3 velocity)
    {
        _pool                 = pool;
        _endpoint             = null;
        _endpointWasAttached  = false;
        _releasedTime         = 0f;
        State                 = ThrowableState.ThrownByMonster;

        _rb.isKinematic    = false;
        _rb.linearVelocity  = velocity;
        _rb.angularVelocity = Vector3.zero;
    }

    /// <summary>Returns this object to the pool. Safe to call from any state.</summary>
    public void Recycle()
    {
        if (State == ThrowableState.Spent || State == ThrowableState.Pooled) return;

        State            = ThrowableState.Spent;
        _rb.isKinematic  = true;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        _pool?.Return(this);
    }

    // -----------------------------------------------------------------------
    // Hit validation

    /// <summary>
    /// Returns true if this object was recently released by the player and hitting
    /// the monster now counts as a player-scored hit.
    /// </summary>
    public bool IsValidPlayerProjectile()
    {
        return State == ThrowableState.ReleasedByPlayer &&
               (Time.time - _releasedTime) <= validHitWindow;
    }

    // -----------------------------------------------------------------------
    // State tracking (polls GrappleChainEndpoint — no coupling to chain internals)

    void FixedUpdate()
    {
        if (State == ThrowableState.Pooled || State == ThrowableState.Spent) return;

        // Lazily grab the endpoint component — GrappleChainFactory adds it at attach time.
        if (_endpoint == null)
            _endpoint = GetComponent<GrappleChainEndpoint>();

        bool isAttached = _endpoint != null && _endpoint.IsAttached;

        switch (State)
        {
            case ThrowableState.ThrownByMonster:
                if (isAttached)
                    State = ThrowableState.HookedByPlayer;
                break;

            case ThrowableState.HookedByPlayer:
                if (!isAttached)
                {
                    State         = ThrowableState.ReleasedByPlayer;
                    _releasedTime = Time.time;
                }
                break;

            case ThrowableState.ReleasedByPlayer:
                // If player re-hooks it after releasing, track the new attach.
                if (isAttached)
                    State = ThrowableState.HookedByPlayer;
                // Expire the valid hit window.
                else if ((Time.time - _releasedTime) > validHitWindow)
                    State = ThrowableState.ThrownByMonster; // treat as non-player controlled
                break;
        }

        _endpointWasAttached = isAttached;
    }
}
