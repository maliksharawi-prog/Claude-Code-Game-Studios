# Sweet Cascade — Playable Slice v2

**Status: BUILT & VERIFIED (23/23 headless checks, 2026-07-18) — awaiting founder playtest**

A design-validation reference build of the full MVP loop, playable in any
browser (open `index.html`, no install; touch and mouse). Also published as
a private artifact for phone play.

## What it implements (and which GDD governs each part)

| Feature | Governing doc |
|---|---|
| 10 balanced levels, saw-tooth difficulty, masked/7×7/9×9 boards | `design/gdd/level-data-format.md` + scoring framework thresholds |
| Swap/match/cascade state machine, column-segment gravity, reshuffle | `design/gdd/board-engine.md` (APPROVED) |
| Striped H/V (same-axis), Color Bomb, 4-cell combo matrix, deterministic passive detonation | `design/gdd/special-candies.md` |
| Point formula: chain × (20×pieces + per-piece bonuses 60/180); framework star thresholds | `design/gdd/scoring-stars.md` |
| Win at stabilization when objectives met; move matrix; unified collect counting | `design/gdd/level-objectives.md` |
| Swipe + tap-tap input, DPI-scaled threshold, busy-drop policy | `design/gdd/touch-input.md` (APPROVED) |
| Map → pre-level card → gameplay → results flow; retry skips card | `design/gdd/screen-flow.md` |
| Sequential unlock, star records on map | `design/gdd/world-map.md` |
| localStorage profile v1 (stars/best/settings), monotonic best | `design/gdd/save-persistence.md` |
| SVG candy sprites (shape+color double-coding), particles, callout ladder | `design/art/art-bible.md` |
| Cascade pitch-escalating WebAudio SFX, reduced-motion support | `design/gdd/juice-layer.md` |
| Fizz mascot + line pools (intro/win/lose/stars) | `design/narrative/characters-and-tone.md` |

## Deliberate deltas from the concept prototype

- Same-axis striped orientation (was arbitrary-perpendicular)
- No solo-striped firing on swap — must be matched (specials GDD scope cut)
- Passive bombs detonate deterministically (most-common color) instead of randomly
- Objectives/move-limit win-lose evaluation (was score-target-only)

## Known limitations (by design, this is not production)

- Godot 4.6/GDScript remains the production target; this file is throwaway
  reference code and is never refactored into production (prototype rule).
- Juice is a thin subset of `juice-layer.md` (no Shadow Board Model — logic
  and presentation share state here; fine for validation, wrong for production).
- No lives, no brewing, no events, no social — per MVP scope.

## Verification

Headless Chromium suite (scratchpad `test_slice.js`): flow, swap semantics,
same-axis stripe spawn fixture, bomb activation fixture, full L1 bot
playthrough (win, stars recorded), reload persistence, progression unlock,
masked-board integrity, 9×9 boot, zero console errors. 23/23 PASS.
