# Story 005: Swap rules, validity (Formula 2) & single-step resolution (Swapping → Clearing → Falling → Refilling)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 5 (Swap Rules), § Detailed Rules 6 (Resolution Loop state machine), Formula 2 (Swap Validity), § Detailed Rules 3 (seam call sites + defensive validation)
**Requirement**: `TR-be-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — `SwapStarted`/`SwapRejected`/`SwapAccepted`/`SpecialActivated`/`MatchCleared`/`SpecialSpawned` records + the `TriggerSource`/`SwapRejectReason` enums, emitted through the injected `IBoardEventSink`)
**Governing ADRs (secondary)**: `architecture.md` §8.1 (seam 1 in validity, seams 3/4 in Clearing).
**ADR Decision Summary**: `BoardModel` emits every event through the injected `IBoardEventSink`; it never names a Feature type. Every clear/spawn payload carries full `PieceSnapshot` identity captured at the instant the event fires.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, synchronous — no `await`, no timer, no per-frame yield. No `UnityEngine`. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: all Domain logic resolves synchronously in one call; `BoardModel` emits only through the injected `IBoardEventSink`; payload collections are `IReadOnlyList<T>`, immutable by construction.
- Required: Board owns ALL validity judgment — the structural precondition is re-checked here, never trusted from the caller; seam responses are defensively validated against live board state (out-of-bounds / non-`OCCUPIED` cells dropped + logged).
- Forbidden: live C# `event`/delegate across the Domain→Game boundary; threading `ScoreKeeper` through any seam.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] **Structural precondition** (defensive, always checked): a `swap_request(a,b)` is rejected with zero mutation if either cell is out of bounds, `VOID`, not `OCCUPIED`, or not Manhattan-adjacent (distance ≠ 1) → `SwapRejected(reason = NotAdjacent)`.
- [ ] **Validity** (Formula 2): execute the model swap first, then judge valid iff it produces ≥1 run OR seam 1 returns `true`. `test_swap_producing_match_is_accepted` (`SwapAccepted(trigger = SwapMatch)`); `test_swap_seam_1_activation_bypasses_match_requirement` (`trigger = SpecialActivation`); `test_swap_union_of_match_and_activation_clears` (seam-1-and-match coexistence → union, nothing lost/duplicated).
- [ ] **Invalid swap revert**: `test_swap_no_match_reverts` — swap back to exact pre-swap state, zero net change, zero move consumed, `SwapRejected(reason = NoMatchNoActivation)`, loop returns to `Idle`. `test_swap_non_adjacent_cells_rejected`.
- [ ] Emits, in order for a single-step move: `SwapStarted(pre-swap snapshots)` → `SwapAccepted` → *(if seam-1)* `SpecialActivated` → `MatchCleared(chain_index=1, cleared_pieces, run_data, trigger)`; and, per Clearing pass, one `SpecialSpawned` per seam-3-exempted cell.
- [ ] **Resolution order within one cascade step**: Matching → seam 3 (spawns) → seam 4 (chain expansion) → Clearing; `chain_index` increments on entering Clearing; seam-3-exempted cells are removed from the clear set before seam 4 runs.
- [ ] `board_input_enabled` flips `false` on entering `Swapping` and `true` again at `Idle` (`BoardInputEnabledChanged`), synchronously in the same call.
- [ ] Defensive seam validation: `test_seam_3_invalid_cell_response_dropped` — a seam-3 cell outside the current clear set is dropped + logged; BoardModel's own clear set is unaffected.

---

## Implementation Notes

*Derived from board-engine §5/§6/§3 + ADR-005:*

- Execute-then-judge (mirrors the prototype's `modelSwap()`-then-check): exchange the two pieces, run match detection (Story 002) + seam 1; if invalid, swap back and emit `SwapRejected`.
- Clearing pass: apply seam 3 (spawns) against the raw run list + `swap_anchor_cells = {cell_a, cell_b}` (this is `chain_index = 1`, `trigger ∈ {SwapMatch, SpecialActivation}`); remove exempted cells; apply seam 4 once (the full fixpoint loop is Story 006); finalize the clear set; emit `MatchCleared` with full pre-clear `PieceSnapshot` identity, then `SpecialSpawned` per exempted cell; then Falling (gravity, Story 004) and Refilling (Story 004 → emits `PiecesSpawned(CascadeRefill)`).
- `SpecialActivated.cleared_pieces` carries seam-2's contribution ONLY (a subset of the following `MatchCleared`) — it is NOT summed downstream; fire it immediately before the first `MatchCleared` of a `SpecialActivation` move.
- This story delivers the **single-step** resolution (one Clearing pass); multi-step cascade continuation + caps is Story 006.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: cascade continuation (looping Matching after Refilling), `chain_index ≥ 2`, `CascadeStepAdvanced`/`CascadeEnded`, `MAX_CASCADE_DEPTH`, the seam-4 fixpoint loop + `MAX_CHAIN_EXPANSION_ITERATIONS`.
- Story 007/008: reshuffle check at end of move; bootstrap.
- Story 009: `MoveResolver` orchestration + whole-move ordered log.
- Stories 012–015: the concrete seam resolver (this story uses the no-op default or a test double).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — structural rejection** (`test_swap_non_adjacent_cells_rejected`)
  - Given: a swap of two cells with Manhattan distance ≠ 1 (or a VOID/EMPTY target).
  - When: `swap_request` is processed.
  - Then: `SwapRejected(NotAdjacent)`, zero grid mutation, zero move.
- **AC — valid match / activation / union** (`test_swap_producing_match_is_accepted`, `test_swap_seam_1_activation_bypasses_match_requirement`, `test_swap_union_of_match_and_activation_clears`)
  - Given: (a) a swap forming a run; (b) a mocked seam-1 → `true` with zero runs; (c) both a run and seam-1 true.
  - When: validity is judged.
  - Then: (a) `SwapAccepted(SwapMatch)`; (b) `SwapAccepted(SpecialActivation)`; (c) clear set = union with no cell lost/duplicated.
- **AC — revert** (`test_swap_no_match_reverts`)
  - Given: a swap producing zero runs, no activation.
  - When: judged.
  - Then: exact pre-swap state restored; `SwapRejected(NoMatchNoActivation)`; loop returns to `Idle`; no move consumed.
- **AC — seam-3 defensive drop** (`test_seam_3_invalid_cell_response_dropped`)
  - Given: a mocked seam-3 returning a cell outside the clear set.
  - When: Clearing applies seam 3.
  - Then: the entry is dropped + logged; BoardModel's clear set is unaffected.
- **AC — event order & input flag**
  - Given: a single-step accepted swap.
  - When: resolved.
  - Then: `SwapStarted → SwapAccepted → [SpecialActivated] → MatchCleared(1) → [SpecialSpawned…] → PiecesSpawned(CascadeRefill)`; `BoardInputEnabledChanged(false)` on `Swapping`, `(true)` at `Idle`.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/swap_validity_resolution_test.cs` — must exist and pass (capturing test `IBoardEventSink`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (match detection + union), Story 003 (seam interface + no-op), Story 004 (gravity/refill for Falling/Refilling).
- Unlocks: Story 006 (cascade continuation), Story 013 (combo matrix uses seams 1/2 in validity).
