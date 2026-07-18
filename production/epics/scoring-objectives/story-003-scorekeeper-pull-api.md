# Story 003: ScoreKeeper pull API — `get_current_score()` & `get_score_results() → ScoreResults`

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/scoring-stars.md` (Rev 2) — § 10 (`ScoreResults` fields, pulled not pushed), § 10a (the ratified pull interface)
**Requirement**: `TR-ss-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.3 (`IScoreProvider`: `GetCurrentScore`, `GetScoreResults → ScoreResults`) + ADR-005 (D3 subscriber ordering; `ScoreResults` DTO — `long FinalScore`, `int StarsEarned`, `float ScoreProgressRatio`, `int ScoreProgressPercent`)
**Governing ADRs (secondary)**: ADR-005 (the pinned `[ScoreKeeper, ObjectiveEvaluator]` order — ScoreKeeper must process a `MatchCleared` before ObjectiveEvaluator reads `get_current_score()` on that event).
**ADR Decision Summary**: Two synchronous, read-only query seams — never signals, never a push `finalize_results`. `get_current_score()` returns the live running total; `get_score_results()` returns the `ScoreResults` struct at/after the resolving `board_stabilized`. Objective is the sole `ResultsData` assembler; Scoring exposes only score-owned fields.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain, zero engine surface. `ScoreResults.FinalScore` is `long`. Synchronous plain-function query pattern (never a signal). No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: `MoveResolver` dispatches logic subscribers in the fixed constant order `[ScoreKeeper, ObjectiveEvaluator]`; assert the order in a test. `ScoreResults.FinalScore` is `long`.
- Forbidden: a push-style `finalize_results(objectives_resolution)` seam (dropped in Rev 2); an `is_new_best_score`/`is_new_best_stars` field on `ScoreResults` (Screen Flow owns that comparison); reading Objective's internal state from Scoring.

---

## Acceptance Criteria

*From `design/gdd/scoring-stars.md` § 10/§10a, scoped to this story:*

- [ ] `get_current_score() -> long` returns the exact live running `final_score` at the instant called: `test_get_current_score_returns_live_running_total` — called mid-cascade (between two `MatchCleared` of the same move) returns the exact total accumulated so far, never a stale cached value.
- [ ] `get_score_results() -> ScoreResults` (`{ final_score: long, stars_earned: int, score_progress_ratio: float, score_progress_percent: int }`): `test_get_score_results_final_score_matches_final_score`; `test_get_score_results_stars_earned_matches_formula_7`; `test_get_score_results_closest_miss_score_progress_fields` (Formula 8 for a LOSE fixture).
- [ ] `test_get_score_results_never_includes_best_score_flag`: interface inspection confirms no `is_new_best_score`/`is_new_best_stars`-equivalent field.
- [ ] `test_scoring_never_receives_objectives_resolution`: interface inspection confirms no `finalize_results()`-style seam accepting an `ObjectivesResolution` parameter exists.
- [ ] **Pinned subscriber order**: on any `MatchCleared`, `get_current_score()` read by `ObjectiveEvaluator` already includes that step's `step_score` (ScoreKeeper processed first) — asserted via the ordering test (ADR-005 D3).

---

## Implementation Notes

*Derived from scoring-stars §10/§10a + ADR-005 D3 + `architecture.md` §8.3:*

- Expose `IScoreProvider` with the two pull seams; `get_score_results()` composes `final_score` (Story 001) + `stars_earned`/closest-miss (Story 002) into the `ScoreResults` struct.
- `get_score_results()` is valid to call any time at/after the resolving `board_stabilized` since `final_score` is complete by then (scoring accrues synchronously in Board Engine's own resolution frame).
- The pinned order `[ScoreKeeper, ObjectiveEvaluator]` is the mechanism that makes `score_target` evaluation correct — encode it as the constant list (E03 Story 009 created the slot); a determinism test asserts `get_current_score()` on a `MatchCleared` already includes that step. This is the sole ordering assertion owned here; Objective's consumption of it is Story 005.
- `ScoreResults` carries no `ResultsData`/`ObjectivesResolution` knowledge — Scoring stays unaware of Objective state (one-directional read).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001/002: the underlying accrual + threshold/closest-miss math packaged here.
- Story 005: `ObjectiveEvaluator`'s consumption of `get_current_score()` for `score_target`.
- Story 007: `ResultsData` assembly (Objective is the sole assembler; it pulls `get_score_results()`).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — get_current_score** (`test_get_current_score_returns_live_running_total`)
  - Given: a 2-step cascade.
  - When: `get_current_score()` is called between the two `MatchCleared` events.
  - Then: returns the exact partial running total, not a stale value.
- **AC — get_score_results** (`test_get_score_results_final_score_matches_final_score`, `..._stars_earned_matches_formula_7`, `..._closest_miss_score_progress_fields`)
  - Given: a resolved attempt with authored thresholds (LOSE fixture).
  - When: `get_score_results()` is called.
  - Then: `final_score`/`stars_earned`/`score_progress_ratio`/`score_progress_percent` match Formulas 2/7/8.
- **AC — omissions** (`test_get_score_results_never_includes_best_score_flag`, `test_scoring_never_receives_objectives_resolution`)
  - Given: the `ScoreResults` struct + `IScoreProvider` interface.
  - When: inspected.
  - Then: no best-score flag; no `finalize_results`/`ObjectivesResolution` seam.
- **AC — pinned order**
  - Given: a `MatchCleared` dispatched through `MoveResolver`.
  - When: `ObjectiveEvaluator` reads `get_current_score()` on that event.
  - Then: the value already includes that step's `step_score` (ScoreKeeper processed first).

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/scoring-stars/score_pull_api_test.cs` — must exist and pass (drives `MoveResolver` end-to-end for the ordering assertion).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (`final_score`), Story 002 (`stars_earned`/closest-miss), E03 (Story 009 `MoveResolver` pinned-order slot).
- Unlocks: Story 005 (`score_target` reads `get_current_score`), Story 007 (`ResultsData` pulls `get_score_results`).
