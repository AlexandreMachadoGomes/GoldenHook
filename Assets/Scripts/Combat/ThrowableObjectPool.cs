using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simple pool for arena throwable objects. Pre-spawns instances and recycles them
/// via SetActive to avoid runtime Instantiate/Destroy costs.
///
/// MonsterThrower calls Get() to retrieve a ready object.
/// ThrowablePhysicsObject.Recycle() calls Return() when done.
/// </summary>
public class ThrowableObjectPool : MonoBehaviour
{
    [SerializeField] GameObject throwablePrefab;
    [SerializeField] int        preloadCount = 5;

    readonly Queue<ThrowablePhysicsObject> _available = new();

    void Start()
    {
        for (int i = 0; i < preloadCount; i++)
            _available.Enqueue(CreateNew());
    }

    /// <summary>Returns a ready-to-launch object. Spawns a new one if pool is empty.</summary>
    public ThrowablePhysicsObject Get()
    {
        var obj = _available.Count > 0 ? _available.Dequeue() : CreateNew();
        obj.gameObject.SetActive(true);
        return obj;
    }

    /// <summary>Called by ThrowablePhysicsObject.Recycle().</summary>
    public void Return(ThrowablePhysicsObject obj)
    {
        obj.transform.SetParent(transform, worldPositionStays: false);
        obj.transform.localPosition = Vector3.zero;
        obj.gameObject.SetActive(false);
        _available.Enqueue(obj);
    }

    ThrowablePhysicsObject CreateNew()
    {
        var go  = Instantiate(throwablePrefab, transform);
        go.name = $"Throwable_{_available.Count + 1:00}";
        go.SetActive(false);
        return go.GetComponent<ThrowablePhysicsObject>();
    }
}
