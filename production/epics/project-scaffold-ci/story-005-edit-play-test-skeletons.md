# Story 005: Edit/Play Mode test skeletons + example conventions test

> **Epic**: Project Scaffold & CI Activation (E01)
> **Status**: In Review — PlayMode asmdef + EditMode skeleton folders + both example tests
> (`ConventionsExampleTests.cs`, `SmokeExampleTests.cs`) authored 2026-07-18; PASS evidence
> pending the first Unity test run (CI blocked on UNITY_LICENSE, concern C8; container has no
> Unity editor). Do not mark Complete until both suites pass under the Unity Test Runner.
> **Layer**: Foundation (infrastructure / test harness)
> **Type**: Integration
> **Estimate**: 1.5 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: 2026-07-18 (gameplay-programmer — PlayMode asmdef, skeleton folders, and both example tests authored)

## Context

**GDD**: — (governed by `tests/README.md`, `.claude/docs/coding-standards.md` Testing Standards, `docs/architecture/architecture.md` §5.1/§5.3)
**Requirement**: `TR-perf-002`
*(This story stands up the Edit/Play test harness that makes the "CI-guarded" clause of TR-perf-002 real — the assemblies `game-ci` executes. It also establishes the naming/structure conventions all later logic/integration stories test against. Read the current text fresh from `docs/architecture/tr-registry.yaml`.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity CI Guard (primary — §5 pins the Edit Mode golden-vector suite location `Assets/Tests/EditMode/Rng/` and the headless-Domain test model)
**Governing ADRs (secondary)**: `architecture.md` §5.1/§5.3 (test asmdef layout); `.claude/rules/test-standards.md` + `tests/README.md` (C#/NUnit conventions).
**ADR Decision Summary**: Domain compiles and runs in a plain .NET test runner; Edit Mode is the bulk of coverage (headless Domain), Play Mode covers integration. Tests are deterministic, isolated, order-independent, and never touch external IO.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (Unity Test Framework / NUnit is pre-cutoff-stable)
**Engine Notes**: The EditMode Domain.Tests asmdef references only `SweetCascade.Domain` + the Test Framework — it must NOT pull `UnityEngine` gameplay types into Domain's test surface (headless-testability is the point). PlayMode tests may use engine types (they reference Game).

**Control Manifest Rules (Editor & CI + testing)**:
- Required: Unity Test Framework — Edit Mode for the headless Domain layer, Play Mode for integration.
- Required: tests are deterministic (no random seeds / no time-dependent assertions), isolated (own setup/teardown), independent (order-free); Edit Mode never touches filesystem/network.
- Naming (C#/NUnit, `tests/README.md`): files `[System][Feature]Tests.cs`; methods `Test_[Scenario]_[Expected]`; explicit Arrange/Act/Assert.

---

## Acceptance Criteria

*From `tests/README.md`, coding-standards Testing Standards, and `architecture.md` §5.3, scoped to this story:*

- [x] The Edit Mode test asmdef `Assets/Tests/EditMode/SweetCascade.Domain.Tests.asmdef` exists (created in Story 002) and the `EditMode/` skeleton folders from `tests/README.md` are present (`Rng/ Board/ Specials/ Scoring/ Objectives/ Save/` — empty/`.gitkeep`, mirroring the planned coverage map). `Rng/` and `Levels/` are already populated by E02; `Board/ Specials/ Scoring/ Objectives/ Save/` added 2026-07-18 as empty `.gitkeep` folders.
- [x] The Play Mode test asmdef `Assets/Tests/PlayMode/SweetCascade.Game.Tests.asmdef` exists, references `SweetCascade.Game` + `SweetCascade.Domain` + the Test Framework, and its `PlayMode/` folder is present. Authored 2026-07-18.
- [~] One **example EditMode conventions test** exists (e.g. `Assets/Tests/EditMode/Conventions/ConventionsExampleTests.cs` with `Test_DomainAssembly_IsEngineFree()`) — serving as the copy-me template for later logic stories. **Authored, not yet confirmed passing** (no Unity editor available in this authoring pass — see Test Evidence).
- [~] One **example PlayMode smoke test** exists (`Assets/Tests/PlayMode/SmokeExampleTests.cs`, `Test_PlayModeHarness_EntersPlayAndRunsOneFrame_AssertsTriviallyTrue()`) — a minimal `[UnityTest]` that enters Play Mode and asserts a trivially true integration condition. **Authored, not yet confirmed passing.**
- [ ] Both example tests run and pass under `game-ci/unity-test-runner@v4 testMode: all` (verified via Story 004's gate). Empty suites elsewhere are acceptable. **Not yet verified — blocked on UNITY_LICENSE (C8), same as Story 004 and E02-001..005.**
- [x] The example tests are deterministic and isolated (no seeds, no clock, no IO) — they must not become a source of flaky CI. Verified by code review: no `System.Random`, no `DateTime`, no filesystem/network IO in either test file.

---

## Implementation Notes

*Derived from `tests/README.md`, `.claude/rules/test-standards.md`, ADR-004 §5:*

**File-based (no editor GUI required):**
- Create `Assets/Tests/PlayMode/SweetCascade.Game.Tests.asmdef` with references `["SweetCascade.Game", "SweetCascade.Domain"]`, the TestAssemblies reference, and Play Mode platform inclusion.
- Create the `EditMode/` coverage sub-folders (`Rng/ Board/ Specials/ Scoring/ Objectives/ Save/`) with `.gitkeep` to mirror `tests/README.md`'s "Planned Edit Mode Coverage" so later epics drop tests into a ready home.
- Author `ConventionsExampleTests.cs` (EditMode): a single NUnit `[Test]` named `Test_[Scenario]_[Expected]`, with explicit `// Arrange / // Act / // Assert` blocks and a deterministic assertion. Keep it engine-free (references Domain only) to demonstrate headless Domain testing. Add a doc-comment header pointing later authors at the naming rules.
- Author `SmokeExampleTests.cs` (PlayMode): a minimal `[UnityTest]`/`[Test]` proving the PlayMode runner executes.
- Do NOT author real domain tests (RNG golden vectors, board, scoring) — those are E02+ and depend on real Domain code.

**Manual editor-checklist items (require the Unity 6.3 editor):**
- [ ] Open the project and open Window ▸ General ▸ Test Runner; confirm both Edit Mode and Play Mode tabs list the example tests and both pass locally. Commit generated `.meta` for the new asmdef/tests.

---

## Out of Scope

*Handled by neighbouring stories or later epics — do not implement here:*

- Story 002: the EditMode `SweetCascade.Domain.Tests.asmdef` (created there; this story adds the PlayMode asmdef + example tests + folder skeletons).
- Story 004: wiring `game-ci` to run these assemblies (this story provides the assemblies + example tests; Story 004 executes them in the gate).
- **E02**: the real EditMode suites — RNG golden vectors (`Rng/`), save round-trip, etc. — plus the L3 IL2CPP/WebGL golden runs.
- **E03+**: Board/Specials/Scoring/Objectives EditMode suites and integration PlayMode tests (event replay, input routing, save round-trip).

---

## QA Test Cases

*Authored at story creation (lean mode). The example tests are automated and blocking — they prove the harness runs end-to-end.*

**Test — AC: EditMode conventions example passes headlessly**
- Given: `ConventionsExampleTests.cs` in the Domain.Tests asmdef with a deterministic assertion.
- When: Edit Mode tests run (Test Runner locally and game-ci in CI).
- Then: the test passes; it references no `UnityEngine` gameplay type and no IO.
- Edge cases: run twice — identical result (determinism); the test passes in isolation regardless of order.

**Test — AC: PlayMode smoke example passes**
- Given: `SmokeExampleTests.cs` in the Game.Tests asmdef.
- When: Play Mode tests run.
- Then: the test enters play/loads the empty scene and asserts a trivially true condition, passing.
- Edge cases: no reliance on frame timing beyond a single yield; no external asset load.

**Manual check — AC: skeleton folders + asmdefs present**
- Setup: inspect `Assets/Tests/EditMode/` and `Assets/Tests/PlayMode/`.
- Verify: EditMode coverage sub-folders exist (`Rng/ Board/ Specials/ Scoring/ Objectives/ Save/`); PlayMode asmdef references Game+Domain+TestAssemblies.
- Pass condition: the harness matches `tests/README.md`'s planned layout and is ready for later epics to fill.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: Integration/automated — the example EditMode and PlayMode tests **must pass** (both locally in Test Runner and in the game-ci gate). Evidence: the committed example tests + a documented run in `production/qa/evidence/story-005-test-harness-evidence.md` (Test Runner pass + the game-ci run showing both modes executed). The EditMode example doubles as Logic-grade automated evidence for the harness.

**Status**: [x] Created — 2026-07-18. Both example tests
(`Assets/Tests/EditMode/Conventions/ConventionsExampleTests.cs`,
`Assets/Tests/PlayMode/SmokeExampleTests.cs`) and the PlayMode asmdef are authored, and a
documented-run evidence doc exists at
`production/qa/evidence/story-005-test-harness-evidence.md`. **Caveat**: this authoring pass
had no Unity installation available to compile/run either suite — tests are written and
internally consistent with the project's NUnit/Unity Test Framework conventions, but pass/fail
has not been confirmed by an actual Unity Test Runner execution, and the `game-ci` gate run is
blocked on the `UNITY_LICENSE` secret (concern C8), same as Story 004 and E02-001..005. Do not
treat this as a green CI run — that confirmation is still owed once Story 004's gate is active.

---

## Dependencies

- Depends on: Story 002 (test asmdefs reference `SweetCascade.Domain`; the EditMode Domain.Tests asmdef is created there).
- Unlocks: Story 004 (the example Edit/Play tests give the game-ci runner real assemblies to execute in the first green run).
