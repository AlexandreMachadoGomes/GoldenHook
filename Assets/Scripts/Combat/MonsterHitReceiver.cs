using System;
using UnityEngine;

/// <summary>
/// Detects valid player-scored hits on the monster placeholder.
/// A hit is valid only if the colliding object is a ThrowablePhysicsObject
/// in the ReleasedByPlayer state within its hit window.
/// </summary>
public class MonsterHitReceiver : MonoBehaviour
{
    [Header("Tuning")]
    [SerializeField] int hitsToKill = 5;

    public int  HitCount  { get; private set; }
    public bool IsDead    { get; private set; }

    /// <summary>Fired on each valid hit. int param = total hit count.</summary>
    public event Action<int> OnValidHit;

    /// <summary>Fired when hit count reaches hitsToKill.</summary>
    public event Action OnDeath;

    void OnCollisionEnter(Collision collision)
    {
        if (IsDead) return;

        var throwable = collision.gameObject.GetComponent<ThrowablePhysicsObject>();
        if (throwable == null || !throwable.IsValidPlayerProjectile()) return;

        throwable.Recycle();

        HitCount++;
        OnValidHit?.Invoke(HitCount);
        GrappleRuntimeEvents.RaiseMonsterHit(HitCount);
        Debug.Log($"[MonsterHitReceiver] Hit {HitCount}/{hitsToKill}!");

        if (HitCount >= hitsToKill)
        {
            IsDead = true;
            OnDeath?.Invoke();
            GrappleRuntimeEvents.RaiseMonsterDefeated();
            Debug.Log("[MonsterHitReceiver] Monster defeated!");
        }
    }
}
