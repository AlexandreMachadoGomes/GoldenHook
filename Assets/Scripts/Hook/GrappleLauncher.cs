using UnityEngine;

/// <summary>
/// Spawns and manages the lifecycle of one hand's hook projectile and physics chain.
/// Bridges GrappleHandController intent → physical hook → chain spawn → chain teardown.
///
/// OWNERSHIP: hook lifecycle + chain spawn/destroy. Does NOT know about retraction
/// (that's GrappleRetractionMotor, Batch 4) or joint tuning (GrappleChainJointDriver).
///
/// PLACEMENT: Add to the same controller GameObject as GrappleHandController.
/// </summary>
public class GrappleLauncher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GrappleHandController handController;
    [SerializeField] HandAnchorProvider    anchorProvider;
    [SerializeField] VRPrototypeRigRefs    rigRefs;

    [Header("Prefab & Config")]
    [SerializeField] GameObject             hookPrefab;
    [SerializeField] GrappleChainConfig     chainConfig;
    [SerializeField] GrappleReleaseVelocityHelper releaseVelocityHelper;

    [Header("Tuning")]
    [SerializeField] float launchSpeed = 15f;

    // ---- Active state ----
    GrappleHookProjectile _activeHook;
    GrappleChainRuntime   _activeChain;

    public GrappleHookProjectile ActiveHook  => _activeHook;
    public GrappleChainRuntime   ActiveChain => _activeChain;

    // -----------------------------------------------------------------------

    void OnEnable()
    {
        if (handController == null) return;
        handController.OnFireRequested    += HandleFireRequested;
        handController.OnReleaseRequested += HandleReleaseRequested;
    }

    void OnDisable()
    {
        if (handController == null) return;
        handController.OnFireRequested    -= HandleFireRequested;
        handController.OnReleaseRequested -= HandleReleaseRequested;
    }

    // -----------------------------------------------------------------------

    void HandleFireRequested()
    {
        if (hookPrefab == null)
        {
            Debug.LogWarning($"[GrappleLauncher] hookPrefab not assigned on {gameObject.name}");
            return;
        }

        CleanupAll(); // defensive: clear any stale hook/chain

        var hookGO = Instantiate(hookPrefab);
        _activeHook = hookGO.GetComponent<GrappleHookProjectile>();

        if (_activeHook == null)
        {
            Debug.LogError("[GrappleLauncher] hookPrefab missing GrappleHookProjectile component.");
            Destroy(hookGO);
            return;
        }

        _activeHook.OnAttached += HandleHookAttached;
        _activeHook.OnMissed   += HandleHookMissed;
        _activeHook.OnDetached += HandleHookDetached;

        Transform palm = rigRefs.GetHandTransform(handController.Hand);
        _activeHook.Launch(palm.position, palm.forward * launchSpeed);

        GrappleRuntimeEvents.RaiseHookFired(handController.Hand);
    }

    void HandleReleaseRequested()
    {
        GrappleRuntimeEvents.RaiseChainReleased(handController.Hand);

        // Apply smoothed release velocity BEFORE chain is destroyed.
        releaseVelocityHelper?.ApplyRelease();

        _activeHook?.Detach();

        if (_activeChain != null)
        {
            _activeChain.Release();
            _activeChain = null;
        }
    }

    void HandleHookAttached(GrappleAttachResult result)
    {
        GrappleRuntimeEvents.RaiseHookAttached(handController.Hand, result.WorldAttachPoint);
        handController.SetState(GrappleState.Attached);

        if (chainConfig != null)
        {
            _activeChain = GrappleChainFactory.SpawnChain(anchorProvider, result, chainConfig);
            releaseVelocityHelper?.BeginTracking(_activeChain.TargetBody);
        }
        else
        {
            Debug.LogWarning($"[GrappleLauncher] chainConfig not assigned on {gameObject.name} — chain skipped.");
        }
    }

    void HandleHookMissed()
    {
        GrappleRuntimeEvents.RaiseHookMissed(handController.Hand);
        _activeHook = null; // hook destroyed itself
        handController.SetState(GrappleState.Idle);
    }

    void HandleHookDetached()
    {
        CleanupAll();
        handController.SetState(GrappleState.Idle);
    }

    void CleanupAll()
    {
        if (_activeHook != null)
        {
            _activeHook.OnAttached -= HandleHookAttached;
            _activeHook.OnMissed   -= HandleHookMissed;
            _activeHook.OnDetached -= HandleHookDetached;
            _activeHook = null;
        }

        if (_activeChain != null)
        {
            _activeChain.Release();
            _activeChain = null;
        }
    }
}
