using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Drives controller haptics in response to GrappleRuntimeEvents.
/// Uses UnityEngine.XR.InputDevices — works with OpenXR on Quest.
///
/// Add one instance to each hand's controller GameObject.
/// Set the Hand field to match Left or Right.
/// </summary>
public class GrappleHapticsDriver : MonoBehaviour
{
    [Header("Hand")]
    [SerializeField] Hand hand;

    [Header("Impulses (amplitude 0–1, duration seconds)")]
    [SerializeField] float fireAmplitude      = 0.3f; [SerializeField] float fireDuration      = 0.08f;
    [SerializeField] float attachAmplitude    = 0.8f; [SerializeField] float attachDuration    = 0.12f;
    [SerializeField] float missAmplitude      = 0.15f;[SerializeField] float missDuration      = 0.05f;
    [SerializeField] float releaseAmplitude   = 0.5f; [SerializeField] float releaseDuration   = 0.10f;
    [SerializeField] float monsterHitAmplitude= 1.0f; [SerializeField] float monsterHitDuration= 0.20f;

    void OnEnable()
    {
        GrappleRuntimeEvents.OnHookFired      += OnHookFired;
        GrappleRuntimeEvents.OnHookAttached   += OnHookAttached;
        GrappleRuntimeEvents.OnHookMissed     += OnHookMissed;
        GrappleRuntimeEvents.OnChainReleased  += OnChainReleased;
        GrappleRuntimeEvents.OnMonsterHit     += OnMonsterHit;
    }

    void OnDisable()
    {
        GrappleRuntimeEvents.OnHookFired      -= OnHookFired;
        GrappleRuntimeEvents.OnHookAttached   -= OnHookAttached;
        GrappleRuntimeEvents.OnHookMissed     -= OnHookMissed;
        GrappleRuntimeEvents.OnChainReleased  -= OnChainReleased;
        GrappleRuntimeEvents.OnMonsterHit     -= OnMonsterHit;
    }

    void OnHookFired(Hand h)            { if (h == hand) Rumble(fireAmplitude,       fireDuration); }
    void OnHookMissed(Hand h)           { if (h == hand) Rumble(missAmplitude,       missDuration); }
    void OnChainReleased(Hand h)        { if (h == hand) Rumble(releaseAmplitude,    releaseDuration); }
    void OnMonsterHit(int _)                             { Rumble(monsterHitAmplitude, monsterHitDuration); }

    void OnHookAttached(Hand h, Vector3 _)
    { if (h == hand) Rumble(attachAmplitude, attachDuration); }

    void Rumble(float amplitude, float duration)
    {
        XRNode node = hand == Hand.Left ? XRNode.LeftHand : XRNode.RightHand;
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(node, devices);
        foreach (var device in devices)
            device.SendHapticImpulse(0, amplitude, duration);
    }
}
