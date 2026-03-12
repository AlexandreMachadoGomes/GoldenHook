using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks the attached target's velocity while the chain is live and applies a
/// weighted-average smoothed velocity at the moment of release.
///
/// Problem it solves: joint oscillation in the final frame before detach can give
/// the target a noisy, incorrect velocity. Smoothing over the last N frames gives
/// a more natural throw direction and speed.
///
/// USAGE:
///   Call BeginTracking(targetBody) when the chain attaches.
///   Call ApplyRelease()           just before the chain is destroyed.
///
/// PLACEMENT: Same controller GameObject as GrappleLauncher.
/// </summary>
public class GrappleReleaseVelocityHelper : MonoBehaviour
{
    [Header("Tuning")]
    [Tooltip("Number of FixedUpdate frames to average over.")]
    [SerializeField] int   sampleCount    = 6;
    [Tooltip("Maximum speed applied on release (m/s). 0 = no cap.")]
    [SerializeField] float maxReleaseSpeed = 20f;

    Rigidbody        _target;
    Queue<Vector3>   _velocitySamples = new Queue<Vector3>();

    // -----------------------------------------------------------------------

    /// <summary>Start sampling the target's velocity each FixedUpdate.</summary>
    public void BeginTracking(Rigidbody target)
    {
        _target = target;
        _velocitySamples.Clear();
    }

    /// <summary>
    /// Apply the smoothed release velocity to the target, then stop tracking.
    /// Call this before GrappleChainRuntime.Release() so TargetBody is still valid.
    /// </summary>
    public void ApplyRelease()
    {
        if (_target == null || _velocitySamples.Count == 0)
        {
            StopTracking();
            return;
        }

        Vector3 smoothed = WeightedAverage(_velocitySamples);

        if (maxReleaseSpeed > 0f)
            smoothed = Vector3.ClampMagnitude(smoothed, maxReleaseSpeed);

        _target.linearVelocity = smoothed;
        StopTracking();
    }

    // -----------------------------------------------------------------------

    void FixedUpdate()
    {
        if (_target == null) return;

        _velocitySamples.Enqueue(_target.linearVelocity);
        while (_velocitySamples.Count > sampleCount)
            _velocitySamples.Dequeue();
    }

    void StopTracking()
    {
        _target = null;
        _velocitySamples.Clear();
    }

    /// <summary>
    /// Linearly weighted average — more recent samples have higher weight.
    /// Weight of sample at index i (0 = oldest) = i + 1.
    /// </summary>
    static Vector3 WeightedAverage(Queue<Vector3> samples)
    {
        Vector3 sum        = Vector3.zero;
        float   totalWeight = 0f;
        int     i          = 0;

        foreach (var v in samples)
        {
            float w  = i + 1;
            sum         += v * w;
            totalWeight += w;
            i++;
        }

        return totalWeight > 0f ? sum / totalWeight : Vector3.zero;
    }
}
