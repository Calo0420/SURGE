// ============================================================================
// SURGE — SurgeVisualBridge
//
// The one file that is allowed to know about both halves of the project.
//
// WHY IT EXISTS AT ALL: Surge.Runtime is an assembly definition, and an asmdef
// cannot reference Unity's predefined assemblies. SurgePalette and VFXManager
// live in Assembly-CSharp, so BoardView can never see them directly no matter
// how the folders are arranged. This file sits in Assets/Surge/Bridge with no
// asmdef of its own, which puts it in Assembly-CSharp — where it can see the
// visual scripts, and can also see Surge.Runtime because that asmdef is
// autoReferenced. The dependency runs one way only: visuals depend on the
// engine seam, never the reverse.
//
// It translates twice:
//   SurgePalette  -> BoardView's colour table (cell byte 1..5 -> Color)
//   BoardDelta    -> VFXManager calls
// ============================================================================

using Surge.Runtime;
using SurgeCore;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class SurgeVisualBridge : MonoBehaviour
{
    [Header("Engine seam")]
    [SerializeField] private MatchDriver driver;
    [SerializeField] private BoardView boardView;
    [SerializeField] private BoardInputController boardInput;

    [Header("Visuals")]
    [SerializeField] private SurgePalette palette;
    [SerializeField] private VFXManager vfx;

    [Header("Match")]
    [Tooltip("Seed used until a real SkillzSeedSource is wired. See docs/DESIGN_LINEAGE.md.")]
    [SerializeField] private ulong devSeed = 12345;
    [SerializeField] private bool beginMatchOnStart = true;

    // Cell values are 1..NumColors; index 0 is the empty cell and is never
    // drawn. The order here must match SurgeVfxColor, since ToVfxColor below
    // converts by subtracting one.
    private Color[] BuildColorTable() => new[]
    {
        Color.clear,
        palette.neonBlue,
        palette.neonPink,
        palette.neonGreen,
        palette.neonYellow,
        palette.neonPurple
    };

    private static SurgeVfxColor ToVfxColor(byte cellValue) =>
        (SurgeVfxColor)Mathf.Clamp(cellValue - 1, 0, 4);

    private void Start()
    {
        if (driver == null || boardView == null || palette == null)
        {
            Debug.LogError($"{nameof(SurgeVisualBridge)} is missing a required reference " +
                           $"(driver/boardView/palette).", this);
            enabled = false;
            return;
        }

        if (beginMatchOnStart && !driver.MatchRunning)
        {
            if (SkillzCrossPlatform.IsMatchInProgress())
                driver.BeginMatch(new Surge.Skillz.SkillzSeedSource(devSeed));
            else
                driver.BeginMatch(new TestSeedSource(devSeed));
        }

        boardView.Bind(driver);
        boardView.SetPalette(BuildColorTable());

        if (SurgeSkillzMatchController.Instance == null)
        {
            GameObject skillzGo = new GameObject("SurgeSkillzMatchController");
            skillzGo.AddComponent<SurgeSkillzMatchController>();
        }

        if (HapticManager.Instance == null)
        {
            GameObject hapticGo = new GameObject("HapticManager");
            hapticGo.AddComponent<HapticManager>();
        }

        if (SurgeAudioManager.Instance == null)
        {
            GameObject audioGo = new GameObject("SurgeAudioManager");
            audioGo.AddComponent<SurgeAudioManager>();
        }

        if (GetComponent<SurgePathTrailRenderer>() == null)
            gameObject.AddComponent<SurgePathTrailRenderer>();

        if (boardInput != null) boardInput.PathCommitted += OnPathCommitted;
    }

    private void OnDestroy()
    {
        if (boardInput != null) boardInput.PathCommitted -= OnPathCommitted;
    }

    private void OnPathCommitted(ClearResult result)
    {
        // Push the committed clear through the view first: it diffs the board
        // and classifies every changed cell, which is what the effects below
        // are driven from.
        BoardDelta delta = boardView.ApplyClear(result);
        if (delta == null || vfx == null) return;

        // Cleared nodes burst in their own colour — read from the pre-clear
        // snapshot via the delta, since the cell already holds its new value.
        foreach (CellDelta d in delta.Changed)
        {
            if (!delta.Cleared.Contains(d.Index)) continue;
            vfx.SpawnClearBurst(boardView.WorldPositionOf(d.Index), ToVfxColor(d.From));
        }

        // Heavy Clear: 5+ nodes detonates lightning arcs and expanding shockwave
        if (result.Path != null && result.Path.Length >= 5)
        {
            Vector3 center = boardView.WorldPositionOf(result.Path[result.Path.Length / 2]);
            SurgeVfxColor clearCol = ToVfxColor(delta.Changed.Count > 0 ? delta.Changed[0].From : (byte)1);
            vfx.PlayLightningBurst(center, clearCol, 0.75f);
            vfx.PlayShockwave(center, clearCol, 0.9f);
        }

        // A spark streak along the path gives the clear a direction.
        if (result.Path != null && result.Path.Length >= 2)
        {
            int a = result.Path[0], b = result.Path[result.Path.Length - 1];
            Vector3 from = boardView.WorldPositionOf(a);
            Vector3 to = boardView.WorldPositionOf(b);
            vfx.EmitSparkStreak(from, ((Vector2)(to - from)).normalized,
                                ToVfxColor(driver.Engine.Board.Cells[b]));
        }

        // Purge freeze celebration (EMP Implosion + mega-shockwave in neon blue)
        if (result.Purge && result.Path != null && result.Path.Length > 0)
        {
            Vector3 center = boardView.WorldPositionOf(result.Path[result.Path.Length / 2]);
            vfx.PlayPurgeFreeze(center);
            vfx.PlayImplosion(center, SurgeVfxColor.NeonBlue, 1.1f);
            vfx.PlayShockwave(center, SurgeVfxColor.NeonBlue, 1.4f);
        }

        // Combo pop on multiplier chains
        if (result.ChainMult > 1 && result.Path != null && result.Path.Length > 0)
        {
            Vector3 lastPos = boardView.WorldPositionOf(result.Path[result.Path.Length - 1]);
            vfx.PlayComboPop(lastPos);
            vfx.PlayShockwave(lastPos, SurgeVfxColor.NeonYellow, 0.5f);
        }

        // Electricity arcs along cascading cells
        if (delta.Fell != null && delta.Fell.Count >= 2)
        {
            for (int i = 0; i < delta.Fell.Count - 1 && i < 4; i++)
            {
                int idxA = delta.Fell[i];
                int idxB = delta.Fell[i + 1];
                vfx.PlayChainLink(boardView.WorldPositionOf(idxA), boardView.WorldPositionOf(idxB),
                                  ToVfxColor(driver.Engine.Board.Cells[idxB]));
            }
        }
    }

    private bool _wasSurgeActive;

    private void Update()
    {
        if (driver == null || !driver.MatchRunning) return;

        bool isSurge = driver.SurgeActive;
        if (isSurge && !_wasSurgeActive)
        {
            if (vfx != null)
            {
                vfx.PlaySurgeActivation(transform.position);
                vfx.PlayShockwave(transform.position, SurgeVfxColor.NeonPink, 1.8f);
                vfx.PlayLightningBurst(transform.position, SurgeVfxColor.NeonPink, 1.2f);
            }

            if (SurgeAudioManager.Instance != null)
                SurgeAudioManager.Instance.PlaySurgeActivation();
        }
        _wasSurgeActive = isSurge;

        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.SetComboMultiplier(driver.Chain);
    }
}
