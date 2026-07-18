# Story 008: Save canonical JSON writer + FNV-1a-32 checksum + SerializeForDisk

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (§1 File Format, §2 Schema, Formula 1 FNV-1a)
**Requirement**: `TR-sp-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-003: Save Serialization Format & Atomic Durability
**ADR Decision Summary**: A **hand-rolled `CanonicalJsonWriter` in `SweetCascade.Domain`** (System.Text/Globalization/Collections only, zero third-party dependency) emits compact UTF-8 with a frozen positional field order, ordinal-sorted `level_records`, and invariant-culture numbers → byte-reproducible forever. **FNV-1a-32** (Formula 1) is the corruption-detection checksum; the writer is two-pass (hash over all fields except `checksum`, then emit with `checksum` at its §2 position). `JsonUtility`/`System.Text.Json`/Newtonsoft are rejected as the primary serializer.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (Domain codec is pure BCL; no reflection → no IL2CPP AOT surprises)
**Engine Notes**: No `UnityEngine` type is linkable from Domain, so `JsonUtility` is disqualified by construction. No floats exist in the schema, so no `"R"`/`"G17"` round-trip question ever arises. `best_score` is `long` (ADR-005 residue sweep: score is 64-bit; the blueprint's `int` is superseded).

**Control Manifest Rules (Domain layer)**:
- Required: the Save serializer is the hand-rolled `CanonicalJsonWriter` (+ `SaveJsonReader`, Story 009); pure C#, zero third-party dependency.
- Required: FNV-1a-32 computed exactly per Formula 1 (`OFFSET_BASIS=0x811C9DC5`, `PRIME=0x01000193`, unchecked `uint`); canonical byte contract frozen — UTF-8 no BOM, compact, fixed positional field order (never alphabetical), `level_records` sorted ascending by `level_id` via `StringComparer.Ordinal`, numbers via `ToString(InvariantCulture)`, booleans lowercase, absent optional timestamps = literal `null`.
- Required: `best_score`/final score is `long` (Int64), never `int`, on the wire; every constant (`CHECKSUM_ALGORITHM_VERSION="fnv1a-32-v1"`, `CURRENT_SCHEMA_VERSION=1`, `SLOT_COUNT=2`, the two FNV-1a constants) lives in one Domain config location.
- Forbidden: `UnityEngine.JsonUtility`; `System.Text.Json`/Newtonsoft as the write/checksum serializer; any file IO / `Application.persistentDataPath` in Domain (IO is E06).

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md` §1/§2/Formula 1 and ADR-003 Validation Criteria, scoped to this story:*

- [ ] `CanonicalJsonWriter` emits compact UTF-8 (no BOM, no whitespace) in the frozen positional field order: top-level `schema_version, write_counter, checksum, created_utc, modified_utc, total_stars, settings, level_records`; `settings` in table order; each `LevelRecord` as `best_stars, best_score, completion_count, first_completed_utc, last_completed_utc`.
- [ ] `level_records` entries are emitted **sorted ascending by `level_id` using `StringComparer.Ordinal`** (never culture-aware).
- [ ] Numbers use `ToString(CultureInfo.InvariantCulture)`; booleans are lowercase `true`/`false`; absent optional timestamps are literal `null`; `best_score` is serialized as a plain `long` JSON integer.
- [ ] `Fnv1a32.Compute` uses the Formula 1 constants in `unchecked uint`; `test_checksum_matches_formula_1_worked_example`: `Compute([0x61])` == `3826002220` (`0xE40C292C`).
- [ ] Two-pass `SerializeForDisk`: pass 1 hashes every field **except** `checksum`; pass 2 emits on-disk bytes with `checksum` inserted at index 3 (after `write_counter`). `checksum` is never part of its own hashed input.
- [ ] `test_canonical_bytes_match_golden`: output for a fixed known `Profile` is byte-identical to `canonical_golden_v1.sav`.
- [ ] `test_dictionary_key_ordinal_sort`: `level_records` serialize in ordinal-sorted key order regardless of insertion order.
- [ ] `test_number_invariant_culture_under_de_DE`: serialization under a comma-decimal locale still emits ASCII decimal digits.

---

## Implementation Notes

*Derived from ADR-003 Implementation Guidelines and Canonical byte contract:*

- Place `CanonicalJsonWriter.cs`, `Fnv1a32.cs`, and the Domain config constants under `Assets/Domain/Save/` in `SweetCascade.Domain` (arch §5.3). `Profile`/`Settings`/`LevelRecord` records land in Story 009 (or stub them here and finalize there — coordinate so both compile).
- `Fnv1a32.Compute` is the ADR-003 listing verbatim (`OffsetBasis=0x811C9DC5`, `Prime=0x01000193`, `unchecked { foreach byte: hash ^= b; hash *= Prime; }`). Note the shared-by-value FNV-1a primitive with the RNG `ForkStream` (Story 003) — same constants, intentionally.
- The writer maps PascalCase members to snake_case wire names **explicitly** (no reflection, no naming-policy config that could drift). Strings (`level_id` keys) are JSON-escaped per RFC 8259.
- Freeze `canonical_golden_v1.sav` from a fixed known `Profile`; it is the anti-drift guardian — any output change fails CI. A deliberate contract change is a `CHECKSUM_ALGORITHM_VERSION` bump, not a silent edit.
- Fixtures live at `src/SweetCascade/Assets/Tests/EditMode/Save/Fixtures/` (`canonical_golden_v1.sav`, `valid_v1_profile.json`).

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: the tolerant `SaveJsonReader` (parse/tokenize), round-trip, unknown-field tolerance, and escaping round-trip.
- Story 010: single-slot `Decode` — S1–S8 validation + §7 tamper-accept classification.
- Story 011: `Merge` (Formula 2), `RecomputeTotalStars` (Formula 3), migration, and Formula 4 slot-selection logic.
- E06: all file IO — `ISaveStore`, write→flush→read-back-verify, `NativeSaveStore`/`WebGlSaveStore`, the two-slot ResolveLoad ladder (TR-sp-002).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: byte-exact golden (`test_canonical_bytes_match_golden`).
  - Given: a fixed known `Profile`.
  - When: `SerializeForDisk` runs.
  - Then: output is byte-identical to `canonical_golden_v1.sav`.

- **AC-2**: FNV-1a worked example (`test_checksum_matches_formula_1_worked_example`).
  - Given: the single-byte payload `[0x61]`.
  - When: `Fnv1a32.Compute` runs.
  - Then: result == `3826002220`.
  - Edge cases: `checksum` is excluded from its own hashed input (two-pass); an empty `level_records` still hashes deterministically.

- **AC-3**: ordinal key sort (`test_dictionary_key_ordinal_sort`).
  - Given: a `Profile` whose `level_records` are inserted out of order.
  - When: serialized.
  - Then: keys appear in `StringComparer.Ordinal` ascending order.

- **AC-4**: culture invariance (`test_number_invariant_culture_under_de_DE`).
  - Given: the thread culture set to `de-DE`.
  - When: serialized.
  - Then: numbers are ASCII decimal digits (no comma decimal, no group separators); `best_score` is a plain `long` integer.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/save-persistence/save_canonical_writer_test.cs` + committed `canonical_golden_v1.sav` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Save/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **E01 Story 002** (Domain + `Domain.Tests` asmdefs; `Assets/Domain/Save/` tree). Soft: **E01 Story 005** (Edit-Mode test conventions).
- Unlocks: Story 009 (reader round-trips the writer's bytes), Story 010 (decode re-hashes via `Fnv1a32`), Story 011 (merge/migration serialize through this writer).
