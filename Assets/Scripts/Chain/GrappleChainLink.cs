using UnityEngine;

/// <summary>
/// Marker component on each chain link GameObject.
/// Provides typed access to the Rigidbody and link index within the chain.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GrappleChainLink : MonoBehaviour
{
    /// <summary>0 = player-side end, N-1 = target-side end.</summary>
    public int Index { get; private set; }

    public Rigidbody Body { get; private set; }

    void Awake() => Body = GetComponent<Rigidbody>();

    public void Initialize(int index) => Index = index;
}
