using System.Collections;
using UnityEngine;

/// <summary>
/// Automated chain stability tester. Programmatically fires the hook at the
/// Test Throwable, waits for attach, immediately retracts, then releases —
/// repeatedly. Watch for joint explosions, NaN positions, or link tunneling.
///
/// Right-click this component in the Inspector → "Run Stress Test" to start.
/// The test stops after <cycleCount> cycles or when you exit play mode.
///
/// Requires: GrappleLauncher + GrappleHandController wired on the same GameObject.
/// Wire testTarget to the Test Throwable or any GrappleTarget object.
/// </summary>
public class ChainStressTester : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GrappleLauncher      launcher;
    [SerializeField] GrappleHandController handController;
    [SerializeField] Transform            testTarget;

    [Header("Tuning")]
    [SerializeField] int   cycleCount         = 20;
    [SerializeField] float waitForAttach      = 1.5f;  // seconds to wait for hook to attach
    [SerializeField] float retractDuration    = 1.0f;  // seconds to hold retract
    [SerializeField] float cooldownBetween    = 0.5f;  // seconds between cycles

    [Header("Live Status (read-only)")]
    public int  CyclesCompleted;
    public int  AttachSuccesses;
    public int  AttachFailures;
    public bool IsRunning;

    [ContextMenu("Run Stress Test")]
    public void RunStressTest()
    {
        if (!Application.isPlaying)
        { Debug.LogWarning("[ChainStressTester] Must be in play mode."); return; }
        if (IsRunning)
        { Debug.LogWarning("[ChainStressTester] Already running."); return; }

        StartCoroutine(StressTestRoutine());
    }

    [ContextMenu("Stop Stress Test")]
    public void StopStressTest() => StopAllCoroutines();

    IEnumerator StressTestRoutine()
    {
        IsRunning        = true;
        CyclesCompleted  = 0;
        AttachSuccesses  = 0;
        AttachFailures   = 0;

        Debug.Log($"[ChainStressTester] Starting {cycleCount} cycles.");

        for (int i = 0; i < cycleCount; i++)
        {
            Debug.Log($"[ChainStressTester] Cycle {i + 1}/{cycleCount}");

            // Fire
            handController.SimulateFire();
            yield return new WaitForSeconds(waitForAttach);

            // Check attach
            if (launcher.ActiveChain != null)
            {
                AttachSuccesses++;
                Debug.Log($"[ChainStressTester] Cycle {i + 1}: ATTACHED. Links: {launcher.ActiveChain.Links.Length}");

                // Retract briefly
                handController.SimulateRetractStart();
                yield return new WaitForSeconds(retractDuration);
                handController.SimulateRetractStop();

                // Release
                handController.SimulateRelease();
            }
            else
            {
                AttachFailures++;
                Debug.LogWarning($"[ChainStressTester] Cycle {i + 1}: MISSED (no chain after {waitForAttach}s)");
            }

            yield return new WaitForSeconds(cooldownBetween);
            CyclesCompleted++;
        }

        IsRunning = false;
        Debug.Log($"[ChainStressTester] Done. Successes: {AttachSuccesses}, Failures: {AttachFailures}");
    }
}
