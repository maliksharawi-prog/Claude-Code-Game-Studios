# Story 001: InputRouter Gesture → Intent State Machine

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Core (InputRouter)
> **Type**: Logic
> **Estimate**: M (~4h)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/touch-input.md`
**Requirement**: `TR-ti-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: N/A — `docs/architecture/architecture.md` §6 (InputRouter) is the architectural home; no dedicated ADR governs the gesture grammar. The composed input-gate this router will later read is owned by ADR-005 D5 (story 002).
**ADR Decision Summary**: Gesture recognition is a stateless-per-frame classifier holding exactly one bit of interpretation state (selected/armed cell); it emits a three-intent vocabulary and never touches board state.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: This story is pure gesture-interpretation logic operating on injected pointer samples (canvas-space positions) + an injected hit-test delegate + injected threshold config. No `UnityEngine` input APIs here — the HIGH-risk Input System surface is story 003. Keep this class headless-testable (Edit Mode).

**Control Manifest Rules (this layer — Game)**:
- Required: All gameplay values data-driven — `threshold_ratio`, `threshold_min_px`, `threshold_max_px`, `input_buffer_depth`, `MIN_TOUCH_TARGET_PX` come from config, never literals.
- Forbidden: Never the legacy `Input` class (`Input.mousePosition`, `Input.GetMouseButton`, etc.) — this class receives already-abstracted pointer samples.
- Guardrail: Classification + intent emission adds ≤ 0.1 ms of frame time; compare squared distance vs. squared threshold to avoid a per-frame `sqrt` (non-normative).

---

## Acceptance Criteria

*From GDD `design/gdd/touch-input.md`, scoped to this story:*

- [ ] Pointer-down + pointer-up on cell A with travel `< threshold_px` and no prior selection emits exactly `select_cell(A)`; state → AwaitingSecond(A).
- [ ] The same tap again on cell A (state AwaitingSecond(A)) emits exactly `cancel()`; state → Idle.
- [ ] `select_cell(A)` then a tap on adjacent cell B (Manhattan distance 1) emits exactly `swap_request(A, B)`; state → Idle.
- [ ] `select_cell(A)` then a tap on non-adjacent cell C (distance > 1, C ≠ A) emits exactly `select_cell(C)`; state → AwaitingSecond(C).
- [ ] Pointer-down on A then movement exceeding `threshold_px` with `|dx| > |dy|` targets the horizontal neighbor in `sign(dx)`; with `|dy| >= |dx|` (including exact tie `|dx| == |dy|`) targets the vertical neighbor in `sign(dy)` (Formula 2).
- [ ] A swipe whose resolved target falls outside `[0, rows) × [0, cols)` emits no `swap_request`; any pending selection is cleared via `cancel()`; state → Idle.
- [ ] A swipe starting from any cell C (state AwaitingSecond(A)) emits `cancel()` then `swap_request(C, D)` for an in-bounds neighbor D — a stale selection is always cleared the moment any swipe resolves.
- [ ] `swap_request` is never emitted for two cells with Manhattan distance ≠ 1, for any input sequence.
- [ ] Pointer lost mid-gesture: nothing emitted if no threshold crossed and no prior selection; `cancel()` emitted if a selection was pending; state always returns to Idle.
- [ ] Formulas 1, 2, and 3 each reproduce the GDD worked-example outputs for the GDD worked-example inputs (regression-pinned).
- [ ] `hit_size_px` is asserted ≥ `MIN_TOUCH_TARGET_PX` (44px) in debug builds; `cell_size_px` below the floor logs/asserts but this class never resizes the board (Formula 3).

---

## Implementation Notes

*Derived from architecture.md §6 + `touch-input.md` §§1–5:*

- Model exactly one internal state bit: Idle vs. AwaitingSecond(cell). This is gesture-interpretation state, NOT board state — never shared with or owned by Board Engine.
- Emit exactly three intent types: `select_cell(cell)`, `swap_request(cell_a, cell_b)`, `cancel()`. Cells are `(row, col)` grid coords only — never candy IDs/colors/special flags.
- Formula 1 (swipe threshold): `threshold_px = clamp(threshold_ratio * cell_size_px, threshold_min_px, threshold_max_px)`; `gesture_is_swipe` becomes true the first frame `distance >= threshold_px`. Resolve the instant the threshold is crossed — never wait for release.
- Formula 2 (dominant axis): `axis = horizontal if |dx| > |dy| else vertical` (tie → vertical); exactly one of `delta_row`/`delta_col` is non-zero; diagonal targets are structurally impossible.
- `swap_request` guarantees adjacency (Manhattan 1) and nothing else — no color/match/legality checks (that is Board Engine's job, downstream).
- Intents are fire-and-forget; do not read any return value. Do not expose a continuous drag-position stream (explicitly out of MVP contract).
- MVP default `input_buffer_depth = 0` (drop-while-busy). The `input_buffer_depth = 1` single-slot buffer variant is a tuning knob, off by default — implement the drop path as the default; the buffer path is a guarded, config-gated branch.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: the four-term `InputGate` (Formula 5) composition — this story consumes the resulting boolean via an injected accessor only.
- Story 003: the Unity Input System pointer surface (`EnhancedTouch`/`Pointer`), single-active-touch rejection, hover ring, real pointer→cell hit-testing against a live board rect, ≤1-frame gate read.
- The `input_buffer_depth = 1` playtest tuning variant beyond wiring the config gate.

---

## QA Test Cases

*Derived from `touch-input.md` Acceptance Criteria (unit-testable list). Logic story — automated Edit Mode specs; deterministic synthetic pointer-event sequences, no RNG/timers.*

- **AC-1 (tap → select)**: Given Idle + no selection, When pointer-down/up on A with travel `< threshold_px`, Then exactly one `select_cell(A)` is emitted and state is AwaitingSecond(A). Edge cases: travel exactly at `threshold_px − ε`; slow hold with no movement.
- **AC-2 (deselect)**: Given AwaitingSecond(A), When A is tapped again, Then exactly one `cancel()` and state Idle.
- **AC-3 (tap-tap swap)**: Given AwaitingSecond(A), When adjacent B tapped, Then exactly one `swap_request(A,B)` and state Idle. Edge cases: all four orthogonal neighbors.
- **AC-4 (reselect)**: Given AwaitingSecond(A), When non-adjacent C tapped, Then `select_cell(C)`, state AwaitingSecond(C). Edge cases: distance-2 same row; diagonal (distance 2 Manhattan).
- **AC-5 (dominant axis)**: Given pointer-down on A, When drag crosses threshold with (dx,dy), Then target = horizontal neighbor if `|dx|>|dy|` else vertical. Edge cases: `|dx| == |dy|` → vertical; sign of dx/dy at all four directions.
- **AC-6 (out-of-bounds swipe)**: Given origin on an edge cell, When swipe resolves off-board, Then no `swap_request`, pending selection cleared via `cancel()`, state Idle.
- **AC-7 (adjacency invariant)**: For a fuzzed set of tap-tap and swipe sequences, Then no emitted `swap_request` ever has Manhattan distance ≠ 1.
- **AC-8 (lost gesture)**: Given a mid-gesture cancel signal, Then `cancel()` iff a selection was pending, else nothing; state always Idle.
- **AC-9 (formula regression)**: Formula 1 worked example (`cell_size_px=130, ratio=0.35` → `threshold_px≈45.5`); Formula 2 worked example (origin (3,4), delta → target (4,4)); Formula 3 worked example (`hit_size_px=130`, `hit_expansion_px=20.8`, `130 ≥ 44`) — each reproduced within documented epsilon.
- **AC-10 (44px floor assert)**: Given `cell_size_px = 40`, Then Formula 3 constraint fails and a debug assert/log fires; the class does not mutate layout.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/input/gesture_intent_state_machine_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E01 (assemblies + CI). No intra-epic dependency (pure logic; consumes an injected gate accessor + hit-test delegate + config).
- Unlocks: Story 003 (pointer surface feeds this state machine).
