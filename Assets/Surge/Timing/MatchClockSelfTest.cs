// ============================================================================
// SURGE — MatchClock self-test. Headless, no Unity, same shape as
// SurgeCore.SelfTest: throws on failure, returns a report string on success.
// Run from CI and from the Surge/Verify editor menu.
// ============================================================================

using System;
using SurgeCore;

namespace Surge.Timing
{
    public static class MatchClockSelfTest
    {
        public static string Run(int fuzzCases = 5000)
        {
            Monotonic();
            FreezeHoldsGameTime();
            FreezeCarriesSurplus();
            PauseDiscardsRealTime();
            PurgeFreezePreservesChainWindow();
            GranularityInvariance(fuzzCases);

            return $"MatchClockSelfTest OK: 6 properties, {fuzzCases} granularity " +
                   "fuzz cases";
        }

        static void Check(bool ok, string what)
        {
            if (!ok) throw new Exception("MatchClock: " + what);
        }

        // A clock that can step backwards would let the engine see a timestamp
        // earlier than one already in the action log.
        static void Monotonic()
        {
            var c = new MatchClock();
            long prev = c.NowMs;
            foreach (long d in new long[] { 16, 0, -50, 33, -1, 100 })
            {
                c.Advance(d);
                Check(c.NowMs >= prev, $"went backwards after Advance({d})");
                prev = c.NowMs;
            }
            Check(c.NowMs == 149, $"expected 149 after mixed deltas, got {c.NowMs}");
        }

        static void FreezeHoldsGameTime()
        {
            var c = new MatchClock();
            c.Advance(1000);
            long before = c.NowMs;

            c.Freeze(1500);
            Check(c.IsFrozen, "not frozen after Freeze(1500)");

            c.Advance(1500);                       // exactly the freeze duration
            Check(c.NowMs == before, $"game time moved during freeze: {before} -> {c.NowMs}");
            Check(!c.IsFrozen, "still frozen after the full duration elapsed");

            c.Advance(200);
            Check(c.NowMs == before + 200, "clock did not resume after freeze");
        }

        // The remainder of a delta that straddles the end of a freeze must
        // reach game time, or the clock would depend on frame boundaries.
        static void FreezeCarriesSurplus()
        {
            var c = new MatchClock();
            c.Freeze(100);
            c.Advance(250);                        // 100 frozen, 150 should land
            Check(c.NowMs == 150, $"surplus lost across freeze end: got {c.NowMs}, want 150");
            Check(!c.IsFrozen, "freeze did not end");
        }

        static void PauseDiscardsRealTime()
        {
            var c = new MatchClock();
            c.Advance(500);
            c.Pause();
            c.Advance(60000);                      // a minute backgrounded
            Check(c.NowMs == 500, $"paused clock advanced to {c.NowMs}");
            c.Resume();
            c.Advance(100);
            Check(c.NowMs == 600, $"clock wrong after resume: {c.NowMs}");
        }

        // The reason the freeze exists at all: a purge celebration must not
        // burn the player's speed-chain window. Two clears separated by a full
        // purge freeze plus real animation time must still read as chained.
        static void PurgeFreezePreservesChainWindow()
        {
            var cfg = new SurgeConfig();
            var c = new MatchClock();

            c.Advance(1000);
            long firstClear = c.NowMs;

            c.Freeze(cfg.PurgeFreezeMs);           // purge fires
            c.Advance(cfg.PurgeFreezeMs);          // celebration plays out
            c.Advance(400);                        // player reacts

            long secondClear = c.NowMs;
            long gameGap = secondClear - firstClear;
            long realGap = cfg.PurgeFreezeMs + 400;

            Check(gameGap == 400, $"game-clock gap was {gameGap}, want 400");
            Check(gameGap <= cfg.SpeedChainWindowMs,
                  $"purge freeze broke the chain window: {gameGap} > {cfg.SpeedChainWindowMs}");
            Check(realGap > cfg.SpeedChainWindowMs,
                  "test is vacuous: real gap did not exceed the chain window");
        }

        // The property that matters most, and the one that broke in v1's bank
        // drain: total game time must not depend on how real time was chunked.
        static void GranularityInvariance(int cases)
        {
            var rng = new Rng(0x5EED1234UL);

            for (int i = 0; i < cases; i++)
            {
                long totalReal = 1 + rng.NextInt(20000);
                long freezeAt = rng.NextInt((int)totalReal);
                long freezeFor = rng.NextInt(3000);

                long coarse = RunSchedule(totalReal, freezeAt, freezeFor, 0, rng);
                long fine = RunSchedule(totalReal, freezeAt, freezeFor, 1, rng);
                long jitter = RunSchedule(totalReal, freezeAt, freezeFor, 2, rng);

                if (coarse != fine || coarse != jitter)
                    throw new Exception(
                        $"MatchClock: granularity divergence (real {totalReal}, " +
                        $"freeze {freezeFor} at {freezeAt}): " +
                        $"coarse {coarse}, fine {fine}, jitter {jitter}");
            }
        }

        // mode 0: one delta per phase. 1: 1ms steps. 2: random chunks.
        static long RunSchedule(long totalReal, long freezeAt, long freezeFor,
                                int mode, Rng rng)
        {
            var c = new MatchClock();
            Feed(c, freezeAt, mode, rng);
            c.Freeze(freezeFor);
            Feed(c, totalReal - freezeAt, mode, rng);
            return c.NowMs;
        }

        static void Feed(MatchClock c, long ms, int mode, Rng rng)
        {
            if (ms <= 0) return;
            if (mode == 0) { c.Advance(ms); return; }

            long fed = 0;
            while (fed < ms)
            {
                long step = mode == 1 ? 1 : 1 + rng.NextInt(97);
                if (fed + step > ms) step = ms - fed;
                c.Advance(step);
                fed += step;
            }
        }
    }
}
