# Story 003: Four extension seams (`ISpecialResolver`) with no-op MVP defaults

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Integration
> **Estimate**: S
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 3 (Extension Seams / Special Candies Handoff)
**Requirement**: `TR-be-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: `architecture.md` §8.1 (the four synchronous seams — `ISpecialResolver`; Board owns all validity judgment; seams re-checked against live state)
**Governing ADRs (secondary)**: ADR-005 (the `SpecialSpawn`/`SpecialType` shapes the seams return/consume).
**ADR Decision Summary**: Board Engine defines four synchronous seams (activation-check / activation-clears / special-spawns / chain-expansion). Each has a documented MVP no-op default so BoardModel is fully functional and headless-testable in isolation. **Advisory (non-blocking):** master-architecture §8.1 still threads `ScoreKeeper` through `ActivationClears`/`ExpandChain`; ADR-005 D2 is authoritative and **removes** that parameter — do NOT thread `ScoreKeeper` through any seam.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C# interface + default no-op implementation; no `UnityEngine`. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: Board owns ALL validity judgment; every seam response is re-validated against live board state before use (defensive validation lives with the callers in Stories 005/006).
- Forbidden: threading `ScoreKeeper` (or any Feature type) through `resolve_special_activation_clears` / `expand_special_chain_reaction` — this is the blueprint's superseded pattern (ADR-005). `BoardModel` references no Feature type by name.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md`, scoped to this story:*

- [ ] Four seams defined with exact signatures: `is_special_activation_swap(piece_a, piece_b) -> bool`; `resolve_special_activation_clears(piece_a, piece_b) -> Set[cell]`; `resolve_special_spawns(runs, swap_anchor_cells) -> Map[cell, SpecialSpawn]` where `SpecialSpawn = {special_type: int, color: int | null}`; `expand_special_chain_reaction(cleared_set) -> Set[cell]`.
- [ ] `test_seams_default_to_noop_with_no_consumer`: with no resolver registered, `is_special_activation_swap` always returns `false`, `resolve_special_spawns` always returns an empty map, and `expand_special_chain_reaction` always returns its input unchanged — BoardModel functions as a pure runs-only engine.
- [ ] Seam 2 is never called unless seam 1 returned `true` for that swap (seam-1 gates it).
- [ ] `swap_anchor_cells` semantics are supplied to seam 3 by the caller: `{cell_a, cell_b}` at `chain_index = 1` with `trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}`; empty set at `chain_index = 1` `BOOTSTRAP` and at every `chain_index ≥ 2` (verified via the caller in Stories 005/006).
- [ ] `SpecialSpawn.color = null` maps to a colorless special (`COLOR_NONE = -1`); an explicit `color_pool` index maps to a colored special.

---

## Implementation Notes

*Derived from board-engine §3 + `architecture.md` §8.1 + ADR-005:*

- Define `ISpecialResolver` in Domain with the four methods above. Provide a `NoOpSpecialResolver` default implementing the documented MVP defaults so BoardModel needs no `special-candies.md` dependency to run or test.
- `SpecialSpawn` is a Domain value type: `{ SpecialType Special; int? Color; }` — `null` Color → `ColorSentinel.None`.
- BoardModel holds the resolver behind the interface (constructor-injected). The concrete `SpecialResolver` (Stories 012–015) is registered by consumers; the four seam **call sites** live in swap validity (seam 1, Story 005), Clearing (seams 3 & 4, Stories 005/006).
- Seam 4's fixpoint loop is owned by BoardModel, not the resolver (each call is one single-pass expansion) — this story only defines the interface + no-op; the loop lives in Story 006.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 005: seam 1 call site (swap validity) + seam 3/4 call sites (Clearing) + defensive cell-membership validation of seam responses.
- Story 006: the seam-4 fixpoint loop + `MAX_CHAIN_EXPANSION_ITERATIONS` cap.
- Stories 012–015: the concrete `SpecialResolver` implementation of the four seams.

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — no-op defaults** (`test_seams_default_to_noop_with_no_consumer`)
  - Given: a BoardModel with the `NoOpSpecialResolver`.
  - When: each seam is invoked directly.
  - Then: seam 1 → `false`; seam 3 → empty map; seam 4 → input set unchanged; seam 2 is never reached (gated by seam 1).
  - Edge cases: seam 3 called with an empty `swap_anchor_cells`; seam 4 called with an empty clear set.
- **AC — signature/shape conformance**
  - Given: the `ISpecialResolver` interface.
  - When: inspected.
  - Then: signatures match board-engine §3 exactly; `SpecialSpawn` carries `{special_type, color: int?}`; no `ScoreKeeper`/Feature parameter appears on any seam.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: `tests/unit/board-engine/special_resolver_seams_noop_test.cs` — must exist and pass (interface + no-op behaviour).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (`Piece`/`Cell` types the seams operate on).
- Unlocks: Story 005 (seam 1 in swap validity, seam 3/4 in Clearing), Story 006 (seam-4 fixpoint loop), Stories 012–015 (concrete resolver).
