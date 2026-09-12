using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a Gabriel Aguiar vfx_Electricity_01 instance for chain/cascade link feedback.
/// Uses pre-baked URP materials (one per palette color) assigned via sharedMaterial at init.
/// No renderer.material calls at runtime — zero material leaks.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class VFXElectricityController : MonoBehaviour
{
    [Header("Pre-Baked Materials (one per palette color, assigned in prefab)")]
    [SerializeField] private Material matNeonBlue;
    [SerializeField] private Material matNeonPink;
    [SerializeField] private Material matNeonGreen;
    [SerializeField] private Material matNeonYellow;
    [SerializeField] private Material matNeonPurple;

    [Header("Tuning")]
    [SerializeField] private float startSize = 0.06f;
    [SerializeField] private float lifetime = 0.15f;
    [SerializeField] private int maxParticles = 60;
    [SerializeField] private float trailWidth = 0.20f;

    private ParticleSystem[] childSystems;
    private Coroutine lifetimeRoutine;

    public System.Action<VFXElectricityController> ReturnToPoolCallback { get; private set; }

    private void Awake()
    {
        childSystems = GetComponentsInChildren<ParticleSystem>();
    }

    public void InitializePool(System.Action<VFXElectricityController> returnCallback)
    {
        ReturnToPoolCallback = returnCallback;
    }

    /// <summary>
    /// Play the electricity arc between two points with the given palette color.
    /// </summary>
    public void Play(Vector3 fromPosition, Vector3 toPosition, SurgeVfxColor colorId, float duration = 0.5f)
    {
        if (childSystems == null || childSystems.Length == 0)
        {
            Debug.LogError($"{nameof(VFXElectricityController)} on '{name}' has no particle systems.", this);
            ReturnToPool();
            return;
        }

        // Position at midpoint, orient toward target
        Vector3 midpoint = (fromPosition + toPosition) * 0.5f;
        transform.position = midpoint;

        Vector3 dir = (toPosition - fromPosition).normalized;
        if (dir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
        }

        // Scale along the connection length
        float distance = Vector3.Distance(fromPosition, toPosition);
        transform.localScale = new Vector3(0.35f, distance * 0.5f, 0.35f);

        Material targetMat = GetMaterialForColor(colorId);
        Color targetColor = GetColorForId(colorId);

        foreach (ParticleSystem ps in childSystems)
        {
            var main = ps.main;
            main.startSize = startSize;
            main.startLifetime = lifetime;
            main.maxParticles = maxParticles;
            main.startColor = targetColor * 1.6f;

            var trails = ps.trails;
            if (trails.enabled)
            {
                trails.widthOverTrail = trailWidth;
                trails.inheritParticleColor = true;
            }

            // Assign material via sharedMaterial (no leak)
            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            if (renderer != null && targetMat != null)
            {
                renderer.sharedMaterial = targetMat;
                renderer.trailMaterial = targetMat;
            }

            ps.Clear(true);
            ps.Play(true);
        }

        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
        }
        lifetimeRoutine = StartCoroutine(ReturnAfterDelay(duration));
    }

    private Material GetMaterialForColor(SurgeVfxColor colorId)
    {
        return colorId switch
        {
            SurgeVfxColor.NeonBlue => matNeonBlue,
            SurgeVfxColor.NeonPink => matNeonPink,
            SurgeVfxColor.NeonGreen => matNeonGreen,
            SurgeVfxColor.NeonYellow => matNeonYellow,
            SurgeVfxColor.NeonPurple => matNeonPurple,
            _ => matNeonBlue,
        };
    }

    private Color GetColorForId(SurgeVfxColor colorId)
    {
        return colorId switch
        {
            SurgeVfxColor.NeonBlue => new Color(0.12f, 0.76f, 1f),
            SurgeVfxColor.NeonPink => new Color(1f, 0.2f, 0.624f),
            SurgeVfxColor.NeonGreen => new Color(0.2f, 1f, 0.45f),
            SurgeVfxColor.NeonYellow => new Color(1f, 0.867f, 0.15f),
            SurgeVfxColor.NeonPurple => new Color(0.64f, 0.35f, 1f),
            _ => new Color(0.12f, 0.76f, 1f),
        };
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
