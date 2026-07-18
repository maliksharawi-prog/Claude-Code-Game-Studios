# Story 014: LevelManifestGenerator editor tool — append-only, tombstone, prefix-guard, CI verify

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Integration
> **Estimate**: 3 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/world-map.md` (§2 append-only manifest), `design/gdd/board-engine.md` §2 (ordinal never reused/renumbered)
**Requirement**: `TR-wm-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-006: Level Manifest Generation & Cross-Validation
**ADR Decision Summary**: An `Editor`-assembly tool scans `assets/data/levels/**` for `LevelDataAsset`s, reconciles against the committed manifest, **freezes existing entries at their index**, sorts new `level_id`s lexicographically and **appends** them, and **tombstones** removed ones via `retired_ordinals`. A **prefix-invariant guard** aborts (writing nothing) if any existing slot changed. `Reconcile(apply:true)` is the only writer (menu Regenerate); a pre-build `IPreprocessBuildWithReport` hook + CI run `Reconcile(apply:false)` (dry-run diff) and the full cross-validator, failing the build on staleness or violations.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW–MEDIUM — this is the **one non-pure story** in E02: it lives in `SweetCascade.Editor` and uses `AssetDatabase.FindAssets`/`LoadAssetAtPath`/`SaveAssets` and `IPreprocessBuildWithReport` (all stable across 2022→6.3, pre-cutoff-reliable).
**Engine Notes**: `AssetDatabase.FindAssets("t:LevelDataAsset")` enumeration order is **not** OS-stable — use it only as an unordered *set* source; determinism comes from our own lexicographic sort. Confirm the boot-time manifest load is a **direct serialized reference**, not `Addressables.LoadAssetAsync` (which throws on failure in 6.2+).

**Control Manifest Rules (Editor & CI layer)**:
- Required: `LevelManifestGenerator.Reconcile(apply:true)` is the ONLY writer of `level_manifest.asset`, invoked only from the `Sweet Cascade ▸ Level Manifest ▸ Regenerate` menu item.
- Required: before writing, the generator asserts the new `entries` list has the committed list as an exact **prefix**; any changed/missing existing `level_id` aborts with an error and writes nothing.
- Required: new `level_id`s are sorted lexicographically ascending (`StringComparer.Ordinal`) before append; `FindAssets` is used only as an unordered set source.
- Required: a pre-build hook regenerates into memory and diffs against the committed asset, failing the build if they differ; the same dry-run diff + the full `ManifestCrossValidator` (Bijection, W6, W7, W8) runs headless in CI as a blocking gate; the generator is idempotent (no-change Regenerate → `Changed == false`, byte-identical asset).
- Forbidden: writing `level_manifest.asset` from anywhere other than `Reconcile(apply:true)`; letting the pre-build hook silently mutate; merging the ordinal generator with the hand-authored world-map manifest tool.

---

## Acceptance Criteria

*From ADR-006 Decision §2–§5, Validation Criteria, and world-map/board-engine §2, scoped to this story:*

- [ ] `Reconcile(apply:true)` scans `assets/data/levels/**` for `LevelDataAsset`s, freezes existing `entries` at their index, sorts new `level_id`s lexicographically ascending, appends them, and writes `level_manifest.asset` (the ONLY write path; menu `Sweet Cascade ▸ Level Manifest ▸ Regenerate`).
- [ ] Idempotency: a no-change Regenerate yields `Changed == false` and a byte-identical asset.
- [ ] Adding a level appends exactly one new ordinal; **no** existing ordinal changes (prefix-invariant holds).
- [ ] Deleting a level file adds its ordinal to `retired_ordinals`, leaves `entries` untouched, and never reuses the slot.
- [ ] A hand-edit that reorders/removes an `entries` slot makes `Reconcile` **abort with an error and write nothing** (prefix-invariant breach).
- [ ] The pre-build `IPreprocessBuildWithReport` hook regenerates into memory, diffs against the committed asset, and **fails the build** on any staleness ("manifest stale — run Regenerate and commit") — it never mutates during build.
- [ ] The same dry-run diff plus the full `ManifestCrossValidator.Validate` (Bijection/W6/W7/W8, Story 013) runs headless in CI as a **blocking** gate; a deliberately stale committed manifest fails it.
- [ ] The boot manifest load is a direct serialized reference (not Addressables) — confirmed, not implemented here (E06 wires the BootLoader reference).

---

## Implementation Notes

*Derived from ADR-006 Decision §2–§5, Implementation Guidelines, and Migration Plan:*

- Place `LevelManifestGenerator.cs`, the menu items, and the pre-build hook under `Assets/Editor/ManifestGen/` in `SweetCascade.Editor` (Editor-platform-only asmdef; never shipped in a build) — into the folder/stub layout established by **E01 Story 006**.
- `Reconcile(bool apply)` returns `ManifestGenResult(LevelManifest, bool Changed, IReadOnlyList<ManifestViolation>)`. `apply:false` is the dry-run used by the pre-build hook and CI verify; `apply:true` writes via `AssetDatabase.SaveAssets`.
- Prefix-invariant: before writing, assert the committed `entries` is an exact prefix of the new list (same `level_id` at every existing index). On breach, throw/abort and write nothing — renumbering is structurally unreachable through the tool.
- Tombstoning: a `level_id` in the committed manifest but absent from the scan adds its ordinal to `retired_ordinals`; `entries` is never compacted.
- Wire the CI cross-validation as an **Edit Mode test** so it runs under the existing `game-ci/unity-test-runner` gate (do not invent a separate CI job) — it calls the same `ManifestCrossValidator.Validate` (Story 013) the menu `Validate` item calls.
- Exercise ADR-006 Migration Plan step 4 (tombstoning) once on a throwaway level as the acceptance walkthrough: add → regenerate (append) → delete → regenerate (tombstone) → confirm the ordinal is retained in `entries`, added to `retired_ordinals`, and that leaving it in a `level_sequence` fails W6.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 012: the `LevelManifest`/`WorldMapManifest` Domain POCOs + `ResolveOrdinal` this tool produces/reads.
- Story 013: the `ManifestCrossValidator.Validate` rules (this tool invokes them, does not reimplement them).
- E06: the `LevelManifestAsset`/`WorldMapManifestAsset` ScriptableObject wrappers + `ToDomain()` and the BootLoader direct-reference load.
- The hand-authored `world_map_manifest.asset` content (World Map's, not this generator's — never merged).
- The full IL2CPP/WebGL build matrix (ADR-J) beyond registering the pre-build hook.

---

## QA Test Cases

*Authored at story creation (lean mode). This is an Editor-tool Integration story: automated Edit-Mode tests for the pure reconcile/diff logic, plus a documented editor walkthrough for the menu/asset write.*

- **AC-1 (automated)**: idempotency + append (`Changed`/prefix).
  - Given: a committed manifest and the same level set.
  - When: `Reconcile(apply:false)` runs; then a new level is added and it runs again.
  - Then: first run `Changed == false` (byte-identical); after add, exactly one new ordinal appended, all existing ordinals unchanged.

- **AC-2 (automated)**: prefix-invariant abort.
  - Given: a manifest whose committed `entries` has been hand-reordered/removed at an existing slot.
  - When: `Reconcile` runs.
  - Then: it aborts with an error and writes nothing.

- **AC-3 (automated)**: tombstone + stale-fail.
  - Given: a level file deleted from the scan; and separately, a deliberately stale committed manifest.
  - When: `Reconcile`/the CI dry-run diff runs.
  - Then: the deleted level's ordinal is added to `retired_ordinals` (entries untouched); the stale manifest fails the pre-build/CI verify.

- **AC-4 (manual walkthrough)**: menu Regenerate + boot-load path.
  - Setup: `Sweet Cascade ▸ Level Manifest ▸ Regenerate` against the 10 MVP levels.
  - Verify: `candy_kingdom_hub-001…010` append in lexicographic order (ordinals 1–10), `retired_ordinals = []`; the committed diff is human-reviewable; the manifest loads via a direct serialized reference (not Addressables).
  - Pass condition: a re-run produces no diff; the cross-validator reports zero blocking failures.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/world-map/manifest_generator_reconcile_test.cs` (Edit-Mode reconcile/diff/tombstone/prefix logic) — must exist and pass — PLUS a documented editor walkthrough at `production/qa/evidence/story-014-manifest-generator-evidence.md` (menu Regenerate, the human-reviewable append diff, the deliberately-stale build-fail). In-project: `src/SweetCascade/Assets/Editor/ManifestGen/` + `Assets/Tests/EditMode/Levels/`.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 012** (manifest POCOs), **Story 013** (the cross-validator the tool invokes), **E01 Story 006** (the `Assets/Editor/ManifestGen/` folder + manifest-generator stub placement), **E01 Story 002** (Editor asmdef references Domain). Soft: **E01 Story 004** (game-ci gate for the blocking CI verify).
- Unlocks: E10 (content — generating the real 10-level MVP manifest + world-map cross-validation), E03 Board Engine (a valid ordinal manifest to `ResolveOrdinal` against at bootstrap).
