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
        SpriteRenderer[] _sockets;
        byte[] _shown;
        MatchDriver _driver;
        Sprite _generatedCore;
        Sprite _generatedSocket;
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
            _sockets = new SpriteRenderer[size * size];
            _shown = new byte[size * size];

            if (_nodeMaterial == null)
                _nodeMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));

            Sprite coreSprite = nodeSprite != null ? nodeSprite : GeneratedCoreSprite();
            Sprite socketSprite = GeneratedSocketSprite();

            for (int i = 0; i < _nodes.Length; i++)
            {
                var go = new GameObject($"Node_{BoardGeometry.Row(i, size)}_{BoardGeometry.Col(i, size)}");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = LocalPositionOf(i);
                go.transform.localScale = Vector3.one * nodeScale;

                // 1. Dark titanium cyber socket housing (stays on floor)
                var socketObj = new GameObject("Socket");
                socketObj.transform.SetParent(go.transform, false);
                socketObj.transform.localPosition = Vector3.zero;
                socketObj.transform.localScale = Vector3.one * 1.05f;

                var socketSr = socketObj.AddComponent<SpriteRenderer>();
                socketSr.sprite = socketSprite;
                socketSr.sortingLayerName = sortingLayerName;
                socketSr.sortingOrder = sortingOrder - 1;
                socketSr.sharedMaterial = _nodeMaterial;
                _sockets[i] = socketSr;

                // 2. Glowing cyber capacitor lens (tinted with cell color)
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = coreSprite;
                sr.sortingLayerName = sortingLayerName;
                sr.sortingOrder = sortingOrder;
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

        void OnDestroy()
        {
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
