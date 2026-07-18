# Story 006: juice_input_lock + Gravity Re-derivation + Reshuffle Live-Query

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Presentation (JuiceDirector replay core)
> **Type**: Logic
> **Estimate**: M (~4h)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/juice-layer.md` (§2 gravity re-derivation + reshuffle exception, §10 input-lock ownership)
**Requirement**: `TR-jl-002` (owns `juice_input_lock_changed`; gravity re-derivation from events; one live-query reshuffle exception)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D5, term 4)
**ADR Decision Summary**: `JuiceDirector` owns the fourth Formula-5 veto term — `juice_input_lock` is `true` when the first Reveal Step is dequeued and `false` when `SETTLE_REVEAL` finishes; it fires `juice_input_lock_changed`, which `InputGate` caches.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Gravity re-derivation and the lock signal are pure logic against the shadow grid — headless-testable. The single `get_piece_at()` reshuffle sweep is the one sanctioned live Domain read; it is made safe by the single-active-queue invariant, not by a new engine API.

**Control Manifest Rules (this layer — Game)**:
- Required: `JuiceDirector` writes only its own `juice_input_lock` term (into `InputGate`); gravity compaction is re-derived from the event stream by re-running Board Engine's own documented algorithm against the shadow grid.
- Forbidden: never query live `BoardModel` during replay — the sole exception is `BoardReshuffled`'s one live `get_piece_at()` sweep, permitted only while presenting `RESHUFFLE_REVEAL` and only after confirming the lock has held continuously since the queue began.
- Guardrail: 60fps/16.6ms; the lock keeps input vetoed across the whole multi-frame replay and re-enables exactly at `SETTLE_REVEAL` completion.

---

## Acceptance Criteria

*From GDD `design/gdd/juice-layer.md` §2 + §10, scoped to this story:*

- [ ] `juice_input_lock` becomes `true` the instant the first Reveal Step is dequeued and `false` the instant that Queue's final `SETTLE_REVEAL` finishes presenting; `juice_input_lock_changed` fires on each edge and is written into `InputGate` (term 4).
- [ ] Gravity re-derivation: given a synthetic multi-segment column (mirroring `board-engine.md` Formula 3's worked example) and a mocked clear, the independently-derived fall paths match Board Engine's compaction rule (pieces settle toward their own segment's bottom, never crossing a `VOID`) — requiring no new Board Engine signal.
- [ ] Fall distances are computed per-segment using Board Engine's segment boundaries read once at bootstrap from `cell_mask`, never recomputed mid-level.
- [ ] The one permitted live `get_piece_at()` sweep fires only while presenting `RESHUFFLE_REVEAL`, and only after asserting `juice_input_lock = true` has held continuously since the queue began; its result is folded into the shadow grid.
- [ ] After a force-stabilize at `MAX_CASCADE_DEPTH` (runs left uncleared), the Queue simply ends at `cascade_ended.final_chain_index`; no special-casing — the lock still clears at `SETTLE_REVEAL`.

---

## Implementation Notes

*Derived from ADR-005 D5 and `juice-layer.md` §2, §10:*

- The lock exists because Board Engine's own `board_input_enabled` flips back to `true` synchronously within the resolve frame — long before a single visible frame of the cascade has replayed. `juice_input_lock` covers the gap between "logically done" and "visually settled."
- Gravity compaction is a fully deterministic, already-published algorithm — re-run Board Engine's own rule (segments read once at bootstrap, pieces settle to segment bottom, stop at `VOID`) against the shadow grid to compute which surviving piece animates from which old cell to which new cell.
- `BoardReshuffled`'s payload is `AttemptsUsed` only; it is always the last board-mutating event before stabilize/bootstrap, and the input-lock policy guarantees no intervening mutation — so one synchronous `get_piece_at()` sweep at that specific moment is safe and sufficient. This is a narrow, justified exception, not a weakening of the never-query-live rule.
- This story only *writes* term 4 into `InputGate`; the composition itself is story 002.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: the `InputGate` composition (this story only pushes the `juice_input_lock` term).
- Story 004/005: Shadow Board Model construction and Reveal Queue ordering (consumed here).
- Story 009: `ScreenFlowController` gating T15/T16 on `juice_input_lock` (this story only produces the signal).

---

## QA Test Cases

*Logic story — automated Edit Mode specs; deterministic, mocked event stream, no real timers.*

- **AC-1 (lock timing)**: Given a Queue, When the first step is dequeued, Then `juice_input_lock == true` and a `changed(true)` fired; When `SETTLE_REVEAL` finishes, Then `false` and `changed(false)` fired. Edge cases: single-step move; a move that reshuffles before settling.
- **AC-2 (`test_shadow_board_gravity_rederivation_matches_segment_definition`)**: Given the Formula-3 multi-segment column + a mocked clear, Then derived fall paths match documented compaction (no `VOID` crossing).
- **AC-3 (`test_reshuffle_reveal_live_query_occurs_exactly_once_and_only_after_lock_confirmed`)**: The single live query fires only during `RESHUFFLE_REVEAL` and only after `juice_input_lock` held continuously since the queue began (assert a live-query counter == 1 and lock-held precondition).
- **AC-4 (depth-cap termination)**: A `cascade_ended` at `MAX_CASCADE_DEPTH` ends the Queue cleanly with the lock clearing at `SETTLE_REVEAL`.
- **AC-5 (term isolation)**: Assert `JuiceDirector` writes only term 4 into `InputGate` and reads no other owner's internals.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/juice-layer/juice_input_lock_gravity_reshuffle_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (Shadow Board Model), Story 005 (Reveal Queue), Story 002 (InputGate term 4 sink); E02 (BoardEvent catalog), E03 (BoardModel `get_piece_at` for the reshuffle sweep + segment/`cell_mask` definitions).
- Unlocks: Story 007 (presenter animates re-derived falls), Story 009 (Results waits on this lock).
