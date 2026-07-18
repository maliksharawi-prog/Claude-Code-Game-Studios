# Story 013: ManifestCrossValidator — bijection + W6 + W7 + W8 (pure, shared by tool & test)

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/world-map.md` (§7 Validation Contract W5–W8, Acceptance Criteria), `design/gdd/level-data-format.md` (W8 `region` registry)
**Requirement**: `TR-wm-002`, `TR-ldf-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-006: Level Manifest Generation & Cross-Validation
**ADR Decision Summary**: All reconciliation logic lives in a **single pure `ManifestCrossValidator.Validate`** taking the ordinal `LevelManifest`, the hand-authored `WorldMapManifest`, and the scanned `LevelIdentity` set, returning a violation list. It is called **identically** by the Editor tool (Story 014) and the Edit-Mode/CI test — never duplicated. Tombstoning makes W7 compatible with append-only: retired entries are legitimately invisible to the map.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure C#, `noEngineReferences`)
**Engine Notes**: None — the validator is pure Domain set logic. It composes with Board Engine's bijection test and the world-map W-rules; it reads only static POCOs, never runtime state.

**Control Manifest Rules (Domain layer)**:
- Required: `ManifestCrossValidator.Validate` is a single pure function called identically by the Editor tool and the Edit-Mode/CI test — never duplicated logic between them.
- Required: the validator returns violations as data (`ManifestViolation(Rule, Detail)`); `Rule ∈ {Bijection, W6, W7, W8, Prefix, Tombstone, Duplicate}`.
- Required: retired entries are treated specially so W7 stays a hard check without contradicting append-only.
- Guardrail: O(n≤120) set reconciliation, off the runtime path.

---

## Acceptance Criteria

*From GDD `world-map.md` §7 (W5–W8) / ADR-006 Decision §5 & Validation Criteria, scoped to this story:*

- [ ] `Validate(ordinal, worldMap, levelFiles)` returns a `ManifestViolation` list; an empty list means the three-way (ordinal ↔ map ↔ files) consistency holds.
- [ ] **Bijection**: every `level_id` under the scanned level files equals exactly one **non-retired** `entries` slot; no duplicates.
- [ ] **W6**: every `level_id` in any region's `level_sequence` is a **non-retired** `entries` member (a retired level left in a sequence fails W6).
- [ ] **W7**: every **non-retired** `entries` member appears in exactly one `level_sequence`; **retired** entries must appear in **none**.
- [ ] **W8**: each scanned level file's `region` equals the `region_code` of the region whose `level_sequence` lists that `level_id`.
- [ ] `test_w6_orphaned_level_sequence_entry_rejected`: a `level_id` in a `level_sequence` but absent from `entries` fails W6.
- [ ] `test_w7_orphaned_board_engine_entry_rejected`: a non-retired `entries` member absent from every `level_sequence` fails W7.
- [ ] `test_w8_region_field_mismatch_rejected`: a level file's `region` differing from its assigned region's `region_code` fails W8.
- [ ] A retired ordinal left in a `level_sequence` fails W6; a non-retired entry in no `level_sequence` fails W7 (both fail-closed).

---

## Implementation Notes

*Derived from ADR-006 Decision §5 (W6–W7 cross-validation + tombstoning) and Key Interfaces:*

- Place `ManifestCrossValidator.cs` under `Assets/Domain/Levels/` as `public static class ManifestCrossValidator { static IReadOnlyList<ManifestViolation> Validate(LevelManifest, WorldMapManifest, IReadOnlyCollection<LevelIdentity>) }`.
- Compute the non-retired entry set once (filter `Entries` by `!IsRetired(ordinal)`); every rule keys off it. Retired entries are excluded from Bijection/W6/W7's "must appear" checks and are explicitly forbidden from any `level_sequence`.
- W8 joins each `LevelIdentity.Region` to the `region_code` of the region whose `level_sequence` contains that `level_id` — this is the formal `region` registry check level-data-format's Open Question deferred to world-map (W8), realized here.
- This is the exact same function the Editor tool (Story 014) invokes and the Edit-Mode/CI test (`tests/unit/world-map/`) runs — one source of truth for the rules. The `Prefix`/`Tombstone`/`Duplicate` violation kinds are surfaced by the generator (Story 014); this validator owns Bijection/W6/W7/W8.
- Fixtures under `Assets/Tests/EditMode/Levels/Fixtures/`: an orphaned-sequence-entry manifest, an orphaned-ordinal manifest, a region-mismatch pair, and a clean MVP (1 region / 10 levels) manifest.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 012: the `LevelManifest`/`WorldMapManifest`/`LevelIdentity` POCOs this validator consumes.
- Story 014: the Editor generator — the append-only prefix-invariant guard, deterministic append, tombstoning writes, `Reconcile`, pre-build hook, and CI verify wiring.
- E07: the world-map runtime unlock/display formulas (Formulas 1–6).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: clean manifest passes (`test_real_manifest_zero_blocking_failures` analogue).
  - Given: a clean MVP fixture (1 region, 10 levels; ordinal ↔ map ↔ files consistent).
  - When: `Validate` runs.
  - Then: an empty violation list.

- **AC-2**: W6 orphan (`test_w6_orphaned_level_sequence_entry_rejected`).
  - Given: a `level_sequence` referencing a `level_id` absent from `entries` (and separately, a retired one).
  - When: `Validate` runs.
  - Then: a W6 violation in both cases.

- **AC-3**: W7 orphan (`test_w7_orphaned_board_engine_entry_rejected`).
  - Given: a non-retired `entries` member in no `level_sequence`.
  - When: `Validate` runs.
  - Then: a W7 violation; a retired entry absent from all sequences is legitimately allowed (no violation).

- **AC-4**: W8 mismatch + bijection (`test_w8_region_field_mismatch_rejected`).
  - Given: a level file whose `region` ≠ its assigned region's `region_code`; and a duplicate `level_id` across the scanned files.
  - When: `Validate` runs.
  - Then: a W8 violation and a Bijection/Duplicate violation respectively.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/world-map/manifest_cross_validator_test.cs` — must exist and pass (the BLOCKING gate per world-map.md ACs). In-project: `src/SweetCascade/Assets/Tests/EditMode/Levels/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 012** (the manifest POCOs + `LevelIdentity`), **Story 005** (the `LevelData.region` field W8 checks). Transitively **E01 Story 002**.
- Unlocks: Story 014 (the Editor tool calls this exact validator), E07 World Map view, E10 (the real MVP manifests must pass this gate).
