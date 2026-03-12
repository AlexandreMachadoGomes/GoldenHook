using System;
using UnityEngine;

/// <summary>
/// Physics projectile that travels from the hand to the target.
///
/// Lifecycle:
///   Launch() → flies as a Rigidbody → OnCollisionEnter → GrappleAttachResolver
///   → if valid: Attach() — becomes kinematic, parents to target → fires OnAttached
///   → if hook travels beyond maxRange: Miss() → fires OnMissed → self-destructs
///   → Detach() called externally on release → fires OnDetached → self-destructs
///
/// The chain (Batch 3) attaches between the HandAnchorProvider and this transform.
/// The chain's target-side endpoint connects to the target's Rigidbody directly,
/// not to this hook — the hook is visual/positional only after attachment.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class GrappleHookProjectile : MonoBehaviour
{
    [Header("Flight")]
    [SerializeField] float maxRange = 20f;

    // ---- Events ----
    public event Action<GrappleAttachResult> OnAttached;
    public event Action                      OnMissed;
    public event Action                      OnDetached;

    // ---- State ----
    public bool               IsAttached   { get; private set; }
    public GrappleAttachResult AttachResult { get; private set; }

    Rigidbody _rb;
    Vector3   _launchPosition;

    // -----------------------------------------------------------------------

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // ContinuousDynamic prevents tunnelling through thin objects at high speed.
        _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        _rb.interpolation          = RigidbodyInterpolation.Interpolate;
    }

    /// <summary>Called by GrappleLauncher immediately after instantiation.</summary>
    public void Launch(Vector3 position, Vector3 velocity)
    {
        transform.position = position;
        _launchPosition    = position;
        _rb.isKinematic    = false;
        _rb.linearVelocity = velocity;

        // Orient the hook to face the travel direction.
        if (velocity != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
    }

    void Update()
    {
        if (IsAttached) return;

        if (Vector3.Distance(transform.position, _launchPosition) > maxRange)
            Miss();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (IsAttached) return;

        var result = GrappleAttachResolver.Resolve(collision);
        if (!result.IsValid)
        {
            // Hit something non-grappleable (wall, floor) — treat as a miss.
            Miss();
            return;
        }

        Attach(result);
    }

    void Attach(GrappleAttachResult result)
    {
        IsAttached   = true;
        AttachResult = result;

        // Stop physics. Switch to kinematic so the hook doesn't slide after embed.
        _rb.isKinematic = true;
        _rb.linearVelocity  = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        // Parent to the target so the hook moves with it visually.
        // The chain (Batch 3) will connect to result.TargetBody directly for force transfer.
        if (result.TargetBody != null)
            transform.SetParent(result.TargetBody.transform, worldPositionStays: true);

        OnAttached?.Invoke(result);
    }

    void Miss()
    {
        OnMissed?.Invoke();
        Destroy(gameObject);
    }

    /// <summary>Called by GrappleLauncher on release or cancel.</summary>
    public void Detach()
    {
        if (!IsAttached) return;

        transform.SetParent(null);
        OnDetached?.Invoke();
        Destroy(gameObject);
    }

    void OnDestroy()
    {
        // Clear event subscriptions so GrappleLauncher doesn't hold a stale reference.
        OnAttached = null;
        OnMissed   = null;
        OnDetached = null;
    }
}
