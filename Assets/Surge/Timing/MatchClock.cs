// ============================================================================
// SURGE — MatchClock
//
// The single authority for game time. Every timestamp the engine ever sees —
// Tick, TryCommitPath, CommitSurge, Banked, SurgeActive — comes from here and
// from nowhere else. Nothing else may read UnityEngine.Time; the moment two
// sources of "now" exist, replay diverges from live and the whole determinism
// contract is worthless.
//
// Pure C#, integer milliseconds, no Unity references. MatchDriver feeds it
// real elapsed time; this class decides what that means for game time.
//
// ---------------------------------------------------------------- FREEZE --
// SurgeCore documents ActionLog.TimeMs as "game-clock ms (pauses during purge
// freeze)". That pause is the whole reason this class exists.
//
// A purge is a celebration: the board flashes, the player watches. If game
// time kept running through it, the 1500ms speed-chain window would quietly
// expire behind the animation and the player would lose their chain to a
// reward. So game time HOLDS while the celebration plays out in real time.
//
// THE TRAP: MatchEngine.FreezeUntilMs is stamped as `nowMs + PurgeFreezeMs`
// in GAME-CLOCK units, and the engine never reads it back — it is advisory
// output for the presentation layer only. Because game time is frozen, NowMs
// can never reach FreezeUntilMs. Polling `NowMs >= FreezeUntilMs` to decide
// when the freeze ends is therefore an infinite freeze. The freeze is owned
// here and counted in REAL time. Use IsFrozen, never FreezeUntilMs.
//
// ------------------------------------------------------ SCHEDULE-INVARIANT --
// Game time is exactly (real time fed) - (real time frozen) - (real time
// paused), independent of how the real time was chunked. One 100ms delta and
// ten 10ms deltas produce the same NowMs, including across a freeze boundary,
// because leftover time past the end of a freeze carries into game time
// rather than being dropped. This mirrors the guarantee SurgeCore's
// ReplayInvariance enforces on Tick, and MatchClockSelfTest fuzzes it.
// ============================================================================

namespace Surge.Timing
{
    public sealed class MatchClock
    {
        long _nowMs;
        long _freezeRemainingMs;
        bool _paused;

        /// Game-clock milliseconds. The only value the engine should be given.
        public long NowMs => _nowMs;

        /// True while a purge freeze is holding game time.
        public bool IsFrozen => _freezeRemainingMs > 0;

        /// True while the match is suspended (app backgrounded, or explicitly
        /// paused). Real time passed while paused is discarded, not banked.
        public bool IsPaused => _paused;

        /// Real milliseconds left on the current freeze; 0 when not frozen.
        /// Drive the celebration animation from this.
        public long FreezeRemainingMs => _freezeRemainingMs;

        /// Feeds real elapsed time. Non-positive deltas are ignored, so the
        /// clock is monotonic even if the underlying source ever steps back.
        public void Advance(long realDeltaMs)
        {
            if (_paused || realDeltaMs <= 0) return;

            if (_freezeRemainingMs > 0)
            {
                if (realDeltaMs <= _freezeRemainingMs)
                {
                    _freezeRemainingMs -= realDeltaMs;
                    return;                       // still frozen; game time held
                }
                // Freeze ended partway through this delta. The remainder must
                // carry into game time — dropping it would make the clock
                // depend on frame boundaries, which is exactly the class of
                // bug that broke the v1 bank drain.
                realDeltaMs -= _freezeRemainingMs;
                _freezeRemainingMs = 0;
            }

            _nowMs += realDeltaMs;
        }

        /// Holds game time for `durationMs` of REAL time. Call on purge with
        /// SurgeConfig.PurgeFreezeMs. Never shortens a freeze already running.
        public void Freeze(long durationMs)
        {
            if (durationMs > _freezeRemainingMs) _freezeRemainingMs = durationMs;
        }

        /// Suspends the match. Real time passing while paused is discarded:
        /// backgrounding the app must not burn the player's bank fuse.
        public void Pause() => _paused = true;

        public void Resume() => _paused = false;

        /// Returns the clock to match start.
        public void Reset()
        {
            _nowMs = 0;
            _freezeRemainingMs = 0;
            _paused = false;
        }
    }
}
