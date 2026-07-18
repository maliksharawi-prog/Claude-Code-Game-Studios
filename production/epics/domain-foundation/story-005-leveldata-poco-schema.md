# Story 005: LevelData Domain POCO — schema v1 shape, defaults, closed enums, additive display_name

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-data-format.md` (§2 Schema v1 Field Reference, §3 Versioning & Migration, §6 Out of Scope; Objective Types closed enum)
**Requirement**: `TR-ldf-001`, `TR-ldf-004`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: N/A — no dedicated ADR. The LevelData Domain POCO shape/defaults/closed-enum handling and additive `display_name` are governed by master architecture **§6 (LevelData Domain POCO)**, the architectural home for TR-ldf-001/004. (ADR-006 governs only the manifest cross-check, not the POCO shape.)
**ADR Decision Summary**: The versioned level schema lives as a pure Domain POCO with typed fields, documented defaults for optional fields, closed enums for `candy_type`/`objectives[].type`, and an additive-field migration policy (a new **optional** field with a default needs **no** `schema_version` bump). `LevelDataAsset.ToDomain()` (the Game SO wrapper) maps into this POCO — but the SO itself is E06.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure C# POCO, zero engine surface)
**Engine Notes**: `.tres`→`.asset` is an ADR-001 residue concern for the SO wrapper (E06); the Domain POCO here references no engine type. The GDD's Godot `Resource`/Inspector authoring language maps to a Unity `ScriptableObject` at the Game layer, out of scope for this pure story.

**Control Manifest Rules (Domain layer)**:
- Required: engine types (`UnityEngine.*`) never appear in Domain; the schema is a pure POCO.
- Required: every gameplay constant is data-driven — `MIN_PLAYABLE_CELLS`, `CURRENT_SCHEMA_VERSION`, the supported-version set, and the canonical candy roster live in one centralized Domain config location, never scattered literals.
- Required: closed-enum values (`candy_type`, `objectives[].type`) are not silently ignored — an unrecognized closed-enum value is a hard failure (validated in Story 006), never a partial load.
- Guardrail: schema migration is an ordered pipeline of pure `MigrateV{n}ToV{n+1}` functions (none exist yet; current == 1).

---

## Acceptance Criteria

*From GDD `design/gdd/level-data-format.md` (§2, §3, Edge Cases) and arch §6, scoped to this story:*

- [ ] A `LevelData` POCO carries every schema-v1 field with the GDD types: `schema_version:int`, `level_id:string`, `region:string`, `display_number:int`, `grid_width:int`, `grid_height:int`, `cell_mask:IReadOnlyList<string>`, `pre_placed_pieces` (list of `{row,col,candy_type}`), `color_pool:IReadOnlyList<string>`, `move_limit:int`, `objectives` (list of typed objective records), `star_1_score/star_2_score/star_3_score:int`, `rng_seed:int`.
- [ ] Optional fields apply their documented defaults when absent: `cell_mask` → full rectangle (`grid_height` rows of `grid_width` `'1'`s); `pre_placed_pieces` → `[]`; `rng_seed` → `-1`.
- [ ] Objective types are a **closed enum** with exactly two v1 members — `score_target {target:int}` and `collect_color {color:string, count:int}`.
- [ ] The **additive `display_name`** field (TR-ldf-004): a new **optional** string field is present with a safe default and requires **no** `schema_version` bump — demonstrating the additive-field migration policy (§3 row 1). A file lacking it loads with the default; a file carrying an unrecognized *optional* field loads (skipped + warned), never rejected.
- [ ] `CURRENT_SCHEMA_VERSION == 1` and the supported set is `{1}`; the migration pipeline is an ordered list of pure `MigrateV{n}ToV{n+1}` functions (empty at v1).
- [ ] No gameplay value (grid size, color pool, move limit, targets, star thresholds) is a hardcoded literal in `src/` — every instance is traceable to level data (spot-check).

---

## Implementation Notes

*Derived from GDD §2/§3 and arch §6:*

- Place `LevelData.cs` (+ `Objective` record types, `PrePlacedPiece` record) under `Assets/Domain/Levels/` in `SweetCascade.Domain` (arch §5.3).
- Model objectives as a discriminated set (base + `ScoreTargetObjective`/`CollectColorObjective` records) or a typed record with a closed `ObjectiveType` enum — either way the *parse/validation* rejects unknown types (Story 006), and the POCO preserves list order (Screen Flow reads order for badge display).
- The canonical candy roster `{strawberry, citrus, lemon, apple, grape}` and `MIN_PLAYABLE_CELLS = 16` are centralized Domain constants (art-bible roster), not inlined.
- Additive `display_name`: add it as `string? DisplayName` (default `null`/empty) per the adoption-ledger note in the E02 epic and level-data-format §3 additive-field policy — no bump. This is the concrete demonstration of TR-ldf-004; document in the field's doc-comment that it was added at v1.1 with no schema bump.
- The unrecognized-optional-field *tolerance* is a reader behaviour (§3 row 1). The pure `ToDomain`/parse maps known fields, defaults absent optionals, and surfaces unknown-field warnings (return them, do not `Debug.Log` — Domain is engine-free).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 006: the V1–V19 scalar/structural `Validate()` rules.
- Story 007: the V8 connectivity flood-fill.
- Story 012: the LevelManifest ordinal resolution keyed off `level_id`.
- E06: the `LevelDataAsset` ScriptableObject wrapper + `ToDomain()` at the Game layer, Addressables load, and the Inspector authoring workflow.

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: full-field typed POCO (from level-data-format AC row 1).
  - Given: a fixture with every required field populated.
  - When: parsed into `LevelData`.
  - Then: every field is present with the specified type.

- **AC-2**: optional-field defaults (from AC "omits every optional field").
  - Given: a fixture omitting `cell_mask`, `pre_placed_pieces`, `rng_seed`.
  - When: parsed.
  - Then: `cell_mask` = full rectangle; `pre_placed_pieces` = `[]`; `rng_seed` = `-1`; no error raised.

- **AC-3**: additive display_name, no bump (TR-ldf-004).
  - Given: a v1 fixture with `display_name` set, and one without it, and one with an unrelated unknown optional field.
  - When: each is parsed.
  - Then: `display_name` round-trips when present, defaults when absent; the unknown optional field loads with a warning, never a rejection; `schema_version` stays `1`.

- **AC-4**: closed-enum shape.
  - Given: fixtures using `score_target` and `collect_color`.
  - When: parsed.
  - Then: both objective types map to their typed records with correct params; objective list order is preserved.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/level-data-format/leveldata_schema_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Levels/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **E01 Story 002** (Domain + `Domain.Tests` asmdefs; `Assets/Domain/Levels/` tree). Soft: **E01 Story 005** (Edit-Mode test conventions).
- Unlocks: Story 006 (validation operates on this POCO), Story 007 (V8 reads `cell_mask`), Story 013 (cross-validator reads `level_id`/`region` from `LevelIdentity` derived from this schema).
