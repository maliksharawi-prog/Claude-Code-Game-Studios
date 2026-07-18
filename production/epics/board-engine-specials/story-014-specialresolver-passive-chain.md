# Story 014: `SpecialResolver` passive chain reaction & deterministic bomb detonation (seam 4, Formula 8) + Harvest Observation Point

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Feature
> **Type**: Logic
> **Estimate**: L
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/special-candies.md` — § Detailed Rules 6 (Passive Chain-Reaction Rules, seam 4), Formula 8 (target-color resolution), § Detailed Rules 9 (Harvest Observation Point)
**Requirement**: `TR-sc-001`, `TR-sc-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.1 (SpecialResolver implements seam 4 `expand_special_chain_reaction`, single-pass, with zero Board Engine change)
**Governing ADRs (secondary)**: ADR-005 (`PieceSnapshot` identity on every `MatchCleared`/`SpecialActivated` — the Harvest Observation Point, TR-sc-003).
**ADR Decision Summary**: Seam 4 is a single non-recursive pass; Board Engine owns the fixpoint loop (Story 006). The Harvest Observation Point is a documented commitment (not a new API): per-color identity is preserved on every clear path via the existing `cleared_pieces` `PieceSnapshot` payloads.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#, zero engine surface, zero RNG (Formula 8 is deterministic most-common-color). No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: each seam-4 call performs exactly one single-pass expansion and returns `cleared_set ∪ contributions`; the resolver never loops internally (Board Engine owns the fixpoint). `MAX_CHAIN_EXPANSION_ITERATIONS` is Board Engine's config, not re-declared here.
- Required: per-color `PieceSnapshot` identity is preserved on every clear path this resolver touches (Harvest Observation Point — no API change).
- Forbidden: threading `ScoreKeeper`/any Feature type through seam 4; consuming the `special-drop` RNG stream (zero draws at MVP).

---

## Acceptance Criteria

*From `design/gdd/special-candies.md`, scoped to this story:*

- [ ] **Single-pass contribution**: for each cell in the input `cleared_set` whose piece has a non-`SPECIAL_NONE` type, add its contribution — `STRIPE_H` → its own row's `OCCUPIED` cells; `STRIPE_V` → its own column; `COLOR_BOMB` → every cell of the Formula 8 target color — and return `cleared_set ∪ contributions`. `test_seam4_single_call_never_recurses_internally`.
- [ ] **Passive Striped chain** (multi-step via Board Engine's fixpoint loop): `test_passive_striped_chain_multi_step` — one Striped's row-clear catches a second Striped whose column-clear catches a third, all within one Clearing pass.
- [ ] **Passive bomb target** (Formula 8): `test_passive_bomb_targets_most_common_color` (clears the board's single most-common color at that instant); `test_passive_bomb_tiebreak_lowest_color_index` (ties → lowest `color_pool` index, deterministic); `test_passive_bomb_no_colored_pieces_no_expansion` (all-specials board → zero expansion, no error); `test_two_passive_bombs_same_step_idempotent` (both compute the identical target against the frozen `board_state`; set-union idempotent).
- [ ] **Passive vs swap-activated mutual exclusivity**: `test_swap_activated_bomb_never_uses_passive_target_rule` — a swap-activated bomb uses its partner's color (Formula 4/7), never Formula 8.
- [ ] **Freshly-spawned specials not caught same step**: `test_freshly_spawned_special_not_caught_same_step` — a seam-3-created special is absent from that same Clearing pass's seam-4 input.
- [ ] **Void exclusion**: `test_striped_line_clear_excludes_void_cells_automatically` — a Striped line-clear naturally excludes `VOID` cells with no special-casing.
- [ ] **Harvest Observation Point** (TR-sc-003): every cell this resolver clears reports through Board Engine's `MatchCleared`/`SpecialActivated` `cleared_pieces` with full `PieceSnapshot` identity — a per-color tally is derivable from the event stream with zero API change (verified end-to-end against Story 009's deferred-replay tally).

---

## Implementation Notes

*Derived from special-candies §6 + Formula 8 + §9:*

- `expand_special_chain_reaction(cleared_set)` is exactly one pass (Board Engine re-invokes it to a fixpoint per Story 006). A piece already fully accounted for contributes nothing new — Board Engine's fixpoint detection terminates the loop naturally; no "already processed" bookkeeping in this resolver.
- Formula 8 `target_color` is evaluated **once** against `board_state` as it stood at the start of the current Clearing pass (Board Engine does not mutate occupancy until after the seam-4 fixpoint completes), so it is stable and identical across every seam-4 call within the step; two bombs agree by construction (idempotent union).
- All-specials board (empty `argmax` domain): the bomb contributes no expansion cells (clears only as its own single cell) — the defined, safe fallback, never an error.
- Harvest Observation Point is not a new API/signal/seam — it is a discipline: never strip per-piece color identity from any clear path. Board Engine's existing `cleared_pieces` `PieceSnapshot` payloads already carry it; this resolver adds nothing and removes nothing.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: the seam-4 fixpoint loop + `MAX_CHAIN_EXPANSION_ITERATIONS` cap (Board Engine owns it; this story supplies the single-pass resolver).
- Story 012/013: creation rules; swap-triggered combos.
- Story 015: reshuffle survival, determinism, seam-compat closers.
- Booster Brewing ingredient yield (Phase 2 — this story only preserves the identity the future tally will read).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — single pass & Striped chain** (`test_seam4_single_call_never_recurses_internally`, `test_passive_striped_chain_multi_step`)
  - Given: a clear set containing chained Striped candies.
  - When: seam 4 is invoked (Board Engine looping it).
  - Then: each call is one pass; the multi-step chain fully expands within one Clearing pass.
- **AC — Formula 8 target** (`test_passive_bomb_targets_most_common_color`, `test_passive_bomb_tiebreak_lowest_color_index`, `test_passive_bomb_no_colored_pieces_no_expansion`, `test_two_passive_bombs_same_step_idempotent`)
  - Given: boards with a clear most-common color / a tie / all-specials / two caught bombs.
  - When: a bomb is passively caught.
  - Then: clears the most-common color; ties → lowest index; all-specials → no expansion; two bombs agree idempotently.
- **AC — mutual exclusivity & fresh-spawn** (`test_swap_activated_bomb_never_uses_passive_target_rule`, `test_freshly_spawned_special_not_caught_same_step`)
  - Given: a swap-activated bomb; a seam-3-spawned special this step.
  - When: resolution proceeds.
  - Then: swap-activated bomb uses partner color (never Formula 8); the fresh special is not in this step's seam-4 input.
- **AC — void exclusion & harvest** (`test_striped_line_clear_excludes_void_cells_automatically`)
  - Given: a `cell_mask` void in a Striped's row/column; a full move's clear stream.
  - When: the line clears and the stream is tallied.
  - Then: void cells excluded automatically; a per-color tally is derivable from `cleared_pieces` alone (zero live queries).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/special-candies/passive_chain_test.cs` — must exist and pass.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 012 (`SpecialResolver` base), Story 013 (combo matrix — mutual-exclusivity check), Story 006 (seam-4 fixpoint loop), Story 009 (deferred-replay tally for the Harvest check).
- Unlocks: Story 015 (closers), E04 (scoring/objectives read `cleared_pieces` identity this preserves).
