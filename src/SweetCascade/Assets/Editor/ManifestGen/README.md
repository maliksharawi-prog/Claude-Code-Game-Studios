# Editor/ManifestGen — placement note

Per ADR-006 (`docs/architecture/adr-006-level-manifest.md`), this folder is
the permanent home of `LevelManifestGenerator`, the **only** writer of
`assets/data/level_manifest.asset`.

## What's here now (E01-006, scaffold only)

- `LevelManifestGenerator.cs` — an inert stub. `Reconcile(bool apply)` always
  throws `NotImplementedException`; the `Sweet Cascade ▸ Level Manifest ▸
  Regenerate` menu item catches it and logs the same "not yet implemented"
  notice, writing nothing. No `AssetDatabase` write, no
  `IPreprocessBuildWithReport` hook, and no call into
  `ManifestCrossValidator` exist yet.

## What lands here later (E02/E03, per ADR-006)

- The real `Reconcile` implementation: scan `assets/data/levels/**`, enforce
  the prefix-invariant (existing ordinals never move), append new
  `level_id`s in lexicographic order, and tombstone removed ones into
  `retired_ordinals` — never delete or renumber an existing slot.
- The `IPreprocessBuildWithReport` verify hook (dry-run diff; fails the build
  on a stale manifest, never silently mutates).
- Wiring to the pure `ManifestCrossValidator` (lives in
  `SweetCascade.Domain`, `Levels/` — not here) for the W6/W7/W8 + bijection
  checks, called identically by this Editor tool and the Edit Mode/CI test.

## Ownership boundaries (do not violate when implementing the real generator)

- The two manifests (`level_manifest.asset`, `world_map_manifest.asset`) are
  loaded at boot via a **direct serialized reference**, never Addressables
  (ADR-006 + ADR-002 as amended 2026-07-18). Do not create an Addressables
  group for either manifest.
- `level_manifest.asset` (generated) and `world_map_manifest.asset`
  (hand-authored) are two separate assets — never merge them into one
  ScriptableObject/file (ADR-006 Decision, part 1).
- `LevelManifestGenerator.Reconcile(apply: true)` must remain the *only*
  writer of `level_manifest.asset` — no other code path, menu item, or hook
  may write it.

See also `Assets/Editor/LevelValidation/` and `Assets/Editor/LevelPreview/`
(reserved, empty at E01) and
`production/epics/project-scaffold-ci/story-006-editor-tooling-manifest-stub.md`.
