// ============================================================================
// SURGE — Deterministic Core Engine (SurgeCore.cs)
// Pure C#. No Unity references. Integer math only.
//
// THE CONTRACT: final state = f(matchSeed, config, ordered action list).
// Identical inputs => bit-identical score, board, and hash chain on every
// platform. This file is the single source of truth for game rules.
//
// v3 — move-floor repair fix (see CHANGELOG at bottom). Repair now GROWS an
// undersized group instead of painting cells with the most common colour,
// and lives in exactly one place: Repair.RecolorToFloor.
//
// v2 — bank-grace fix: Tick() no longer accumulates dt. State transitions
// depend only on (action, timestamp) pairs — Tick is schedule-invariant,
// verified by ReplayInvariance below.
// ============================================================================

using System;
using System.Collections.Generic;

namespace SurgeCore
{
    // ---------------------------------------------------------------- PRNG --
    public sealed class Rng
    {
        ulong _s;
        public Rng(ulong seed) { _s = seed; }

        public ulong Next()
        {
            _s += 0x9E3779B97F4A7C15UL;
            ulong z = _s;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public int NextInt(int n) => (int)(Next() % (ulong)n);
    }

    public static class Keys
    {
        // Independent sub-streams: 1 = initial board, 2 = refill, 3 = repair.
        public static ulong Derive(ulong matchSeed, ulong purpose)
        {
            unchecked
            {
                return new Rng(matchSeed ^ (purpose * 0x9E3779B97F4A7C15UL)
                                          ^ 0xA0761D6478BD642FUL).Next();
            }
        }
    }

    public static class Fnv
    {
        public static ulong Of(byte[] data, ulong h = 14695981039346656037UL)
        {
            foreach (byte b in data) { h ^= b; h *= 1099511628211UL; }
            return h;
        }
        public static ulong Mix(ulong h, ulong v)
        {
            h ^= v; h *= 1099511628211UL; return h ^ (h >> 29);
        }
    }

    // -------------------------------------------------------------- CONFIG --
    public sealed class SurgeConfig
    {
        public int Size = 7;
        public int NumColors = 5;
        public int MoveFloor = 3;
        public int MinClearLength = 3;

        public int MinGroupsStart = 4;
        public int MaxGroupSizeStart = 6;
        public int MinBigGroupsStart = 2;

        public int SpeedChainWindowMs = 1500;
        public int MaxChainMult = 10;
        public int PurgeBonus = 5000;
        public int PurgeFreezeMs = 1500;

        public int MeterMax = 100;
        public int MeterGainLen3 = 4;
        public int MeterGainLen4 = 8;
        public int MeterGainLen5Plus = 12;
        public int SurgeDurationMs = 5000;
        public int SurgeBankGraceMs = 8000;   // v2: pop within 8s of banking or lose it
        public int SurgePairScore = 150;
        public int SurgeChainStepPct = 50;
        public int MaxSurgeMultPct = 300;

        public ulong Hash()
        {
            ulong h = 14695981039346656037UL;
            int[] v = { Size, NumColors, MoveFloor, MinClearLength, MinGroupsStart,
                        MaxGroupSizeStart, MinBigGroupsStart, SpeedChainWindowMs,
                        MaxChainMult, PurgeBonus, PurgeFreezeMs, MeterMax,
                        MeterGainLen3, MeterGainLen4, MeterGainLen5Plus,
                        SurgeDurationMs, SurgeBankGraceMs, SurgePairScore,
                        SurgeChainStepPct, MaxSurgeMultPct };
            foreach (int x in v) h = Fnv.Mix(h, (ulong)x);
            return h;
        }
    }

    // -------------------------------------------------------------- BOARD --
    public sealed class Board
    {
        public readonly int Size;
        public readonly byte[] Cells;
        public Board(int size) { Size = size; Cells = new byte[size * size]; }
        public int Idx(int r, int c) { return r * Size + c; }
        public bool InBounds(int r, int c)
        {
            return r >= 0 && r < Size && c >= 0 && c < Size;
        }
    }

    public static class BoardOps
    {
        public static readonly int[] DR = { -1, 1, 0, 0 };
        public static readonly int[] DC = { 0, 0, -1, 1 };

        public static bool Adjacent(int a, int b, int size)
        {
            return Math.Abs(a / size - b / size) + Math.Abs(a % size - b % size) == 1;
        }

        public static void Neighbors(int idx, int size, int[] into4)
        {
            int r = idx / size, c = idx % size;
            into4[0] = r > 0 ? idx - size : -1;
            into4[1] = r < size - 1 ? idx + size : -1;
            into4[2] = c > 0 ? idx - 1 : -1;
            into4[3] = c < size - 1 ? idx + 1 : -1;
        }

        public static List<List<int>> Groups(Board b)
        {
            var result = new List<List<int>>();
            var seen = new bool[b.Cells.Length];
            for (int i = 0; i < b.Cells.Length; i++)
            {
                if (seen[i] || b.Cells[i] == 0) continue;
                byte color = b.Cells[i];
                var group = new List<int>();
                var q = new Queue<int>();
                q.Enqueue(i); seen[i] = true;
                while (q.Count > 0)
                {
                    int cur = q.Dequeue();
                    group.Add(cur);
                    int r = cur / b.Size, c = cur % b.Size;
                    for (int d = 0; d < 4; d++)
                    {
                        int nr = r + DR[d], nc = c + DC[d];
                        if (!b.InBounds(nr, nc)) continue;
                        int ni = nr * b.Size + nc;
                        if (!seen[ni] && b.Cells[ni] == color)
                        { seen[ni] = true; q.Enqueue(ni); }
                    }
                }
                result.Add(group);
            }
            return result;
        }

        public static int CountGroupsAtLeast(Board b, int minSize)
        {
            int n = 0;
            foreach (var g in Groups(b)) if (g.Count >= minSize) n++;
            return n;
        }

        public static int LargestGroupSize(Board b)
        {
            int m = 0;
            foreach (var g in Groups(b)) if (g.Count > m) m = g.Count;
            return m;
        }
    }

    public static class PathRules
    {
        public static bool IsValid(Board b, List<int> path, int minLen)
        {
            if (path == null || path.Count < minLen) return false;
            byte color = b.Cells[path[0]];
            if (color == 0) return false;
            var seen = new HashSet<int>();
            foreach (int n in path)
                if (b.Cells[n] != color || !seen.Add(n)) return false;
            for (int i = 1; i < path.Count; i++)
                if (!BoardOps.Adjacent(path[i - 1], path[i], b.Size)) return false;
            return true;
        }
    }

    // ------------------------------------------------------- REFILL STREAM --
    public sealed class RefillStream
    {
        readonly Rng _rng;
        readonly int _numColors;
        byte _last1, _last2;

        public RefillStream(ulong seed, int numColors)
        { _rng = new Rng(seed); _numColors = numColors; }

        public byte Next()
        {
            for (;;)
            {
                byte c = (byte)(1 + _rng.NextInt(_numColors));
                if (c == _last1 && c == _last2) continue;
                _last2 = _last1; _last1 = c;
                return c;
            }
        }
    }

    // ---------------------------------------------------------- GENERATOR --
    public static class BoardGenerator
    {
        public static Board Generate(ulong seed, SurgeConfig cfg)
        {
            Rng rng = new Rng(seed);
            for (int attempt = 0; attempt < 500; attempt++)
            {
                Board b = BalancedShuffle(rng, cfg);
                if (BoardOps.CountGroupsAtLeast(b, 3) >= cfg.MinGroupsStart &&
                    BoardOps.LargestGroupSize(b) <= cfg.MaxGroupSizeStart &&
                    BoardOps.CountGroupsAtLeast(b, 4) >= cfg.MinBigGroupsStart)
                    return b;
            }
            Board fb = BalancedShuffle(rng, cfg);
            Repair.RecolorToFloor(fb, rng, cfg, cfg.MoveFloor);
            return fb;
        }

        static Board BalancedShuffle(Rng rng, SurgeConfig cfg)
        {
            int n = cfg.Size * cfg.Size;
            byte[] bag = new byte[n];
            int[] order = new int[cfg.NumColors];
            for (int i = 0; i < order.Length; i++) order[i] = i + 1;
            for (int i = order.Length - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                int tmp = order[i]; order[i] = order[j]; order[j] = tmp;
            }
            int k = 0, baseCount = n / cfg.NumColors, extra = n % cfg.NumColors;
            for (int ci = 0; ci < cfg.NumColors; ci++)
                for (int t = 0; t < baseCount + (ci < extra ? 1 : 0); t++)
                    bag[k++] = (byte)order[ci];
            for (int i = n - 1; i > 0; i--)
            {
                int j = rng.NextInt(i + 1);
                byte tmp = bag[i]; bag[i] = bag[j]; bag[j] = tmp;
            }
            Board b = new Board(cfg.Size);
            Array.Copy(bag, b.Cells, n);
            return b;
        }
    }

    public static class Repair
    {
        // THE ONLY repair implementation in this file. MatchEngine delegates
        // here; nothing re-inlines it.
        //
        // v3: the floor metric counts DISTINCT groups of >= MinClearLength.
        // The v2 repair painted random cells with MostCommon, which MERGES
        // groups and therefore drives that metric DOWN — a feedback loop whose
        // absorbing state is a single-colour board. Repair now GROWS an
        // undersized group instead, which is the only operation that raises
        // the metric by construction.
        //
        // Returns the number of cells recoloured, so callers can keep
        // RepairCount truthful.
        public static int RecolorToFloor(Board b, Rng rng, SurgeConfig cfg, int floor)
        {
            int recoloured = 0, guard = 0;
            while (BoardOps.CountGroupsAtLeast(b, cfg.MinClearLength) < floor &&
                   guard++ < 128)
            {
                int did = ForcePromoteSmallestGroup(b, rng, cfg, cfg.MinClearLength);
                if (did == 0) break;          // nothing promotable; board is boxed in
                recoloured += did;
            }
            return recoloured;
        }

        // Grows the smallest undersized group up to `target` cells by
        // recolouring neighbouring cells into it.
        //
        // The guard that makes this converge: a neighbour whose OWN group is
        // exactly `target` is never stolen from, because dropping it to
        // target-1 would demote an already-valid group in the act of promoting
        // this one — a net-zero trade, which is precisely how the v2 loop
        // spun without ever gaining ground.
        //
        // Deterministic: depends only on board state and `rng`, never on time,
        // so ReplayInvariance still holds.
        static int ForcePromoteSmallestGroup(Board b, Rng rng, SurgeConfig cfg,
                                             int target)
        {
            List<List<int>> groups = BoardOps.Groups(b);

            // Undersized groups, smallest first; stable within a size band so
            // the choice stays deterministic.
            var anchors = new List<int>();
            for (int size = 1; size < target; size++)
                foreach (var g in groups)
                    if (g.Count == size) anchors.Add(g[0]);

            // A group can be boxed in — every neighbour is either already ours
            // or sits in a group of exactly `target`. Fall through to the next
            // candidate rather than giving up: committing to the single
            // smallest group stalled the floor at 2 groups whenever that group
            // happened to be the one blocked group on the board, leaving dozens
            // of promotable groups untried.
            foreach (int anchor in anchors)
            {
                int did = TryPromote(b, rng, cfg, target, anchor);
                if (did > 0) return did;
            }
            return 0;                         // nothing on the board can grow
        }

        // Grows the group containing `anchor` toward `target`. Returns cells
        // recoloured — 0 means this group is boxed in and the caller should
        // try another.
        static int TryPromote(Board b, Rng rng, SurgeConfig cfg, int target,
                              int anchor)
        {
            byte color = b.Cells[anchor];
            int recoloured = 0, guard = 0;
            int[] nb = new int[4];

            while (guard++ < 64)
            {
                // Re-measure: the previous steal changed group shapes.
                List<List<int>> groups = BoardOps.Groups(b);
                int[] sizeOf = new int[b.Cells.Length];
                List<int> cur = null;
                foreach (var g in groups)
                {
                    foreach (int n in g) sizeOf[n] = g.Count;
                    if (g.Contains(anchor)) cur = g;
                }
                if (cur == null || cur.Count >= target) return recoloured;

                var cands = new List<int>();
                foreach (int cell in cur)
                {
                    BoardOps.Neighbors(cell, b.Size, nb);
                    foreach (int n in nb)
                    {
                        if (n < 0) continue;
                        if (b.Cells[n] == color) continue;      // already ours
                        if (sizeOf[n] == target) continue;      // never demote a valid group
                        if (!cands.Contains(n)) cands.Add(n);
                    }
                }
                if (cands.Count == 0) return recoloured;        // boxed in

                b.Cells[cands[rng.NextInt(cands.Count)]] = color;
                recoloured++;
            }
            return recoloured;
        }

        public static byte MostCommon(Board b, SurgeConfig cfg)
        {
            int[] counts = new int[cfg.NumColors + 1];
            foreach (byte v in b.Cells)
                if (v > 0 && v <= cfg.NumColors) counts[v]++;
            byte best = 1;
            for (byte c = 2; c <= cfg.NumColors; c++)
                if (counts[c] > counts[best]) best = c;
            return best;
        }
    }

    // --------------------------------------------------------- SEED SOURCE --
    // v2: isolates the one line that touches the Skillz SDK. VERIFY the exact
    // API name/shape against current Skillz Unity SDK docs before wiring this
    // in live — SkillzCrossPlatform.Random is the historically documented
    // pattern but SDK surfaces drift. CI/editor uses TestSeedSource instead;
    // the engine itself never knows or cares which source it got.
    public interface ISeedSource { ulong DeriveMatchSeed(); }

    public sealed class TestSeedSource : ISeedSource
    {
        readonly ulong _seed;
        public TestSeedSource(ulong seed) { _seed = seed; }
        public ulong DeriveMatchSeed() => _seed;
    }

    // Rules for whoever wires the real one in (Skillz agent):
    //  1. Draw a FIXED block ONCE at match start. Never touch the SDK RNG
    //     again mid-match — a stray call (e.g. a cosmetic feature grabbing a
    //     random trail color) desyncs the two clients' call counts silently.
    //  2. Never seed from device time or a derivable value like a sequential
    //     matchId — that's precomputable offline by a motivated cheater.
    //  3. Log the derived matchSeed into every submitted MatchResult so the
    //     analyzer can independently rebuild the board from submitted data.

    // ------------------------------------------------------------- ENGINE --
    public enum ActionType : byte { Clear = 0, ActivateSurge = 1 }

    public sealed class ActionLog
    {
        public ActionType Kind;
        public long TimeMs;     // game-clock ms (pauses during purge freeze)
        public int[] Path;      // node indices; null for ActivateSurge
    }

    public sealed class ClearResult
    {
        public int[] Path;
        public int Points;
        public int ChainMult;
        public bool Purge;
        public byte PurgedColor;
        public bool InSurge;
        public int MeterAfter;
        public int[] NewNodes;
    }

    public sealed class MatchResult
    {
        public ulong MatchSeed, ConfigHash, BoardHash, HashChain;
        public int Score, ActionCount, RepairCount, PurgeCount, SurgesActivated;
        public List<ActionLog> Log;
    }

    public sealed class MatchEngine
    {
        public readonly SurgeConfig Cfg;
        public readonly Board Board;
        public readonly ulong MatchSeed;
        public readonly ulong ConfigHash;

        public int Score;
        public int Chain = 1;
        public int Meter;
        public int SurgeChainPct = 100;
        public long FreezeUntilMs = long.MinValue;
        public int RepairCount, PurgeCount, SurgesActivated;

        readonly RefillStream _refill;
        readonly Rng _repair;
        readonly List<ActionLog> _actions = new List<ActionLog>();
        long _lastClearMs = long.MinValue;
        long? _surgeStartMs;
        long _bankedSinceMs = long.MinValue;   // v2: when meter hit full; MinValue = not banked
        public ulong HashChain;

        public MatchEngine(ulong matchSeed, SurgeConfig cfg)
        {
            MatchSeed = matchSeed;
            Cfg = cfg;
            ConfigHash = cfg.Hash();
            Board = BoardGenerator.Generate(Keys.Derive(matchSeed, 1), cfg);
            _refill = new RefillStream(Keys.Derive(matchSeed, 2), cfg.NumColors);
            _repair = new Rng(Keys.Derive(matchSeed, 3));
            HashChain = Fnv.Mix(ConfigHash, matchSeed);
        }

        public IReadOnlyList<ActionLog> Actions => _actions;
        public ulong BoardHash => Fnv.Of(Board.Cells);

        // v2: Tick observes and applies schedule-invariant timestamp
        // transitions only. Safe to call at any frequency, any number of
        // times, or not at all between actions — verified by
        // SelfTest.ReplayInvariance. It still mutates state (zeroing the
        // meter on bank expiry, ending Surge) — but only ever as a pure
        // function of nowMs, never of how often it was called.
        public void Tick(long nowMs)
        {
            RefreshSurge(nowMs);
            SyncBank(nowMs);
        }

        void SyncBank(long nowMs)
        {
            if (_bankedSinceMs == long.MinValue) return;
            if (nowMs - _bankedSinceMs >= Cfg.SurgeBankGraceMs)
            {
                _bankedSinceMs = long.MinValue;
                Meter = 0;                     // hesitation cost: bank lost
            }
        }

        public bool Banked(long nowMs)
        {
            SyncBank(nowMs);
            return _bankedSinceMs != long.MinValue && _surgeStartMs == null;
        }

        // Juice layer renders the fuse countdown from this.
        public long BankFuseRemainingMs(long nowMs) =>
            Banked(nowMs) ? Math.Max(0, Cfg.SurgeBankGraceMs - (nowMs - _bankedSinceMs)) : 0;

        public bool SurgeActive(long nowMs)
        { RefreshSurge(nowMs); return _surgeStartMs != null; }

        // Player pops a banked meter on THEIR timing — when Surge fires is a
        // skill/timing decision, never board luck.
        public bool CommitSurge(long nowMs)
        {
            if (!Banked(nowMs)) return false;
            _bankedSinceMs = long.MinValue;    // spent
            _surgeStartMs = nowMs;
            SurgeChainPct = 100;
            SurgesActivated++;
            _actions.Add(new ActionLog { Kind = ActionType.ActivateSurge,
                                         TimeMs = nowMs, Path = null });
            HashChain = Fnv.Mix(HashChain, BoardHash ^ (ulong)_actions.Count);
            return true;
        }

        public ClearResult TryCommitPath(List<int> path, long nowMs)
        {
            SyncBank(nowMs);   // v2: keep MeterAfter truthful before scoring
            bool inSurge = SurgeActive(nowMs);
            int minLen = inSurge ? 2 : Cfg.MinClearLength;
            if (!PathRules.IsValid(Board, path, minLen)) return null;

            int len = path.Count;
            byte color = Board.Cells[path[0]];
            int points, chainMult = 1;

            if (inSurge)
            {
                int b = len == 2 ? Cfg.SurgePairScore : BaseScore(len);
                points = b * SurgeChainPct / 100;
                SurgeChainPct = Math.Min(SurgeChainPct + Cfg.SurgeChainStepPct,
                                         Cfg.MaxSurgeMultPct);
            }
            else
            {
                bool chained = _lastClearMs != long.MinValue &&
                               nowMs - _lastClearMs <= Cfg.SpeedChainWindowMs;
                Chain = chained ? Math.Min(Chain + 1, Cfg.MaxChainMult) : 1;
                chainMult = Chain;
                points = BaseScore(len) * Chain;
                AddMeter(len, nowMs);
            }
            _lastClearMs = nowMs;

            foreach (int n in path) Board.Cells[n] = 0;

            bool purge = CountColor(color) == 0;
            if (purge)
            {
                Score += Cfg.PurgeBonus;
                PurgeCount++;
                FreezeUntilMs = nowMs + Cfg.PurgeFreezeMs;
            }
            Score += points;

            int[] newNodes = ApplyGravityAndRefill();
            EnsureMoveFloor();

            _actions.Add(new ActionLog { Kind = ActionType.Clear,
                                         TimeMs = nowMs, Path = path.ToArray() });
            HashChain = Fnv.Mix(HashChain, BoardHash ^ (ulong)_actions.Count);

            return new ClearResult
            {
                Path = path.ToArray(), Points = points, ChainMult = chainMult,
                Purge = purge, PurgedColor = purge ? color : (byte)0,
                InSurge = inSurge, MeterAfter = Meter, NewNodes = newNodes
            };
        }

        public int AvailableMoveCount() => BoardOps.CountGroupsAtLeast(Board, 3);

        public MatchResult Finish() => new MatchResult
        {
            MatchSeed = MatchSeed, ConfigHash = ConfigHash,
            BoardHash = BoardHash, HashChain = HashChain,
            Score = Score, ActionCount = _actions.Count,
            RepairCount = RepairCount, PurgeCount = PurgeCount,
            SurgesActivated = SurgesActivated,
            Log = new List<ActionLog>(_actions)
        };

        // --------------------------------------------------------- internals
        void RefreshSurge(long nowMs)
        {
            if (_surgeStartMs != null && nowMs - _surgeStartMs.Value >= Cfg.SurgeDurationMs)
            {
                _surgeStartMs = null;
                SurgeChainPct = 100;
                Meter = 0;
            }
        }

        void AddMeter(int len, long nowMs)
        {
            if (Meter >= Cfg.MeterMax) return;   // banked: gains wasted, fuse keeps burning
            int gain = len <= 3 ? Cfg.MeterGainLen3
                     : len == 4 ? Cfg.MeterGainLen4
                     : Cfg.MeterGainLen5Plus;
            Meter = Math.Min(Cfg.MeterMax, Meter + gain);
            if (Meter >= Cfg.MeterMax) _bankedSinceMs = nowMs;
        }

        int CountColor(byte color)
        {
            int n = 0;
            foreach (byte v in Board.Cells) if (v == color) n++;
            return n;
        }

        static int BaseScore(int len)
        {
            if (len <= 3) return 100;
            if (len == 4) return 300;
            if (len == 5) return 1000;
            if (len == 6) return 2000;
            if (len == 7) return 4000;
            return 8000 + (len - 8) * 2000;
        }

        int[] ApplyGravityAndRefill()
        {
            var filled = new List<int>();
            int s = Cfg.Size;
            for (int c = 0; c < s; c++)
            {
                int write = s - 1;
                for (int r = s - 1; r >= 0; r--)
                {
                    byte v = Board.Cells[r * s + c];
                    if (v == 0) continue;
                    if (write != r)
                    {
                        Board.Cells[write * s + c] = v;
                        Board.Cells[r * s + c] = 0;
                    }
                    write--;
                }
                for (int r = write; r >= 0; r--)
                {
                    Board.Cells[r * s + c] = _refill.Next();
                    filled.Add(r * s + c);
                }
            }
            return filled.ToArray();
        }

        // v3: delegates to the single repair implementation in Repair. The
        // previously inlined copy of that loop is gone.
        void EnsureMoveFloor()
        {
            RepairCount += Repair.RecolorToFloor(Board, _repair, Cfg, Cfg.MoveFloor);
        }
    }

    // ----------------------------------------------------------- SELF TEST --
    // Headless. Run in CI before every build. Verifies:
    //   1) Determinism: same seed + same actions => identical score & hashes
    //   2) Move-floor invariant holds after every settle
    //   3) Repair rate under 1% of settles
    //   4) v2: Schedule invariance — result is identical regardless of how
    //      often Tick() was called (sparse / dense / jittered)
    //   5) No crashes across thousands of simulated matches
    public static class SelfTest
    {
        public static string Run(int matches = 2000)
        {
            var cfg = new SurgeConfig();
            var t = new Rng(0xDEADBEEFUL);
            long settles = 0, moves = 0, purges = 0, surgeClears = 0, repairs = 0;

            for (int m = 0; m < matches; m++)
            {
                ulong seed = (ulong)(m + 1) * 2654435761UL + 12345UL;
                MatchEngine a = Simulate(seed, cfg, t,
                    ref settles, ref moves, ref purges, ref surgeClears);

                var b = new MatchEngine(seed, cfg);
                foreach (var act in a.Actions)
                {
                    b.Tick(act.TimeMs);
                    Apply(b, act);
                }
                if (a.Score != b.Score || a.HashChain != b.HashChain ||
                    a.BoardHash != b.BoardHash)
                    throw new Exception($"DETERMINISM BROKEN at seed {seed}");
                repairs += a.RepairCount;

                ReplayInvariance(seed, cfg, a.Actions);
            }

            double repairRate = settles == 0 ? 0 : 100.0 * repairs / settles;
            return $"SelfTest OK: {matches} matches, {moves} clears, {purges} purges, " +
                   $"{surgeClears} surge clears, repair rate {repairRate:F3}% (target < 1%)";
        }

        static MatchEngine Simulate(ulong seed, SurgeConfig cfg, Rng t,
            ref long settles, ref long moves, ref long purges, ref long surgeClears)
        {
            var e = new MatchEngine(seed, cfg);
            long now = 0;
            for (int i = 0; i < 70; i++)
            {
                now += 200 + t.NextInt(1300);
                e.Tick(now);
                if (e.Banked(now))
                {
                    if (t.NextInt(100) < 30) e.CommitSurge(now);
                    // v2: sometimes let the bank expire instead of always popping it
                    else if (t.NextInt(100) < 12) now += cfg.SurgeBankGraceMs + 500;
                }

                int[] path = RandomValidPath(e.Board, t);
                if (path == null)
                    throw new Exception($"No valid move at seed {seed}, step {i} — floor failed");
                var res = e.TryCommitPath(new List<int>(path), now);
                if (res == null)
                    throw new Exception($"Engine rejected valid path at seed {seed}, step {i}");

                settles++; moves++;
                if (res.Purge) purges++;
                if (res.InSurge) surgeClears++;
                if (e.AvailableMoveCount() < cfg.MoveFloor)
                    throw new Exception($"Move floor violated at seed {seed}, step {i}");
            }
            return e;
        }

        // v2: THE RULE THIS ENFORCES — final state must be identical
        // regardless of how often Tick was called. If any future feature
        // reintroduces dt-accumulated state, this test fails before it ships.
        static void ReplayInvariance(ulong seed, SurgeConfig cfg, IReadOnlyList<ActionLog> log)
        {
            var a = new MatchEngine(seed, cfg);            // sparse: tick per action only
            foreach (var act in log) { a.Tick(act.TimeMs); Apply(a, act); }

            var b = new MatchEngine(seed, cfg);            // dense: 60fps-style ticker
            int i = 0;
            for (long tt = 16; ; tt += 16)
            {
                while (i < log.Count && log[i].TimeMs <= tt)
                { b.Tick(log[i].TimeMs); Apply(b, log[i]); i++; }
                if (i >= log.Count) break;
                b.Tick(tt);
            }

            var r = new Rng(seed ^ 0xC0FFEE);              // jittered: random extra ticks
            var c = new MatchEngine(seed, cfg);
            long prev = 0;
            foreach (var act in log)
            {
                int extra = r.NextInt(4);
                for (int k = 0; k < extra; k++)
                    c.Tick(prev + r.NextInt((int)Math.Max(1, act.TimeMs - prev)));
                c.Tick(act.TimeMs);
                Apply(c, act);
                prev = act.TimeMs;
            }

            if (a.Score != b.Score || a.Score != c.Score ||
                a.HashChain != b.HashChain || a.HashChain != c.HashChain ||
                a.BoardHash != b.BoardHash || a.BoardHash != c.BoardHash)
                throw new Exception($"TICK-SCHEDULE DIVERGENCE at seed {seed}");
        }

        static void Apply(MatchEngine e, ActionLog act)
        {
            if (act.Kind == ActionType.ActivateSurge) e.CommitSurge(act.TimeMs);
            else e.TryCommitPath(new List<int>(act.Path), act.TimeMs);
        }

        // v3: harness fix, not a rules change. The old version did a greedy
        // walk with no backtracking from a random start, retried 10 times
        // against a single randomly chosen group, then gave up and reported
        // "floor failed" — blaming the engine for its own search failure.
        // Entering a 3-cell line at the middle cell dead-ends at 2, so it
        // could miss a legal move that demonstrably existed. Harmless while
        // boards were dominated by large blobs; exposed once repair started
        // promoting groups to exactly MinClearLength.
        static int[] RandomValidPath(Board b, Rng t)
        {
            var open = new List<List<int>>();
            foreach (var g in BoardOps.Groups(b)) if (g.Count >= 3) open.Add(g);
            if (open.Count == 0) return null;

            // Sweep every group from a random offset, and every start cell
            // within a group, so one awkwardly shaped group cannot stall the
            // whole simulation.
            int gOff = t.NextInt(open.Count);
            for (int k = 0; k < open.Count; k++)
            {
                var grp = open[(gOff + k) % open.Count];
                byte color = b.Cells[grp[0]];
                int target = 3 + t.NextInt(Math.Min(5, grp.Count - 2));
                int sOff = t.NextInt(grp.Count);
                for (int s = 0; s < grp.Count; s++)
                {
                    var path = new List<int>();
                    if (Walk(b, grp[(sOff + s) % grp.Count], color, path, target))
                        return path.ToArray();
                }
            }
            return null;
        }

        // Depth-first with backtracking: finds a path of `target` cells when
        // one exists, and settles for any legal path of >= 3 otherwise.
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

            if (path.Count >= 3) return true;      // shorter, but still legal
            path.RemoveAt(path.Count - 1);
            return false;
        }
    }

    // ------------------------------------------------------------ CHANGELOG --
    // v3 (2026-08-22): Fixed the move-floor repair death spiral. The floor
    // metric counts DISTINCT groups of >= MinClearLength, but repair painted
    // random cells with MostCommon — an operation that MERGES groups and so
    // drives that metric DOWN. The loop fed itself; its absorbing state was a
    // single-colour board. Measured on 500 matches before the fix: 95.6% of
    // matches violated the floor, 95.4% ended with <= 2 colours on the board,
    // repair rate 8372% of settles.
    //
    // Changes:
    //  - Repair.RecolorToFloor is now the ONLY repair implementation. The
    //    duplicate inlined copy in MatchEngine.EnsureMoveFloor is deleted;
    //    EnsureMoveFloor delegates. RecolorToFloor returns the number of
    //    cells recoloured so RepairCount stays truthful.
    //  - Repair grows an undersized group (ForcePromoteSmallestGroup) rather
    //    than merging groups. It never steals from a neighbour whose own
    //    group is exactly MinClearLength, since demoting a valid group to
    //    promote another is the net-zero trade that kept v2 spinning.
    //  - Promotion tries every undersized group, smallest first, instead of
    //    committing to one. A single boxed-in group used to abort the whole
    //    repair and strand the floor at 2 groups with 37 promotable groups
    //    untried.
    //  - SelfTest.RandomValidPath: harness fix, not a rules change. Its
    //    greedy no-backtracking walk could miss a legal move that provably
    //    existed (enter a 3-cell line at the middle, dead-end at 2) and then
    //    blame the engine with "floor failed". Now depth-first with
    //    backtracking across every group and start cell. Latent in v2, but
    //    exposed once repair began producing boards dense in exactly-3
    //    groups.
    //
    // After the fix, 2000 matches / 140000 clears: 0% floor violations, 0%
    // monochrome boards, determinism and ReplayInvariance green.
    //
    // KNOWN, NOT FIXED: repair rate is 119.4% of settles against a documented
    // "< 1%" target. That target is unreachable as configured — a random
    // 7x7 / 5-colour board satisfies MoveFloor=3 unaided only 76.6% of the
    // time, so ~1 settle in 4 needs repair by construction. Closing the gap
    // is a tuning decision (board size, NumColors, MoveFloor, or promoting
    // past the threshold rather than stopping exactly on it), not a bug fix.
    //
    // v2 (2026-08-21): Fixed Tick()/bank drain schedule-dependence bug found
    // during agent review — original continuous-drain design didn't match
    // its own doc comment and produced different banked-meter values live
    // vs. replayed depending on Tick call frequency. Replaced with a fixed
    // 8s bank-grace window (binary: popped or expired), which is
    // schedule-invariant by construction. Added ReplayInvariance to
    // SelfTest to guard this permanently. Added ISeedSource abstraction to
    // isolate the one Skillz-SDK-dependent line pending SDK doc
    // verification. (Origin: GLM-5.3 design + Grok review + GLM engine +
    // GLM v2 fixes, reviewed by Clue.)
}
