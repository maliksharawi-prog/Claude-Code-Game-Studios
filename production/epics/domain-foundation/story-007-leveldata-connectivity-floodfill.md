# Story 007: LevelData V8 connectivity flood-fill (single 4-connected board)

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 1 day
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-data-format.md` (§4 rule V8, §6 "split boards" out-of-scope, Edge Cases, Acceptance Criteria)
**Requirement**: `TR-ldf-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: N/A — no dedicated ADR. V8 is governed by master architecture **§6 (LevelData `Validate()`)**; it is the connectivity clause of the V1–V19 suite (TR-ldf-003). Board Engine's gravity/refill logic assumes single-region connectivity, which V8 guarantees.
**ADR Decision Summary**: The playable (`'1'`) cells in `cell_mask` must form a single 4-directionally-connected region — a flood-fill from any playable cell must reach every other playable cell. Disconnected/split boards are invalid in v1 (Blocking).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure C# graph traversal)
**Engine Notes**: None — a BFS/DFS over the boolean mask, no engine surface. Deterministic and headless.

**Control Manifest Rules (Domain layer)**:
- Required: `Validate()` stays a pure Domain function; V8 returns a violation as data, no engine types, no IO.
- Required: every constant is data-driven — V8 composes with V6/V7's parsed mask, using the same `MIN_PLAYABLE_CELLS` config where relevant.
- Guardrail: O(cells) traversal over a ≤ 9×9 = 81-cell board — trivially sub-millisecond, off any runtime path.

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-format.md` §4 (V8), Edge Cases, and Acceptance Criteria, scoped to this story:*

- [ ] V8 flood-fills from any single `'1'` cell using 4-directional adjacency and asserts every `'1'` cell is reached; if any playable cell is unreached, the level is **invalid** (Blocking).
- [ ] A fully-connected mask (including the full-rectangle default) passes V8.
- [ ] A mask describing two or more disconnected playable regions fails V8.
- [ ] `test_v8_rejects_disconnected_regions`: a `cell_mask` with two disjoint playable regions fails V8.
- [ ] V8 runs only over the parsed/defaulted `cell_mask` (composes with V6); it does not re-validate mask shape (that is V6).
- [ ] V8 is wired into the same `Validate()` violation list as V1–V19 (Story 006), so a single validation pass reports V8 alongside the other Blocking rules.

---

## Implementation Notes

*Derived from GDD §4 (V8) and §6 (split boards deferred):*

- Implement as a helper on the validator in `Assets/Domain/Levels/` (e.g. `IsSingleConnectedRegion(cellMask)`), invoked by `Validate()` as rule V8.
- Use an explicit BFS/DFS with a visited set over `(row, col)` neighbours `{up, down, left, right}`, restricted to `'1'` cells and grid bounds. Count reached cells and compare to the total `'1'` count.
- Order relative to V6/V7: only run V8 after V6 (mask shape) parses cleanly; a malformed mask is a V6 failure, not a V8 one.
- Split boards are explicitly out of v1 scope (§6) — do not add a "multiple regions allowed" mode; V8 is a hard single-region requirement.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: all other V1–V19 scalar/structural rules (V8 slots into that same suite).
- Story 005: `cell_mask` parsing/defaulting (V8 consumes the parsed mask).
- Any relaxation to multi-region ("split") boards — deferred per level-data-format §6.

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: connected passes.
  - Given: a full-rectangle mask and an L-shaped connected mask (both ≥ 16 cells).
  - When: V8 runs.
  - Then: both pass (single connected region).

- **AC-2**: disconnected fails (`test_v8_rejects_disconnected_regions`).
  - Given: a mask with two disjoint `'1'` clusters separated by `'0'`s.
  - When: V8 runs.
  - Then: V8 fails Blocking; the violation identifies the connectivity rule.
  - Edge cases: a single-cell "island" separated from the main region also fails; a diagonal-only adjacency does NOT count as connected (4-directional only).

- **AC-3**: integration with the suite.
  - Given: a fixture that is both disconnected (V8) and has non-increasing stars (V16).
  - When: `Validate()` runs once.
  - Then: both V8 and V16 violations appear in the single returned list.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/level-data-format/leveldata_connectivity_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Levels/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 005** (parsed `cell_mask`), **Story 006** (the `Validate()` violation-list host V8 slots into). Transitively **E01 Story 002**.
- Unlocks: E10 (the 10 MVP levels' masks are connectivity-validated), E03 Board Engine (relies on the single-region guarantee for gravity/refill).
