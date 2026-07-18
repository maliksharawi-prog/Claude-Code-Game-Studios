# Story 004: Gravity & segment-scoped refill (stop-at-void; bootstrap-retry vs cascade-uniform asymmetry)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 9 (Gravity & Column Segmentation), § Detailed Rules 10 (Refill), § Detailed Rules 6 (RNG draw-ordering contract)
**Requirement**: `TR-be-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity Guard (primary — BoardModel draws the `board-refill` stream through `IRngService`)
**Governing ADRs (secondary)**: ADR-005 (the `PiecesSpawned` event refill emits); `architecture.md` §7 / board-engine §9–10 (gravity mechanism — architectural home).
**ADR Decision Summary**: All `board-refill` draws use `IRngService.NextColor(...)` via the exact integer multiply-shift `(raw × range) >> 32` — never `System.Random`, never `floor(next_float × range)`. Draw order is the single fixed call order in board-engine §6; determinism depends on the platform-stable SplitMix32 generator.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# — no `UnityEngine`. RNG is single-threaded (no `[ThreadStatic]`/locks); no `float`/`Mathf`/`Math.Floor` on the draw path. Determinism must hold byte-identically across Mono/IL2CPP/WebGL (owned by ADR-004's golden vectors). No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `board-refill` draws use `IRngService.NextColor` with the integer multiply-shift; `level_id` reaching RNG is a resolved `int` ordinal (Story 007).
- Required: refill emits `PiecesSpawned(pieces, source)` with an `IReadOnlyList<PieceSnapshot>` payload, immutable by construction; never emitted with an empty `pieces` list.
- Forbidden: `System.Random` / `UnityEngine.Random`; `float`/`Mathf`/`Math.Floor` on the RNG draw path; hashing a `level_id` string to seed.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] Gravity operates **independently within each segment** (Formula 3); a piece never crosses from one segment into another. **Stop-at-void**: pieces settle toward their own segment's bottom row, never falling through a `VOID` (`GRAVITY_MODE = stop_at_void`, fixed at MVP).
- [ ] `test_gravity_stops_at_void_never_falls_through`, `test_gravity_independent_per_segment`, `test_fully_void_column_never_touched`.
- [ ] **Cascade-step refill** uses plain uniform draws (`NextColor("board-refill", color_pool)`), **no retry / no filtering** — a refilled cell may coincidentally complete a run (`test_cascade_refill_allows_immediate_match`).
- [ ] **Bootstrap fill** uses **retry-until-no-match** (redraw if placing would create a run of ≥3 with already-placed up/left neighbours, up to `BOOTSTRAP_MAX_RETRIES_PER_CELL = 100`, then accept last draw unconditionally): `test_bootstrap_fill_never_allows_immediate_match_for_rng_cells`.
- [ ] `test_refill_draw_order_matches_documented_contract`: refill draws occur in exactly column (0→cols-1) → segment (top→bottom) → cell (top→bottom) order, one `NextColor` per `EMPTY` cell after gravity compaction.
- [ ] Each completed Refilling pass emits exactly one `PiecesSpawned(source = CascadeRefill)` covering exactly the cells filled that pass; bootstrap's combined fill emits one `PiecesSpawned(source = Bootstrap)` (bootstrap emission wired in Story 007).

---

## Implementation Notes

*Derived from board-engine §9/§10/§6 + ADR-004:*

- Gravity: for each column, for each segment, compact `OCCUPIED` pieces toward the segment's `bottom_row`; the vacated cells above become `EMPTY`. Never move a piece across a `VOID`.
- Refill traversal (the RNG draw-order contract §6, situation 3): column `0→cols-1`; within a column, each segment top→bottom; within a segment, each `EMPTY` cell after compaction, top→bottom; one `NextColor` per cell, assign `color`, `special_type = SPECIAL_NONE`, a fresh `piece_id`, mark `OCCUPIED`.
- The bootstrap/cascade **asymmetry is load-bearing**: bootstrap fill (retry-until-no-match) vs cascade refill (plain uniform, no retry). Implement them as two distinct fill routines sharing the same traversal order but differing only in the redraw predicate.
- Refill knows nothing about "a piece visually enters from above the screen" — it only records `EMPTY → OCCUPIED` with a final `(row, col)`. Presentation entry paths are Juice-layer scope.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 007: the full bootstrap procedure that calls the bootstrap-fill routine + emits `PiecesSpawned(Bootstrap)`.
- Story 005/006: the Falling→Refilling transitions inside the resolution loop that call gravity/refill per cascade step.
- Story 008: reshuffle's `board-refill` `shuffle()` draws (a different draw situation).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — gravity per-segment & stop-at-void** (`test_gravity_stops_at_void_never_falls_through`, `test_gravity_independent_per_segment`, `test_fully_void_column_never_touched`)
  - Given: a column with a mid-column void and cleared cells in one segment.
  - When: gravity compacts.
  - Then: pieces settle to their own segment's bottom; the void is never crossed; the other segment is unaffected; a fully-void column is never touched.
- **AC — cascade refill allows a match** (`test_cascade_refill_allows_immediate_match`)
  - Given: a mocked `board-refill` stream engineered to produce a matching run on refill.
  - When: cascade-step refill runs.
  - Then: the run is NOT filtered/retried; it exists on the next Matching pass.
- **AC — bootstrap fill avoids a match** (`test_bootstrap_fill_never_allows_immediate_match_for_rng_cells`)
  - Given: bootstrap fill with retries under the cap.
  - When: an RNG-filled cell is placed.
  - Then: no run of ≥3 exists through it at placement.
  - Edge cases: retries exhaust `BOOTSTRAP_MAX_RETRIES_PER_CELL` → last draw accepted unconditionally (deterministic).
- **AC — draw order** (`test_refill_draw_order_matches_documented_contract`)
  - Given: a mocked `board-refill` stream logging draws.
  - When: a refill pass runs.
  - Then: draws occur in exactly column→segment→cell order, one per `EMPTY` cell.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/gravity_refill_test.cs` — must exist and pass (seeded `IRngService`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (segments + cell-state), Story 002 (bootstrap fill's up/left match check), E02 (`IRngService` + `board-refill` stream).
- Unlocks: Story 005 (Falling/Refilling states), Story 007 (bootstrap fill), Story 008 (reshuffle shares the `board-refill` stream).
