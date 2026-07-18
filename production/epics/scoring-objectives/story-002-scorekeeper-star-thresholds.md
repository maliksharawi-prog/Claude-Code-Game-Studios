# Story 002: ScoreKeeper — reference-score table, star-threshold framework, star evaluation, closest-miss & integer safety

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/scoring-stars.md` (Rev 2) — § 7 (star-threshold framework), § 8 (closest-miss), § 9 (anti-inflation & integer safety), Formulas 4–9
**Requirement**: `TR-ss-002`, `TR-ss-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.3 (Scoring pull API / star computation ownership) + ADR-005 (`ScoreResults` DTO fields feed `ResultsData`)
**Governing ADRs (secondary)**: ADR-005 (`ScoreResults.FinalScore`/`ResultsData.ScoreEarned` are `long`).
**ADR Decision Summary**: Star thresholds are the deterministic output of a level's `move_limit` + `color_pool` size via the `REFERENCE_SCORE_PER_MOVE(K)` table and the fixed star fractions — never hand-typed per level.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain, zero engine surface, zero RNG. `long` final score; Formula 9 proves ~6 orders of magnitude of Int64 headroom (no overflow logic). No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `REFERENCE_SCORE_PER_MOVE(3/4/5)` table, `STAR_1/2/3_FRACTION`, `TILE_BASE_VALUE`, `COLOR_BOMB_ACTIVATION_BONUS` are centralized Domain config constants; `MAX_CASCADE_DEPTH`/`max_cells_per_step` are inherited from Board Engine (not re-declared).
- Required: final score is `long`; no saturating/clamp arithmetic (Formula 9 proves it unnecessary).
- Forbidden: declaring an independent score cap (ties to Board Engine's existing caps); any `UnityEngine.*` in Domain.

---

## Acceptance Criteria

*From `design/gdd/scoring-stars.md`, scoped to this story:*

- [ ] **Reference-score table** (Formula 4): `test_reference_score_per_move_table_matches_worked_computation` — `REFERENCE_SCORE_PER_MOVE(3) = 255`, `(4) = 185`, `(5) = 160`.
- [ ] **Reference max score** (Formula 5): `test_reference_max_score_l1_reference_level` (`25 × 160 = 4,000`); `test_reference_max_score_k3_hypothetical` (`25 × 255 = 6,375`).
- [ ] **Star-threshold framework** (Formula 6): `test_star_threshold_framework_l1_reference_level` (`2,500 / 3,200 / 3,900`); `test_star_threshold_framework_k3_hypothetical` (`3,984 / 5,100 / 6,216`); `test_star_threshold_framework_collect_color_only_fallback` (no `score_target` → `STAR_1_FRACTION × reference_max_score` branch); `test_star_thresholds_always_strictly_increasing`.
- [ ] **Star evaluation** (Formula 7, inclusive `>=`): `test_star_evaluation_boundary_inclusive` (exactly `star_2_score → 2 stars`; exactly `star_3_score → 3`); `test_star_evaluation_all_tiers` (0/1/2/3).
- [ ] **Closest-miss** (Formula 8): `test_closest_miss_ratio_matches_worked_example` (`2,100 / 2,500 → 0.84 / 84`); `test_closest_miss_ratio_clamps_above_one` (`final ≥ star_1 → 1.0`, never `> 1.0`).
- [ ] **Integer safety** (Formula 9): `test_max_move_score_bound_matches_worked_example` (`9×9`, `MAX_CASCADE_DEPTH = 20` → `3,402,000`; fits Int64 with orders-of-magnitude headroom).

---

## Implementation Notes

*Derived from scoring-stars §7–9 + Formulas 4–9:*

- `REFERENCE_SCORE_PER_MOVE(K)` is a small closed 3-value table anchored on `K = 5 → 160` (the prototype's greedy-bot `3,960/25 ≈ 158.4`), scaled by the coincidental-cascade ratio `M(K)/M(5)`; round to nearest 5. Trust the ratio, not the absolute model output.
- `star_1_score` = the level's `score_target` when present, else `round(STAR_1_FRACTION × reference_max_score)`; `star_2/3` = the fixed fractions × `reference_max_score`. Fixed strictly-increasing fractions guarantee `star_1 < star_2 < star_3` by construction (satisfies V16 with no separate check).
- Star evaluation is inclusive at every boundary (`>=`, never `>`).
- Closest-miss ratio clamps to `[0.0, 1.0]`; `score_progress_percent = round(100 × ratio)`.
- Formula 9 is a proof-only degenerate bound (every cell an activated bomb, every step a full-board re-clear) — used to justify no overflow guard; never a runtime clamp.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: `step_score` / activation bonus / chain multiplier (the running total this story evaluates thresholds against).
- Story 003: the `get_current_score`/`get_score_results` pull API packaging these fields.
- The objective-completion dimension of `closest_miss_summary` (Objective's scope — Story 007).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — reference table & max score** (`test_reference_score_per_move_table_matches_worked_computation`, `test_reference_max_score_l1_reference_level`, `test_reference_max_score_k3_hypothetical`)
  - Given: `K = 3/4/5`; L1 (25 moves, 5 colors) and a K3 (25 moves, 3 colors) level.
  - When: the table + `reference_max_score` are computed.
  - Then: `255/185/160`; `4,000`; `6,375`.
- **AC — thresholds** (`test_star_threshold_framework_l1_reference_level`, `..._k3_hypothetical`, `..._collect_color_only_fallback`, `test_star_thresholds_always_strictly_increasing`)
  - Given: L1, K3, and a collect_color-only level; plus a synthetic `(move_limit, color_pool)` corpus.
  - When: thresholds are derived.
  - Then: `2,500/3,200/3,900`; `3,984/5,100/6,216`; fallback branch used; strictly increasing everywhere.
- **AC — star eval & closest-miss** (`test_star_evaluation_boundary_inclusive`, `test_star_evaluation_all_tiers`, `test_closest_miss_ratio_matches_worked_example`, `test_closest_miss_ratio_clamps_above_one`)
  - Given: scores on/around each threshold; `2,100` vs `2,500`; a score ≥ star_1 on a lose.
  - When: evaluated.
  - Then: boundary-inclusive stars; `0.84/84`; ratio clamps to `1.0`.
- **AC — integer safety** (`test_max_move_score_bound_matches_worked_example`)
  - Given: `9×9`, `MAX_CASCADE_DEPTH = 20`.
  - When: Formula 9 is computed.
  - Then: `3,402,000`, fits Int64 with headroom.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scoring-stars/star_thresholds_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (`final_score` accumulator + constants location), E02 (`LevelData` `move_limit`/`color_pool`/`score_target`/star fields).
- Unlocks: Story 003 (pull API returns `stars_earned`/closest-miss from here).
