# Story 010: Single-slot Decode — S1–S8 validation + detect-and-accept tamper classification

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (§5 Load-Time Validation S1–S8, §7 Integrity & Tamper Policy)
**Requirement**: `TR-sp-001`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-003: Save Serialization Format & Atomic Durability
**ADR Decision Summary**: `SaveModel.Decode(byte[]? rawSlot)` parses **one** slot, runs S1–S8, and classifies it per §7 (detect-and-accept): a checksum-only failure (S4) on an otherwise structurally/range-valid file is **accepted** with `integrity_flag = true`; S4 combined with any other Blocking failure is **rejected** as genuine corruption. To verify S4, re-run the writer's pass-1 hash over the parsed fields and compare to the stored `checksum`.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure BCL; single-slot logic, no IO)
**Engine Notes**: This is the codec-owned single-slot subset (ADR-003 fixture list). The **two-slot** ResolveLoad ladder (§6), the A/B write→flush→read-back, and file IO are the Save slice (E06, TR-sp-002) — not this story.

**Control Manifest Rules (Domain layer)**:
- Required: FNV-1a-32 S4 verification re-hashes the parsed fields via the same `Fnv1a32.Compute` (Story 008) — `checksum` excluded from its own input.
- Required: transient annotations (`integrity_flag`, `LoadStatus`, later `recovery_notice_needed`) are never serialized; they live on `LoadResult`/`SlotDecode`.
- Required: `SaveModel` is pure and constructor-injectable into its Game-layer consumers (E06); no file IO in Domain.
- Guardrail: decode of a ~18KB profile is sub-ms, off the 16.6ms hot path.

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md` §5/§7 and ADR-003 Validation Criteria, scoped to this story:*

- [ ] `Decode(rawSlot)` parses one slot and evaluates S1 (`schema_version` supported/migratable), S2 (`write_counter` present, non-negative), S3 (`checksum` present — an absent checksum is structural, not tamper), S4 (recomputed FNV-1a == stored), S5 (`settings.*` present with correct types), S6 (`level_records` shape/ranges; `best_stars ∈ [0,3]`, `best_score ≥ 0`, `completion_count ≥ 0`, `first ≤ last` when both non-null), S7 (**Advisory** — `total_stars == sum(best_stars)`; triggers self-heal, never rejection), S8 (`created_utc ≤ modified_utc`).
- [ ] `test_tamper_wellformed_accepted_with_flag` / `test_checksum_mismatch_well_formed_accepted`: a slot passing S1,S2,S3,S5,S6,S8 but failing only S4 is **accepted**, sets `integrity_flag = true`, and does not get rejected.
- [ ] `test_corrupt_structural_rejected` / `test_checksum_detects_corruption`: S4 failing **and** a structural/range rule (S5/S6) failing → the slot is classified **invalid** (genuine corruption).
- [ ] `test_future_schema_version_rejected`: `schema_version: 999` (outside `{1}`) fails S1 → slot invalid.
- [ ] S7 mismatch alone never invalidates a slot — it flags the self-heal correction (executed in Story 011), not corruption.
- [ ] `Decode` returns a `SlotDecode` carrying the parsed `Profile` (when parseable), the validity classification, `integrity_flag`, and any reader warnings — all as data (no engine logging).

---

## Implementation Notes

*Derived from ADR-003 Key Interfaces (`SlotDecode`/`Decode`) and Checksum-field handling:*

- Add `SaveModel.Decode` under `Assets/Domain/Save/`, returning a `SlotDecode` record `(Profile? parsed, validity, integrity_flag, warnings)`.
- S4 verification: re-serialize pass-1 (all fields except `checksum`) over the parsed `Profile`, hash with `Fnv1a32.Compute`, compare to the stored `checksum` — exactly the writer's two-pass model (Story 008).
- Classification per §7 table: checksum-match → trusted; checksum-mismatch + all other Blocking pass → accepted + `integrity_flag`; checksum-mismatch + any other Blocking fail → rejected.
- S1 supports migration entry: an older `schema_version` with a defined migration path is not an S1 failure (the migration pipeline is Story 011); a *newer* version fails S1 (never partially load).
- Fixtures (ADR-003): `tamper_wellformed.json`, `corrupt_structural.json`, `future_schema_999.json` under `Assets/Tests/EditMode/Save/Fixtures/`.
- **Do not** implement the two-slot ladder here — `Decode` classifies a single slot; `ResolveLoad(a,b)` (the §6 ladder + Formula 4 + Formula 3) is E06's Save slice.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 009: the reader/POCOs/round-trip that produce the parsed `Profile`.
- Story 011: `RecomputeTotalStars` self-heal (S7's correction), merge, migration, and Formula 4 slot-selection.
- E06 (TR-sp-002): the two-slot `ResolveLoad` corruption ladder, A/B write→flush→read-back-verify, `recovery_notice_needed` two-slot outcome, and all file IO.

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: tamper-accept (`test_tamper_wellformed_accepted_with_flag`).
  - Given: `tamper_wellformed.json` (one `best_stars` hand-edited, checksum stale, all other rules pass).
  - When: `Decode` runs.
  - Then: accepted, `integrity_flag = true`, not rejected.

- **AC-2**: genuine corruption (`test_corrupt_structural_rejected`).
  - Given: `corrupt_structural.json` (checksum mismatch + an out-of-range/truncated field).
  - When: `Decode` runs.
  - Then: classified invalid.

- **AC-3**: future schema (`test_future_schema_version_rejected`).
  - Given: `future_schema_999.json`.
  - When: `Decode` runs.
  - Then: fails S1 → invalid.
  - Edge cases: an absent `checksum` fails S3 as structural (not tamper); `created_utc > modified_utc` fails S8 unconditionally regardless of checksum status.

- **AC-4**: S7 advisory.
  - Given: a structurally valid slot whose stored `total_stars` ≠ `sum(best_stars)`.
  - When: `Decode` runs.
  - Then: the slot stays valid; an S7 self-heal indication is surfaced (correction applied in Story 011), never a rejection.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/save-persistence/save_slot_decode_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Save/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 009** (parsed `Profile` + POCOs), **Story 008** (`Fnv1a32` for S4). Transitively **E01 Story 002**.
- Unlocks: Story 011 (S7 self-heal correction) and E06 (the two-slot ResolveLoad ladder composes single-slot `Decode` results).
