# Story 007: No-valid-move detection & reshuffle (Fisher–Yates, regeneration fallback)

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 11 (No-Valid-Move Detection & Reshuffle Policy), § Detailed Rules 6 (RNG draw ordering — reshuffle situations 2 & 4)
**Requirement**: `TR-be-005`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity Guard (primary — `IRngService.Shuffle("board-refill", list)` Fisher–Yates per rng-service Formula F6)
**Governing ADRs (secondary)**: ADR-005 (`NoValidMovesDetected`/`BoardReshuffled` records); board-engine §11 (algorithm — architectural home).
**ADR Decision Summary**: Reshuffle draws the `board-refill` stream through `IRngService.Shuffle`; determinism (same seed + prior draw history → same result) is guaranteed by ADR-004's platform-stable generator.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, seeded RNG — no `UnityEngine`, no `System.Random`, no `float` on the draw path. Determinism must hold across Mono/IL2CPP/WebGL. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: reshuffle uses `IRngService.Shuffle("board-refill", …)`; `RESHUFFLE_MAX_TRIES` (default 60) is centralized Domain config.
- Required: `BoardReshuffled(attempts_used)` payload emitted through the injected sink; reshuffle reassigns existing pieces' `(color, special_type)` across cells — never clears/spawns/places new pieces.
- Forbidden: `System.Random`/`UnityEngine.Random`; consuming any RNG stream other than `board-refill`.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] `has_available_move()`: for every `OCCUPIED` cell, simulate the swap with each of its right/down neighbours and check whether either swapped position joins a run ≥3 (then revert); additionally, for every `special_type != SPECIAL_NONE` cell, check seam 1 against each of its four neighbours. Returns `true` on the first available move; `false` only when exhausted. `test_has_available_move_detects_horizontal_and_vertical_candidates`; `test_has_available_move_false_on_synthetic_deadlock_board`; `test_special_piece_always_counts_as_available_move`.
- [ ] Reshuffling is entered when a stabilized board would return to `Idle` but `has_available_move()` is `false`: emit `NoValidMovesDetected` (no payload) before shuffling.
- [ ] Reshuffle algorithm: collect every `OCCUPIED` piece's `(color, special_type)` in row-major order; `Shuffle("board-refill", list)`; apply candidate; accept iff (a) zero runs exist AND (b) `has_available_move()` is `true`. On success emit `BoardReshuffled(attempts_used)` and proceed to `Idle`. `test_reshuffle_never_produces_immediate_match`; `test_reshuffle_guarantees_available_move`.
- [ ] Retry up to `RESHUFFLE_MAX_TRIES = 60`; on exhaustion, full board regeneration via the bootstrap fill algorithm with fresh `board-refill` draws. `test_reshuffle_exhausts_to_full_regeneration` (fallback triggers exactly once, produces a valid movable board).
- [ ] `test_reshuffle_consumes_no_player_move`: reshuffle never emits `SwapAccepted` and never affects any external move counter (invisible to Level Objective).

---

## Implementation Notes

*Derived from board-engine §11/§6 + ADR-004:*

- Reshuffle reassigns attributes to positions, not positions themselves: collect `(color, special_type)` pairs row-major (row 0→rows-1, left→right — the same traversal bootstrap fill uses), shuffle the list, map each candidate index back to the same row-major-ordered cell.
- Specials survive intact: a not-yet-activated Striped/Color Bomb keeps its exact `(color, special_type)` and relocates with the shuffle (verified further in Story 015).
- The reshuffle-check is invoked at the end of every move's cascade (after `CascadeEnded`, Story 006) and at bootstrap step 9 (Story 008). This story delivers the mechanism; the callers hook it in.
- Full regeneration fallback re-runs bootstrap fill (Story 004's retry-until-no-match routine) with fresh draws; itself fully deterministic (same seed + prior history → same trigger + same board).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: the cascade loop that calls the reshuffle-check.
- Story 008: bootstrap step-9 reshuffle invocation.
- `board_reshuffled` deferred-replay payload sufficiency (Open Question, `juice-layer.md` scope — a live `get_piece_at()` sweep remains valid at MVP).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — has_available_move** (`test_has_available_move_detects_horizontal_and_vertical_candidates`, `test_has_available_move_false_on_synthetic_deadlock_board`, `test_special_piece_always_counts_as_available_move`)
  - Given: (a) boards with a hypothetical match in each direction; (b) a hand-constructed deadlock board; (c) a mocked seam-1 → `true` for a given special.
  - When: `has_available_move()` runs.
  - Then: (a) `true`; (b) `false`; (c) any cell adjacent to that special registers as available regardless of color.
- **AC — reshuffle validity** (`test_reshuffle_never_produces_immediate_match`, `test_reshuffle_guarantees_available_move`)
  - Given: a stabilized board with no legal move.
  - When: reshuffle succeeds.
  - Then: `NoValidMovesDetected` fires; result has zero runs AND a legal move; `BoardReshuffled(attempts_used)`.
- **AC — regeneration fallback** (`test_reshuffle_exhausts_to_full_regeneration`)
  - Given: a mocked `shuffle()` engineered to never satisfy both conditions within `RESHUFFLE_MAX_TRIES`.
  - When: reshuffle runs.
  - Then: full regeneration triggers exactly once, producing a valid movable board.
- **AC — zero move cost** (`test_reshuffle_consumes_no_player_move`)
  - Given: a mid-game reshuffle.
  - When: it resolves.
  - Then: no `SwapAccepted`; no external move-counter effect.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/reshuffle_test.cs` — must exist and pass (seeded `IRngService`).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (board model), Story 002 (run check), Story 003 (seam 1 for special-move detection), Story 004 (regeneration reuses bootstrap fill), E02 (`IRngService.Shuffle` + `board-refill`).
- Unlocks: Story 006/008 callers (reshuffle-check hook), Story 015 (special-survives-reshuffle).
