# ADR-006: Level Manifest Generation & Cross-Validation

## Status

Accepted

> **Founder review may amend.** This is a Technical Director self-review verdict
> under the project's Lean review mode. It is Accepted so the Board Engine, RNG,
> and World Map slices are unblocked, but the founder may amend the two-file
> decision, the tombstone representation, or the run-point policy at the next
> review pass. Amendments supersede via a new ADR, never a silent edit.

## Date

2026-07-18

## Last Verified

2026-07-18

## Decision Makers

Technical Director (self-review, gate `TD-ARCHITECTURE` continuation). Pending
founder review. Consulted (via their authored, APPROVED GDDs): systems-designer
(`board-engine.md`, `world-map.md`, `level-data-format.md`, `rng-service.md`).
This ADR is `ADR-E` in `docs/architecture/architecture.md` §11's Required-ADR
list, renumbered `ADR-006` under the founder's `A→002 … E→006` scheme.

## Summary

Two separately-owned ScriptableObject manifests — Board Engine's **generated,
append-only ordinal table** (`level_manifest.asset`, `level_id → 1-based ordinal`
for RNG seed derivation) and World Map's **hand-authored region node graph**
(`world_map_manifest.asset`) — are kept as two assets, not merged, and reconciled
by a single pure cross-validator enforced at editor time and in an Edit Mode /
CI test. An editor tool regenerates only the ordinal manifest by scanning
`assets/data/levels/`, deterministically appending new `level_id`s and
**tombstoning** removed ones so ordinals are never reused, renumbered, or
deleted (the invariant RNG determinism depends on).

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.x) |
| **Domain** | Core / Scripting / Editor tooling |
| **Knowledge Risk** | LOW–MEDIUM — `ScriptableObject`, `AssetDatabase`, and `IPreprocessBuildWithReport` are stable, pre-cutoff-reliable APIs; the only post-cutoff-adjacent surface is the deliberate decision NOT to route the manifests through Addressables (see below) |
| **References Consulted** | `docs/engine-reference/unity/plugins/addressables.md`, `docs/engine-reference/unity/current-best-practices.md`, `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None. The generator uses `UnityEditor.AssetDatabase` (FindAssets/LoadAssetAtPath/SaveAssets) and `UnityEditor.Build.IPreprocessBuildWithReport`, all stable across 2022→6.3. |
| **Verification Required** | Confirm `AssetDatabase.FindAssets("t:LevelDataAsset", …)` enumeration is used only as a *set* source (order is normalized by our own sort — see Decision), never relied on for ordering. Confirm the boot-time manifest load uses a **direct serialized reference**, not `Addressables.LoadAssetAsync` (which throws on failure in 6.2+ per `plugins/addressables.md`). |

> **Note**: Knowledge Risk is LOW–MEDIUM. The mechanism does not depend on any
> post-cutoff behaviour; if the project upgrades engines, only the editor-tooling
> API names need re-checking, not the design.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | None (foundational). The ordinal contract is self-contained. |
| **Enables** | ADR-C (Deterministic RNG generator — `architecture.md` §11, expected ADR-004): supplies the platform-stable **integer** `level_id` that RNG Formula F1 requires. Board Engine bootstrap (`board-engine.md` §2). World Map node-graph wiring (`world-map.md`). |
| **Blocks** | Board Engine slice, RNG session seeding, and World Map cannot ship without an authoritative ordinal manifest and its validation gate. |
| **Ordering Note** | This is a Foundation-tier ADR; author/accept before the Board Engine and World Map epics enter sprint. It composes with ADR-A (Addressables grouping) only insofar as ADR-A owns the *per-level* `LevelDataAsset` load path; the two **manifests** themselves are intentionally NOT Addressable (see Decision §Load path). |

## Context

### Problem Statement

Three approved GDDs place mutually-dependent demands on a level-identity mapping:

1. **`rng-service.md` (F1, §3, Edge Cases, TR-rng-004):** `start_level_session()`
   accepts an **integer** `level_id`. A raw string identifier must be resolved to
   "a fixed, versioned ordinal integer via a level manifest before reaching this
   service — never through a runtime string-hash function, whose output stability
   across [export] targets is not guaranteed." The ordinal is folded into the
   master seed; if it ever changes for a level, that level's *entire* board and
   cascade sequence changes retroactively.
2. **`board-engine.md` §Detailed Rules 2:** Board Engine **owns** the ordinal
   manifest (`entries`, an ordered **append-only** list; `manifest_index =
   1 + entries.find(level_id)`). "A `manifest_index` is assigned once, at first
   authoring, and is never reused or renumbered, even if a level is later removed
   from rotation." The GDD anticipates a manual "append at validation time" step
   and a companion test asserting bijection with the level files.
3. **`world-map.md` §Detailed Rules 2 + Validation rules W6–W7:** World Map owns a
   *second*, hand-authored manifest (region node graph, star gates, theming) and
   explicitly argues for **"two files, cross-validated — not one shared file."**
   W6: every `level_id` in a region `level_sequence` appears in the ordinal
   manifest. W7: every ordinal-manifest entry appears in exactly one region's
   `level_sequence` — "no RNG-registered level is invisible to the map."

The decision that must be made now: **one merged manifest asset or two**, and the
concrete **generation + append-only enforcement + cross-validation + removal**
mechanism — none of which the GDDs specify at the implementation level (they
correctly defer it to this ADR / `unity-specialist` territory).

### Current State

Nothing is implemented. The GDDs describe `.tres` Godot Resources; ADR-001 pivoted
the engine to Unity 6.3 LTS, so these become `.asset` ScriptableObjects
(`architecture.md` §2 residue sweep, §5.3 already names
`assets/data/level_manifest.asset` as "generated"). No generator, no validation
gate, and no removal policy exist yet. The append-only invariant is currently only
a sentence in a GDD, not a mechanically-enforced property — which is a Pillar-2
risk: a hand-maintained ordinal list is one careless reorder away from silently
rewriting every downstream seed.

### Constraints

- **Append-only ordinal order is sacred.** RNG determinism (Pillar 2, "Clever,
  Never Cheated") depends on ordinals never changing once assigned. Any mechanism
  that can renumber an existing entry — including a merge conflict, an Inspector
  drag, or a regeneration bug — is unacceptable.
- **The ordinal manifest is *generated*; the world-map manifest is *hand-authored*.**
  Region grouping order, star gates, theming, and `level_sequence` are creative
  decisions not derivable by scanning files; they must be authored. The ordinal
  table, by contrast, must be machine-produced to enforce append-only discipline.
- **Determinism across developer machines / CI.** `AssetDatabase.FindAssets`
  enumeration order is not guaranteed stable across OSes; generation output must be
  identical regardless.
- **Two approved GDDs already specify two files.** Overriding them requires a
  design re-opening, not a unilateral technical ADR.
- **Logic must be headless-testable** (`architecture.md` Principle 1): validation
  logic belongs in the pure `Domain` assembly, callable from both the editor tool
  and a headless Edit Mode test.

### Requirements

- Resolve `level_id (string) → ordinal (int, 1-based)`, platform-stable, no runtime
  string hashing.
- Enforce append-only: existing ordinals never move, never reused, never renumbered.
- A single deterministic editor tool produces the ordinal manifest from
  `assets/data/levels/`.
- W6–W7 (plus Board Engine's bijection test and World Map's W8 region-field match)
  enforced as a **blocking** gate; a **stale** manifest must fail the build/CI.
- A defined, non-destructive behaviour when a level file is removed (tombstoning).

## Decision

**Two ScriptableObject manifests, not one.** Preserve the ownership boundaries the
two approved GDDs already draw. This ADR owns the **generation of the ordinal
manifest**, the **append-only + tombstone discipline**, and the **cross-validator**
that reconciles both manifests with the level files. It does **not** generate the
world-map manifest (that stays hand-authored per `world-map.md`).

Decision, in five parts:

**1. Two assets.** `assets/data/level_manifest.asset` (`LevelManifestAsset`,
generated) and `assets/data/world_map_manifest.asset` (`WorldMapManifestAsset`,
hand-authored). Rationale for rejecting a merge is in Alternatives §1; the decisive
factor is that a *generated* artifact and a *hand-authored* artifact must not share
a file — every regeneration would otherwise have to read-modify-write around human
curation data it has no business touching, and every routine world-map reorder
would edit the same asset that holds the sacred append-only table.

**2. Generation mechanism (ordinal manifest only).** An `Editor`-assembly tool
scans `assets/data/levels/**` for `LevelDataAsset`s via
`AssetDatabase.FindAssets("t:LevelDataAsset")`, reads each `level_id`, and
reconciles against the *existing* committed manifest:
   - Existing entries are **frozen at their index** — never moved, never removed.
   - `level_id`s present in files but absent from `entries` are the *new set*;
     they are **sorted lexicographically ascending** (deterministic, independent of
     OS file-enumeration order) and **appended** at the end.
   - Ordinal = `index + 1` (faithful to `board-engine.md` Formula 1;
     `manifest_index = 1 + entries.find(level_id)`; index-0 stays the "unresolved"
     sentinel).
   - **The ordinal order carries no gameplay meaning.** It is an arbitrary-but-stable
     RNG-seed key only. Player-facing order and play order come exclusively from
     `world_map_manifest.asset` (World Map Formula 4 `display_number`). This is why
     lexicographic append is safe: a new `apple_grove-001` still appends *after*
     an older `candy_kingdom_hub-010` (higher ordinal), because existing entries
     never move.

**3. Append-only enforcement — how the tool refuses to renumber.** The tool never
performs a delete or an in-place reorder. Its only two write operations are
*append new* and *set-retired* (§5). Before writing, it asserts that the new
`entries` list has the committed list as an exact **prefix** (same `level_id` at
every existing index); if that prefix invariant would be violated — a level_id
changed at an existing slot, or an entry disappeared from the middle — it
**aborts with an error and writes nothing**. Renumbering is therefore not a policy
the tool declines to do; it is structurally unreachable through the tool.

**4. When generation runs.**
   - **Editor menu (apply mode):** `Sweet Cascade ▸ Level Manifest ▸ Regenerate` —
     the *only* path that writes the asset. A designer runs it after adding/removing
     a level; the append/tombstone diff is committed to VCS and is human-reviewable.
   - **Pre-build hook (verify mode):** an `IPreprocessBuildWithReport` regenerates
     into memory and **diffs against the committed asset**; if they differ, the
     build **fails** with "manifest stale — run Regenerate and commit." It does not
     silently mutate during build, keeping the checked-in asset the single
     reviewable source of truth.
   - **CI check (verify mode):** the same dry-run diff runs headless in CI
     (`game-ci/unity-test-runner`), plus the full cross-validator (§5). **A stale
     or invalid manifest fails the build** — satisfying the task's explicit gate.

**5. W6–W7 cross-validation + tombstoning.** All reconciliation logic lives in a
**pure `Domain` validator** (`ManifestCrossValidator`) taking POCOs + the scanned
`level_id` set and returning a violation list. It runs at two points: **editor
time** (inside Regenerate and a standalone `Validate` menu item, surfacing
violations immediately) and as a **headless Edit Mode / CI test**
(`tests/unit/world-map/` per `world-map.md` Acceptance Criteria — the BLOCKING
gate). The editor tool and the test call the *same* Domain function.

   **Tombstoning (removal behaviour).** When a level file is deleted, its ordinal
   entry is **not** removed (append-only). Regenerate instead adds its ordinal to a
   new additive field `retired_ordinals: List<int>` (empty by default → the schema
   stays backward-compatible with `board-engine.md`'s stated `entries: Array[String]`;
   `entries` itself is untouched). A tombstoned slot keeps its ordinal forever; the
   slot is never reused for a different `level_id`. The validator then treats
   retired entries specially, which is exactly what lets W7 remain a hard check
   without contradicting append-only:
   - **Bijection (Board Engine test):** every `level_id` under `assets/data/levels/`
     equals exactly one **non-retired** entry; no duplicates.
   - **W6:** every `level_id` in a region `level_sequence` is a **non-retired**
     entry. (A `level_sequence` referencing a retired level fails — this forces a
     designer who deletes a level to also remove it from the map.)
   - **W7:** every **non-retired** entry appears in exactly one `level_sequence`;
     **retired** entries must appear in **none**. (A retired level is legitimately
     invisible to the map — resolving the append-only-vs-W7 conflict.)
   - **W8 (retained):** each level file's `region` field equals the `region_code`
     of the region whose `level_sequence` lists it.

### Architecture

```
                 assets/data/levels/<region_code>/*.asset   (LevelDataAsset, hand-authored)
                             │  level_id, region
          ┌──────────────────┴───────────────────────────────────────────┐
          │                                                                │
   (scan, Editor)                                                   (read region field)
          ▼                                                                │
  ┌───────────────────────────────┐        cross-validate (W6/W7/W8)       │
  │  LevelManifestGenerator        │◄───────────────┐                      │
  │  (SweetCascade.Editor asmdef)  │                 │                      │
  │  • Regenerate (apply → write)  │        ┌────────┴───────────────┐      │
  │  • Verify (dry-run diff)       │        │  ManifestCrossValidator │      │
  │  • prefix-invariant guard      │        │  (SweetCascade.Domain,  │◄─────┘
  │  • deterministic append+       │        │   PURE — no engine ref) │
  │    tombstone                   │        └────────┬───────────────┘
  └───────────────┬────────────────┘                 │ same function
                  │ writes ONLY                       │ called by
                  ▼                                   ▼
  ┌───────────────────────────────┐        ┌────────────────────────────┐
  │ level_manifest.asset           │        │ Edit Mode / CI test        │
  │ (GENERATED, append-only)       │        │ tests/unit/world-map/      │
  │  entries: List<string>         │        │ (BLOCKING gate)            │
  │  retired_ordinals: List<int>   │        └────────────────────────────┘
  │  schema_version: int           │                 ▲
  └───────────────┬────────────────┘                 │
                  │ ToDomain()                        │ reads
                  ▼                                   │
  ┌───────────────────────────────┐        ┌────────────────────────────┐
  │ LevelManifest (Domain POCO)    │        │ world_map_manifest.asset   │
  │ ResolveOrdinal(id) : int  ─────┼──► RNG │ (HAND-AUTHORED, reorderable)│
  │ (feeds F1 integer level_id)    │  F1    │  regions[], level_sequence  │
  └───────────────────────────────┘        └────────────────────────────┘
       ▲ boot: direct reference load (NOT Addressables)      ▲ read every map visit
       │  Board Engine bootstrap step 6                      │  World Map / Screen Flow
```

### Key Interfaces

```csharp
// ── SweetCascade.Domain (PURE C#, noEngineReferences) ──────────────────────
// Faithful port of board-engine.md §2. `entries` unchanged; retired_ordinals
// is additive (empty default => backward-compatible with the GDD schema).
public sealed class LevelManifest                       // POCO built from the SO
{
    public int SchemaVersion { get; }
    public IReadOnlyList<string> Entries { get; }        // append-only, positional
    public IReadOnlyCollection<int> RetiredOrdinals { get; }

    // Formula 1: manifest_index = 1 + entries.find(level_id); 0 = unresolved sentinel.
    public int ResolveOrdinal(string levelId);           // 1-based; 0 if absent
    public bool IsRetired(int ordinal);
}

public readonly record struct ManifestViolation(
    ManifestRule Rule, string Detail);                   // Rule ∈ {Bijection,W6,W7,W8,Prefix,Tombstone,Duplicate}

public static class ManifestCrossValidator               // pure; called by tool AND test
{
    public static IReadOnlyList<ManifestViolation> Validate(
        LevelManifest ordinal,
        WorldMapManifest worldMap,
        IReadOnlyCollection<LevelIdentity> levelFiles);  // LevelIdentity = (levelId, region)
}

// Result of a generation pass; verify-mode asserts !Changed && Violations.Count==0.
public readonly record struct ManifestGenResult(
    LevelManifest Manifest, bool Changed,
    IReadOnlyList<ManifestViolation> Violations);

// ── SweetCascade.Game (ScriptableObject wrappers; engine types live here) ──
public sealed class LevelManifestAsset : ScriptableObject
{
    [SerializeField] int schemaVersion = 1;
    [SerializeField] List<string> entries = new();       // never reordered/removed
    [SerializeField] List<int> retiredOrdinals = new();  // tombstones
    public LevelManifest ToDomain();                     // mirrors LevelDataAsset.ToDomain()
}

// ── SweetCascade.Editor (UNITY_EDITOR only; the ONLY writer) ───────────────
public static class LevelManifestGenerator
{
    // apply=true  -> writes level_manifest.asset (menu Regenerate)
    // apply=false -> dry-run diff only (pre-build hook + CI verify)
    public static ManifestGenResult Reconcile(bool apply);   // throws on prefix-invariant breach
}
```

### Implementation Guidelines

- Place `LevelManifest`, `WorldMapManifest`, `ManifestCrossValidator`,
  `LevelIdentity` in `SweetCascade.Domain` (`Levels/`), asmdef `noEngineReferences`.
  Place the `ScriptableObject` wrappers in `SweetCascade.Game`. Place the generator,
  menu items, and pre-build hook in `SweetCascade.Editor` (`ManifestGen/`), guarded
  by the asmdef's editor platform (never shipped in a build).
- **Boot load path:** the two manifests are tiny and needed before any board
  bootstrap (`architecture.md` boot step 6). Load them as **direct serialized
  references** on the BootLoader/config object, **not** via Addressables —
  `plugins/addressables.md` explicitly says "DON'T use Addressables for assets
  needed immediately at startup." This also sidesteps the 6.2+ throw-on-failure
  behaviour. Per-level `LevelDataAsset`s remain Addressable (ADR-A's concern).
- `ResolveOrdinal` is `entries.FindIndex(id) + 1`; O(n), n ≤ 120, run once per
  level entry — never in a frame loop. Board Engine calls it at bootstrap and hands
  the int to `RngService.StartLevelSession(ordinal, attempt)`.
- The generator must be **idempotent**: a Regenerate with no file changes produces
  a byte-identical asset (`Changed == false`). This is what makes the CI dry-run
  diff meaningful.
- Wire the CI verify + cross-validation as an Edit Mode test so it runs under the
  existing `game-ci/unity-test-runner` gate; do not invent a separate CI job.

## Alternatives Considered

### Alternative 1: Single merged ScriptableObject (ordinal table + node graph)

- **Description**: One asset carrying both the append-only `entries` and the
  hand-authored `regions[]`/`level_sequence`; Board Engine and World Map each read
  their slice. (The framing this ADR was asked to weigh.)
- **Pros**: One artifact to locate; the ordinal↔sequence cross-check becomes
  intra-file; one generator output.
- **Cons**: Co-locates a **generated** artifact with a **hand-authored** one — every
  Regenerate must read-modify-write around human curation data, coupling the
  generator to the world-map schema it should not know. Every routine world-map
  reorder (an expected, frequent edit per `world-map.md` §2 and its Formula-4
  reorder edge case) then edits the *same file* that holds the sacred append-only
  table, inviting an accidental ordinal perturbation — the one outcome append-only
  exists to prevent. Two developers (one curating regions, one adding a level) now
  collide on one YAML asset instead of two independent files. And it **contradicts
  two APPROVED GDDs** that both specify two files, requiring a design re-opening.
- **Estimated Effort**: Similar build effort, higher ongoing risk.
- **Rejection Reason**: The generated-vs-authored split is dispositive, and
  `world-map.md` §2 already refuted the merge with three sound reasons (different
  stability contracts, different consumers, no cost to separation). Merging trades a
  cosmetic "one file" tidiness for a determinism hazard. The GDDs' ownership
  boundaries demand two files.

### Alternative 2: Delete-and-compact on removal (renumber) instead of tombstoning

- **Description**: When a level is removed, delete its `entries` slot and compact,
  so ordinals stay contiguous.
- **Pros**: No `retired_ordinals` field; `entries` is always a clean 1:1 with live
  levels.
- **Cons**: Renumbers every ordinal after the removed slot, silently rewriting those
  levels' master seeds (F1) and thus their entire board/cascade histories — a direct
  violation of `board-engine.md` §2 and Pillar 2. Any logged bug-repro seed for an
  affected level becomes non-reproducible.
- **Rejection Reason**: Breaks the single most important invariant in the system.

### Alternative 3: No manifest — hash the string `level_id` at runtime

- **Description**: Derive the integer seed input directly from the string
  identifier via a hash, skipping the manifest entirely (the "do nothing" baseline).
- **Pros**: Zero tooling.
- **Cons**: `rng-service.md` §3 and its Edge Cases explicitly forbid this: string
  hash output is not guaranteed stable across iOS/Android/Web export targets, so the
  same level could produce different boards on different devices — fatal to daily
  challenges and to cross-platform reproducibility.
- **Rejection Reason**: Prohibited by the RNG contract; defeats determinism.

*(A fourth option — keep the GDD's purely manual "append at validation time" with no
tool — is folded into the Decision: it is retained as the mental model but upgraded
with a deterministic generator and a CI staleness gate, because a hand-maintained
append-only list has no mechanical protection against a careless reorder.)*

## Consequences

### Positive

- Append-only is now a **mechanically enforced** property (prefix-invariant guard +
  generated-only writes), not a documented hope — Pillar 2 becomes structurally true.
- The ordinal↔map↔files three-way consistency is a **blocking CI gate**; an orphaned
  or unreachable level (W6/W7) or a stale manifest cannot ship.
- Validation logic is pure `Domain` — headless-testable, engine-neutral, reused
  verbatim by the editor tool and the CI test (single source of truth for the rules).
- Faithful to both approved GDDs; no design re-opening required. Reordering the
  world map remains a zero-migration, zero-risk edit (World Map's key advantage is
  preserved because that file is untouched by the generator).
- Removal is safe and reviewable: a tombstone is a small, diffable VCS change.

### Negative

- Introduces a build/CI dependency: forget to Regenerate after adding a level and
  the build fails (by design — a hard forcing function, mildly annoying in daily flow).
- Adds an additive `retired_ordinals` field beyond `board-engine.md`'s literal
  `Array[String]` schema — a (backward-compatible) extension that should be recorded
  reciprocally in `board-engine.md` at its next review pass (flagged, not edited here).
- Two files still require the cross-validator to stay honest; the safety rests on the
  CI gate actually running (mitigated below).

### Neutral

- `.tres` → `.asset`; Godot `Resource` → `ScriptableObject` (already mandated by
  ADR-001's residue sweep).
- The ordinal order is explicitly declared *semantically meaningless* (RNG key only),
  which some may find counter-intuitive versus play order — documented to prevent
  anyone "fixing" it to match level order.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| A stale manifest ships (level added, not regenerated) | Medium | High (wrong/absent seed, unbootable level) | Pre-build hook + CI dry-run diff fail the build on any staleness. |
| Manual Inspector edit reorders/deletes an `entries` slot | Low | Critical (silent seed rewrite) | Prefix-invariant guard aborts the tool; Edit Mode bijection test fails; VCS diff review flags any hand edit to a generated asset. |
| Generator non-determinism across OS (file enumeration order) | Medium | Medium (spurious CI diffs) | New `level_id`s sorted lexicographically before append; `FindAssets` used only as an unordered set source. |
| Tombstone/W7 gap: retired level left in a `level_sequence` | Low | Medium (dead map node) | W6 requires non-retired; W7 forbids retired-in-sequence — both blocking. |
| Two-file drift (map vs ordinal) | Low | High | W6/W7/W8 blocking cross-validator in CI; cannot merge on failure. |
| CI gate silently disabled | Low | High | Rule already in `coding-standards.md` ("never disable failing tests"); the test is Logic-tier BLOCKING per `world-map.md` ACs. |

## Performance Implications

The manifest is a boot-time, editor-time, and CI-time concern only — **zero
per-frame cost**. Figures below are the one-time boot resolve and the tooling scan.

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | 0 ms | 0 ms (no per-frame work; resolve is one O(n≤120) `IndexOf` at level entry) | 16.6 ms |
| Memory | 0 MB | < 0.05 MB (two small assets: ≤120 strings + ints, loaded once at boot) | 400 MB |
| Load Time | n/a | Negligible (< 1 ms boot resolve; direct-reference load, no async catalog) | — |
| Network (if applicable) | n/a | n/a (local assets, not Addressable/remote) | — |

Editor/CI generation is an O(n) directory scan + O(n) reconcile over ≤120 files —
sub-second, off the runtime path.

## Migration Plan

1. **Bootstrap the ordinal manifest.** Run `Sweet Cascade ▸ Level Manifest ▸
   Regenerate` against the initial 10 MVP `LevelDataAsset`s. This appends
   `candy_kingdom_hub-001…010` in lexicographic order (ordinals 1–10) and writes
   `level_manifest.asset` with `retired_ordinals = []`. Commit; verify the diff.
2. **Author the world-map manifest** (hand-authored, out of this tool's scope) and
   run the cross-validator; resolve any W6/W7/W8 violations until CI is green
   (`world-map.md` AC `test_real_manifest_zero_blocking_failures`).
3. **Wire the gates:** register the `IPreprocessBuildWithReport` verify hook and the
   Edit Mode cross-validation test in CI. Confirm a deliberately-stale manifest fails
   the build (negative test).
4. **Exercise tombstoning once** on a throwaway level: add it, regenerate (append),
   delete the file, regenerate (tombstone), and confirm the ordinal is retained in
   `entries`, added to `retired_ordinals`, and that leaving it in a `level_sequence`
   fails W6.

**Rollback plan**: delete `level_manifest.asset` and re-run Regenerate — because the
tool is deterministic and idempotent, a clean regenerate from the current level set
reproduces the live ordinals exactly (tombstones for already-deleted files are the
only history that a from-scratch regen cannot recover; while any prior manifest
exists in VCS this is a non-issue — restore it and regenerate on top). If the ADR is
reversed entirely, revert to a hand-maintained list; no runtime code outside
`ResolveOrdinal`'s single call site is affected.

## Validation Criteria

- [ ] `ResolveOrdinal("candy_kingdom_hub-007")` returns a stable 1-based ordinal;
      `ResolveOrdinal(unknown)` returns `0` (sentinel).
- [ ] Regenerate is idempotent: a no-change run yields `Changed == false` and a
      byte-identical asset.
- [ ] Adding a level appends exactly one new ordinal; **no** existing ordinal changes
      (prefix-invariant holds).
- [ ] Deleting a level file adds its ordinal to `retired_ordinals`, leaves `entries`
      untouched, and never reuses the slot.
- [ ] A hand-edit that reorders/removes an `entries` slot makes `Reconcile` abort with
      an error and write nothing.
- [ ] Cross-validator: `test_w6_orphaned_level_sequence_entry_rejected`,
      `test_w7_orphaned_board_engine_entry_rejected`,
      `test_w8_region_field_mismatch_rejected` all fail-closed (per `world-map.md` ACs).
- [ ] A retired ordinal left in a `level_sequence` fails W6; a non-retired entry in
      no `level_sequence` fails W7.
- [ ] A deliberately stale committed manifest fails the pre-build hook and CI.
- [ ] No hardcoded `level_id→int` mapping exists anywhere in `Assets/` except via
      `ResolveOrdinal` (data-driven rule).

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/board-engine.md` | Board Engine | §2: owns `level_manifest`; append-only `entries`; `manifest_index = 1 + entries.find(level_id)`; ordinal "never reused or renumbered, even if a level is removed" | Generated `LevelManifestAsset` with a prefix-invariant guard and generated-only writes; `ResolveOrdinal` implements Formula 1 verbatim; tombstoning realises "never reused even if removed." |
| `design/gdd/board-engine.md` | Board Engine | §2 companion test: every `level_id` under `assets/data/levels/` appears once; no duplicate `entries` | Bijection check in `ManifestCrossValidator`, run in the Edit Mode / CI test. |
| `design/gdd/world-map.md` | World Map | §2 "two files, cross-validated — not one shared file"; W6, W7, W8 blocking rules | Two assets preserved; W6/W7/W8 implemented in the pure validator, blocking in CI; tombstone rules make W7 compatible with append-only. |
| `design/gdd/level-data-format.md` | Level Data Format | §2: `level_id` `<region_code>-<3-digit-sequence>`, stable, never reused; `region` validated against a registry (W8) | Ordinal keyed off the stable string `level_id`; W8 checks each file's `region` against its region's `region_code`. |
| `design/gdd/rng-service.md` | RNG Service | F1/§3/Edge Cases/TR-rng-004: integer `level_id` via manifest, never runtime string-hash; platform-stable | `ResolveOrdinal` supplies the platform-stable integer; no hashing; the append-only guarantee keeps every F1 master seed permanent. |

## Related

- `docs/architecture/architecture.md` §11 (this is Required ADR "ADR-E"), §5.3
  (`assets/data/level_manifest.asset` generated), §6 Foundation table (`LevelManifest`
  Domain POCO + generated SO), §7.4 boot step 6.
- `docs/architecture/adr-001-engine-selection-unity.md` (`.tres`→`.asset`,
  GDScript→C#).
- Enables ADR-C (Deterministic RNG generator, `architecture.md` §11 — not yet
  written, expected ADR-004) and ADR-A (Addressables grouping — owns the *per-level*
  load path, distinct from the direct-reference manifest load decided here).
- Reciprocal follow-up (flagged, not edited): `board-engine.md` §2 should record the
  additive `retired_ordinals` field and this ADR as its implementation reference;
  `world-map.md`'s Open Question about a reciprocal cross-manifest test entry is
  answered by the shared `ManifestCrossValidator`.
- Code (once implemented): `Assets/Domain/Levels/LevelManifest.cs`,
  `ManifestCrossValidator.cs`; `Assets/Game/…/LevelManifestAsset.cs`;
  `Assets/Editor/ManifestGen/LevelManifestGenerator.cs`; `tests/unit/world-map/`.
