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
        byte[] _shown;
        MatchDriver _driver;
        Sprite _generated;
        Material _nodeMaterial;

        /// Raised after every applied change, with the classified diff. The
        /// same BoardDelta instance is reused each time — read it inside the
        /// handler, do not store it.
        public event Action<BoardDelta> BoardChanged;

        public int Size { get; private set; }
        public float CellSize => cellSize;
        public bool Built => _nodes != null;

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
            if (_nodes != null)
                foreach (SpriteRenderer r in _nodes)
                    if (r != null) Destroy(r.gameObject);

            Size = size;
            _nodes = new SpriteRenderer[size * size];
            _shown = new byte[size * size];

            Sprite sprite = nodeSprite != null ? nodeSprite : GeneratedSprite();

            for (int i = 0; i < _nodes.Length; i++)
            {
                var go = new GameObject($"Node_{BoardGeometry.Row(i, size)}_{BoardGeometry.Col(i, size)}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = LocalPositionOf(i);
                go.transform.localScale = Vector3.one * nodeScale;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;

                // AddComponent<SpriteRenderer> silently inherits URP 2D's default
                // material, which is Sprite-Lit-Default — that multiplies every
                // node colour by the scene's Global Light 2D before it ever
                // reaches Bloom/Tonemapping, crushing the HDR palette and
                // tinting it toward whatever colour that light happens to be.
                // The mockup art Calo approved by eye uses Unlit materials with
                // no such dependency, so the live nodes must match that: an
                // explicit Unlit sprite material, not an implicit lit default.
                if (_nodeMaterial == null)
                    _nodeMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
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

            byte[] cells = _driver.Engine.Board.Cells;
            BoardGeometry.Diff(_shown, cells,
                               result?.Path, result?.NewNodes, _delta);

            foreach (CellDelta d in _delta.Changed) Paint(d.Index, d.To);
            Array.Copy(cells, _shown, cells.Length);

            BoardChanged?.Invoke(_delta);
            return _delta;
        }

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
            sr.color = value < colors.Length ? colors[value] : Color.magenta;
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
        // A soft circle so the board renders before any art is assigned.
        // Assigning nodeSprite in the inspector overrides it entirely.
        Sprite GeneratedSprite()
        {
            if (_generated != null) return _generated;

            const int px = 128;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, false)
            {
                name = "SurgeNode_Generated",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            float r = px * 0.5f;
            var pixels = new Color32[px * px];
            for (int y = 0; y < px; y++)
            for (int x = 0; x < px; x++)
            {
                float dx = x - r + 0.5f, dy = y - r + 0.5f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / r;
                // Solid core, feathered rim: reads as a node at any size and
                // gives Bloom a clean edge to pick up.
                float a = Mathf.Clamp01(1f - Mathf.SmoothStep(0.78f, 1f, d));
                pixels[y * px + x] = new Color32(255, 255, 255, (byte)(a * 255));
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            _generated = Sprite.Create(tex, new Rect(0, 0, px, px),
                                       new Vector2(0.5f, 0.5f), px);
            _generated.name = "SurgeNode_Generated";
            return _generated;
        }

        void OnDestroy()
        {
            if (_generated != null)
            {
                if (_generated.texture != null) Destroy(_generated.texture);
                Destroy(_generated);
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
