# Story 002: InputGate — Four-Term Input-Lock Composition (Formula 5)

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Core (InputGate)
> **Type**: Logic
> **Estimate**: S (~2–3h)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/screen-flow.md` (Formula 5) · `design/gdd/juice-layer.md` (§10, term 4)
**Requirement**: `TR-sf-002` (primary — the four-term composition)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D5)
**ADR Decision Summary**: `EffectiveBoardInputEnabled` is the AND of four independently-owned boolean terms, composed at a single point — `InputGate` (Game). No owner reads another's internals; each contributes exactly one veto term.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure boolean composition — no post-cutoff APIs. A plain Game-layer class caching four cached terms; the owning systems (ScreenFlowController, GameController/BoardModel, JuiceDirector) push their own term.

**Control Manifest Rules (this layer — Game)**:
- Required: `InputGate` is the single composition point for the four Formula-5 terms: `_baseStateIsGameplay && _boardInputEnabled && !_overlayIsActive && !_juiceInputLock`. No other code recombines these; each owner writes only its own term; consumers read `EffectiveBoardInputEnabled`.
- Forbidden: Never let any consumer re-derive the composition from the raw terms; never wire a live C# event across the Domain→Game boundary for board events (term 2 mirrors `BoardInputEnabledChanged` via `GameController`, not a live cross-assembly delegate).
- Guardrail: Read at ≤1-frame latency by `InputRouter` (story 003).

---

## Acceptance Criteria

*From GDD `design/gdd/screen-flow.md` Formula 5 + `juice-layer.md` Formula 5, scoped to this story:*

- [ ] `EffectiveBoardInputEnabled == false` whenever `juice_input_lock == true`, regardless of the other three terms.
- [ ] `EffectiveBoardInputEnabled == true` only when all four hold simultaneously: `base_state == GAMEPLAY`, `board_input_enabled == true`, `overlay_is_active == false`, `juice_input_lock == false`.
- [ ] Worked example 1 (overlay veto): mid-cascade + Pause tap → `true AND false AND NOT true AND NOT true = false`.
- [ ] Worked example 2 (juice-replay veto, no overlay): Board Engine idle but Reveal Queue on step 3 of 4 → `true AND true AND true AND NOT true = false`; flips true only when `SETTLE_REVEAL` completes and `juice_input_lock` clears.
- [ ] Each term has exactly one writer/owner; `InputGate` exposes only per-term setters (one per owner) plus the read-only `EffectiveBoardInputEnabled`. No API recombines raw terms.

---

## Implementation Notes

*Derived from ADR-005 D5:*

- Terms and owners: (1) `base_state == GAMEPLAY` — `ScreenFlowController`, written on every T1–T21 transition; (2) `board_input_enabled` — Board Engine raw busy term, mirrored by `GameController` bracketing the synchronous `ResolveSwap` false→true; (3) `!overlay_is_active` — `ScreenFlowController`, written on overlay push/pop; (4) `!juice_input_lock` — `JuiceDirector`, fired via `juice_input_lock_changed` and cached by `InputGate`.
- In practice term 2 is false only during the microscopic synchronous resolve window (within one frame); term 4 (`juice_input_lock`) is what actually holds input across the multi-frame cascade replay. Term 2 exists to reject re-entrant submission during resolve.
- This ADR fixes **where** the composition lives (`InputGate`, Game) and **who owns each term**; do not move the AND anywhere else.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 003: `InputRouter` reading `EffectiveBoardInputEnabled` at ≤1-frame latency (drop-while-busy).
- Story 006: `JuiceDirector` setting/clearing the `juice_input_lock` term (this story only accepts and caches it).
- Story 008: `ScreenFlowController` transition logic that writes the `base_state`/`overlay_is_active` terms (this story only exposes the setters).

---

## QA Test Cases

*Logic story — automated Edit Mode specs; deterministic, no timers.*

- **AC-1 (juice veto dominates)**: Given any combination with `juice_input_lock = true`, When `EffectiveBoardInputEnabled` is read, Then it is `false`. Edge cases: all 8 combinations of the other three terms with lock=true.
- **AC-2 (all-true)**: Given the exact quad (GAMEPLAY, boardInput=true, overlay=false, juiceLock=false), Then `true`; flipping any single term to its veto value yields `false` (4 single-flip cases).
- **AC-3 (worked example 1)**: Reproduce `(GAMEPLAY, false, overlay=true, lock=true)` → `false`.
- **AC-4 (worked example 2)**: Reproduce `(GAMEPLAY, true, overlay=false, lock=true)` → `false`, then lock=false → `true`.
- **AC-5 (single-writer)**: Assert the public surface exposes exactly one setter per term and a single read-only accessor (compile-time/reflection check acceptable).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input/input_gate_composition_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E01 (assemblies).
- Unlocks: Story 003 (router reads the gate), Story 006 (JuiceDirector writes term 4), Story 008 (ScreenFlowController writes terms 1 & 3).
