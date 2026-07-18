# Story 005: ObjectiveEvaluator — unified `collect_color` counting, `score_target` seam, move accounting & progress emission

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-objectives.md` (Rev 2) — § 3 (`score_target` score-provider seam), § 4 (unified `collect_color` counting), § 5 (`objective_progressed` emission), § 6 (move accounting), § 7 (low-moves warning), Formulas 5–6
**Requirement**: `TR-lo-001`, `TR-lo-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — D3: `ObjectiveEvaluator` consumes `SwapAccepted` (decrement moves → `MovesRemainingChanged`), `MatchCleared` (tally `collect_color` from `ClearedPieces`, read `get_current_score()` for `score_target`, emit `ObjectiveProgressed`))
**Governing ADRs (secondary)**: `architecture.md` §6.
**ADR Decision Summary**: Objective consumes `MatchCleared` **only** for `collect_color` tallying (never `SpecialActivated` — its `ClearedPieces` is a subset of the following `MatchCleared`; summing both double-counts). Derived feature events are emitted back through `MoveResolver.Emit` so they interleave in-position and have no logic subscribers.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain, zero engine surface, zero RNG. Objective consumes zero synchronous queries from Board Engine; the only seam is `get_current_score()` into Scoring. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `collect_color` tallying subscribes to `MatchCleared` only; derived feature events (`ObjectiveProgressed`, `MovesRemainingChanged`) are emitted through `MoveResolver.Emit` (in-position, append-only). `LOW_MOVES_THRESHOLD` is centralized Domain config.
- Forbidden: summing `SpecialActivated.ClearedPieces` for tally (double-counts); registering a logic subscriber for a derived feature event (re-entrancy); any `UnityEngine.*` in Domain.

---

## Acceptance Criteria

*From `design/gdd/level-objectives.md`, scoped to this story:*

- [ ] **Unified `collect_color` counting**: for every `PieceSnapshot` in every `MatchCleared.ClearedPieces` (any `chain_index`, any `trigger_source`), each `collect_color` tracker whose `params.color` matches increments `current` by exactly 1. A single fixture move combining a plain 3-match + a Striped row-clear + a passively-caught Color Bomb sums every target-color cell **exactly once**, with zero double-counting from `SpecialActivated`.
- [ ] **BOOTSTRAP counts**: a `MatchCleared(trigger = BOOTSTRAP)` fixture correctly increments a `collect_color` tally before any player swap (deliberate — not filtered).
- [ ] **`score_target` seam**: `get_current_score()` is queried via a plain synchronous call (never a signal) at every relevant evaluation point; a mocked mid-cascade score change is reflected in the very next `objective_progressed` emission for a `score_target` tracker.
- [ ] **Move accounting** (Formula 5): `swap_accepted` (`SWAP_MATCH` or `SPECIAL_ACTIVATION`) decrements `moves_remaining` by exactly 1 (synchronously, before the cascade resolves); `swap_rejected` (either reason) and `board_reshuffled` leave it unchanged; bootstrap consumes 0. Emit `moves_remaining_changed(moves_remaining, moves_used, move_limit, is_low_moves)` once per `swap_accepted`.
- [ ] **Progress emission cadence**: `objective_progressed` fires once per `MatchCleared` for every tracker that step affected (a 3-step cascade clearing the tracked color on steps 1 and 3 emits exactly two events for that tracker, not three, each with the correctly-incremented running `current`); never fires for an unaffected tracker.
- [ ] **Low-moves predicate** (Formula 6): `is_low_moves = (moves_remaining <= LOW_MOVES_THRESHOLD) AND NOT all_objectives_complete`; suppressed once the level is about to resolve WIN.

---

## Implementation Notes

*Derived from level-objectives §3–7 + Formulas 5–6 + ADR-005 D3:*

- Completeness proof (why `MatchCleared` only): every clear path (match, Striped sweep, bomb wipe, jackpot combo, passive chain) lands in the single finalized `MatchCleared.ClearedPieces` for that step; Board Engine's union rule dedupes shared cells; `SpecialActivated` is a subset fired earlier — consuming it would double-count.
- `score_target.current` is never computed here — it is always `get_current_score()` (Story 003), queried once per `MatchCleared` (ScoreKeeper processed first, so the value already includes that step).
- `moves_remaining` initializes to `move_limit` at bootstrap; decrement synchronously on `SwapAccepted` (the move is "considered consumed" the instant it fires, before the cascade begins) — decoupled from win/lose timing (Story 006).
- Emit `ObjectiveProgressed`/`MovesRemainingChanged` back through `MoveResolver.Emit` so they interleave in-position with the board event that produced them (HUD climbs per clear). They have no logic subscribers — pure append.
- `is_low_moves`'s `NOT all_objectives_complete` term suppresses the warning once a WIN is about to resolve.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: tracker model, field normalization, handler registry, progress/completion predicates.
- Story 006: win/lose evaluation at `board_stabilized` (this story only updates `current`/`moves_remaining` synchronously; it never resolves outcome).
- Story 007: `ResultsData` assembly + persist handshake.

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — unified counting & no double-count**
  - Given: a move combining a plain 3-match, a Striped row-clear, and a passively-caught Color Bomb.
  - When: the move's `MatchCleared` events are consumed.
  - Then: every target-color cell counted exactly once; zero contribution from `SpecialActivated`.
- **AC — BOOTSTRAP counts**
  - Given: a `MatchCleared(trigger = BOOTSTRAP)` fixture.
  - When: consumed.
  - Then: the `collect_color` tally increments before any player swap.
- **AC — score_target seam**
  - Given: a mocked `get_current_score()` changing mid-cascade.
  - When: a `score_target` tracker updates.
  - Then: queried via a plain synchronous call; the next `objective_progressed` reflects the new value.
- **AC — move accounting** (Formula 5)
  - Given: `SWAP_MATCH`/`SPECIAL_ACTIVATION` accepts, `SwapRejected`, `BoardReshuffled`.
  - When: consumed.
  - Then: accepts decrement by exactly 1 (synchronously); rejects/reshuffle unchanged; one `MovesRemainingChanged` per accept.
- **AC — emission cadence & low-moves** (Formula 6)
  - Given: a 3-step cascade clearing the tracked color on steps 1 and 3; and a low-moves-but-won state.
  - When: consumed.
  - Then: exactly two `objective_progressed` for that tracker with correct running `current`; `is_low_moves` suppressed when the win is about to resolve.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-objectives/collect_color_move_accounting_test.cs` — must exist and pass (mocked event fixtures + mocked `get_current_score`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (tracker model + handler registry), Story 003 (`get_current_score()`), E03 (Story 009 `MatchCleared`/`SwapAccepted`/`SwapRejected`/`BoardReshuffled` stream + `MoveResolver.Emit`).
- Unlocks: Story 006 (win/lose reads the updated `current`/`moves_remaining`).
