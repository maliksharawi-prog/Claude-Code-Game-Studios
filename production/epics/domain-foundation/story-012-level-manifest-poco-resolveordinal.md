# Story 012: LevelManifest Domain POCO — ResolveOrdinal, tombstones, WorldMapManifest POCO

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/world-map.md` (§2 Manifest Architecture), `design/gdd/rng-service.md` (§3 integer `level_id`), `design/gdd/board-engine.md` §2 (append-only ordinal)
**Requirement**: `TR-rng-004`, `TR-wm-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-006: Level Manifest Generation & Cross-Validation (primary)
**Governing ADRs (secondary)**: ADR-004 (the resolved integer `level_id` this POCO produces is what RNG Formula F1 consumes — TR-rng-004).
**ADR Decision Summary**: The ordinal manifest is a pure Domain POCO with an **append-only** `entries: List<string>` and an additive `retired_ordinals: List<int>` tombstone field. `ResolveOrdinal(levelId) = entries.FindIndex(id) + 1` (1-based; `0` = unresolved sentinel). The hand-authored `WorldMapManifest` POCO (regions + `level_sequence`) is a **separate** type. `LevelIdentity = (levelId, region)` feeds the cross-validator (Story 013).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure C# POCO; `noEngineReferences`)
**Engine Notes**: The `ScriptableObject` wrappers (`LevelManifestAsset.ToDomain()`, `WorldMapManifestAsset`) live in `SweetCascade.Game` (E06); this story is the pure Domain POCO only. The generated `.asset` (append-only) and the hand-authored world-map `.asset` are never merged into one file (ADR-006).

**Control Manifest Rules (Domain layer)**:
- Required: `LevelManifest`, `WorldMapManifest`, `ManifestCrossValidator`, `LevelIdentity` live in Domain (`Levels/`), `noEngineReferences`.
- Required: `ResolveOrdinal(levelId) = entries.FindIndex(id) + 1`; `0` is the "unresolved" sentinel; O(n≤120), called once per level entry, never in a frame loop.
- Required: `level_id` reaching the RNG must be a resolved `int` ordinal from `ResolveOrdinal`, never a runtime string hash.
- Forbidden: hashing the string `level_id` at runtime to derive an RNG seed input; hardcoding any `level_id → int` mapping anywhere outside `ResolveOrdinal`; deleting/renumbering an existing `entries` slot (tombstone via `retired_ordinals` — enforced in Story 014).

---

## Acceptance Criteria

*From GDD `world-map.md` §2 / `rng-service.md` §3 / ADR-006 Key Interfaces & Validation Criteria, scoped to this story:*

- [ ] `LevelManifest` is a pure POCO exposing `SchemaVersion`, `IReadOnlyList<string> Entries` (append-only, positional), `IReadOnlyCollection<int> RetiredOrdinals`, `int ResolveOrdinal(string)`, `bool IsRetired(int)`.
- [ ] `ResolveOrdinal("candy_kingdom_hub-007")` returns a stable 1-based ordinal; `ResolveOrdinal(unknown)` returns `0` (sentinel).
- [ ] `IsRetired(ordinal)` returns true for a tombstoned ordinal; a retired ordinal's slot is never reused for a different `level_id`.
- [ ] `WorldMapManifest` POCO carries the hand-authored region graph shape needed for cross-validation: `regions` with `region_code`, `stars_required_to_unlock`, and ordered `level_sequence: IReadOnlyList<string>` per region.
- [ ] `LevelIdentity` = `(string LevelId, string Region)` — the scanned-file input to the cross-validator (Story 013).
- [ ] No hardcoded `level_id → int` mapping exists anywhere except `ResolveOrdinal` (data-driven rule).
- [ ] The Domain POCOs compile under `noEngineReferences` — zero `UnityEngine`/`UnityEditor` symbols.

---

## Implementation Notes

*Derived from ADR-006 Key Interfaces and Implementation Guidelines:*

- Place `LevelManifest.cs`, `WorldMapManifest.cs`, `LevelIdentity.cs`, and the `ManifestViolation`/`ManifestRule`/`ManifestGenResult` types under `Assets/Domain/Levels/` in `SweetCascade.Domain` (ADR-006; arch §5.3).
- `ResolveOrdinal` is `entries.FindIndex(id) + 1` (faithful to board-engine Formula 1; index-0 stays the unresolved sentinel). It is called once per level entry at bootstrap and hands the int to `RngService.StartLevelSession(ordinal, attempt)` (Story 001) — the concrete TR-rng-004 mechanism.
- `retired_ordinals` defaults to empty → backward-compatible with board-engine's stated `entries: Array[String]` schema (the additive extension flagged in ADR-006 for a reciprocal board-engine.md note).
- The **generation** of the asset, the prefix-invariant append-only guard, and tombstoning writes are the Editor tool (Story 014); this POCO only *reads* an already-produced manifest and resolves ordinals.
- `WorldMapManifest` here is the pure read-model consumed by the cross-validator; the full world-map Formulas 1–6 (unlock/display-number/completion) are E07's runtime concern, not this Foundation story.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 013: `ManifestCrossValidator.Validate` (bijection + W6/W7/W8).
- Story 014: `LevelManifestGenerator` (the Editor tool that scans, appends, tombstones, and writes the asset; the prefix-invariant guard; pre-build/CI verify).
- E06: the `LevelManifestAsset`/`WorldMapManifestAsset` ScriptableObject wrappers + `ToDomain()` + direct-reference boot load (not Addressables).
- E07: world-map runtime formulas (region/level unlock, display number, completion %).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: ordinal resolution (ADR-006 Validation).
  - Given: a `LevelManifest` with `entries = [candy_kingdom_hub-001 … -010]`.
  - When: `ResolveOrdinal("candy_kingdom_hub-007")` and `ResolveOrdinal("nope")` are called.
  - Then: returns `7` and `0` respectively.
  - Edge cases: `entries` order determines ordinal; the 1-based offset is exact; index-0 is never a valid ordinal.

- **AC-2**: tombstone read.
  - Given: a manifest with `retired_ordinals = [3]`.
  - When: `IsRetired(3)` and `IsRetired(4)` are called.
  - Then: `true` and `false`; `entries` still contains the retired slot's `level_id` (never removed).

- **AC-3**: world-map read-model + LevelIdentity.
  - Given: a `WorldMapManifest` with one region and a `LevelIdentity` set from scanned files.
  - When: read.
  - Then: region `level_sequence` order is preserved; `LevelIdentity` exposes `(LevelId, Region)` for each scanned level.

- **AC-4**: purity + data-driven.
  - Setup: inspect `Assets/Domain/Levels/`.
  - Verify: no `UnityEngine`/`UnityEditor` symbol; no hardcoded `level_id→int` map outside `ResolveOrdinal`.
  - Pass condition: compiles under `noEngineReferences`; the L2 denylist scan passes.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/world-map/level_manifest_resolve_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Levels/` (Domain manifest POCO), headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **E01 Story 002** (Domain + `Domain.Tests` asmdefs; `Assets/Domain/Levels/` tree). Soft: **Story 005** (shares the `Levels/` folder and `level_id` conventions).
- Unlocks: Story 013 (cross-validator consumes `LevelManifest`/`WorldMapManifest`/`LevelIdentity`), Story 014 (generator produces the asset this POCO reads), E03 Board Engine (calls `ResolveOrdinal` at bootstrap → RNG F1).
