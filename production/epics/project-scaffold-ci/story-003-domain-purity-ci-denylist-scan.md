# Story 003: Domain-purity CI denylist scan (L2) + injected-violation self-test

> **Epic**: Project Scaffold & CI Activation (E01)
> **Status**: Ready
> **Layer**: Foundation (infrastructure / CI)
> **Type**: Integration
> **Estimate**: 1.5 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: — (governed by `docs/architecture/architecture.md` §5.1 + ADR-004 §6)
**Requirement**: `TR-perf-002`
*(This story satisfies the "domain assembly zero-`UnityEngine` (**CI-guarded**)" portion of TR-perf-002 — specifically the L2 denylist layer. Read the current text fresh from `docs/architecture/tr-registry.yaml`.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity CI Guard (primary — §6 L2)
**ADR Decision Summary**: `noEngineReferences` alone cannot catch BCL types like `System.Random`, so a pre-build CI job `domain-purity` `ripgrep`s `Assets/Domain/` for a denylist and fails the PR/push on any hit. A reviewed `// rng-purity-allow: <reason>` pragma is the only sanctioned per-line escape.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (the scan is engine-independent shell/`ripgrep`; **needs no Unity license**, so it fails fast before the Unity build)
**Engine Notes**: This job runs before any Unity invocation — it is pure `ripgrep` over source text, so it is immune to engine-version drift. It is the only E01 CI gate that can run without the `UNITY_LICENSE` secret.

**Control Manifest Rules (Editor & CI layer)**:
- Required: `domain-purity` CI job (L2 denylist `ripgrep` scan of `Assets/Domain/`) runs **before** the Unity build (no Unity license needed, fails fast) and blocks the PR/push on any forbidden-token hit. A reviewed `// rng-purity-allow: <reason>` pragma is the only sanctioned per-line escape.
- Required (cross-cutting): Domain purity is enforced at three independent layers (asmdef L1 + CI denylist L2 + golden vectors L3).
- Forbidden: `System.Random`/`new Random(`, `UnityEngine`/`UnityEditor`, `DateTime.Now/UtcNow/Today`, `Environment.TickCount`, `Stopwatch`, `Guid.NewGuid`, and (scoped to `Assets/Domain/Rng/**`) bare `float`, anywhere in Domain.
- Guardrail: never disable or skip a failing check to force CI green.

---

## Acceptance Criteria

*From ADR-004 §6 (L2) and Validation Criteria, scoped to this story:*

- [ ] A CI job `domain-purity` runs on every PR and every push to `main`, **before** the Unity test-runner job (so it fails fast, no license required).
- [ ] The job `ripgrep`s `src/SweetCascade/Assets/Domain/` for the ADR-004 denylist patterns and exits non-zero (blocking) on any hit:
  - `\bSystem\.Random\b`, `\bnew\s+Random\s*\(`, `\bUnityEngine\b`, `\bUnityEditor\b`, `\bDateTime\s*\.\s*(Now|UtcNow|Today)\b`, `\bEnvironment\s*\.\s*TickCount\b`, `\bStopwatch\b`, `\bGuid\s*\.\s*NewGuid\b`, and `\bfloat\b` **path-scoped to `Assets/Domain/Rng/**` only**.
- [ ] A reviewed inline escape `// rng-purity-allow: <reason>` suppresses a single justified line (lines containing the pragma are excluded from the scan).
- [ ] An **injected-violation self-test** proves the guard works: a script/test that (a) creates a temp Domain file containing `System.Random` → asserts the scan exits non-zero; (b) removes it / scans clean Domain → asserts the scan exits zero; (c) confirms a `// rng-purity-allow:` line is not flagged. The self-test runs in CI and is itself blocking.
- [ ] The `float`-in-Rng rule is path-scoped so a legitimate Domain `float` outside `Rng/` (e.g. a future `ScoreProgressRatio`) does **not** false-positive.

---

## Implementation Notes

*Derived from ADR-004 §6 L2 (denylist verbatim) and Risks table:*

**File-based (no editor GUI required):**
- Add a `domain-purity` job (or a first step gating the test job) in `.github/workflows/` — either extend `tests.yml` with a `domain-purity` job that the `test` job `needs:`, or add a dedicated workflow. It must run before `game-ci/unity-test-runner`.
- Implement the scan as a small committed script (e.g. `tools/ci/domain-purity-scan.sh`) invoked by the workflow, so the exact same command runs locally and in CI. Use `ripgrep` with the ADR-004 patterns; exclude lines matching `// rng-purity-allow:`; scope the `\bfloat\b` pattern to `Assets/Domain/Rng/**` (separate `rg` invocation with a path glob).
- The self-test (e.g. `tools/ci/domain-purity-selftest.sh`) creates a throwaway file under a temp path that the scan treats as Domain, asserts non-zero exit, then asserts zero exit on the clean tree, then asserts the pragma line is skipped. Return non-zero if any assertion fails.
- Guard for the empty-tree case: at E01 the `Assets/Domain/` tree is empty/`.gitkeep`, so a real scan of it trivially passes — the **self-test** (which injects a known violation) is what proves the guard is actually wired and armed, not vacuously green.

**Manual checklist:** none strictly required — this story is fully file/script-based. (First green CI observation is folded into Story 004.)

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 002: the L1 asmdef `noEngineReferences` layer.
- Story 004: wiring/observing the full `game-ci/unity-test-runner` gate and the first end-to-end green run (this story delivers the license-free L2 job + its self-test; Story 004 sequences it ahead of the Unity job and captures the first green run).
- **E02**: the L3 golden-vector RNG suite (`rng_golden_v1.json` + `RngGoldenVectorTest.cs`) run byte-for-byte under Mono/IL2CPP/WebGL — that needs real RNG code and the export matrix (see ADR-J gap). L3 is not built here.
- The optional fast-follow Roslyn analyzer (`SC-RNG-001/002`) — explicitly "recommended, not blocking for MVP" in ADR-004; not this epic.

---

## QA Test Cases

*Authored at story creation (lean mode). The denylist guard is directly and deterministically testable via the self-test — this is an automated integration test, blocking.*

**Test — AC: scan blocks a forbidden token**
- Given: a Domain file (temp fixture) containing `var r = new System.Random(42);`.
- When: `domain-purity-scan.sh` runs over the Domain tree including the fixture.
- Then: the script exits non-zero and prints the offending file:line.
- Edge cases: repeat for `UnityEngine`, `DateTime.Now`, `Guid.NewGuid`, and a bare `float` **inside** `Assets/Domain/Rng/` — each must trip the scan.

**Test — AC: clean Domain passes**
- Given: the committed (empty/`.gitkeep`) `Assets/Domain/` tree with no forbidden tokens.
- When: the scan runs.
- Then: it exits zero.
- Edge cases: a bare `float` **outside** `Assets/Domain/Rng/` (e.g. `Assets/Domain/Scoring/`) must NOT be flagged (path-scoped rule).

**Test — AC: pragma escape is honored**
- Given: a Domain line `... // rng-purity-allow: interop shim, reviewed` that also contains a would-be-flagged token.
- When: the scan runs.
- Then: that line is excluded and does not cause a non-zero exit.
- Edge cases: a token on a different line without the pragma still trips the scan (the escape is per-line, not per-file).

**Test — AC: self-test is armed, not vacuous**
- Given: `domain-purity-selftest.sh`.
- When: it runs in CI.
- Then: it injects a known violation, asserts non-zero, cleans up, asserts zero, and asserts the pragma skip — failing loudly if any assertion is wrong.
- Edge cases: the self-test must fail if someone accidentally neuters the scan (e.g. removes a pattern).

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: Integration test — the self-test is an automated, deterministic CI gate and **must pass**. Evidence: the committed `tools/ci/domain-purity-selftest.sh` plus a documented CI run in `production/qa/evidence/story-003-purity-scan-evidence.md` showing (a) a red run on the injected violation and (b) a green run on the clean tree with the self-test passing.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 002 (needs the `Assets/Domain/` tree + asmdef to scope the scan).
- Unlocks: Story 004 (the `domain-purity` job is sequenced ahead of the Unity test-runner and observed green there).
