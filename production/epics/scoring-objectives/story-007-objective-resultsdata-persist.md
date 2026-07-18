# Story 007: ObjectiveEvaluator — `ResultsData` assembly, persist-before-`LevelResolved` (`record_level_completion`) & `level_resolved` emission

> **Epic**: Scoring & Objectives (Domain Feature) — E04
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-objectives.md` (Rev 2) — § 9 (the `level_resolved` / `ResultsData` handoff; persistence-write-precedes-resolution-event ordering)
**Requirement**: `TR-lo-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — D4: the fixed win-persistence order — evaluate outcome → pull `ScoreResults` → compose `ResultsData` → `ISaveWriter.RecordLevelCompletion(levelId, stars, score)` on WIN → **only then** emit `LevelResolved`)
**Governing ADRs (secondary)**: ADR-003: Save Serialization (the `record_level_completion` / `RecordLevelCompletion` API shape — a Domain write interface the Game `SaveService` implements; `best_score`/`score` are `long`).
**ADR Decision Summary**: Level Objective is the **sole** `ResultsData` assembler: it pulls Scoring's `get_score_results()`, composes `ResultsData` with its own `outcome` + objective-completion data, calls `RecordLevelCompletion` on WIN strictly before emitting `LevelResolved` (never on LOSE), and never inspects/retries the write. This is the 2026-07-18 win-persistence fix.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# Domain — `ISaveWriter.RecordLevelCompletion` is a Domain **interface**; the concrete `SaveService` implementation (file IO) is E06. `stars`/`score` cross the interface as `int`/`long`. No post-cutoff API in the Domain path.

**Control Manifest Rules (Domain)**:
- Required (D4, fixed): on `BoardStabilized` with `outcome == WIN` — evaluate outcome → pull `ScoreResults` → compose `ResultsData` → `ISaveWriter.RecordLevelCompletion(levelId, stars, score)` → **only then** emit `LevelResolved`. Never called on LOSE. Step 4 strictly precedes step 5.
- Required: `score`/`best_score` is `long` (Int64). `ObjectiveEvaluator` emits `LevelResolved` through `MoveResolver.Emit` (append-only, no logic subscriber).
- Forbidden: a `finalize_results()` push seam into Scoring (dropped Rev 2); inspecting/retrying the `RecordLevelCompletion` result from the Domain side; any `UnityEngine.*` / file IO in Domain.

---

## Acceptance Criteria

*From `design/gdd/level-objectives.md` § 9, scoped to this story:*

- [ ] **`ResultsData` assembly** (sole assembler): given a mock `get_score_results()` returning a stub `ScoreResults`, the emitted `LevelResolved` event's `ResultsData` composes `score_earned`/`stars_earned`/`closest_miss_summary.score_progress_ratio`/`score_progress_percent` from that stub **verbatim**, `outcome` from this document's own Formula 4 result, and `objectives_final` (per-tracker final `current`/`target_value`/`is_complete`, uncapped `current`), `moves_used`, `moves_remaining`.
- [ ] **Win → persistence ordering**: given a mock `record_level_completion()` seam, a WIN resolution invokes it **exactly once** with `level_id`/`stars_earned`/`score_earned` matching the composed `results_data`, **strictly before** the `level_resolved` listener observes the event; a LOSE resolution invokes it **zero times**.
- [ ] `level_resolved(outcome, results_data)` is fired by ObjectiveEvaluator (the single emitter), using the self-assembled record — never a value passed through verbatim from Scoring.
- [ ] The `record_level_completion` call is fire-and-forget: ObjectiveEvaluator neither inspects nor retries its result; whether the write durably succeeds is Save & Persistence's concern (E06).

---

## Implementation Notes

*Derived from ADR-005 D4 + ADR-003 + level-objectives §9:*

- At the resolving `BoardStabilized` (outcome determined by Story 006), execute D4's five steps **in order**: (1) evaluate outcome; (2) `IScoreProvider.GetScoreResults()`; (3) compose `ResultsData` (LevelId pass-through, `outcome`, `ScoreEarned = ScoreResults.FinalScore`, `StarsEarned`, `ClosestMiss` = score dims from `ScoreResults` + this attempt's objective-completion data, `ObjectivesFinal`); (4) **if WIN** `ISaveWriter.RecordLevelCompletion(levelId, stars, score)`; (5) emit `LevelResolved(outcome, results)`.
- Step 4 strictly precedes step 5 — persistence is requested at **resolve** time so a mid-replay app-kill still records the win, while the Results-screen transition (E05/E07) is gated later on `juice_input_lock`. Never call on LOSE.
- `ISaveWriter` is a Domain interface (keeps ObjectiveEvaluator engine-free); the Game `SaveService` implements it in E06. Depend only on the interface — the write IO is E06's responsibility (persist-before-event ordering is what this story owns).
- The `finalize_results()` push seam is fully dropped (Rev 2): Scoring never receives `ObjectivesResolution`; ObjectiveEvaluator composes `outcome`/`closest_miss_summary` itself.
- The objective-completion dimension of `closest_miss_summary` shape remains an Open Question — carry `objectives_final` as its raw source; do not invent a final shape.

---

## Out of Scope

*Handled by neighbouring stories / other epics — do not implement here:*

- Story 003: `get_score_results()` itself (pulled here).
- Story 006: outcome determination + state machine (this story fires at the instant it detects).
- E06: the concrete `SaveService`/`ISaveStore` file IO behind `ISaveWriter.RecordLevelCompletion` (durability, A/B slots, checksum). This story depends only on the interface.
- E05/E07: routing `LevelResolved` to the Results screen (gated on `juice_input_lock`).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — ResultsData assembly**
  - Given: a mock `get_score_results()` returning a stub `ScoreResults`; resolved trackers.
  - When: `level_resolved` is emitted.
  - Then: `ResultsData` composes the score fields verbatim, `outcome` from Formula 4, and `objectives_final`/`moves_used`/`moves_remaining` from this attempt's trackers.
- **AC — win persistence ordering**
  - Given: a mock `record_level_completion()` seam + a `level_resolved` listener.
  - When: a WIN resolves.
  - Then: `record_level_completion` invoked exactly once with matching `level_id`/`stars`/`score`, strictly before the listener observes `level_resolved`.
- **AC — lose never persists**
  - Given: the same mocks.
  - When: a LOSE resolves.
  - Then: `record_level_completion` invoked zero times; `level_resolved(LOSE, …)` still emitted.
- **AC — fire-and-forget**
  - Given: a `record_level_completion` mock returning failure/success.
  - When: a WIN resolves.
  - Then: ObjectiveEvaluator neither inspects nor retries the result; ordering is unaffected.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/level-objectives/resultsdata_persist_ordering_test.cs` — must exist and pass (mock `IScoreProvider` + mock `ISaveWriter`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 006 (outcome + resolution instant), Story 003 (`get_score_results()`), E03 (Story 009 `BoardStabilized` + `MoveResolver.Emit`), E02/E06 (`ISaveWriter` interface — Domain-side; concrete impl is E06).
- Unlocks: None (E04 epic closer — Scoring & Objectives fully proven in Edit Mode; `LevelResolved` consumed by E05/E07).
