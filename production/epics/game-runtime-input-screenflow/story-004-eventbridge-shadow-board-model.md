# Story 004: EventBridge Hand-off + Shadow Board Model

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Presentation (JuiceDirector replay core)
> **Type**: Logic
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/juice-layer.md` (§§1–2)
**Requirement**: `TR-jl-001` (capture-then-replay: Shadow Board Model + Reveal Queue, single active queue — Shadow Board Model + capture side)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D1, D3 Mode B)
**ADR Decision Summary**: The Domain produces one ordered, immutable `IReadOnlyList<BoardEvent>` per move; `EventBridge` (Game) receives the whole snapshot after the move fully resolves and drains it. Presentation reconstructs every historical piece from `PieceSnapshot` payloads with zero live queries (reshuffle aside).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Plain C# consuming the E02 `BoardEvent` catalog. The Shadow Board Model is engine-free grid bookkeeping — keep it headless-testable (Edit Mode) with a mocked event stream; no `UnityEngine` types in the reconstruction logic.

**Control Manifest Rules (this layer — Game)**:
- Required: `EventBridge.EnqueueMove` asserts no un-drained prior move exists (single-active-queue invariant); presentation consumers read the drained snapshot, never live Domain state.
- Forbidden: Never wire a live C# event/delegate from Domain across the Domain→Game boundary for board events; never query live `BoardModel` state during replay (sole exception is `BoardReshuffled`'s one live `get_piece_at()` sweep — story 006, not here).
- Guardrail: single-active-queue means no steady-state buffer growth; a worst-case `MatchCleared` carries ≤81 `PieceSnapshot`s (transient, human-paced).

---

## Acceptance Criteria

*From GDD `design/gdd/juice-layer.md` §§1–2 + ADR-005 D1/D3, scoped to this story:*

- [ ] `EventBridge.EnqueueMove(snapshot)` accepts one fully-resolved move's immutable `IReadOnlyList<BoardEvent>` and asserts no un-drained prior move exists (single-active-queue).
- [ ] The Shadow Board Model initializes once per level entry from `BoardBootstrapped` (grid dimensions, `cell_mask`) + the following `PiecesSpawned(source=BOOTSTRAP)` (every playable cell's `(color, special_type)`).
- [ ] From that point every `MatchCleared.ClearedPieces` entry removes a cell; every `SpecialSpawned`/`PiecesSpawned` entry adds one — applied in the exact order signals arrive; the shadow grid is an exact independently-derived replica of the historical board at each event instant.
- [ ] Replaying `board-engine.md` §Detailed Rules 13's worked 2-step-cascade event sequence through the Shadow Board Model reproduces the exact documented final grid state (every cell's `color`/`special_type`) with **zero** live board queries performed.
- [ ] The presentation reads only the drained snapshot — no consumer mutates Domain state or calls back into Domain to decide a gameplay outcome.

---

## Implementation Notes

*Derived from ADR-005 D1/D3 Mode B and `juice-layer.md` §2:*

- `EventBridge` is a plain in-memory hand-off with no engine dependency; `GameController` hands the immutable per-move snapshot to `EnqueueMove` only after `ResolveSwap`/`ResolveBootstrap` fully returns, and the Domain log is cleared immediately after.
- Build the Shadow Board Model entirely from the subscribed event stream — never from a live query. `PieceSnapshot = (Cell, Color, SpecialType)` is a value snapshot at the instant the event fired.
- Treat all payload collections as immutable (`IReadOnlyList<T>`) — never retain and mutate a reference. This is what makes "store now, replay later" safe.
- Reconstruction requires no `piece_id`: position-preserving gravity compaction resolves the mapping uniquely (see `juice-layer.md` Cross-References) — do not assume a persistent ID exists in the payload.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: deriving the ordered Reveal Queue + replay scheduler from the captured burst.
- Story 006: gravity re-derivation, the `juice_input_lock` signal, and the `BoardReshuffled` one-live-query exception.
- Story 007: the `BoardPresenter` that animates pooled grey-box transforms.
- The E02 `BoardEvent` catalog itself (consumed here, authored in E02/Domain).

---

## QA Test Cases

*Logic story — automated Edit Mode specs; deterministic, driven by a mocked Board Engine event stream, no real timers/RNG.*

- **AC-1 (single-active-queue assert)**: Given a move already enqueued and not drained, When `EnqueueMove` is called again, Then it asserts/throws (invariant enforced). Edge cases: drain fully, then a second enqueue succeeds.
- **AC-2 (bootstrap construction)**: Given `BoardBootstrapped` + `PiecesSpawned(BOOTSTRAP)`, Then the shadow grid matches the spawned `(color, special_type)` for every playable cell and respects `cell_mask` voids.
- **AC-3 (event application order)**: Given an interleaved burst of `MatchCleared`/`SpecialSpawned`/`PiecesSpawned`, Then the shadow grid after each event equals the historical board at that instant (assert per-event, not just final).
- **AC-4 (`test_shadow_board_reconstruction_matches_worked_walkthrough`)**: Replaying `board-engine.md` §13's worked 2-step cascade reproduces the exact documented final grid with zero live queries (assert a live-query counter == 0).
- **AC-5 (read-only)**: Assert no Domain mutation API is invoked during reconstruction (spy/mock the Domain surface).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/juice-layer/shadow_board_model_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E02 (BoardEvent record types + a bootstrapped board's event stream); E01 (assemblies).
- Unlocks: Story 005 (Reveal Queue consumes the Shadow Board Model + drained snapshot), Story 006 (gravity re-derivation reads the shadow grid).
