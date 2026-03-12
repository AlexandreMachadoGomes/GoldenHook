using UnityEngine;

/// <summary>
/// All chain tuning lives here. Nothing is hardcoded in runtime scripts.
/// Create an instance via Assets > Create > GoldenHook > GrappleChainConfig.
/// Drop the asset into GrappleLauncher.chainConfig in the Inspector.
/// </summary>
[CreateAssetMenu(menuName = "GoldenHook/GrappleChainConfig", fileName = "GrappleChainConfig")]
public class GrappleChainConfig : ScriptableObject
{
    [Header("Structure")]
    [Tooltip("Maximum link count. Fewer links may be used for short-range attachments.")]
    public int maxLinkCount = 10;
    [Tooltip("World-space distance between link centers. Determines visual density.")]
    public float linkSpacing = 0.15f;

    [Header("Rigidbody")]
    public float linkMass         = 0.05f;
    public float linkDrag         = 2f;
    public float linkAngularDrag  = 5f;
    public int   solverIterations         = 16;
    public int   solverVelocityIterations = 8;

    [Header("Joint")]
    [Tooltip("Angular damper applied to all link joints. Higher = stiffer, less oscillation.")]
    public float angularDamper = 10f;

    [Header("Visual (placeholder)")]
    [Tooltip("World-space radius of the sphere primitive used for each link.")]
    public float linkRadius = 0.02f;
}
