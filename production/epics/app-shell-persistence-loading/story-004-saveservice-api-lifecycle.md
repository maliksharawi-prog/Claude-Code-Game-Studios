# Story 004: SaveService — API Surface & Lifecycle Triggers

> **Epic**: App Shell — Boot, Persistence IO & Content Loading (E06)
> **Status**: Ready
> **Layer**: Foundation (Game-side)
> **Type**: Integration
> **Estimate**: M (~4h)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (§3 save triggers, §10 no mid-level resume, §11 API surface)
**Requirement**: `TR-sp-004` (`record_level_completion` / `load_profile` / `get_profile` / `update_setting` / `flush_if_dirty` API)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-003: Save Serialization & Atomic Durability. Secondary: ADR-005 (D4 — win-persistence ordering: `RecordLevelCompletion` strictly precedes `LevelResolved`, WIN only).
**ADR Decision Summary**: `SaveService.FlushIfDirty()` fires on `OnApplicationPause(true)`/focus-lost/quit; settings writes are debounced 750ms; `ISaveWriter.RecordLevelCompletion` is implemented by `SaveService` and called once (fire-and-forget) by Domain `ObjectiveEvaluator` on WIN, before `LevelResolved` is emitted.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: MEDIUM
**Engine Notes**: `OnApplicationPause(bool)` / `OnApplicationFocus(bool)` drive `FlushIfDirty` (arch §2 residue map). The debounce timer and lifecycle hooks are Game-layer MonoBehaviour concerns; the merge/serialize math is Domain (E02). No post-cutoff API on the critical path.

**Control Manifest Rules (this layer — Game):**
- Required: `RecordLevelCompletion` is implemented by `SaveService`; `ObjectiveEvaluator` (Domain) calls it once, fire-and-forget — Game never retries or inspects the result from the Domain side; `FlushIfDirty()` fires on pause/focus-lost/quit; settings writes debounced 750ms (`SETTINGS_SAVE_DEBOUNCE_MS`); win-persistence is WIN-only and ordered before `LevelResolved` (ADR-005 D4).
- Forbidden: never call `RecordLevelCompletion` on LOSE or abandonment (no mid-level board-state save at MVP); never write level results from `ScreenFlowController` (Screen Flow only routes).
- Guardrail: disk is read exactly once per session (at `LoadProfile`); every other op reads/writes the in-memory copy; a save runs only when `dirty == true`.

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md` §3, §11 + ADR-005 D4, scoped to this story:*

- [ ] `load_profile()` is called once at boot, runs the full load algorithm (delegating to the Domain `ResolveLoad`), and returns either a validated existing profile or a freshly-defaulted one; disk is read exactly once per session.
- [ ] `get_profile()` / `get_total_stars()` are read-only in-memory accessors that never touch disk.
- [ ] `record_level_completion(level_id, stars_earned, score_earned)` merges a win via the Domain monotonic merge (Formula 2), updates `total_stars`, marks dirty, and triggers an immediate save; it is the only entry point that creates/updates a `LevelRecord`; it is never called on a loss/abandonment.
- [ ] Win-persistence ordering: `RecordLevelCompletion` is observed strictly before `LevelResolved` is emitted, on WIN outcomes only (ADR-005 D4) — proven by an integration test.
- [ ] `update_setting(key, value)` updates one settings field, marks dirty, and triggers a save subject to the 750ms debounce (rapid toggles coalesce into one write).
- [ ] `flush_if_dirty()` fires on `OnApplicationPause(true)`/focus-lost/quit, performs the atomic write only if `dirty == true`, and returns whether a write occurred (closing the debounce gap before backgrounding).
- [ ] A level attempt that never calls `record_level_completion()` leaves that `level_id` entirely absent from `level_records` (no mid-level resume; no partial/zero-star artifact).

---

## Implementation Notes

*Derived from ADR-003 §11 + ADR-005 D4:*

- Compose these operations on top of the story-003 atomic write path — this story adds the public API, the dirty flag semantics, the debounce timer, and the lifecycle hooks; it does not re-implement the A/B IO.
- `RecordLevelCompletion` implements the Domain `ISaveWriter` interface (keeps `ObjectiveEvaluator` engine-free). Fire-and-forget from the Domain side: Game never retries or inspects the result. The Domain calls it at resolve time, strictly before emitting `LevelResolved` (D4), so a mid-replay app kill still records the win while the Results screen (E05 story 009) waits on `juice_input_lock`.
- Debounce settings writes with `SETTINGS_SAVE_DEBOUNCE_MS = 750` from the single Domain config location; the pause/focus flush checks the dirty flag unconditionally (covers a pending debounced write).
- No mid-level board-state save at MVP (§10) — only a win is a save-worthy gameplay event.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 003: the atomic A/B write → flush → read-back-verify IO path and the corruption ladder (called by this API).
- Story 005: the `BootLoader` invoking `LoadProfile` in sequence and surfacing `recovery_notice`.
- E04: computing `stars_earned`/`score_earned`/`outcome` and the `ObjectiveEvaluator` that *calls* `RecordLevelCompletion` (Domain-side) — this story implements the callee.
- E07: the Settings screen UI that calls `update_setting`.

---

## QA Test Cases

*Integration story — Play Mode; deterministic (mock clock for debounce; no real OS lifecycle).*

- **AC-1 (load once)**: `load_profile()` reads disk exactly once; subsequent `get_profile`/`get_total_stars` never touch disk.
- **AC-2 (`test_level_record_merge_monotonic`)**: `record_level_completion` with a worse result leaves `best_stars`/`best_score` unchanged while `completion_count` increments and `last_completed_utc` updates (Formula 2 numbers).
- **AC-3 (win-persist ordering)**: Integration — on a WIN `BoardStabilized`, `RecordLevelCompletion` is observed strictly before `LevelResolved`; on LOSE it is never called (ADR-005 D4 / ADR validation).
- **AC-4 (`test_settings_debounce_coalesces_writes`)**: Multiple `update_setting` calls within 750ms produce exactly one disk write (mock clock).
- **AC-5 (`test_background_flushes_pending_dirty_state`)**: A dirty, not-yet-debounced settings change is written when the background/pause trigger fires, before the debounce timer would.
- **AC-6 (`test_mid_level_abandon_not_saved`)**: A simulated attempt that never calls `record_level_completion` leaves the `level_id` absent from `level_records`.

*Verification: Play Mode API test + smoke check (`production/qa/smoke-*.md`).*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/save-persistence/save_service_api_lifecycle_test.cs` (Play Mode) — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 (atomic A/B write path); E02 (Domain `Merge`/`ResolveLoad`/codec + config constants), E05 (win-persistence ordering seam: Domain `ObjectiveEvaluator` calls `RecordLevelCompletion` before `LevelResolved` — the D4 contract this story's callee satisfies).
- Unlocks: Story 005 (BootLoader `LoadProfile`); E07 (Settings `update_setting`, World Map `get_total_stars`).
