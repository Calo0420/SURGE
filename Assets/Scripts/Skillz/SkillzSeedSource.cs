// ============================================================================
// SURGE — SkillzSeedSource
//
// Skillz deterministic seed provider.
// Fulfills the ISeedSource contract in SurgeCore using SkillzCrossPlatform.Random.
// Both players in a head-to-head match receive the exact same sequence.
// ============================================================================

using SurgeCore;
using UnityEngine;

namespace Surge.Skillz
{
    public sealed class SkillzSeedSource : ISeedSource
    {
        private readonly ulong _fallbackSeed;

        public SkillzSeedSource(ulong fallbackSeed = 12345UL)
        {
            _fallbackSeed = fallbackSeed;
        }

        public ulong DeriveMatchSeed()
        {
            try
            {
                float val = SkillzCrossPlatform.Random.Value();
                ulong seed = (ulong)Mathf.RoundToInt(val * int.MaxValue);
                Debug.Log($"[Surge] Derived Skillz seed: {seed} (from Skillz Random: {val})");
                return seed != 0 ? seed : _fallbackSeed;
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[Surge] SkillzCrossPlatform.Random failed ({ex.Message}), falling back to dev seed {_fallbackSeed}");
                return _fallbackSeed;
            }
        }
    }
}
