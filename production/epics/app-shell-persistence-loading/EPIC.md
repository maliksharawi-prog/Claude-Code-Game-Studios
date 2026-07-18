# Epic: App Shell — Boot, Persistence IO & Content Loading

> **Epic ID**: E06
> **Layer**: Foundation (Game-side)
> **GDD**: `design/gdd/save-persistence.md` (IO/API) · `design/gdd/level-data-format.md` (SO wrapper + loading)
> **Architecture Module**: `SweetCascade.Game` — BootLoader (cold-start sequence, Addressables init), Addressables loader (throw-wrapped `IContentLoader`), LevelDataAsset ScriptableObject (`ToDomain()`), SaveService (two on-disk slot files, atomic A/B write ladder, dirty flag, lifecycle flush)
> **Status**: Ready
> **Stories**: 5 stories created (2026-07-18) — see [## Stories](#stories)

## Scope

The Game-assembly shell that boots the app, loads content, and durably persists progress —
wrapping the E05 runtime. Delivers: the **BootLoader** cold-start sequence (Addressables init →
`LoadProfile` → settings drive audio/juice → RngService construct → manifests via **direct
serialized reference** → ScreenFlow to WORLD_MAP, with a one-shot `recovery_notice`); the
**Addressables loader** (`IContentLoader` wrapping every `LoadAssetAsync` in a `TryLoad`
discriminated result, since 6.2+ throws on failure); the **LevelDataAsset** ScriptableObject
(Inspector-editable, one file per level in region subdirectories, `ToDomain()` to the pure
schema); and the **SaveService** — the atomic A/B double-buffer write ladder at
`persistentDataPath` (no rename dependency), `RecordLevelCompletion` (WIN-only, persist before
`LevelResolved`), debounced `UpdateSetting`, and defensive `FlushIfDirty` on
`OnApplicationPause/Focus`. Implements the `ISaveService` / `IContentLoader` interfaces E05
consumes.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-002: Addressables Grouping & Load Strategy | Group layout, `TryLoad` throw-wrapping, FATAL boot-error classification; **manifests load by direct reference** (amended 2026-07-18, CONFLICT-1 reconciled) | HIGH |
| ADR-003: Save Serialization & Atomic Durability | `persistentDataPath` A/B write ladder, `FileStream.Flush(true)`, WebGL IDBFS sync, dirty/debounce/flush timing | MEDIUM |
| ADR-006: Level Manifest | The two manifest SOs loaded by direct serialized reference on the BootLoader (not Addressable) | LOW |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-ldf-002 | Designer Inspector-editable, one file per level, region subdirectories (LevelDataAsset SO + loading) | ADR-002 + arch §6 ✅ |
| TR-sp-002 | Atomic A/B double-buffer (no rename dependency); corruption ladder; tamper detect+accept (**file IO**) | ADR-003 ✅ |
| TR-sp-004 | `record_level_completion` / `load_profile` / `get_profile` / `update_setting` / `flush_if_dirty` API (SaveService) | ADR-003 ✅ |

## Depends On

- **E01** (assemblies + Addressables package + CI).
- **E02** (Save codec `Serialize/Deserialize/Merge/Checksum` + slot-selection logic that SaveService writes to disk; LevelData schema for `ToDomain()`; manifest asset the BootLoader direct-references).
- **E05** (the ScreenFlowController + BoardModel runtime the BootLoader initializes and hands control to; E06 implements the `ISaveService`/`IContentLoader` interfaces E05 declares).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **HIGH — Addressables 6.2+ throws on load failure** (does not return `null`). Every load
  MUST be wrapped (`TryLoad` / try-catch + `.Status` belt-and-suspenders); reference-counted
  `Release` is required (verified `plugins/addressables.md`).
- **MEDIUM — Persistence / File IO.** `Application.persistentDataPath` replaces Godot's
  `user://`; **no atomic-rename guarantee on WebGL** (IndexedDB-backed virtual FS) — the A/B
  double-buffer already avoids depending on one. `FileStream.Flush(true)` / WebGL IDBFS sync is
  a Verification-Required durability primitive (ADR-003), not assumed.
- **CONFLICT-1 status: RESOLVED.** ADR-002 was amended 2026-07-18 to load the two manifests by
  direct serialized reference per ADR-006 (engine-reference favors it: "DON'T use Addressables
  for startup-immediate assets"); A3 scoped to non-retired entries (CONFLICT-2). The BootLoader
  manifest-load slice is **unblocked** — a single load mechanism is now specified.
- `OnApplicationPause(bool)` / `OnApplicationFocus(bool)` drive `FlushIfDirty` (arch §2 residue map).

## Stories

| # | Story | Type | Status | ADR | TRs |
|---|-------|------|--------|-----|-----|
| 001 | IContentLoader TryLoad choke point (Addressables wrap) | Integration | Ready | ADR-002 | ldf-002 (loading) |
| 002 | LevelDataAsset ScriptableObject + ToDomain() | Integration | Ready | ADR-002 (+ADR-006) | ldf-002 (SO/mapping) |
| 003 | SaveService atomic A/B durable write (ISaveStore) | Integration | Ready | ADR-003 | sp-002 |
| 004 | SaveService API surface & lifecycle triggers | Integration | Ready | ADR-003 (+ADR-005 D4) | sp-004 |
| 005 | BootLoader cold-start sequence + FATAL boot-error + recovery notice | Integration | Ready | ADR-002 + ADR-003 + ADR-006 | ldf-002, sp-004 (boot) |

*Manifest Version embedded in every story: 2026-07-18. TR coverage: ldf-002, sp-002, sp-004 — all 3 owned TRs allocated. Work stories in ascending order; each story's `Depends on:` field lists prerequisites (intra-epic + E01, E02, E05).*

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- BootLoader completes the arch §7.4 init order (Addressables → LoadProfile → settings →
  RNG → direct-reference manifests → ScreenFlow), shows a FATAL boot-error screen on a core
  load failure, and surfaces `recovery_notice` once.
- The Addressables loader converts a forced load failure into a handled discriminated result
  (no unhandled throw) and releases by refcount; LevelDataAsset round-trips through `ToDomain()`.
- SaveService performs an atomic A/B durable write, survives a simulated mid-write corruption
  via the ladder, orders WIN persistence before `LevelResolved`, and flushes on pause — proven
  by Play Mode round-trip tests.
- save-persistence.md (IO/API) and level-data-format.md (SO/loading) acceptance criteria are met.

## Next Step

Run `/create-stories app-shell-persistence-loading`.
