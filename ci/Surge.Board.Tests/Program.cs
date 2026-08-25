// ============================================================================
// SURGE — BoardGeometry self-test (headless)
//
// Pins the two things BoardView depends on and cannot verify at runtime:
//   1. Geometry round-trips: every cell centre maps back to its own index.
//   2. The gravity replay in BoardGeometry.Diff matches the real engine.
//
// (2) is the one that matters. Diff distinguishes a node that FELL from one
// that EnsureMoveFloor RECOLOURED by predicting the post-gravity board and
// attributing the residual to repair. If that prediction drifts from
// MatchEngine.ApplyGravityAndRefill, every fall would be reported as a repair
// and the VFX layer would fire the wrong effect on almost every settle.
// ============================================================================

using System;
using System.Collections.Generic;
using SurgeCore;
using Surge.Runtime;

static class Program
{
    static int Main()
    {
        try
        {
            GeometryRoundTrip();
            GeometryOffBoard();
            DiffMatchesEngine(1500);
            Console.WriteLine(_report);
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("BoardGeometry self-test FAILED: " + e.Message);
            return 1;
        }
    }

    static string _report = "";

    static void Check(bool cond, string msg)
    {
        if (!cond) throw new Exception(msg);
    }

    // Every cell centre must map back to the cell it came from, for a range of
    // sizes and cell scales — BoardInputController's hit-testing is this
    // function and nothing else.
    static void GeometryRoundTrip()
    {
        foreach (int size in new[] { 5, 7, 9 })
        foreach (float cell in new[] { 0.5f, 1.05f, 2.3f })
        {
            for (int i = 0; i < size * size; i++)
            {
                float x = BoardGeometry.CellX(i, size, cell);
                float y = BoardGeometry.CellY(i, size, cell);
                Check(BoardGeometry.TryCellAt(x, y, size, cell, out int back),
                      $"centre of cell {i} missed the board (size {size})");
                Check(back == i,
                      $"round-trip {i} -> {back} (size {size}, cell {cell})");

                // Nudging within the cell must not change the answer.
                float q = cell * 0.45f;
                foreach (var (dx, dy) in new[] { (q, q), (-q, q), (q, -q), (-q, -q) })
                {
                    Check(BoardGeometry.TryCellAt(x + dx, y + dy, size, cell, out int n)
                          && n == i,
                          $"corner nudge of cell {i} left the cell (size {size})");
                }
            }
        }
    }

    // Row 0 must be the TOP row, matching the engine's refill direction.
    static void GeometryOffBoard()
    {
        const int size = 7;
        const float cell = 1.05f;

        Check(BoardGeometry.CellY(0, size, cell) > BoardGeometry.CellY(size * size - 1, size, cell),
              "row 0 must sit above the last row");
        Check(Math.Abs(BoardGeometry.CellX(0, size, cell) + 3.15f) < 1e-4f,
              "col 0 centre must be -3.15 at cellSize 1.05");
        Check(Math.Abs(BoardGeometry.CellY(0, size, cell) - 3.15f) < 1e-4f,
              "row 0 centre must be +3.15 at cellSize 1.05");

        float half = size * cell * 0.5f;
        Check(!BoardGeometry.TryCellAt(half + 0.01f, 0, size, cell, out _),
              "point right of the board was accepted");
        Check(!BoardGeometry.TryCellAt(0, -half - 0.01f, size, cell, out _),
              "point below the board was accepted");
        Check(!BoardGeometry.TryCellAt(0, 0, size, 0f, out _),
              "zero cell size must be rejected, not divided by");
    }

    // The real test: drive actual matches and confirm the replay.
    static void DiffMatchesEngine(int matches)
    {
        var delta = new BoardDelta();
        long settles = 0, repairedSettles = 0, repairedCells = 0, fellCells = 0;

        for (int m = 0; m < matches; m++)
        {
            var cfg = new SurgeConfig();
            var eng = new MatchEngine((ulong)m * 2654435761UL + 12345UL, cfg);
            long now = 0;

            for (int step = 0; step < 60; step++)
            {
                List<int> path = FindPath(eng.Board, cfg.MinClearLength);
                if (path == null) break;

                byte[] before = (byte[])eng.Board.Cells.Clone();
                int repairBefore = eng.RepairCount;

                now += 400;
                ClearResult res = eng.TryCommitPath(path, now);
                Check(res != null, "engine rejected a path the harness built");

                int repairOps = eng.RepairCount - repairBefore;
                byte[] after = eng.Board.Cells;

                BoardGeometry.Diff(before, after, res.Path, res.NewNodes, delta);
                settles++;

                // When the engine did not repair, the replay must reproduce
                // the board exactly — zero residual. This is the strict pin.
                if (repairOps == 0)
                    Check(delta.Repaired.Count == 0,
                          $"match {m} step {step}: replay diverged from engine " +
                          $"with no repair ({delta.Repaired.Count} residual cells)");

                // A repair op can repaint the same cell twice, so distinct
                // repaired cells can only ever be <= the op count.
                Check(delta.Repaired.Count <= repairOps,
                      $"match {m} step {step}: {delta.Repaired.Count} repaired " +
                      $"cells but engine reported only {repairOps} ops");

                // Every changed cell must be accounted for exactly once.
                var seen = new HashSet<int>();
                foreach (int i in delta.Refilled) seen.Add(i);
                foreach (int i in delta.Fell)
                    Check(seen.Add(i), $"cell {i} classified as both refilled and fallen");
                foreach (int i in delta.Repaired)
                    Check(seen.Add(i), $"cell {i} classified twice (repair overlap)");

                foreach (CellDelta d in delta.Changed)
                    Check(seen.Contains(d.Index),
                          $"match {m} step {step}: changed cell {d.Index} was not classified");

                if (repairOps > 0) repairedSettles++;
                repairedCells += delta.Repaired.Count;
                fellCells += delta.Fell.Count;
            }
        }

        Check(settles > 1000, $"harness only produced {settles} settles; test is too weak");
        _report =
            $"BoardGeometry self-test OK: {settles} settles across {matches} matches, " +
            $"{repairedSettles} with repair ({100.0 * repairedSettles / settles:F1}%), " +
            $"{repairedCells} repaired cells, {fellCells} fallen cells, " +
            $"replay divergence 0";
    }

    // Smallest legal path: any group of >= minLen, walked with backtracking.
    static List<int> FindPath(Board b, int minLen)
    {
        foreach (List<int> g in BoardOps.Groups(b))
        {
            if (g.Count < minLen) continue;
            foreach (int start in g)
            {
                var path = new List<int>();
                if (Walk(b, start, b.Cells[start], path, minLen)) return path;
            }
        }
        return null;
    }

    static bool Walk(Board b, int cur, byte color, List<int> path, int target)
    {
        path.Add(cur);
        if (path.Count >= target) return true;
        int[] nb = new int[4];
        BoardOps.Neighbors(cur, b.Size, nb);
        foreach (int n in nb)
            if (n >= 0 && b.Cells[n] == color && !path.Contains(n) &&
                Walk(b, n, color, path, target))
                return true;
        path.RemoveAt(path.Count - 1);
        return false;
    }
}
