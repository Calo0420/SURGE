using UnityEngine;

public sealed class VFXManager : MonoBehaviour
{
    public static VFXManager Instance { get; private set; }

    [Header("Prefabs")]
    [SerializeField] private VFXFrameAnimator clearBurstPrefab;
    [SerializeField] private VFXSparkStreakController sparkStreakPrefab;

    [Header("Palette")]
    [SerializeField] private SurgePalette palette;

    [Header("Pool")]
    [SerializeField] private int initialBurstPoolSize = 12;

    [Header("Tuning")]
    [SerializeField] private float burstHdrIntensity = 2.0f;

    private SimpleVFXPool burstPool;

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
