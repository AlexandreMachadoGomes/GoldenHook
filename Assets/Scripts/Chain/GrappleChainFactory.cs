using UnityEngine;

/// <summary>
/// Spawns the segmented physics chain between the player's hand anchor and the attached target.
///
/// Joint geometry guarantee (no spawn jolt):
///   Links are placed at positions P_i = anchorPos + dir * linkSpacing * (i + 0.5)
///   so that each link's back anchor (local -Z * halfSpacing) and the previous
///   link's front anchor (local +Z * halfSpacing) already coincide in world space
///   at the moment the ConfigurableJoint is created → Locked constraint is
///   satisfied immediately → no impulse jolt on spawn.
/// </summary>
public static class GrappleChainFactory
{
    /// <summary>
    /// Spawns a full chain and returns the runtime that owns it.
    /// Call from GrappleLauncher.HandleHookAttached().
    /// </summary>
    public static GrappleChainRuntime SpawnChain(
        HandAnchorProvider  playerAnchor,
        GrappleAttachResult attachResult,
        GrappleChainConfig  config)
    {
        Vector3 startPos   = playerAnchor.AnchorPosition;
        Vector3 endPos     = attachResult.WorldAttachPoint;
        float   totalDist  = Vector3.Distance(startPos, endPos);

        // Direction along the chain (player → target).
        Vector3 dir = totalDist > 0.001f
            ? (endPos - startPos).normalized
            : Vector3.forward;

        // Scale link count to distance; cap at config max.
        int   linkCount   = Mathf.Clamp(Mathf.RoundToInt(totalDist / config.linkSpacing), 1, config.maxLinkCount);
        float linkSpacing = totalDist / linkCount;   // exact spacing so chain spans anchor→target
        float halfSpacing = linkSpacing * 0.5f;

        int chainLayer = LayerMask.NameToLayer("ChainLink");

        // Parent container
        var chainGO  = new GameObject("GrappleChain");
        var runtime  = chainGO.AddComponent<GrappleChainRuntime>();
        var colMgr   = chainGO.AddComponent<GrappleChainCollisionManager>();

        var links = new GrappleChainLink[linkCount];

        for (int i = 0; i < linkCount; i++)
        {
            // Place link center along the line, offset by half-spacing so the
            // back anchor lands exactly at the previous link's front anchor.
            Vector3    pos    = startPos + dir * (linkSpacing * (i + 0.5f));
            Quaternion rot    = Quaternion.LookRotation(dir);

            var linkGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            linkGO.name = $"Link_{i:00}";
            linkGO.transform.SetParent(chainGO.transform, worldPositionStays: true);
            linkGO.transform.SetPositionAndRotation(pos, rot);
            linkGO.transform.localScale = Vector3.one * config.linkRadius * 2f;

            if (chainLayer >= 0) linkGO.layer = chainLayer;

            // SphereCollider — CreatePrimitive(Sphere) adds one automatically; configure it.
            var col    = linkGO.GetComponent<SphereCollider>();
            col.radius = 0.5f; // local-space half-size (world radius = linkRadius after scale)

            // Rigidbody
            var rb = linkGO.AddComponent<Rigidbody>();
            rb.mass                     = config.linkMass;
            rb.linearDamping            = config.linkDrag;
            rb.angularDamping           = config.linkAngularDrag;
            rb.useGravity               = true;
            rb.interpolation            = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode   = CollisionDetectionMode.ContinuousSpeculative;
            rb.solverIterations         = config.solverIterations;
            rb.solverVelocityIterations = config.solverVelocityIterations;
            rb.maxAngularVelocity       = 20f;

            var link = linkGO.AddComponent<GrappleChainLink>();
            link.Initialize(i);

            // Joint — connects this link to the previous link (or player anchor for link 0)
            var driver = linkGO.AddComponent<GrappleChainJointDriver>();

            if (i == 0)
            {
                // Link 0: back of this link → player anchor center
                driver.Initialize(
                    connectedBody:    playerAnchor.AnchorBody,
                    localAnchor:      new Vector3(0f, 0f, -halfSpacing),
                    connectedAnchor:  Vector3.zero,
                    config:           config);
            }
            else
            {
                // Link i: back of this link → front of previous link
                driver.Initialize(
                    connectedBody:    links[i - 1].Body,
                    localAnchor:      new Vector3(0f, 0f, -halfSpacing),
                    connectedAnchor:  new Vector3(0f, 0f,  halfSpacing),
                    config:           config);
            }

            links[i] = link;
        }

        // Connect last link's FRONT to the target Rigidbody at the attach point.
        ConnectToTarget(links[linkCount - 1], halfSpacing, attachResult, config, runtime);

        // Per-instance collision filtering: links must not collide with the target object.
        if (attachResult.Target != null)
            colMgr.IgnoreTargetColliders(links, attachResult.Target.gameObject);

        runtime.Initialize(links, playerAnchor.AnchorBody, attachResult.TargetBody, attachResult, config, linkSpacing);
        return runtime;
    }

    // -------------------------------------------------------------------------

    static void ConnectToTarget(
        GrappleChainLink    lastLink,
        float               halfSpacing,
        GrappleAttachResult attachResult,
        GrappleChainConfig  config,
        GrappleChainRuntime runtime)
    {
        if (attachResult.TargetBody == null) return;

        // Compute attach point in target body's local space.
        Vector3 localAttach = attachResult.TargetBody.transform.InverseTransformPoint(
            attachResult.WorldAttachPoint);

        // Add a second ConfigurableJoint on the last link connecting it to the target.
        // (A Rigidbody can have multiple joints — one at its back to the chain,
        //  one at its front to the target.)
        var joint = lastLink.gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody                = attachResult.TargetBody;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor                       = new Vector3(0f, 0f, halfSpacing); // front of last link
        joint.connectedAnchor              = localAttach;

        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;
        joint.angularXMotion = ConfigurableJointMotion.Free;
        joint.angularYMotion = ConfigurableJointMotion.Free;
        joint.angularZMotion = ConfigurableJointMotion.Free;

        var drive = new JointDrive
        {
            positionSpring = 0f,
            positionDamper = config.angularDamper,
            maximumForce   = float.MaxValue
        };
        joint.angularXDrive  = drive;
        joint.angularYZDrive = drive;
        joint.enablePreprocessing = false;

        // Tag the target object so other systems know it's hooked.
        if (attachResult.Target != null)
        {
            var endpoint = attachResult.Target.GetComponent<GrappleChainEndpoint>()
                        ?? attachResult.Target.gameObject.AddComponent<GrappleChainEndpoint>();
            endpoint.Attach(runtime, localAttach);
        }
    }
}
