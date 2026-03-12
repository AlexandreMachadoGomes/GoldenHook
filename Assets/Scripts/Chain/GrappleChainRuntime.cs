using UnityEngine;

/// <summary>
/// Owns the live physical chain between the player's hand and the attached target.
///
/// OWNERSHIP: links, joints, endpoints. Does NOT know about input, launching, or retraction.
///
/// Lifetime: spawned by GrappleChainFactory on hook attach, destroyed via Release().
/// </summary>
public class GrappleChainRuntime : MonoBehaviour
{
    // ---- Chain structure ----
    public GrappleChainLink[]  Links        { get; private set; }
    public Rigidbody           PlayerAnchor { get; private set; }
    public Rigidbody           TargetBody   { get; private set; }
    public GrappleAttachResult AttachResult { get; private set; }

    /// <summary>World-space distance between consecutive link centers at spawn time.</summary>
    public float LinkSpacing { get; private set; }

    GrappleChainConfig _config;
    float _halfSpacing;

    // ---- Convenience ----
    public GrappleChainLink PlayerSideLink => Links != null && Links.Length > 0 ? Links[0]              : null;
    public GrappleChainLink TargetSideLink => Links != null && Links.Length > 0 ? Links[Links.Length-1] : null;
    public bool             CanRemoveLink  => Links != null && Links.Length >= 2;

    // -----------------------------------------------------------------------

    public void Initialize(
        GrappleChainLink[]  links,
        Rigidbody           playerAnchor,
        Rigidbody           targetBody,
        GrappleAttachResult attachResult,
        GrappleChainConfig  config,
        float               linkSpacing)
    {
        Links        = links;
        PlayerAnchor = playerAnchor;
        TargetBody   = targetBody;
        AttachResult = attachResult;
        _config      = config;
        LinkSpacing  = linkSpacing;
        _halfSpacing = linkSpacing * 0.5f;
    }

    // -----------------------------------------------------------------------
    // Retraction (Batch 4)

    /// <summary>
    /// Removes the player-side link and reconnects the new first link to the HandAnchorProvider.
    /// Returns false if the chain is too short to shorten further.
    ///
    /// Mechanism: updates connectedBody on the existing joint in-place (no destroy/recreate)
    /// then destroys link 0's GameObject. Avoids jolt better than re-adding joints.
    /// </summary>
    public bool RemovePlayerSideLink()
    {
        if (!CanRemoveLink) return false;

        var linkToRemove = Links[0];
        var newFirstLink = Links[1];

        // Rewire link 1's joint so it now connects to the HandAnchorProvider (player anchor)
        // instead of the old link 0. Anchor geometry matches a first-link configuration.
        var driver = newFirstLink.GetComponent<GrappleChainJointDriver>();
        driver.Reconnect(
            newConnectedBody: PlayerAnchor,
            localAnchor:      new Vector3(0f, 0f, -_halfSpacing),
            connectedAnchor:  Vector3.zero);

        Destroy(linkToRemove.gameObject);

        // Rebuild array without link 0.
        var newLinks = new GrappleChainLink[Links.Length - 1];
        System.Array.Copy(Links, 1, newLinks, 0, newLinks.Length);
        Links = newLinks;

        return true;
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// Detaches from the target and destroys the entire chain hierarchy.
    /// Safe to call multiple times.
    /// </summary>
    public void Release()
    {
        if (AttachResult?.Target != null)
        {
            var ep = AttachResult.Target.GetComponent<GrappleChainEndpoint>();
            if (ep != null) ep.Detach();
        }
        Destroy(gameObject);
    }
}
