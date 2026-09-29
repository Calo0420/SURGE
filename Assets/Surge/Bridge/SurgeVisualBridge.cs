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
using UnityEngine.Rendering.Universal;

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
    [SerializeField] private SurgeHUDController hud;

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

    private void Awake()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
            }
        }
    }

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
        if (hud == null)
            hud = FindAnyObjectByType<SurgeHUDController>();

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

        if (FindAnyObjectByType<SurgeTutorialController>() == null)
        {
            GameObject tutGo = new GameObject("SurgeTutorialController");
            tutGo.AddComponent<SurgeTutorialController>();
        }

        var trail = GetComponent<SurgePathTrailRenderer>();
        if (trail == null)
            trail = gameObject.AddComponent<SurgePathTrailRenderer>();
        trail.Initialize(boardInput, boardView, driver, palette);

        if (boardInput != null)
        {
            boardInput.PathChanged += OnPathChanged;
            boardInput.PathCommitted += OnPathCommitted;
            boardInput.PathRejected += OnPathRejected;
        }
    }

    private void OnDestroy()
    {
        if (boardInput != null)
        {
            boardInput.PathChanged -= OnPathChanged;
            boardInput.PathCommitted -= OnPathCommitted;
            boardInput.PathRejected -= OnPathRejected;
        }
    }

    private void OnPathChanged(System.Collections.Generic.IReadOnlyList<int> path)
    {
        if (boardView != null)
            boardView.HighlightPath(path, boardInput != null && boardInput.PathIsLegal);
    }

    private void OnPathRejected()
    {
        if (boardView != null)
            boardView.HighlightPath(null, false);
    }

    private void OnPathCommitted(ClearResult result)
    {
        if (boardView != null)
            boardView.HighlightPath(null, false);

        // Push the committed clear through the view first: it diffs the board
        // and classifies every changed cell, which is what the effects below
        // are driven from.
        BoardDelta delta = boardView.ApplyClear(result);
        if (delta == null) return;

        SurgeFeedbackTier tier = SurgeFeedback.Classify(result);
        if (hud != null)
            hud.PresentClear(result, tier);
        if (vfx == null) return;

        float burstScale = tier switch
        {
            SurgeFeedbackTier.Exceptional => 0.42f,
            SurgeFeedbackTier.Strong => 0.36f,
            _ => 0.30f
        };

        // Cleared nodes burst in their own colour — read from the pre-clear
        // snapshot via the delta, since the cell already holds its new value.
        byte clearedCellValue = 1;
        bool foundClearedCell = false;
        foreach (CellDelta d in delta.Changed)
        {
            if (!delta.Cleared.Contains(d.Index)) continue;
            if (!foundClearedCell)
            {
                clearedCellValue = d.From;
                foundClearedCell = true;
            }
            vfx.SpawnClearBurst(boardView.WorldPositionOf(d.Index), ToVfxColor(d.From), burstScale);
        }

        SurgeVfxColor clearCol = ToVfxColor(clearedCellValue);

        // Each tier gets one controlled center accent. Purge has its own
        // presentation below and deliberately skips this generic detonation.
        if (!result.Purge && result.Path != null && result.Path.Length >= 3)
        {
            Vector3 center = boardView.WorldPositionOf(result.Path[result.Path.Length / 2]);
            float shockScale = tier switch
            {
                SurgeFeedbackTier.Exceptional => 0.78f,
                SurgeFeedbackTier.Strong => 0.58f,
                _ => 0.38f
            };
            vfx.PlayShockwave(center, clearCol, shockScale);

            if (tier == SurgeFeedbackTier.Strong)
            {
                vfx.PlayLightningBurst(center, clearCol, 0.48f);
            }
            else if (tier == SurgeFeedbackTier.Exceptional)
            {
                vfx.PlayLightningBurst(center, clearCol, 0.64f);
            }
        }

        // A spark streak along the path gives the clear a direction.
        if (result.Path != null && result.Path.Length >= 2)
        {
            int a = result.Path[0], b = result.Path[result.Path.Length - 1];
            Vector3 from = boardView.WorldPositionOf(a);
            Vector3 to = boardView.WorldPositionOf(b);
            int sparkCount = tier switch
            {
                SurgeFeedbackTier.Exceptional => 14,
                SurgeFeedbackTier.Strong => 10,
                _ => 6
            };
            vfx.EmitSparkStreak(from, ((Vector2)(to - from)).normalized,
                                clearCol, sparkCount);
        }

        // Purge keeps its identity without stacking an oversized generic clear.
        if (result.Purge && result.Path != null && result.Path.Length > 0)
        {
            Vector3 center = boardView.WorldPositionOf(result.Path[result.Path.Length / 2]);
            vfx.PlayPurgeFreeze(center);
            vfx.PlayImplosion(center, SurgeVfxColor.NeonBlue, 0.82f);
            vfx.PlayShockwave(center, SurgeVfxColor.NeonBlue, 0.92f);
        }

        // Combo pop on multiplier chains
        if (result.ChainMult > 1 && result.Path != null && result.Path.Length > 0)
        {
            Vector3 lastPos = boardView.WorldPositionOf(result.Path[result.Path.Length - 1]);
            vfx.PlayComboPop(lastPos);
        }

        // Electricity arcs along cascading cells
        if (delta.Fell != null && delta.Fell.Count >= 2)
        {
            int arcBudget = tier == SurgeFeedbackTier.Exceptional ? 3 : 2;
            for (int i = 0; i < delta.Fell.Count - 1 && i < arcBudget; i++)
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
            if (vfx != null && boardView != null)
            {
                vfx.PlaySurgeOverdrive(boardView.transform.position, boardView.Extent * 0.5f);
            }

            if (SurgeAudioManager.Instance != null)
                SurgeAudioManager.Instance.PlaySurgeActivation();

            if (HapticManager.Instance != null)
                HapticManager.Instance.PlayHeavy();
        }
        else if (!isSurge && _wasSurgeActive && vfx != null && boardView != null)
        {
            vfx.PlaySurgeRelease(boardView.transform.position);
        }
        _wasSurgeActive = isSurge;

        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.SetComboMultiplier(driver.Chain);
    }
}
