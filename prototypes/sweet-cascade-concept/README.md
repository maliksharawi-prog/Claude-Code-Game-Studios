# Sweet Cascade — Concept Prototype

**Status: BUILT — awaiting user playtest verdict (PROCEED / PIVOT / KILL)**

- **Concept**: `design/gdd/game-concept.md`
- **Path**: HTML (puzzle logic — timing precision is not the hypothesis)
- **Date**: 2026-07-17
- **Build**: `prototype.html` — open it by double-clicking; works in any browser, mouse or touch, no install.

## Hypothesis

> If the player swaps candies to trigger matches and multi-tile cascades, the
> loop will feel intrinsically satisfying — we will know this is true if the
> player voluntarily replays at least once and reports the cascade moments
> (not the level win) as the high point.

**Riskiest assumption tested first**: that cascades — the part the player
doesn't directly control — feel like a reward for a clever swap rather than
random noise (Pillar 2: Clever, Never Cheated).

## What's in scope

- 8×8 board, 5 candy types, swap by swipe or tap-tap
- Match-3 clears; match-4 spawns a striped candy (clears a line); match-5 spawns a color bomb (clears a color)
- Cascade chains with escalating multiplier and callouts ("Sweet! ×2"…)
- 25 moves, 2500 score target, 3-star thresholds, replay button
- Invalid swaps shake and revert at no cost; auto-reshuffle when no moves exist

## Explicitly cut (prototype waste rules)

Menus, tutorial, sound, lives, level variety, blockers, booster brewing meta,
themes/events, leaderboards, persistence. None of these test the hypothesis.

## Automated smoke test

Verified headless (Chromium) on 2026-07-17: board renders 64 tiles; valid swap
scores and consumes a move; invalid swap costs nothing; full simulated game
ends correctly (score 3960, best chain ×4, no board holes); replay resets;
zero console errors.

## Playtest debrief (fill in after playing)

1. Hypothesis: CONFIRMED / PARTIALLY CONFIRMED / REFUTED — what did you see?
2. Best moment (be specific):
3. Worst / most confusing moment (be specific):
4. Anything unexpected?
5. Verdict: **PROCEED / PIVOT / KILL** — and one sentence why.

Record the result in `REPORT.md` (template: `.claude/docs/templates/prototype-report.md`)
and update `prototypes/index.md`.
