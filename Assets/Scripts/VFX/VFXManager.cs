using UnityEngine;

public sealed class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private VFXFrameAnimator clearBurstPrefab;
    [SerializeField] private VFXSparkStreakController sparkStreakPrefab;
    [SerializeField] private VFXShieldController shieldPrefab;
    [SerializeField] private VFXElectricityController electricityPrefab;
    [SerializeField] private VFXAmbientController ambientPrefab;

    [Header("High Voltage VFX")]
    [SerializeField] private GameObject shockwavePrefab;
    [SerializeField] private GameObject lightningPrefab;
    [SerializeField] private GameObject implosionPrefab;

    [Header("Palette")]
    [SerializeField] private SurgePalette palette;

    [Header("Pool")]
    [SerializeField] private int initialBurstPoolSize = 12;
    [SerializeField] private int initialShieldPoolSize = 4;
    [SerializeField] private int initialElectricityPoolSize = 8;

    [Header("Tuning")]
    [SerializeField] private float burstHdrIntensity = 2.0f;

    private SimpleVFXPool burstPool;
    private CallbackVFXPool<VFXShieldController> shieldPool;
    private CallbackVFXPool<VFXElectricityController> electricityPool;
    private VFXAmbientController ambientInstance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (clearBurstPrefab == null)
        {
            Debug.LogError($"{nameof(VFXManager)} requires a clearBurstPrefab reference.", this);
            return;
        }

        burstPool = new SimpleVFXPool(clearBurstPrefab, initialBurstPoolSize, transform);

        if (shieldPrefab != null)
        {
            shieldPool = new CallbackVFXPool<VFXShieldController>(
                shieldPrefab, initialShieldPoolSize, transform,
                (instance, cb) => instance.InitializePool(cb));
        }
        else
        {
            Debug.LogWarning($"{nameof(VFXManager)} has no shieldPrefab assigned; Surge/Purge/Combo VFX will be skipped.", this);
        }

        if (electricityPrefab != null)
        {
            electricityPool = new CallbackVFXPool<VFXElectricityController>(
                electricityPrefab, initialElectricityPoolSize, transform,
                (instance, cb) => instance.InitializePool(cb));
        }
        else
        {
            Debug.LogWarning($"{nameof(VFXManager)} has no electricityPrefab assigned; chain link VFX will be skipped.", this);
        }

        if (ambientPrefab != null)
        {
            ambientInstance = Instantiate(ambientPrefab, transform);
            ambientInstance.Initialize(palette != null ? palette.hudPrimary : Color.white);
            ambientInstance.StartAmbient();
        }
        else
        {
            Debug.LogWarning($"{nameof(VFXManager)} has no ambientPrefab assigned; ambient background VFX will be skipped.", this);
        }

        // Auto-load high-voltage VFX prefabs from Resources/VFX if not assigned
        if (shockwavePrefab == null)
            shockwavePrefab = Resources.Load<GameObject>("VFX/vfx_Shockwave_01");
        if (lightningPrefab == null)
            lightningPrefab = Resources.Load<GameObject>("VFX/vfx_Lightning_01");
        if (implosionPrefab == null)
            implosionPrefab = Resources.Load<GameObject>("VFX/vfx_Implosion_01");
    }

    /// <summary>High-voltage lightning burst on node clears (especially 5+ chains)</summary>
    public void PlayLightningBurst(Vector3 worldPosition, SurgeVfxColor colorId, float scale = 0.65f)
    {
        if (lightningPrefab == null) return;
        GameObject go = Instantiate(lightningPrefab, worldPosition, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * scale;
        Color col = GetPaletteColor(colorId);
        ApplyVfxColor(go, col);
        Destroy(go, 1.2f);
    }

    /// <summary>Expanding neon shockwave ring</summary>
    public void PlayShockwave(Vector3 worldPosition, SurgeVfxColor colorId, float scale = 0.75f)
    {
        if (shockwavePrefab == null) return;
        GameObject go = Instantiate(shockwavePrefab, worldPosition, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * scale;
        Color col = GetPaletteColor(colorId);
        ApplyVfxColor(go, col);
        Destroy(go, 1.5f);
    }

    /// <summary>Detonates full EMP Implosion for Color Purge</summary>
    public void PlayImplosion(Vector3 worldPosition, SurgeVfxColor colorId, float scale = 1.0f)
    {
        if (implosionPrefab == null) return;
        GameObject go = Instantiate(implosionPrefab, worldPosition, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * scale;
        Color col = GetPaletteColor(colorId);
        ApplyVfxColor(go, col);
        Destroy(go, 2.0f);
    }

    private void ApplyVfxColor(GameObject root, Color color)
    {
        var systems = root.GetComponentsInChildren<ParticleSystem>();
        foreach (var ps in systems)
        {
            var main = ps.main;
            main.startColor = color;
        }
    }

    public void SpawnClearBurst(Vector3 worldPosition, SurgeVfxColor colorId)
    {
        if (burstPool == null)
        {
            Debug.LogError($"{nameof(VFXManager)} pool was not initialized.", this);
            return;
        }

        VFXFrameAnimator burst = burstPool.Get();
        if (burst == null)
        {
            Debug.LogError($"{nameof(VFXManager)} failed to get a burst instance from the pool.", this);
            return;
        }

        burst.transform.position = worldPosition;
        burst.Play(GetPaletteColor(colorId), burstHdrIntensity);
    }

    public void EmitSparkStreak(Vector3 worldPosition, Vector2 direction, SurgeVfxColor colorId, int count = 12)
    {
        if (sparkStreakPrefab == null)
        {
            Debug.LogError($"{nameof(VFXManager)} requires a sparkStreakPrefab reference.", this);
            return;
        }

        VFXSparkStreakController streak = Instantiate(sparkStreakPrefab, worldPosition, Quaternion.identity, transform);
        streak.EmitSparks(worldPosition, direction, GetPaletteColor(colorId), count);
        Destroy(streak.gameObject, 2f);
    }


    /// <summary>Surge Mode activation: full-scale Shield pulse in the palette's accent (pink) color.</summary>
    public void PlaySurgeActivation(Vector3 worldPosition)
    {
        PlayShield(worldPosition, SurgeVfxColor.NeonPink, localScale: 1f, simSpeed: 1f);
    }

    /// <summary>Purge Freeze: mid-scale Shield flash in blue.</summary>
    public void PlayPurgeFreeze(Vector3 worldPosition)
    {
        PlayShield(worldPosition, SurgeVfxColor.NeonBlue, localScale: 0.6f, simSpeed: 1f);
    }

    /// <summary>Combo multiplier pop: small, fast Shield flash in yellow.</summary>
    public void PlayComboPop(Vector3 worldPosition)
    {
        PlayShield(worldPosition, SurgeVfxColor.NeonYellow, localScale: 0.3f, simSpeed: 2f);
    }

    private void PlayShield(Vector3 worldPosition, SurgeVfxColor colorId, float localScale, float simSpeed)
    {
        if (shieldPool == null)
        {
            Debug.LogError($"{nameof(VFXManager)} shield pool was not initialized.", this);
            return;
        }

        VFXShieldController shield = shieldPool.Get();
        if (shield == null)
        {
            Debug.LogError($"{nameof(VFXManager)} failed to get a shield instance from the pool.", this);
            return;
        }

        shield.transform.position = worldPosition;
        shield.Play(GetPaletteColor(colorId), localScale: localScale, simSpeed: simSpeed);
    }

    /// <summary>Chain/cascade link feedback: electricity arc between two adjacent nodes.</summary>
    public void PlayChainLink(Vector3 fromPosition, Vector3 toPosition, SurgeVfxColor colorId)
    {
        if (electricityPool == null)
        {
            Debug.LogError($"{nameof(VFXManager)} electricity pool was not initialized.", this);
            return;
        }

        VFXElectricityController arc = electricityPool.Get();
        if (arc == null)
        {
            Debug.LogError($"{nameof(VFXManager)} failed to get an electricity instance from the pool.", this);
            return;
        }

        arc.Play(fromPosition, toPosition, colorId);
    }

    /// <summary>Adjust ambient background VFX intensity (e.g. dimmed during Surge Mode overlay).</summary>
    public void SetAmbientIntensity(float factor)
    {
        ambientInstance?.SetIntensity(factor);
    }


    private Color GetPaletteColor(SurgeVfxColor colorId)
    {
        if (palette == null)
        {
            Debug.LogError($"{nameof(VFXManager)} is missing palette reference. Defaulting to magenta.", this);
            return Color.magenta;
        }

        return colorId switch
        {
            SurgeVfxColor.NeonBlue => palette.neonBlue,
            SurgeVfxColor.NeonPink => palette.neonPink,
            SurgeVfxColor.NeonGreen => palette.neonGreen,
            SurgeVfxColor.NeonYellow => palette.neonYellow,
            SurgeVfxColor.NeonPurple => palette.neonPurple,
            _ => palette.neonBlue,
        };
    }
}
