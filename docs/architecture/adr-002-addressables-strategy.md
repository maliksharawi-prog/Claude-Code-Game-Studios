# ADR-002: Addressables Grouping & Load Strategy

## Status

Accepted

> Accepted 2026-07-18 (Technical Director). Consistent with the master architecture's
> deferred sign-off pattern: **founder review may amend** the local-vs-remote split and
> the resident-set memory sub-budget. This ADR is the concrete realization of the master
> architecture's Required-ADR **"ADR-A — Addressables grouping & content-load strategy"**
> (`docs/architecture/architecture.md` §11), renumbered ADR-002 in this repository.

## Date

2026-07-18

## Last Verified

2026-07-18

## Decision Makers

Technical Director (author); Founder (amend rights on local/remote split and memory
sub-budget); consulted: `unity-addressables-specialist`, `performance-analyst`.

## Summary

Sweet Cascade needs a content-load architecture that keeps memory flat as the catalog
grows from the MVP's 1 region / 10 levels to launch's 4 regions / 120 levels, handles
Unity 6.2+ Addressables' throw-on-failure loads without crashing gameplay, and reserves
a clean remote path for Phase 3 events without paying its cost now. This ADR defines a
**seven-group layout** (Core, Shared board rig, per-region level-data / theme / music
groups, one reserved remote group), an **all-local MVP** delivery model, a
**boot / region-enter / level-enter load-and-release lifecycle** whose resident set is
always "Core + rig + exactly one region," a **single `IContentLoader.TryLoad` choke
point** that converts every throwing load into a discriminated result with per-content
fallback, and **editor-time + CI validation hooks** that make catalog integrity a
blocking gate.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.x) — per ADR-001 |
| **Domain** | Core (asset / content management, boot sequencing) |
| **Knowledge Risk** | HIGH — Addressables 2.x (Unity 6.2+) throw-on-failure semantics are post-cutoff; LLM training covers ~Addressables 1.x return-null behavior |
| **References Consulted** | `docs/engine-reference/unity/plugins/addressables.md`, `docs/engine-reference/unity/VERSION.md`, `docs/architecture/architecture.md` §5–§9, §11 |
| **Post-Cutoff APIs Used** | `Addressables.InitializeAsync`, `Addressables.LoadAssetAsync<T>` / `LoadAssetsAsync<T>` (throw-on-failure in 6.2+), `AsyncOperationHandle<T>` + `.Status` + `.OperationException`, `Addressables.Release` / `ReleaseInstance`, `DownloadDependenciesAsync` / `GetDownloadSizeAsync` (Phase 3 remote only), the Addressables "Check Duplicate Bundle Dependencies" analyze rule |
| **Verification Required** | (1) Play Mode test: load an invalid address on the pinned 6.3 build and assert `TryLoad` returns `Ok == false` with **no exception escaping** the loader. (2) Confirm ASTC-compressed candy atlas memory footprint on a 2022-era mid-range Android against the resident-set sub-budget. (3) Confirm `AsyncOperationHandle` release timing does not stall the reveal frame. |

> **Note**: Knowledge Risk is HIGH. If the project upgrades off Unity 6.3 LTS, this ADR
> must be re-validated (Addressables throw-vs-null semantics have already shifted once
> between 1.x and 2.x) and marked Superseded if the load contract changes.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-001 (Engine Selection — Unity 6.3 LTS, Accepted) — the entire Addressables surface is Unity-specific |
| **Enables** | The level-load slice, the `BootLoader` cold-start sequence (architecture §6, §7.4), World Map region content loading, and the Phase 3 Events/Theming Engine remote-delivery path |
| **Blocks** | Board Engine bootstrap slice and BootLoader implementation cannot start until this ADR is Accepted (both consume the `IContentLoader` contract and the address==`level_id` invariant) |
| **Ordering Note** | Composes with the future **ADR-E (Level manifest generation & W6–W7 cross-validation)**: the editor-time checks here extend W6/W7 into the Addressables catalog (the address==`level_id` invariant). Independent of **ADR-B (Save serialization)** — save data lives on `persistentDataPath` and is deliberately **not** Addressable. Touches **ADR-G (Audio)** via the per-region music group and **ADR-H (Particles)** via the shared candy/particle atlas in the board-rig group. |

## Context

### Problem Statement

Every playable level is a `LevelData` ScriptableObject on disk
(`assets/data/levels/<region_code>/*.asset`), one file per level, growing from 10 files
(1 region) at MVP to ~120 files (4 regions) at launch (`level-data-format.md` §1;
`world-map.md` Overview). Region theming (gradient, trim, prop set, ambient particles,
map diorama) is a per-region art payload that is the literal backbone of Pillar 3
(`world-map.md` §4). We must decide **how this content is grouped into bundles, whether
it ships in the binary or over a network, and exactly when each piece loads and
releases** — before the BootLoader or the level-load slice can be written
(architecture §11 lists this as a "must have before coding starts" Foundation ADR).

The decision is forced now because two downstream slices are blocked on it (BootLoader,
Board Engine bootstrap), and because getting the resident-set discipline wrong is
expensive to reverse: if per-level or per-region content is loaded without a release
policy, memory grows with catalog size and silently breaks the ≤400MB budget somewhere
between MVP and launch — a failure that would not surface until Alpha content exists.

### Current State

No content-load code exists. The master architecture names a `BootLoader` that does
"Addressables init" (§6), a boot sequence that loads the `LevelManifest` and then a
`LevelDataAsset` "via Addressables (try/catch — 6.2+ throws)" (§7.4 steps 2, 6, 8), and
a memory note that "LevelData and region assets load on demand via Addressables and
release by refcount, so only the active region is resident" (§9.4) — but the concrete
group layout, the load/release triggers, the failure contract, and the validation hooks
are unspecified. This ADR specifies them.

### Constraints

- **Engine (HIGH risk):** Unity 6.2+ Addressables **throw on load failure** (invalid
  key, missing dependency, corrupt bundle, remote fetch failure) instead of returning
  `null` or a quietly-inspectable failed handle. Every load must be wrapped
  (`plugins/addressables.md`; VERSION.md; architecture §1, §6).
- **Engine guidance:** the Addressables reference explicitly says *don't* use
  Addressables for "assets needed immediately at startup" — pushing boot-critical shared
  content toward a load-once-and-pin strategy rather than per-use async loads.
- **Assembly boundary (compiler-enforced):** `SweetCascade.Domain` has **zero**
  `UnityEngine` references (architecture §5, Principle 1). `AsyncOperationHandle`,
  `Addressables`, and every content type must live in `SweetCascade.Game`; the Domain
  receives only materialized POCO data (`LevelData.ToDomain()`).
- **Platform:** mobile-primary (iOS/Android), Web secondary. WebGL has no synchronous
  file access and a bundle-download-on-demand model — the loader must be async-only.
- **Memory:** ≤400MB on 2022-era mid-range Android (technical-preferences.md;
  architecture §9).
- **Offline-first:** MVP has no backend (architecture §11 ADR-I is deferred). Save is
  local, RNG is deterministic-local — a working game must never require a network fetch.

### Requirements

- One-file-per-level, per-region-subdirectory content addressable individually
  (`level-data-format.md` §1; TR-ldf-002).
- Per-region theming payload loadable/releasable at region granularity for Pillar 3
  (`world-map.md` §4; TR-wm-001).
- **Load fails loudly, never partially** — a level that cannot load must never produce a
  partial board; it must fail visibly and return control, never crash
  (`level-data-format.md` §Edge Cases; `world-map.md` W6 rationale).
- Peak resident memory must be **independent of total catalog size** (must not grow from
  MVP's 1 region to launch's 4 regions).
- A remote path must be reachable in Phase 3 **without re-architecture** (Events/Theming
  Engine #13; daily-challenge levels with `rng_seed >= 0`, `level-data-format.md`
  §2 / Edge Cases).

## Decision

Adopt a **seven-group, all-local-at-MVP** Addressables layout with a **single loader
choke point** and a **region-scoped resident set**.

### Architecture

```
                         ┌──────────────────────── SweetCascade.Game ────────────────────────┐
   Addressables 6.3      │                                                                    │
   (throwing loads)  ◄───┤  IContentLoader  ── the ONLY code that calls Addressables.* ───    │
        │                │     TryLoad<T>(address)         TryLoadByLabel<T>(label)            │
        │                │     Release(handle)             ReleaseLabel(label)                 │
        │                │        │  returns LoadResult<T> (Ok | Fail+reason) — never throws   │
        │                │        ▼                                                            │
        │                │  BootLoader ─ RegionContentController ─ LevelLoadController          │
        └────────────────┤        │              │                      │                      │
                         │        │ pin session   │ per region           │ per level           │
                         └────────┼───────────────┼──────────────────────┼──────────────────────┘
                                  ▼               ▼                      ▼      ToDomain()
                          ┌───────────────┐ ┌───────────────┐ ┌───────────────┐   │
                          │ RESIDENT      │ │ RESIDENT       │ │ (resolved from │  ▼
                          │ FOR SESSION   │ │ FOR 1 REGION   │ │  region batch) │ SweetCascade.Domain
                          │ Core + Rig    │ │ Theme+Levels+  │ │  LevelData →   │ (POCO LevelData,
                          │               │ │ Music          │ │  ToDomain()    │  no engine types)
                          └───────────────┘ └───────────────┘ └───────────────┘

   Resident set at any instant  =  Core  +  Shared_BoardRig  +  exactly ONE region's { Theme, Levels, Music }
   → independent of total catalog size (1 region at MVP, 4 at launch: resident set is ~constant).
```

### Group Layout (7 group templates; MVP build target = all Local / bundled)

| # | Group | Label | Contents | Delivery (MVP) | Lifecycle |
|---|-------|-------|----------|----------------|-----------|
| 1 | `Core_Bootstrap` | `core` | `level_manifest.asset` (LevelManifest SO), `world_map_manifest.asset` (WorldMapManifest SO), core UI Toolkit assets (HUD, overlay shells, Results/Pre-Level Card UXML+USS), core/shared SFX bank (`audio_pop_base`, UI clicks) | Local | Load once at **boot**, **pin for session**, release on quit |
| 2 | `Shared_BoardRig` | `board-rig` | 5 shared fruit meshes, the 13-material glass-candy set, the single candy/particle **atlas** texture, the pooled `FruitPiece` prefab, well meshes, blob-shadow decal | Local | Load once at **boot**, **pin for session**, release on quit |
| 3 | `Region_Theme_<region_code>` (one per region) | `region-theme:<code>` | Art-owned Region Theme Resource: background gradient triplet, UI trim/accent color, decorative prop set (2–4 silhouettes), ambient particle theme, `map_diorama` vignette — the **Pillar 3 reskin payload** (`world-map.md` §4) | Local | Load on **region-enter**, release on **region-change** (only active region resident) |
| 4 | `Levels_<region_code>` (one per region) | `level-data:<code>` (each `LevelData` addressed by its `level_id`) | The per-region folder `assets/data/levels/<region_code>/*.asset` of `LevelData` SOs | Local | **Batch-load** the whole region's set on **region-enter**, release on **region-change** |
| 5 | `Audio_Region_<region_code>` (one per region) | `music:<code>` | Per-region music track(s) and region-specific ambient audio (the largest per-region payload) | Local | Load on **region-enter**, release on **region-change** |
| 6 | `Remote_Events` (reserved) | `event:*` | **Empty at MVP.** Reserved template for Phase 3 event/daily-challenge content and seasonal region re-themes (`level-data-format.md` §6; `world-map.md` §4 Phase-3 seam) | **Remote** (unbuilt at MVP) | Phase 3: `DownloadDependenciesAsync` on demand, release after event |
| 7 | `Editor_Fixtures` (test-only) | `test:*` | Deterministic level/theme fixtures for Play Mode tests; **excluded from player builds** | Local (editor only) | Test setup/teardown only |

**Group-granularity rationale.** Level data, theme, and music are split into **separate
groups per region** (not one "all levels" bundle) precisely because the resident set must
be region-scoped: a single mixed bundle would force all 120 levels + 4 themes resident
together, defeating the memory strategy. Level data is loaded/released as a **per-region
batch** (rather than one bundle per level) because each `LevelData` is a tiny data-only
SO — a per-level bundle would multiply catalog/bundle overhead for no memory win, while
a per-region batch loads ~30 KB-scale assets in one operation and lets a `level-enter`
resolve its `LevelData` from the already-resident batch with **no additional Addressables
call**. The shared board rig is one group because it is byte-identical across every level
and region — duplicating it into region bundles would be the single worst memory
regression (guarded by the duplicate-dependency analyzer, below).

### Local vs Remote

**MVP = 100% local (bundled in the player binary).** Justification:

1. **Content fits the binary.** The whole MVP art payload is "trivially inside 400MB"
   (architecture §9.4); 120 tiny `LevelData` SOs + 4 region themes at launch is still a
   modest bundle. No content is large enough to *need* streaming.
2. **Offline-first is a hard requirement.** Save, RNG, and gameplay are fully local; a
   working game must never block on a network fetch. Remote-by-default would violate this.
3. **Publishing simplicity (ADR-001 founder priority).** A self-contained binary means no
   CDN, no first-launch download UX, no catalog-version QA, no content-update pipeline —
   all of which are pure cost with zero MVP benefit.
4. **Remote is reserved, not removed.** The `Remote_Events` group template and the
   loader's remote-capable path (`DownloadDependenciesAsync`, `GetDownloadSizeAsync`,
   `CheckForCatalogUpdates`) are defined now so Phase 3 events — the content class that
   *genuinely* benefits from shipping without an app-store update — drop in with no
   re-architecture. At MVP this group is empty and unbuilt.

### Load / Release Lifecycle (mapped to Screen Flow base states)

```
COLD BOOT (B1 BOOT_LOADING):
  1. Addressables.InitializeAsync()                      → catalog ready
  2. loader.TryLoadByLabel("core")      → pin (AppRoot retains handle for session)
  3. loader.TryLoadByLabel("board-rig") → pin (AppRoot retains handle for session)
     [ both #2/#3 failing here is FATAL — see failure contract ]
  4. SaveService.LoadProfile()  (settings drive audio/juice)
  5. → WORLD_MAP (T1)

REGION-ENTER (world map focuses a region, OR entering a level whose region isn't resident):
  6. release previous region's { region-theme:<old>, level-data:<old>, music:<old> }
  7. loader.TryLoadByLabel("region-theme:<code>")  (fallback: Core default theme)
  8. loader.TryLoadByLabel("level-data:<code>")    (batch; fallback: block that level)
  9. loader.TryLoadByLabel("music:<code>")         (fallback: silent)
     [ MVP has one region → this runs once and is effectively pinned ]

LEVEL-ENTER (T4/T11/T17/T18 → GAMEPLAY):
 10. resolve LevelData from the resident region batch by level_id  (NO new load)
     [ defensive: if batch not resident, loader.TryLoad<LevelDataAsset>(level_id) ]
 11. LevelData.ToDomain() → BoardModel bootstrap (Domain never sees an Addressables type)

LEVEL-EXIT (Results → map, or retry T18):
 12. nothing released at level granularity — LevelData stays resident (same region);
     retry reuses the resident LevelData (matches screen-flow "retry skips Pre-Level Card")

BACKGROUND (OnApplicationPause true):  release NOTHING (fast resume); SaveService.FlushIfDirty only
APP QUIT:  release all pinned handles (Core, Rig, active region)
```

**Invariant:** at any instant the resident set is exactly
`Core ∪ Shared_BoardRig ∪ (one region's {theme, levels, music})`. Entering a second
region releases the first region's three groups before loading the new one's — this
release-on-region-change discipline is implemented **from day one** even though MVP's
single region never exercises it, so the 4-region launch needs zero new lifecycle code.

### The `TryLoad` Failure-Handling Contract

`SweetCascade.Game` exposes **one** type that touches `Addressables.*`. Every load is
wrapped so a 6.2+ throw becomes a discriminated result; callers never see an exception.

### Key Interfaces

```csharp
// SweetCascade.Game — the ONLY Addressables choke point. No other file may call Addressables.*.
public interface IContentLoader
{
    Task<LoadResult<T>>        TryLoad<T>(string address)     where T : UnityEngine.Object;
    Task<LoadResult<IList<T>>> TryLoadByLabel<T>(string label) where T : UnityEngine.Object;
    void Release(IContentHandle handle);   // opaque wrapper over AsyncOperationHandle
    void ReleaseLabel(string label);       // releases a whole region group batch
}

public enum ContentLoadError { None, InvalidKey, MissingDependency, CorruptBundle, RemoteFetchFailed, Timeout, Unknown }

public readonly struct LoadResult<T>
{
    public bool            Ok         { get; }   // false ⇒ Asset/Handle are default, Error is set
    public T               Asset      { get; }
    public IContentHandle  Handle     { get; }
    public string          FailureKey { get; }
    public ContentLoadError Error     { get; }
}

// Reference implementation of the wrap (the load-bearing part — 6.2+ THROWS, so this MUST catch):
async Task<LoadResult<T>> TryLoad<T>(string address) where T : UnityEngine.Object
{
    AsyncOperationHandle<T> h = default;
    try
    {
        h = Addressables.LoadAssetAsync<T>(address);
        await h.Task;
        if (h.Status != AsyncOperationStatus.Succeeded || h.Result == null)
        {
            var err = Classify(h.OperationException);
            if (h.IsValid()) Addressables.Release(h);
            return LoadResult<T>.Fail(address, err);
        }
        return LoadResult<T>.Success(h.Result, Wrap(h));
    }
    catch (System.Exception e)                 // Unity 6.2+ throw-on-failure lands here
    {
        if (h.IsValid()) Addressables.Release(h);
        return LoadResult<T>.Fail(address, Classify(e));
    }
}
```

**Per-content fallback policy (what a caller does with `Ok == false`):**

| Content class | On load failure | Severity |
|---|---|---|
| **Core_Bootstrap / Shared_BoardRig** (at boot) | **FATAL** — show a boot-error screen with a Retry action; never enter GAMEPLAY. No rig = no game; fail loudly at boot, never mid-play. | Unrecoverable |
| **LevelData** (level-enter) | Abort the level start; Screen Flow shows a non-crashing "level unavailable" notice and returns to WORLD_MAP. **Never a partial board** (honors `level-data-format.md`'s load-loudly-never-partial rule at the runtime-load layer). | Recoverable, level-scoped |
| **Region_Theme** (region-enter) | Fall back to the Core neutral default theme so map/board still render; log. Pillar-3 dressing is non-critical to playability. | Graceful degrade |
| **Audio_Region music** (region-enter) | Silent-degrade to no music (or a core fallback loop); log. Never blocks gameplay. | Graceful degrade |

Every failure logs a structured record `(address, label, group, ContentLoadError)`. The
loader releases the failed handle before returning (no leaked refcount). **No Domain code
ever calls a load or sees an Addressables type** — this contract lives entirely in Game,
preserving the assembly boundary (Principle 1).

### Memory Budget Alignment (≤400MB)

Because only one region is resident at a time, peak Addressables-managed memory is
`Core + Rig + one region` regardless of whether the catalog holds 1 region or 4. A soft
**resident sub-budget of ≤120MB** is allocated to Addressables content out of the 400MB
ceiling, leaving headroom for the engine, render targets, audio voices, and the UI
Toolkit runtime.

| Resident item | Est. footprint (target Android, compressed) | Notes |
|---|---|---|
| `Shared_BoardRig` | ~30–50 MB | 5 low-poly meshes + 13 materials + 1 ASTC candy/particle atlas + pooled prefabs (§9.4: "trivially inside 400MB") |
| `Core_Bootstrap` | ~10–20 MB | UI Toolkit assets + core SFX + two tiny manifest SOs |
| One `Region_Theme` | ~10–20 MB | props, diorama vignette, particle textures |
| One `Audio_Region` | ~5–15 MB | compressed music/ambient |
| One `Levels_<code>` batch | < 1 MB | ~30 data-only SOs |
| **Peak resident total** | **~55–105 MB** | comfortably within the ≤120MB sub-budget and the 400MB ceiling |

These are **design-time estimates**; they are validated by a memory-profiling gate on
target hardware at the Vertical Slice checkpoint (ties to architecture QQ-05).

### Editor-Time & CI Validation Hooks

An `AddressablesValidation` tool in `SweetCascade.Editor` runs as a designer menu item
and as a **blocking** CI step (`game-ci`, mirroring the level-data V-rules and world-map
W-rules gate philosophy). Checks:

| ID | Check | Severity |
|---|---|---|
| A1 | Every `LevelData` SO under `assets/data/levels/<region_code>/` is marked Addressable and lives in its region's `Levels_<region_code>` group. | Blocking |
| A2 | **address == `level_id` invariant** — each `LevelData`'s Addressables address equals its `level_id` field, so Board Engine resolves `level_id → LevelData` by address with no lookup table. | Blocking |
| A3 | **Manifest ↔ catalog cross-check** — every `level_id` in `level_manifest.asset` has an addressable entry (extends `world-map.md` W6/W7 into the catalog); every region in `world_map_manifest.asset` has a `Region_Theme_<code>` **and** an `Audio_Region_<code>` group. | Blocking |
| A4 | **Label/grouping integrity** — each group carries exactly its designated label; no asset is in two groups; no orphaned addressable (marked addressable but in no known group). | Blocking |
| A5 | **Local-only MVP policy** — no group has a Remote build/load path at MVP; `Remote_Events` is empty. Prevents shipping an accidental remote dependency in the MVP binary. | Blocking |
| A6 | **Duplicate-dependency analyzer** — run Addressables' built-in "Check Duplicate Bundle Dependencies" rule; a shared asset (e.g., a candy material) duplicated into a region bundle instead of staying in `Shared_BoardRig` fails. | Blocking |

### Implementation Guidelines

- Delegate the concrete `ContentLoader`, `BootLoader`, `RegionContentController`, and
  `LevelLoadController` implementation to `lead-programmer` / `unity-addressables-specialist`
  within this ADR's group/lifecycle contract.
- `Classify(exception)` maps message/type to `ContentLoadError`; keep it defensive
  (default `Unknown`) — the exact 6.3 exception surface is a Verification Required item.
- Group templates and labels are authored in the Addressables Groups window and committed
  under `Assets/AddressableGroups/`; per-region groups are generated/checked by the same
  editor tool that runs A1–A6 so adding a region is a data step, not a code step.
- `IContentHandle` is an opaque interface so no consumer outside `ContentLoader` can
  reach the underlying `AsyncOperationHandle` (keeps the choke point enforceable in review).

## Alternatives Considered

### Alternative 1: `Resources.Load` / direct-reference everything (no Addressables)

- **Description**: Hold all content via direct `[SerializeField]` references on a
  bootstrap object, or under a `Resources/` folder loaded synchronously.
- **Pros**: Simplest possible; no async, no refcount, no catalog.
- **Cons**: Everything referenced is force-loaded and resident — memory grows linearly
  with the 120-level / 4-region catalog and blows the 400MB budget by Alpha. No path to
  Phase 3 remote content without a later rewrite. `Resources/` is explicitly discouraged
  in Unity 6.
- **Estimated Effort**: Lower now, far higher later (a forced migration at Alpha).
- **Rejection Reason**: Directly violates the "resident set independent of catalog size"
  requirement and strands Phase 3.

### Alternative 2: One monolithic Addressables bundle for all content

- **Description**: A single local group containing every level, theme, and track.
- **Pros**: One bundle, trivial catalog, no per-region wiring.
- **Cons**: Loading any level pulls the whole catalog resident (same memory failure as
  Alt 1, just via Addressables); no region-granular release; a single corrupt bundle
  takes down everything.
- **Estimated Effort**: Comparable.
- **Rejection Reason**: Defeats the entire point of Addressables (on-demand load/unload);
  fails the memory requirement.

### Alternative 3: Per-level bundles (one Addressables group per level)

- **Description**: Each `LevelData` (and its deps) in its own group/bundle, loaded and
  released per level-enter/exit.
- **Pros**: Finest-grained residency; only the one active level's data is resident.
- **Cons**: ~120 bundles of KB-scale data — bundle/catalog overhead dwarfs the payload;
  a per-level async load adds a hitch on every level entry and on retry; no benefit over
  a per-region batch since a whole region's `LevelData` is < 1 MB.
- **Estimated Effort**: Higher (more groups, more per-entry lifecycle code).
- **Rejection Reason**: Over-granular; the per-region batch achieves the same memory
  result with one load per region and zero per-level load hitch.

### Alternative 4: Remote-first delivery (CDN) from MVP

- **Description**: Ship a thin binary; fetch level/region content from a CDN at runtime.
- **Pros**: Content updates without an app-store release from day one.
- **Cons**: Requires CDN + catalog-versioning + download UX + offline handling + content
  QA — all before the game is playable; breaks offline-first; adds a network failure mode
  to the core loop; contradicts publishing-simplicity.
- **Estimated Effort**: Much higher.
- **Rejection Reason**: Pure cost at MVP for a benefit only Phase 3 events need — which
  the reserved `Remote_Events` group already covers without paying the cost now.

## Consequences

### Positive

- Peak memory is region-scoped and **flat as the catalog scales** MVP → launch; the
  ≤400MB budget is defended by construction, not by hope.
- A **single choke point** makes the 6.2+ throw-on-failure risk a one-place concern and
  keeps every `UnityEngine`/Addressables type out of the Domain (Principle 1 intact).
- **Load-loudly-never-partial** is honored at the runtime layer with graceful per-content
  degradation (a broken theme never bricks a playable board).
- Phase 3 remote events are a **data drop**, not a re-architecture.
- Catalog integrity (address==`level_id`, manifest cross-check, no duplicate deps) is a
  **blocking CI gate**, catching content-authoring mistakes at build time.

### Negative

- A one-time async cost at boot (Core + Rig load) — hidden behind the boot/splash screen,
  but it is real and must stay off the interactive path.
- Region-change incurs a load/release burst (3 groups). Acceptable at region granularity
  (rare, screen-transition-masked); would not be acceptable at level granularity — which
  is why level-enter resolves from the resident batch instead.
- More groups to author and keep labeled correctly — mitigated by the A1–A6 editor tool
  making group membership a checked, generated concern rather than manual discipline.
- Pinning Core + Rig for the whole session trades a small permanent memory floor for zero
  mid-play load churn on the shared art — an intentional, budgeted trade.

### Neutral

- The address==`level_id` invariant couples the Addressables address scheme to Level Data
  Format's identifier — deliberate, and enforced by A2 so it cannot silently drift.
- Save data is intentionally outside Addressables (owned by ADR-B on `persistentDataPath`).

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| 6.3's exact throw-vs-failed-handle behavior differs from the reference's description | Medium | High | Verification-Required Play Mode test asserts `TryLoad` swallows an invalid-key load; `Classify` defaults to `Unknown`; loader catches *and* checks `.Status` (belt and suspenders) |
| A shared material/mesh gets duplicated into region bundles, inflating memory/size | Medium | Medium | A6 duplicate-dependency analyzer as a blocking CI gate |
| Resident-set estimates wrong on real hardware; sub-budget breached | Medium | High | Vertical-Slice memory-profiling gate on 2022-era Android (QQ-05); ASTC footprint verification |
| Handle leak (a load whose handle is never released) grows memory over a session | Low | Medium | All handles flow through `IContentLoader`; region groups released by `ReleaseLabel`; Play Mode leak test asserts refcount returns to baseline after region-change |
| Founder amends local→remote split late, forcing bundle re-layout | Low | Low | `Remote_Events` template + remote-capable loader path already exist; moving a group from Local to Remote is a group-setting change, not a code change |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a (no loader) | ~0 ms in-loop (all loads are boot/region-transition, masked by screens; Domain does no loading) | 16.6 ms |
| Memory (resident) | n/a | ~55–105 MB Addressables content (Core + Rig + 1 region), flat across MVP→launch | ≤400 MB total; ≤120 MB Addressables sub-budget |
| Load Time (boot) | n/a | Core + Rig async load behind splash (target < ~2 s on mid-range Android; verify) | Splash-masked; no hard SLA yet |
| Load Time (region-enter) | n/a | 3-group batch, screen-transition-masked; level-enter = 0 additional load | Transition-masked |
| Network (MVP) | n/a | 0 KB/s — all local | 0 (offline-first) |

## Migration Plan

Greenfield — no existing content-load system to migrate. Rollout order:

1. Author the 7 group templates + labels under `Assets/AddressableGroups/`; mark the MVP
   region's `LevelData` addressable with address==`level_id`. Verify A1–A6 pass.
2. Implement `IContentLoader` + `LoadResult<T>` + the `TryLoad` wrap; land the invalid-key
   Play Mode test (Verification Required #1).
3. Implement `BootLoader` (Core + Rig pin) and the boot-error fatal path.
4. Implement `RegionContentController` (region-enter load / region-change release) and
   `LevelLoadController` (resolve-from-batch + fallback).
5. Wire A1–A6 into CI as a blocking gate.

**Rollback plan**: content-load is isolated behind `IContentLoader`; reverting to plain
direct references (Alternative 1) for a hypothetical tiny build means swapping the loader
implementation and dropping the groups — no Domain or gameplay code changes, since the
Domain never depended on Addressables.

## Validation Criteria

- [ ] Play Mode test: `TryLoad` on an invalid address returns `Ok == false` with a set
      `ContentLoadError` and **no exception escaping** (proves the 6.2+ throw is contained).
- [ ] Leak test: enter region A, enter region B, return to A — Addressables refcount for
      A's groups returns to its post-first-load baseline (region-change releases correctly).
- [ ] Memory profile on target Android at Vertical Slice: resident Addressables content
      ≤ 120 MB with one region loaded; total ≤ 400 MB.
- [ ] A1–A6 run in CI and block a build that (a) has a non-addressable `LevelData`,
      (b) breaks address==`level_id`, (c) has a manifest/catalog mismatch, (d) ships a
      remote group at MVP, or (e) duplicates a shared dependency.
- [ ] LevelData load-failure path returns to WORLD_MAP with a notice and **no partial
      board** (fault-injection test).
- [ ] Region-theme load-failure path renders the Core default theme and continues.
- [ ] Boot-time Core/Rig load-failure path shows the boot-error/Retry screen and never
      enters GAMEPLAY.

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/level-data-format.md` | Level Data Format | "Levels are stored one-file-per-level … one subdirectory per region" (§1); 120-level launch scope; save keys off `level_id` | `Levels_<region_code>` groups mirror the per-region folders; each `LevelData` is addressed by its `level_id` (A2 invariant), resolvable individually |
| `design/gdd/level-data-format.md` | Level Data Format | "Load fails loudly … never a silent skip or a partially-populated level" (§Edge Cases) | `TryLoad` + LevelData fallback aborts the level and returns to map with a notice; never a partial board |
| `design/gdd/level-data-format.md` | Level Data Format | `rng_seed >= 0` reserved for Phase 3 daily-challenge/event levels (§2, Edge Cases) | `Remote_Events` reserved group carries that Phase 3 content class without MVP cost |
| `design/gdd/world-map.md` | Level Progression / World Map | Per-region theming slots (gradient, trim, prop set, ambient particles, diorama) are the Pillar 3 backbone (§4); 4-region/120-level roster, MVP content-gated to 1 region | `Region_Theme_<region_code>` + `Audio_Region_<region_code>` groups loaded/released at region granularity; MVP's single region exercises the same path unchanged |
| `.claude/docs/technical-preferences.md` / architecture §9 | Performance | ≤400MB on mid-range mobile | Region-scoped resident set (Core + Rig + one region), flat across MVP→launch; ≤120MB Addressables sub-budget; Vertical-Slice memory gate |
| `docs/architecture/architecture.md` §6, §7.4, §9.4, §11 | BootLoader / Foundation | Addressables init, throw-on-failure wrapping, on-demand load + refcount release "so only the active region is resident"; this is Required-ADR "ADR-A" | This ADR is the concrete realization: boot pin, region-scoped release, `TryLoad` contract, validation hooks |

## Related

- **ADR-001** (Engine Selection — Unity 6.3 LTS, Accepted) — dependency; Addressables is
  Unity-specific.
- **ADR-E** (Level manifest generation & W6–W7 cross-validation, planned) — the A2/A3
  editor checks extend its W6/W7 rules into the Addressables catalog.
- **ADR-B** (Save serialization, planned) — deliberately disjoint: save lives on
  `persistentDataPath`, not Addressables.
- **ADR-G** (Audio) / **ADR-H** (Particles), planned — the per-region music group and the
  shared candy/particle atlas respectively.
- `docs/engine-reference/unity/plugins/addressables.md` — API surface and throw-on-failure
  note this ADR is grounded in.
- Implementation targets (once written): `Assets/Game/Loading/ContentLoader.cs`,
  `BootLoader.cs`, `RegionContentController.cs`, `LevelLoadController.cs`;
  `Assets/Editor/AddressablesValidation/`.
