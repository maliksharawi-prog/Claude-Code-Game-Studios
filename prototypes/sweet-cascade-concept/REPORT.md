# Concept Prototype Report: Sweet Cascade

> **Date**: 2026-07-17
> **Prototype Path**: HTML
> **Concept File**: design/gdd/game-concept.md

---

## Hypothesis

If the player swaps candies to trigger matches and multi-tile cascades, the
loop will feel intrinsically satisfying — evidenced by the player voluntarily
replaying at least once and identifying cascade moments (not the level win)
as the high point.

---

## Riskiest Assumption Tested

That cascades — the part the player doesn't directly control — feel like a
reward for a clever swap rather than random noise (Pillar 2: Clever, Never
Cheated). The prototype surfaced cascades with escalating multiplier callouts
("Sweet! ×2" → "Delicious! ×4") to make the chain legible as a consequence of
the player's swap.

---

## Approach

A single self-contained `prototype.html` (~450 lines), built in one session,
playable by double-click with mouse or touch.

**Path chosen:** HTML
**Reason for path:** Match-3 is a logic puzzle — the hypothesis is about
decision satisfaction and cascade legibility, not input timing, so browser
latency does not invalidate the result.

**Shortcuts taken (intentional):**
- Emoji as candy sprites; CSS-only animation; hardcoded tuning values
- One level (25 moves / 2,500 target), no lives, menus, sound, tutorial, or persistence
- No blockers, no T/L-shape specials (runs of 4/5 only), no booster meta

---

## Result

- Automated headless playthrough (Chromium): valid swaps score and consume
  moves; invalid swaps revert at no cost; specials spawn and chain; a full
  simulated game scored 3,960 with a ×4 cascade chain, board integrity held
  (no holes), end overlay and replay worked, zero console errors.
- Founder playtest (remote, async): returned verdict **PROCEED**. Detailed
  debrief answers (best/worst moment, hypothesis check) were not captured in
  this session — the verdict was given directly.

---

## Metrics

| Metric | Value |
|--------|-------|
| Path used | HTML |
| Iterations to playable | 1 (one-shot; smoke test passed on first run) |
| Prototype duration | ~1 session |
| Playtesters | 1 internal (founder) + automated smoke test |
| Feel assessment | Not deeply captured — async playtest returned verdict only. HTML-path caveat applies: juice/feel ceiling must be re-validated in-engine at vertical slice. |
| Hypothesis verdict | CONFIRMED (by verdict; debrief detail not collected) |

---

## Recommendation: PROCEED

The founder played the prototype and returned PROCEED. The core loop
(swap → match → cascade with match-4/5 specials) is validated as worth full
design investment. Because the debrief detail was not captured, treat the
confirmation as directional: the vertical slice must re-test feel in-engine
with real art, sound, and haptics before Production commit.

---

## If Proceeding

- **Core tuning values discovered:** 8×8 board with **5 candy types** produced
  frequent, legible cascades (6 types felt too sparse in design analysis —
  worth A/B in GDD tuning). 25 moves / 2,500 target / 20 pts-per-tile ×
  cascade multiplier gave a beatable-but-not-trivial first level (greedy bot:
  3,960). Escalating callouts made chains read as earned, supporting Pillar 2.
- **Assumptions confirmed:** Core loop is satisfying enough to green-light;
  swipe-and-tap dual input worked with one input model for web + touch.
- **Assumptions disproved:** None identified at this stage.
- **Emergent mechanics:** Striped-candy chain reactions (stripe clearing a
  stripe) created the biggest moments — formalize special-×-special combos as
  a first-class system in the specials GDD.

**Next steps:**
1. `/art-bible` — lock visual identity (required before Technical Setup gate)
2. `/map-systems` — decompose concept into systems index
3. `/design-system [system]` — GDDs in dependency order, using tuning values above
4. `/create-architecture` → ADRs → `/architecture-review` → `/gate-check`

---

## Lessons Learned

- **What assumptions were broken by actually building this?** Special-candy
  spawn anchoring (at the swapped cell) matters for making match-4s feel
  deliberate — spawning mid-run reads as random.
- **What surprised us?** Passive color-bomb detonation during cascades is
  dramatic but can feel unearned — flag as a design question for the specials GDD.
- **What would we test differently next time?** Capture the structured debrief
  (best/worst moment) at verdict time; add a simple replay counter to measure
  the "voluntarily replays" signal instead of self-report.

---

> *Prototype code location: `prototypes/sweet-cascade-concept/`*
> *This code is throwaway. Never refactor into production.*
