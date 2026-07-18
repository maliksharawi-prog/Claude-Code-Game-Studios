# Story 009: Results Transition Waits on juice_input_lock + Retry Skips Pre-Level Card

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Presentation (ScreenFlowController)
> **Type**: Integration
> **Estimate**: M (~4h)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/screen-flow.md` (§Detailed Rules 10a, §6) · `design/gdd/juice-layer.md` (§10)
**Requirement**: `TR-sf-003` (Results transition waits on `juice_input_lock` with safety ceiling; retry skips Pre-Level Card)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D3) + architecture.md §7.3
**ADR Decision Summary**: `ScreenFlowController` routes `LevelResolved` to the Results screen, gating the base-state transition on `juice_input_lock`; the win is persisted at resolve time (D4) while the Results screen waits for the final cascade to finish replaying.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Timing/gating logic — the wait is on the `juice_input_lock` signal (story 006) with a hard safety ceiling; simulate both the lock-clears path and the ceiling-elapses path by mocking signals/time. No new engine surface.

**Control Manifest Rules (this layer — Game/UI):**
- Required: T15/T16 hold the base-state change until `juice_input_lock` returns `false` OR `RESULTS_TRANSITION_SAFETY_CEILING_MS` elapses (whichever first); `ScreenFlowController` never writes level results directly (persistence is the Objectives→Save seam, resolve-time).
- Forbidden: never transition to Results before the wait condition; never soft-lock on a stuck Reveal Queue (the ceiling is mandatory).
- Guardrail: retry latency budget (`RETRY_LATENCY_BUDGET_MS = 2000`) for the resident-level retry path; base transition ≤ `MAX_SCREEN_TRANSITION_MS`.

---

## Acceptance Criteria

*From GDD `design/gdd/screen-flow.md` §Detailed Rules 10a + §6, scoped to this story:*

- [ ] On `level_resolved(WIN, results_data)` (T15) / `level_resolved(LOSE, results_data)` (T16), the base-state transition to `RESULTS_WIN` / `RESULTS_LOSE` is deferred until `juice_input_lock` returns `false` (the board reads visually idle and the final move's Reveal Queue has finished).
- [ ] A hard safety ceiling `RESULTS_TRANSITION_SAFETY_CEILING_MS` forces the transition if `juice_input_lock` never clears — whichever of (lock cleared) or (ceiling elapsed) occurs first triggers the transition; a stuck/defective Reveal Queue can never soft-lock the game.
- [ ] The final-move cascade is never cut off mid-animation by an early Results transition (the "clever, not lucky" peak-drama beat is preserved).
- [ ] Retry (T17) from `RESULTS_WIN` or `RESULTS_LOSE` transitions directly to `(B3,[])` — the Pre-Level Card is unconditionally skipped; the resulting state is never `(B2,[O1])`.
- [ ] Restart (T11) from Pause likewise skips the Pre-Level Card and re-bootstraps with `attempt_number + 1`.

---

## Implementation Notes

*Derived from ADR-005 D3/D4 and `screen-flow.md` §10a, §6:*

- `level_resolved` can fire the instant Level Objective's synchronous win/lose evaluation resolves — typically well before the Juice Layer has finished (or even started) presenting that move's cascade (the §7 three-way timing gap). Do not transition immediately; wait on `juice_input_lock`.
- Persistence ordering is already handled at resolve time (ADR-005 D4: `RecordLevelCompletion` strictly precedes `LevelResolved`, WIN only) — the Results *screen* transition is a separate, replay-time concern gated here. The win is durably recorded even if the app is killed mid-replay, yet Results never celebrates a win Save was not first asked to record.
- The safety ceiling protects the opposite failure mode (a hung queue). Default `RESULTS_TRANSITION_SAFETY_CEILING_MS = 15000` (Tuning Knobs) — must stay above `juice-layer.md` Formula 2's worst-case `total_presentation_ms(MAX_CASCADE_DEPTH)` (~14.1s) so a legitimate deep cascade is never cut off.
- The Pre-Level Card is only shown on first entry (T4) or a genuinely new level (T18); both retry paths (T11, T17) skip it — this is the flow-state-critical path.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 006: producing the `juice_input_lock` signal (consumed here).
- Story 008: the base T1–T21 transition table and composite-state validity (extended here only for T15/T16 timing).
- E04: computing `ResultsData`/`stars_earned`/`outcome` and calling `RecordLevelCompletion` (Domain-side, resolve-time) — this story only consumes the `LevelResolved` event and routes it.
- E07: the Results screen layout, star-ceremony composition, closest-miss display.

---

## QA Test Cases

*Integration story — Play Mode / simulated-signal integration test; deterministic (mock `juice_input_lock` edges and a mock clock for the ceiling).*

- **AC-1 (wait on lock)**: Given `level_resolved(WIN)` fired while `juice_input_lock = true`, Then no base transition occurs; When the lock clears, Then transition to `RESULTS_WIN` on that edge. Same for LOSE → `RESULTS_LOSE`.
- **AC-2 (safety ceiling)**: Given the lock never clears, Then the transition fires exactly when `RESULTS_TRANSITION_SAFETY_CEILING_MS` elapses (mock clock); whichever of lock/ceiling is first wins.
- **AC-3 (no early cut-off)**: A final-move multi-step cascade completes its Reveal Queue before the Results transition when the lock clears normally.
- **AC-4 (retry skips card)**: T17 from `RESULTS_WIN`/`RESULTS_LOSE` → `(B3,[])` (never `(B2,[O1])`); T11 from Pause → `(B3,[])`; both increment `attempt_number` by 1.
- **AC-5 (persist-before-celebrate, integration)**: Assert `RecordLevelCompletion` (E04/E06 seam) was observed before `LevelResolved` was routed to Results (order check against the drained sequence).

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/screen-flow/results_wait_on_juice_lock_test.cs` (Play Mode / simulated signals) — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 008 (state machine), Story 006 (`juice_input_lock`), Story 007 (a real replay to wait on); E04 (`level_resolved` / `ResultsData` producer).
- Unlocks: E07 (Results screen consumes the routed transition + `ResultsData`).
