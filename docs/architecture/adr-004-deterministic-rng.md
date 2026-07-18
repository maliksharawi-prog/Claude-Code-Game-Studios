# ADR-004: Deterministic RNG Implementation & Domain-Purity CI Guard

## Status

Accepted

> Accepted 2026-07-18 (Technical Director). Consistent with the master architecture's
> deferred sign-off pattern: **founder review may amend** the concrete finalizer/generator
> choice (behind the `algorithm_version` tag) and the CI denylist surface. This ADR is the
> concrete realization of the master architecture's Required-ADR
> **"ADR-C — Deterministic RNG generator & domain-assembly determinism guard"**
> (`docs/architecture/architecture.md` §11), renumbered ADR-004 in this repository. It
> discharges TD-ARCHITECTURE **Condition 1** (§10.3) and resolves Open Question **QQ-04**
> (§13). It **implements** `design/gdd/rng-service.md`; it does not re-design it — every
> formula, stream ID, API operation, and Honest-Randomness guarantee below traces to that GDD.

## Date

2026-07-18

## Last Verified

2026-07-18

## Decision Makers

Technical Director (author, owns the finalizer/generator selection and the purity guard);
Founder (amend rights on the algorithm choice and denylist); consulted:
`godot-gdscript-specialist` → `unity-specialist` (C# implementation idioms),
`performance-analyst` (draw-cost budget), `systems-designer` (GDD fidelity check).

## Summary

Sweet Cascade's entire Pillar-2 "Clever, Never Cheated" guarantee rests on board randomness
being **byte-identical across iOS/Android/WebGL and fully replayable from a logged seed** —
which `System.Random` cannot provide (its algorithm is not stable across .NET runtimes) and
which no engine RNG can provide either. This ADR fixes the exact PRNG (a **SplitMix32**
stream generator plus a **MurmurHash3 `fmix32`** finalizer, with every constant, type, and
operation order spelled out so two independent implementations agree bit-for-bit),
implements the `rng-service.md` §6 API 1:1 inside the engine-free `SweetCascade.Domain`
assembly, replaces the float-formula core path with an exact integer equivalent for
cross-platform stability, and enforces the purity contract with a **three-layer guard**
(asmdef `noEngineReferences`, a CI denylist scan, and a checked-in golden-vector regression
suite run under Mono + IL2CPP + WebGL).

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 6.3 LTS (6000.3.x) — per ADR-001 |
| **Domain** | Core / Scripting (pure-C# Foundation logic — `SweetCascade.Domain`, zero engine surface) |
| **Knowledge Risk** | LOW — the entire decision is standard C# / .NET BCL integer arithmetic with **zero** `UnityEngine` API. This is deliberate: the RNG is the most determinism-critical and least engine-coupled code in the project, and is placed where engine-version drift cannot reach it. |
| **References Consulted** | `design/gdd/rng-service.md` (governing GDD), `docs/architecture/architecture.md` §1 (determinism caveat), §5 (assembly boundary), §6 (RngService ownership), §10.3 (TD Condition 1), §11 (ADR-C), §13 (QQ-04); `.claude/docs/technical-preferences.md` (forbidden patterns, determinism rule); `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | **None.** The Domain assembly references no `UnityEngine`/`UnityEditor` type by construction. Cross-platform determinism depends only on the C# language spec (integer overflow wraps mod 2^n in `unchecked`; `>>` on `uint` is logical) and IEEE-754 `double` for the one non-core float helper — both stable across Mono, IL2CPP (C++), and the WebGL/WebAssembly backend. |
| **Verification Required** | The golden-vector Edit-Mode suite must pass **byte-for-byte on all three scripting backends**: Mono (editor), IL2CPP (standalone player build), and WebGL (in-browser). The suite is the standing regression gate; the three-backend run is the acceptance proof for TR-rng-002. |

> **Note**: Knowledge Risk is LOW and this ADR does **not** need re-validation on an
> engine-version bump — that is the point of housing it in the engine-free Domain. It
> would only need revisiting if the .NET language semantics for `unchecked` integer
> overflow or `uint` shifts changed, which the C# spec fixes.

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-001 (Engine Selection — Unity 6.3 LTS, Accepted) for the C#/asmdef toolchain and the `SweetCascade.Domain` assembly boundary it establishes. Soft dependency on **ADR-E (Level manifest generation)** for the resolved integer `level_id` ordinal that `StartLevelSession` consumes — RNG accepts the ordinal as an opaque `int` and does **not** require ADR-E to be Accepted first (it is dependency-free per `rng-service.md` Dependencies). |
| **Enables** | The Match-3 Board Engine slice (bootstrap fill, gravity/refill, no-valid-moves reshuffle all draw the `board-refill` stream), the Special Candies slice (reserved `special-drop` stream), and **every deterministic Edit-Mode test in the project** — the seedable RNG is what makes the Domain's board/scoring/objective tests reproducible. |
| **Blocks** | Board Engine epic and Special Candies epic cannot begin implementation until this ADR is Accepted (both compile against `IRngService`); the project's determinism test harness cannot be authored without the golden-vector fixture format defined here. |
| **Ordering Note** | Foundation ADR — should land first (or jointly) among ADR-002 (Addressables) / ADR-003 (Save serialization) / this ADR-004, because Board Engine bootstrap is the earliest gameplay slice and it needs a seeded `board-refill` stream on frame one. The FNV-1a-32 label hash defined here (`ForkStream`) uses the **same** FNV-1a constants as the Save serialization checksum (ADR-003 / save-persistence.md), deliberately, so both foundations share one verified hash primitive. |

## Context

### Problem Statement

`rng-service.md` promises three player-facing guarantees — honest odds, provable fairness
(replay any board from a logged seed), and identical daily-challenge boards for every player.
All three are only real if the random sequence produced from a given seed is **identical on
every device the game ships to**. The master architecture (§1, §2, §11) flags loudly that
the blueprint's placeholder `new System.Random(seed)` **cannot deliver this**: `System.Random`'s
internal algorithm is an implementation detail that is **not guaranteed byte-stable across
.NET runtimes** (it has in fact changed between .NET Framework, Mono, and .NET Core/5+), so a
seed that reproduces a board under the editor's Mono runtime may produce a *different* board
under an IL2CPP iOS build or the WebGL WebAssembly runtime. That silently breaks daily-challenge
fairness and every "replay this exact board" bug-repro — the mechanical backbone of Pillar 2.

The decision must be made **now** because the Match-3 Board Engine is the first gameplay slice
and it draws the `board-refill` stream on its very first bootstrap fill; there is no board without
a seeded, deterministic generator. Deferring means either blocking the Board Engine or letting it
build against the forbidden placeholder and rewriting it later.

### Current State

- The visual/interface blueprint seeds the Domain layer with a **spec-by-example**
  `new Random(seed)` placeholder (architecture §1, §2). It is explicitly a placeholder, not a
  decision — the "Godot → Unity residue sweep" (§2) lists replacing it as one of only two
  mandatory source edits on port.
- `rng-service.md` fully specifies the *design*: stream model & isolation (§1), the append-only
  stream registry (§2), the seed lifecycle (§3), the Honest Randomness Contract (§4), refill
  distribution (§5), the API surface (§6), and Formulas **F1–F6** — but it deliberately leaves
  the concrete `mix32` finalizer and the per-call generator as "a lead-programmer/technical-director
  implementation decision, version-tagged via `algorithm_version`."
- The assembly boundary exists on paper (architecture §5): `SweetCascade.Domain` is to carry
  `noEngineReferences` and be CI-guarded, but the guard's mechanics are unspecified. `System.Random`
  would compile cleanly inside a `noEngineReferences` assembly — the asmdef flag blocks
  `UnityEngine`, not the BCL — so a second, independent guard is required.

### Constraints

- **Cross-runtime byte-stability** is a hard requirement: same seed ⇒ same sequence on Mono
  (editor), IL2CPP (iOS/Android C++ backend), and WebGL (WebAssembly). This is TR-rng-002.
- **Engine purity**: the generator must live in `SweetCascade.Domain` with **zero** `UnityEngine`
  references (architecture §5, §12 Principle 1; technical-preferences.md forbidden pattern).
- **Single-threaded**: no concurrency model is permitted (rng-service.md Edge Cases; board resolves
  single-threaded). Draw order alone determines the sequence.
- **GDD-fixed constants**: F1/F2/F3 pin `K1 = 2,654,435,761` and `K2 = 40,503` and the
  `combine()` shape `(a·K1 + b·K2) mod 2^32`. This ADR must not alter those; the worked examples
  in the GDD (`combine(1007,3)=1,547,274,724`, etc.) must remain reproducible.
- **API shape is fixed by the GDD §6** — `start_*_session`, `next_float/int/color`, `shuffle`,
  `fork_stream`, `get_session_log`. This ADR maps them 1:1 to C#, it does not add or remove operations.
- **Draw-count discipline**: F6 (Fisher–Yates) requires shuffle to consume **exactly**
  `length − 1` draws; any generation method that sometimes consumes a variable number of raw
  draws per `next_int` would break that contract and the GDD's worked example.

### Requirements

- **TR-rng-001** — named, independently-seeded streams; drawing on stream A never perturbs stream B.
- **TR-rng-002** — platform-stable seed derivation (F1–F3 + avalanche finalizer), byte-identical
  across iOS/Android/Web; **not** `System.Random`.
- **TR-rng-003** — the full §6 API: `next_float/int/color`, `shuffle`, `fork_stream`,
  `get_session_log`.
- **TR-rng-004** — `level_id` supplied as a resolved **int** ordinal, never a runtime string-hash.
- **TR-perf-002** — Domain assembly carries zero `UnityEngine`; CI-guarded.
- **Non-functional** (rng-service.md Edge Cases) — one stream must support ≥ 10^9 draws with no
  measurable statistical degradation; filling a 64-cell board plus a worst-case cascade refill
  (≤ 128 draws) must complete in < 1 ms on target mid-range mobile.

## Decision

Adopt a **fully-specified, integer-first, engine-free RNG** built from two primitives — a
`Mix32` avalanche finalizer and a `SplitMix32` stream generator — implemented in
`SweetCascade.Domain.Rng`, with a three-layer purity guard. Every constant and operation
order below is normative: an implementation is correct **iff** it reproduces the checked-in
golden vectors bit-for-bit.

### Architecture

```
                         SweetCascade.Domain  (asmdef: noEngineReferences: true)
                         ┌───────────────────────────────────────────────────────────┐
 Board Engine ─────────► │  IRngService                                               │
 (board-refill)          │   ├─ StartLevelSession(int lvl, int attempt) ── F1 ──┐     │
 Special Candies ──────► │   ├─ StartDailySession(int chal, int dateUtc) ─ F2 ──┤     │
 (special-drop, reserved)│   ├─ StartTestSession(uint masterSeed) ───────────────┤     │
 Tests ────────────────► │   │                                        master_seed ▼    │
                         │   │                        per registry stream_id ─► F3 ──► stream_seed
                         │   ├─ NextFloat / NextInt / NextColor / Shuffle           │  │
                         │   ├─ ForkStream(parent, label)  (FNV-1a-32 of label)     │  │
                         │   └─ GetSessionLog() ─► RngSessionLog (ids, seed, ver, ts)│  │
                         │                                                           │  │
                         │  ── primitives (normative) ────────────────────────────  │  │
                         │   Combine(a,b) = a·K1 + b·K2   (unchecked uint, mod 2^32) │  │
                         │   Mix32(x)     = MurmurHash3 fmix32   (uint, logical >>)  │  │
                         │   SplitMix32   state += GAMMA; return Mix32(state)        │  │
                         │   NextInt/Color = (uint)(((ulong)NextRaw()·range) >> 32)  │◄─┘
                         │       ↑ exact integer equivalent of floor(next_float·range)│
                         └───────────────────────────────────────────────────────────┘
                                    ▲ clock injected (IClock) — timestamp only, never a draw input
   SweetCascade.Game  ──────────────┘   (SystemClock impl reads DateTime.UtcNow HERE, not in Domain)

  Purity guard (three independent layers):
   L1  asmdef noEngineReferences:true  → UnityEngine/UnityEditor unresolvable (compile fails)
   L2  CI denylist scan (ripgrep)      → System.Random, new Random(, DateTime.Now, … (build fails)
   L3  golden-vector suite             → Mono + IL2CPP + WebGL byte-for-byte (test fails)
```

### Key Interfaces

The public surface maps 1:1 to `rng-service.md` §6 (C# naming per technical-preferences.md;
PascalCase members, past-tense events elsewhere). It lives in `SweetCascade.Domain.Rng`.

```csharp
namespace SweetCascade.Domain.Rng;

public interface IRngService
{
    // Seed lifecycle (rng-service.md §3) ──────────────────────────────────────────────
    void StartLevelSession(int levelId, int attemptNumber);           // master_seed = F1(...)
    void StartDailySession(int dailyChallengeId, int calendarDateUtc); // master_seed = F2(...)
    void StartTestSession(uint masterSeed);                            // direct seed; bypasses F1/F2

    // Draws (rng-service.md §6) ────────────────────────────────────────────────────────
    double NextFloat(string streamName);                              // [0,1). double, NOT float.
    int    NextInt (string streamName, int minValue, int maxValue);   // F4, inclusive [min,max]
    T      NextColor<T>(string streamName, IReadOnlyList<T> activeColors);          // F5
    T[]    Shuffle<T>(string streamName, IReadOnlyList<T> source);    // F6, returns a NEW array

    // Ad hoc streams & logging ─────────────────────────────────────────────────────────
    string        ForkStream(string parentStreamName, string label);  // child = (parent,label)
    RngSessionLog GetSessionLog();                                    // bug-repro record (§3)
}

public readonly record struct RngSessionLog(
    long   PrimaryId,               // level_id            OR daily_challenge_id
    long   InstanceId,              // attempt_number      OR calendar_date_utc
    uint   MasterSeed,
    string AlgorithmVersion,        // "v1" (rng-service.md Tuning Knobs)
    string SessionStartTimestampIso);   // filled from injected IClock (Game supplies UtcNow)

// Purity-preserving clock seam: keeps DateTime OUT of Domain (see Implementation Guidelines).
public interface IClock { string UtcNowIso(); }
```

**Registered streams** (`rng-service.md` §2, append-only — IDs are never reused/renumbered
because `stream_id` feeds F3):

```csharp
internal static class StreamRegistry            // Domain, const table
{
    public const int BoardRefill = 1;           // Active  (MVP) — Match-3 Board Engine
    public const int SpecialDrop = 2;           // Reserved (MVP infra) — Special Candies
    public const int Harvest     = 3;           // Reserved (Phase 2)
    public const int Events      = 4;           // Reserved (Phase 3)
    // string name → id, e.g. "board-refill" → 1. Every registered stream is sub-seeded at
    // session start via F3 whether or not it has a live consumer (zero cost, stable numbering).
}
```

### Implementation Guidelines

#### 1. The normative primitives (spell-out — two implementations agree bit-for-bit)

All variables are `uint` (unsigned 32-bit) unless noted; every arithmetic block is `unchecked`
(so `*` and `+` wrap mod 2^32 rather than throwing in a checked build); every `>>` is a
**logical** shift (guaranteed because the operands are `uint`, never `int`).

```csharp
// GDD-fixed constants (rng-service.md F1/F2/F3). DO NOT change without an algorithm_version bump.
const uint K1    = 0x9E3779B1u;   // 2,654,435,761  (Knuth ≈ 2^32/φ)   — GDD combine() term for a
const uint K2    = 0x9E37u;       //        40,503  (odd)              — GDD combine() term for b
// SplitMix / finalizer constants (this ADR's choice). NOTE: GAMMA ≠ K1 (0x…B9 vs 0x…B1).
const uint GAMMA = 0x9E3779B9u;   // 2,654,435,769  (odd Weyl increment)
const uint M1    = 0x85EBCA6Bu;   // 2,246,822,507  (MurmurHash3 fmix32)
const uint M2    = 0xC2B2AE35u;   // 3,266,489,909  (MurmurHash3 fmix32)
// FNV-1a-32 for ForkStream label hashing (SAME primitive as the save checksum, ADR-003).
const uint FNV_OFFSET = 0x811C9DC5u;  // 2,166,136,261
const uint FNV_PRIME  = 0x01000193u;  //    16,777,619

// combine(a,b) = (a·K1 + b·K2) mod 2^32     — F1, F2, F3 all use this exact form.
static uint Combine(uint a, uint b) { unchecked { return a * K1 + b * K2; } }

// Mix32 = MurmurHash3 fmix32 avalanche finalizer. This IS rng-service.md's `mix32`.
static uint Mix32(uint x)
{
    unchecked {
        x ^= x >> 16;
        x  = x * M1;
        x ^= x >> 13;
        x  = x * M2;
        x ^= x >> 16;
        return x;
    }
}
// Sanity anchor: Mix32(0) == 0 (fmix32 fixes zero). The full seed→sequence vectors are frozen
// in the golden fixture (below), generated by this reference code, never hand-computed here.

// SplitMix32 stream: state initialized to the F3 stream_seed; one raw 32-bit draw per call.
uint NextRaw(ref uint state) { unchecked { state += GAMMA; return Mix32(state); } }
```

Seed derivation is then literally the GDD formulas:

```csharp
// F1 — standard level attempt
uint MasterSeedLevel(int levelId, int attemptNumber)
    => Mix32(Combine((uint)levelId, (uint)attemptNumber));
// F2 — daily challenge (NO player-identifying/-performance input — Honest Randomness Contract §4)
uint MasterSeedDaily(int dailyChallengeId, int calendarDateUtc)
    => Mix32(Combine((uint)dailyChallengeId, (uint)calendarDateUtc));
// F3 — per-stream sub-seed (isolation guarantee): each registered stream_id → its own state
uint StreamSeed(uint masterSeed, int streamId)
    => Mix32(Combine(masterSeed, (uint)streamId));
```

`combine()` reproduces the GDD worked examples exactly under `uint` wraparound:
`Combine(1007,3) = 1,547,274,724`, `Combine(42,20650) = 653,539,216`,
`Combine(500,1) = 73,026,539` — these are the GDD-verified anchors the fixture cross-checks.

#### 2. Integer refinement of F4/F5 — the core path carries **no** float

`rng-service.md` F4/F5 express selection as `floor(next_float(stream) × range)` with a
floating-point boundary clamp. Implementing that literally reintroduces a float multiply and a
`floor` on the **hottest, most determinism-critical path** (`board-refill` on every cascade
step) — exactly where cross-runtime float behavior is least trustworthy. This ADR implements the
**exact integer equivalent** and treats it as a refinement of F4/F5, not a redesign:

For `next_float = raw / 2^32` (raw a uint32), `floor(next_float × range)` is *identically equal* to
`(raw × range) div 2^32 = (raw × range) >> 32` over non-negative integers (division by 2^32 is a
right-shift by 32; `raw × range < 2^64` fits a `ulong`). So:

```csharp
public int NextInt(string stream, int min, int max)
{
    if (min > max) throw new ArgumentException("min > max");       // GDD Edge Case: loud, no swap
    uint range = (uint)(max - min + 1);                            // range ≥ 1
    uint idx   = (uint)(((ulong)NextRaw(ref S(stream)) * range) >> 32);
    return min + (int)idx;                                         // exactly one raw draw consumed
}

public T NextColor<T>(string stream, IReadOnlyList<T> activeColors)
{
    if (activeColors.Count == 0) throw new ArgumentException("empty color pool"); // GDD Edge Case
    uint idx = (uint)(((ulong)NextRaw(ref S(stream)) * (uint)activeColors.Count) >> 32);
    return activeColors[(int)idx];
}
```

Two properties this buys, both stronger than the float formula:

- **The F4/F5 boundary clamp becomes structurally impossible.** Max `idx` is
  `((2^32−1)·range) >> 32 = range − 1`; the multiply-shift can never yield `range`. The GDD's
  "only silent clamp" and its `test_boundary_rounding_clamped` acceptance criterion are satisfied
  by construction (nothing to clamp), not by a runtime guard. Note this reframing in the test file.
- **Exactly one raw draw per `next_int`.** No rejection loop, so F6's "consumes `length − 1` draws"
  contract and worked example hold verbatim.

`NextFloat` still exists (some future caller may want a raw uniform double) and is defined
byte-stably in `double` — `raw × 2^-32` is exact in IEEE-754 `double` because `raw < 2^32 ≤ 2^53`
and the scale is a power of two — but it is **not** on the board-refill path:

```csharp
public double NextFloat(string stream) => NextRaw(ref S(stream)) * (1.0 / 4294967296.0); // 2^-32
```

`Shuffle` is F6 verbatim (copy-in, Fisher–Yates high-to-low, `next_int(0,i)` per step, returns the copy):

```csharp
public T[] Shuffle<T>(string stream, IReadOnlyList<T> source)
{
    var r = new T[source.Count];
    for (int k = 0; k < r.Length; k++) r[k] = source[k];          // never mutate the input
    for (int i = r.Length - 1; i >= 1; i--)
    {
        int j = NextInt(stream, 0, i);                            // one raw draw each
        (r[i], r[j]) = (r[j], r[i]);
    }
    return r;
}
```

#### 3. `ForkStream` — deterministic, idempotent, string-hash-portable

The child stream is derived from the parent's **initial** `stream_seed` (not its current advanced
state) so a fork is independent of how many draws the parent has already consumed — this is what
makes `test_fork_stream_deterministic` hold regardless of fork timing. The label is hashed with
**FNV-1a-32 over its UTF-8 bytes** (deterministic and portable; the C# BCL `string.GetHashCode()`
is randomized per-process and is FORBIDDEN here — see §4). Idempotency is a session-scoped
dictionary keyed by child name.

```csharp
public string ForkStream(string parent, string label)
{
    string child = parent + "/" + label;
    if (_streams.ContainsKey(child)) return child;                // idempotent: same live stream
    uint h = FNV_OFFSET;
    foreach (byte b in System.Text.Encoding.UTF8.GetBytes(label)) // UTF-8, byte-canonical
        unchecked { h ^= b; h *= FNV_PRIME; }
    uint childSeed = Mix32(Combine(_streams[parent].InitialSeed, h));
    _streams[child] = new StreamState(childSeed);
    return child;
}
```

#### 4. Cross-platform stability — C# constructs REQUIRED and FORBIDDEN in the core path

The core path is integer-only precisely so the platform-variable parts of floating point never
apply. Enforce the following (the CI guard in §6 mechanizes the first two rows; the rest are
review + golden-vector-enforced):

| Rule | Reason |
|---|---|
| **FORBIDDEN: `System.Random` / `new Random(...)`** | Algorithm not byte-stable across .NET runtimes — the entire reason for this ADR (architecture §1, §2, QQ-04). |
| **FORBIDDEN: `UnityEngine.*` / `UnityEditor.*`** including `UnityEngine.Random`, `Mathf`, `Time` | Domain purity (architecture §5, §12-1); engine RNG is also not cross-runtime-pinned. |
| **FORBIDDEN in the RNG core path: 32-bit `float`, `Mathf`/`MathF`, `Math.Floor` on the draw path** | 32-bit float ops and library `floor` are the least portable across Mono/IL2CPP/WebGL; the integer multiply-shift removes them entirely. (`NextFloat` uses `double`, and is off the board path.) |
| **FORBIDDEN: FMA / `-ffast-math`-style contraction reliance** | We never write `a*b+c` on floats in the core path, so fused-multiply-add reassociation cannot change a result. (Unity's IL2CPP does not enable `-ffast-math` by default; integer-only makes us immune regardless.) |
| **FORBIDDEN: `string.GetHashCode()`, `object.GetHashCode()` as a draw/seed input** | Randomized per-process since .NET Core; produces different values every run. `level_id` must be a pre-resolved manifest ordinal (TR-rng-004); label hashing uses the explicit FNV-1a-32 above. |
| **FORBIDDEN: `DateTime.Now/UtcNow`, `Environment.TickCount`, `Stopwatch`, `Guid.NewGuid()`** anywhere in Domain | Wall-clock / entropy sources; non-deterministic. The session-log timestamp is injected via `IClock` (impl lives in `SweetCascade.Game`), never read in Domain. |
| **REQUIRED: `uint`/`ulong` with `unchecked`** for all mixing/combining | Signed `int` `>>` is arithmetic (sign-extends) and `checked` overflow throws; `uint` gives logical shift and mod-2^32 wraparound, which is what F1–F3 specify. |
| **REQUIRED: single-threaded, no `[ThreadStatic]`/locks** | rng-service.md Edge Cases: draw order alone determines the sequence; there is no concurrency model. |

#### 5. Golden-vector regression suite (the byte-stability gate)

A checked-in fixture is the frozen ground truth. It is generated **once** by the reference
implementation above, reviewed, committed, and thereafter immutable for `algorithm_version = "v1"`;
any code change that alters a single value fails the suite (that is the intended alarm).

- **Location**: `src/SweetCascade/Assets/Tests/EditMode/Rng/golden/rng_golden_v1.json`
  (fixture) + `RngGoldenVectorTest.cs` (NUnit Edit-Mode reader/asserter).
- **Format**: values stored as **decimal `uint`** strings (never raw bytes — sidesteps endianness),
  keyed by input. Contents:
  - **Seed derivation**: a table of `(levelId, attemptNumber) → masterSeed` (F1),
    `(dailyChallengeId, calendarDateUtc) → masterSeed` (F2), and `(masterSeed, streamId) →
    streamSeed` (F3), including the GDD anchors (1007/3, 42/20650, 500/1).
  - **Raw sequences**: for ≥ 5 pinned master seeds (incl. 0, 1, 0xFFFFFFFF, and the F1 anchor),
    the first **64** `NextRaw` outputs on `board-refill`.
  - **Derived draws**: for the same seeds, the first 64 `NextInt(0,4)` and `NextColor` over a
    5-element pool (the launch default), plus `NextFloat` for 8 draws (double, printed to 17
    significant digits / round-trip `"R"` format).
  - **Shuffle**: `(seed, [0..19]) → permutation` and the length-0/1/2 boundary cases.
  - **Fork**: `(seed, parent="board-refill", label="probe") → childSeed` and its first 16 draws.
- **Cross-backend proof (TR-rng-002)**: the identical fixture is asserted under
  1. **Mono** — Edit-Mode run in the editor, every PR (`unity-test-runner@v4`, blocking);
  2. **IL2CPP** — the same assertions compiled into a standalone player build and executed in CI;
  3. **WebGL** — the same assertions run in-browser (headless in CI where available, otherwise a
     blocking manual gate at the Vertical Slice checkpoint). One shared fixture, three backends,
     byte-for-byte — this is the acceptance evidence for "same seed → same sequence on
     IL2CPP/Mono/WebGL."

#### 6. Domain-purity CI guard — three independent layers

`noEngineReferences` alone is insufficient because it blocks only `UnityEngine`/`UnityEditor`,
not `System.Random` (BCL, always linkable). Defense in depth:

- **L1 — asmdef (compile-time, free).** `SweetCascade.Domain.asmdef`:
  ```json
  { "name": "SweetCascade.Domain", "noEngineReferences": true,
    "references": [], "autoReferenced": true, "allowUnsafeCode": false }
  ```
  With `noEngineReferences: true`, any `using UnityEngine;` / engine type fails to compile — the
  build itself is the guard for engine purity. (Test/Game asmdefs reference Domain downward.)
- **L2 — CI denylist scan (catches what L1 cannot).** A pre-build CI job `domain-purity` (runs
  **before** the Unity build; needs no Unity license, so it fails fast) `ripgrep`s
  `src/SweetCascade/Assets/Domain/` for the denylist; any hit exits non-zero and blocks the PR/push:
  ```
  \bSystem\.Random\b
  \bnew\s+Random\s*\(
  \bUnityEngine\b
  \bUnityEditor\b
  \bDateTime\s*\.\s*(Now|UtcNow|Today)\b
  \bEnvironment\s*\.\s*TickCount\b
  \bStopwatch\b
  \bGuid\s*\.\s*NewGuid\b
  \bfloat\b                     # scoped to Assets/Domain/Rng/** only (score-ratio float lives elsewhere)
  ```
  A reviewed inline escape hatch `// rng-purity-allow: <reason>` suppresses a single justified
  line (grep excludes lines containing the pragma), keeping the guard precise rather than blunt.
- **L3 — golden-vector suite (§5).** Blocking in CI on every PR (Mono) and on the release matrix
  (IL2CPP + WebGL). This is what actually proves determinism, versus merely proving absence of
  forbidden tokens.
- **(Fast-follow, recommended not blocking for MVP) — Roslyn analyzer.** Package a small analyzer
  (`SC-RNG-001: System.Random is forbidden in SweetCascade.Domain`, `SC-RNG-002: engine type in
  Domain`) referenced by the Domain asmdef so the *editor* red-squiggles a violation the instant
  it is typed, semantically (no false positives on comments/strings, unlike L2). L2 covers MVP;
  the analyzer upgrades L2 from textual to semantic when time allows.

## Alternatives Considered

### Alternative 1: `System.Random` (the blueprint placeholder)

- **Description**: Keep `new System.Random(masterSeed)` per stream.
- **Pros**: Zero implementation; familiar; adequate statistical quality for a casual game.
- **Cons**: **Not byte-stable across .NET runtimes** — the sequence can differ between the editor's
  Mono, an IL2CPP device build, and WebGL. Silently breaks daily-challenge fairness, seed-replay
  bug-repro, and every deterministic Domain test's portability. Also seeded with a signed `int`, not
  the full uint32 master seed.
- **Estimated Effort**: none.
- **Rejection Reason**: Directly violates TR-rng-002 and Pillar 2; explicitly forbidden by the
  master architecture (§1, §2, TD Condition 1) and this ADR's entire reason to exist.

### Alternative 2: `UnityEngine.Random` / a Unity-native RNG

- **Description**: Use the engine's RNG (`UnityEngine.Random`, seed via `Random.InitState`).
- **Pros**: Built-in.
- **Cons**: A `UnityEngine` type — categorically barred from the Domain (architecture §5, §12-1),
  which would collapse the headless-testability and reversibility pillars. `Random.InitState` is
  global mutable state (no stream isolation → violates TR-rng-001) and its cross-backend stability
  is undocumented/unpinned.
- **Estimated Effort**: low.
- **Rejection Reason**: Breaks the assembly-purity invariant and the isolation guarantee; wrong layer.

### Alternative 3: Literal float F4/F5 (`floor(next_float × range)` on the core path)

- **Description**: Implement `next_int`/`next_color` exactly as written in the GDD, multiplying a
  `double`/`float` in `[0,1)` by the range and flooring.
- **Pros**: Textually identical to the GDD formula; keeps `next_float` on the hot path.
- **Cons**: Puts floating-point multiply + `floor` on the most determinism-critical path; requires a
  runtime boundary clamp; 32-bit `float` (if used) is the least portable primitive across
  Mono/IL2CPP/WebGL.
- **Estimated Effort**: equal.
- **Rejection Reason**: The integer multiply-shift is *provably identical* to this formula (when
  `next_float = raw/2^32`) yet removes float from the core path and makes the clamp structurally
  unnecessary — strictly better with no loss of GDD fidelity. Adopted as a refinement, not a rival.

### Alternative 4: xoshiro128\*\* or PCG32 as the stream generator

- **Description**: Use a 128-bit-state (xoshiro128\*\*, period 2^128−1) or 64-bit-state (PCG32,
  period 2^64) generator instead of SplitMix32 (32-bit state, period 2^32).
- **Pros**: Larger period and stronger statistical quality (both pass TestU01 BigCrush); removes
  any doubt about the 10^9-draw non-functional requirement.
- **Cons**: More state and a more elaborate seeding step (widening the 32-bit F3 `stream_seed` to
  128/64 bits without ambiguity), i.e. **more surface where two implementations could disagree** —
  in direct tension with the "spell out so they agree bit-for-bit" mandate. SplitMix32 is the
  minimal fully-specifiable option and is exactly the GDD's suggested "SplitMix32-style" choice.
- **Estimated Effort**: modestly higher.
- **Rejection Reason for MVP**: SplitMix32's period 2^32 ≈ 4.3×10^9 clears the 10^9 floor with ~4×
  margin, and actual board play draws ~10^2–10^3 per level (6+ orders of magnitude of headroom).
  Its output over a full period is a bijection (a permutation of all 2^32 values), so within any
  sub-period run the distribution is near-perfect. **Pre-approved upgrade path**: if a future
  feature ever needs > 10^9 draws or BigCrush-grade quality, promote xoshiro128\*\* as
  `algorithm_version = "v2"` — the GDD's version-tag Edge Case makes this a clean, disclosed
  migration (old logged seeds documented non-reproducible under v2, which the log already records).

### Alternative 5: Namespace-only purity (no separate asmdef) / grep-only guard (no L1/L3)

- **Description**: Enforce purity by convention/namespaces and a single grep, without the assembly
  boundary or the golden-vector suite.
- **Pros**: Less setup.
- **Cons**: Convention is not compiler-enforced (architecture §5.2); a single grep proves only
  *absence of tokens*, never *determinism*. Neither catches a subtle constant/order regression that
  silently changes the sequence.
- **Rejection Reason**: The three layers are independent and catch different failure classes
  (engine leak / forbidden BCL type / sequence drift); dropping any one leaves a real gap.

## Consequences

### Positive

- **Pillar 2 is structurally true, not just reviewed.** Byte-identical sequences across all
  backends make daily-challenge fairness and seed-replay bug-repro real; the API's total absence of
  a player-skill/performance parameter makes rubber-banding un-buildable without a visible new
  argument (Honest Randomness Contract §4).
- **Every Domain test becomes reproducible and headless** — the seedable RNG is the keystone that
  lets board/scoring/objective Edit-Mode tests assert exact outcomes with no scene or frame sim.
- **One shared, verified hash primitive** (FNV-1a-32) across RNG label-forking and the save
  checksum reduces the count of things that must be independently trusted.
- **Reversible by design** — the entire generator is engine-neutral pure C#; an engine change moves
  it unchanged (ADR-001 reversibility pillar), and an algorithm change is a disclosed
  `algorithm_version` bump, not a silent break.

### Negative

- **SplitMix32's period (2^32) is finite** and its statistical quality, while good, is below
  xoshiro/PCG at extreme scale. Accepted for MVP with a documented, pre-approved v2 upgrade path.
- **The golden fixture is a maintenance obligation**: it must be regenerated (and reviewed as a
  deliberate break) on any intentional `algorithm_version` change, and CI must run three backends.
- **A subtle discrepancy from the GDD's literal F4/F5 float wording exists** (integer refinement) —
  mitigated by documenting the exact algebraic equivalence and flagging it in the test file so a
  future GDD reviewer does not mistake it for a divergence.

### Neutral

- Draw dispatch is keyed by `string streamName`; the string is only a registry lookup and is
  **never** fed to the generator, so BCL string-hash randomization cannot affect the sequence
  (it affects only dictionary bucketing). Hot paths may cache the resolved stream handle.
- `NextColor`/`Shuffle` are generic (`<T>`); the board uses `int` color indices, but the generic
  surface matches the GDD's array-of-elements shape at no cost.

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| A `checked`-build or an `int` (signed) variable slips into the mixer, changing `>>`/overflow behavior | Low | High (silent sequence change) | Constructs table (§4) + golden-vector suite catches any value drift immediately; `unchecked`/`uint` are shown normatively in the reference code |
| WebGL/WebAssembly diverges on some operation despite integer-only design | Low | High | L3 explicitly runs the fixture **in-browser**; treated as an acceptance gate, not an assumption (Verification Required) |
| Someone reintroduces `System.Random` (habit) inside Domain | Medium | High | L1 asmdef won't catch it → L2 denylist scan does (`\bSystem\.Random\b`, `\bnew\s+Random\s*\(`), blocking; fast-follow Roslyn `SC-RNG-001` makes it an editor-time error |
| SplitMix32 period exhausted by a pathological/stress test (> 4.3×10^9 draws) | Very Low | Medium | Documented; real play is 6+ orders below; pre-approved xoshiro128\*\* `v2` path via `algorithm_version` |
| Golden fixture generated with a bug becomes the "wrong" ground truth | Low | High | Fixture generation is code-reviewed against the GDD anchors (`combine()` values are independently verifiable) and the `Mix32(0)=0` invariant before it is frozen |
| CI denylist `\bfloat\b` false-positives on legitimate Domain float (e.g. `ScoreResults.ScoreProgressRatio`) | Medium | Low | The `float` pattern is **path-scoped to `Assets/Domain/Rng/**`** only; the `// rng-purity-allow:` pragma covers any reviewed exception elsewhere |

## Performance Implications

| Metric | Before | Expected After | Budget |
|--------|--------|---------------|--------|
| CPU (frame time) | n/a (placeholder) | Bootstrap fill (64 draws) + worst-case cascade refill (≤ 64 draws) ≈ a few µs total; each draw is one add + one multiply-based finalizer + one 64-bit multiply-shift, all integer | < 1 ms for the fill+cascade (rng-service.md Perf AC); well inside the 16.6 ms frame budget |
| Memory | n/a | Per stream: one `uint` state (+ small dictionary entry). Four registered streams + occasional forks ≈ a few hundred bytes total | ≤ 400 MB ceiling — negligible |
| Load Time | n/a | Session start derives 4 stream sub-seeds (4× `Combine`+`Mix32`) — sub-microsecond | No measurable impact |
| Network | n/a | None — RNG is offline/deterministic; the daily seed is `(challenge_id, calendar_date_utc)`, no fetch | n/a |

## Migration Plan

This is a foundational addition (no legacy RNG in shipped code — only the blueprint placeholder),
so "migration" is the initial build plus the one mandated source edit.

1. Create `SweetCascade.Domain.asmdef` with `noEngineReferences: true` (L1). Verify a deliberate
   `using UnityEngine;` in a Domain file fails to compile, then remove it.
2. Implement `SweetCascade.Domain.Rng` (`IRngService`, `RngService`, `StreamRegistry`, `IClock`)
   per the normative primitives (§1–§3). Provide a `SystemClock : IClock` in `SweetCascade.Game`.
3. **Delete the blueprint's `new Random(seed)` placeholder** (architecture §2's second mandatory
   port edit) wherever the seeded-Domain example referenced it; route callers through `IRngService`.
4. Generate `rng_golden_v1.json` from the reference implementation; review it against the GDD
   anchors and `Mix32(0)=0`; freeze it. Author `RngGoldenVectorTest.cs` (§5).
5. Wire the `domain-purity` CI job (L2 denylist) to run before the Unity build; wire the
   golden-vector Edit-Mode test into the PR gate (Mono) and the IL2CPP + WebGL runs into the
   release matrix (L3).
6. Board Engine consumes `board-refill` via `IRngService` (dependency-injected, per coding-standards).

**Rollback plan**: The RNG is behind the `IRngService` interface and versioned by
`algorithm_version`. If the SplitMix32 choice proves inadequate, implement the pre-approved
xoshiro128\*\* generator as `"v2"`, bump the tag, regenerate `rng_golden_v2.json`, and document
`v1` seeds as non-reproducible under `v2` (the session log records which version produced each
seed). No interface change; callers are unaffected.

## Validation Criteria

- [ ] `Mix32(0) == 0`, and `Combine(1007,3)==1547274724`, `Combine(42,20650)==653539216`,
      `Combine(500,1)==73026539` (GDD anchors) hold in code.
- [ ] The golden-vector suite passes **byte-for-byte under Mono, IL2CPP, and WebGL** (TR-rng-002).
- [ ] rng-service.md acceptance criteria pass as Edit-Mode tests: `test_same_seed_same_sequence`,
      `test_stream_isolation`, `test_fork_stream_*`, `test_uniform_color_distribution`
      (chi-square p > 0.01, each color within ±2%), `test_next_int_bounds_respected`,
      `test_shuffle_preserves_multiset` / `_does_not_mutate_input`,
      `test_next_int_invalid_range_errors`, `test_next_color_empty_pool_errors`,
      `test_daily_seed_deterministic_across_calls`, `test_daily_seed_excludes_player_data`,
      `test_no_hidden_bias_parameter_on_draw_functions`, `test_session_log_*`.
- [ ] `Shuffle` of an N-element array consumes exactly `N − 1` raw draws (F6 contract).
- [ ] The `domain-purity` CI job fails a PR that introduces `System.Random`/`new Random(`/
      `UnityEngine`/`DateTime.Now` into `Assets/Domain/`, and blocks merge (L2).
- [ ] `SweetCascade.Domain` compiles with `noEngineReferences: true`; a probe `using UnityEngine;`
      fails the build (L1).
- [ ] `IRngService`'s signatures contain **no** parameter representing player skill, streak,
      performance, or spend (Honest Randomness Contract, verified by interface inspection).

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/rng-service.md` | RNG Service | §1 Stream isolation — drawing on A never perturbs B | Each registered `stream_id` gets an independent SplitMix32 state via F3 (`Mix32(Combine(master_seed, stream_id))`); separate `StreamState` per name |
| `design/gdd/rng-service.md` | RNG Service | §3 / F1–F3 seed lifecycle with platform-stable `mix32` | `Combine` (GDD `K1`/`K2`, `unchecked uint`) + `Mix32` (MurmurHash3 fmix32) + SplitMix32; all integer, byte-stable across backends |
| `design/gdd/rng-service.md` | RNG Service | §6 API surface (`start_*_session`, `next_float/int/color`, `shuffle`, `fork_stream`, `get_session_log`) | `IRngService` maps 1:1; §6 signatures reproduced in Key Interfaces |
| `design/gdd/rng-service.md` | RNG Service | F4/F5/F6 uniform int / color / Fisher–Yates | Integer multiply-shift `(raw·range)>>32` (exact equivalent of `floor(next_float·range)`); F6 verbatim, `N−1` draws |
| `design/gdd/rng-service.md` | RNG Service | §4 Honest Randomness Contract — no player-performance input | API has no such parameter (architecturally excluded); F2 daily seed takes only `(challenge_id, calendar_date_utc)` |
| `design/gdd/rng-service.md` | RNG Service | §3 `level_id` must be a resolved int, never a runtime string-hash (TR-rng-004) | `StartLevelSession(int levelId, …)`; `string.GetHashCode()` forbidden in Domain (§4, CI-guarded) |
| `design/gdd/rng-service.md` | RNG Service | §3 bug-repro session log; `algorithm_version` tag | `RngSessionLog` record with `AlgorithmVersion = "v1"`; timestamp injected via `IClock` to keep Domain clock-free |
| `docs/architecture/architecture.md` | Assembly / Determinism | §5, §10.3 Condition 1, TR-perf-002 — Domain zero-`UnityEngine`, CI-guarded; §2 replace `System.Random` | Three-layer guard (asmdef `noEngineReferences` + CI denylist + golden vectors); `System.Random` forbidden and scanned |
| `.claude/docs/technical-preferences.md` | Testing / Determinism | "All board RNG must be seedable so match/cascade tests are reproducible" | `StartTestSession(uint masterSeed)` for direct seed injection; golden fixtures make the sequence a frozen, testable contract |

## Related

- **Realizes** `docs/architecture/architecture.md` §11 Required-ADR **"ADR-C"**; discharges
  TD-ARCHITECTURE **Condition 1** (§10.3) and resolves **QQ-04** (§13).
- **Depends on** `docs/architecture/adr-001-engine-selection-unity.md` (Accepted).
- **Sibling Foundation ADRs**: `docs/architecture/adr-002-addressables-strategy.md` (provides the
  content-load path for `LevelData`); ADR-003 (Save serialization — shares the **FNV-1a-32**
  primitive defined here); future **ADR-E** (Level manifest — supplies the resolved integer
  `level_id` ordinal `StartLevelSession` consumes).
- **Governing GDD**: `design/gdd/rng-service.md` (F1–F6, stream registry, Honest Randomness Contract).
- **Implementation targets (once built)**: `src/SweetCascade/Assets/Domain/Rng/` (`RngService.cs`,
  `StreamRegistry.cs`), `src/SweetCascade/Assets/Tests/EditMode/Rng/` (golden fixture + tests),
  CI `domain-purity` job.
