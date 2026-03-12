using UnityEngine;

/// <summary>
/// Drives chain retraction in response to player input.
/// When the hand is in the Retracting state, accumulates retraction distance
/// and passes it to GrappleLengthController to remove chain links.
///
/// PLACEMENT: Same controller GameObject as GrappleLauncher and GrappleHandController.
/// </summary>
public class GrappleRetractionMotor : MonoBehaviour
{
    [SerializeField] GrappleHandController  handController;
    [SerializeField] GrappleLauncher        launcher;
    [SerializeField] GrappleLengthController lengthController;

    [Header("Tuning")]
    [SerializeField] float retractSpeed = 2f; // m/s — how fast the chain shortens

    void OnEnable()  => lengthController?.Reset();

    void FixedUpdate()
    {
        if (handController == null || launcher == null || lengthController == null) return;
        if (handController.State != GrappleState.Retracting) return;

        var chain = launcher.ActiveChain;
        if (chain == null) return;

        lengthController.AddRetraction(retractSpeed * Time.fixedDeltaTime, chain);
    }
}
