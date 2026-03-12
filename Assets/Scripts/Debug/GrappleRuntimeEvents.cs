using System;
using UnityEngine;

/// <summary>
/// Static event bus for decoupled communication between grapple subsystems.
/// Audio, VFX, haptics, and telemetry subscribe here rather than coupling to core scripts.
///
/// Fire sites: GrappleLauncher (hook events), MonsterHitReceiver (combat events).
/// Subscriber sites: ImpactFeedback, GrappleHapticsDriver, GrappleTelemetry.
/// </summary>
public static class GrappleRuntimeEvents
{
    /// <summary>A grapple hook was fired from this hand.</summary>
    public static event Action<Hand>          OnHookFired;

    /// <summary>Hook attached to a target. worldPos = world-space attach point.</summary>
    public static event Action<Hand, Vector3> OnHookAttached;

    /// <summary>Hook missed (hit nothing or exceeded max range).</summary>
    public static event Action<Hand>          OnHookMissed;

    /// <summary>Player released the chain (throw or detach).</summary>
    public static event Action<Hand>          OnChainReleased;

    /// <summary>Monster received a valid player-scored hit. hitCount = running total.</summary>
    public static event Action<int>           OnMonsterHit;

    /// <summary>Monster's hit count reached hitsToKill.</summary>
    public static event Action                OnMonsterDefeated;

    public static void RaiseHookFired(Hand hand)                        => OnHookFired?.Invoke(hand);
    public static void RaiseHookAttached(Hand hand, Vector3 worldPos)   => OnHookAttached?.Invoke(hand, worldPos);
    public static void RaiseHookMissed(Hand hand)                       => OnHookMissed?.Invoke(hand);
    public static void RaiseChainReleased(Hand hand)                    => OnChainReleased?.Invoke(hand);
    public static void RaiseMonsterHit(int hitCount)                    => OnMonsterHit?.Invoke(hitCount);
    public static void RaiseMonsterDefeated()                           => OnMonsterDefeated?.Invoke();
}
