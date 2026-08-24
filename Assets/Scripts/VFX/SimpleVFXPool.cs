using System.Collections.Generic;
using UnityEngine;

public sealed class SimpleVFXPool
{
    private readonly VFXFrameAnimator prefab;
    private readonly Transform parent;
    private readonly Queue<VFXFrameAnimator> pool = new();

    public SimpleVFXPool(VFXFrameAnimator prefab, int initialSize, Transform parent)
    {
        this.prefab = prefab;
        this.parent = parent;

        if (this.prefab == null)
        {
            Debug.LogError($"{nameof(SimpleVFXPool)} requires a valid prefab reference.");
            return;
        }

        int warmCount = Mathf.Max(0, initialSize);
        for (int i = 0; i < warmCount; i++)
        {
            VFXFrameAnimator instance = Object.Instantiate(this.prefab, this.parent);
            instance.InitializePool(this);
            instance.gameObject.SetActive(false);
            pool.Enqueue(instance);
        }
    }

    public VFXFrameAnimator Get()
    {
        if (prefab == null)
        {
            return null;
        }

        if (pool.Count > 0)
        {
            VFXFrameAnimator reusedInstance = pool.Dequeue();
            reusedInstance.gameObject.SetActive(true);
            return reusedInstance;
        }

        VFXFrameAnimator newInstance = Object.Instantiate(prefab, parent);
        newInstance.InitializePool(this);
        return newInstance;
    }

    public void ReturnToPool(VFXFrameAnimator instance)
    {
        if (instance == null)
        {
            return;
        }

        instance.gameObject.SetActive(false);
        pool.Enqueue(instance);
    }
}
