# Epic: Game Runtime — Input, Reveal Replay & Screen Flow

> **Epic ID**: E05
> **Layer**: Core (InputRouter) + Presentation (Reveal replay, ScreenFlowController)
> **GDD**: `design/gdd/touch-input.md` · `design/gdd/juice-layer.md` (replay mechanism) · `design/gdd/screen-flow.md`
> **Architecture Module**: `SweetCascade.Game` — InputRouter (gesture→intent), EventBridge (per-move buffer), JuiceDirector replay core (Shadow Board Model + Reveal Queue + `juice_input_lock`), BoardPresenter (pooled grey-box piece animation), ScreenFlowController (2-layer state machine + T1–T21 + InputGate Formula 5)
> **Status**: Ready
> **Stories**: 9 stories created (2026-07-18) — see [## Stories](#stories)

## Scope

The Game-assembly runtime that makes a swap produce paced on-screen motion and makes the app
navigable — the "grey-box playable" milestone, before any candy art or sensory juice. Delivers:
the **InputRouter** (unified touch+mouse pointer via Input System `EnhancedTouch`, gesture→intent
with swipe threshold + dominant-axis, single-active-touch, ≥44px targets, reads the composed
gate at ≤1-frame latency); the **EventBridge** per-move snapshot buffer; the **JuiceDirector
replay core** — Shadow Board Model, single-active Reveal Queue, gravity re-derived from events,
and ownership of `juice_input_lock` (the reveal is the frame-budget pacing strategy: Domain
resolves instantly, presentation paces reveals); the **BoardPresenter** animating pooled
FruitPiece transforms (grey-box; glass-candy materials arrive in E08); and the
**ScreenFlowController** — the base+overlay state machine (9 composites, T1–T21), the four-term
InputGate (Formula 5), `overlay_is_active` / `attempt_number` ownership, and the Results
transition that waits on `juice_input_lock` with a safety ceiling (retry skips the Pre-Level Card).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-005: Event Bridge & Deferred-Replay Contract | EventBridge/Reveal Queue location (Game), Shadow Board Model, D5 four-term input-lock composition ownership (Formula 5) | LOW (contract) |
| arch §6 | InputRouter (gesture state machine, Input System `EnhancedTouch`); ScreenFlowController ownership | HIGH (Input System) |
| arch §7.1 / §7.3 | Frame update path (synchronous resolve → paced reveal); Results-waits-on-juice-lock | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ti-001 | Gesture → intent (`select_cell` / `swap_request` / `cancel`); swipe threshold + dominant-axis | arch §6 (InputRouter) ✅ |
| TR-ti-002 | Unified touch+mouse pointer; hover additive-only; single-active-touch | arch §6 ✅ |
| TR-ti-003 | Reads composed `effective_board_input_enabled` gate; ≤1 frame latency; ≥44px hit target | ADR-005 (D5) + arch §8.5 ✅ |
| TR-jl-001 | Capture-then-replay: Shadow Board Model + Reveal Queue, single active queue | ADR-005 (D1/D3 Mode B) ✅ |
| TR-jl-002 | Owns `juice_input_lock_changed`; gravity re-derivation from events; one live-query reshuffle exception | ADR-005 (D5 term 4) ✅ |
| TR-sf-001 | 2-layer (base+overlay) state machine, 9 legal composites, T1–T21 transitions | arch §6 (ScreenFlowController) ✅ |
| TR-sf-002 | Formula 5 four-term input-lock composition; owns `overlay_is_active`, `attempt_number` | ADR-005 (D5) ✅ |
| TR-sf-003 | Results transition waits on `juice_input_lock` with safety ceiling; retry skips Pre-Level Card | ADR-005 (D3) + arch §7.3 ✅ |

## Depends On

- **E01** (assemblies + Input System backend + CI).
- **E02** (BoardEvent record types; RNG session for a bootstrapped board).
- **E03** (BoardModel + event stream to replay; `board_input_enabled` raw term).
- **E04** (`level_resolved` / `ResultsData` drives the Results transition, sf-003).

> DIP note: E05 consumes `ISaveService` / `IContentLoader` as **consumer-defined interfaces**;
> E06 implements them. E05 does not depend on E06's concrete IO (keeps the epic graph acyclic —
> E06 boots into E05, not the reverse).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **HIGH — Input System.** Legacy `Input` class is deprecated/forbidden; use the unified
  pointer and `EnhancedTouch`. `EnhancedTouchSupport.Enable()` is required before
  `Touch.activeTouches` (verified `modules/input.md`).
- The reveal scheduler / Shadow Board Model / BoardPresenter are plain MonoBehaviour timing
  (LOW risk); the input surface is the HIGH-risk part of this epic.
- **Contract integrity:** the InputGate composes four independently-owned booleans
  (base_state · `board_input_enabled` · `¬overlay_is_active` · `¬juice_input_lock`) — no owner
  reads another's internals; each contributes one veto term (arch §7.2).

## Stories

| # | Story | Type | Status | ADR | TRs |
|---|-------|------|--------|-----|-----|
| 001 | InputRouter gesture → intent state machine | Logic | Ready | N/A (arch §6) | ti-001 |
| 002 | InputGate four-term composition (Formula 5) | Logic | Ready | ADR-005 (D5) | sf-002 |
| 003 | Unified pointer surface + gate-gated routing | Integration | Ready | ADR-005 (D5) / arch §6 | ti-002, ti-003 |
| 004 | EventBridge hand-off + Shadow Board Model | Logic | Ready | ADR-005 (D1/D3) | jl-001 |
| 005 | Reveal Queue & replay scheduler ordering | Logic | Ready | ADR-005 (D1/D3) | jl-001 |
| 006 | juice_input_lock + gravity re-derivation + reshuffle live-query | Logic | Ready | ADR-005 (D5) | jl-002 |
| 007 | BoardPresenter grey-box replay (swap → paced motion → input unlock) | Integration | Ready | ADR-005 (D1/D3/D5) | jl-001, jl-002, ti-003 |
| 008 | ScreenFlowController 2-layer state machine (T1–T21) | Logic | Ready | arch §6 / ADR-005 (D5) | sf-001, sf-002 |
| 009 | Results waits on juice_input_lock + retry skips Pre-Level Card | Integration | Ready | ADR-005 (D3) / arch §7.3 | sf-003 |

*Manifest Version embedded in every story: 2026-07-18. TR coverage: ti-001/002/003, jl-001/002, sf-001/002/003 — all 8 owned TRs allocated. Work stories in ascending order; each story's `Depends on:` field lists prerequisites (intra-epic + E01–E04).*

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- A touch/mouse swap produces a `SwapRequest`, drives `BoardModel.TrySwap`, and the resulting
  event stream replays as paced grey-box piece motion; input re-enables exactly on
  `SETTLE_REVEAL` complete.
- The InputGate correctly vetoes input under each of the four terms; the ScreenFlowController
  walks all 9 composites and T1–T21 with the Results-waits-on-juice-lock ceiling honored.
- Integration stories have passing Play Mode tests (event replay, input routing) in
  `tests/integration/`; touch-input.md, screen-flow.md, and the juice-layer replay ACs are met.

## Next Step

Run `/create-stories game-runtime-input-screenflow`.
