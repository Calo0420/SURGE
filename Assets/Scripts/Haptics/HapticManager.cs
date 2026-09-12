// ============================================================================
// SURGE — HapticManager
//
// Mobile tactile feedback for Surge. Provides:
//   - PlayLight: subtle tactile tick on each node connected along swipe path
//   - PlayMedium: crisp punch on standard (3-4 node) clears
//   - PlayHeavy: deep THUD on 5+ node clears, Surge activation, & Purge freeze
// ============================================================================

using System.Runtime.InteropServices;
using UnityEngine;

[DisallowMultipleComponent]
public class HapticManager : MonoBehaviour
{
    public static HapticManager Instance { get; private set; }

    [SerializeField] private bool hapticsEnabled = true;

    public bool HapticsEnabled
    {
        get => hapticsEnabled;
        set => hapticsEnabled = value;
    }

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void _surge_TriggerImpactLight();

    [DllImport("__Internal")]
    private static extern void _surge_TriggerImpactMedium();

    [DllImport("__Internal")]
    private static extern void _surge_TriggerImpactHeavy();
#endif

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Subtle, crisp click when the finger crosses over a valid connecting node.
    /// </summary>
    public void PlayLight()
    {
        if (!hapticsEnabled) return;

#if UNITY_IOS && !UNITY_EDITOR
        try { _surge_TriggerImpactLight(); } catch { Handheld.Vibrate(); }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// Moderate punch for 3-node and 4-node clears.
    /// </summary>
    public void PlayMedium()
    {
        if (!hapticsEnabled) return;

#if UNITY_IOS && !UNITY_EDITOR
        try { _surge_TriggerImpactMedium(); } catch { Handheld.Vibrate(); }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }

    /// <summary>
    /// Heavy THUD for 5+ chain clears, Surge Mode activation, and Color Purge.
    /// </summary>
    public void PlayHeavy()
    {
        if (!hapticsEnabled) return;

#if UNITY_IOS && !UNITY_EDITOR
        try { _surge_TriggerImpactHeavy(); } catch { Handheld.Vibrate(); }
#elif UNITY_ANDROID && !UNITY_EDITOR
        Handheld.Vibrate();
#endif
    }
}
