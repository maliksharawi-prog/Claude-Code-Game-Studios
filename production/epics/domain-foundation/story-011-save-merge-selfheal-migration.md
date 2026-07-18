# Story 011: Monotonic merge + self-healing total_stars + additive migration + A/B slot-selection

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (Formula 2 merge, Formula 3 self-heal, Formula 4 slot selection, §8 migration)
**Requirement**: `TR-sp-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-003: Save Serialization Format & Atomic Durability
**ADR Decision Summary**: `SaveModel.Merge` implements Formula 2 (monotonic max stars/score, `completion_count + 1`, first/last utc); `RecomputeTotalStars` implements Formula 3 (always recomputed on write, re-validated + silently corrected on load — never trusted as caller-supplied). Migration is an ordered pipeline of pure `MigrateV{n}ToV{n+1}` functions (none exist at v1). The pure A/B slot-selection logic (Formula 4 `SelectPrimary`/`NextTarget`) is delivered here for E06's file IO to consume.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure BCL; no IO)
**Engine Notes**: The merge rule is commutative + idempotent — the exact property a future Phase-3 cloud-sync layer reuses unmodified. `now_utc` is a caller-supplied `long` (never read from a Domain clock).

**Control Manifest Rules (Domain layer)**:
- Required: schema migration is an ordered pipeline of pure `MigrateV{n}ToV{n+1}` functions; the fully-migrated result is written via the normal serialize path (Story 008), so migration cost is paid once per device.
- Required: `total_stars` is recomputed by the save logic itself on write (never accepted caller-supplied) and re-validated on load (Formula 3 self-heal).
- Required: every constant (`CURRENT_SCHEMA_VERSION=1`, `SUPPORTED_SCHEMA_VERSIONS={1}`, `SLOT_COUNT=2`) is centralized; `SaveModel` is pure and injected into its Game consumers.
- Guardrail: merge/recompute/selection are trivial O(level_count) pure functions, off the hot path.

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md` Formulas 2/3/4, §8 and Acceptance Criteria, scoped to this story:*

- [ ] `Merge(current, level_id, stars, score, now_utc)` (Formula 2): `best_stars' = max(existing, stars)`, `best_score' = max(existing, score)`, `completion_count' = existing + 1`, `first_completed_utc'` set once (kept if non-null), `last_completed_utc' = now_utc`; creates the record on first completion.
- [ ] `test_level_record_merge_monotonic`: a worse replay leaves `best_stars`/`best_score` unchanged, increments `completion_count`, updates `last_completed_utc` — reproducing Formula 2's worked-example numbers (3★/4100 vs 1★/2600 → 3★/4100, count 6→7, last→1753000000).
- [ ] `RecomputeTotalStars` (Formula 3): `total_stars = Σ best_stars` on write; on load, if stored ≠ true sum, correct silently (no notice, no slot invalidation).
- [ ] `test_total_stars_self_heals`: a loaded profile whose stored `total_stars` ≠ `sum(best_stars)` is corrected to the true sum after load, without `recovery_notice_needed` or slot invalidation.
- [ ] Migration pipeline: an ordered list of pure `MigrateV{n}ToV{n+1}` functions runs ascending for an older `schema_version`; at v1 the list is empty and a v1 fixture loads with zero migration steps (`schema_version` stays 1).
- [ ] `test_migration_v1_fixture_identity`: `valid_v1_profile.json` loads with zero migration steps applied.
- [ ] Formula 4 slot-selection is pure: `SelectPrimary(a,b)` = the valid slot with the higher `write_counter` (tie → A); `NextTarget(primary)` = the non-primary slot — no IO, consumed by E06.
- [ ] `test_slot_selection_prefers_higher_write_counter`: given two valid slots, the higher `write_counter` is primary and `NextTarget` is the other.

---

## Implementation Notes

*Derived from ADR-003 Implementation Guidelines and Formulas 2/3/4:*

- Add `Merge`, `RecomputeTotalStars`, `SelectPrimary`/`NextTarget`, and the `MigrateV{n}ToV{n+1}` pipeline to `SaveModel` under `Assets/Domain/Save/`.
- Merge is the Formula 2 named expression verbatim; it is only ever invoked on a win (so `stars ≥ 1`), and is commutative/idempotent for cloud-sync reuse.
- Formula 3 self-heal: on write always overwrite `total_stars` with the recomputed sum; on load compare + correct silently (S7 is Advisory — Story 010 surfaced the flag; this story applies the correction).
- Formula 4 `SelectPrimary`/`NextTarget` are pure decisions over two slots' `(is_valid, write_counter)`; they make backup rotation "free." **E06** wraps them with the actual file write→flush→read-back-verify and the two-slot ResolveLoad ladder (TR-sp-002) — do not add IO here.
- At v1 no `MigrateV*` exists; provide the ordered-pipeline scaffold + the identity path so a future bump slots in cleanly.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 008/009/010: writer/reader/single-slot decode this story builds on.
- E06 (TR-sp-002): the two-slot `ResolveLoad` ladder (§6), the atomic A/B write→flush→read-back-verify, `SaveService`, `ISaveStore`, debounce, and `flush_if_dirty` — all file IO / app-lifecycle.
- The Game-side `record_level_completion`/`update_setting` API surface (TR-sp-004, E06); this story delivers only the pure `Merge`/recompute/selection logic those APIs call.

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: monotonic merge (`test_level_record_merge_monotonic`).
  - Given: an existing `{3★, 4100, count 6, first 1751000000, last 1752000000}` record.
  - When: `Merge` runs with `1★, 2600, now 1753000000`.
  - Then: `{3★, 4100, count 7, first 1751000000, last 1753000000}`.
  - Edge cases: first-ever completion creates the record with `first == last == now`.

- **AC-2**: self-heal (`test_total_stars_self_heals`).
  - Given: a loaded profile with `best_stars` `3,2,3` but stored `total_stars = 7`.
  - When: the load recompute runs.
  - Then: `total_stars` corrected to `8`; no notice, no invalidation.

- **AC-3**: migration identity (`test_migration_v1_fixture_identity`).
  - Given: `valid_v1_profile.json` (`schema_version: 1`).
  - When: loaded.
  - Then: zero migration steps applied; `schema_version` remains `1`.

- **AC-4**: slot selection (`test_slot_selection_prefers_higher_write_counter`).
  - Given: two valid slots with `write_counter` 42 and 41.
  - When: `SelectPrimary`/`NextTarget` run.
  - Then: 42 is primary; `NextTarget` returns the 41 slot; tie-break prefers A.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/save-persistence/save_merge_selfheal_migration_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Save/`, headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 009** (POCOs), **Story 010** (S7 flag it corrects; decode results). Transitively **Story 008**, **E01 Story 002**.
- Unlocks: E06 Save slice (wraps `Merge`/`SelectPrimary`/`NextTarget` with file IO), E04 (win→persist handshake calls `record_level_completion` which uses `Merge`).
