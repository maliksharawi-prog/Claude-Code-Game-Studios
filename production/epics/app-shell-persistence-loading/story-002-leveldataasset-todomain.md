# Story 002: LevelDataAsset ScriptableObject + ToDomain()

> **Epic**: App Shell — Boot, Persistence IO & Content Loading (E06)
> **Status**: Ready
> **Layer**: Foundation (Game-side)
> **Type**: Integration
> **Estimate**: M (~4h)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-data-format.md` (§1 file format/location, §2 schema, §5 authoring)
**Requirement**: `TR-ldf-002` (Designer Inspector-editable, one file per level, region subdirectories — LevelDataAsset SO + mapping)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-002: Addressables Grouping & Load Strategy (address==`level_id`; per-region `Levels_<code>` group). Secondary: ADR-006 (the `level_id`→ordinal linkage is resolved in Domain via `LevelManifest`, not here).
**ADR Decision Summary**: Each `LevelData` is a ScriptableObject on disk (`Assets/.../levels/<region_code>/*.asset`), one file per level, marked Addressable with its address equal to its `level_id`; the Game-side `LevelDataAsset.ToDomain()` materializes the pure Domain `LevelData` POCO (`.tres` → `.asset`, GDScript → C# per ADR-001).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: MEDIUM
**Engine Notes**: `ScriptableObject` + `AssetDatabase` are stable pre-cutoff APIs (LOW), but the addressable-address invariant and the region-batch resolution path (ADR-002) carry the HIGH Addressables risk indirectly. The GDD text is Godot-era (`.tres`/`@export`) — the design-level schema (every field name/type/default) is engine-agnostic and binding; the SO wrapper is the Unity realization.

**Control Manifest Rules (this layer — Game):**
- Required: engine types (the `ScriptableObject` wrapper) live in Game; `ToDomain()` produces a pure Domain POCO the Domain assembly can consume with zero engine reference; gameplay values are data-driven (authored in the asset, never hardcoded).
- Forbidden: never put `UnityEngine.*` types in the Domain `LevelData` POCO; never resolve a `level_id → int` mapping anywhere except `LevelManifest.ResolveOrdinal` (Domain); never load a level via `Resources.Load`.
- Guardrail: a whole region's `LevelData` batch is < 1 MB (~30 data-only SOs); manifest/level resolve is O(n≤120), off the frame loop.

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-format.md` §§1–2, §5 + ADR-002, scoped to this story:*

- [ ] A `LevelDataAsset` ScriptableObject holds one typed field per schema-v1 field (§2): `schema_version`, `level_id`, `region`, `display_number`, `grid_width`, `grid_height`, `cell_mask`, `pre_placed_pieces`, `color_pool`, `move_limit`, `objectives`, `star_1/2/3_score`, `rng_seed` — editable directly in the Inspector.
- [ ] Levels live one-file-per-level under `Assets/.../levels/<region_code>/`, one subdirectory per region (MVP: `candy_kingdom_hub`).
- [ ] Each `LevelDataAsset` is marked Addressable with its Addressables address equal to its `level_id` field (address==`level_id` invariant), in its region's `Levels_<region_code>` group.
- [ ] `ToDomain()` round-trips: a populated `LevelDataAsset` materializes a Domain `LevelData` POCO field-for-field, applying documented defaults for omitted optionals (`cell_mask` → full rectangle, `pre_placed_pieces` → `[]`, `rng_seed` → `-1`).
- [ ] A level-enter resolves its `LevelData` from the already-resident region batch by `level_id` with no additional Addressables call (defensive fallback: `TryLoad<LevelDataAsset>(level_id)` if the batch is not resident).
- [ ] The Domain `LevelData` POCO carries zero `UnityEngine` symbols (CI Domain-purity check passes for it).

---

## Implementation Notes

*Derived from ADR-002 + `level-data-format.md` §§1–2:*

- The SO wrapper is Game-side; `ToDomain()` mirrors `LevelDataAsset.ToDomain()` the same way `LevelManifestAsset.ToDomain()` does (ADR-006) — an explicit field-by-field map, no reflection.
- The pure `LevelData` schema (the POCO target) is E02's Domain deliverable — this story maps into it, it does not define it.
- Do not implement runtime schema validation (V1–V19) here — that is E02's Domain `Validate()` + the Editor validation suite; this story's job is the SO shape, the `ToDomain()` mapping, and the addressable wiring.
- `rng_seed = -1` on all MVP levels (unseeded); any `>= 0` is reserved for Phase 3 daily-challenge/event levels — carry the field through `ToDomain()` unchanged.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 001: the `IContentLoader` load mechanism (used to resolve the asset).
- E02: the pure Domain `LevelData` schema/POCO, the V1–V19 validation suite, and the `LevelManifest` ordinal resolution.
- The Editor A1–A6 `AddressablesValidation` tool that enforces address==`level_id` in CI (Editor & CI layer) — this story sets the invariant on the asset; the CI gate lives elsewhere.
- Authoring the 10 MVP level `.asset` files themselves (E10 content-levels-manifest).

---

## QA Test Cases

*Integration story — Edit/Play Mode round-trip test (SO ↔ Domain POCO + addressable resolution).*

- **AC-1 (schema fields)**: A `LevelDataAsset` exposes every §2 field with the correct type, Inspector-editable. Edge cases: optional fields omitted.
- **AC-2 (ToDomain round-trip)**: A populated asset → `ToDomain()` → Domain `LevelData` is field-for-field identical; omitted `cell_mask`/`pre_placed_pieces`/`rng_seed` apply documented defaults.
- **AC-3 (address==level_id)**: Assert the asset's Addressables address equals its `level_id` field and it belongs to `Levels_<region_code>`.
- **AC-4 (resolve from batch)**: Play Mode — with a region batch resident, resolving a `level_id` returns its `LevelData` with no additional Addressables call; with the batch absent, the defensive `TryLoad` path returns it (or `Fail` cleanly on an unknown id).
- **AC-5 (Domain purity)**: The `LevelData` POCO produced by `ToDomain()` contains no `UnityEngine`/`UnityEditor` symbols (Domain CI grep passes).

*Verification: round-trip test + a data-driven smoke check (`production/qa/smoke-*.md`) over the resident region batch.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/content-loading/leveldata_todomain_test.cs` — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (IContentLoader); E01 (assemblies + Addressables), E02 (Domain `LevelData` schema/POCO that `ToDomain()` targets).
- Unlocks: Story 005 (BootLoader / level-enter resolves `LevelData` via this asset); E10 (authors the 10 MVP level assets against this SO).
