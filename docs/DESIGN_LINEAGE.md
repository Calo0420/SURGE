# SURGE — Design & Engineering Lineage

Origin trail, for anyone (human or agent) who needs to understand why a
decision was made the way it was.

1. **GLM-5.3** — original concept and full Game Design Document (elevator
   pitch, core loop, scoring, Surge Mode, monetization).
2. **Grok** — design review. Endorsed the zero-RNG / mirrored-board / "loss =
   my hesitation" philosophy; flagged cascade readability, input precision,
   Surge-trigger "luck" perception, and match length as friction points.
3. **GLM-5.3** — deterministic engine (`SurgeCore.cs` v1). Resolved the
   doc's gravity contradiction, redefined Purge as "contains every node of
   that color currently on the board," specified the refill/move-floor/
   repair system, and proposed the Orca agent split (Core / Input / Juice /
   Skillz / Analyzer).
4. **Calo's agent (reviewing v1)** — flagged three issues: a Tick/bank
   schedule-dependence bug, a missing Skillz seed-source spec, and an
   undefined wire format for match results.
5. **GLM-5.3 (v2 fixes)** — replaced continuous meter drain with a fixed
   8-second bank-grace window (schedule-invariant by construction), added
   the `ISeedSource` abstraction (flagging the exact Skillz SDK API as
   unverified pending doc check), and defined the internal replay/wire
   format — noting Skillz's actual contract is just `ReportScore(int)`, so
   the replay JSON is internal tooling, not something to match externally.
6. **Clue (reviewing v2)** — merged the v2 patch into a single
   `SurgeCore.cs`, confirmed the fix design is correct, and flagged one
   open action item for the Skillz agent (see below).

## Open action items before this touches Unity

- **Verify `SkillzCrossPlatform.Random` (or current equivalent) against the
  live Skillz Unity SDK docs.** This is the one line in `SurgeCore.cs`
  (`ISeedSource` / `TestSeedSource`) that is explicitly a placeholder
  pending that check — do not wire a real `SkillzSeedSource` without
  confirming the exact API surface first.
- Everything else in the file is considered locked core per the agent
  split table in v1 — nobody but the Core agent edits `SurgeCore.cs`
  directly; everyone else files change requests.
