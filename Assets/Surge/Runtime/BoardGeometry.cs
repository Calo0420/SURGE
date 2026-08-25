// ============================================================================
// SURGE — BoardGeometry
//
// Index <-> board-space mapping and the post-settle board diff. Deliberately
// free of UnityEngine types so it compiles and tests headlessly alongside the
// engine (see ci/Surge.Board.Tests). BoardView adds the Vector/Transform layer
// on top; none of the arithmetic below needs it.
//
// Row/column convention matches the engine exactly:
//   index = row * Size + col,  row 0 = TOP, row Size-1 = BOTTOM.
// That is not a free choice — MatchEngine.ApplyGravityAndRefill writes from
// row Size-1 upward and refills the low-numbered rows, so row 0 is where new
// nodes appear. Board space therefore has +Y up and row 0 at the highest Y.
//
// Default cellSize is 1.05, which is not arbitrary either: it is the spacing
// baked into PF_BoardVisualBaseline's grid lines (-3.15 .. 3.15 in steps of
// 1.05). Keeping it means the data-driven board lands exactly on the existing
// art instead of next to it.
// ============================================================================

using System.Collections.Generic;

namespace Surge.Runtime
{
    public struct CellDelta
    {
        public int Index;
        public byte From;
        public byte To;
    }

    /// What actually happened to the board across one committed clear.
    /// Changed is the raw diff; the three classified lists explain it, and
    /// together they are what the VFX layer reads to decide what to play.
    public sealed class BoardDelta
    {
        public readonly List<CellDelta> Changed = new List<CellDelta>();

        /// Cells the player's path removed. Straight from ClearResult.Path.
        public readonly List<int> Cleared = new List<int>();

        /// Cells the refill stream created. Straight from ClearResult.NewNodes.
        public readonly List<int> Refilled = new List<int>();

        /// Cells whose colour changed because gravity pulled a node down into
        /// them. Animate these as a fall, not as a spawn.
        public readonly List<int> Fell = new List<int>();

        /// Cells that changed colour but were neither cleared, refilled, nor
        /// fallen into — i.e. EnsureMoveFloor recoloured them. The engine
        /// never reports these, so a diff is the only way to see them at all.
        /// Measured at 0-2 cells on 99.2% of settles (SurgeCore CHANGELOG),
        /// so treat this as a quiet cross-fade, never a pop.
        public readonly List<int> Repaired = new List<int>();

        public bool Any => Changed.Count > 0;

        public void Clear()
        {
            Changed.Clear();
            Cleared.Clear();
            Refilled.Clear();
            Fell.Clear();
            Repaired.Clear();
        }
    }

    public static class BoardGeometry
    {
        public const float DefaultCellSize = 1.05f;

        public static int Row(int index, int size) => index / size;
        public static int Col(int index, int size) => index % size;
        public static int Index(int row, int col, int size) => row * size + col;

        public static bool InBounds(int row, int col, int size) =>
            row >= 0 && row < size && col >= 0 && col < size;

        /// Board-space X of a cell centre, board centred on the origin.
        public static float CellX(int index, int size, float cellSize) =>
            (Col(index, size) - (size - 1) * 0.5f) * cellSize;

        /// Board-space Y of a cell centre. Row 0 sits at the TOP, so Y is
        /// negated relative to the row number.
        public static float CellY(int index, int size, float cellSize) =>
            ((size - 1) * 0.5f - Row(index, size)) * cellSize;

        /// Inverse of CellX/CellY. Returns false when the point falls outside
        /// the board, so callers can reject strays without a bounds dance.
        public static bool TryCellAt(float x, float y, int size, float cellSize,
                                     out int index)
        {
            index = -1;
            if (cellSize <= 0f) return false;

            int col = (int)System.Math.Floor(x / cellSize + size * 0.5f);
            int row = (int)System.Math.Floor(size * 0.5f - y / cellSize);
            if (!InBounds(row, col, size)) return false;

            index = Index(row, col, size);
            return true;
        }

        /// Full board extent in board space, useful for framing the camera.
        public static float Extent(int size, float cellSize) => size * cellSize;

        // ----------------------------------------------------------- diff --
        /// Diffs two board snapshots and classifies every change.
        ///
        /// `path` and `newNodes` come from ClearResult and may be null (the
        /// first sync of a match, where the whole board is simply new).
        ///
        /// Classifying by "not in path and not in newNodes" is WRONG and was
        /// the first version of this method: gravity rewrites cells that
        /// appear in neither list, so every fallen node would be reported as
        /// a repair. Instead we replay the engine's own gravity to predict
        /// the post-settle board, and attribute only the residual — what the
        /// prediction cannot explain — to EnsureMoveFloor.
        ///
        /// The gravity replay below must stay identical to
        /// MatchEngine.ApplyGravityAndRefill. BoardDiffMatchesEngine in
        /// ci/Surge.Board.Tests pins that against the real engine.
        public static void Diff(byte[] before, byte[] after,
                                int[] path, int[] newNodes, BoardDelta into)
        {
            into.Clear();
            if (before == null || after == null ||
                before.Length != after.Length) return;

            int size = SizeOf(before.Length);
            if (size <= 0) return;

            for (int i = 0; i < after.Length; i++)
            {
                if (before[i] == after[i]) continue;
                into.Changed.Add(new CellDelta
                {
                    Index = i, From = before[i], To = after[i]
                });
            }

            if (path != null)
                foreach (int i in path) into.Cleared.Add(i);
            if (newNodes != null)
                foreach (int i in newNodes) into.Refilled.Add(i);

            // Predict: clear the path, drop each column, then take the refill
            // values from `after` at exactly the indices the engine reported.
            byte[] predicted = (byte[])before.Clone();
            if (path != null)
                foreach (int i in path) predicted[i] = 0;

            for (int c = 0; c < size; c++)
            {
                int write = size - 1;
                for (int r = size - 1; r >= 0; r--)
                {
                    byte v = predicted[r * size + c];
                    if (v == 0) continue;
                    if (write != r)
                    {
                        predicted[write * size + c] = v;
                        predicted[r * size + c] = 0;
                    }
                    write--;
                }
                for (int r = write; r >= 0; r--)
                    predicted[r * size + c] = after[r * size + c];
            }

            for (int i = 0; i < predicted.Length; i++)
                if (predicted[i] != after[i]) into.Repaired.Add(i);

            // Anything that moved under gravity: changed, explained by the
            // prediction, and not itself a refill.
            foreach (CellDelta d in into.Changed)
            {
                if (Contains(newNodes, d.Index)) continue;
                if (predicted[d.Index] != after[d.Index]) continue;  // repaired
                into.Fell.Add(d.Index);
            }
        }

        /// Square-board side length, or -1 if `cells` is not a perfect square.
        static int SizeOf(int cellCount)
        {
            int s = (int)System.Math.Round(System.Math.Sqrt(cellCount));
            return s * s == cellCount ? s : -1;
        }

        static bool Contains(int[] set, int value)
        {
            if (set == null) return false;
            for (int i = 0; i < set.Length; i++)
                if (set[i] == value) return true;
            return false;
        }
    }
}
