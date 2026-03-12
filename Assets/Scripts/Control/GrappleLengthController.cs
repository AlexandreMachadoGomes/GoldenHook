using UnityEngine;

/// <summary>
/// Tracks how much retraction distance has accumulated and converts it into
/// discrete link removals on the chain.
///
/// OWNERSHIP: Knows about chain structure (link count, spacing). Does NOT know
/// about input or timing — that's GrappleRetractionMotor.
/// </summary>
public class GrappleLengthController : MonoBehaviour
{
    // Accumulates sub-link-length retraction distance until a full link can be removed.
    float _accumMeters;

    public void Reset() => _accumMeters = 0f;

    /// <summary>
    /// Called by GrappleRetractionMotor each FixedUpdate while retracting.
    /// Converts accumulated meters into link removals on the chain.
    /// </summary>
    public void AddRetraction(float meters, GrappleChainRuntime chain)
    {
        if (chain == null) return;

        _accumMeters += meters;

        while (_accumMeters >= chain.LinkSpacing && chain.CanRemoveLink)
        {
            chain.RemovePlayerSideLink();
            _accumMeters -= chain.LinkSpacing;
        }

        // If chain is fully retracted (1 link left), clamp accumulator so it
        // doesn't build up indefinitely.
        if (!chain.CanRemoveLink)
            _accumMeters = 0f;
    }
}
