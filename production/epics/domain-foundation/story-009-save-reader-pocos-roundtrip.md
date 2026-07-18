# Story 009: SaveJsonReader + Profile/Settings/LevelRecord POCOs + round-trip & unknown-field tolerance

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 3 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (§2 Schema, §8 Versioning — unknown-field tolerance, Acceptance Criteria)
**Requirement**: `TR-sp-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-003: Save Serialization Format & Atomic Durability
**ADR Decision Summary**: A tolerant, hand-rolled `SaveJsonReader` parses the canonical bytes back into the `Profile`/`Settings`/`LevelRecord` records (snake_case wire ↔ PascalCase members mapped explicitly). It **skips** unrecognized keys at any object level (never fails structurally over an unknown *field*) and surfaces warnings as data. Round-trip fidelity is field-for-field (excluding the deliberately-incremented `write_counter`).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure BCL; own the tokenizer/escaping)
**Engine Notes**: No reflection → no IL2CPP AOT stripping surprises. Domain cannot `Debug.Log`; unknown-field warnings return in a `SlotDecode.Warnings` list (or via an injected `ILogSink`) for the Game layer to forward.

**Control Manifest Rules (Domain layer)**:
- Required: the reader is hand-rolled, pure C# (System.Text/Globalization/Collections), zero third-party dependency; snake_case ↔ PascalCase mapped explicitly (no reflection/naming-policy that could drift).
- Required: `SaveModel`/POCOs stay in Domain; the reader returns warnings/results as data (no engine logging).
- Required: `best_score` is `long`; transient load annotations (`integrity_flag`, `recovery_notice_needed`, `LoadStatus`) are **never** serialized to disk (not JSON fields) — they live on `LoadResult` (finalized in Story 010).
- Forbidden: `UnityEngine.JsonUtility`; `System.Text.Json` as primary; Newtonsoft is a sanctioned fallback for the **read path only** and only if the bespoke reader proves too costly (no trigger defined — treat hand-rolled as the only implementation).

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md` §2/§8 and ADR-003 Validation Criteria, scoped to this story:*

- [ ] `Profile`, `Settings`, `LevelRecord` are C# records with the §2 fields and types; `best_score` is `long`; `first_completed_utc`/`last_completed_utc` are nullable (`long?`, literal `null` when absent).
- [ ] `SaveJsonReader` parses canonical bytes into a `Profile` with explicit snake_case mapping (no reflection).
- [ ] `test_round_trip_fidelity`: a `Profile` with non-default settings and multiple `level_records`, serialized (Story 008) then deserialized, is field-for-field identical (excluding `write_counter`).
- [ ] `test_unknown_field_tolerated_on_load`: an extra unrecognized top-level (or nested) key loads (key skipped + warned), never rejected.
- [ ] `test_missing_optional_field_defaulted`: a file missing an optional field gets its documented default.
- [ ] `test_null_completion_timestamps_round_trip`: `null` `first_/last_completed_utc` round-trip as `null`, not `0`.
- [ ] `test_escaped_levelid_round_trip`: a `level_id` key containing escapable characters round-trips exactly (writer/reader string escaping per RFC 8259).

---

## Implementation Notes

*Derived from ADR-003 Implementation Guidelines and Key Interfaces:*

- Place `SaveJsonReader.cs`, `Profile.cs`, `Settings.cs`, `LevelRecord.cs` under `Assets/Domain/Save/`. `LoadResult`/`LoadStatus` transient annotation types are declared here but *populated* by Story 010's decode.
- The reader is a small tolerant JSON tokenizer + object walker: recognize known keys, map to record fields, skip unknown keys (collect a warning), default absent optionals. Own the escaping/parsing correctness (the schema is closed, so the input space is bounded).
- Round-trip pairs this reader with Story 008's writer; assert field equality excluding `write_counter` (which the write algorithm increments by exactly 1).
- Unknown-field-without-version-bump forward case: the reader tolerates it (skip + warn); the S4 checksum consequence (loads via tamper-accept, `integrity_flag`) is Story 010's classification, not this story's.
- Ship the ADR-003 fixtures used here: `valid_v1_profile.json`, `unknown_field_additive.json`, `escaped_levelid.json` under `Assets/Tests/EditMode/Save/Fixtures/`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008: the canonical writer + FNV-1a-32 + `SerializeForDisk` (this story consumes its bytes).
- Story 010: single-slot `Decode` — S1–S8 validation + §7 tamper-accept classification + `LoadStatus` population.
- Story 011: merge/self-heal/migration and slot-selection.
- E06: file IO and the two-slot ResolveLoad ladder (TR-sp-002).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: round-trip fidelity (`test_round_trip_fidelity`).
  - Given: a populated `Profile` (non-default settings, multiple `level_records`).
  - When: serialized then deserialized.
  - Then: field-for-field identical except `write_counter` (incremented by 1).

- **AC-2**: unknown-field tolerance (`test_unknown_field_tolerated_on_load`).
  - Given: `unknown_field_additive.json` with an extra key.
  - When: read.
  - Then: the file loads, the unknown key is skipped and a warning is returned; no rejection.

- **AC-3**: optionals & nulls (`test_missing_optional_field_defaulted`, `test_null_completion_timestamps_round_trip`).
  - Given: a file missing an optional field, and a record with `null` completion timestamps.
  - When: read then re-serialized.
  - Then: the default is applied; `null` timestamps stay `null` (not `0`).

- **AC-4**: string escaping (`test_escaped_levelid_round_trip`).
  - Given: `escaped_levelid.json` with an escapable-character `level_id` key.
  - When: round-tripped.
  - Then: the key is byte-exact after write→read.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/save-persistence/save_reader_roundtrip_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Save/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 008** (writer/checksum to round-trip against; the shared Domain config). Transitively **E01 Story 002**.
- Unlocks: Story 010 (decode validates the parsed `Profile`), Story 011 (merge/migration operate on these records).
