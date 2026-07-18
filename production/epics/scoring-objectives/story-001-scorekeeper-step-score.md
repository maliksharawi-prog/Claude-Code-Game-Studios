# Story 001: ScoreKeeper — `step_score`, unified per-piece activation bonus & linear chain multiplier

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/scoring-stars.md` (Rev 2) — § Detailed Rules 1 (canonical signal: `match_cleared` only), § 2 (unified activation bonus), § 3 (linear chain multiplier), § 6 (bootstrap never scores), Formulas 1–3
**Requirement**: `TR-ss-001`, `TR-ss-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — D3 Mode A: `ScoreKeeper` is the first fixed-order logic subscriber, consuming `MatchCleared` only, reading `special_type` directly from `MatchCleared.ClearedPieces`)
**Governing ADRs (secondary)**: `architecture.md` §2 (64-bit `long` score).
**ADR Decision Summary**: `ScoreKeeper` consumes `MatchCleared` only; it accrues `step_score = chain_index × (TILE_BASE_VALUE × |cleared| + Σ activation_bonus(special))`, reading each `PieceSnapshot.Special` from `MatchCleared.ClearedPieces`. It needs no side-channel — the blueprint's `OnSpecialConsumed`/`_pendingBonus` accumulator is **superseded**. It ignores `SpecialActivated` to avoid double-counting.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain, zero engine surface (CI-guarded). `long` (Int64) accumulator — the GDD's own `int → long` widening (scoring-stars §9). Zero RNG consumed. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `best_score`/final score is `long` (Int64), never `int`. Scoring reads exclusively from `MatchCleared.ClearedPieces`.
- Required: `TILE_BASE_VALUE`, `STRIPE_ACTIVATION_BONUS`, `COLOR_BOMB_ACTIVATION_BONUS` are centralized Domain config constants, never scattered literals.
- Forbidden: threading `ScoreKeeper` through the four Special Candies seams (superseded pattern); summing `SpecialActivated.ClearedPieces` (double-counts); any `UnityEngine.*` in Domain.

---

## Acceptance Criteria

*From `design/gdd/scoring-stars.md`, scoped to this story:*

- [ ] **Canonical signal**: `final_score` is the running sum of every non-`BOOTSTRAP` `MatchCleared` event's `step_score`; no other signal (`SpecialActivated`, `PiecesSpawned`, `CascadeEnded`, `SwapAccepted`, `BoardReshuffled`) contributes score.
- [ ] **Chain multiplier** (Formula 1, linear): `test_chain_multiplier_linear` — `chain_multiplier(n) = n` for `n = 1,2,5,20`.
- [ ] **Activation bonus** (Formula 2): `test_activation_bonus_lookup` — `SPECIAL_NONE → 0`, `STRIPE_H = STRIPE_V → 60`, `COLOR_BOMB → 180`.
- [ ] **step_score** (Formula 2): `test_step_score_no_specials_matches_worked_example_a` (60 then 120 → 180 across a 2-step cascade); `test_step_score_solo_stripe_matches_worked_example_b` (220); `test_step_score_bomb_plus_bomb_matches_worked_example_c` (1,640).
- [ ] **Derived combo table** (Formula 3): `test_combo_matrix_derived_bonus_table` — each of the five combo rows reproduces its exact derived bonus (`60/180/360/240/120`) via Formula 2 alone (no independent second lookup).
- [ ] **Exclusions**: `test_bootstrap_triggered_clears_score_zero` (`trigger = BOOTSTRAP` contributes 0, regardless of cell count/specials); `test_reshuffle_never_contributes_score`; `test_invalid_swap_never_contributes_score`.

---

## Implementation Notes

*Derived from ADR-005 D3 + scoring-stars §1–3, 6, Formulas 1–3:*

- `ScoreKeeper` is registered as the **first** logic subscriber in `MoveResolver`'s fixed constant order `[ScoreKeeper, ObjectiveEvaluator]` (the slot is created in E03 Story 009; register the concrete `ScoreKeeper` here). It handles `MatchCleared` only.
- Read `special_type` directly from each `MatchCleared.ClearedPieces` `PieceSnapshot` — do NOT read `SpecialActivated` (a subset of the following `MatchCleared`; summing double-counts).
- The combo matrix's entire pricing falls out of Formula 2 as a consequence: a step with two `COLOR_BOMB` snapshots prices at `2 × 180`; a bomb+stripe at `180 + 60`. Formula 3's table is a derived reference, not a second source of truth.
- `BOOTSTRAP`-sourced `MatchCleared` is outside Formula 2's domain entirely (not merely zero-valued) — never accrues.
- Accumulate into a `long`; Formula 9's degenerate ceiling (~3.4M/move) sits ~6 orders of magnitude below the Int64 ceiling — no overflow/clamp logic.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: reference-score table, star thresholds, closest-miss, integer-safety bound.
- Story 003: the pull API (`get_current_score`, `get_score_results`).
- E03: the `MatchCleared` event + the `MoveResolver` subscriber slot (consumed here).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — multiplier & bonus** (`test_chain_multiplier_linear`, `test_activation_bonus_lookup`)
  - Given: chain indices 1/2/5/20; special types NONE/STRIPE/BOMB.
  - When: the functions are evaluated.
  - Then: `chain_multiplier(n) = n`; bonuses `0/60/180`.
- **AC — step_score worked examples** (`test_step_score_no_specials_matches_worked_example_a`, `..._solo_stripe_..._b`, `..._bomb_plus_bomb_..._c`)
  - Given: the three worked-example `cleared_pieces` fixtures.
  - When: `step_score` is computed and summed.
  - Then: 60/120 (→180); 220; 1,640 exactly.
- **AC — derived combo table** (`test_combo_matrix_derived_bonus_table`)
  - Given: `cleared_pieces` fixtures matching each of the five combo rows.
  - When: Formula 2 runs.
  - Then: bonuses `60/180/360/240/120` — no independent lookup.
- **AC — exclusions** (`test_bootstrap_triggered_clears_score_zero`, `test_reshuffle_never_contributes_score`, `test_invalid_swap_never_contributes_score`)
  - Given: a `BOOTSTRAP` `MatchCleared`; a `BoardReshuffled`; a `SwapRejected`.
  - When: processed.
  - Then: `final_score` unchanged in every case.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/scoring-stars/step_score_test.cs` — must exist and pass (mocked `MatchCleared` event fixtures, no live RNG).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E03 (Story 009 `MoveResolver` sink + fixed subscriber slot; `MatchCleared` with `PieceSnapshot.Special` identity), E02 (Domain assembly + config).
- Unlocks: Story 002 (thresholds read `final_score`), Story 003 (pull API exposes the accumulator).
