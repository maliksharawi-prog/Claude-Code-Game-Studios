# Story 011: Cell-size / touch-target floor proof across the 3–9 grid range (Formula 4)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: S
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — Formula 4 (Touch-Target Floor Proof), Acceptance Criteria "Touch-Target Floor Proof (Formula 4)"
**Requirement**: `TR-be-006` *(no board TR in `tr-registry.yaml` owns the cell-size/touch-target proof — it is a cross-cutting rendering assumption the board GDD closes on behalf of `touch-input.md`/`level-data-format.md`. Flagged for TR assignment; nearest architectural home is board-engine Formula 4. See note below.)*

**ADR Governing Implementation**: ADR: N/A — pure geometric constant/math proof; no architectural pattern required. Governed by `design/gdd/board-engine.md` Formula 4 (a rendering-layout assumption BoardModel documents but does not own the rendering of).
**ADR Decision Summary**: — (no ADR; the proof is a closed-form `min(available_w/gw, available_h/gh) ≥ 44` check over the 49 schema-legal `(gw, gh)` pairs.)

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# arithmetic (this is a headless math proof, not rendering). The `cell_size_px` value is consumed by `touch-input.md`'s own formulas at runtime; that consumption + the actual stretch-mode rendering assumption live in E05 (input) / E08 (rendering), not here. No `UnityEngine` needed for the proof. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `MIN_TOUCH_TARGET_PX = 44`, `BOARD_SIDE_MARGIN_PX = 40`, `BOARD_TOP_ALLOCATION_PX = 640`, `BOARD_BOTTOM_ALLOCATION_PX = 200`, `CANVAS_WIDTH_PX = 1080`, `CANVAS_HEIGHT_PX = 1920` are centralized Domain/config constants, never scattered literals.
- Forbidden: lowering `MIN_TOUCH_TARGET_PX` below 44 (hard floor).

---

## Acceptance Criteria

*From `design/gdd/board-engine.md` Formula 4, scoped to this story:*

- [ ] `test_cell_size_at_9x9_meets_floor`: `cell_size_px(9, 9)` reproduces Formula 4's worked example (`111.11px`) and is `≥ 44`.
- [ ] `test_cell_size_monotonically_decreasing`: for a sample of `(gw, gh)` pairs across `[3,9]×[3,9]`, `cell_size_px` is verified non-increasing as either dimension increases (empirically confirming the monotonicity argument).
- [ ] `test_cell_size_never_below_floor_for_any_schema_legal_dimension`: an exhaustive loop over all 49 `(gw, gh)` combinations asserts `cell_size_px(gw, gh) ≥ MIN_TOUCH_TARGET_PX` for every one.

---

## Implementation Notes

*Derived from board-engine Formula 4:*

- Implement `cell_size_px(gw, gh) = min((CANVAS_WIDTH_PX − 2×BOARD_SIDE_MARGIN_PX)/gw, (CANVAS_HEIGHT_PX − BOARD_TOP_ALLOCATION_PX − BOARD_BOTTOM_ALLOCATION_PX)/gh)` as a pure function over the centralized constants.
- The proof's global minimum is at `(9, 9)` (jointly non-increasing in both args); `111.11px ≥ 44` with ~2.52× headroom. The exhaustive 49-pair loop is a defense-in-depth check that should structurally never fail.
- This is a headless numeric proof only. The stretch-mode/coordinate-space rendering assumption (`1080×1920` canvas always fully visible) is an implementation decision owned by E05/E08 — this story does not implement rendering or touch handling.
- **TR-ID note for review**: assign or create a TR for the touch-target floor proof (currently unowned by TR-be-001..005). If review decides this belongs to E05/E08 rather than E03 domain-core, move the story accordingly — it is included here only because board-engine Formula 4 states its ACs and the E03 DoD references "all board-engine.md acceptance criteria."

---

## Out of Scope

*Handled by neighbouring stories / other epics — do not implement here:*

- Actual touch-hit-testing and `cell_size_px` runtime consumption (E05, `touch-input.md`).
- The stretch-mode / canvas rendering configuration (E08 rendering).
- Any board-logic behaviour (Stories 001–010).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC-1** (`test_cell_size_at_9x9_meets_floor`)
  - Given: `gw = gh = 9` and the Formula 4 constants.
  - When: `cell_size_px(9,9)` is computed.
  - Then: `≈ 111.11px` and `≥ 44`.
- **AC-2** (`test_cell_size_monotonically_decreasing`)
  - Given: a sample of `(gw, gh)` pairs.
  - When: `cell_size_px` is computed.
  - Then: non-increasing as either dimension increases.
- **AC-3** (`test_cell_size_never_below_floor_for_any_schema_legal_dimension`)
  - Given: all 49 `(gw, gh)` pairs in `[3,9]×[3,9]`.
  - When: each is evaluated.
  - Then: every result `≥ 44`.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/touch_target_floor_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (centralized Domain config constants location). Otherwise standalone.
- Unlocks: None (E05/E08 consume `cell_size_px` at runtime, outside this epic).
