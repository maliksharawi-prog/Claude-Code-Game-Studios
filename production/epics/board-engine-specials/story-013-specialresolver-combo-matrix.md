# Story 013: `SpecialResolver` swap-triggered combo matrix (seams 1 & 2, Formulas 4–7)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/special-candies.md` — § Detailed Rules 5 (Swap-Triggered Combo Matrix, seams 1 & 2), Formulas 4–7
**Requirement**: `TR-sc-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.1 (SpecialResolver implements seam 1 `is_special_activation_swap` + seam 2 `resolve_special_activation_clears` with zero Board Engine change)
**Governing ADRs (secondary)**: ADR-005 (`SpecialType` enum; the union with a coexisting normal match is handled by BoardModel's clear-set union, Story 005).
**ADR Decision Summary**: The combo matrix is exactly four cells (Bomb+Color, Bomb+Bomb, Bomb+Striped, Striped+Striped) because only Color Bomb (colorless) structurally requires seam 1/2; Striped candies participate in normal run detection. **Do NOT thread `ScoreKeeper` through seam 2** (ADR-005 supersedes the stale §8.1 signature).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, zero engine surface, zero RNG. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: seam 2 returns a `Set[cell]` computed by Formulas 4–7; the dispatch order checks Bomb+Bomb first (it would otherwise also satisfy the "at least one bomb" branch).
- Forbidden: threading `ScoreKeeper`/any Feature type through seam 2; extending/narrowing seam 2's `Set[cell]` return type; implementing solo-Striped-swap-with-anything activation (deliberate MVP scope cut).

---

## Acceptance Criteria

*From `design/gdd/special-candies.md`, scoped to this story:*

- [ ] **Seam 1 dispatch**: `is_special_activation_swap = (a or b is COLOR_BOMB) OR (both a and b ∈ {STRIPE_H, STRIPE_V})`. A Striped + regular candy with no resulting match returns `false` (falls through to normal validity — the deliberate MVP scope cut; no solo-Striped swap-fire).
- [ ] **Seam 2 dispatch** (priority order): Formula 5 (Bomb+Bomb) → Formula 7 (Bomb+Striped) → Formula 4 (Bomb+Color) → Formula 6 (Striped+Striped).
- [ ] **Formula 4 — Bomb + Color**: `test_bomb_plus_color_clears_bomb_cell_and_matching_color` — clear set = the bomb cell ∪ every cell of the partner's color (exact live count).
- [ ] **Formula 5 — Bomb + Bomb**: `test_bomb_plus_bomb_clears_every_occupied_cell` — every `OCCUPIED` cell, independent of color diversity (fixture with only 1 distinct color present covers the `<2`-colors edge case).
- [ ] **Formula 6 — Striped + Striped**: each piece fires its own line from its post-swap landing cell. `test_striped_plus_striped_same_orientation_same_axis_swap_single_line` (8 cells); `test_striped_plus_striped_same_orientation_cross_axis_swap_double_line` (16); `test_striped_plus_striped_mixed_orientation_cross` (15); `test_striped_plus_striped_ignores_color_mismatch` (different colors still combo).
- [ ] **Formula 7 — Bomb + Striped ("jackpot")**: `test_bomb_plus_striped_clears_full_lines_for_every_matching_cell` — union of the full row/column (per the Striped's orientation) of every cell sharing the Striped's color.
- [ ] `test_wrapped_combo_cells_structurally_unreachable`: no code path produces a `WRAPPED`-typed piece at MVP (the "N/A — structurally unreachable" matrix rows are true by construction).

---

## Implementation Notes

*Derived from special-candies §5 + Formulas 4–7 + `architecture.md` §8.1:*

- Implement seam 1 as the single boolean dispatch above; implement seam 2 as the priority-ordered branch selecting Formulas 4–7. Bomb+Bomb is checked first.
- Formula 6 fires each piece from its **post-swap landing cell** (`piece_a`, which moved into `cell_b`, fires along its own orientation from `cell_b`; `piece_b` from `cell_a`) — this matches what the player's eye tracks during the swap slide.
- Formula 7 reproduces the full "every matching candy detonates its line" result using only seam 2's `Set[cell]` return (no intermediate spawn, no composite v2 seam) — union each same-colored cell's own full line.
- `line(H/V, cell)` = every `OCCUPIED` cell sharing that row/column; `VOID` cells are trivially excluded (never `OCCUPIED`) — no cross-column-segment special-casing needed.
- Coexistence with a normal match is handled by BoardModel's clear-set union (Story 005); this resolver only returns seam 2's activation clears.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 012: creation rules (seam 3).
- Story 014: passive chain reaction (seam 4) — a Bomb/Striped caught in a cascade rather than swap-activated.
- Story 015: reshuffle survival, determinism, seam-compat closers.
- Wrapped-involving combos (structurally unreachable at MVP; Vertical Slice).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — seam 1 dispatch**
  - Given: Bomb+anything, Striped+Striped, and Striped+regular-no-match swaps.
  - When: `is_special_activation_swap` runs.
  - Then: `true` for any bomb-involving swap and Striped+Striped; `false` for Striped+regular-no-match (falls through to normal validity).
- **AC — Formulas 4/5** (`test_bomb_plus_color_clears_bomb_cell_and_matching_color`, `test_bomb_plus_bomb_clears_every_occupied_cell`)
  - Given: a fixture with a known partner-color count; a fully-occupied board incl. a 1-color fixture.
  - When: seam 2 resolves.
  - Then: Bomb+Color = count + bomb cell; Bomb+Bomb = every `OCCUPIED` cell regardless of color diversity.
- **AC — Formula 6** (`test_striped_plus_striped_*`)
  - Given: same-axis, cross-axis, and mixed-orientation Striped+Striped swaps; and a color-mismatch pair.
  - When: seam 2 resolves.
  - Then: 8 / 16 / 15 cells respectively; color mismatch computes identically to same-color.
- **AC — Formula 7** (`test_bomb_plus_striped_clears_full_lines_for_every_matching_cell`)
  - Given: a fixture with N cells of the Striped partner's color across M distinct rows/columns.
  - When: seam 2 resolves.
  - Then: clear set = union of those M full lines.
- **AC — Wrapped unreachable** (`test_wrapped_combo_cells_structurally_unreachable`)
  - Given: this resolver's creation + combo paths.
  - When: inspected.
  - Then: no path produces a `WRAPPED` piece at MVP.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/special-candies/combo_matrix_test.cs` — must exist and pass (one fixture-driven test per matrix cell).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 012 (`SpecialResolver` base + vocabulary), Story 005 (seams 1/2 invoked in swap validity + clear-set union), Story 003 (seam interface).
- Unlocks: Story 014 (passive chain), Story 015 (closers).
