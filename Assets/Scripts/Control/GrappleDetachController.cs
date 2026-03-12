using UnityEngine;

/// <summary>
/// Triggers a force-detach when chain tension exceeds the configured maximum.
/// Manual release (trigger press while attached) is handled by GrappleHandController.
///
/// PLACEMENT: Same controller GameObject as GrappleHandController.
/// </summary>
public class GrappleDetachController : MonoBehaviour
{
    [SerializeField] GrappleHandController handController;
    [SerializeField] GrappleTensionAnalyzer tensionAnalyzer;

    [Header("Tuning")]
    [Tooltip("Chain auto-detaches if tension exceeds this value (Newtons). 0 = disabled.")]
    [SerializeField] float maxTension = 0f;

    void FixedUpdate()
    {
        if (handController == null) return;
        if (handController.State == GrappleState.Idle ||
            handController.State == GrappleState.Fired) return;

        if (maxTension > 0f &&
            tensionAnalyzer != null &&
            tensionAnalyzer.CurrentTension > maxTension)
        {
            handController.ForceRelease();
        }
    }
}
