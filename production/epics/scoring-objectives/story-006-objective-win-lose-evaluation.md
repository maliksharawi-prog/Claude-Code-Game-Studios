# Story 006: ObjectiveEvaluator — win/lose evaluation at `board_stabilized` (last-move-cascade win) & the Tracking→Resolved state machine

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-objectives.md` (Rev 2) — § 8 (win/lose evaluation timing, predicate & precedence), § 9 (state machine; session-end-at-objective-complete), Formulas 3–4
**Requirement**: `TR-lo-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — D3/D4: `ObjectiveEvaluator` evaluates win/lose **only** at `BoardStabilized`; win check first)
**Governing ADRs (secondary)**: `architecture.md` §6 (win/lose predicate ownership).
**ADR Decision Summary**: Because `BoardStabilized` fires only after a move's entire cascade has resolved, and win evaluation reads already-updated `current` values, a final-move cascade that completes an objective on its 2nd/3rd clear still resolves WIN at that single stabilization. Win is checked first, unconditionally, so "win wins" on a simultaneous win-and-moves-exhausted stabilization.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain, zero engine surface, zero RNG. Evaluation is a pure function of already-resolved signals + Scoring's live score. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: win/lose is evaluated **only** at `BoardStabilized`, exactly once per stabilization; the `Tracking → Resolved` state machine is terminal per attempt (further signals ignored, mirroring Board Engine's defensive drop policy).
- Forbidden: driving win/lose off `ObjectiveProgressed`/any per-clear signal; any `UnityEngine.*` in Domain.

---

## Acceptance Criteria

*From `design/gdd/level-objectives.md`, scoped to this story:*

- [ ] **Completion predicate** (Formula 3): `win_condition_met(trackers) = AND over every tracker's is_complete` (with `n = 1` reduces to a single check).
- [ ] **Outcome resolution** (Formula 4, win-first precedence): a truth-table test asserts — all complete + moves > 0 → WIN; all complete + `moves_remaining == 0` → WIN (not LOSE); not all complete + `moves_remaining == 0` → LOSE; not all complete + moves > 0 → `null` (no resolution, play continues).
- [ ] **Evaluation timing**: win/lose is evaluated exactly once per `board_stabilized`, and at no other point. `current` values are already fully up to date at that instant (every `MatchCleared` of the move fired first).
- [ ] **Last-move-cascade win**: a `move_limit = 1` level with a `collect_color` objective satisfiable only by a 2-step cascade (target reached at `chain_index = 2`) resolves WIN at the single `board_stabilized` after the full cascade, with `moves_used == 1` — never LOSE.
- [ ] **State machine**: `Tracking → Resolved` is terminal for the attempt — after resolution, a subsequent (out-of-contract) `match_cleared` produces no further `objective_progressed` and no second resolution; a fresh `board_bootstrapped` re-initializes.
- [ ] **Session ends at the objective-completing stabilization**: on WIN with moves remaining, the level ends at that stabilization (Candy-Crush convention); remaining moves are not spent, no leftover-move bonus at MVP.

---

## Implementation Notes

*Derived from level-objectives §8/§9 + Formulas 3–4:*

- Evaluate exactly once per `BoardStabilized`: win check first (Formula 3), then lose (`moves_remaining == 0`), else `null`. Win-first precedence is the formal "win wins" rule.
- Move consumption (Story 005) and win/lose evaluation are deliberately decoupled in timing: consumption at `swap_accepted`; evaluation only after the full cascade settles at `board_stabilized`. This is what lets a final-move cascade win.
- The `Tracking → Resolved` transition is terminal per attempt: once resolved, ignore every further Board Engine signal (defensive guard) until the next fresh `board_bootstrapped`. This structurally prevents a second `level_resolved`.
- This story delivers the outcome decision + state machine; the `ResultsData` assembly, `record_level_completion` persist handshake, and `level_resolved` emission are Story 007 (which fires at the resolution instant this story detects).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: move accounting + progress tracking that keep `current`/`moves_remaining` current.
- Story 007: `ResultsData` assembly, `record_level_completion` on WIN, `level_resolved` emission.
- Blocker/ingredient objective types (post-MVP).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — predicate & precedence** (Formulas 3/4)
  - Given: the full outcome truth table.
  - When: evaluated at `board_stabilized`.
  - Then: WIN / WIN (win-first) / LOSE / null respectively.
- **AC — evaluation timing**
  - Given: a multi-step cascade move.
  - When: `board_stabilized` fires.
  - Then: exactly one evaluation; `current` values already fully updated.
- **AC — last-move-cascade win**
  - Given: `move_limit = 1`, a `collect_color` satisfiable only at `chain_index = 2`.
  - When: the final move's cascade resolves.
  - Then: WIN at the single `board_stabilized`, `moves_used == 1` (never LOSE).
- **AC — terminal state machine**
  - Given: an attempt already resolved.
  - When: an out-of-contract `match_cleared` is injected.
  - Then: no further `objective_progressed`, no second resolution; a fresh `board_bootstrapped` re-initializes.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-objectives/win_lose_evaluation_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005 (updated `current`/`moves_remaining` at evaluation time), Story 004 (predicate over trackers), E03 (Story 009 `BoardStabilized`/`board_bootstrapped`).
- Unlocks: Story 007 (assembles `ResultsData` + persists + emits `level_resolved` at the resolution instant this story detects).
