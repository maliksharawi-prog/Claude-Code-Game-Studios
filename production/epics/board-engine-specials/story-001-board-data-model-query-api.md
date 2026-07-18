# Story 001: Board data model, cell/piece state, column segmentation & synchronous query API

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 1 (Board Model), § Detailed Rules 8 (Board State Query API), Formula 3 (Column Segmentation)
**Requirement**: `TR-be-005`, `TR-be-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — supplies the `Cell` / `PieceSnapshot` value types and the `IBoardEventSink` injection point)
**Governing ADRs (secondary)**: `architecture.md` §7.1 / §8.2 (BoardModel ownership + board-state query surface — the architectural home; no dedicated ADR).
**ADR Decision Summary**: `Cell` is a `readonly record struct(int Row, int Col)` (supersedes the blueprint's hand-rolled `R*16+C` hash); `PieceSnapshot(Cell, int Color, SpecialType Special)` is the value-snapshot identity type. `BoardModel` depends only on an injected `IBoardEventSink`; it never names a Feature type.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# `record` / `readonly record struct` + BCL collections only — no `UnityEngine` type by construction (CI-guarded). C# 9 records are the Unity 6 default; verify the `SweetCascade.Domain.asmdef` compiles headlessly in the Edit-Mode runner. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: Domain assembly `noEngineReferences: true`; zero `UnityEngine`/`UnityEditor` symbols (CI-guarded). `BoardModel` emits every event only through an injected `IBoardEventSink` and never references `ScoreKeeper`/`ObjectiveEvaluator`/any Feature type by name.
- Required: every GDD/ADR constant (`MIN_RUN_LENGTH`, `COLOR_NONE`, `SPECIAL_NONE`, grid bounds) lives in one centralized Domain config location — never a scattered literal.
- Forbidden: any `UnityEngine.*` type in Domain; a live C# `event`/delegate across the Domain→Game boundary.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] Cell-state is a three-value enum per grid position: `VOID` (mask `'0'`, never occupied/targeted), `EMPTY` (transient, only between Clearing and Refilling), `OCCUPIED` (holds exactly one `Piece`). An `EMPTY` cell still present at `Idle` is a contract violation (asserted in debug builds).
- [ ] `Piece` carries `piece_id` (monotonic, unique for its lifetime), `color ∈ [-1, pool_size-1]` (`COLOR_NONE = -1`), `special_type ∈ {0} ∪ opaque non-zero` (`SPECIAL_NONE = 0` is the only value BoardModel assigns/interprets), and `row`/`col` always kept in sync with the grid's `OCCUPIED` record.
- [ ] A `color = -1` (`COLOR_NONE`) piece is permanently excluded from every color-based run comparison (enforced here at the data layer; consumed by Story 002).
- [ ] Column segmentation (Formula 3): a **segment** is a maximal contiguous run of playable (`EMPTY`/`OCCUPIED`) cells in one column, bounded by a `VOID` or a board edge; reproduces the Formula 3 5×5 worked example (a mid-column void → two height-2 segments). A fully-`VOID` column produces zero segments.
- [ ] Synchronous query API (§8, plain function calls, never signals): `get_grid_dimensions()`, `get_cell_state(row,col)`, `get_piece_at(row,col) → Piece|null`, `is_playable_cell(row,col)`, `get_column_segments(col)`, `is_board_input_enabled()`, `get_current_chain_index()` (`0` when Idle).

---

## Implementation Notes

*Derived from ADR-005 Key Interfaces + board-engine §1/§3/§8:*

- Copy `Cell`, `PieceSnapshot`, and the `SpecialType`/`ColorSentinel` types **verbatim** from ADR-005's Key Interfaces (authored as Domain foundation in E02 per this epic's Depends-On — consume them as-is; if E02's decomposition placed the catalog elsewhere, author `Assets/Domain/Events/` from ADR-005 first). `Cell` uses value equality/hashing — never re-introduce the `R*16+C` hash.
- `BoardModel`'s constructor takes `IBoardEventSink` (ADR-005 D2). No event is emitted in this story beyond structural setup — behaviours emit in later stories through this injected sink.
- Precompute `get_column_segments` once at allocation and cache; gravity/refill (Story 004) consume it. Keep segments a `{top_row, bottom_row}` value list.
- `get_current_chain_index()` mirrors the live resolution counter (Story 006 drives it); return `0` at `Idle`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: run detection over `color` (this story only enforces the `COLOR_NONE` exclusion at the data layer).
- Story 004: gravity/refill that consume the segment list.
- Story 007: bootstrap that populates the grid from `cell_mask`.
- Story 009: `MoveResolver` — the concrete `IBoardEventSink` implementation and ordered log.

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — cell-state & EMPTY invariant**
  - Given: a grid allocated from a `cell_mask` with a `'0'` cell.
  - When: `get_cell_state` is queried across VOID/EMPTY/OCCUPIED positions.
  - Then: masked cells report `VOID`; occupied cells report `OCCUPIED`; a debug-build assert fires if any `EMPTY` cell is observed at `Idle`.
  - Edge cases: fully-`VOID` column; single-cell playable segment.
- **AC — Piece identity & COLOR_NONE**
  - Given: pieces spanning the legal `color`/`special_type` ranges including `color = -1`.
  - When: fields are read back.
  - Then: `piece_id` is unique per lifetime; `SPECIAL_NONE = 0`; a `color = -1` piece is flagged excluded from color comparison.
  - Edge cases: `color = pool_size-1` boundary; `special_type` non-zero opaque value stored and returned unchanged.
- **AC — segmentation matches worked example** (`test_segmentation_matches_worked_example`)
  - Given: the Formula 3 5×5 board with column 2 row 2 void.
  - When: `get_column_segments(2)` is computed.
  - Then: returns `[{0,1},{3,4}]`; columns without voids return one `{0,4}` segment.
  - Edge cases: fully-void column → empty list; void at a board edge.
- **AC — query API surface**
  - Given: a populated board.
  - When: each query function is called.
  - Then: all return synchronously (no await/signal); `get_current_chain_index()` is `0` at `Idle`.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/board_model_query_api_test.cs` — must exist and pass (Edit Mode, headless).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E02 (BoardEvent catalog record types — `Cell`/`PieceSnapshot`/`SpecialType` — + `SweetCascade.Domain` assembly & purity guard authored as Domain foundation).
- Unlocks: Story 002 (match detection over this model), Story 003 (seams operate on `Piece`), Story 004 (gravity/refill consume segments).
