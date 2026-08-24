using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a Gabriel Aguiar vfx_Shield_01 instance.
/// Retints via ParticleSystem.main.startColor + colorOverLifetime (no material instancing).
/// Used for: Surge Mode activation, Purge Freeze, Combo Multiplier pop.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class VFXShieldController : MonoBehaviour
{
    [SerializeField] private ParticleSystem[] childSystems;

    private Coroutine lifetimeRoutine;
    private SimpleVFXPool ownerPool;
    private VFXFrameAnimator dummyForPool; // pool compatibility placeholder

    private void Awake()
    {
        if (childSystems == null || childSystems.Length == 0)
        {
            childSystems = GetComponentsInChildren<ParticleSystem>();
        }
    }

    public void InitializePool(System.Action<VFXShieldController> returnCallback)
    {
        ReturnToPoolCallback = returnCallback;
    }

    public System.Action<VFXShieldController> ReturnToPoolCallback { get; private set; }

    /// <summary>
    /// Play the shield effect with the given palette color, scale, and simulation speed.
    /// </summary>
    /// <param name="color">Palette color (non-HDR, will be multiplied by hdrIntensity).</param>
    /// <param name="hdrIntensity">HDR multiplier for Bloom pickup.</param>
    /// <param name="localScale">Effect scale (0.3 for combo pop, 0.6 for purge, 1.0 for surge mode).</param>
    /// <param name="simSpeed">Simulation speed (2.0 for fast flash, 1.0 for normal, 0.5 for sustained).</param>
    /// <param name="duration">How long before returning to pool. 0 = use particle system duration.</param>
    public void Play(Color color, float hdrIntensity = 1.5f, float localScale = 1f, float simSpeed = 1f, float duration = 0f)
    {
        if (childSystems == null || childSystems.Length == 0)
        {
            Debug.LogError($"{nameof(VFXShieldController)} on '{name}' has no particle systems.", this);
            ReturnToPool();
            return;
        }

        transform.localScale = Vector3.one * localScale;

        Color hdrColor = color * hdrIntensity;
        hdrColor.a = 1f;

        Color fadeColor = color * (hdrIntensity * 0.3f);
        fadeColor.a = 0f;

        foreach (ParticleSystem ps in childSystems)
        {
            var main = ps.main;
            main.startColor = hdrColor;
            main.simulationSpeed = simSpeed;

            var col = ps.colorOverLifetime;
            if (col.enabled)
            {
                Gradient grad = new();
                grad.SetKeys(
                    new GradientColorKey[] {
                        new(hdrColor, 0f),
                        new(fadeColor, 1f)
                    },
                    new GradientAlphaKey[] {
                        new(1f, 0f),
                        new(0f, 1f)
                    }
                );
                col.color = grad;
            }

            ps.Clear(true);
            ps.Play(true);
        }

        float effectDuration = duration > 0f ? duration : GetLongestDuration();

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
        }
        lifetimeRoutine = StartCoroutine(ReturnAfterDelay(effectDuration));
    }

    public void Stop()
    {
        foreach (ParticleSystem ps in childSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }

        ReturnToPool();
    }

    private float GetLongestDuration()
    {
        float longest = 1f;
        foreach (ParticleSystem ps in childSystems)
        {
            float d = ps.main.duration + ps.main.startLifetime.constantMax;
            if (d > longest) longest = d;
        }
        return longest;
    }

    private IEnumerator ReturnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        lifetimeRoutine = null;
        ReturnToPool();
    }

    private void ReturnToPool()
    {
        foreach (ParticleSystem ps in childSystems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        if (ReturnToPoolCallback != null)
        {
            ReturnToPoolCallback.Invoke(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }
    }
}
