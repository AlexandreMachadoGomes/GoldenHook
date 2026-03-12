using UnityEngine;

/// <summary>
/// Reads the constraint force on the player-side chain joint to estimate chain tension.
/// Exposed as CurrentTension for use by GrappleDetachController and debug displays.
///
/// High tension = chain is taut and under load (object resisting, retracting hard).
/// Near-zero tension = chain is slack (object moving toward player, or no attachment).
/// </summary>
public class GrappleTensionAnalyzer : MonoBehaviour
{
    [SerializeField] GrappleLauncher launcher;

    /// <summary>Magnitude of the constraint force on the player-side joint this FixedUpdate.</summary>
    public float CurrentTension { get; private set; }

    void FixedUpdate()
    {
        var chain = launcher?.ActiveChain;
        if (chain?.PlayerSideLink == null)
        {
            CurrentTension = 0f;
            return;
        }

        var driver = chain.PlayerSideLink.GetComponent<GrappleChainJointDriver>();
        CurrentTension = driver?.Joint != null
            ? driver.Joint.currentForce.magnitude
            : 0f;
    }
}
