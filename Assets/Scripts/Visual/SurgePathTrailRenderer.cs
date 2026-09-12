// ============================================================================
// SURGE — SurgePathTrailRenderer
//
// Renders the glowing neon path trail as the player's finger swipes across nodes.
// Triggers tactile haptics on node connection and clears.
// ============================================================================

using System.Collections.Generic;
using Surge.Runtime;
using SurgeCore;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class SurgePathTrailRenderer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardInputController boardInput;
    [SerializeField] private BoardView boardView;
    [SerializeField] private MatchDriver driver;
    [SerializeField] private SurgePalette palette;

    [Header("Line Tuning")]
    [SerializeField] private float lineWidth = 0.22f;
    [SerializeField] private float zOffset = -0.05f;

    private LineRenderer _line;
    private int _lastPathCount;

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        if (_line == null)
        {
            GameObject trailChild = new GameObject("TrailRenderer_Runtime");
            trailChild.transform.SetParent(transform, false);
            _line = trailChild.AddComponent<LineRenderer>();
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                             ?? Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            _line.material = new Material(unlitShader);
        }

        ConfigureLineRenderer();

        if (boardInput == null) boardInput = FindFirstObjectByType<BoardInputController>();
        if (boardView == null) boardView = FindFirstObjectByType<BoardView>();
        if (driver == null) driver = FindFirstObjectByType<MatchDriver>();
    }

    private void Start()
    {
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

    private void ConfigureLineRenderer()
    {
        _line.useWorldSpace = true;
        _line.startWidth = lineWidth;
        _line.endWidth = lineWidth;
        _line.positionCount = 0;
        _line.numCornerVertices = 5;
        _line.numCapVertices = 5;
        _line.alignment = LineAlignment.TransformZ;
        _line.sortingLayerName = "Default";
        _line.sortingOrder = 15;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;
    }

    private void OnPathChanged(IReadOnlyList<int> path)
    {
        if (path == null || path.Count == 0 || boardView == null || driver == null || !driver.MatchRunning)
        {
            _line.positionCount = 0;
            _lastPathCount = 0;
            return;
        }

        // Tactile tick + ascending audio chime when dragging onto a new valid node
        if (path.Count > _lastPathCount)
        {
            if (HapticManager.Instance != null)
                HapticManager.Instance.PlayLight();

            if (SurgeAudioManager.Instance != null)
                SurgeAudioManager.Instance.PlayNodeTick(path.Count - 1);
        }
        _lastPathCount = path.Count;

        // Determine path color based on cell value
        byte cellVal = driver.Engine.Board.Cells[path[0]];
        Color pathColor = GetColorForCell(cellVal);

        _line.startColor = pathColor;
        _line.endColor = pathColor;

        _line.positionCount = path.Count;
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 pos = boardView.WorldPositionOf(path[i]);
            pos.z += zOffset;
            _line.SetPosition(i, pos);
        }
    }

    private void OnPathCommitted(ClearResult result)
    {
        _line.positionCount = 0;
        _lastPathCount = 0;

        if (HapticManager.Instance != null)
        {
            if (result.Purge || (result.Path != null && result.Path.Length >= 5))
            {
                HapticManager.Instance.PlayHeavy();
            }
            else
            {
                HapticManager.Instance.PlayMedium();
            }
        }

        if (SurgeAudioManager.Instance != null)
        {
            SurgeAudioManager.Instance.PlayClear(result.Path?.Length ?? 3, result.Purge);
            if (result.ChainMult > 1)
                SurgeAudioManager.Instance.PlayCombo(result.ChainMult);
        }
    }

    private void OnPathRejected()
    {
        _line.positionCount = 0;
        _lastPathCount = 0;

        if (SurgeAudioManager.Instance != null)
            SurgeAudioManager.Instance.PlayReject();
    }

    private Color GetColorForCell(byte cellValue)
    {
        if (palette == null) return Color.cyan;
        return cellValue switch
        {
            1 => palette.neonBlue,
            2 => palette.neonPink,
            3 => palette.neonGreen,
            4 => palette.neonYellow,
            5 => palette.neonPurple,
            _ => Color.white
        };
    }
}
