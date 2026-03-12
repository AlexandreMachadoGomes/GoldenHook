using UnityEngine;

/// <summary>
/// Owns and configures the ConfigurableJoint on one chain link.
/// Exposes runtime update methods so Batch 4 (retraction/tension) can
/// stiffen or loosen joints without knowing Unity's joint API directly.
/// </summary>
public class GrappleChainJointDriver : MonoBehaviour
{
    ConfigurableJoint _joint;

    public ConfigurableJoint Joint => _joint;

    /// <summary>
    /// Call once immediately after AddComponent to build the joint.
    /// </summary>
    /// <param name="connectedBody">Rigidbody this link is chained to.</param>
    /// <param name="localAnchor">Joint pivot in this link's local space.</param>
    /// <param name="connectedAnchor">Joint pivot in connectedBody's local space.</param>
    public void Initialize(
        Rigidbody connectedBody,
        Vector3 localAnchor,
        Vector3 connectedAnchor,
        GrappleChainConfig config)
    {
        _joint = gameObject.AddComponent<ConfigurableJoint>();
        _joint.connectedBody = connectedBody;
        _joint.autoConfigureConnectedAnchor = false;
        _joint.anchor          = localAnchor;
        _joint.connectedAnchor = connectedAnchor;

        // Locked linear DOF → rigid link length (ball-and-socket chain)
        _joint.xMotion = ConfigurableJointMotion.Locked;
        _joint.yMotion = ConfigurableJointMotion.Locked;
        _joint.zMotion = ConfigurableJointMotion.Locked;

        // Free angular DOF → links can rotate in any direction
        _joint.angularXMotion = ConfigurableJointMotion.Free;
        _joint.angularYMotion = ConfigurableJointMotion.Free;
        _joint.angularZMotion = ConfigurableJointMotion.Free;

        ApplyAngularDamper(config.angularDamper);

        // Disable preprocessing for better stability on long chains
        _joint.enablePreprocessing = false;
        _joint.breakForce  = float.PositiveInfinity;
        _joint.breakTorque = float.PositiveInfinity;
    }

    /// <summary>
    /// Reconnects this joint to a different body without destroying and recreating it.
    /// Used by GrappleChainRuntime.RemovePlayerSideLink() to rewire link 1 → HandAnchorProvider.
    /// Unity supports changing connectedBody at runtime; the solver adjusts next step.
    /// </summary>
    public void Reconnect(Rigidbody newConnectedBody, Vector3 localAnchor, Vector3 connectedAnchor)
    {
        if (_joint == null) return;
        _joint.connectedBody   = newConnectedBody;
        _joint.anchor          = localAnchor;
        _joint.connectedAnchor = connectedAnchor;
    }

    /// <summary>Tune angular damping at runtime (Batch 4 retraction stiffening).</summary>
    public void SetAngularDamper(float damper) => ApplyAngularDamper(damper);

    void ApplyAngularDamper(float damper)
    {
        if (_joint == null) return;
        var drive = new JointDrive
        {
            positionSpring = 0f,
            positionDamper = damper,
            maximumForce   = float.MaxValue
        };
        _joint.angularXDrive  = drive;
        _joint.angularYZDrive = drive;
    }
}
