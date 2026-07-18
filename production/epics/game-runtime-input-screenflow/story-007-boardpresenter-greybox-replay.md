# Story 007: BoardPresenter Grey-Box Replay (Swap → Paced Motion → Input Unlock)

> **Epic**: Game Runtime — Input, Reveal Replay & Screen Flow (E05)
> **Status**: Ready
> **Layer**: Presentation (BoardPresenter)
> **Type**: Integration
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/juice-layer.md` (§§1–4 replay realization) · `design/gdd/touch-input.md` (§4 input re-enable timing)
**Requirement**: `TR-jl-001` + `TR-jl-002` (integration realization) · `TR-ti-003` (input re-enables exactly on `SETTLE_REVEAL` complete)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-005: Event Bridge & Deferred-Replay Contract (D1/D3 Mode B, D5)
**ADR Decision Summary**: `GameController` hands the immutable per-move snapshot to `EventBridge.EnqueueMove`; presentation drains it (`JuiceDirector` builds Shadow Board + Reveal Queue) and the `BoardPresenter` animates pooled `FruitPiece` transforms; input re-enables when `SETTLE_REVEAL` clears `juice_input_lock`.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW–MEDIUM
**Engine Notes**: MonoBehaviour timing + object pooling for `FruitPiece` transforms. **Grey-box only** — glass-candy materials, bloom, and VFX are E08/E09 and are explicitly out of scope. No realtime shadows; one directional key light (manifest). Pooled bursts / particle backend are E09 (ADR-H pending).

**Control Manifest Rules (this layer — Game/Presentation)**:
- Required: presentation reads the drained snapshot only; SRP Batcher + GPU Instancing on shared meshes when materials arrive (E08) — at grey-box, use pooled placeholder transforms.
- Forbidden: never mutate Domain state or query live `BoardModel` during replay (reshuffle exception aside); never a physics dependency (`Rigidbody`/`Physics.*`) on the gameplay path — piece motion is tween-driven.
- Guardrail: 60fps/16.6ms; input re-enables exactly at `SETTLE_REVEAL` completion, never before.

---

## Acceptance Criteria

*From GDD `design/gdd/juice-layer.md` + epic Definition of Done, scoped to this story:*

- [ ] A touch/mouse swap produces a `SwapRequest`, drives `BoardModel.TrySwap`, and the resulting event stream replays as paced grey-box piece motion (optimistic slide → clears → falls → refills → settle).
- [ ] Surviving pieces (from the Shadow Board Model gravity re-derivation) drop into their re-derived cells; newly-refilled pieces enter from their segment's top, staggered per column.
- [ ] Input is vetoed for the full multi-frame replay and re-enables exactly on `SETTLE_REVEAL` completion (`juice_input_lock` clears) — a second swap cannot fire mid-replay.
- [ ] The optimistic slide begins immediately on `swap_started`; a rejected swap completes the slide, plays a soft revert, and slides back — zero move consumed, no punishing cue.
- [ ] Pooled `FruitPiece` transforms are reused across moves (no per-move instantiate/destroy churn); a worst-case cascade replays without stutter at grey-box fidelity.

---

## Implementation Notes

*Derived from ADR-005 D1/D3/D5 and `juice-layer.md` §§1–4:*

- Wire `GameController` → `EventBridge.EnqueueMove(snapshot)` after `ResolveSwap` returns; the `BoardPresenter` drains via `JuiceDirector`'s Reveal Queue (story 005) and animates each Reveal Step.
- Motion is tween-driven against the Shadow Board Model's re-derived positions (story 006) — never a live Board Engine query. Fall/refill staggering follows the `FALL_REVEAL`/`REFILL_REVEAL` steps.
- Grey-box: placeholder cube/quad meshes and flat colors are acceptable; the glass-candy material set, emissive bloom, and cascade FX arrive in E08/E09 — do not author them here.
- The end-to-end input-unlock timing is the load-bearing integration assertion: `EffectiveBoardInputEnabled` must flip true only after `SETTLE_REVEAL`.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 004/005/006: the Shadow Board Model, Reveal Queue ordering, gravity re-derivation, and `juice_input_lock` logic (consumed here, not re-implemented).
- E08 rendering-materials: glass-candy materials, bloom, per-instance hue, SRP-batched fruit set.
- E09 juice-vfx-audio-haptics: particle bursts, audio hook map, haptics, milestone callout visuals, star ceremony.

---

## QA Test Cases

*Integration story — Play Mode integration test + advisory feel walkthrough.*

- **AC-1 (end-to-end replay)**: Play Mode — inject a `SwapRequest`, drive `BoardModel.TrySwap`, assert the event stream replays as ordered grey-box motion (slide → clear → fall → refill → settle) and the final on-screen grid matches the Shadow Board Model.
- **AC-2 (input re-enable timing)**: Play Mode — assert `EffectiveBoardInputEnabled` is `false` for the full replay and flips `true` on the same frame `SETTLE_REVEAL` completes; a second injected swap mid-replay is dropped.
- **AC-3 (optimistic slide + revert)**: Play Mode — a swap that Board Engine rejects plays the slide→revert→slide-back with zero move consumed; an accepted swap slides cleanly into the first clear.
- **AC-4 (pooling)**: Play Mode — run a multi-move session and assert `FruitPiece` instances are drawn from a pool (no growth in instantiate count across moves).
- **AC-5 (feel, advisory)**: Manual — a 4-link cascade reads as paced grey-box motion, not a jump-cut; no half-settled board on suspend/resume.

*Verification: Play Mode recording + grey-box screenshots/GIF in `production/qa/evidence/`.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/juice-layer/board_presenter_replay_test.cs` (Play Mode) — must exist and pass (BLOCKING)
- Advisory: `production/qa/evidence/board-presenter-greybox-evidence.md` (feel walkthrough + capture)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 003 (input surface produces the swap), Story 005 (Reveal Queue), Story 006 (gravity + `juice_input_lock`), Story 002 (InputGate); E02 (EventBridge snapshot types), E03 (`BoardModel.TrySwap` + event stream).
- Unlocks: Story 009 (Results transition waits on the replay this story drives); E08/E09 build their material/VFX passes on this grey-box presenter.
