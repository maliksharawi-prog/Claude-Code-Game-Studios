# Story 005: Reveal Queue & Replay Scheduler Ordering

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Presentation (JuiceDirector replay core)
> **Type**: Logic
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/juice-layer.md` (§3, §5)
**Requirement**: `TR-jl-001` (capture-then-replay: Shadow Board Model + Reveal Queue, single active queue — Reveal Queue / scheduler side)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D1, D3 Mode B)
**ADR Decision Summary**: Because input stays locked for a move's full visual replay, the Domain can never resolve a second move before the first is drained — only one Reveal Queue is ever active; no merge/interleave path is built at MVP.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: The reveal scheduler is plain MonoBehaviour timing; the *ordering derivation* itself is pure logic and must be headless-testable (Edit Mode) against a mocked signal burst. Reveal *pacing* (deceleration curve, tween durations) is wall-clock-based and deliberately NOT part of the deterministic sequence.

**Control Manifest Rules (this layer — Game)**:
- Required: derive the Reveal Queue directly from the captured signal burst; trust Board Engine's two documented ordering guarantees unconditionally — never re-derive or second-guess signal order.
- Forbidden: never query live `BoardModel` during replay; never merge/interleave two in-flight queues (single-active-queue makes it structurally impossible at MVP).
- Guardrail: 60fps/16.6ms; per-frame drain work stays bounded and paced by the reveal curve.

---

## Acceptance Criteria

*From GDD `design/gdd/juice-layer.md` §3 + §5, scoped to this story:*

- [ ] Each triggering event produces exactly one Reveal Queue of ordered Reveal Steps, drained one at a time; only one Queue is ever active.
- [ ] Given a mocked special-activation burst reproducing `board-engine.md`'s ordering guarantee, the derived Queue produces `SWAP_REVEAL → ACTIVATION_REVEAL → CLEAR_REVEAL(1) → FALL_REVEAL(1)/REFILL_REVEAL(1) → … → SETTLE_REVEAL` in exactly that order.
- [ ] Given a mocked bootstrap burst (with accidental cascade + reshuffle), the derived Queue produces `BOOTSTRAP_OPEN_REVEAL → CLEAR_REVEAL(1) → … → RESHUFFLE_REVEAL → SETTLE_REVEAL` with **no** `MILESTONE_REVEAL` steps anywhere.
- [ ] `MILESTONE_REVEAL` is produced only for `chain_index ≥ 2` **and** `trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}`; never for `trigger_source = BOOTSTRAP` (any depth); never for `chain_index = 1`.
- [ ] On a suspend signal (mirroring `screen-flow.md` T8/T9) mid-Queue, every remaining step applies its end-state immediately (no remaining beats/tweens execute) — the player is never shown a half-settled board.

---

## Implementation Notes

*Derived from ADR-005 D3 Mode B and `juice-layer.md` §3, §5:*

- The Reveal Step table (`juice-layer.md` §3) is a one-to-one translation of Board Engine's two published ordering guarantees; derive steps from the captured burst, not from live state.
- `SETTLE_REVEAL` is always last; its completion is the point at which `juice_input_lock` clears (owned by story 006).
- Milestone suppression is keyed off `trigger_source` — a level opening must never shout an escalation the player did nothing to earn.
- Suspension policy is fast-forward-to-completion (skip remaining `inter_step_beat_ms` and in-flight tweens, apply every remaining end-state), mirroring `screen-flow.md`'s interrupted-transition policy.
- Fold `EventBridge` drain wiring from story 004 into the scheduler entry point (drain the single un-drained move).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 004: Shadow Board Model construction + `EventBridge` single-active-queue assertion.
- Story 006: gravity re-derivation (fall paths), `juice_input_lock` signal ownership, and the `BoardReshuffled` live-query exception.
- Story 007: the pooled grey-box `BoardPresenter` that visually renders each Reveal Step.
- Exact reveal *pacing* constants beyond wiring them as data-driven config (Formulas 1–4 timing values are E09 Juice tuning — validated on device there).

---

## QA Test Cases

*Logic story — automated Edit Mode specs; deterministic, mocked event stream, no real timers.*

- **AC-1 (`test_reveal_queue_matches_board_engine_special_activation_ordering`)**: Given the mocked special-activation guarantee, Then exactly `SWAP_REVEAL → ACTIVATION_REVEAL → CLEAR_REVEAL(1) → FALL/REFILL(1) → … → SETTLE_REVEAL`.
- **AC-2 (`test_reveal_queue_matches_board_engine_bootstrap_ordering`)**: Given the mocked bootstrap guarantee (accidental cascade + reshuffle), Then `BOOTSTRAP_OPEN_REVEAL → CLEAR_REVEAL(1) → … → RESHUFFLE_REVEAL → SETTLE_REVEAL` with zero `MILESTONE_REVEAL`.
- **AC-3 (`test_milestone_reveal_suppressed_for_bootstrap_trigger_source`)**: For any `trigger_source = BOOTSTRAP` step at any `chain_index`, no `MILESTONE_REVEAL`.
- **AC-4 (`test_milestone_reveal_present_from_chain_index_2_for_player_moves`)**: For `trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}`, `MILESTONE_REVEAL` for every `chain_index ≥ 2` and none at `chain_index = 1`.
- **AC-5 (`test_suspend_fast_forwards_active_queue`)**: A suspend signal mid-Queue applies every remaining step's end-state immediately with no beats/tweens executing.
- **AC-6 (single active queue)**: Attempting to start a second Queue while one drains is rejected/asserted.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/juice-layer/reveal_queue_scheduler_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 004 (Shadow Board Model + EventBridge drain); E02 (BoardEvent catalog + ordering guarantees).
- Unlocks: Story 006 (lock/gravity/reshuffle attach to the Queue), Story 007 (presenter renders each step).
