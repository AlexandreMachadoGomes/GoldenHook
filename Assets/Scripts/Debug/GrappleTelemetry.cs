using UnityEngine;

/// <summary>
/// Subscribes to GrappleRuntimeEvents and logs timestamped session stats.
/// Also exposes running counters readable in the Inspector during play mode.
///
/// Add to any scene GameObject. Zero configuration required.
/// </summary>
public class GrappleTelemetry : MonoBehaviour
{
    [Header("Live Stats (read-only during play)")]
    public int   ShotsFired;
    public int   Hits;
    public int   Misses;
    public int   Attaches;
    public int   Releases;
    public int   MonsterHits;
    public float SessionTime;

    void OnEnable()
    {
        GrappleRuntimeEvents.OnHookFired      += OnHookFired;
        GrappleRuntimeEvents.OnHookAttached   += OnHookAttached;
        GrappleRuntimeEvents.OnHookMissed     += OnHookMissed;
        GrappleRuntimeEvents.OnChainReleased  += OnChainReleased;
        GrappleRuntimeEvents.OnMonsterHit     += OnMonsterHit;
        GrappleRuntimeEvents.OnMonsterDefeated+= OnMonsterDefeated;
    }

    void OnDisable()
    {
        GrappleRuntimeEvents.OnHookFired      -= OnHookFired;
        GrappleRuntimeEvents.OnHookAttached   -= OnHookAttached;
        GrappleRuntimeEvents.OnHookMissed     -= OnHookMissed;
        GrappleRuntimeEvents.OnChainReleased  -= OnChainReleased;
        GrappleRuntimeEvents.OnMonsterHit     -= OnMonsterHit;
        GrappleRuntimeEvents.OnMonsterDefeated-= OnMonsterDefeated;
    }

    void Update() => SessionTime = Time.time;

    void OnHookFired(Hand hand)
    {
        ShotsFired++;
        Debug.Log($"[Telemetry] [{Time.time:F2}s] Hook fired — {hand} hand | Shots: {ShotsFired}");
    }

    void OnHookAttached(Hand hand, Vector3 worldPos)
    {
        Hits++;
        Attaches++;
        Debug.Log($"[Telemetry] [{Time.time:F2}s] Hook attached — {hand} hand @ {worldPos:F2} | Hits: {Hits}");
    }

    void OnHookMissed(Hand hand)
    {
        Misses++;
        Debug.Log($"[Telemetry] [{Time.time:F2}s] Hook missed — {hand} hand | Misses: {Misses}");
    }

    void OnChainReleased(Hand hand)
    {
        Releases++;
        Debug.Log($"[Telemetry] [{Time.time:F2}s] Chain released — {hand} hand | Releases: {Releases}");
    }

    void OnMonsterHit(int hitCount)
    {
        MonsterHits = hitCount;
        Debug.Log($"[Telemetry] [{Time.time:F2}s] Monster hit! Total hits: {hitCount}");
    }

    void OnMonsterDefeated()
    {
        float accuracy = ShotsFired > 0 ? (Hits / (float)ShotsFired) * 100f : 0f;
        Debug.Log($"[Telemetry] [{Time.time:F2}s] *** MONSTER DEFEATED *** | " +
                  $"Shots: {ShotsFired}, Hits: {Hits}, Misses: {Misses}, " +
                  $"Accuracy: {accuracy:F1}%, Time: {SessionTime:F1}s");
    }
}
