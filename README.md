# SURGE

**A neon cascade puzzle game, built for the Skillz competitive gaming platform.**

---

## About

SURGE is a match/cascade puzzle game where board clears chain into combos across a colorful neon aesthetic. It's built around **SurgeCore**, a fully deterministic game engine — every match, cascade, and board state is reproducible from a seed, which is the core requirement for fair, verifiable competitive play on Skillz.

## Status

In active development.

- **Engine:** SurgeCore — deterministic core verified via large-scale headless simulation (90,000+ simulated board settles with zero seed-mismatch failures)
- **Presentation layer:** Board rendering and input pipeline verified against the live engine (clears match engine state 1:1, no visual/logic drift)
- **Skillz integration:** Registered (Game ID 103146), SDK integration in progress

## Tech Stack

- **Engine:** Unity
- **Language:** C#
- **Platform target:** Skillz (competitive real-money and free-play tournaments)
- **Core architecture:** Deterministic seeded RNG, engine/presentation separation (SurgeCore drives logic, BoardView renders it — no gameplay logic in the view layer)

## Why Determinism Matters Here

Skillz tournaments pit players against the same board conditions to determine a fair winner. That only works if the exact same sequence of moves on the exact same seed always produces the exact same result — no floating-point drift, no hidden randomness, no client/server disagreement. SurgeCore is built and tested around that guarantee first, with visuals layered on top.

---

*Part of an ongoing multi-title indie game development effort (Reyeso Studio / Game Factory).*
