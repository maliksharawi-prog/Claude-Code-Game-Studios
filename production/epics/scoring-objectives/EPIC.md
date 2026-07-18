# Epic: Scoring & Objectives (Domain Feature)

> **Epic ID**: E04
> **Layer**: Feature
> **GDD**: `design/gdd/scoring-stars.md` (Rev 2) · `design/gdd/level-objectives.md` (Rev 2)
> **Architecture Module**: `SweetCascade.Domain` — ScoreKeeper (`long` accumulator, activation bonus, chain multiplier, star thresholds, pull API) and ObjectiveEvaluator (tracker registry, move counter, win/lose predicate, `ResultsData` assembly, persistence handshake)
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories scoring-objectives`

## Scope

The two Domain Feature systems that turn board events into progression truth. **ScoreKeeper**
observes `MatchCleared` from the MoveResolver sink and accrues a 64-bit `long` running total —
unified per-piece activation bonus (priced from `MatchCleared.ClearedPieces`, no BoardModel
threading), linear chain multiplier, `REFERENCE_SCORE_PER_MOVE(K)` table, and 1/2/3-star
thresholds — exposing the pull API (`GetCurrentScore()`, `GetScoreResults()`). **ObjectiveEvaluator**
runs the tracker registry (`score_target`, `collect_color`) with field normalization and an
extensible handler map, counts moves on `swap_accepted`, evaluates win/lose **only** at
`board_stabilized` (with last-move-cascade win), assembles `ResultsData`, calls
`record_level_completion()` on WIN **before** firing `level_resolved`, and emits
`objective_progressed` / `moves_remaining_changed`. Pure Domain, headless-testable.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: Event Bridge & BoardEvent Catalog | D3 ScoreKeeper observes the sink; D4 ObjectiveEvaluator assembles `ResultsData` + persist-before-`LevelResolved` on WIN | LOW |
| arch §8.3 | Scoring pull API (`IScoreProvider`: `GetCurrentScore`, `GetScoreResults → ScoreResults`) | — |
| arch §6 | ObjectiveEvaluator ownership, handler registry, win/lose predicate | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ss-001 | `step_score` from `match_cleared` only; unified per-piece activation bonus; linear chain multiplier | ADR-005 (D3) ✅ |
| TR-ss-002 | `long` score accumulator (64-bit); `REFERENCE_SCORE_PER_MOVE(K)` table | ADR-005 (long) + arch §2 ✅ |
| TR-ss-003 | Pull API: `get_current_score()`, `get_score_results() → ScoreResults` | arch §8.3 + ADR-005 ✅ |
| TR-lo-001 | Objective tracker registry (`score_target`, `collect_color`), field normalization, extensible handler map | arch §6 (ObjectiveEvaluator) ✅ |
| TR-lo-002 | Win/lose evaluated **only** at `board_stabilized`; last-move-cascade win | ADR-005 (D3/D4) ✅ |
| TR-lo-003 | Assembles `ResultsData`; fires `level_resolved`; calls `record_level_completion()` on WIN **before** the event | ADR-005 (D4) ✅ |
| TR-lo-004 | Move accounting on `swap_accepted`; `moves_remaining_changed`, `objective_progressed` | ADR-005 (D3) ✅ |

## Depends On

- **E01** (assemblies + purity guard + CI).
- **E02** (LevelData objectives config; RNG not required directly).
- **E03** (the BoardEvent stream + `MatchCleared.ClearedPieces` it observes; MoveResolver sink).

> Note: ObjectiveEvaluator's `record_level_completion()` call targets `ISaveService` (a
> consumer-defined interface implemented by **E06**). The Domain evaluator depends only on the
> interface, not on E06's IO — the write itself is E06's responsibility (persist-before-event ordering).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **LOW — pure Domain, zero engine surface** (CI-enforced).
- **`int → long` widening** is the GDD's own position (scoring-stars §9), corrected from the
  blueprint placeholder — `FinalScore` and the accumulator are `Int64`, not C# 32-bit `int`.
- **Design watch:** scoring and objectives are tightly coupled but one-directional (Objective
  reads live score; Scoring never reads Objective state) — resolved as a read, not a cycle
  (systems-index Circular Dependencies).

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- ScoreKeeper reproduces the specified `step_score`, activation bonus, chain multiplier, and
  star thresholds against seeded event fixtures; the `long` accumulator is proven not to
  overflow at the reference ceiling.
- ObjectiveEvaluator evaluates win/lose only at `board_stabilized`, honors last-move-cascade
  win, assembles correct `ResultsData`, and orders `record_level_completion()` strictly before
  `LevelResolved` on WIN — all in Edit Mode.
- All scoring-stars.md and level-objectives.md acceptance criteria have passing Edit Mode
  tests in `tests/unit/`.

## Next Step

Run `/create-stories scoring-objectives`.
