# Story 003: Unified Pointer Surface + Gate-Gated Routing

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Core (InputRouter)
> **Type**: Integration
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/touch-input.md` (§§5–6) · `design/ux/interaction-patterns.md` (Swipe / Tap-Tap / Hover Ring — Draft, pending `/ux-review`)
**Requirement**: `TR-ti-002` (unified touch+mouse pointer; hover additive-only; single-active-touch) · `TR-ti-003` (reads composed `effective_board_input_enabled`; ≤1-frame latency; ≥44px hit target)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D5, for the gate read); architecture.md §6 for the Input System surface (no dedicated ADR).
**ADR Decision Summary**: `InputRouter` (Game) reads `EffectiveBoardInputEnabled` at the instant it would accept a gesture (≤1-frame latency); the gate is composed elsewhere (`InputGate`).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: HIGH
**Engine Notes**: This is the HIGH-risk Input System surface. Legacy `Input` class is deprecated/forbidden — use the unified pointer and `EnhancedTouch`. `EnhancedTouchSupport.Enable()` is REQUIRED before `Touch.activeTouches` (verified `docs/engine-reference/unity/modules/input.md`). Cross-reference the engine-reference directory before citing any Input System API — LLM training reliably covers only ~6.0/6.1.

**Control Manifest Rules (this layer — Game)**:
- Required: Input System package only (`EnhancedTouch`/`Pointer`/`Mouse`) for all gesture and pointer handling; touch input reads `EffectiveBoardInputEnabled` at ≤1-frame latency; all interactive elements / hit regions ≥ 44px.
- Forbidden: Never the legacy `Input` class (`Input.GetKey/GetMouseButton/GetAxis/mousePosition`); no hover-only interactions (hover is additive, desktop/web-only, never required, never emits an intent).
- Guardrail: 60fps/16.6ms; gesture classification ≤ 0.1 ms/frame.

---

## Acceptance Criteria

*From GDD `design/gdd/touch-input.md`, scoped to this story:*

- [ ] A single unified pointer stream (position + phase began/moved/ended/cancelled) drives the story-001 gesture state machine identically whether the event originated from touch or mouse; the Web build behaves identically to mobile (left mouse down = pointer-down, drag = pointer-move, release = pointer-up).
- [ ] While a gesture is in progress, any additional simultaneous touch point is ignored outright — only the first/primary contact drives the active gesture until it resolves or is lost (single-active-touch).
- [ ] Mouse hover (no button held) drives at most an additive visual affordance (hover ring); it never emits an intent, never changes state-machine state, and is never required to play; fully absent on touch with zero information loss.
- [ ] A gesture may only begin inside a valid cell's hit region; touches starting on HUD chrome/background/margin never enter the board state machine.
- [ ] Hit-testing uses the full `cell_size_px` pitch (Formula 3), never the 84%-scaled candy sprite — zero dead zones between adjacent candies; effective hit target ≥ 44px across at least 3 tested cell pairs including gutters.
- [ ] `InputRouter` reads `EffectiveBoardInputEnabled` at pointer-down within ≤1 frame; while it is `false` (MVP default `input_buffer_depth = 0`), every gesture that starts in that window is dropped entirely — no intent, no visual feedback, no queue.
- [ ] Rapidly tapping/swiping during an active cascade produces zero stuck highlights, glitches, or double-fired swaps (validates drop-while-busy end to end).

---

## Implementation Notes

*Derived from architecture.md §6, ADR-005 D5, and `touch-input.md` §§5–6:*

- Enable `EnhancedTouchSupport.Enable()` at input-surface init before reading `Touch.activeTouches`. Use the Input System pointer/touch action with phase tracking (`Started`/`Performed`/`Canceled`).
- Feed a single unified gesture state-machine instance (story 001) from both touch and mouse — do NOT implement Tap-Tap as a separate handler layered on Swipe; `touch-input.md` §2's table is the single source of truth for interleaving.
- Track `distance` in the same canvas-space coordinate system used for hit-testing (UI Toolkit panel coordinates, or board world-space→canvas mapping — coordinate-system choice is a `unity-specialist`/`unity-ui-specialist` implementation decision, not decided in the GDD).
- Gesture begin is gated at pointer-down on `EffectiveBoardInputEnabled`; a new gesture cannot begin while busy, and `board_input_enabled` only flips false as a consequence of a swap this system just triggered (no in-flight gesture is caught by a busy transition it did not cause).
- Interaction-patterns.md is Draft (pending `/ux-review`) — treat its press-state/hover detail as advisory; the binding contract is `touch-input.md`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: the gesture→intent classification logic (this story only wires the real pointer source + hit-test into it).
- Story 002: the four-term `InputGate` composition (this story only reads `EffectiveBoardInputEnabled`).
- Board layout / `cell_size_px` computation and the actual board rect — supplied by Board Engine rendering (E03/E08); this story hit-tests against the rect it is handed.
- The `input_buffer_depth = 1` expert-flow buffer (config-gated, off by default).

---

## QA Test Cases

*Integration story — Play Mode interaction test + manual device walkthrough (`touch-input.md` manual list).*

- **AC-1 (unified pointer, mouse↔touch parity)**: Play Mode — inject synthetic mouse click-drag-release and a touch swipe of equivalent travel; assert identical `swap_request` output. Manual: on the Web build, mouse click-drag-release reproduces a touch swipe of equal travel; hover with no click never changes state.
- **AC-2 (single active touch)**: Play Mode — begin a gesture, inject a second simultaneous touch mid-gesture; assert the second is ignored and the first resolves normally. Manual: an off-hand brush during one-handed play does not corrupt a gesture.
- **AC-3 (hover additive)**: Manual (desktop/web) — hovering a cell shows the ring; no intent fires; the game is fully playable with hover absent.
- **AC-4 (hit region confinement)**: Play Mode — pointer-down on HUD/margin coordinates never enters the state machine. Manual: tap near cell boundaries across ≥3 cell pairs — no dead zones, full-pitch hit.
- **AC-5 (≤1-frame gate read + drop-while-busy)**: Play Mode — with `EffectiveBoardInputEnabled = false`, inject a full gesture; assert zero intents and the gate was sampled within one frame of pointer-down. Manual: rapid tap/swipe during an active cascade → zero glitches/stuck highlights/double swaps.
- **AC-6 (44px floor)**: Verify computed hit region ≥ 44px on the reference 8×8 board and the 320px-viewport worst case (`design/ux/hud.md` §7 — Draft context).

*Verification: capture a Play Mode interaction recording + screenshots for the manual walkthrough in `production/qa/evidence/`.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/input/pointer_surface_gate_routing_test.cs` (Play Mode) — must exist and pass (BLOCKING)
- Manual walkthrough doc: `production/qa/evidence/unified-pointer-surface-evidence.md` (ADVISORY — device parity + drop-while-busy)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (gesture state machine), Story 002 (InputGate); E01 (Input System backend + CI), E03 (BoardModel + board rect / `cell_size_px` to hit-test against).
- Unlocks: Story 007 (end-to-end swap→replay uses this surface).
