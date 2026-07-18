# Story 009: `MoveResolver` sink pipeline, ordered per-move sequence & `PieceSnapshot` payload completeness

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 7 (Signal Catalog + ordering guarantees), § Detailed Rules 13 (deferred-replay payload sufficiency)
**Requirement**: `TR-be-001`, `TR-be-003`, `TR-sc-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — D1 transport, D2 `MoveResolver` sink + authoritative log, D3 consumption modes, the byte-identical ordering guarantee, D6 threading)
**Governing ADRs (secondary)**: board-engine §7/§13 (the two verbatim ordering guarantees + PieceSnapshot sufficiency).
**ADR Decision Summary**: The Domain produces, per move, **one ordered `IReadOnlyList<BoardEvent>`**. `MoveResolver` is the injected `IBoardEventSink`; it appends every board event in strict emission order and dispatches to a **fixed, constant-ordered** logic-subscriber list `[ScoreKeeper, ObjectiveEvaluator]` (both empty at E03; registered in E04). `ResolveSwap(a,b)` / `ResolveBootstrap()` run the whole move synchronously and return the immutable snapshot. Everything is single-threaded (D6).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, `record`/`readonly record struct`, `IReadOnlyList<T>`/`List<T>` only — no `UnityEngine`, no post-cutoff API. No thread boundary anywhere in the event path (D6).

**Control Manifest Rules (Domain)**:
- Required: the event catalog is **copied verbatim** from ADR-005 Key Interfaces — never re-derived or "improved". `MoveResolver` registers logic subscribers in the fixed constant order `[ScoreKeeper, ObjectiveEvaluator]` (encode as a constant list, assert in a test). All payload collections are `IReadOnlyList<T>`, immutable by construction.
- Required: `BoardModel` emits every event only through the injected `IBoardEventSink`; it never names `ScoreKeeper`/`ObjectiveEvaluator`/any Feature type.
- Forbidden: a live C# `event`/delegate across the Domain→Game boundary; registering a logic subscriber for a derived feature event (append-only, prevents re-entrant `Emit`).

---

## Acceptance Criteria

*From `design/gdd/board-engine.md` §7/§13 + ADR-005, scoped to this story:*

- [ ] `MoveResolver` implements `IBoardEventSink`; `ResolveSwap(Cell a, Cell b)` and `ResolveBootstrap()` run the entire move/bootstrap synchronously and return the move's immutable `IReadOnlyList<BoardEvent>`; the log is cleared for the next move.
- [ ] Fixed logic-subscriber order encoded as a constant list `[ScoreKeeper, ObjectiveEvaluator]` (slots empty at E03; the order constant + dispatch mechanism exist and are asserted). Derived feature events have no logic subscribers (pure append).
- [ ] **Ordering guarantee — special-activation move**: `SwapStarted → SwapAccepted → SpecialActivated → MatchCleared(1, SpecialActivation) → PiecesSpawned(CascadeRefill) → …(cascade from 2)… → CascadeEnded → BoardStabilized`.
- [ ] **Ordering guarantee — bootstrap ("move 0")**: `PiecesSpawned(Bootstrap) → [if accidental match] MatchCleared(1, Bootstrap) → PiecesSpawned(CascadeRefill) → … → CascadeEnded → [if step-9 reshuffle] NoValidMovesDetected → BoardReshuffled → BoardBootstrapped → BoardInputEnabledChanged(true)`.
- [ ] **PieceSnapshot completeness** (§7/§13): `test_swap_started_carries_piece_snapshots`; `test_match_cleared_cleared_pieces_carries_full_identity` (multi-run, multi-color, single step); `test_special_activated_carries_piece_snapshots`; `test_pieces_spawned_fires_per_refilling_state` (each Refilling emits exactly one `PiecesSpawned(CascadeRefill)` covering exactly that pass's cells). `BoardStabilized` fires when the full loop for one triggering event returns to `Idle`.
- [ ] **Deferred-replay tally** (§13 Harvest Observation Point, TR-sc-003): `test_deferred_replay_color_tally_matches_live_tally` — a per-color tally summed from `cleared_pieces` across a **stored + later-replayed** copy of the full stream exactly matches a **live** tally, with zero synchronous board queries (reproduces the §13 2-step red-tile walkthrough).

---

## Implementation Notes

*Derived from ADR-005 D1–D3, D6 + board-engine §7/§13:*

- `MoveResolver` is the Domain composition root for one level's logic (BoardModel now; ScoreKeeper + ObjectiveEvaluator in E04). Its `Emit(BoardEvent e)` appends to the authoritative ordered log, then dispatches synchronously, in the fixed constant order, to the logic-subscriber list.
- Build every payload array/list fully, then construct the record — never retain and mutate a handed-out reference. This is what makes "store now, replay later" safe.
- Preserve the two GDD ordering guarantees **verbatim** as emitter invariants; a determinism test (Story 010) asserts byte-identity of the whole sequence.
- The E04 subscribers (ScoreKeeper, ObjectiveEvaluator) plug into the existing constant list; do not build a dynamic registration path that could reorder them. Derived feature events (`ObjectiveProgressed`/`MovesRemainingChanged`/`LevelResolved`) ride the same sequence but have no logic subscribers — a pure append, no re-entrancy.
- Threading (D6): everything is main-thread single-threaded; `Emit`, dispatch, and the returned snapshot are all produced on one call stack — no locks, no worker threads.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- The board behaviours themselves (Stories 001–008) — this story composes them and validates whole-move ordering.
- `EventBridge` / `InputGate` (Game layer, E05) — the Domain→Game hand-off and the four-term input-lock composition are outside this epic.
- `ScoreKeeper` / `ObjectiveEvaluator` bodies (E04) — this story delivers the pinned-order slot they register into.
- Story 010: the byte-identical determinism master gate (this story validates ordering shape; 010 validates reproducibility).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — orchestration & pinned order**
  - Given: `MoveResolver` with an empty logic-subscriber list.
  - When: `ResolveSwap`/`ResolveBootstrap` run.
  - Then: the whole move resolves synchronously and returns an immutable ordered list; the subscriber-order constant is `[ScoreKeeper, ObjectiveEvaluator]` (asserted); the log clears between moves.
- **AC — ordering guarantees**
  - Given: (a) a special-activation move; (b) a bootstrap with an accidental match + step-9 reshuffle.
  - When: resolved.
  - Then: the emitted sequences match the two §7 ordering guarantees exactly.
- **AC — PieceSnapshot completeness** (`test_swap_started_carries_piece_snapshots`, `test_match_cleared_cleared_pieces_carries_full_identity`, `test_special_activated_carries_piece_snapshots`, `test_pieces_spawned_fires_per_refilling_state`)
  - Given: a multi-run, multi-color, multi-step move.
  - When: resolved.
  - Then: every clear/spawn/place payload carries correct pre-event `color`/`special_type`; each Refilling pass emits exactly one `PiecesSpawned(CascadeRefill)` covering exactly its cells.
- **AC — deferred replay** (`test_deferred_replay_color_tally_matches_live_tally`)
  - Given: the §13 2-step red-tile cascade.
  - When: a per-color tally is computed live and again from a stored+replayed copy of the stream.
  - Then: the two tallies are identical; zero synchronous board queries used in the replayed path.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/board-engine/move_resolver_ordering_test.cs` — must exist and pass (Edit Mode, headless).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Stories 005, 006, 007, 008 (the full move + bootstrap behaviours it composes), E02 (BoardEvent catalog record types copied verbatim from ADR-005).
- Unlocks: Story 010 (determinism master gate), E04 (ScoreKeeper/ObjectiveEvaluator register into the pinned-order slot).
