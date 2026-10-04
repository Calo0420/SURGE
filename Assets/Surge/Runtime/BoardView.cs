// ============================================================================
// SURGE — BoardView
//
// Renders MatchEngine.Board as 49 SpriteRenderers and reports what changed.
//
// The view is a pure projection of engine state: it never decides a colour, a
// clear, or a refill, it only reads Board.Cells. Every mutation arrives as a
// ClearResult, and the resulting BoardDelta is what the VFX layer subscribes
// to — cleared / refilled / fell / repaired, already classified.
//
// SpriteRenderer, not the mockup's MeshRenderer quads: PF_BoardVisualBaseline
// carries 23 MeshRenderer + MeshCollider pairs, which are dead weight for a
// tap game and cannot use sorting layers. The mockup stays as the backdrop and
// frame; the 49 live nodes are sprites drawn above it.
//
// Geometry defaults to cellSize 1.05, the spacing already baked into that
// mockup's grid lines, so the data-driven board lands on the existing art.
// ============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using SurgeCore;
using UnityEngine;

namespace Surge.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BoardView : MonoBehaviour
    {
        [Header("Geometry")]
        [SerializeField] float cellSize = BoardGeometry.DefaultCellSize;
        [Tooltip("Node diameter as a fraction of a cell. 0.8 matches the mockup's NodePreview scale.")]
        [SerializeField] float nodeScale = 0.8f;

        [Header("Rendering")]
        [SerializeField] string sortingLayerName = "Default";
        [SerializeField] int sortingOrder = 10;
        [Tooltip("Leave empty to generate a soft circle at runtime.")]
        [SerializeField] Sprite nodeSprite;

        [Header("Palette")]
        [Tooltip("Indexed by cell value: element 0 is the empty cell and is never drawn. " +
                 "SurgeVisualBridge overwrites this from SurgePalette at Awake.")]
        [SerializeField] Color[] colors = new Color[6];

        readonly BoardDelta _delta = new BoardDelta();
        SpriteRenderer[] _nodes;
        SpriteRenderer[] _sockets;
        SpriteRenderer[] _halos;
        Transform[] _nodeRoots;
        Transform[] _visuals;
        byte[] _shown;
        MatchDriver _driver;
        Sprite _generatedCore;
        Sprite _generatedSocket;
        Sprite _generatedHalo;
        Material _nodeMaterial;
        Coroutine _transitionRoutine;
        bool[] _transitioning;

        const float IdleIntensity = 0.58f;
        const float InactivePathIntensity = 0.22f;
        const float ClearStagger = 0.022f;
        const float ClearDuration = 0.11f;
        const float FallDuration = 0.20f;
        const float RefillDuration = 0.24f;
        const float ColumnStagger = 0.014f;

        /// Raised after every applied change, with the classified diff. The
        /// same BoardDelta instance is reused each time — read it inside the
        /// handler, do not store it.
        public event Action<BoardDelta> BoardChanged;

        public int Size { get; private set; }
        public float CellSize => cellSize;
        public float Extent => BoardGeometry.Extent(Size, cellSize);
        public bool Built => _nodes != null;

        readonly List<int> _activePath = new List<int>();
        bool _pathIsLegal;

        public void HighlightPath(IReadOnlyList<int> path, bool isLegal)
        {
            _activePath.Clear();
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++) _activePath.Add(path[i]);
            }
            _pathIsLegal = isLegal;
        }

        private void Update()
        {
            if (_nodes == null || _nodes.Length == 0 || _shown == null) return;

            bool hasPath = _activePath.Count > 0;
            float dt = Time.deltaTime;
            float lerpSpeed = 22f;

            for (int i = 0; i < _nodes.Length; i++)
            {
                SpriteRenderer sr = _nodes[i];
                if (sr == null || !sr.enabled) continue;
                if (_transitioning != null && _transitioning[i]) continue;

                int pathIndex = hasPath ? _activePath.IndexOf(i) : -1;
                bool inPath = pathIndex >= 0;

                byte cellVal = i < _shown.Length ? _shown[i] : (byte)0;
                Color baseCol = cellVal < colors.Length ? colors[cellVal] : Color.white;

                float targetScale = nodeScale;
                Color targetCol = baseCol * IdleIntensity;
                SpriteRenderer halo = _halos != null && i < _halos.Length ? _halos[i] : null;

                if (inPath)
                {
                    targetScale = _pathIsLegal ? (nodeScale * 1.22f) : (nodeScale * 1.12f);

                    if (_pathIsLegal)
                    {
                        float hum = 1f + 0.035f * Mathf.Sin(Time.time * 24f + pathIndex * 0.9f);
                        targetScale *= hum;
                        targetCol = baseCol * 1.35f;
                    }
                    else
                    {
                        targetCol = baseCol * 0.82f;
                    }

                    if (halo != null)
                    {
                        float haloPulse = 1f + 0.06f * Mathf.Sin(Time.time * 18f + pathIndex * 0.7f);
                        halo.enabled = true;
                        halo.transform.localScale = Vector3.Lerp(
                            halo.transform.localScale,
                            Vector3.one * 1.38f * haloPulse,
                            dt * 18f);

                        Color haloColor = baseCol * (_pathIsLegal ? 1.05f : 0.45f);
                        haloColor.a = _pathIsLegal ? 0.42f : 0.18f;
                        halo.color = Color.Lerp(halo.color, haloColor, dt * 20f);
                    }
                }
                else if (hasPath)
                {
                    targetCol = baseCol * InactivePathIntensity;
                    targetScale = nodeScale;
                }
                else if (_driver != null && _driver.MatchRunning && _driver.SurgeActive)
                {
                    float shimmer = 0.72f + 0.12f * Mathf.Sin(Time.time * 8f + i * 0.5f);
                    targetCol = baseCol * shimmer;
                    targetScale = nodeScale * (1f + 0.025f * Mathf.Sin(Time.time * 7f + i * 0.35f));
                }

                if (!inPath && halo != null && halo.enabled)
                {
                    Color fadedHalo = halo.color;
                    fadedHalo.a = Mathf.MoveTowards(fadedHalo.a, 0f, dt * 5f);
                    halo.color = fadedHalo;
                    if (fadedHalo.a <= 0.001f) halo.enabled = false;
                }

                targetCol.a = 1f;

                Transform t = _visuals != null && i < _visuals.Length
                    ? _visuals[i]
                    : sr.transform;
                t.localScale = Vector3.Lerp(t.localScale, Vector3.one * targetScale, dt * lerpSpeed);
                t.localPosition = Vector3.Lerp(t.localPosition, Vector3.zero, dt * lerpSpeed);
                sr.color = Color.Lerp(sr.color, targetCol, dt * lerpSpeed);
            }
        }

        // ------------------------------------------------------------ setup --
        /// Builds the node grid for the driver's live match. Safe to call
        /// again on a new match; the renderers are reused.
        public void Bind(MatchDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            if (!driver.MatchRunning)
                throw new InvalidOperationException(
                    "BoardView.Bind requires a running match — call MatchDriver.BeginMatch first.");

            _driver = driver;
            Board board = driver.Engine.Board;

            if (_nodes == null || Size != board.Size) Build(board.Size);
            SyncAll();
        }

        void Build(int size)
        {
            if (_nodeRoots != null)
                foreach (Transform root in _nodeRoots)
                    if (root != null) Destroy(root.gameObject);

            Size = size;
            _nodes = new SpriteRenderer[size * size];
            _sockets = new SpriteRenderer[size * size];
            _halos = new SpriteRenderer[size * size];
            _nodeRoots = new Transform[size * size];
            _visuals = new Transform[size * size];
            _transitioning = new bool[size * size];
            _shown = new byte[size * size];

            if (_nodeMaterial == null)
                _nodeMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

            Sprite coreSprite = nodeSprite != null ? nodeSprite : GeneratedCoreSprite();
            Sprite socketSprite = GeneratedSocketSprite();
            Sprite haloSprite = GeneratedHaloSprite();

            for (int i = 0; i < _nodes.Length; i++)
            {
                var go = new GameObject($"Node_{BoardGeometry.Row(i, size)}_{BoardGeometry.Col(i, size)}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = LocalPositionOf(i);
                go.transform.localScale = Vector3.one;
                _nodeRoots[i] = go.transform;

                // 1. Dark titanium cyber socket housing (stays on floor)
                var socketObj = new GameObject("Socket");
                socketObj.transform.SetParent(go.transform, false);
                socketObj.transform.localPosition = Vector3.zero;
                socketObj.transform.localScale = Vector3.one * nodeScale * 1.05f;

                var socketSr = socketObj.AddComponent<SpriteRenderer>();
                socketSr.sprite = socketSprite;
                socketSr.sortingLayerName = sortingLayerName;
                socketSr.sortingOrder = sortingOrder - 1;
                socketSr.sharedMaterial = _nodeMaterial;
                socketSr.color = Color.white;
                _sockets[i] = socketSr;

                // 2. Selection-only corona. Keeping this separate lets idle
                // nodes stay crisp and below the global bloom threshold.
                var visualObj = new GameObject("NodeVisual");
                visualObj.transform.SetParent(go.transform, false);
                visualObj.transform.localPosition = Vector3.zero;
                visualObj.transform.localScale = Vector3.one * nodeScale;
                _visuals[i] = visualObj.transform;

                var haloObj = new GameObject("SelectionHalo");
                haloObj.transform.SetParent(visualObj.transform, false);
                haloObj.transform.localPosition = Vector3.zero;
                haloObj.transform.localScale = Vector3.one * 1.38f;

                var haloSr = haloObj.AddComponent<SpriteRenderer>();
                haloSr.sprite = haloSprite;
                haloSr.sortingLayerName = sortingLayerName;
                haloSr.sortingOrder = sortingOrder;
                haloSr.sharedMaterial = _nodeMaterial;
                haloSr.enabled = false;
                _halos[i] = haloSr;

                // 3. Glowing cyber capacitor lens (tinted with cell color)
                var coreObj = new GameObject("Core");
                coreObj.transform.SetParent(visualObj.transform, false);
                coreObj.transform.localPosition = Vector3.zero;
                coreObj.transform.localScale = Vector3.one;

                var sr = coreObj.AddComponent<SpriteRenderer>();
                sr.sprite = coreSprite;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder + 1;
                sr.sharedMaterial = _nodeMaterial;

                _nodes[i] = sr;
            }
        }

        public Vector3 LocalPositionOf(int index) => new Vector3(
            BoardGeometry.CellX(index, Size, cellSize),
            BoardGeometry.CellY(index, Size, cellSize),
            0f);

        public Vector3 WorldPositionOf(int index) =>
            transform.TransformPoint(LocalPositionOf(index));

        /// World point -> cell index. Returns false off-board.
        public bool TryCellAt(Vector3 worldPoint, out int index)
        {
            Vector3 local = transform.InverseTransformPoint(worldPoint);
            return BoardGeometry.TryCellAt(local.x, local.y, Size, cellSize, out index);
        }

        // ------------------------------------------------------------ sync --
        /// Repaints every node from the board with no diff or event. Used on
        /// bind, and after anything that invalidates the cached snapshot.
        public void SyncAll()
        {
            if (_driver == null || !_driver.MatchRunning || _nodes == null) return;

            byte[] cells = _driver.Engine.Board.Cells;
            for (int i = 0; i < _nodes.Length; i++)
            {
                _shown[i] = cells[i];
                Paint(i, cells[i]);
            }
        }

        /// Applies a committed clear: diffs the board against what is on
        /// screen, repaints, and raises BoardChanged with the classified
        /// delta. Call this with the ClearResult that MatchDriver returned.
        public BoardDelta ApplyClear(ClearResult result)
        {
            if (_driver == null || !_driver.MatchRunning || _nodes == null) return null;

            _activePath.Clear();
            byte[] before = (byte[])_shown.Clone();
            byte[] cells = _driver.Engine.Board.Cells;
            int[] clearedNodes = result?.ClearedNodes ?? result?.Path;
            BoardGeometry.Diff(_shown, cells,
                               clearedNodes, result?.NewNodes, _delta);

            Array.Copy(cells, _shown, cells.Length);
            StartTransition(before, cells, result, clearedNodes);

            BoardChanged?.Invoke(_delta);
            return _delta;
        }

        void StartTransition(byte[] before, byte[] after, ClearResult result, int[] clearedNodes)
        {
            if (_transitionRoutine != null)
            {
                StopCoroutine(_transitionRoutine);
                _transitionRoutine = null;
                SnapVisualsToShown();
            }

            _transitionRoutine = StartCoroutine(
                AnimateTransition(before, after, result, clearedNodes));
        }

        IEnumerator AnimateTransition(byte[] before, byte[] after,
                                      ClearResult result, int[] clearedNodes)
        {
            int[] path = clearedNodes;
            var cleared = new HashSet<int>();
            if (path != null)
                for (int i = 0; i < path.Length; i++)
                    if (path[i] >= 0 && path[i] < _nodes.Length)
                        cleared.Add(path[i]);

            int[] sourceForDestination =
                BoardGeometry.BuildGravitySourceMap(before, clearedNodes);
            float clearWindow = path == null || path.Length == 0
                ? 0f
                : (path.Length - 1) * ClearStagger + ClearDuration;

            for (int i = 0; i < _nodes.Length; i++)
            {
                bool moves = sourceForDestination[i] != i;
                bool clears = cleared.Contains(i);
                _transitioning[i] = moves || clears;

                if (clears)
                {
                    _nodes[i].enabled = true;
                    SetNodeColor(i, before[i], 1.15f);
                    SetSocketCharge(i, before[i], 0.45f);
                }
                else if (moves)
                {
                    _nodes[i].enabled = false;
                    _halos[i].enabled = false;
                }
                else
                {
                    Paint(i, after[i]);
                    ResetVisual(i);
                }
            }

            float elapsed = 0f;
            while (elapsed < clearWindow)
            {
                elapsed += Time.unscaledDeltaTime;

                if (path != null)
                {
                    for (int p = 0; p < path.Length; p++)
                    {
                        int index = path[p];
                        if (index < 0 || index >= _nodes.Length) continue;

                        float localTime = elapsed - p * ClearStagger;
                        if (localTime < 0f) continue;

                        float t = Mathf.Clamp01(localTime / ClearDuration);
                        float punch = t < 0.42f
                            ? Mathf.Lerp(1f, 0.72f, EaseIn(t / 0.42f))
                            : Mathf.Lerp(0.72f, 1.18f, EaseOut((t - 0.42f) / 0.58f));
                        _visuals[index].localScale = Vector3.one * nodeScale * punch;

                        Color flash = Color.Lerp(
                            ColorForValue(before[index]) * 1.15f,
                            Color.white * 1.6f,
                            EaseOut(t));
                        flash.a = 1f - Mathf.Clamp01((t - 0.62f) / 0.38f);
                        _nodes[index].color = flash;
                        SetSocketCharge(index, before[index],
                                       0.35f + Mathf.Sin(t * Mathf.PI) * 0.55f);
                    }
                }

                yield return null;
            }

            if (path != null)
                for (int i = 0; i < path.Length; i++)
                {
                    int index = path[i];
                    if (index >= 0 && index < _nodes.Length)
                        _nodes[index].enabled = false;
                }

            float maxTravel = 0f;
            for (int i = 0; i < _nodes.Length; i++)
            {
                int source = sourceForDestination[i];
                if (source == i)
                {
                    _transitioning[i] = false;
                    continue;
                }

                bool refill = source < 0;
                int row = BoardGeometry.Row(i, Size);
                int refillDepth = Mathf.Max(1, Size - row);
                Vector3 startOffset = refill
                    ? Vector3.up * cellSize * refillDepth
                    : LocalPositionOf(source) - LocalPositionOf(i);

                _visuals[i].localPosition = startOffset;
                _visuals[i].localScale = Vector3.one * nodeScale * (refill ? 0.82f : 0.94f);
                SetNodeColor(i, after[i], refill ? 0.82f : IdleIntensity);
                _nodes[i].enabled = after[i] != 0;
                _halos[i].enabled = false;

                maxTravel = Mathf.Max(maxTravel, Mathf.Abs(startOffset.y));
            }

            float travelElapsed = 0f;
            float travelDuration = (maxTravel > cellSize * 1.1f ? RefillDuration : FallDuration) +
                                   (Size - 1) * ColumnStagger;
            while (travelElapsed < travelDuration)
            {
                travelElapsed += Time.unscaledDeltaTime;
                float residualCharge = 1f - Mathf.Clamp01(travelElapsed / travelDuration);

                foreach (int index in cleared)
                    SetSocketCharge(index, before[index], residualCharge * 0.35f);

                for (int i = 0; i < _nodes.Length; i++)
                {
                    int source = sourceForDestination[i];
                    if (source == i) continue;

                    bool refill = source < 0;
                    float delay = BoardGeometry.Col(i, Size) * ColumnStagger;
                    float nodeDuration = refill ? RefillDuration : FallDuration;
                    float t = Mathf.Clamp01((travelElapsed - delay) / nodeDuration);
                    if (t <= 0f) continue;
                    float eased = EaseOutCubic(t);

                    Vector3 startOffset = source < 0
                        ? Vector3.up * cellSize * Mathf.Max(1, Size - BoardGeometry.Row(i, Size))
                        : LocalPositionOf(source) - LocalPositionOf(i);

                    _visuals[i].localPosition = Vector3.LerpUnclamped(startOffset, Vector3.zero, eased);

                    float landing = t > 0.78f
                        ? Mathf.Sin((t - 0.78f) / 0.22f * Mathf.PI) * 0.10f
                        : 0f;
                    _visuals[i].localScale = Vector3.one * nodeScale * (1f + landing);

                    Color target = ColorForValue(after[i]) * IdleIntensity;
                    target.a = 1f;
                    _nodes[i].color = Color.Lerp(_nodes[i].color, target, eased);
                }

                yield return null;
            }

            SnapVisualsToShown();
            _transitionRoutine = null;
        }

        void SnapVisualsToShown()
        {
            if (_nodes == null || _shown == null) return;

            for (int i = 0; i < _nodes.Length; i++)
            {
                _transitioning[i] = false;
                ResetVisual(i);
                if (_sockets != null && i < _sockets.Length && _sockets[i] != null)
                    _sockets[i].color = Color.white;
                Paint(i, _shown[i]);
            }
        }

        void ResetVisual(int index)
        {
            if (_visuals == null || index < 0 || index >= _visuals.Length ||
                _visuals[index] == null) return;

            _visuals[index].localPosition = Vector3.zero;
            _visuals[index].localScale = Vector3.one * nodeScale;
            if (_halos[index] != null) _halos[index].enabled = false;
        }

        void SetNodeColor(int index, byte value, float intensity)
        {
            if (index < 0 || index >= _nodes.Length || _nodes[index] == null) return;
            Color color = ColorForValue(value) * intensity;
            color.a = 1f;
            _nodes[index].color = color;
        }

        void SetSocketCharge(int index, byte value, float strength)
        {
            if (_sockets == null || index < 0 || index >= _sockets.Length ||
                _sockets[index] == null) return;

            Color charge = ColorForValue(value) * 1.15f;
            charge.a = 1f;
            _sockets[index].color = Color.Lerp(Color.white, charge, Mathf.Clamp01(strength));
        }

        Color ColorForValue(byte value) =>
            value < colors.Length ? colors[value] : Color.magenta;

        static float EaseIn(float t) => t * t;
        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
        static float EaseOutCubic(float t) =>
            1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);

        void Paint(int index, byte value)
        {
            SpriteRenderer sr = _nodes[index];
            if (sr == null) return;

            if (value == 0)                      // never happens post-settle
            {
                sr.enabled = false;
                return;
            }

            sr.enabled = true;
            Color baseColor = ColorForValue(value);
            baseColor *= IdleIntensity;
            baseColor.a = 1f;
            sr.color = baseColor;
        }

        /// Replaces the colour table. Element 0 is the empty cell and is
        /// ignored; element N is the colour for cell value N.
        public void SetPalette(Color[] byCellValue)
        {
            if (byCellValue == null || byCellValue.Length == 0) return;
            colors = byCellValue;
            SyncAll();
        }

        // ---------------------------------------------------------- sprite --
        // High-resolution procedural Cyber Capacitor terminal and base socket.
        // Assigning nodeSprite in the inspector overrides the core lens.
        Sprite GeneratedSocketSprite()
        {
            if (_generatedSocket != null) return _generatedSocket;

            const int px = 256;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false)
            {
                name = "SurgeSocket_Generated",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float r = px * 0.5f;
            var pixels = new Color32[px * px];
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float dx = (x - r + 0.5f) / r;
                float dy = (y - r + 0.5f) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > 1.0f)
                {
                    pixels[y * px + x] = new Color32(0, 0, 0, 0);
                    continue;
                }

                // Directional lighting from top-left (arcade cabinet overhead light)
                float light = (-dx * 0.707f + dy * 0.707f);
                float a = 1f;

                if (d > 0.94f)
                {
                    a = Mathf.Clamp01((1f - d) / 0.06f);
                }

                float shade;
                if (d >= 0.80f)
                {
                    // Outer titanium beveled bezel
                    shade = Mathf.Lerp(0.20f, 0.48f, (light + 1f) * 0.5f);
                    // 4 alignment notches at cardinal directions
                    float angle = Mathf.Abs(Mathf.Atan2(dy, dx));
                    if (Mathf.Abs(angle) < 0.07f || Mathf.Abs(angle - Mathf.PI * 0.5f) < 0.07f || Mathf.Abs(angle - Mathf.PI) < 0.07f)
                    {
                        shade *= 0.4f;
                    }
                }
                else if (d >= 0.70f)
                {
                    // Recessed dark trench
                    shade = 0.05f + 0.04f * (1f - (d - 0.70f) / 0.10f);
                }
                else
                {
                    // Inner metallic bed
                    shade = 0.08f + 0.05f * (light + 1f) * 0.5f;
                }

                byte val = (byte)(Mathf.Clamp01(shade) * 255);
                byte alpha = (byte)(Mathf.Clamp01(a) * 255);
                // Subtle cyan tint on the titanium housing
                pixels[y * px + x] = new Color32((byte)(val * 0.85f), (byte)(val * 0.95f), val, alpha);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _generatedSocket = Sprite.Create(tex, new Rect(0, 0, px, px),
                                             new Vector2(0.5f, 0.5f), px);
            _generatedSocket.name = "SurgeSocket_Generated";
            return _generatedSocket;
        }

        Sprite GeneratedCoreSprite()
        {
            if (_generatedCore != null) return _generatedCore;

            const int px = 256;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false)
            {
                name = "SurgeCore_Generated",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float r = px * 0.5f;
            var pixels = new Color32[px * px];
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float dx = (x - r + 0.5f) / r;
                float dy = (y - r + 0.5f) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);

                if (d > 0.85f)
                {
                    // Soft outer glow halo
                    float a = Mathf.Clamp01(1f - (d - 0.85f) / 0.15f);
                    float glow = a * 0.22f;
                    byte gb = (byte)(glow * 255);
                    pixels[y * px + x] = new Color32(gb, gb, gb, gb);
                    continue;
                }

                // 1. Neon Energy Ring around rim (0.58 to 0.82)
                float ring = 0f;
                if (d >= 0.58f && d <= 0.82f)
                {
                    float ringDist = Mathf.Abs(d - 0.70f) / 0.12f;
                    ring = Mathf.Clamp01(1f - ringDist) * 1.5f;
                }

                // 2. Convex 3D Spherical Dome Core (0 to 0.65)
                float coreD = Mathf.Clamp01(d / 0.65f);
                float dome = Mathf.Cos(coreD * Mathf.PI * 0.5f);
                float plasmaCenter = Mathf.Exp(-coreD * coreD * 3.8f) * 0.9f;

                // 3. Curved specular glass glint (top-left reflection arc)
                float glintDx = dx - (-0.20f);
                float glintDy = dy - (0.22f);
                float glintDist = Mathf.Sqrt(glintDx * glintDx + glintDy * glintDy);
                float glint = Mathf.Exp(-glintDist * glintDist * 18f) * 1.25f;

                // Combine:
                float intensity = dome * 0.60f + plasmaCenter + ring + glint;
                float alpha = Mathf.Clamp01(Mathf.Max(ring * 0.8f, dome) + glint * 0.6f);

                byte c = (byte)(Mathf.Clamp01(intensity) * 255);
                byte aByte = (byte)(Mathf.Clamp01(alpha) * 255);
                pixels[y * px + x] = new Color32(c, c, c, aByte);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _generatedCore = Sprite.Create(tex, new Rect(0, 0, px, px),
                                           new Vector2(0.5f, 0.5f), px);
            _generatedCore.name = "SurgeCore_Generated";
            return _generatedCore;
        }

        Sprite GeneratedHaloSprite()
        {
            if (_generatedHalo != null) return _generatedHalo;

            const int px = 128;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false)
            {
                name = "SurgeHalo_Generated",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float r = px * 0.5f;
            var pixels = new Color32[px * px];
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float dx = (x - r + 0.5f) / r;
                float dy = (y - r + 0.5f) / r;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-Mathf.Pow((d - 0.66f) * 5.2f, 2f));
                float falloff = Mathf.Clamp01(1f - d);
                float alpha = Mathf.Clamp01(ring * 0.75f + falloff * 0.12f);
                byte a = (byte)(alpha * 255f);
                pixels[y * px + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(pixels);
            tex.Apply();

            _generatedHalo = Sprite.Create(tex, new Rect(0, 0, px, px),
                                           new Vector2(0.5f, 0.5f), px);
            _generatedHalo.name = "SurgeHalo_Generated";
            return _generatedHalo;
        }

        void OnDestroy()
        {
            if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);

            if (_generatedCore != null)
            {
                if (_generatedCore.texture != null) Destroy(_generatedCore.texture);
                Destroy(_generatedCore);
            }
            if (_generatedSocket != null)
            {
                if (_generatedSocket.texture != null) Destroy(_generatedSocket.texture);
                Destroy(_generatedSocket);
            }
            if (_generatedHalo != null)
            {
                if (_generatedHalo.texture != null) Destroy(_generatedHalo.texture);
                Destroy(_generatedHalo);
            }
        }

        // --------------------------------------------------------- editor --
        void OnDrawGizmosSelected()
        {
            int size = Size > 0 ? Size : 7;
            float extent = BoardGeometry.Extent(size, cellSize);
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.35f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(extent, extent, 0.01f));
        }
    }
}
