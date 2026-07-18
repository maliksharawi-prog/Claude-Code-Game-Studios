# Story 012: `SpecialResolver` creation rules — eligibility, same-axis orientation, anchoring & cluster precedence (seam 3, Formulas 1–3)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/special-candies.md` — § Detailed Rules 1 (MVP roster & vocabulary), § Detailed Rules 2 (creation rules), § Detailed Rules 3 (Wrapped/T-L deferral), § Detailed Rules 4 (cluster precedence), Formulas 1–3
**Requirement**: `TR-sc-001`, `TR-sc-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.1 (the seam contract — SpecialResolver implements seam 3 `resolve_special_spawns` with zero Board Engine change)
**Governing ADRs (secondary)**: ADR-005 (`SpecialType` enum: `StripeH=1, StripeV=2, ColorBomb=3, Wrapped=4` reserved; `SpecialSpawn` shape).
**ADR Decision Summary**: Special creation is expressed entirely through Board Engine's seam 3, returning `Map[cell, SpecialSpawn]`; the `special_type` vocabulary is append-only with `WRAPPED = 4` reserved (never renumbering `COLOR_BOMB = 3`).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, zero engine surface (CI-guarded). Consumes zero RNG — every creation rule is a deterministic function of run geometry. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `special_type` vocabulary is append-only; `SpecialType` copied from ADR-005 (`WRAPPED = 4` reserved). The resolver returns cells to seam 3 that are all real members of the winning run (passes Board Engine's own defensive validation).
- Required: creation eligibility, anchoring, and cluster precedence are pure deterministic functions of `Run` geometry — zero RNG draws.
- Forbidden: any `UnityEngine.*`; extending/narrowing seam 3's signature; producing a `WRAPPED`-typed piece at MVP.

---

## Acceptance Criteria

*From `design/gdd/special-candies.md`, scoped to this story:*

- [ ] **Eligibility & type** (Formula 1): run length 3 → no spawn; length 4 → Striped (same-axis: horizontal→`STRIPE_H` clears row, vertical→`STRIPE_V` clears column); length ≥ 5 → `COLOR_BOMB` (colorless, `color = null → COLOR_NONE`). `test_match3_run_produces_no_spawn`; `test_horizontal_match4_creates_stripe_h_same_axis`; `test_vertical_match4_creates_stripe_v_same_axis`; `test_match5_creates_colorless_color_bomb`.
- [ ] **Anchoring** (Formula 2): swapped-cell-first, run-middle fallback. `test_anchor_prefers_swap_cell_when_swap_anchor_available`; `test_anchor_tiebreak_both_swap_cells_interior_to_run` (nearer-to-middle wins); `test_anchor_falls_back_to_run_middle_when_no_swap_anchor` (`swap_anchor_cells = {}` at `chain_index ≥ 2` or `BOOTSTRAP`).
- [ ] **Cluster precedence** (Formula 3): a cluster (runs connected by shared cells) grants **at most one** spawn — the longest eligible (≥4) run; ties prefer a run touching `swap_anchor_cells`, then lexicographically-smallest cell. `test_cluster_precedence_5_run_beats_4_run`; `test_pure_tl_intersection_produces_zero_spawns` (two length-3 runs → zero spawns, all cells clear via one `MatchCleared`); `test_disjoint_clusters_each_spawn_independently`; `test_no_cell_ever_claimed_by_two_spawns`.
- [ ] The losing run in a cluster still clears normally (its cells are simply omitted from the returned spawn map, never removed from Board Engine's clear set).

---

## Implementation Notes

*Derived from special-candies §1–4 + Formulas 1–3 + `architecture.md` §8.1:*

- Implement `SpecialResolver.resolve_special_spawns(runs, swap_anchor_cells) -> Map[cell, SpecialSpawn]` as the concrete seam 3. `spawn_type` is a closed 4-value function of `length` + `orientation`; `spawn_color = run.color` for Striped, `null` for Color Bomb.
- Same-axis (not perpendicular Candy-Crush convention) is the ruling — a horizontal match-4 → `STRIPE_H`. Do not add swap-direction as an input.
- Anchor (Formula 2): compute `run_middle_cell` from the run's own coordinate bounds (storage-order-independent); if a `swap_anchor_cell` lies in the run, prefer it (nearer-to-middle on a 2-overlap tie), else the middle cell.
- Cluster precedence (Formula 3): compute connected components of `runs` by shared cells; per cluster, pick the longest `length ≥ 4` run (tie → prefer swap-anchor-touching, then lex-smallest cell); a cluster with no eligible run grants zero spawns (the pure T/L case). At most one `SpecialSpawn` per cluster — this structurally prevents two spawns claiming one cell.
- T/L recognition is not computed at MVP (Wrapped deferred to Vertical Slice); a pure T/L clears as an ordinary match with no spawn.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 013: the swap-triggered combo matrix (seams 1 & 2).
- Story 014: passive chain reaction (seam 4) + Formula 8.
- Story 015: seam-compatibility, reshuffle survival & determinism closers.
- Wrapped Candy creation/activation (Vertical Slice scope — not built).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — eligibility/type** (`test_match3_run_produces_no_spawn`, `test_horizontal_match4_creates_stripe_h_same_axis`, `test_vertical_match4_creates_stripe_v_same_axis`, `test_match5_creates_colorless_color_bomb`)
  - Given: runs of length 3, horizontal-4, vertical-4, and ≥5.
  - When: `resolve_special_spawns` runs.
  - Then: no spawn; `STRIPE_H` at run color; `STRIPE_V` at run color; `COLOR_BOMB` with `color = null → COLOR_NONE` (confirmed excluded from a later Matching pass).
- **AC — anchoring** (`test_anchor_prefers_swap_cell_when_swap_anchor_available`, `test_anchor_tiebreak_both_swap_cells_interior_to_run`, `test_anchor_falls_back_to_run_middle_when_no_swap_anchor`)
  - Given: Formula 2's worked examples (single overlap; both interior; empty anchor set).
  - When: the anchor is resolved.
  - Then: matches the worked outputs exactly; the anchor is always a real cell of the run.
- **AC — cluster precedence** (`test_cluster_precedence_5_run_beats_4_run`, `test_pure_tl_intersection_produces_zero_spawns`, `test_disjoint_clusters_each_spawn_independently`, `test_no_cell_ever_claimed_by_two_spawns`)
  - Given: Formula 3's worked examples + a property-style multi-run corpus.
  - When: spawns are resolved.
  - Then: 5-run beats 4-run (one spawn); pure T/L → zero spawns (all cells still clear); two disjoint match-4s → two independent spawns; no cell is ever claimed by two clusters.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/special-candies/creation_rules_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 (`ISpecialResolver` seam interface), Story 009 (seam 3 invoked in the resolution loop for end-to-end verification), Story 002 (`Run` geometry).
- Unlocks: Story 013 (combo matrix), Story 014 (passive chain), Story 015 (closers).
