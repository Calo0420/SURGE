// ============================================================================
// SURGE — SurgePathTrailRenderer
//
// Renders a high-voltage dynamic electric current arcing across connected
// capacitor nodes as the player swipes. Dual-layer architecture:
//   - Outer LineRenderer: Pulsing neon corona glow in the active node color.
//   - Inner LineRenderer: White-hot jagged plasma core crackling at high frequency.
// Voltage surges into high-intensity oscillation upon reaching 3+ nodes.
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
    [SerializeField] private float lineWidth = 0.14f;
    [SerializeField] private float zOffset = -0.05f;

    [Header("Electric Current Tuning")]
    [SerializeField] private int segmentsPerLink = 8;
    [SerializeField] private float arcDisplacement = 0.11f;
    [SerializeField] private float arcFrequency = 32f;
    [SerializeField] private float coreWidthMultiplier = 0.45f;
    [SerializeField] private float glowWidthMultiplier = 1.6f;

    private LineRenderer _line;       // Outer glowing neon corona
    private LineRenderer _coreLine;   // Inner white-hot electric plasma core
    private int _lastPathCount;
    private Color _currentPathColor = Color.cyan;
    private readonly List<Vector3> _pathNodePositions = new();

    private Vector3[] _outerPositions = new Vector3[256];
    private Vector3[] _corePositions = new Vector3[256];

    private void Awake()
    {
        _line = GetComponent<LineRenderer>();
        if (_line == null)
        {
            GameObject glowChild = new GameObject("ElectricGlow_Runtime");
            glowChild.transform.SetParent(transform, false);
            _line = glowChild.AddComponent<LineRenderer>();
        }

        Transform coreChild = transform.Find("ElectricCore_Runtime");
        if (coreChild == null)
        {
            GameObject coreGo = new GameObject("ElectricCore_Runtime");
            coreGo.transform.SetParent(transform, false);
            _coreLine = coreGo.AddComponent<LineRenderer>();
        }
        else
        {
            _coreLine = coreChild.GetComponent<LineRenderer>();
        }

        Shader unlitShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                             ?? Shader.Find("UI/NeonAdditiveTint")
                             ?? Shader.Find("Sprites/Default");
        if (unlitShader != null)
        {
            Material glowMat = new Material(unlitShader);
            if (glowMat.HasProperty("_Surface")) glowMat.SetFloat("_Surface", 1);
            if (glowMat.HasProperty("_Blend")) glowMat.SetFloat("_Blend", 1);
            if (glowMat.HasProperty("_SrcBlend")) glowMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (glowMat.HasProperty("_DstBlend")) glowMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (glowMat.HasProperty("_ZWrite")) glowMat.SetFloat("_ZWrite", 0);
            glowMat.renderQueue = 3100;

            _line.material = glowMat;
            _coreLine.material = new Material(glowMat);
        }

        ConfigureLineRenderers();

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

    private void ConfigureLineRenderers()
    {
        // Outer Corona
        _line.useWorldSpace = true;
        _line.startWidth = lineWidth * glowWidthMultiplier;
        _line.endWidth = lineWidth * glowWidthMultiplier;
        _line.positionCount = 0;
        _line.numCornerVertices = 4;
        _line.numCapVertices = 4;
        _line.alignment = LineAlignment.View;
        _line.sortingLayerName = "Default";
        _line.sortingOrder = 15;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;

        // Inner Core
        _coreLine.useWorldSpace = true;
        _coreLine.startWidth = lineWidth * coreWidthMultiplier;
        _coreLine.endWidth = lineWidth * coreWidthMultiplier;
        _coreLine.positionCount = 0;
        _coreLine.numCornerVertices = 4;
        _coreLine.numCapVertices = 4;
        _coreLine.alignment = LineAlignment.View;
        _coreLine.sortingLayerName = "Default";
        _coreLine.sortingOrder = 16;
        _coreLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _coreLine.receiveShadows = false;
    }

    private readonly List<Vector3> _activeRenderPoints = new();

    private void Update()
    {
        if (driver == null || !driver.MatchRunning || driver.IsPaused || _pathNodePositions.Count == 0)
        {
            if (_line != null && _line.positionCount > 0)
                ClearLines();
            return;
        }

        List<Vector3> pts = BuildRenderPoints();
        if (pts.Count < 2)
        {
            if (_line != null && _line.positionCount > 0)
                ClearLines();
            return;
        }

        RenderElectricArc(pts);
    }

    private List<Vector3> BuildRenderPoints()
    {
        _activeRenderPoints.Clear();
        for (int i = 0; i < _pathNodePositions.Count; i++)
        {
            _activeRenderPoints.Add(_pathNodePositions[i]);
        }

        // Live finger tether: crackles continuously from the last connected node to the moving finger
        if (boardInput != null && boardInput.Dragging && _pathNodePositions.Count > 0)
        {
            Vector3 finger = boardInput.CurrentDragWorldPosition;
            finger.z = _pathNodePositions[0].z;
            Vector3 lastNode = _pathNodePositions[_pathNodePositions.Count - 1];
            float dist = Vector3.Distance(lastNode, finger);
            if (dist > 0.08f)
            {
                float maxReach = (boardView != null ? boardView.CellSize : 1.05f) * 1.85f;
                if (dist > maxReach)
                {
                    finger = lastNode + (finger - lastNode).normalized * maxReach;
                }
                _activeRenderPoints.Add(finger);
            }
        }

        return _activeRenderPoints;
    }

    private void RenderElectricArc(List<Vector3> points)
    {
        int linkCount = points.Count - 1;
        int totalPoints = linkCount * segmentsPerLink + 1;

        if (_outerPositions.Length < totalPoints)
        {
            int newCap = Mathf.NextPowerOfTwo(totalPoints);
            _outerPositions = new Vector3[newCap];
            _corePositions = new Vector3[newCap];
        }

        // Surge modulation: Circuit is fully charged upon 3+ nodes (or 2+ in Surge)
        int minReq = (driver != null && driver.SurgeActive) ? 2 : 3;
        bool isCharged = _pathNodePositions.Count >= minReq;
        float voltagePulse = isCharged ? (1f + 0.22f * Mathf.Sin(Time.time * 50f)) : 1f;
        float voltageAmp = isCharged ? 1.4f : 1.0f;

        // Dynamic widths
        float outerW = lineWidth * glowWidthMultiplier * voltagePulse;
        float coreW = lineWidth * coreWidthMultiplier * (isCharged ? 1.25f : 1.0f);

        _line.startWidth = outerW;
        _line.endWidth = outerW;
        _coreLine.startWidth = coreW;
        _coreLine.endWidth = coreW;

        // Dynamic colors: Glowing neon outer sheath + blinding white-hot plasma core
        Color outerCol = _currentPathColor * (isCharged ? 1.4f : 1.0f);
        outerCol.a = isCharged ? 1.0f : 0.85f;

        Color coreCol = Color.Lerp(Color.white, _currentPathColor, 0.2f) * (isCharged ? 1.6f : 1.0f);
        coreCol.a = 1.0f;

        _line.startColor = outerCol;
        _line.endColor = outerCol;
        _coreLine.startColor = coreCol;
        _coreLine.endColor = coreCol;

        int pointIdx = 0;
        float timeNoise = Time.time * arcFrequency;

        for (int i = 0; i < linkCount; i++)
        {
            Vector3 pA = points[i];
            Vector3 pB = points[i + 1];
            Vector3 dir = (pB - pA).normalized;
            Vector3 normal = new Vector3(-dir.y, dir.x, 0f);

            float linkDist = Vector3.Distance(pA, pB);
            float baseAmp = Mathf.Min(linkDist * 0.15f, arcDisplacement) * voltageAmp;

            for (int k = 0; k <= segmentsPerLink; k++)
            {
                // Skip duplicate connection points between adjacent segments
                if (i > 0 && k == 0) continue;

                float t = (float)k / segmentsPerLink;
                Vector3 basePos = Vector3.Lerp(pA, pB, t);

                if (k == 0 || k == segmentsPerLink)
                {
                    // Firmly anchor the electric arc onto the capacitor node terminals
                    _outerPositions[pointIdx] = basePos;
                    _corePositions[pointIdx] = basePos;
                    pointIdx++;
                    continue;
                }

                // Envelope: Zero at capacitor nodes, maximum at center of link
                float envelope = Mathf.Sin(t * Mathf.PI);

                // High-voltage traveling electrical sine wave
                float wave = Mathf.Sin(t * 14f - Time.time * 36f + i * 5.3f) * 0.35f;

                // High-speed Perlin noise for jagged lightning kinks
                float perlinOuter = (Mathf.PerlinNoise(t * 6f + i * 13.7f, timeNoise) - 0.5f) * 2f;

                // Fast stochastic micro-jitter
                float jitter = (Random.value - 0.5f) * 0.65f;

                float outerDisp = (perlinOuter + wave + jitter) * baseAmp * envelope;

                // Inner core crackles independently with higher frequency
                float perlinCore = (Mathf.PerlinNoise(t * 10f + i * 29.3f, timeNoise * 1.4f) - 0.5f) * 2f;
                float coreJitter = (Random.value - 0.5f) * 0.8f;
                float coreDisp = (perlinCore + coreJitter) * (baseAmp * 0.85f) * envelope;

                _outerPositions[pointIdx] = basePos + normal * outerDisp;
                _corePositions[pointIdx] = basePos + normal * coreDisp;
                pointIdx++;
            }
        }

        _line.positionCount = pointIdx;
        _line.SetPositions(_outerPositions);

        _coreLine.positionCount = pointIdx;
        _coreLine.SetPositions(_corePositions);
    }

    private void ClearLines()
    {
        if (_line != null) _line.positionCount = 0;
        if (_coreLine != null) _coreLine.positionCount = 0;
        _pathNodePositions.Clear();
        _lastPathCount = 0;
    }

    private void OnPathChanged(IReadOnlyList<int> path)
    {
        if (path == null || path.Count == 0 || boardView == null || driver == null || !driver.MatchRunning)
        {
            ClearLines();
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
        _currentPathColor = GetColorForCell(cellVal);

        _pathNodePositions.Clear();
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 pos = boardView.WorldPositionOf(path[i]);
            pos.z += zOffset;
            _pathNodePositions.Add(pos);
        }
    }

    private void OnPathCommitted(ClearResult result)
    {
        ClearLines();

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
        ClearLines();

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
