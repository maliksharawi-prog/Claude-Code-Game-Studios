# Story 004: ObjectiveEvaluator — tracker registry, field normalization, handler map & progress predicates

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-objectives.md` (Rev 2) — § 1 (scope/roster), § 2 (tracker model & field normalization), § 10 (objective display model), § 11 (extensibility / handler registry), Formulas 1–2
**Requirement**: `TR-lo-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §6 (ObjectiveEvaluator ownership: tracker registry, handler map, win/lose predicate)
**Governing ADRs (secondary)**: ADR-005 (`ObjectiveEvaluator` is the second fixed-order logic subscriber; feature-event records).
**ADR Decision Summary**: One tracker per `objectives[]` entry (keyed by array index, never by type); a normalized `target_value` resolves the `params.target` vs `params.count` divergence exactly once; an extensible `ObjectiveHandler` registry maps `type` string → handler (`score_target`, `collect_color` at MVP).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain, zero engine surface, zero RNG. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: trackers keyed by `objective_index` (array position), never by `type`; `LOW_MOVES_THRESHOLD` and objective targets are data-driven (from `LevelData`), never hardcoded literals.
- Required: field normalization resolved exactly once at tracker init (`target` / `count` → `target_value`); downstream rules read only `target_value`.
- Forbidden: any `UnityEngine.*` in Domain; a static singleton for evaluator state (constructor-injected).

---

## Acceptance Criteria

*From `design/gdd/level-objectives.md`, scoped to this story:*

- [ ] **Tracker init**: at every `board_bootstrapped`, initialize one `Tracker { objective_index, type, params, target_value, current }` per `objectives[]` entry in array order; `current` starts at literal `0` on every bootstrap (Restart/Retry/Next Level all re-init). Level Objective holds no state across attempts.
- [ ] **Field normalization**: `score_target` reads `params.target`, `collect_color` reads `params.count`, both into the single `target_value` — every downstream rule uses `target_value` only.
- [ ] **Handler registry**: an `ObjectiveHandler` map keyed by `type` string (`initialize` / `on_event` / `get_progress`); MVP registers exactly two (`score_target`, `collect_color`). A future type plugs in via a new registration with zero change to the win-evaluation predicate.
- [ ] **Progress fraction** (Formula 1, clamped display): `test` — `current = 23, target_value = 20 → progress_fraction = 1.0` (never 1.15); the uncapped `current` is preserved separately for `ObjectiveResult`.
- [ ] **Completion predicate** (Formula 2, unclamped): `is_complete(tracker) = tracker.current >= tracker.target_value` — identical in shape for both MVP types.
- [ ] **Array-index tracker independence**: two `collect_color` objectives of different colors each get their own tracker; a single cleared piece increments only the matching tracker.

---

## Implementation Notes

*Derived from level-objectives §1/§2/§10/§11 + Formulas 1–2:*

- `ObjectiveEvaluator` is registered as the **second** logic subscriber in `MoveResolver`'s pinned order `[ScoreKeeper, ObjectiveEvaluator]` (slot created in E03 Story 009) — so `get_current_score()` reads already include the current `MatchCleared`'s score.
- Normalize `target` vs `count` at exactly one point (tracker init); never re-read `params.target`/`params.count` in any formula.
- `ObjectiveHandler` = `{ initialize(params) -> TrackerState, on_event(signal, state) -> TrackerState, get_progress(state) -> {current, target_value, is_complete} }`. Win evaluation (Story 006) is generically "AND over every tracker's `is_complete`", agnostic to per-type computation.
- Progress fraction is a **display** value (clamped `[0,1]`); the completion predicate uses the **unclamped** `current` directly. Keep the uncapped `current` for `ObjectiveResult` stats (Story 007).
- The `ObjectiveDisplayModel` shape (for Pre-Level Card + HUD chips) is de-normalized (`target`, not `target_value`); presentation never sees a raw `Tracker`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: the concrete `score_target` (score-provider seam) and `collect_color` (unified counting) handler bodies + move accounting + emission cadence.
- Story 006: win/lose evaluation at `board_stabilized`.
- Story 007: `ResultsData` assembly + persist handshake.
- Blocker-clear / ingredient objective types (post-MVP; only the registry mechanism is built).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — tracker init & reset**
  - Given: a level with multiple objectives; then a Retry.
  - When: `board_bootstrapped` fires each time.
  - Then: one tracker per entry in array order; every `current` starts at 0; no state survives the Retry.
- **AC — field normalization**
  - Given: a `score_target` (`params.target`) and a `collect_color` (`params.count`).
  - When: trackers initialize.
  - Then: both read into `target_value`; downstream rules never touch `params.target`/`params.count`.
- **AC — handler registry**
  - Given: the registry.
  - When: inspected.
  - Then: exactly two handlers (`score_target`, `collect_color`); a new type would register without changing the win predicate.
- **AC — progress fraction & completion** (Formula 1/2)
  - Given: `current = 23, target_value = 20`; and boundary values.
  - When: evaluated.
  - Then: `progress_fraction = 1.0` (uncapped `current = 23` retained); `is_complete` uses `>=` on the unclamped `current`.
- **AC — array-index independence**
  - Given: two `collect_color` objectives of different colors.
  - When: a single piece of one color clears.
  - Then: only the matching tracker increments.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/level-objectives/tracker_registry_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E02 (`LevelData` `objectives`/`move_limit` config), E03 (Story 008 `board_bootstrapped`; Story 009 pinned-order subscriber slot).
- Unlocks: Story 005 (handler bodies + move accounting), Story 006 (win/lose predicate).
