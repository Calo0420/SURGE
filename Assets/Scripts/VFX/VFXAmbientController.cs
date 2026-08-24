using UnityEngine;

/// <summary>
/// Controls ambient idle particles (Creepy Cat Effect_01).
/// Always-on background effect, dim hudPrimary color, slow movement.
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public sealed class VFXAmbientController : MonoBehaviour
{
    [Header("Config")]
    [SerializeField] private ParticleSystem ambientSystem;
    [SerializeField] private float dimFactor = 0.3f;

    private Color ambientColor;

    private void Awake()
    {
        if (ambientSystem == null)
        {
            ambientSystem = GetComponent<ParticleSystem>();
        }
    }

    /// <summary>
    /// Initialize with the hudPrimary color from the palette (dimmed for subtlety).
    /// </summary>
    public void Initialize(Color hudPrimaryColor)
    {
        if (ambientSystem == null)
        {
            Debug.LogError($"{nameof(VFXAmbientController)} on '{name}' has no particle system.", this);
            return;
        }

        ambientColor = hudPrimaryColor * dimFactor;
        ambientColor.a = 0.4f;

        var main = ambientSystem.main;
        main.startColor = ambientColor;
        main.simulationSpeed = 0.4f;
    }

    public void StartAmbient()
    {
        if (ambientSystem == null) return;

        if (!ambientSystem.isPlaying)
        {
            ambientSystem.Play(true);
        }
    }

    public void StopAmbient()
    {
        if (ambientSystem == null) return;

        ambientSystem.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }

    public void SetIntensity(float factor)
    {
        if (ambientSystem == null) return;

        dimFactor = Mathf.Clamp01(factor);
        var main = ambientSystem.main;
        Color col = ambientColor;
        col.a = factor;
        main.startColor = col;
    }
}
