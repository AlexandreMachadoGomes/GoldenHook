using System;
using UnityEngine;

/// <summary>
/// Owns the intent state machine for one grapple hand (Left or Right).
///
/// OWNERSHIP: This component knows WHAT the player wants. It does NOT know
/// how the chain works or how the hook launches. Other systems subscribe to
/// its events and drive their own behavior.
///
/// UPDATE / FIXEDUPDATE BRIDGE:
/// - Fire input is captured via XRHandGrappleInput's callback system (frame-safe).
/// - State transitions and event dispatch happen in FixedUpdate so downstream
///   physics systems see consistent state within the physics step.
/// - Retract is read via IsPressed() directly in FixedUpdate (stateless, safe).
/// </summary>
[DisallowMultipleComponent]
public class GrappleHandController : MonoBehaviour
{
    [SerializeField] Hand hand;
    [SerializeField] XRHandGrappleInput input;

    // ---- State ----
    public GrappleState State { get; private set; } = GrappleState.Idle;
    public Hand Hand => hand;

    // ---- Events (dispatched from FixedUpdate) ----
    /// <summary>Hook should be launched from the hand anchor this frame.</summary>
    public event Action OnFireRequested;

    /// <summary>Retract state changed. bool = is now retracting.</summary>
    public event Action<bool> OnRetractChanged;

    /// <summary>Hook should detach and object released.</summary>
    public event Action OnReleaseRequested;

    /// <summary>State changed to Attached (called by GrappleLauncher after hook lands).</summary>
    public event Action OnAttached;

    // ---- Internal ----
    bool _wasRetracting;

    // -----------------------------------------------------------------------
    // Public API — called by other systems to drive state

    /// <summary>
    /// Forces an immediate release from any active state.
    /// Used by GrappleDetachController for tension-based or programmatic detach.
    /// Fires OnReleaseRequested so GrappleLauncher can clean up chain/hook.
    /// </summary>
    public void ForceRelease()
    {
        if (State == GrappleState.Idle || State == GrappleState.Fired) return;
        _wasRetracting = false;
        State = GrappleState.Idle;
        OnReleaseRequested?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Simulation API — for ChainStressTester (automated testing only)

    /// <summary>Programmatically fires the hook. No-op if not Idle.</summary>
    public void SimulateFire()
    {
        if (State != GrappleState.Idle) return;
        State = GrappleState.Fired;
        OnFireRequested?.Invoke();
    }

    /// <summary>Programmatically starts retraction. No-op if not Attached.</summary>
    public void SimulateRetractStart()
    {
        if (State != GrappleState.Attached) return;
        _wasRetracting = true;
        State = GrappleState.Retracting;
        OnRetractChanged?.Invoke(true);
    }

    /// <summary>Programmatically stops retraction. No-op if not Retracting.</summary>
    public void SimulateRetractStop()
    {
        if (State != GrappleState.Retracting) return;
        _wasRetracting = false;
        State = GrappleState.Attached;
        OnRetractChanged?.Invoke(false);
    }

    /// <summary>Programmatically releases the chain. No-op if Idle or Fired.</summary>
    public void SimulateRelease()
    {
        if (State == GrappleState.Idle || State == GrappleState.Fired) return;
        _wasRetracting = false;
        State = GrappleState.Idle;
        OnReleaseRequested?.Invoke();
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// External systems (GrappleLauncher, chain) call this to advance state.
    /// Only valid transitions are allowed; invalid calls are silently ignored.
    /// </summary>
    public void SetState(GrappleState next)
    {
        if (next == State) return;

        switch (next)
        {
            case GrappleState.Idle:
                // Any state → Idle (detach/cancel)
                break;
            case GrappleState.Fired:
                if (State != GrappleState.Idle) return;
                break;
            case GrappleState.Attached:
                if (State != GrappleState.Fired) return;
                break;
            case GrappleState.Retracting:
                if (State != GrappleState.Attached) return;
                break;
        }

        State = next;
        if (next == GrappleState.Attached) OnAttached?.Invoke();
    }

    // -----------------------------------------------------------------------

    void FixedUpdate()
    {
        switch (State)
        {
            case GrappleState.Idle:
                HandleIdle();
                break;
            case GrappleState.Fired:
                // Waiting for attach confirmation from GrappleLauncher.
                // Optional: cancel fire if trigger released while in flight.
                break;
            case GrappleState.Attached:
            case GrappleState.Retracting:
                HandleAttached();
                break;
        }
    }

    void HandleIdle()
    {
        bool fire = hand == Hand.Left
            ? input.ConsumeLeftFire()
            : input.ConsumeRightFire();

        if (!fire) return;

        State = GrappleState.Fired;
        OnFireRequested?.Invoke();
    }

    void HandleAttached()
    {
        bool retractHeld = hand == Hand.Left
            ? input.LeftRetractHeld
            : input.RightRetractHeld;

        // Detect retract state change and notify.
        if (retractHeld != _wasRetracting)
        {
            _wasRetracting = retractHeld;
            State = retractHeld ? GrappleState.Retracting : GrappleState.Attached;
            OnRetractChanged?.Invoke(retractHeld);
        }

        // Detect release: fire button pressed while attached → release.
        bool releaseFire = hand == Hand.Left
            ? input.ConsumeLeftFire()
            : input.ConsumeRightFire();

        if (releaseFire)
        {
            _wasRetracting = false;
            State = GrappleState.Idle;
            OnReleaseRequested?.Invoke();
        }
    }

    void OnDisable()
    {
        // Clean up state if component toggled at runtime.
        if (State != GrappleState.Idle)
        {
            _wasRetracting = false;
            State = GrappleState.Idle;
        }
    }
}
