# ADR-003: Save Serialization Format & Atomic Durability

## Status

Accepted — founder review may amend.

## Date

2026-07-18

## Last Verified

2026-07-18

## Decision Makers

Technical Director (author / self-review). Implements the founder-approved
`save-persistence.md` GDD and the master architecture's ADR-B scope
(`docs/architecture/architecture.md` §7.3, §11). Founder sign-off deferred to the
same founder-review pass that governs the master architecture (Draft) and ADR-001.

## Summary

Sweet Cascade must durably persist a single on-device player profile (settings,
per-level star/score records) as compact canonical JSON with an FNV-1a integrity
checksum and OS-kill-safe atomic writes, while keeping all serialization logic in the
pure-C# `SweetCascade.Domain` assembly. This ADR decides a **hand-rolled canonical
JSON codec in Domain** (rejecting `JsonUtility`, `System.Text.Json`, and Newtonsoft as
the primary serializer), ratifies **FNV-1a-32** per the GDD, and specifies the A/B
double-buffer file layout, the write→flush→read-back-verify durability sequence at
`Application.persistentDataPath`, the WebGL degraded path, and the Edit Mode fixtures
the format must ship with.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.x) |
| **Domain** | Core — Persistence (Scripting / File IO) |
| **Knowledge Risk** | MEDIUM — the Domain codec is pure BCL C# (LOW); the Game-layer file-IO durability surface (`FileStream.Flush(true)` on iOS/Android, IndexedDB/IDBFS sync on WebGL) is the MEDIUM-risk part, per architecture §1 Persistence row. |
| **References Consulted** | `docs/architecture/architecture.md` §1/§5/§6/§7.3/§11; `design/gdd/save-persistence.md` (all sections); `docs/engine-reference/unity/current-best-practices.md` (C# 9 records, Unity 6); `docs/engine-reference/unity/VERSION.md`; ADR-001. |
| **Post-Cutoff APIs Used** | None required by the Domain codec (System.* BCL only). Game layer uses `Application.persistentDataPath` and `System.IO.FileStream.Flush(bool)` (both BCL/engine-stable, pre-cutoff-safe). No Unity 6.3-only API is on the critical path. |
| **Verification Required** | (1) Confirm the WebGL `persistentDataPath` → IndexedDB flush primitive/timing in 6.3 IL2CPP (resolves the GDD's Open Question on the `user://` fsync-equivalent). (2) Confirm `FileStream.Flush(true)` maps to a real physical-storage sync on iOS/Android IL2CPP. (3) Confirm `System.Text.Json` is genuinely *not* required (it is rejected here — no manual DLL vendoring needed). |

> **Note**: Knowledge Risk is MEDIUM — re-validate the WebGL flush and IL2CPP
> `Flush(true)` behaviours if the project changes Unity minor version or scripting
> backend. The Domain codec itself carries no engine-version exposure.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-001 (Engine Selection — Unity 6.3 LTS, Accepted) — establishes C#/Unity, the `persistentDataPath` platform surface, and the `SweetCascade.Domain` `noEngineReferences` assembly rule this ADR relies on. |
| **Enables** | The Save & Persistence implementation slice (`SaveModel` in Domain, `SaveService` in Game); unblocks Screen Flow boot (`LoadProfile`), World Map star-gated unlocks (`GetProfile`/`GetTotalStars`), and the Level Objective win→persist handshake (`RecordLevelCompletion`). |
| **Blocks** | Epic "Save & Persistence" cannot start until this ADR is Accepted (it now is). The level-completion persist path (Level Objective slice) transitively waits on it. |
| **Ordering Note** | This is the master architecture's **ADR-B** (§11). It is independent of the sibling Foundation ADRs — ADR-A (Addressables grouping) and ADR-C (deterministic RNG) — and may be implemented in parallel; none share files with the save codec. |

## Context

### Problem Statement

The Save & Persistence GDD is Reviewed/Approved and specifies *what* is stored (profile
schema v1), *how it is protected* (A/B double-buffer, FNV-1a checksum, corruption
ladder, tamper-accept), and *how it evolves* (additive schema migration). It deliberately
stops at the engine boundary: it names JSON, canonical bytes, FNV-1a, and `persistentDataPath`
but leaves the concrete serializer, the exact byte-ordering discipline, and the disk
write/durability primitive to the Technical Director (its Open Questions explicitly punt
the writer choice and the fsync-equivalent to `technical-director`). Those must be decided
before the Save slice can be coded, because every one of the GDD's blocking Acceptance
Criteria (round-trip fidelity, checksum regression pin, atomic-interrupt safety) depends
on a single, fixed byte contract. Not deciding blocks the Save slice, and transitively
Screen Flow boot, World Map unlocks, and any test that persists a win.

### Current State

No save code exists. `docs/architecture/architecture.md` §7.3 records the open serializer
question and the load-bearing constraints: (a) serialization *logic* lives in Domain
(pure, testable), file *IO* lives in Game; (b) Unity's `JsonUtility` cannot serialize the
`Dictionary<string, LevelRecord>` profile; (c) canonical (fixed field order, compact) bytes
are required so the FNV-1a checksum is byte-reproducible. The `visual-interface-blueprint.md`
seed types are engine-neutral C# already; this ADR extends them with the save codec.

### Constraints

- **Domain purity (compiler-enforced).** `SweetCascade.Domain.asmdef` sets
  `noEngineReferences: true`; a CI grep fails the build if any `UnityEngine`/`UnityEditor`
  symbol appears in compiled Domain (architecture §5.1). Any serializer that lives in
  Domain must reference only the .NET BCL. This **disqualifies `UnityEngine.JsonUtility`**
  outright — it is an engine type and cannot be linked from Domain.
- **Byte-exact canonical output.** FNV-1a (GDD Formula 1) is computed over "the canonical
  UTF-8 serialization of every `Profile` field except `checksum`, in the fixed field order
  of §2 … with no extraneous whitespace." The same logical data must produce byte-identical
  output on every platform and every library version, forever — a silent reordering or
  formatting change would invalidate every previously-stored checksum.
- **Platform reality (architecture §1, GDD §9).** iOS/Android give a sandboxed native FS;
  WebGL backs `persistentDataPath` with a virtual, IndexedDB-backed FS with **no atomic-rename
  guarantee** and eviction risk. The GDD's A/B scheme already avoids depending on `rename()`.
- **Schema is small, closed, and integer-only.** Every numeric field in schema v1 is an
  integer type — `schema_version`, `write_counter` (uint64), `checksum` (uint32), the two
  `*_utc` timestamps (unix seconds), `total_stars`, `best_stars`, `best_score`,
  `completion_count`, and the two per-record `*_utc` fields. **There are no floating-point
  fields anywhere.** The only strings are the `level_id` dictionary keys
  (`<region_code>-<3-digit-sequence>`, ASCII). This is the decisive enabler for a hand-rolled
  writer (see Decision).
- **Single-threaded, offline, tiny.** All save logic runs on the main thread; total on-disk
  footprint is <40 KB at full 120-level scope (GDD Formula 5); disk is read exactly once per
  session at boot.

### Requirements

- **Correctness** — implement GDD §2 (schema), §4 (A/B write algorithm), §5 (S1–S8), §6
  (ladder), §7 (tamper-accept), §8 (additive migration), Formula 1 (FNV-1a), Formula 2
  (monotonic merge), Formula 3 (self-healing total), Formula 4 (slot selection) exactly.
- **Byte-reproducibility** — identical logical profile → identical bytes → identical checksum,
  cross-platform and cross-runtime.
- **Domain-pure serialization, Game-only IO** — no engine type in the serialize/checksum path.
- **Durability** — an OS process-kill at any point never damages the previously-good slot;
  a completed write is verified before it is trusted.
- **Performance** — off the 16.6 ms frame hot path entirely; boot load is one small read+parse.
- **Testability** — the format ships with deterministic Edit Mode fixtures and tests, no real
  timers, no real process kills, no real disk beyond fixture files.

## Decision

**Serializer: a hand-rolled canonical JSON codec in `SweetCascade.Domain`** — a
`CanonicalJsonWriter` (byte-exact emitter) plus a tolerant `SaveJsonReader`, together forming
`SaveSerializer`. Pure C# (System.Text, System.Globalization, System.Collections.Generic
only), zero `UnityEngine` reference, zero third-party dependency. **`JsonUtility` is
disqualified** (engine-bound → not linkable from Domain; cannot serialize `Dictionary`;
cannot serialize records/properties). **`System.Text.Json` and Newtonsoft.Json are rejected
as the primary serializer** (see Alternatives): neither guarantees byte-stable canonical
output without so much custom-converter code that the library stops adding value while adding
IL2CPP/AOT and version-drift risk to the *checksum contract itself*. The integer-only schema
makes the bespoke writer small and defect-resistant — integers have exactly one
`InvariantCulture` decimal representation, so the classic canonicalization hazard (float
formatting/rounding/locale) does not exist here.

**Checksum: FNV-1a 32-bit**, ratified exactly as GDD Formula 1 specifies (`OFFSET_BASIS =
0x811C9DC5`, `FNV_PRIME = 0x01000193`, mod 2^32), implemented in `unchecked` `uint` arithmetic.
Tag `CHECKSUM_ALGORITHM_VERSION = "fnv1a-32-v1"`. FNV-1a is correct here because its job is
**corruption detection** (bit flips, truncated writes), *not* tamper-proofing — the GDD's
tamper policy is detect-and-accept, and there is no backend/leaderboard integrity to defend at
MVP. A cryptographic MAC would be wasted cost against the wrong threat; if Phase 3 leaderboards
ever need tamper resistance, that is a separate HMAC decision, out of scope here (GDD §7 deferred).

**File layout & durability:** two files `profile_a.sav` / `profile_b.sav` under
`Application.persistentDataPath + "/save/"`, each a complete self-describing compact-JSON copy;
no separate pointer file (primary is derived from `write_counter` at load, Formula 4). Writes
follow a **write → flush → read-back-verify** sequence; the "swap" of which slot is primary is
implicit (never a `rename`). WebGL uses the identical logic with only the flush *primitive*
swapped (an IDBFS sync request instead of `Flush(true)`), per GDD §9's one-code-path rule.

### Architecture

```
                       SweetCascade.Game  (file IO, app lifecycle, MEDIUM engine risk)
   ┌───────────────────────────────────────────────────────────────────────────┐
   │  SaveService                                                                │
   │   • owns profile_a.sav / profile_b.sav, dirty flag, 750ms settings debounce │
   │   • OnApplicationPause/Focus/quit → FlushIfDirty()                           │
   │   • orchestrates write → flush → read-back-verify                            │
   │            │ calls (pure, returns byte[]/LoadResult)   ▲ raw bytes           │
   │            ▼                                           │                     │
   │        ISaveStore  ─────────────────────────────────────────┐ (Game iface)  │
   │         NativeSaveStore : FileStream + Flush(true)           │               │
   │         WebGlSaveStore  : write + IDBFS sync request         │ per-platform  │
   └────────────┼────────────────────────────────────────────────┼──────────────┘
                │ (never sees engine types)                       │ File.* / IDBFS
                ▼
   ┌───────────────────────────────────────────────────────────────────────────┐
   │  SweetCascade.Domain  (PURE C#, noEngineReferences — LOW engine risk)        │
   │  SaveModel (facade)                                                          │
   │   • SaveSerializer = CanonicalJsonWriter + SaveJsonReader                    │
   │   • Fnv1a32.Compute(bytes)                                                   │
   │   • Validate() → S1–S8 + §7 tamper classification                           │
   │   • Merge() Formula 2 · RecomputeTotalStars() Formula 3                      │
   │   • SelectPrimary()/NextTarget() Formula 4                                   │
   │  Profile / Settings / LevelRecord  (records; snake_case JSON via explicit map)│
   └───────────────────────────────────────────────────────────────────────────┘
                              ▲ referenced by
                    SweetCascade.Domain.Tests (Edit Mode, NUnit, headless)
```

### Key Interfaces

Domain schema types (records; wire field names are snake_case, mapped explicitly in the codec —
no reflection, no naming-policy config that could drift). **`best_score` is `long`** (widened
per architecture §2 residue sweep — scoring is 64-bit). `checksum`, `integrity_flag`, and
`recovery_notice_needed` are handled specially (see notes).

```csharp
// SweetCascade.Domain/Save
public sealed record Profile
{
    public int SchemaVersion { get; init; }                 // "schema_version"
    public ulong WriteCounter { get; init; }                // "write_counter"  (uint64)
    public uint Checksum { get; init; }                     // "checksum"       (uint32) — see note
    public long CreatedUtc { get; init; }                   // "created_utc"
    public long ModifiedUtc { get; init; }                  // "modified_utc"
    public int TotalStars { get; init; }                    // "total_stars"    — recomputed on write
    public Settings Settings { get; init; }
    public IReadOnlyDictionary<string, LevelRecord> LevelRecords { get; init; } // "level_records"
}

public sealed record Settings(                              // all bool, table order fixed
    bool MusicEnabled, bool SfxEnabled, bool HapticsEnabled,
    bool ReducedMotionEnabled, bool ColorblindAssistEnabled);

public sealed record LevelRecord(
    int BestStars, long BestScore, int CompletionCount,     // best_score widened int→long
    long? FirstCompletedUtc, long? LastCompletedUtc);       // null when absent

// Transient load annotations — NEVER serialized to disk (not JSON fields):
public sealed record LoadResult(
    Profile Profile, LoadStatus Status,
    bool IntegrityFlag,            // §7 detect-and-accept: S4 failed but structurally valid
    bool RecoveryNoticeNeeded);   // §6 rung 3: both slots invalid → fresh profile + notice
public enum LoadStatus { LoadedTrusted, LoadedAccepted, FreshFirstLaunch, FreshAfterCorruption }

// The pure Domain facade (no IO, no engine types):
public interface ISaveModel
{
    byte[] SerializeForDisk(Profile p);   // canonical bytes, checksum embedded at index 3
    SlotDecode Decode(byte[]? rawSlot);   // parse + S1–S8 + §7 classification for ONE slot
    LoadResult ResolveLoad(SlotDecode a, SlotDecode b);      // §6 ladder + Formula 4 + Formula 3
    Profile Merge(Profile current, string levelId, int stars, long score, long nowUtc); // Formula 2
    uint Checksum(byte[] payloadExcludingChecksum);          // Formula 1 (FNV-1a-32)
}

// The IO seam — lives in Game, never in Domain. Bytes in, bytes out, plus a durable flush.
public interface ISaveStore
{
    byte[]? ReadSlot(Slot slot);          // null if the file is absent
    bool WriteSlotDurable(Slot slot, byte[] bytes); // write + platform flush; false on IO failure
}
public enum Slot { A, B }
```

**Checksum-field handling (faithful to Formula 1).** The writer builds canonical bytes in two
passes: pass 1 emits every field **except** `checksum` (fields in §2 order:
`schema_version, write_counter, created_utc, modified_utc, total_stars, settings,
level_records`) → FNV-1a over those bytes yields the checksum; pass 2 emits the **on-disk**
bytes with `checksum` inserted at its §2 position (index 3, right after `write_counter`). To
verify S4 on load, the reader parses the file into a `Profile`, re-runs pass 1 over the parsed
fields, hashes, and compares to the stored `checksum`. `checksum` is therefore never part of its
own hashed input.

**Canonical byte contract (frozen; changing it is a schema-version event).**
- Encoding: UTF-8, no BOM. Compact: no spaces, no newlines, no indentation.
- Object member order is **positional and fixed**, never alphabetical:
  top-level `schema_version, write_counter, checksum, created_utc, modified_utc, total_stars,
  settings, level_records`; `settings` = `music_enabled, sfx_enabled, haptics_enabled,
  reduced_motion_enabled, colorblind_assist_enabled`; each `LevelRecord` = `best_stars,
  best_score, completion_count, first_completed_utc, last_completed_utc`.
- `level_records` entries are emitted **sorted ascending by `level_id` using ordinal
  (`StringComparer.Ordinal`) comparison** — NOT culture-aware — so ordering is byte-stable
  across locales (GDD Formula 1 requires the sort; ordinal makes it deterministic).
- Numbers: `ulong`/`uint`/`long`/`int` via `ToString(CultureInfo.InvariantCulture)` — plain
  ASCII decimal digits, no group separators, no sign (all values are non-negative). **No floats
  exist**, so no `"R"`/`"G17"` round-trip formatting question ever arises.
- Booleans: lowercase `true` / `false`. Absent optional timestamps: literal `null`.
- Strings (`level_id` keys): JSON-escaped per RFC 8259; in practice ASCII, but the writer/reader
  implement standard escaping for robustness.

```csharp
// FNV-1a-32 (Formula 1) — unchecked wrapping uint arithmetic.
public static uint Compute(ReadOnlySpan<byte> payload)
{
    const uint OffsetBasis = 0x811C9DC5u; // 2,166,136,261
    const uint Prime       = 0x01000193u; // 16,777,619
    uint hash = OffsetBasis;
    unchecked { foreach (byte b in payload) { hash ^= b; hash *= Prime; } }
    return hash;   // Compute([0x61]) == 3_826_002_220  (0xE40C292C) — regression-pinned
}
```

**A/B write → flush → read-back-verify sequence** (SaveService, Game; called on every triggered
save with `dirty == true`):
1. `target = SaveModel.NextTarget(primary)` (Formula 4 — the non-primary slot).
2. In Domain: increment `write_counter`, set `modified_utc = now`, recompute `total_stars`
   (Formula 3), `bytes = SaveModel.SerializeForDisk(profile)` (checksum embedded). *(Pure — no IO.)*
3. `ISaveStore.WriteSlotDurable(target, bytes)`:
   - **Native (iOS/Android/desktop):** open `FileStream`, write all bytes, `stream.Flush(true)`
     (`flushToDisk: true` — forces OS buffers to physical storage; this is the fsync-equivalent
     the GDD's Open Question asked for), then dispose/close.
   - **WebGL:** write bytes to the virtual FS, then request an IDBFS→IndexedDB sync (see
     Degraded Path). Never rename (the scheme depends on no rename primitive).
4. **Read-back-verify:** SaveService immediately `ReadSlot(target)` and runs `SaveModel.Decode`
   on the bytes it just wrote. Only if the read-back **validates** (S1–S8 pass, checksum matches)
   is the write trusted: `dirty = false`, and `target` is now (by its higher `write_counter`) the
   primary. If read-back fails, the write is treated as failed: `dirty` stays `true`, the
   untouched other slot remains authoritative, and the next trigger retries. This closes the gap
   where `Flush(true)` returns but the bytes did not durably land (full disk, eviction mid-write).
5. There is **no swap step** — "which slot is primary" is derived from the two slots' own
   `write_counter`s at the next load (Formula 4). No pointer file is mutated, so there is no
   second write to leave inconsistent.

**WebGL degraded path (GDD §9 — one logic path, platform-branched flush only).** The schema,
A/B algorithm, checksum, corruption ladder, and tamper policy run **unmodified** on WebGL; the
only fork is inside `ISaveStore`. `WebGlSaveStore` writes to the IDBFS-backed
`persistentDataPath` and requests a sync to IndexedDB; because that sync is not guaranteed
synchronous, WebGL durability is **best-effort** — an immediate tab-close after a write may lose
the just-written slot (never the previously-good one — the untouched slot is unaffected). The
Game layer additionally triggers the IDBFS sync at the same `OnApplicationPause`/focus-lost /
`beforeunload` points the GDD already uses for `FlushIfDirty()`, so the pause flush doubles as
the WebGL commit point. Eviction of both slots remains an **accepted, unmitigated** MVP risk,
indistinguishable from first launch (GDD §6 rule 4 / §9): fresh profile, no recovery notice. The
real fix is Phase 3 cloud sync, not local-storage engineering now.

### Implementation Guidelines

- Put the codec in `Assets/Domain/Save/` (`SaveModel.cs`, `CanonicalJsonWriter.cs`,
  `SaveJsonReader.cs`, `Fnv1a32.cs`, `Profile.cs`, `Settings.cs`, `LevelRecord.cs`). Put
  `SaveService.cs`, `ISaveStore.cs`, `NativeSaveStore.cs`, `WebGlSaveStore.cs` in
  `Assets/Game/Save/`. This matches architecture §5.3.
- All GDD constants are data-driven, never scattered literals (GDD AC): expose
  `SETTINGS_SAVE_DEBOUNCE_MS = 750`, `CHECKSUM_ALGORITHM_VERSION = "fnv1a-32-v1"`,
  `CURRENT_SCHEMA_VERSION = 1`, `SUPPORTED_SCHEMA_VERSIONS = {1}`, `SLOT_COUNT = 2`, and the two
  FNV-1a constants from one Domain config location.
- Domain cannot call `UnityEngine.Debug.Log`. The reader surfaces unknown-field warnings by
  returning them in a `SlotDecode.Warnings` list (or via an injected `ILogSink`); the Game layer
  forwards them to the Unity console. This preserves purity and keeps warnings testable.
- `SaveModel` is pure and injected into `SaveService` (constructor injection, per
  coding-standards dependency-injection rule) so both are independently unit-testable.
- Migration (`schema_version` older than current): an ordered pipeline of pure
  `MigrateV{n}ToV{n+1}(...)` functions in Domain; none exist yet (current == 1). The
  fully-migrated result is written via the normal §4 algorithm so cost is paid once per device.
- **Unknown-field behaviour must be tested and documented.** On load, the reader **skips**
  unrecognized keys at any object level and never fails a structural rule over them (GDD §8 row
  1). Because S4 verification re-serializes only *known* fields (Formula 1's model), a same-version
  file that carries an unknown additive field will produce an S4 checksum mismatch and therefore
  load via the **tamper-accept** ladder (loads fully, `integrity_flag = true`, unknown field
  dropped on the next write). This is defined, safe behaviour: the true cross-version path is a
  `schema_version` bump + migration (which re-stamps a correct checksum); the unknown-field-without-bump
  case is only reached by an *older* build reading a *newer* build's file (a downgrade), where
  accept-with-flag + drop-on-rewrite is exactly the intended graceful degradation. (If post-launch
  telemetry ever shows this path is common, the clean upgrade is a raw-byte-range checksum that
  excises only the `checksum` member — noted in Risks, not adopted now.)

## Alternatives Considered

### Alternative 1: `UnityEngine.JsonUtility`

- **Description**: Unity's built-in field-based JSON serializer.
- **Pros**: Zero setup, fast, ships with the engine.
- **Cons**: It is a `UnityEngine` type → **cannot be linked from the `noEngineReferences` Domain
  assembly**, which is where serialization logic must live (architecture §5, §12 Principle 1).
  Cannot serialize `Dictionary<string, LevelRecord>` (architecture §7.3), cannot serialize C#
  records/properties or nullable value types cleanly, and gives no control over field order or
  compactness (needed for canonical bytes).
- **Estimated Effort**: Low to call, but architecturally impossible for the Domain-purity rule.
- **Rejection Reason**: Disqualified on three independent grounds — engine-bound, no `Dictionary`,
  no canonical-order control. This is the clearest "no" of the four.

### Alternative 2: `System.Text.Json`

- **Description**: The modern .NET JSON stack (`Utf8JsonWriter`/`Utf8JsonReader` + source-gen).
- **Pros**: Excellent low-level byte-control API; source generation avoids reflection; pure managed
  (could live in Domain).
- **Cons**: **Not shipped with Unity** — requires manually vendoring the NuGet DLL chain
  (`System.Text.Json` + `System.Memory`, `System.Buffers`, `System.Runtime.CompilerServices.Unsafe`,
  `System.Text.Encodings.Web`, `Microsoft.Bcl.AsyncInterfaces`) and IL2CPP/AOT source-gen care.
  Its high-level `JsonSerializer` does **not** emit canonical order (member order follows metadata;
  dictionary order follows insertion, not sorted) — reaching byte-exact canonical output still
  requires hand-driving `Utf8JsonWriter` field-by-field, i.e. writing the same code as the
  hand-rolled option but on top of a vendored dependency. For a <40 KB file read once at boot, its
  performance advantage is irrelevant.
- **Estimated Effort**: Higher than hand-rolled once the canonical writer must be hand-driven anyway,
  plus DLL-vendoring + AOT + a library-approval entry.
- **Rejection Reason**: All of the byte-control work still falls to us; the dependency buys nothing
  for canonical bytes and adds integration surface and a checksum-drift vector on library upgrade.

### Alternative 3: Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`)

- **Description**: The de-facto Unity JSON library, available as an official UPM package; handles
  `Dictionary` natively; pure managed (referenceable from Domain).
- **Pros**: Battle-tested, `Dictionary`-capable, tolerant reader for free, low install friction (it
  is often already present transitively).
- **Cons**: Default output order follows reflection/insertion and is **not contractually stable
  across versions**; dictionary keys are unsorted; to get canonical bytes you must add custom
  `JsonConverter`s / a `ContractResolver` that force field order, ordinal-sorted keys, compact
  formatting, and `InvariantCulture` numbers — at which point the library is doing almost nothing
  the hand-rolled writer wouldn't, while a future Newtonsoft upgrade that changes a formatting
  default would silently break every stored checksum. Reflection-based (de)serialization also needs
  IL2CPP AOT preservation (`[Preserve]`/link.xml).
- **Estimated Effort**: Comparable to hand-rolled after the custom converters, plus a dependency.
- **Rejection Reason**: It ties the **checksum contract** — a durability guarantee — to a
  third-party library's formatting defaults and version drift, for a schema small enough that we can
  own every byte. **Retained as the sanctioned fallback for the READ path only** if the bespoke
  reader proves too costly; the write/checksum path stays hand-rolled unconditionally.

### Alternative 4 (Chosen): Hand-rolled canonical codec in Domain

- **Description**: A purpose-built `CanonicalJsonWriter` + tolerant `SaveJsonReader` in Domain that
  directly implement the §2 field order, ordinal key sort, compact formatting, and Formula 1 hash.
- **Pros**: The checksum can never drift (we own every byte); zero dependency (only BCL) keeps Domain
  genuinely pure; no reflection → no IL2CPP/AOT concerns; maximally testable (byte-exact golden
  file); the integer-only schema makes the writer small and defect-resistant; explicit snake_case
  mapping is self-documenting and frozen by `schema_version`.
- **Cons**: We write and maintain ~a few hundred lines of codec (a JSON tokenizer for the reader is
  the bulk). We own JSON string-escaping and parsing correctness rather than delegating it.
- **Estimated Effort**: Moderate, front-loaded, but the smallest *total* effort once every other
  option must hand-drive canonical output anyway.
- **Why chosen**: It is the only option that satisfies the byte-exact-forever constraint *and* the
  Domain-purity rule *and* the zero-dependency ideal simultaneously, for a schema whose small,
  closed, integer-only shape is exactly the case where a bespoke codec beats a general library.

## Consequences

### Positive

- The FNV-1a checksum contract is immune to library version drift — the durability guarantee rests
  on code we own and golden-file-pin.
- `SweetCascade.Domain` stays dependency-free (BCL only); the `noEngineReferences` CI guard and
  Principle 1 hold with no third-party carve-out.
- No reflection anywhere in the save path → no IL2CPP AOT stripping surprises on iOS/Android.
- The serialize/checksum/validate/merge logic is fully headless-testable in Edit Mode with no scene,
  no engine, no disk (fixtures excepted) — directly serving the GDD's blocking AC list.
- One save logic path across iOS/Android/WebGL; only the flush primitive is platform-branched,
  honoring GDD §9's "no platform-specific save code paths" intent.

### Negative

- We own JSON correctness (escaping, tokenizing, malformed-input handling) rather than delegating to
  a hardened library — mitigated by fixtures for malformed/truncated/escaped inputs and by the schema
  being tiny and closed.
- Adding a genuinely new *shape* later (e.g. a nested array field for Phase 2 brewing inventory)
  means extending the hand-rolled codec, not just annotating a POCO — an accepted, bounded cost that
  the additive-migration discipline (§8) already frames.
- The unknown-field-without-version-bump forward case routes through tamper-accept (documented above),
  which is safe but sets `integrity_flag` on that load — acceptable at MVP, flagged for a future
  raw-byte checksum if it becomes common.

### Neutral

- Wire field names stay snake_case (GDD §2) while C# members are PascalCase; the codec maps
  explicitly. Human-inspectable for QA/bug triage, as the GDD intends.
- `best_score` is `long` on the wire as a plain JSON integer — no format change, just a wider range.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| A future codec edit silently changes canonical bytes → all stored checksums invalidate | Low | High | Byte-exact **golden fixture** (`canonical_golden_v1.sav`) fails CI on any output change; `CHECKSUM_ALGORITHM_VERSION` tag gates deliberate changes. |
| WebGL IDBFS sync is async → an immediate tab-close loses the just-written slot | Medium | Low–Med | Read-back-verify + pause/`beforeunload` sync trigger; previously-good slot is never at risk; eviction already an accepted MVP risk (GDD §9). |
| `FileStream.Flush(true)` does not force physical sync under a given IL2CPP/OS combo | Low | Medium | Read-back-verify catches a non-durable write before trusting it; listed under Verification Required. |
| Hand-rolled JSON reader mishandles an escaped/edge input | Low | Medium | Escaping/truncation/nesting fixtures; the schema is closed so the input space is bounded. |
| Unknown-field forward-compat trips `integrity_flag` more than expected | Low | Low | Documented, safe (loads via tamper-accept); raw-byte-range checksum is the noted upgrade if telemetry shows it. |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | 0 ms | ~0 ms (serialize + FNV-1a over ~18 KB is sub-ms; runs on win/settings/pause, never per-frame) | 16.6 ms — not on the hot path |
| Memory | 0 MB | <40 KB on disk (Formula 5); in-memory profile trivial | ≤400 MB |
| Load Time | — | One <40 KB file read + parse at boot (disk read exactly once/session) | negligible vs. boot |
| Network | n/a | n/a (offline at MVP) | n/a |

## Migration Plan

New system — nothing to migrate. `schema_version` starts at `1`; `SUPPORTED_SCHEMA_VERSIONS = {1}`;
no `MigrateV*` functions exist yet.

1. Land Domain codec (`SaveModel`, writer, reader, `Fnv1a32`, POCOs) + Edit Mode fixtures/tests —
   verify against the golden file and Formula 1 vector.
2. Land Game `SaveService` + `ISaveStore` (`NativeSaveStore`, `WebGlSaveStore`) + Play Mode
   save-round-trip test; wire boot `LoadProfile`, win `RecordLevelCompletion`, pause `FlushIfDirty`.
3. On the first future schema change: add `MigrateV1ToV2`, bump `CURRENT_SCHEMA_VERSION`, extend
   `SUPPORTED_SCHEMA_VERSIONS`, add a `valid_v1→v2` migration fixture.

**Rollback plan**: the codec is engine-neutral pure C# — if the hand-rolled approach proves costlier
than expected, the write/checksum path stays hand-rolled (non-negotiable for the byte contract) while
the *reader* is swapped to Newtonsoft behind `SaveJsonReader`'s interface, with no change to `Profile`,
`SaveService`, or the on-disk format. No stored data is affected.

## Validation Criteria

- [ ] `CanonicalJsonWriter` output for a fixed known `Profile` is byte-identical to
  `canonical_golden_v1.sav` (pins the frozen byte contract).
- [ ] `Fnv1a32.Compute([0x61])` == `3826002220` (Formula 1 worked-example regression pin).
- [ ] Round-trip: a populated `Profile` serialized then deserialized is field-for-field identical
  (excluding the deliberately-incremented `write_counter`).
- [ ] `level_records` serialize in ordinal-sorted key order regardless of insertion order.
- [ ] Serialization under a comma-decimal locale (e.g. `de-DE`) still emits ASCII decimal digits.
- [ ] Well-formed checksum mismatch (S4 only) loads with `integrity_flag = true` and does **not**
  fall back to the other slot; structural+checksum failure is rejected.
- [ ] `schema_version: 999` fails S1 → slot invalid; an unknown additive field loads (skipped +
  warned), not rejected.
- [ ] Interrupted write (mocked partial `ISaveStore.WriteSlotDurable`) leaves the other slot fully
  valid and primary on the next load (Play Mode / SaveService).
- [ ] Re-profiled: no measurable frame-time impact from a save during a cascade (save is off the hot
  path).

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/save-persistence.md` §1 | Save & Persistence | Compact UTF-8 JSON, byte-identical canonical bytes, never `.tres` | Hand-rolled `CanonicalJsonWriter` emits compact UTF-8 with frozen positional field order + ordinal-sorted keys + invariant numbers → byte-reproducible. |
| §2 (schema v1) | Save & Persistence | Exact field set/types; `Dictionary<level_id, LevelRecord>`; `total_stars` denormalized | `Profile`/`Settings`/`LevelRecord` records with explicit snake_case mapping; `Dictionary` serialized directly (impossible in `JsonUtility`); `best_score` widened to `long`. |
| Formula 1 | Save & Persistence | FNV-1a-32 over canonical bytes except `checksum` | `Fnv1a32.Compute` with the specified constants; two-pass writer excludes `checksum` from its own hash; `[0x61]→3826002220` pinned. |
| §4 / Formula 4 | Save & Persistence | A/B double-buffer, no rename dependency, self-healing rotation | Two `.sav` files at `persistentDataPath`; `NextTarget` writes the non-primary slot; primary derived from `write_counter`; no pointer file. |
| §4 / §5 / Edge Cases | Save & Persistence | OS-kill safety; a completed write must be trustworthy | write → `Flush(true)` (native) → **read-back-verify** before trusting; untouched slot never at risk. |
| §5 / §6 / §7 | Save & Persistence | S1–S8, corruption ladder, detect-and-accept tamper policy | `SaveModel.Decode` runs S1–S8 and classifies; `ResolveLoad` implements the ladder + `integrity_flag`/`recovery_notice_needed` as transient (non-persisted) `LoadResult` fields. |
| §8 | Save & Persistence | Additive migration; unknown-field tolerance; future-version rejection | Reader skips unrecognized keys (never fails structurally) + warns; older `schema_version` → ordered pure migrations; newer → S1 invalid. Forward-compat consequence documented. |
| §9 | Save & Persistence | WebGL degraded guarantee; one code path | Identical logic on WebGL; only the flush primitive is branched inside `ISaveStore`; eviction accepted as first-launch-equivalent. |
| Formula 2 / Formula 3 | Save & Persistence | Monotonic merge; self-healing `total_stars` | `SaveModel.Merge` (max stars/score, count+1, first/last utc) and `RecomputeTotalStars` on every write + re-validate on load. |
| Acceptance Criteria | Save & Persistence | Deterministic Edit Mode tests, data-driven constants | Fixtures + Edit Mode test list below; all GDD constants sourced from one Domain config location. |
| `architecture.md` §5/§7.3/§12 | Master architecture | Serialization logic in Domain, IO in Game; `noEngineReferences` | Codec in `SweetCascade.Domain` (BCL only); `SaveService`/`ISaveStore` file IO in `SweetCascade.Game`. |

## Edit Mode Test Fixtures the Format Must Ship With

Fixtures live at `src/SweetCascade/Assets/Tests/EditMode/Save/Fixtures/` (the GDD's logical name
`tests/fixtures/save-persistence/valid_v1_profile.json` maps here):

| Fixture | Purpose |
|---|---|
| `canonical_golden_v1.sav` | Byte-exact golden serialization of a fixed known `Profile` — the anti-drift guardian of the checksum contract. |
| `valid_v1_profile.json` | Canonical, checksum-correct, non-default settings + multiple `level_records` (GDD-named). |
| `tamper_wellformed.json` | One `best_stars` hand-edited, checksum stale, S1/2/3/5/6/8 pass → accepted + `integrity_flag`. |
| `corrupt_structural.json` | Checksum mismatch **and** out-of-range/truncated field → rejected. |
| `future_schema_999.json` | `schema_version: 999` → S1 fail → invalid. |
| `unknown_field_additive.json` | Extra unrecognized top-level key → loads (key skipped + warned), not rejected. |
| `escaped_levelid.json` | A `level_id` key containing escapable characters → exercises writer/reader string escaping. |

Edit Mode tests (Domain, NUnit, headless — the codec-owned subset; broader SaveService behaviour
tests — slot alternation, interrupt, debounce, ladder — belong to the Save slice per the GDD AC):

- `test_canonical_bytes_match_golden` · `test_round_trip_fidelity` ·
  `test_checksum_matches_formula_1_worked_example` · `test_dictionary_key_ordinal_sort` ·
  `test_number_invariant_culture_under_de_DE` · `test_missing_optional_field_defaulted` ·
  `test_null_completion_timestamps_round_trip` · `test_unknown_field_tolerated_on_load` ·
  `test_tamper_wellformed_accepted_with_flag` · `test_corrupt_structural_rejected` ·
  `test_future_schema_version_rejected` · `test_escaped_levelid_round_trip`.

## Related

- **Implements**: `design/gdd/save-persistence.md` (governing design — schema, formulas, ladder,
  migration, WebGL policy). This ADR implements it; it does not re-design it.
- **Is the architecture's ADR-B**: `docs/architecture/architecture.md` §7.3, §11 (Required ADRs),
  §13 QQ-02/QQ-03; resolves the serializer + `persistentDataPath` durability open questions.
- **Depends on**: `docs/architecture/adr-001-engine-selection-unity.md` (Accepted).
- **Sibling Foundation ADRs**: ADR-A (Addressables grouping), ADR-C (deterministic RNG) — parallel,
  no shared files.
- **Verification source**: `docs/engine-reference/unity/VERSION.md`,
  `docs/engine-reference/unity/current-best-practices.md`.
- **Code (once implemented)**: `Assets/Domain/Save/*`, `Assets/Game/Save/*`,
  `Assets/Tests/EditMode/Save/*`.
