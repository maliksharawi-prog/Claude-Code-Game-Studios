# Epic: Domain Foundation — RNG, Save Codec, Level Schema & Manifest Tool

> **Epic ID**: E02
> **Layer**: Foundation
> **GDD**: `design/gdd/rng-service.md` · `design/gdd/level-data-format.md` · `design/gdd/save-persistence.md` · `design/gdd/world-map.md` (W6–W7)
> **Architecture Module**: `SweetCascade.Domain` foundation types + `SweetCascade.Editor` manifest tool — RngService, LevelData schema + `Validate()`, SaveModel + serializer (canonical bytes + FNV-1a + monotonic merge), LevelManifest POCO + generation tool + W6–W7 cross-validation
> **Status**: Ready
> **Stories**: 14 stories created 2026-07-18 (see Stories table below)

## Scope

The pure-C# Domain backbone plus its editor-time manifest tooling — the largest, most
test-covered, and lowest-engine-risk part of the codebase (arch §1 LOW-risk domain). Delivers:
the deterministic, stream-isolated, platform-stable **RngService** (F1–F6, SplitMix32-style
finalizer — never `System.Random`); the versioned **LevelData schema** with the V1–V19
validation suite and connectivity flood-fill; the **Save codec** (compact canonical UTF-8
JSON, FNV-1a checksum, A/B slot-selection logic, monotonic `LevelRecord` merge, self-healing
`total_stars`, additive migration) — logic only, no file IO; and the **LevelManifest** POCO +
append-only ordinal generation tool + W6–W7 editor-time cross-validation. Everything here is
golden-vector-pinned and runs headless in a plain .NET Edit Mode runner. File IO (E06) and
board consumption of the manifest ordinal (E03) live in other epics.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-004: Deterministic RNG & Domain-Purity Guard | Platform-stable `mix32`/SplitMix32; ban on `System.Random`; per-stream isolation; golden vectors | LOW |
| ADR-003: Save Serialization Format & Atomic Durability | `CanonicalJsonWriter` / `SaveJsonReader` / `Fnv1a32` (Domain, BCL-only); integer-only schema; A/B selection logic | LOW |
| ADR-006: Level Manifest Generation & Cross-Validation | Generated LevelManifest SO, append-only ordinals, `retired_ordinals` tombstones, W6–W7 non-retired cross-check | LOW |
| arch §6 | LevelData POCO shape/defaults/closed enums, `Validate()`, additive `display_name` | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-rng-001 | Named, independently-seeded streams; isolation guarantee (draw on A never perturbs B) | ADR-004 ✅ |
| TR-rng-002 | Platform-stable seed derivation (F1–F3 + `mix32`) — byte-identical iOS/Android/Web; **not** `System.Random` | ADR-004 ✅ |
| TR-rng-003 | `next_float/int/color/shuffle`, `fork_stream`, `get_session_log` API surface | ADR-004 ✅ |
| TR-rng-004 | `level_id` supplied as a resolved int ordinal (never runtime string-hash) | ADR-004 + ADR-006 ✅ |
| TR-ldf-001 | Per-level data as a versioned schema (`schema_version`, closed enums, migration policy) | arch §6 (LevelData POCO) ✅ |
| TR-ldf-003 | V1–V19 validation suite (blocking + advisory); connectivity flood-fill (V8) | arch §6 `Validate()` + ADR-006 (W8) ✅ |
| TR-ldf-004 | Additive `display_name` field (v1.1, no schema bump) per adoption ledger | arch §6 / GDD schema ✅ |
| TR-sp-001 | JSON at persistentDataPath; compact canonical bytes; FNV-1a checksum (**codec logic only**) | ADR-003 ✅ |
| TR-sp-003 | Monotonic `LevelRecord` merge; self-healing `total_stars`; additive schema migration | ADR-003 ✅ |
| TR-wm-002 | W6–W7 manifest cross-validation (every authored level ↔ manifest ordinal), editor-time | ADR-006 ✅ |

## Depends On

- **E01** (assemblies + Domain-purity guard + CI must exist before Domain code is buildable/provable).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **LOW — pure C# Domain, zero engine surface by construction** (arch §1). No post-cutoff
  Unity API is touched by RngService / SaveModel / LevelData schema.
- **Determinism caveat (raised loudly, arch §1/§11 QQ-04):** the board RNG MUST implement a
  platform-stable SplitMix32-style finalizer per ADR-004. `System.Random` is byte-unstable
  across .NET runtimes and is banned in Domain (ADR-004 L2 denylist, CI-enforced).
- The **manifest generation tool** uses stable Editor APIs (`AssetDatabase.FindAssets`,
  `IPreprocessBuildWithReport`) — not post-cutoff, LOW risk.
- Save schema is integer-only (`\bfloat\b` is denylisted in Domain/Rng; scores are `long`).
  FNV-1a-32 constants (`0x811C9DC5` / `0x01000193`) are shared by value with ADR-004's
  `ForkStream` (intentional; golden-vector-pinned).

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- RngService reproduces its golden vectors byte-identically; stream-isolation and
  `fork_stream` are unit-proven; a Domain-purity CI run confirms zero engine symbols.
- The Save codec round-trips a profile to canonical bytes and back, computes the correct
  FNV-1a checksum, performs monotonic merge + `total_stars` self-heal, and picks the valid
  higher-`write_counter` slot — all in Edit Mode with fixtures.
- The V1–V19 LevelData validation suite (incl. V8 flood-fill) passes/fails against fixture
  levels as specified; the manifest tool generates an append-only ordinal manifest and W6–W7
  cross-validation catches an unmapped level.
- All Logic stories have passing Edit Mode test files in `tests/`; acceptance criteria from
  the four source GDDs are verified.

## Stories

| # | Story | Type | TRs | Status | ADR |
|---|-------|------|-----|--------|-----|
| 001 | RNG primitives & seed derivation (Mix32 / SplitMix32 / F1–F3) | Logic | TR-rng-002 | Ready | ADR-004 |
| 002 | RNG draw API — NextFloat / NextInt / NextColor / Shuffle (F4–F6) | Logic | TR-rng-003 | Ready | ADR-004 |
| 003 | RNG stream isolation, ForkStream & bug-repro session log | Logic | TR-rng-001, TR-rng-003 | Ready | ADR-004 |
| 004 | RNG golden-vector fixture & byte-for-byte regression suite | Logic | TR-rng-002 | Ready | ADR-004 |
| 005 | LevelData Domain POCO — schema v1, defaults, closed enums, additive display_name | Logic | TR-ldf-001, TR-ldf-004 | Ready | arch §6 |
| 006 | LevelData.Validate() — V1–V19 scalar & structural rules | Logic | TR-ldf-003 | Ready | arch §6 |
| 007 | LevelData V8 connectivity flood-fill (single 4-connected board) | Logic | TR-ldf-003 | Ready | arch §6 |
| 008 | Save canonical JSON writer + FNV-1a-32 checksum + SerializeForDisk | Logic | TR-sp-001 | Ready | ADR-003 |
| 009 | SaveJsonReader + POCOs + round-trip & unknown-field tolerance | Logic | TR-sp-001 | Ready | ADR-003 |
| 010 | Single-slot Decode — S1–S8 + detect-and-accept tamper classification | Logic | TR-sp-001 | Ready | ADR-003 |
| 011 | Monotonic merge + self-healing total_stars + migration + A/B slot-selection | Logic | TR-sp-003 | Ready | ADR-003 |
| 012 | LevelManifest Domain POCO — ResolveOrdinal, tombstones, WorldMapManifest POCO | Logic | TR-rng-004, TR-wm-002 | Ready | ADR-006 (+ADR-004) |
| 013 | ManifestCrossValidator — bijection + W6 + W7 + W8 (pure, shared) | Logic | TR-wm-002, TR-ldf-003 | Ready | ADR-006 |
| 014 | LevelManifestGenerator editor tool — append-only, tombstone, prefix-guard, CI verify | Integration | TR-wm-002 | Ready | ADR-006 |

**TR coverage:** all 10 owned TRs allocated — TR-rng-001 (003), TR-rng-002 (001, 004), TR-rng-003 (002, 003), TR-rng-004 (012), TR-ldf-001 (005), TR-ldf-003 (006, 007, 013/W8), TR-ldf-004 (005), TR-sp-001 (008, 009, 010), TR-sp-003 (011), TR-wm-002 (012, 013, 014).

**Story tracks & critical path:**
- **RNG (ADR-004):** 001 → 002 → 003 → 004. Story 004 (golden vectors) is the standing determinism gate that unblocks E03.
- **Level Data schema (arch §6):** 005 → 006 → 007.
- **Save codec (ADR-003):** 008 → 009 → 010; 011 depends on 009/010. (Codec/single-slot subset only — the two-slot ResolveLoad ladder + file IO = TR-sp-002 in E06.)
- **Manifest tool (ADR-006):** 012 → 013 → 014. 014 is the ONE non-pure story (SweetCascade.Editor; `AssetDatabase`/`IPreprocessBuildWithReport`); it also depends on E01 Story 006.

**All 14 stories depend on E01** (asmdefs + Domain-purity guard + game-ci gate must exist before Domain code is buildable/provable) — specifically E01 Story 002 (asmdefs), Story 003 (L2 denylist scan), Story 004 (game-ci gate), Story 005 (test conventions), and Story 006 (Editor/ManifestGen layout, for Story 014). No governing ADR is Proposed — ADR-003/004/006 are Accepted and present; the LevelData POCO/Validate() home is master-architecture §6. Zero stories are blocked.

## Next Step

Run `/story-readiness production/epics/domain-foundation/story-001-rng-primitives-seed-derivation.md`, then `/dev-story` to begin. Work stories in dependency order (each story's `Depends on:` field lists its prerequisites). The four tracks (RNG / Level Data / Save / Manifest) are largely independent after E01 lands and can proceed in parallel; RNG Story 004's golden-vector gate and Manifest Story 014's CI verify are the two blocking gates E03/E10 wait on.
