# Story 006: Cascade loop, `chain_index` continuation, seam-4 fixpoint & termination caps

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 6 (Resolution Loop / cascade counter), § Detailed Rules 3 (Seam 4 calling semantics), Formula 6 (`MAX_CASCADE_DEPTH`), § Detailed Rules 13 (Logic/Presentation Separation)
**Requirement**: `TR-be-004`, `TR-be-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — `CascadeStepAdvanced`/`CascadeEnded` records; the whole synchronous move is one call)
**Governing ADRs (secondary)**: `architecture.md` §6 (termination caps ownership).
**ADR Decision Summary**: The Domain resolves an entire move — every cascade step, gravity, refill — **synchronously in one call**; the public contract has no `await`/timer/yield. `MAX_CASCADE_DEPTH` / `MAX_CHAIN_EXPANSION_ITERATIONS` live in the centralized Domain config.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, single synchronous call stack — headless-testable with plain synchronous assertions. No `UnityEngine`, no post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: a move's entire resolution (swap → match → seams → clear → gravity → refill → cascade → stabilize) happens inside one function call — no `await`, no timers, no per-frame state.
- Required: `MAX_CASCADE_DEPTH` (default 20) and `MAX_CHAIN_EXPANSION_ITERATIONS` (default 10) are centralized Domain config constants, never scattered literals.
- Guardrail: a typical move resolves well under 1ms; worst-case a single `MatchCleared` carries up to ~81 `PieceSnapshot`s (9×9) — transient, human-paced.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] **`chain_index` semantics**: starts at `1` for the first Clearing of a move; increments by exactly `1` per subsequent Clearing pass within the same move; resets to `1` on the next move. `test_chain_index_increments_per_cascade_step` (a 3-step cascade → `MatchCleared` at `chain_index = 1, 2, 3`); `CascadeStepAdvanced(chain_index)` fires when Matching after Refilling finds ≥1 new run.
- [ ] `test_cascade_ended_reports_correct_final_chain_index`: `CascadeEnded(final_chain_index, total_cells_cleared, trigger)` fires when Matching finds zero new runs; `final_chain_index` equals the last step (e.g. `3`); `total_cells_cleared` is the aggregate count.
- [ ] **Seam-4 fixpoint loop** (BoardModel owns it): call `expand_special_chain_reaction` repeatedly against its own growing result until a call returns its input unchanged (fixpoint) **or** `MAX_CHAIN_EXPANSION_ITERATIONS` calls, whichever first. `test_seam_4_called_iteratively_to_fixpoint` (every added cell present in the same step's `MatchCleared`); `test_seam_4_exceeds_iteration_cap_force_stabilizes` (force-stopped at the cap, last-returned set finalizes, error-level diagnostic; no hang). `test_seam_4` invalid `VOID`/`EMPTY` cells dropped + logged.
- [ ] **`MAX_CASCADE_DEPTH` force-stabilize**: `test_max_cascade_depth_force_stabilizes` — an adversarial `board-refill` sequence that always produces a new run is force-stabilized at exactly `MAX_CASCADE_DEPTH` steps; remaining runs left uncleared; loop proceeds to the reshuffle-check; error-level diagnostic logged; loop reaches `Idle`/`Reshuffling`, never hangs.
- [ ] **`swap_anchor_cells` for cascade steps 2+**: `test_swap_anchor_cells_empty_for_cascade_steps_beyond_first` — equals the triggering swap's two cells at `chain_index = 1`; equals the empty set for every `chain_index ≥ 2`.
- [ ] **Single synchronous call** (§13): `test_full_resolution_completes_within_one_synchronous_call` (trigger a swap, immediately assert the fully-resolved board + full signal list, no `await`/frame yield); `test_no_timer_or_await_in_public_api` (interface inspection: no public method returns a signal/promise requiring the caller to wait across frames).

---

## Implementation Notes

*Derived from board-engine §6/§3/§13 + Formula 6:*

- The cascade loop: after Refilling, return to Matching; if ≥1 run, emit `CascadeStepAdvanced` and continue (Clearing at `chain_index+1`); if zero runs, emit `CascadeEnded` and proceed to the reshuffle-check (Story 007).
- Two independent, complementary caps guarantee halting: `MAX_CHAIN_EXPANSION_ITERATIONS` bounds within-step seam-4 calls; `MAX_CASCADE_DEPTH` bounds across-step iterations. Both are pure iteration counters enforced unconditionally, regardless of the resolver's behaviour.
- Each seam-4 call is exactly one single-pass expansion (the resolver never recurses internally, per board-engine §3); BoardModel re-invokes it against the growing superset.
- Force-stabilize (either cap): stop iterating, use the last-returned set, log an error-level diagnostic, proceed — never throw, never hang. Reaching a cap during genuine play is not expected (Formula 6: `P(depth > 8) ≈ 0.006%`).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: the single Clearing pass mechanics + swap validity (this story loops it).
- Story 007/008: the reshuffle-check the cascade proceeds into; bootstrap's own cascade pass.
- Story 009: whole-move ordered-log validation across `MoveResolver`.
- Stories 014: the concrete seam-4 resolver (this story uses a mocked/adversarial seam-4 double).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — chain_index & cascade end** (`test_chain_index_increments_per_cascade_step`, `test_cascade_ended_reports_correct_final_chain_index`)
  - Given: a mocked `board-refill` engineered to chain 3 times.
  - When: the move resolves.
  - Then: `MatchCleared` at `chain_index = 1,2,3`; `CascadeStepAdvanced` between steps; `CascadeEnded(final = 3, total_cells_cleared, trigger)`.
- **AC — seam-4 fixpoint & cap** (`test_seam_4_called_iteratively_to_fixpoint`, `test_seam_4_exceeds_iteration_cap_force_stabilizes`)
  - Given: (a) a seam-4 adding a strict superset across successive calls; (b) a seam-4 that never reaches a fixpoint.
  - When: the Clearing pass resolves.
  - Then: (a) called repeatedly until unchanged; every added cell in that step's `MatchCleared`; (b) force-stopped at `MAX_CHAIN_EXPANSION_ITERATIONS`, last set finalizes, error diagnostic, no hang.
- **AC — cascade depth cap** (`test_max_cascade_depth_force_stabilizes`)
  - Given: an adversarial `board-refill` always producing a new run.
  - When: the move resolves.
  - Then: force-stabilized at exactly `MAX_CASCADE_DEPTH`; remaining runs uncleared; diagnostic logged; loop reaches `Idle`/`Reshuffling`.
- **AC — anchor cells beyond first step** (`test_swap_anchor_cells_empty_for_cascade_steps_beyond_first`)
  - Given: a mocked seam-3 asserting `swap_anchor_cells`.
  - When: a multi-step cascade resolves.
  - Then: `{cell_a, cell_b}` at `chain_index = 1`; empty set at every `chain_index ≥ 2`.
- **AC — single synchronous call** (`test_full_resolution_completes_within_one_synchronous_call`, `test_no_timer_or_await_in_public_api`)
  - Given: a swap that triggers a multi-step cascade.
  - When: `swap_request` returns.
  - Then: the final board + full signal list are assertable in the same call stack; no public method requires waiting across frames.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/cascade_loop_caps_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 005 (single Clearing pass + swap validity), Story 004 (gravity/refill per step), Story 003 (seam interface).
- Unlocks: Story 007 (reshuffle-check after `CascadeEnded`), Story 008 (bootstrap step-8 cascade), Story 014 (seam-4 passive chain).
