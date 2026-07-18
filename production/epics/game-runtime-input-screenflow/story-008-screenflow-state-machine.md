# Story 008: ScreenFlowController — 2-Layer State Machine (T1–T21)

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Presentation (ScreenFlowController)
> **Type**: Logic
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/screen-flow.md` (§§1–4, §7)
**Requirement**: `TR-sf-001` (2-layer base+overlay state machine, 9 legal composites, T1–T21) · `TR-sf-002` (owns `overlay_is_active`, `attempt_number` — ownership half; the Formula-5 composition itself is story 002)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: architecture.md §6 (ScreenFlowController) is the architectural home; ADR-005 (D5) governs the terms this controller writes into `InputGate`.
**ADR Decision Summary**: The state machine has two independent layers (base + overlay) composed into one composite state; `ScreenFlowController` owns and writes the `base_state == GAMEPLAY` and `overlay_is_active` veto terms and the per-level in-memory `attempt_number`.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: The transition table + composite-state validity are pure logic — headless-testable (Edit Mode). Background/foreground and interrupted transitions are simulated by mocking the relevant signal. No screen *layout* here (that is `design/ux/` + E07 UI); this story owns which screen is active and the legal transitions only.

**Control Manifest Rules (this layer — Game/UI):**
- Required: `ScreenFlowController` writes the `base_state` and `overlay_is_active` terms into `InputGate`; UI reads presentation DTOs and never writes level results directly; `attempt_number` reset to 1 on fresh entry (T4/T18), incremented by 1 on Restart/Retry (T11/T17).
- Forbidden: reach any composite state outside the nine legal ones; `ScreenFlowController` never overwrites Board Engine's internal `board_input_enabled`.
- Guardrail: base-state transitions ≤ `MAX_SCREEN_TRANSITION_MS` (300ms); overlay open/close ≤ `MAX_MODAL_TRANSITION_MS` (200ms).

---

## Acceptance Criteria

*From GDD `design/gdd/screen-flow.md` §§1–4, §7, scoped to this story:*

- [ ] Every transition T1–T21 (§2), given its documented `From` state and `Trigger`, produces exactly its documented `To` state.
- [ ] Every reachable state after any sequence of legal transitions is a member of the nine Legal Composite States (§1) — `(B4,[O1])`, `(B2,[O2])`, and any other combination outside the nine are never reachable.
- [ ] `SETTINGS` is reachable from `WORLD_MAP` (T6) and from `PAUSE` (T13) but never directly from `PRE_LEVEL_CARD`.
- [ ] The Back Button Policy table (§3) is reproduced: for each of the 9 legal composite states, an OS-back event produces exactly the documented result; World Map root back is not intercepted; Results back == Map.
- [ ] App-background while `(B3,[])` produces `(B3,[O2])` (T9) and sets `effective_board_input_enabled = false` (via the `overlay_is_active` term); app-background in any other state (T20) produces no state change and records `flush_if_dirty()` called exactly once.
- [ ] Quit to Map (T12) goes directly `(B3,[O2]) → (B2,[])` without passing through Results; two rapid Retry triggers within one transition window produce exactly one transition.
- [ ] `attempt_number` resets to 1 on T4/T18 and increments by exactly 1 on T11/T17; the value is passed to Board Engine at each bootstrap.
- [ ] Formulas 1, 2, 4, 6 reproduce their worked examples (nav taps 3 & 5; frame budgets 18 & 12; unlock gate both cases; new-best both cases).

---

## Implementation Notes

*Derived from architecture.md §6 and `screen-flow.md` §§1–4, §7:*

- Base Layer (exactly one active): `BOOT_LOADING`, `WORLD_MAP`, `GAMEPLAY`, `RESULTS_WIN`, `RESULTS_LOSE`. Overlay Layer (max stack depth 2): `PRE_LEVEL_CARD`, `PAUSE`, `SETTINGS`. Nine legal composites total.
- On every base-state transition write the `base_state == GAMEPLAY` term into `InputGate`; on every overlay push/pop write `overlay_is_active`.
- `attempt_number` is a single in-memory counter per level (reset on fresh entry from map / Next Level; +1 on Restart/Retry) — forwarded unmodified to Board Engine bootstrap, which passes it to `RngService.StartLevelSession`. Matches `rng-service.md`'s documented reset/increment policy.
- App-background auto-opens Pause (T9) rather than silently flagging input disabled — the suspension must be visible (Pillar 2). This also satisfies Touch & Input's "modal opens mid-gesture → cancel" edge case.
- This story does NOT perform the T15/T16 Results transition timing (the wait-on-`juice_input_lock` + ceiling) — that is story 009.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 002: the four-term `InputGate` composition (this story only writes its two terms).
- Story 009: T15/T16 Results-transition timing (wait on `juice_input_lock` + safety ceiling) and the retry-skips-Pre-Level-Card path.
- E07 ui-toolkit-screens-hud: actual screen layouts, UXML/USS, HUD chips, World Map view, Results screen composition, Fizz mount rendering.

---

## QA Test Cases

*Logic story — automated Edit Mode specs; deterministic, no real timers; background/foreground and interrupted transitions simulated by mocking signals.*

- **AC-1 (transition table)**: For each T1–T21, Given `From` + `Trigger`, Then exactly the documented `To`.
- **AC-2 (composite validity)**: Fuzz legal-transition sequences; assert every reachable state ∈ the nine composites; specific illegal states (`(B4,[O1])`, `(B2,[O2])`) are never produced.
- **AC-3 (settings reachability)**: Reachable from T6 and T13; no table entry connects `PRE_LEVEL_CARD → SETTINGS`.
- **AC-4 (back policy)**: For each of the 9 states, an OS-back event yields the §3 result; World Map root back not intercepted; Results back == Map.
- **AC-5 (background)**: `(B3,[]) + background → (B3,[O2])` with `effective_board_input_enabled = false`; other states + background → no change, `flush_if_dirty()` called exactly once.
- **AC-6 (quit / double-tap)**: T12 → `(B2,[])` without Results; two rapid Retry triggers → exactly one transition.
- **AC-7 (attempt_number)**: Reset to 1 on T4/T18; +1 on T11/T17; value forwarded to bootstrap.
- **AC-8 (formula regression)**: Formula 1 (`n_retries=0→3`, `=2→5`); Formula 2 (`300→18`, `200→12`); Formula 4 (both unlock cases); Formula 6 (both new-best cases).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/screen-flow/screen_flow_state_machine_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (InputGate terms sink); E01 (assemblies), E04 (`level_resolved` / `ResultsData` types referenced by T15/T16 signatures — timing wired in story 009).
- Unlocks: Story 009 (Results-transition timing extends this state machine).
