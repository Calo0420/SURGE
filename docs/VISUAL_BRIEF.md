# SURGE — Visual Direction & Asset Brief (for Copilot / Unity work)

Goal: premium-feeling neon arcade puzzle screen, built mostly from free Asset
Store sourcing + iteration, not from a big art budget. The budget is time
(Copilot/agent cycles) and taste (iteration passes), not dollars.

## Reference feel

Dark, near-black board (not pure black — subtle depth). Thick, saturated
neon node colors with a soft glow, not flat cartoon fills. HUD text is
clean sans-serif, high contrast, minimal chrome. Think: a well-produced
rhythm/arcade game (Beat Saber menu screens, Tetris Effect's neon-on-black
aesthetic) rather than a flat casual-puzzle look (Candy Crush, Bejeweled).
This is the difference between "mobile puzzle game" and "esport."

## Asset Store sourcing (do this first — cheap, fast base layer)

Search categories on the Unity Asset Store, filtered to free:
- **Particle packs**: "neon particles," "glow VFX," "arcade FX" — for clear
  bursts, Surge Mode activation, purge freeze effect
- **UI kits**: "neon UI," "sci-fi UI," "game HUD" — for score/timer/meter
  chrome, panel frames, button states
- **Icon/shape packs**: simple geometric node shapes (diamonds, hexagon
  outlines, gems) — remember: visuals can imply hex, but hitboxes are
  4-way square-adjacent per the engine spec, so pick shapes that read as
  "colored gem" rather than literal hex tiles a player might expect
  6-neighbor behavior from
- **Sound**: "synthwave loop," "arcade SFX pack" — matches the driving
  BPM-scaling soundtrack call from the original design doc

Pull 3-5 candidate packs per category before committing to one — compare
in-engine, not from store thumbnails, since Asset Store preview images
oversell more often than not.

## Customization pass (this is where "premium" actually comes from)

Free assets read as free if left untouched. The polish budget goes here,
not into buying pricier assets:

1. **Recolor to the palette.** Lock 5 node colors (or however many
   `NumColors` ends up being) as hex values shared across VFX, UI accents,
   and node fills — one source of truth, not five packs each guessing
   their own neon palette.
2. **Retime, don't just reskin, particle effects.** A stock "burst" effect
   at its default timing reads as stock. Adjust duration, easing, and
   scale curves to match the game's actual pacing — a 3-node clear burst
   should feel smaller/faster than a 5+ chain burst, even using the same
   base particle asset.
3. **Screen shake and haptic sync.** Small, cheap, and the single highest
   perceived-quality-per-hour investment: a few pixels of shake + a haptic
   tick on every clear, scaled up for chains and Surge. This is iteration
   time, zero asset cost.
4. **Glow consistency.** Whatever glow technique gets picked (bloom
   post-process, sprite-based glow, shader), apply it uniformly across
   nodes, UI accents, and VFX — inconsistent glow (some elements glowing,
   others flat) is the fastest tell that assets were bolted on rather than
   art-directed.

## Feedback loop

Once a build has actual in-engine screenshots or a short capture: bring
them back for a visual critique pass (composition, color balance, contrast,
"does this read as premium or as free-asset-default") before sinking more
iteration time into a direction that might need bigger changes.

## Explicit non-goals

- Not building custom 3D models or hand-painted textures from scratch —
  that's a different budget entirely and not where this project's money
  should go right now.
- Not chasing pixel-perfect uniqueness over shipping — "good enough that it
  doesn't look like a stock template" is the bar, not "no one has ever seen
  this asset before."
