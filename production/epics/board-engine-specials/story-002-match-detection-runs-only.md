# Story 002: Match detection (runs-only, MIN_RUN_LENGTH = 3, overlap-union clear set)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 4 (Match Detection, MVP Policy: Runs-Only)
**Requirement**: `TR-be-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — supplies the `Run(RunOrientation, int Length, IReadOnlyList<Cell> Cells, int Color)` value type consumed by match detection and seam 3)
**Governing ADRs (secondary)**: `architecture.md` §7.1 (BoardModel match mechanism — architectural home; no dedicated ADR).
**ADR Decision Summary**: `Run` carries its shared `color` directly so no downstream consumer (seam 3, Scoring, Objectives) needs a per-cell lookup to know a run's color.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# — `O(rows×cols)` scan per pass, plain BCL collections, no `UnityEngine`. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: all Domain logic resolves synchronously in one call — no `await`, no timers, no per-frame state. `MIN_RUN_LENGTH = 3` lives in the centralized Domain config (though fixed, not a tuning knob).
- Required: `Run` payload collections are `IReadOnlyList<T>`, immutable by construction.
- Forbidden: any `UnityEngine.*` in Domain; treating T/L-shape recognition as a BoardModel concern (it is not — see Out of Scope).

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] Detects only straight-line runs (horizontal or vertical), length ≥ `MIN_RUN_LENGTH` (fixed at 3). Row scan left→right; column scan top→bottom; a run is a maximal sequence of equal-`color`, non-`COLOR_NONE` cells, recorded as `{orientation, length, cells, color}`.
- [ ] `test_horizontal_run_of_three_detected` / `test_vertical_run_of_three_detected`: minimal positive cases.
- [ ] `test_run_of_two_not_detected`: length-2 sequences never register (MIN_RUN_LENGTH boundary).
- [ ] `test_colorless_piece_never_matches`: a `color = -1` (`COLOR_NONE`) piece is never part of any detected run regardless of neighbours' colors.
- [ ] `test_l_shape_intersection_unions_into_one_clear_set`: an intersecting horizontal + vertical run produces exactly **one** clear set whose cell membership contains the shared cell exactly once (overlap/intersection union rule).
- [ ] The `runs` payload (orientation, length, cell list, color per run) is complete enough that a downstream consumer can compute L/T intersection itself — BoardModel adds no T/L-shape branch.

---

## Implementation Notes

*Derived from board-engine §4 + ADR-005:*

- Mirror the concept prototype's `findRuns()` exactly (runs of 3/4/5, no T/L specials).
- Union rule: when a horizontal and vertical run share a cell in the same cascade step, merge their cell sets into one combined clear set for that step; the shared cell is cleared exactly once and exactly one `MatchCleared` fires for the step (the `MatchCleared` emission itself is wired in Story 005's Clearing state — this story delivers the detection + union that feeds it).
- `Run.Color` is the run's shared value (every cell in a run has the same color by definition); populate it directly on the `Run` record so seam 3 has a ready-made source color for a colored spawn.
- T/L recognition is deliberately excluded: emit the flat run list; a seam-3 consumer computes any L/T classification from it (see `special-candies.md` — deferred Wrapped Candy).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: the `Clearing` state that consumes the union and emits `MatchCleared`.
- Story 012: T/L-shape → Wrapped classification (a seam-3 consumer concern, Vertical Slice scope, not built at MVP).
- Gravity/refill (Story 004); cascade continuation (Story 006).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC-1**: horizontal/vertical run of 3 detected
  - Given: a board with exactly three same-color cells in a line.
  - When: match detection runs.
  - Then: one `Run` with `length = 3`, correct `orientation`, `cells`, and shared `color`.
  - Edge cases: run at a board edge; run adjacent to a `VOID`.
- **AC-2**: length-2 boundary (`test_run_of_two_not_detected`)
  - Given: two same-color cells with differing neighbours.
  - When: detection runs.
  - Then: zero runs recorded.
- **AC-3**: colorless never matches (`test_colorless_piece_never_matches`)
  - Given: three collinear cells where the middle is `color = -1`.
  - When: detection runs.
  - Then: no run spans the colorless piece.
  - Edge cases: a full line of `COLOR_NONE` pieces yields zero runs.
- **AC-4**: L/T union (`test_l_shape_intersection_unions_into_one_clear_set`)
  - Given: a horizontal and a vertical run sharing one cell.
  - When: detection + union runs.
  - Then: one clear set; the shared cell appears exactly once; two `Run` entries are still reported for a seam-3 consumer.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/match_detection_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (board data model + `Piece`/cell-state + `COLOR_NONE` exclusion flag).
- Unlocks: Story 005 (Clearing consumes the clear set), Story 007 (bootstrap fill uses up/left match check), Story 012 (creation rules read `Run.orientation`/`Run.color`).
