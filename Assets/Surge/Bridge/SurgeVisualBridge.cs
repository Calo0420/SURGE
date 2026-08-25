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
            driver.BeginMatch(new TestSeedSource(devSeed));

        boardView.Bind(driver);
        boardView.SetPalette(BuildColorTable());

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

        // A spark streak along the path gives the clear a direction.
        if (result.Path != null && result.Path.Length >= 2)
        {
            int a = result.Path[0], b = result.Path[result.Path.Length - 1];
            Vector3 from = boardView.WorldPositionOf(a);
            Vector3 to = boardView.WorldPositionOf(b);
            vfx.EmitSparkStreak(from, ((Vector2)(to - from)).normalized,
                                ToVfxColor(driver.Engine.Board.Cells[b]));
        }

        // NOTE — the remaining moments (Surge activation, purge freeze, combo
        // pop, chain arcs, ambient) are intentionally not fired here.
        // VFXShieldController / VFXElectricityController / VFXAmbientController
        // exist, but VFXManager exposes no pooled entry point for them yet, so
        // there is nothing to call. Once that API lands, they hook up here:
        //   result.Purge      -> shield, neonBlue, 0.6 scale, 2x speed
        //   result.ChainMult  -> shield, neonYellow, 0.3 scale, 2.5x speed
        //   delta.Fell        -> electricity arcs between linked cells
        //   delta.Repaired    -> quiet cross-fade only, never a pop
    }
}
