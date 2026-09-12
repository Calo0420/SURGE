// ============================================================================
// SURGE — MatchDriver
//
// The seam between Unity and the deterministic core. It owns the MatchEngine
// and the MatchClock, and it is the ONLY place in the project permitted to
// read wall-clock time. Gameplay, UI and VFX ask this class for NowMs; they
// never touch UnityEngine.Time themselves.
//
// Why Stopwatch instead of Time.deltaTime / Time.unscaledDeltaTime:
//   - Time.deltaTime is float seconds. Accumulating floats into an integer
//     millisecond clock drifts, and drift is indistinguishable from cheating
//     when two clients are compared.
//   - Time.deltaTime is scaled by Time.timeScale. Anything that pokes
//     timeScale (a pause menu, a hit-stop effect, a tween library) would
//     silently rewrite match timing.
//   - Stopwatch is monotonic, integer, and immune to both.
// ============================================================================

using System.Collections.Generic;
using System.Diagnostics;
using Surge.Timing;
using SurgeCore;
using UnityEngine;

namespace Surge.Runtime
{
    [DisallowMultipleComponent]
    public sealed class MatchDriver : MonoBehaviour
    {
        [Header("Match Settings")]
        [Tooltip("Match duration in seconds. Skillz tournament standard is 90s.")]
        [SerializeField] int matchDurationSeconds = 90;

        readonly Stopwatch _realTime = new Stopwatch();
        readonly MatchClock _clock = new MatchClock();

        MatchEngine _engine;
        long _lastRealMs;

        public MatchEngine Engine => _engine;
        public bool MatchRunning => _engine != null;

        public int MatchDurationSeconds
        {
            get => matchDurationSeconds;
            set => matchDurationSeconds = Mathf.Max(1, value);
        }

        public long MatchDurationMs => matchDurationSeconds * 1000L;

        /// Real milliseconds remaining in the match. Clamped at 0.
        public long RemainingMs =>
            _engine == null ? MatchDurationMs : System.Math.Max(0L, MatchDurationMs - NowMs);

        /// Whole seconds remaining in the match (ceiled so 0.1s shows as 1s).
        public int RemainingSeconds => (int)((RemainingMs + 999L) / 1000L);

        /// 0 at match start, 1 at match end.
        public float MatchProgress =>
            MatchDurationMs <= 0 ? 1f : Mathf.Clamp01((float)NowMs / MatchDurationMs);

        /// Current match score from the engine (0 if match not running).
        public int Score => _engine?.Score ?? 0;

        /// Current Surge meter fill (0..100).
        public int Meter => _engine?.Meter ?? 0;

        /// Current combo chain multiplier (1..10).
        public int Chain => _engine?.Chain ?? 1;

        /// Raised when the match clock expires or EndMatch is called.
        public event System.Action<MatchResult> MatchEnded;

        /// Game-clock milliseconds. The single source of "now" for the whole
        /// project. Read this; never UnityEngine.Time.
        public long NowMs => _clock.NowMs;

        /// True while a purge celebration is holding game time. Input is
        /// refused and the board should be showing the purge VFX.
        public bool IsFrozen => _clock.IsFrozen;

        /// Real milliseconds left on the purge freeze — drive the celebration
        /// from this, not from MatchEngine.FreezeUntilMs, which is stamped in
        /// game-clock units the frozen clock will never reach.
        public long FreezeRemainingMs => _clock.FreezeRemainingMs;

        // ------------------------------------------------------- lifecycle --
        /// Starts a match. The seed source is injected so the editor and CI
        /// use TestSeedSource; the Skillz source stays unwired until its SDK
        /// surface is verified (see docs/DESIGN_LINEAGE.md).
        public void BeginMatch(ISeedSource seedSource, SurgeConfig cfg = null)
        {
            cfg = cfg ?? new SurgeConfig();
            _engine = new MatchEngine(seedSource.DeriveMatchSeed(), cfg);

            _clock.Reset();
            _realTime.Restart();
            _lastRealMs = 0;
        }

        public MatchResult EndMatch()
        {
            if (_engine == null) return null;
            MatchResult result = _engine.Finish();
            _engine = null;
            _realTime.Stop();
            MatchEnded?.Invoke(result);
            return result;
        }

        // ------------------------------------------------------------ loop --
        void Update()
        {
            if (_engine == null) return;

            long realMs = _realTime.ElapsedMilliseconds;
            _clock.Advance(realMs - _lastRealMs);
            _lastRealMs = realMs;

            // Tick is schedule-invariant (SurgeCore ReplayInvariance), so
            // calling it every frame is safe and keeps banked-meter expiry and
            // Surge expiry observable to the UI without extra bookkeeping.
            _engine.Tick(_clock.NowMs);

            // Match countdown expiration (e.g. 90s)
            if (NowMs >= MatchDurationMs)
            {
                EndMatch();
            }
        }

        // ------------------------------------------------------ suspension --
        public void PauseMatch() => SetSuspended(true);
        public void ResumeMatch() => SetSuspended(false);
        public bool IsPaused => _clock.IsPaused;

        void OnApplicationPause(bool paused) => SetSuspended(paused);
        void OnApplicationFocus(bool focused) => SetSuspended(!focused);

        void SetSuspended(bool suspended)
        {
            if (_engine == null) return;

            if (suspended)
            {
                _clock.Pause();
                return;
            }

            // Discard everything that elapsed while backgrounded. Without this
            // resync the whole gap arrives as one delta on the next frame and
            // instantly burns the player's bank fuse.
            _lastRealMs = _realTime.ElapsedMilliseconds;
            _clock.Resume();
        }

        // ---------------------------------------------------- player input --
        /// Commits a clear at the current game time. Returns null if the path
        /// is illegal or the board is frozen.
        public ClearResult TryClear(List<int> path)
        {
            if (_engine == null || _clock.IsFrozen) return null;

            ClearResult result = _engine.TryCommitPath(path, _clock.NowMs);
            if (result == null) return null;

            // Hold game time so the celebration cannot eat the speed-chain
            // window. The engine only records FreezeUntilMs; enforcing it is
            // this layer's job.
            if (result.Purge) _clock.Freeze(_engine.Cfg.PurgeFreezeMs);

            return result;
        }

        /// Pops a banked meter at the current game time.
        public bool TryCommitSurge()
        {
            if (_engine == null || _clock.IsFrozen) return false;
            return _engine.CommitSurge(_clock.NowMs);
        }

        // ------------------------------------------------------ UI readouts --
        public bool Banked => _engine != null && _engine.Banked(_clock.NowMs);

        public long BankFuseRemainingMs =>
            _engine == null ? 0 : _engine.BankFuseRemainingMs(_clock.NowMs);

        public bool SurgeActive => _engine != null && _engine.SurgeActive(_clock.NowMs);
    }
}
