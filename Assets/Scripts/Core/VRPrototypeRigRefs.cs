using Unity.XR.CoreUtils;
using UnityEngine;

/// <summary>
/// Single source of truth for VR rig component references.
/// Lives on the XR Origin GameObject. Assign all fields in the Inspector,
/// or run GoldenHook → 4. Wire Grapple onto XRI Prefab.
///
/// NOTE: XRI 3.3.1 starter prefab uses XRControllerActionBasedMapping +
/// TrackedPoseDriver instead of ActionBasedController. We store plain
/// Transforms here — sufficient for all grapple systems.
/// </summary>
[DisallowMultipleComponent]
public class VRPrototypeRigRefs : MonoBehaviour
{
    [Header("XR Rig")]
    public XROrigin XROrigin;

    [Header("Controller root GameObjects")]
    public GameObject LeftControllerGO;
    public GameObject RightControllerGO;

    [Header("Hand Transforms — used as chain attach point (Attach Point child or controller root)")]
    public Transform LeftHandTransform;
    public Transform RightHandTransform;

    public GameObject GetControllerGO(Hand hand) =>
        hand == Hand.Left ? LeftControllerGO : RightControllerGO;

    public Transform GetHandTransform(Hand hand) =>
        hand == Hand.Left ? LeftHandTransform : RightHandTransform;

    void OnValidate()
    {
        if (XROrigin == null)
            XROrigin = GetComponent<XROrigin>();
    }
}
