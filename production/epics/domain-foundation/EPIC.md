# Epic: Domain Foundation — RNG, Save Codec, Level Schema & Manifest Tool

> **Epic ID**: E02
> **Layer**: Foundation
> **GDD**: `design/gdd/rng-service.md` · `design/gdd/level-data-format.md` · `design/gdd/save-persistence.md` · `design/gdd/world-map.md` (W6–W7)
> **Architecture Module**: `SweetCascade.Domain` foundation types + `SweetCascade.Editor` manifest tool — RngService, LevelData schema + `Validate()`, SaveModel + serializer (canonical bytes + FNV-1a + monotonic merge), LevelManifest POCO + generation tool + W6–W7 cross-validation
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories domain-foundation`

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

## Next Step

Run `/create-stories domain-foundation`.
