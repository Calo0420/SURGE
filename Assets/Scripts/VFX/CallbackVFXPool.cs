using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generic pool for VFX controllers that expose InitializePool(Action&lt;T&gt; returnCallback),
/// the pattern shared by VFXShieldController and VFXElectricityController.
/// Mirrors SimpleVFXPool's warm/get/return behavior without duplicating it per-type.
/// </summary>
public sealed class CallbackVFXPool<T> where T : MonoBehaviour
{
    private readonly T prefab;
    private readonly Transform parent;
    private readonly Queue<T> pool = new();
    private readonly Action<T, Action<T>> bindInitializePool;

    public CallbackVFXPool(T prefab, int initialSize, Transform parent, Action<T, Action<T>> bindInitializePool)
    {
        this.prefab = prefab;
        this.parent = parent;
        this.bindInitializePool = bindInitializePool;

        if (this.prefab == null)
        {
            Debug.LogError($"{nameof(CallbackVFXPool<T>)} requires a valid prefab reference for {typeof(T).Name}.");
            return;
        }

        int warmCount = Mathf.Max(0, initialSize);
        for (int i = 0; i < warmCount; i++)
        {
            T instance = UnityEngine.Object.Instantiate(this.prefab, this.parent);
            this.bindInitializePool(instance, ReturnToPool);
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
    }

    public T Get()
    {
        if (prefab == null)
        {
            return null;
        }

        if (pool.Count > 0)
        {
            T reusedInstance = pool.Dequeue();
            reusedInstance.gameObject.SetActive(true);
            return reusedInstance;
        }

        T newInstance = UnityEngine.Object.Instantiate(prefab, parent);
        bindInitializePool(newInstance, ReturnToPool);
        return newInstance;
    }

    private void ReturnToPool(T instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.gameObject.SetActive(false);
        pool.Enqueue(instance);
    }
}
