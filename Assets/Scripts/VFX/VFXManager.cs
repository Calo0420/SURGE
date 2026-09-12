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
    [SerializeField] private GameObject impactPrefab;
    [SerializeField] private GameObject explosionPrefab;

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
        if (impactPrefab == null)
            impactPrefab = Resources.Load<GameObject>("VFX/vfx_Impact_01");
        if (explosionPrefab == null)
            explosionPrefab = Resources.Load<GameObject>("VFX/vfx_Explosion_02");
    }

    /// <summary>High-voltage electrical impact burst on individual node clears</summary>
    public void PlayImpactBurst(Vector3 worldPosition, SurgeVfxColor colorId, float scale = 0.45f)
    {
        if (impactPrefab == null) return;
        GameObject go = Instantiate(impactPrefab, worldPosition, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * scale;
        Color col = GetPaletteColor(colorId) * burstHdrIntensity;
        col.a = 1f;
        ApplyVfxColor(go, col);
        Destroy(go, 0.8f);
    }

    /// <summary>High-energy plasma explosion on 3+ match completions</summary>
    public void PlayExplosion(Vector3 worldPosition, SurgeVfxColor colorId, float scale = 0.6f)
    {
        if (explosionPrefab == null) return;
        GameObject go = Instantiate(explosionPrefab, worldPosition, Quaternion.identity, transform);
        go.transform.localScale = Vector3.one * scale;
        Color col = GetPaletteColor(colorId) * burstHdrIntensity;
        col.a = 1f;
        ApplyVfxColor(go, col);
        Destroy(go, 1.6f);
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
        // High-voltage electric impact burst at the capacitor terminal
        PlayImpactBurst(worldPosition, colorId, 0.55f);
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
        PlayShield(worldPosition, SurgeVfxColor.NeonPink, localScale: 0.65f, simSpeed: 1.5f);
    }

    /// <summary>
    /// Crisp cyber-overdrive activation: ripples electric spark streaks along border rails
    /// and detonates a sleek outer frame shockwave without blocking the board.
    /// </summary>
    public void PlaySurgeOverdrive(Vector3 center, float halfExtent = 3.65f)
    {
        // 1. Sleek perimeter shockwave around the frame border
        PlayShockwave(center, SurgeVfxColor.NeonPink, 1.25f);

        // 2. High-speed electrical spark streaks racing across top and bottom rails
        Vector3 topRail = center + new Vector3(-halfExtent, halfExtent, 0f);
        Vector3 botRail = center + new Vector3(halfExtent, -halfExtent, 0f);
        EmitSparkStreak(topRail, Vector2.right, SurgeVfxColor.NeonPink, 16);
        EmitSparkStreak(botRail, Vector2.left, SurgeVfxColor.NeonPink, 16);

        // 3. Quick electric arcs at the frame corners
        Vector3 c1 = center + new Vector3(-halfExtent, halfExtent, 0f);
        Vector3 c2 = center + new Vector3(halfExtent, halfExtent, 0f);
        Vector3 c3 = center + new Vector3(halfExtent, -halfExtent, 0f);
        Vector3 c4 = center + new Vector3(-halfExtent, -halfExtent, 0f);

        PlayChainLink(c1, c2, SurgeVfxColor.NeonPink);
        PlayChainLink(c2, c3, SurgeVfxColor.NeonPink);
        PlayChainLink(c3, c4, SurgeVfxColor.NeonPink);
        PlayChainLink(c4, c1, SurgeVfxColor.NeonPink);
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
