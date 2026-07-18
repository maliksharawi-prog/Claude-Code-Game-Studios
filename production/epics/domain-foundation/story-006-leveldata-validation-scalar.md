# Story 006: LevelData.Validate() — V1–V19 scalar & structural rules (blocking + advisory)

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-data-format.md` (§4 Validation Contract V1–V19, Formulas A & B, Edge Cases, Acceptance Criteria)
**Requirement**: `TR-ldf-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: N/A — no dedicated ADR. `Validate()` is governed by master architecture **§6 (LevelData `Validate()`)**, the architectural home for TR-ldf-003. (The V8 flood-fill is Story 007; the manifest W8 cross-check is Story 013 under ADR-006.)
**ADR Decision Summary**: A level file is invalid — and must not load — if it violates any **Blocking** rule; **Advisory** rules (V18) log a warning but do not block. Validation is a pure Domain function returning a violation list, callable headlessly and by the level-validation test suite.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure C#)
**Engine Notes**: None — validation is pure BCL over the POCO. Domain cannot `Debug.Log`; violations are returned as data (a list), and the Game/Editor layer forwards them to the console.

**Control Manifest Rules (Domain layer)**:
- Required: closed-enum values (`objectives[].type`, `candy_type` in `color_pool`) are hard failures when unrecognized — never silently ignored (V13).
- Required: every gameplay constant is data-driven — `MIN_PLAYABLE_CELLS = 16` and the canonical candy roster come from the centralized Domain config, not literals.
- Required: `Validate()` is pure and headless-testable (no engine types, no IO); returns violations as data.
- Guardrail: static V1–V19 cannot check achievability-within-move-limit — that is a smoke check (Config/Data tier), explicitly out of this Blocking suite.

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-format.md` §4 (V1–V19, excluding V8) and Acceptance Criteria, scoped to this story:*

- [ ] `Validate()` implements every V1–V19 rule **except V8** (Story 007), returning a typed violation per failed rule with its severity: V1 (`schema_version` in `{1}`), V2 (`level_id` non-empty + unique across the scanned set), V3 (`region` non-empty), V4 (`display_number` positive + unique), V5 (`grid_width`/`grid_height` in `[3,9]`), V6 (`cell_mask` row/char counts match dims; chars are `'1'`/`'0'`; defaults to all-`'1'`), V7 (`'1'` cell count ≥ `MIN_PLAYABLE_CELLS`=16), V9 (`pre_placed` within bounds, on playable cells, no duplicate `(row,col)`), V10 (`pre_placed.candy_type` ∈ `color_pool`), V11 (`color_pool` 3–5 unique canonical members), V12 (`move_limit` ≥ 1), V13 (`objectives` ≥ 1; every `type` ∈ `{score_target, collect_color}` — unknown is a hard fail), V14 (`score_target.target` > 0), V15 (`collect_color.color` ∈ `color_pool`, `count` > 0), V16 (star scores > 0 and strictly increasing), V17 (if a `score_target` exists, `star_1_score ≤ target`), V18 (**Advisory** — `star_3_score ≤ reference_max_score`), V19 (`rng_seed` ≥ -1).
- [ ] Blocking rules produce an invalid verdict; V18 produces an advisory warning only.
- [ ] V18 evaluates `reference_max_score` against the per-`color_pool`-size `RSPM(K)` table (`K=3→255, K=4→185, K=5→160`) that supersedes the flat `160` (Formula A note / scoring-stars Formula 4), for the level's actual `color_pool` size — still Advisory.
- [ ] `test_v16_rejects_non_increasing_star_thresholds`: `[2500,2500,3000]` fails V16.
- [ ] `test_v15_rejects_collect_color_off_pool`: a `collect_color` whose `color` is absent from `color_pool` fails V15.
- [ ] `test_v7_rejects_under_16_playable_cells`: a `cell_mask` with < 16 `'1'` cells fails V7.
- [ ] `test_v2_rejects_duplicate_level_id`: a directory scan with two files sharing a `level_id` fails V2.
- [ ] The reference level (8×8, 5-color, `move_limit 25`, `score_target 2500`, stars `[2500,3200,3900]`) passes every rule V1–V19 with zero Blocking failures and zero Advisory warnings.

---

## Implementation Notes

*Derived from GDD §4, Formulas A/B, and Acceptance Criteria:*

- Place `LevelValidator.cs` (or `LevelData.Validate()`) under `Assets/Domain/Levels/`, returning `IReadOnlyList<ValidationViolation>` with `(RuleId, Severity, Detail)`.
- Uniqueness rules (V2 `level_id`, V4 `display_number`) are **set-scoped**: `Validate()` takes the scanned collection (or a companion `ValidateSet(...)`) so duplicates across files are caught at directory-scan time — matching the level-validation test that iterates every file under `assets/data/levels/`.
- V6 defaulting composes with Story 005's `cell_mask` default; when absent, validate against the synthesized full-rectangle mask.
- V17 is conditional: skip when the level has no `score_target` objective (a `collect_color`-only level) — score still gates stars via V16.
- V18 uses `RSPM(K)`; keep the `K→value` table and the raised safe-range ceiling (260) in centralized Domain config. Document that Formula A's flat `160` is superseded and V18 stays Advisory (level-data-format Open Question — promotion to Blocking is deferred).
- The in-project level-validation test iterates fixtures under `Assets/Tests/EditMode/Levels/Fixtures/` including the reference level and one deliberately-failing fixture per rule under test.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: the POCO shape/defaults/closed-enum modelling this validator reads.
- Story 007: V8 connectivity flood-fill (single 4-connected region).
- Story 013: W8 (level `region` field ↔ manifest `region_code`) — a manifest **cross**-check, not a single-file rule.
- The smoke-check for `collect_color` achievability-within-move-limit (Config/Data tier, not this Blocking suite).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: reference level passes clean (from level-data-format AC).
  - Given: the reference-level fixture.
  - When: `Validate()` runs.
  - Then: zero Blocking failures and zero Advisory warnings.

- **AC-2**: star-threshold & objective rules (`test_v16_...`, `test_v15_...`, `test_v14`, `test_v17`).
  - Given: fixtures with `[2500,2500,3000]` stars; a `collect_color` off `color_pool`; a `score_target` of `0`; a `star_1_score > target`.
  - When: validated.
  - Then: V16, V15, V14, V17 respectively fail (Blocking); V17 is skipped when no `score_target` is present.

- **AC-3**: board-shape & pool rules (`test_v7_...`, `test_v5`, `test_v11`, `test_v10`).
  - Given: a mask with < 16 playable cells; a `2×2` grid; a `color_pool` of 6 / with a non-canonical member; a `pre_placed.candy_type` off-pool.
  - When: validated.
  - Then: V7, V5, V11, V10 respectively fail.

- **AC-4**: set-scoped uniqueness & advisory (`test_v2_...`, V4, V18).
  - Given: two files sharing a `level_id` (and separately a shared `display_number`); a level whose `star_3_score` exceeds `RSPM(K) × move_limit`.
  - When: the set is validated.
  - Then: V2 and V4 fail Blocking; V18 emits an Advisory warning only (does not invalidate).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/level-data-format/leveldata_validation_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Levels/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 005** (the POCO to validate). Transitively **E01 Story 002**.
- Unlocks: Story 007 (V8 completes the validation suite), Story 013 (W8 composes with the per-file `region` value validated here), E10 (the 10 MVP levels are validated against this suite).
