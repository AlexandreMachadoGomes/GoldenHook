using UnityEngine;

/// <summary>
/// Marks a GameObject as grappleable and defines attach policy.
/// Add this to any object the hook should be able to embed in.
///
/// For the MVP (thrown spheres/cubes), IsGrappleable = true and AttachSocket = null
/// (uses the raw collision contact point). Named sockets can be added later.
/// </summary>
public class GrappleTarget : MonoBehaviour
{
    [Tooltip("Can the hook currently attach to this object?")]
    public bool IsGrappleable = true;

    [Tooltip("Optional fixed attach point. If null, uses the raw collision contact point.")]
    public Transform AttachSocket;

    /// <summary>
    /// Returns the world-space attach point given a raw hit position.
    /// If a socket is defined it takes priority over the hit point.
    /// </summary>
    public Vector3 GetAttachPoint(Vector3 hitPoint) =>
        AttachSocket != null ? AttachSocket.position : hitPoint;
}
