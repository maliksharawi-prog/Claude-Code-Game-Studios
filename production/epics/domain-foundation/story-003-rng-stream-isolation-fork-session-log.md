# Story 003: RNG stream isolation, ForkStream & bug-repro session log

> **Epic**: Domain Foundation (E02)
> **Status**: In Review — implementation + full NUnit Edit-Mode suite authored 2026-07-18; PASS evidence pending the first Unity test run (CI blocked on UNITY_LICENSE, concern C8; container has no Unity editor). Do not mark Complete until the suite passes under Mono.
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: 2026-07-18 (gameplay-programmer — Edit-Mode test suite authored)

## Context

**GDD**: `design/gdd/rng-service.md` (§1 Stream Isolation, §3 bug-repro log, §6 `fork_stream`/`get_session_log`)
**Requirement**: `TR-rng-001`, `TR-rng-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity Guard
**ADR Decision Summary**: Each registered `stream_id` gets an independent `SplitMix32` state via F3, held in a per-name `StreamState`, so a draw on stream A never perturbs B. `ForkStream` derives the child from the parent's **initial** seed (not its advanced state) and hashes the label with **FNV-1a-32 over UTF-8 bytes** (never `string.GetHashCode()`); it is idempotent per `(parent,label)`. The session-log timestamp is injected via `IClock` to keep `DateTime` out of Domain.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure BCL)
**Engine Notes**: The `IClock.UtcNowIso()` seam keeps `DateTime.UtcNow` in `SweetCascade.Game` (`SystemClock`); Domain never reads the clock. `Guid.NewGuid()`/`Stopwatch`/`Environment.TickCount` are forbidden in Domain.

**Control Manifest Rules (Domain layer)**:
- Required: `ForkStream` label hashing uses the FNV-1a-32 constants `FNV_OFFSET=0x811C9DC5`/`FNV_PRIME=0x01000193` (the same primitive shared by value with the Save checksum, Story 008) over UTF-8 label bytes.
- Required: wall-clock time enters Domain only via injected `IClock` (timestamp only, never a draw input).
- Required: RNG is single-threaded, no `[ThreadStatic]`/locks; `IRngService` exposes `ForkStream` + `GetSessionLog` per §6.
- Forbidden: `string.GetHashCode()`/`object.GetHashCode()` as a draw/seed input; sharing mutable state between streams (violates isolation); `DateTime.Now`/`UtcNow` anywhere in Domain.

---

## Acceptance Criteria

*From GDD `design/gdd/rng-service.md` (Stream Isolation, Bug Repro & Logging) and ADR-004 §3, scoped to this story:*

- [x] Each named stream has its own `StreamState` (initial + current). `test_stream_isolation`: interleaving 100 `special-drop` draws between `board-refill` draws produces the exact same `board-refill` sequence as a control run with no interleaving. **Covered**: `RngService_Tests.cs::Test_StreamIsolation_Interleaved100SpecialDropDraws_DoNotPerturbBoardRefillSequence` (also cross-checks both the board-refill AND the interleaved special-drop draws against an independent Python oracle via `golden/rng_golden_v1_draws.json`).
- [x] `ForkStream(parent, label)` derives `childSeed = Mix32(Combine(parentInitialSeed, fnv1a32(label)))`; the child is independent of how many draws the parent has consumed. **Covered**: `RngService_Tests.cs::Test_ForkStream_MatchesGoldenChildSeedAndFirst16Draws`, `Test_ForkStream_ChildSequence_UnaffectedByForkTiming_BeforeOrAfterParentDraws`.
- [x] `test_fork_stream_isolated_from_parent`: drawing N values from the child does not change what the parent returns next (vs a never-forked control). **Covered**: `RngService_Tests.cs::Test_ForkStream_IsolatedFromParent_ChildDrawsDoNotPerturbParentSequence`.
- [x] `test_fork_stream_deterministic`: two fresh identically-seeded sessions forking the same `(parent, label)` produce identical child output sequences. **Covered**: `RngService_Tests.cs::Test_ForkStream_Deterministic_AcrossTwoFreshIdenticallySeededSessions`.
- [x] `test_fork_stream_idempotent_within_session`: a second `ForkStream(parent, label)` with identical args returns the **same** child stream (continuing its sequence), not a reset second stream. **Covered**: `RngService_Tests.cs::Test_ForkStream_Idempotent_SecondCallReturnsSameContinuingChild_NotAReset`.
- [x] `test_session_log_contains_repro_fields`: after `StartLevelSession`, `GetSessionLog()` returns a record with non-null `PrimaryId` (level_id), `InstanceId` (attempt_number), `MasterSeed`, and `AlgorithmVersion ("v1")`. **Covered**: `RngService_Tests.cs::Test_SessionLog_ContainsReproFields_AfterStartLevelSession` (uses `FakeClock`, never the real wall clock).
- [x] `test_session_log_replay`: feeding a logged `(level_id, attempt_number)` back into a fresh `StartLevelSession` reproduces the same `master_seed`. **Covered**: `RngService_Tests.cs::Test_SessionLog_Replay_ReproducesSameMasterSeed`.
- [x] `test_no_hidden_bias_parameter_on_draw_functions`: `NextInt`/`NextFloat`/`NextColor`/`Shuffle`/`ForkStream` accept no player-skill/streak/performance/spend parameter (interface inspection). **Covered**: `RngService_Tests.cs::Test_NoHiddenBiasParameter_OnAnyIRngServiceMethod` (reflects over every `IRngService` method, not just the named subset).

---

## Implementation Notes

*Derived from ADR-004 Implementation Guidelines §3 and Key Interfaces:*

- Hold streams in a session-scoped `Dictionary<string, StreamState>` keyed by name (the string is a lookup only, never fed to the generator — BCL string-hash randomization affects only bucket placement, not the sequence).
- `ForkStream` = ADR-004 §3 listing: `child = parent + "/" + label`; return early if already present (idempotent); FNV-1a-32 the label's UTF-8 bytes; `childSeed = Mix32(Combine(_streams[parent].InitialSeed, h))`; store and return the child name.
- `RngSessionLog` is the ADR-004 record struct `(long PrimaryId, long InstanceId, uint MasterSeed, string AlgorithmVersion, string SessionStartTimestampIso)`. `StartLevelSession`/`StartDailySession` (Story 001) populate ids + seed + `"v1"`; this story adds the `IClock.UtcNowIso()` timestamp and `GetSessionLog()`.
- Define `public interface IClock { string UtcNowIso(); }` in Domain; the concrete `SystemClock` lives in `SweetCascade.Game` (E06 / out of scope here). Domain receives `IClock` by constructor injection.
- Honest Randomness Contract: verify by interface inspection that no draw/fork/session method has a player-performance parameter — this is architecturally excluded, not runtime-guarded.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: F1–F3 seed derivation and per-stream sub-seeding at session start (this story consumes the resulting `StreamState`s).
- Story 002: the draw math itself (`NextInt`/`NextColor`/`Shuffle`).
- Story 004: freezing the fork/session sequences into the golden fixture.
- The concrete `SystemClock : IClock` implementation (Game layer, E06).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: isolation (`test_stream_isolation`).
  - Given: a seeded session.
  - When: 100 `special-drop` draws are interleaved between `board-refill` draws vs a control with no interleaving.
  - Then: the `board-refill` sequence is byte-identical across both runs.

- **AC-2**: fork isolation & determinism (`test_fork_stream_isolated_from_parent`, `test_fork_stream_deterministic`).
  - Given: two identically-seeded sessions.
  - When: forking `(parent, label)` and drawing.
  - Then: parent's next draws are unaffected by child draws; both sessions' child sequences match.
  - Edge cases: fork timing (before vs after parent draws) must not change the child sequence — child derives from the parent's **initial** seed.

- **AC-3**: fork idempotency (`test_fork_stream_idempotent_within_session`).
  - Given: one session.
  - When: `ForkStream(parent, label)` called twice with identical args.
  - Then: the second call returns the same child (continuing, not resetting).

- **AC-4**: session log (`test_session_log_contains_repro_fields`, `test_session_log_replay`).
  - Given: `StartLevelSession(1007, 3)` with an injected fixed `IClock`.
  - When: `GetSessionLog()` is read, then the `(1007,3)` pair is replayed.
  - Then: the log carries non-null ids/seed/`"v1"`; the replay reproduces the same `master_seed`.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/rng-service/rng_stream_isolation_fork_log_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Rng/` (ADR-004 §5), headless Mono via game-ci.

**Status**: [x] Created — 2026-07-18. Every named test above has a matching NUnit method in
`src/SweetCascade/Assets/Tests/EditMode/Rng/RngService_Tests.cs`, backed by
`golden/rng_golden_v1.json` + the additive `golden/rng_golden_v1_draws.json` (special-drop raw
draws + two extra fork vectors, generated via `tools/ci/rng_reference.py`). **Caveat**: no Unity
installation was available to compile/run the suite this pass — pass/fail confirmation is still
owed once E01 Story 004's `game-ci` gate is active.

---

## Dependencies

- Depends on: **Story 001** (seed lifecycle + `StreamState`), **Story 002** (draw API used to prove isolation/fork). Transitively **E01 Story 002/003**.
- Unlocks: Story 004 (golden fixture pins fork `(seed, "board-refill", "probe")` and session-log fields).
