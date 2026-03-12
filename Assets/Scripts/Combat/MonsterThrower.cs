using UnityEngine;

/// <summary>
/// Throws pooled arena objects at the player at regular intervals.
/// Aims at the player's current position with optional random spread.
/// </summary>
public class MonsterThrower : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ThrowableObjectPool pool;
    [SerializeField] Transform           throwSpawnPoint;
    [SerializeField] Transform           playerTarget;   // XROrigin transform

    [Header("Tuning")]
    [SerializeField] float throwInterval         = 3f;   // seconds between throws
    [SerializeField] float throwIntervalVariance = 1f;   // ± random added to interval
    [SerializeField] float throwSpeed            = 7f;   // m/s
    [SerializeField] float spread                = 0.3f; // random aim spread radius (m)

    float _nextThrowTime;

    void Start() => ScheduleNextThrow();

    void Update()
    {
        if (Time.time < _nextThrowTime) return;
        Throw();
        ScheduleNextThrow();
    }

    void Throw()
    {
        if (pool == null || throwSpawnPoint == null || playerTarget == null) return;

        var obj = pool.Get();
        obj.transform.position = throwSpawnPoint.position;
        obj.transform.rotation = Random.rotation;

        // Aim at player with random spread so throws aren't perfectly accurate.
        Vector3 target    = playerTarget.position + Random.insideUnitSphere * spread;
        Vector3 direction = (target - throwSpawnPoint.position).normalized;

        obj.Launch(pool, direction * throwSpeed);
    }

    void ScheduleNextThrow()
    {
        float variance  = Random.Range(-throwIntervalVariance, throwIntervalVariance);
        _nextThrowTime  = Time.time + throwInterval + variance;
    }
}
