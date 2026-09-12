// ============================================================================
// SURGE — BoardInputController
//
// Turns pointer down / drag / up into a node path and hands it to
// MatchDriver.TryClear. Mouse and touch both arrive through Pointer.current:
// the project is set to activeInputHandler 1 (New Input System only), so
// UnityEngine.Input does not exist here.
//
// THE DIVISION OF AUTHORITY, which matters more than the input code:
//   - This class decides what to DRAW as a preview.
//   - MatchEngine decides what actually SCORES. TryCommitPath re-validates
//     every path it is given and returns null if it disagrees.
// So the preview is allowed to be optimistic, and a desync can only ever cost
// a highlight, never a point. The preview calls PathRules.IsValid — the
// engine's own predicate, not a copy of it — so the two cannot drift.
//
// Surge lowers the minimum path length from 3 to 2 (MatchEngine.TryCommitPath
// reads MinClearLength only when not in Surge), so the preview asks the driver
// for the live Surge state rather than assuming 3.
// ============================================================================

using System;
using System.Collections.Generic;
using SurgeCore;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Surge.Runtime
{
    [DisallowMultipleComponent]
    public sealed class BoardInputController : MonoBehaviour
    {
        [SerializeField] MatchDriver driver;
        [SerializeField] BoardView view;
        [Tooltip("Defaults to Camera.main when left empty.")]
        [SerializeField] Camera worldCamera;

        readonly List<int> _path = new List<int>();
        bool _dragging;

        /// Fires whenever the previewed path changes — draw the trail from
        /// this. The list is reused; copy it if you need to keep it.
        public event Action<IReadOnlyList<int>> PathChanged;

        /// Fires when the engine accepted a path. Carries the ClearResult so
        /// the VFX layer gets Points, ChainMult, Purge and InSurge with it.
        public event Action<ClearResult> PathCommitted;

        /// Fires when a drag ended without a legal path, or was refused.
        public event Action PathRejected;

        public IReadOnlyList<int> CurrentPath => _path;
        public bool Dragging => _dragging;

        /// Minimum legal length right now — 2 during Surge, otherwise the
        /// config's MinClearLength.
        public int MinLength =>
            driver != null && driver.MatchRunning && driver.SurgeActive
                ? 2
                : driver != null && driver.MatchRunning
                    ? driver.Engine.Cfg.MinClearLength
                    : 3;

        /// True when the current preview would be accepted by the engine.
        public bool PathIsLegal =>
            driver != null && driver.MatchRunning &&
            PathRules.IsValid(driver.Engine.Board, _path, MinLength);

        void Awake()
        {
            if (worldCamera == null) worldCamera = Camera.main;
        }

        void Update()
        {
            if (driver == null || view == null || !driver.MatchRunning || !view.Built)
                return;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            // Input is refused outright while suspended/paused or during the purge freeze.
            if (driver.IsPaused || driver.IsFrozen)
            {
                if (_dragging) CancelDrag();
                return;
            }

            bool pressed = pointer.press.isPressed;
            Vector2 screen = pointer.position.ReadValue();

            if (pressed && !_dragging) BeginDrag(screen);
            else if (pressed) ContinueDrag(screen);
            else if (_dragging) EndDrag();
        }

        // ------------------------------------------------------------ drag --
        void BeginDrag(Vector2 screen)
        {
            if (!TryCell(screen, out int cell)) return;
            if (driver.Engine.Board.Cells[cell] == 0) return;

            _dragging = true;
            _path.Clear();
            _path.Add(cell);
            PathChanged?.Invoke(_path);
        }

        void ContinueDrag(Vector2 screen)
        {
            if (_path.Count == 0) return;
            if (!TryCell(screen, out int cell)) return;

            int last = _path[_path.Count - 1];
            if (cell == last) return;

            // Dragging back onto the previous node un-picks the last one.
            // Without this the only way out of a misstep is releasing and
            // starting over, which feels broken on a touchscreen.
            if (_path.Count >= 2 && cell == _path[_path.Count - 2])
            {
                _path.RemoveAt(_path.Count - 1);
                PathChanged?.Invoke(_path);
                return;
            }

            if (_path.Contains(cell)) return;                       // no crossing
            if (!BoardOps.Adjacent(last, cell, view.Size)) return;  // 4-way only

            byte[] cells = driver.Engine.Board.Cells;
            if (cells[cell] != cells[_path[0]]) return;             // one colour

            _path.Add(cell);
            PathChanged?.Invoke(_path);
        }

        void EndDrag()
        {
            _dragging = false;

            if (!PathIsLegal)
            {
                _path.Clear();
                PathChanged?.Invoke(_path);
                PathRejected?.Invoke();
                return;
            }

            ClearResult result = driver.TryClear(_path);
            _path.Clear();
            PathChanged?.Invoke(_path);

            if (result == null) { PathRejected?.Invoke(); return; }
            PathCommitted?.Invoke(result);
        }

        void CancelDrag()
        {
            _dragging = false;
            if (_path.Count == 0) return;
            _path.Clear();
            PathChanged?.Invoke(_path);
            PathRejected?.Invoke();
        }

        bool TryCell(Vector2 screen, out int cell)
        {
            cell = -1;
            if (worldCamera == null) return false;

            // Orthographic 2D: push the point to the board's own plane before
            // converting, so a non-zero camera Z does not shift the hit.
            Vector3 p = screen;
            p.z = Mathf.Abs(worldCamera.transform.position.z - view.transform.position.z);
            Vector3 world = worldCamera.ScreenToWorldPoint(p);
            return view.TryCellAt(world, out cell);
        }
    }
}
