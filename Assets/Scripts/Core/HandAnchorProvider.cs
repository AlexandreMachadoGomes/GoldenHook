using UnityEngine;

/// <summary>
/// Kinematic Rigidbody that follows the VR controller transform.
///
/// Why a Rigidbody: the chain's first joint needs a physics body to anchor to.
/// A plain Transform gives joint solvers nothing to push against.
///
/// PLACEMENT: Attach this to a child GameObject of the XR Origin's controller
/// rig, or as a sibling driven by VRPrototypeRigRefs. The Rigidbody is set
/// kinematic so it moves the chain's base without being pulled by it.
///
/// LAG NOTE: ActionBasedController updates the controller transform in Update().
/// FixedUpdate here reads one-frame-old position (~11ms at 90Hz). This is
/// acceptable for chain anchor in a prototype. For tighter tracking, swap
/// targetTransform for a direct InputAction read of devicePosition/deviceRotation.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class HandAnchorProvider : MonoBehaviour
{
    [SerializeField] VRPrototypeRigRefs rigRefs;
    [SerializeField] Hand hand;

    /// <summary>The kinematic body that chain joints attach to.</summary>
    public Rigidbody AnchorBody { get; private set; }

    /// <summary>World position of the anchor this physics step.</summary>
    public Vector3 AnchorPosition => AnchorBody.position;

    /// <summary>World rotation of the anchor this physics step.</summary>
    public Quaternion AnchorRotation => AnchorBody.rotation;

    void Awake()
    {
        AnchorBody = GetComponent<Rigidbody>();
        AnchorBody.isKinematic = true;
        AnchorBody.interpolation = RigidbodyInterpolation.Interpolate;
        AnchorBody.useGravity = false;
        // Collide with nothing — this body exists only for joint anchoring.
        AnchorBody.detectCollisions = false;
    }

    void FixedUpdate()
    {
        Transform target = rigRefs != null ? rigRefs.GetHandTransform(hand) : null;
        if (target == null) return;

        AnchorBody.MovePosition(target.position);
        AnchorBody.MoveRotation(target.rotation);
    }
}
