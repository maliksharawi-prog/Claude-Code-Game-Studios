# Story 015: `SpecialResolver` seam-compatibility, reshuffle interaction & determinism closers

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Feature
> **Type**: Integration
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/special-candies.md` — § Detailed Rules 7 (Reshuffle Interaction), § Detailed Rules 8 (RNG usage: none), § Detailed Rules 10 (Seam Implementation Summary), Acceptance Criteria (Seam Compatibility, Determinism, Reshuffle Interaction)
**Requirement**: `TR-sc-001`, `TR-sc-002`, `TR-sc-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.1 (seam contract — no seam's signature is extended, narrowed, or reinterpreted; SpecialResolver imposes zero requirement on Board Engine's isolated-mode behaviour)
**Governing ADRs (secondary)**: ADR-005 (determinism/ordering; `SpecialType` append-only vocabulary); ADR-004 (`special-drop` stream reserved, zero draws at MVP).
**ADR Decision Summary**: SpecialResolver's four functions match board-engine §3's signatures exactly; the whole special layer is deterministic (zero RNG); specials survive reshuffle intact (BoardModel reassigns `(color, special_type)` pairs across cells).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, zero engine surface. Determinism holds byte-identically across Mono/IL2CPP/WebGL. Zero draws from the reserved `special-drop` stream. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: no seam signature is extended/narrowed/reinterpreted (verified by inspection); `special_type` vocabulary append-only with `WRAPPED = 4` reserved.
- Required: the special layer consumes zero RNG — every combo/passive rule is deterministic; determinism tests are seeded and reproducible.
- Forbidden: consuming the `special-drop` stream at MVP; any `UnityEngine.*` in Domain.

---

## Acceptance Criteria

*From `design/gdd/special-candies.md`, scoped to this story:*

- [ ] **Seam compatibility** (BLOCKING): `test_board_engine_runs_unmodified_with_zero_specials_registered` — with no resolver registered, Board Engine's own `test_seams_default_to_noop_with_no_consumer` suite passes unchanged; `test_seam_signatures_match_board_engine_exactly` — interface inspection confirms the four functions match board-engine §3 exactly (parameter + return types).
- [ ] **Reshuffle interaction**: `test_special_survives_reshuffle_with_identity_intact` — a not-yet-activated Striped or Color Bomb present before a mid-game reshuffle retains its exact `(color, special_type)` at its new post-shuffle cell.
- [ ] **Determinism** (BLOCKING): `test_identical_seed_identical_combo_outcomes` — two runs with an identical injected seed + identical ordered swap-intent sequence (including at least one of every combo-matrix cell and one passive chain) produce byte-identical spawn locations, combo clear sets, and passive-detonation target colors.
- [ ] **Zero special-drop draws**: `test_zero_special_drop_stream_draws_at_mvp` — instrumenting `special-drop` (stream_id 2) across a full synthetic playthrough exercising every creation rule and combo confirms exactly `0` draws.

---

## Implementation Notes

*Derived from special-candies §7/§8/§10:*

- These are integration/regression closers over Stories 012–014 + the board reshuffle (Story 007) and determinism harness (Story 010). No new special behaviour is introduced.
- Reshuffle survival is a property of BoardModel's reassignment (Story 007) — this story verifies a not-yet-activated special keeps its identity through a mid-game reshuffle end-to-end.
- Determinism: drive `MoveResolver` (Story 009) with a seeded `StartTestSession` + a fixed intent sequence covering every matrix cell and a passive chain; assert byte-identical spawn cells, combo clear sets, and Formula 8 target colors across two runs.
- `special-drop` instrumentation: assert the reserved stream (stream_id 2) receives its per-session sub-seed but records zero `NextX` draws throughout — confirming §8's "RNG usage: none" claim.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Stories 012–014: the creation/combo/passive behaviours themselves (this story only regresses them as a whole).
- Story 007: the reshuffle mechanism (this story verifies special survival through it).
- Story 010: the board-level determinism master gate (this story adds the specials-specific determinism assertions).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — seam compatibility** (`test_board_engine_runs_unmodified_with_zero_specials_registered`, `test_seam_signatures_match_board_engine_exactly`)
  - Given: Board Engine with no resolver; and the `ISpecialResolver` signatures.
  - When: run / inspected.
  - Then: the no-op suite passes unchanged; signatures match board-engine §3 exactly.
- **AC — reshuffle survival** (`test_special_survives_reshuffle_with_identity_intact`)
  - Given: a not-yet-activated Striped/Color Bomb before a mid-game reshuffle.
  - When: reshuffle resolves.
  - Then: the special retains its exact `(color, special_type)` at its new cell.
- **AC — determinism** (`test_identical_seed_identical_combo_outcomes`)
  - Given: two runs, identical seed + intent sequence covering every combo cell + a passive chain.
  - When: both resolve.
  - Then: byte-identical spawn cells, combo clear sets, and passive target colors.
- **AC — zero special-drop draws** (`test_zero_special_drop_stream_draws_at_mvp`)
  - Given: a full synthetic playthrough exercising every rule.
  - When: `special-drop` is instrumented.
  - Then: exactly 0 draws.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/integration/special-candies/seam_compat_determinism_test.cs` (+ `tests/unit/special-candies/reshuffle_interaction_test.cs`) — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 012, Story 013, Story 014 (the special behaviours), Story 007 (reshuffle), Story 010 (determinism harness).
- Unlocks: None (E03 epic closer — Board Engine + Special Candies fully proven in Edit Mode).
