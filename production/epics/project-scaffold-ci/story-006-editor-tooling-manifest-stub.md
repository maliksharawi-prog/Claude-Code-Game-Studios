# Story 006: Editor tooling folder layout + manifest-generator stub placement

> **Epic**: Project Scaffold & CI Activation (E01)
> **Status**: In Progress — file-based scaffold complete + verified 2026-07-18; awaiting human Unity editor pass (.meta/lock/URP-asset items) per qa-plan-sprint-01. See production/sprint-status.yaml.
> **Layer**: Foundation (infrastructure / editor tooling scaffold)
> **Type**: Config/Data
> **Estimate**: 1 day
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: — (governed by `docs/architecture/architecture.md` §5.3 folder layout + ADR-006)
**Requirement**: `TR-perf-002` (this story is pure scaffold for the Editor assembly; the *functional* manifest requirements **TR-be-004** (ordinal resolution) and **TR-wm-002** (W6–W7 cross-validation) are owned by **E02/E03** and are explicitly NOT implemented here.)
*(Read the current text fresh from `docs/architecture/tr-registry.yaml`.)*

**ADR Governing Implementation**: ADR-006: Level Manifest Generation & Cross-Validation (primary — §5.3 places the generator in the `Editor` assembly; the menu path and run-points are defined there)
**ADR Decision Summary**: An `Editor`-assembly tool (`LevelManifestGenerator.Reconcile`) is the *only* writer of the generated `level_manifest.asset`, invoked from `Sweet Cascade ▸ Level Manifest ▸ Regenerate`; the pure cross-validator lives in Domain. This story only reserves the folder home + a compile-safe stub — no generation or validation logic.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW–MEDIUM (`AssetDatabase`, `IPreprocessBuildWithReport` are stable; the only nuance is the deliberate decision that the manifests are NOT Addressable)
**Engine Notes** (ADR-006): the boot-time manifest load uses a **direct serialized reference**, NOT `Addressables.LoadAssetAsync` (which throws on failure in 6.2+). No Addressables group is authored for the manifests. `AssetDatabase.FindAssets` is used only as an unordered set source when the real generator lands.

**Control Manifest Rules (Editor & CI layer)**:
- Required (when implemented in E02/E03): `LevelManifestGenerator.Reconcile(apply: true)` is the only writer of `level_manifest.asset`, from the `Sweet Cascade ▸ Level Manifest ▸ Regenerate` menu item; `ManifestCrossValidator.Validate` is one pure function called identically by the Editor tool and the Edit Mode/CI test.
- Forbidden: hand-editing `level_manifest.asset`; merging the ordinal manifest with the hand-authored world-map manifest; writing the manifest from anywhere other than the generator; wiring the manifests into Addressables (they are direct serialized references — ADR-002 as amended + ADR-006).

---

## Acceptance Criteria

*From `architecture.md` §5.3 and ADR-006 §5.3/§Decision, scoped to this story (scaffold only):*

- [ ] The `Editor/` sub-folder layout from `architecture.md` §5.3 exists under the `SweetCascade.Editor` asmdef: `Assets/Editor/ManifestGen/`, `Assets/Editor/LevelValidation/`, `Assets/Editor/LevelPreview/` (empty/`.gitkeep` where no stub is placed).
- [ ] A **compile-safe stub** `Assets/Editor/ManifestGen/LevelManifestGenerator.cs` exists that reserves the API surface without implementing logic: a class with a `Reconcile(bool apply)` signature (or menu-item skeleton for `Sweet Cascade ▸ Level Manifest ▸ Regenerate`) that throws `NotImplementedException` / logs "not yet implemented — see ADR-006, owned by E02/E03" and carries a doc-comment header pointing to ADR-006.
- [ ] The stub compiles in the Editor assembly and does not write any asset, register any build hook, or run any validation — it is inert scaffold.
- [ ] No Addressables group is created for `level_manifest.asset` / `world_map_manifest.asset`; the stub's comments state the direct-serialized-reference decision (ADR-006 + ADR-002 amended).
- [ ] The stub references no logic that belongs to Domain (the pure `ManifestCrossValidator` is a Domain type authored in E02/E03, not here).

---

## Implementation Notes

*Derived from ADR-006 §5.3 (folder home), §Decision parts 2–5 (ownership boundaries this stub must respect), and `architecture.md` §5.3:*

**File-based (no editor GUI required):**
- Create the three `Editor/` sub-folders with `.gitkeep`.
- Author `LevelManifestGenerator.cs` as an inert stub: a doc-comment header citing ADR-006, a `public static class LevelManifestGenerator` with a `Reconcile(bool apply)` method body that throws `NotImplementedException("LevelManifestGenerator is scaffolded in E01; generation logic is delivered in E02/E03 per ADR-006.")`, and (optionally) a `[MenuItem("Sweet Cascade/Level Manifest/Regenerate")]` wrapper that surfaces the same "not yet implemented" notice. Keep it in the Editor asmdef only.
- Do NOT add an `IPreprocessBuildWithReport` hook, an `AssetDatabase` write, or any `Validate` call — those are functional behaviours owned by E02/E03. A live pre-build hook here would fail builds against an asset that does not yet exist.

**Manual editor-checklist items (require the Unity 6.3 editor):**
- [ ] Open the project; confirm the stub compiles in the Editor assembly and the `Sweet Cascade ▸ Level Manifest ▸ Regenerate` menu item (if included) appears and shows the "not yet implemented" notice without side effects. Commit generated `.meta`.

---

## Out of Scope

*Handled by later epics — do not implement here:*

- **E02/E03**: the real `LevelManifestGenerator.Reconcile` (scan/append/tombstone), the `ManifestCrossValidator` pure Domain function, the `IPreprocessBuildWithReport` stale-manifest hook, and the Editor-menu + CI validation gate (ADR-006 §Decision 2–5, §5).
- **E02**: `level_manifest.asset` generation and `LevelManifest`/`LevelIdentity` Domain POCOs.
- **E10**: authoring the 10 MVP levels the generator will eventually scan.
- Any Addressables wiring — the manifests are never Addressable.

---

## QA Test Cases

*Authored at story creation (lean mode). This is a scaffold-only story — a smoke check that the stub compiles and is inert.*

**Manual check — AC: Editor folders + inert stub present and compiling**
- Setup: open the project after the files are added.
- Verify: `Editor/ManifestGen`, `Editor/LevelValidation`, `Editor/LevelPreview` exist; `LevelManifestGenerator.cs` compiles in the Editor assembly; invoking the stub throws/logs "not yet implemented" and writes no asset.
- Pass condition: the Editor assembly compiles green, the stub has zero side effects, and no Addressables group exists for the manifests.

**Manual check — AC: ownership boundaries respected**
- Setup: read the stub.
- Verify: no generation/validation logic, no build hook, no `AssetDatabase` write; a doc-comment cites ADR-006 and names E02/E03 as the owner of the real implementation.
- Pass condition: the stub is unmistakably scaffold, not a partial implementation.

---

## Test Evidence

**Story Type**: Config/Data
**Required evidence**: Smoke check pass — `production/qa/smoke-[date].md` (or a note in the sprint smoke) recording that the Editor assembly compiles with the inert stub and the folder layout matches §5.3. No automated test (there is no logic to assert).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (the `SweetCascade.Editor.asmdef` the stub compiles into).
- Unlocks: None within E01 — provides the reserved Editor home that E02/E03 build the real manifest generator into.
