# Story 001: RNG primitives & seed derivation (Mix32 / SplitMix32 / F1–F3)

> **Epic**: Domain Foundation (E02)
> **Status**: In Review — implementation + full NUnit Edit-Mode suite authored 2026-07-18; PASS evidence pending the first Unity test run (CI blocked on UNITY_LICENSE, concern C8; container has no Unity editor). Do not mark Complete until the suite passes under Mono.
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: 2026-07-18 (gameplay-programmer — Edit-Mode test suite authored)

## Context

**GDD**: `design/gdd/rng-service.md` (§2 Stream Registry, §3 Seed Lifecycle, Formulas F1–F3)
**Requirement**: `TR-rng-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity Guard
**ADR Decision Summary**: A fully-specified, integer-first, engine-free RNG built from two normative primitives — a `Mix32` (MurmurHash3 `fmix32`) avalanche finalizer and a `SplitMix32` stream generator — with every constant and operation order fixed so two implementations agree bit-for-bit. Seed derivation is literally GDD F1/F2/F3 over `Combine(a,b)=(a·K1+b·K2) mod 2^32`.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure BCL integer arithmetic; zero engine surface by construction)
**Engine Notes**: No post-cutoff Unity API is touched. Cross-platform determinism depends only on the C# spec (`unchecked` integer overflow wraps mod 2^n; `>>` on `uint` is logical). This ADR does **not** need re-validation on an engine bump — that is the point of housing it in the engine-free Domain.

**Control Manifest Rules (Domain layer)**:
- Required: RNG generator is SplitMix32 (stream) + MurmurHash3 `fmix32` (`Mix32`, finalizer) with the exact normative constants `K1=0x9E3779B1`, `K2=0x9E37`, `GAMMA=0x9E3779B9`, `M1=0x85EBCA6B`, `M2=0xC2B2AE35`, `FNV_OFFSET=0x811C9DC5`, `FNV_PRIME=0x01000193`.
- Required: all RNG mixing/combining arithmetic is `uint`/`ulong` and `unchecked`; RNG is single-threaded, no `[ThreadStatic]`/locks; every GDD/ADR constant lives in one centralized Domain config location (stream IDs, `algorithm_version`).
- Forbidden: `System.Random` / `new Random(...)`; any `UnityEngine.*`/`UnityEditor.*`; 32-bit `float`/`Mathf`/`Math.Floor` on the RNG core path (`Assets/Domain/Rng/**`); `string.GetHashCode()` as a seed input; `DateTime.Now`/`Stopwatch`/`Guid.NewGuid()`.
- Guardrail: bootstrap fill (64 draws) + worst-case cascade refill (≤64 draws) < 1 ms; per-stream state ≈ a `uint` plus a small dictionary entry.

---

## Acceptance Criteria

*From GDD `design/gdd/rng-service.md` (Determinism, Honest Randomness Contract) and ADR-004 Validation Criteria, scoped to this story:*

- [x] The normative primitives hold in code: `Mix32(0) == 0`, and the GDD `combine()` anchors reproduce under `uint` wraparound — `Combine(1007,3)==1547274724`, `Combine(42,20650)==653539216`, `Combine(500,1)==73026539`. **Covered**: `Mix32_Tests.cs` (`Test_Avalanche_Zero_ReturnsZero`, `Test_Combine_Anchor*`, plus `Test_Combine_DoesNotThrow_UnderAnOuterCheckedContext` / `Test_Combine_WrapsModulo2Pow32_ForMaxRangeInputs` for the edge cases).
- [x] `StartLevelSession(int levelId, int attemptNumber)` derives `master_seed` via F1 (`Mix32(Combine(levelId, attemptNumber))`); `StartDailySession(int dailyChallengeId, int calendarDateUtc)` via F2; `StartTestSession(uint masterSeed)` sets the seed directly, bypassing F1/F2. **Covered**: `RngService_Tests.cs` (`Test_StartLevelSession_MasterSeed_MatchesF1Anchor`, `Test_StartDailySession_MasterSeed_MatchesF2Anchor`, `Test_StartTestSession_SetsMasterSeedDirectly_BypassingF1F2`).
- [x] At every session start, **every** registered stream (`board-refill=1`, `special-drop=2`, `harvest=3`, `events=4`) receives an independent sub-seed via F3 (`Mix32(Combine(masterSeed, streamId))`), whether or not it has a live consumer — `stream_id` numbering is append-only and never renumbered. **Covered**: `RngService_Tests.cs` (`Test_StartTestSession_AllFourStreams_MatchGoldenF3SubSeeds_ForMasterSeed500`, `Test_StartLevelSession_AllFourStreams_MatchGoldenF3SubSeeds_ForF1AnchorMasterSeed`).
- [x] `test_attempt_number_changes_seed`: `(level_id=X, attempt=1)` vs `(X, 2)` produce different `master_seed` values. **Covered**: `RngService_Tests.cs::Test_AttemptNumberChangesSeed`.
- [x] `test_level_id_changes_seed`: `(X, 1)` vs `(Y, 1)` with `X != Y` produce different `master_seed`. **Covered**: `RngService_Tests.cs::Test_LevelIdChangesSeed`.
- [x] `test_daily_seed_deterministic_across_calls`: repeated `StartDailySession` with identical `(daily_challenge_id, calendar_date_utc)` always produce the same `master_seed`. **Covered**: `RngService_Tests.cs::Test_DailySeed_DeterministicAcrossCalls`.
- [x] `test_daily_seed_excludes_player_data`: `StartDailySession`'s signature accepts no player-identifying/performance parameter — verified by interface inspection. **Covered**: `RngService_Tests.cs::Test_DailySeed_ExcludesPlayerData_InterfaceInspectionOfStartDailySession`.
- [x] All mixing arithmetic is `unchecked uint`/`ulong`; a probe confirms zero `System.Random`/`float` on the `Rng/**` core path (L2 denylist, E01 Story 003). **Covered**: `tools/ci/domain-purity-scan.sh` re-run 2026-07-18 — PASS (see this pass's final verification run; also self-tested via `--self-test`, PASS).

---

## Implementation Notes

*Derived from ADR-004 Implementation Guidelines §1 (normative primitives) and §4 (required/forbidden constructs):*

- Place `RngService.cs`, `StreamRegistry.cs`, and the constant table under `Assets/Domain/Rng/` in `SweetCascade.Domain.Rng` (arch §5.3).
- Implement verbatim from ADR-004 §1: `static uint Combine(uint a, uint b) => unchecked(a*K1 + b*K2)`; `Mix32` (the six-step fmix32 with logical `>>` on `uint`); `uint NextRaw(ref uint state) => unchecked { state += GAMMA; return Mix32(state); }`.
- Seed derivation is literally the GDD formulas: `MasterSeedLevel = Mix32(Combine((uint)levelId,(uint)attempt))`, `MasterSeedDaily = Mix32(Combine((uint)dailyId,(uint)dateUtc))`, `StreamSeed = Mix32(Combine(masterSeed,(uint)streamId))`.
- `StreamRegistry` is a Domain const table (`BoardRefill=1`, `SpecialDrop=2`, `Harvest=3`, `Events=4`). Sub-seed all four at session start; this is zero-cost and keeps numbering stable.
- `level_id` reaching the RNG must be a resolved `int` ordinal (from `LevelManifest.ResolveOrdinal`, Story 012) — never a runtime string hash. `StartLevelSession` accepts an opaque `int`; it does not read Level Data Format.
- Delete the visual blueprint's `new Random(seed)` placeholder (arch §2 residue sweep, ADR-004 Migration step 3); route callers through `IRngService`. Note ADR-005's supersession of the blueprint: no `System.Random`, and score is `long` (score is not this story's concern, but the "no Random" edit is).
- `AlgorithmVersion = "v1"` is a centralized constant (rng-service.md Tuning Knobs); a finalizer change is a disclosed version bump, never a silent edit.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: the draw API (`NextFloat`/`NextInt`/`NextColor`/`Shuffle`) built on `NextRaw`.
- Story 003: per-stream `StreamState` isolation, `ForkStream`, the `RngSessionLog` record + injected `IClock` timestamp, and `GetSessionLog()`.
- Story 004: the checked-in `rng_golden_v1.json` fixture + `RngGoldenVectorTest.cs` byte-for-byte regression suite (the cross-backend acceptance proof).
- Story 012: `LevelManifest.ResolveOrdinal` that supplies the resolved integer `level_id` (TR-rng-004).

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: primitives & GDD anchors hold in code.
  - Given: the `Combine`/`Mix32` primitives.
  - When: evaluated on the fixed inputs.
  - Then: `Mix32(0)==0`; `Combine(1007,3)==1547274724`; `Combine(42,20650)==653539216`; `Combine(500,1)==73026539`.
  - Edge cases: assert under a checked build config too (must not throw — arithmetic is `unchecked`); confirm `Combine` wraps mod 2^32 for max-range inputs.

- **AC-2**: seed lifecycle F1/F2/F3.
  - Given: a fresh service.
  - When: `StartLevelSession(1007,3)` runs.
  - Then: `master_seed == Mix32(1547274724)`; all four registered streams hold `Mix32(Combine(master_seed, id))`.
  - Edge cases: `StartTestSession(seed)` sets `master_seed` exactly, bypassing F1/F2; two `StartTestSession(X)` calls yield identical stream sub-seeds.

- **AC-3**: seed sensitivity (`test_attempt_number_changes_seed`, `test_level_id_changes_seed`).
  - Given: two sessions.
  - When: `(X,1)` vs `(X,2)`, and `(X,1)` vs `(Y,1)`.
  - Then: `master_seed` differs in both comparisons.

- **AC-4**: daily determinism & honesty (`test_daily_seed_deterministic_across_calls`, `test_daily_seed_excludes_player_data`).
  - Given: `StartDailySession(42, 20650)` called repeatedly.
  - When: master seeds compared.
  - Then: identical every call; the method signature has no player-skill/streak/spend/identity parameter (interface inspection).

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/rng-service/rng_seed_derivation_test.cs` — must exist and pass. Actual in-project location: `src/SweetCascade/Assets/Tests/EditMode/Rng/` (ADR-004 §5), run headless under Mono via `game-ci/unity-test-runner@v4` (E01 Story 004).

**Status**: [x] Created — 2026-07-18. Every AC bullet above has a covering NUnit Edit-Mode test in
`src/SweetCascade/Assets/Tests/EditMode/Rng/Mix32_Tests.cs` and `RngService_Tests.cs`, asserted
against embedded golden constants in `GoldenVectors.cs` (generated from
`golden/rng_golden_v1.json` — never hand-typed). **Caveat**: this authoring pass had no Unity
installation available to compile/run the suite — tests are written and internally
cross-consistent (anchors independently re-verified via `tools/ci/rng_reference.py`, which PASSED
2026-07-18), but pass/fail has not been confirmed by an actual Unity Test Runner execution. Do not
treat this as a green CI run — that confirmation is still owed once E01 Story 004's `game-ci` gate
is active.

---

## Dependencies

- Depends on: **E01 Story 002** (five asmdefs + `SweetCascade.Domain` `noEngineReferences` + `Domain.Tests` Edit-Mode asmdef must exist to compile & test Domain code); **E01 Story 003** (Domain-purity L2 denylist scan — proves the RNG core path is `System.Random`/`float`-free). Soft: **E01 Story 004** (game-ci gate for the green CI run), **E01 Story 005** (Edit-Mode test conventions).
- Unlocks: Story 002 (draw API needs `NextRaw` + stream states), Story 003 (isolation/fork/log build on the seed lifecycle), Story 004 (golden vectors pin these primitives).
