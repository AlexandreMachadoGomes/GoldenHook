using UnityEngine;

/// <summary>
/// Polls live chain physics data each frame and surfaces it in the Inspector
/// so you can read tension, link count, and target velocity without a custom editor.
///
/// Add to one controller GameObject (or a shared debug host).
/// Wire the Launcher field to one of the GrappleLaunchers.
/// </summary>
public class GrapplePhysicsProfile : MonoBehaviour
{
    [Header("Reference")]
    [SerializeField] GrappleLauncher launcher;
    [SerializeField] GrappleTensionAnalyzer tensionAnalyzer;

    [Header("Live Chain Data (play mode)")]
    public int   ActiveLinkCount;
    public float CurrentTensionN;
    public float TargetSpeed;       // m/s — speed of the hooked object
    public float TargetDistance;    // distance from player anchor to target

    void Update()
    {
        if (launcher == null) return;

        var chain = launcher.ActiveChain;

        ActiveLinkCount = chain != null ? chain.Links.Length : 0;
        CurrentTensionN = tensionAnalyzer != null ? tensionAnalyzer.CurrentTension : 0f;

        if (chain != null && chain.TargetBody != null && chain.PlayerAnchor != null)
        {
            TargetSpeed    = chain.TargetBody.linearVelocity.magnitude;
            TargetDistance = Vector3.Distance(chain.PlayerAnchor.transform.position,
                                              chain.TargetBody.transform.position);
        }
        else
        {
            TargetSpeed    = 0f;
            TargetDistance = 0f;
        }
    }
}
