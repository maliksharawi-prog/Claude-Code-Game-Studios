# Story 003: SaveService — Atomic A/B Durable Write (ISaveStore)

> **Epic**: App Shell — Boot, Persistence IO & Content Loading (E06)
> **Status**: Ready
> **Layer**: Foundation (Game-side)
> **Type**: Integration
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (§4 atomic write, §5 load validation, §6 corruption ladder, §9 WebGL, §11 API)
**Requirement**: `TR-sp-002` (atomic A/B double-buffer, no rename dependency; corruption ladder; tamper detect+accept — **file IO**)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-003: Save Serialization Format & Atomic Durability
**ADR Decision Summary**: Two files `profile_a.sav`/`profile_b.sav` under `Application.persistentDataPath + "/save/"`; each a complete self-describing compact-JSON copy; writes follow **write → flush → read-back-verify**; primary is derived from `write_counter` at load (Formula 4) — no rename, no pointer file. WebGL swaps only the flush primitive (IDBFS sync).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: MEDIUM
**Engine Notes**: The Domain codec is pure BCL (LOW, E02); this story is the Game-layer file-IO durability surface (MEDIUM). `FileStream.Flush(true)` on iOS/Android and the WebGL IndexedDB/IDBFS sync are Verification-Required durability primitives — not assumed. `Application.persistentDataPath` replaces Godot's `user://`. **No atomic-rename guarantee on WebGL** — the A/B scheme is designed around this.

**Control Manifest Rules (this layer — Game):**
- Required: save write sequence = compute bytes in Domain → `ISaveStore.WriteSlotDurable` writes + flushes (native `FileStream.Flush(true)`; WebGL IDBFS sync) → `SaveService` immediately **read-back-verifies** before trusting; on read-back failure `dirty` stays `true` and the untouched slot remains authoritative (no swap step; primary derived from `write_counter`).
- Forbidden: never depend on `rename()`/atomic-rename semantics; never any file IO / `persistentDataPath` access in Domain (IO lives behind `ISaveStore` in Game).
- Guardrail: serialize + FNV-1a over ~18KB is sub-ms, off the 16.6ms hot path (runs on win/settings/pause); on-disk footprint < 40KB at full 120-level scope.

---

## Acceptance Criteria

*From GDD `design/gdd/save-persistence.md` §4–§6 + ADR-003, scoped to this story:*

- [ ] The A/B write algorithm targets the non-primary slot (Formula 4), increments `write_counter` by 1, serializes canonical bytes (via the E02 Domain codec) with `modified_utc` updated, writes + flushes, then read-back-verifies before trusting; two consecutive saves alternate slots.
- [ ] OS-kill safety: an interrupted write (mocked partial `WriteSlotDurable`) leaves the untouched slot fully valid and still returned as primary on the next `load_profile()`, with zero loss of its previously-saved state.
- [ ] Corruption ladder: if one slot is invalid, the valid slot loads as primary (no notice); if both are invalid, a fresh default profile is created **and** `recovery_notice_needed` is set; if both are absent, a fresh profile with **no** notice.
- [ ] Tamper detect+accept: a well-formed checksum mismatch (S4 fails, S1/2/3/5/6/8 pass) loads with `integrity_flag = true` and does NOT fall back to the other slot; a checksum mismatch combined with any other Blocking failure is rejected.
- [ ] `total_stars` self-heals silently to the true sum on load if the stored value drifts (Formula 3) — never treated as corruption.
- [ ] The WebGL path runs the identical schema/A-B/ladder logic; only the flush primitive is branched inside `ISaveStore` (`WebGlSaveStore` IDBFS sync); eviction of both slots is accepted as first-launch-equivalent.

---

## Implementation Notes

*Derived from ADR-003 Decision + Architecture:*

- `SaveService` (Game) owns `profile_a.sav`/`profile_b.sav`, the dirty flag, and orchestrates write → flush → read-back-verify. `ISaveStore` is the IO seam (bytes in, bytes out, durable flush) with `NativeSaveStore` (`FileStream.Flush(true)`) and `WebGlSaveStore` (write + IDBFS sync) implementations.
- The serialize/checksum/validate/merge/slot-selection logic is the **Domain** `SaveModel`/`ISaveModel` (E02) — inject it (constructor injection). This story does not re-implement the codec; it wires the Game-side IO and the read-back-verify trust gate.
- There is no swap step and no pointer file — "which slot is primary" is derived from the two slots' `write_counter`s at the next load (Formula 4). The next successful write naturally targets the previously-bad slot, self-healing it.
- Read-back-verify closes the gap where `Flush(true)` returns but bytes did not durably land (full disk / eviction mid-write): re-read the just-written slot and re-run `Decode`; trust only if S1–S8 pass and the checksum matches.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 004: the `SaveService` public API surface + lifecycle triggers (`RecordLevelCompletion`, `UpdateSetting` debounce, `FlushIfDirty`, `LoadProfile`/`GetProfile`).
- Story 005: the `BootLoader` calling `LoadProfile` in the boot sequence.
- E02: the Domain `SaveModel` codec (`CanonicalJsonWriter`/`SaveJsonReader`/`Fnv1a32`), `Profile`/`Settings`/`LevelRecord`, `Merge`/`Validate`/`ResolveLoad`/slot-selection, and the byte-exact golden fixture.

---

## QA Test Cases

*Integration story — Play Mode save-round-trip; deterministic (interruption simulated by mocking `WriteSlotDurable` to stop partway; no real OS kills).*

- **AC-1 (`test_atomic_write_interrupt_preserves_previous_state`)**: The write to `target_slot` is mocked to stop after a partial byte count; the untouched slot remains valid and is returned as primary on the next load, with zero loss.
- **AC-2 (`test_slot_alternation`)**: Two consecutive successful saves target opposite slots, each incrementing `write_counter` by 1, with `primary_slot` flipping per Formula 4.
- **AC-3 (ladder)**: `test_ladder_primary_to_backup` (A corrupt, B valid → B loads); `test_ladder_both_invalid_fresh_profile_with_notice`; `test_first_launch_no_notice` (both absent → fresh, no notice).
- **AC-4 (tamper)**: `test_checksum_mismatch_well_formed_accepted` (S4-only fail → accept, `integrity_flag`, no fallback); structural+checksum failure → rejected.
- **AC-5 (self-heal)**: `test_total_stars_self_heals` — a drifted stored `total_stars` corrected to the true sum on load, no notice, slot not invalidated.
- **AC-6 (read-back-verify)**: A write whose read-back fails (mocked) leaves `dirty = true` and the untouched slot authoritative.
- **AC-7 (WebGL branch)**: The IDBFS-branch store runs the same ladder; a mocked eviction of both slots resolves to fresh-profile-no-notice.

*Verification: Play Mode round-trip test + a smoke check (`production/qa/smoke-*.md`).*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/save-persistence/save_service_atomic_write_test.cs` (Play Mode) — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E01 (assemblies + CI), E02 (Domain `SaveModel` codec + `Serialize`/`Deserialize`/`Merge`/`Checksum` + slot-selection logic this service writes to disk).
- Unlocks: Story 004 (API surface calls into this write path), Story 005 (BootLoader `LoadProfile`).
