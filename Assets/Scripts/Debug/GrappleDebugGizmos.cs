using UnityEngine;

/// <summary>
/// Draws Scene-view gizmos during play mode for chain links, hook projectile,
/// and attach points. Add to any GameObject in the scene; it will find active
/// chains and hooks automatically.
///
/// Disable this component to turn off gizmos without removing it.
/// </summary>
public class GrappleDebugGizmos : MonoBehaviour
{
    [Header("Colors")]
    [SerializeField] Color chainLinkColor    = new Color(0f, 1f, 0.4f, 0.9f);
    [SerializeField] Color anchorLineColor   = Color.cyan;
    [SerializeField] Color hookColor         = Color.yellow;
    [SerializeField] Color attachPointColor  = Color.red;

    [Header("Sizes")]
    [SerializeField] float linkSphereRadius  = 0.015f;
    [SerializeField] float attachSphereRadius= 0.04f;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;

        DrawActiveChains();
        DrawActiveHooks();
    }

    void DrawActiveChains()
    {
        var chains = FindObjectsByType<GrappleChainRuntime>(FindObjectsSortMode.None);
        foreach (var chain in chains)
        {
            if (chain == null || chain.Links == null) continue;

            // Draw each link as a sphere.
            Gizmos.color = chainLinkColor;
            for (int i = 0; i < chain.Links.Length; i++)
            {
                var link = chain.Links[i];
                if (link == null) continue;
                Gizmos.DrawSphere(link.transform.position, linkSphereRadius);
            }

            // Draw connecting lines between adjacent links.
            Gizmos.color = chainLinkColor * 0.7f;
            for (int i = 1; i < chain.Links.Length; i++)
            {
                if (chain.Links[i - 1] == null || chain.Links[i] == null) continue;
                Gizmos.DrawLine(chain.Links[i - 1].transform.position,
                                chain.Links[i].transform.position);
            }

            // Line from player anchor to first link.
            if (chain.PlayerAnchor != null && chain.Links.Length > 0 && chain.Links[0] != null)
            {
                Gizmos.color = anchorLineColor;
                Gizmos.DrawLine(chain.PlayerAnchor.transform.position,
                                chain.Links[0].transform.position);
            }

            // Attach point on target.
            if (chain.TargetBody != null)
            {
                Gizmos.color = attachPointColor;
                Vector3 worldAttach = chain.TargetBody.transform.TransformPoint(
                    chain.AttachResult.WorldAttachPoint);
                Gizmos.DrawSphere(worldAttach, attachSphereRadius);

                // Line from last link to target attach point.
                if (chain.Links.Length > 0)
                {
                    var lastLink = chain.Links[chain.Links.Length - 1];
                    if (lastLink != null)
                        Gizmos.DrawLine(lastLink.transform.position, worldAttach);
                }
            }
        }
    }

    void DrawActiveHooks()
    {
        var hooks = FindObjectsByType<GrappleHookProjectile>(FindObjectsSortMode.None);
        foreach (var hook in hooks)
        {
            if (hook == null || !hook.gameObject.activeInHierarchy) continue;
            Gizmos.color = hookColor;
            Gizmos.DrawWireSphere(hook.transform.position, 0.04f);
        }
    }
}
