# Test Evidence: E01-005 — Edit/Play Mode test skeletons + example conventions test

**Story**: `production/epics/project-scaffold-ci/story-005-edit-play-test-skeletons.md`
**Story Type**: Integration
**Authored**: 2026-07-18 (gameplay-programmer)
**Required evidence** (per the story's Test Evidence section): the committed example
EditMode + PlayMode tests, plus a documented run showing both modes executed —
Test Runner pass locally and the `game-ci` gate run.

## What was authored

- `src/SweetCascade/Assets/Tests/PlayMode/SweetCascade.Game.Tests.asmdef` — Play Mode
  test assembly, references `SweetCascade.Game` + `SweetCascade.Domain` +
  `UnityEngine.TestRunner`/`UnityEditor.TestRunner`, `includePlatforms: []` (compiles
  for Editor and device Player test runs alike), `defineConstraints: ["UNITY_INCLUDE_TESTS"]`
  — mirrors the house style of the existing `SweetCascade.Domain.Tests.asmdef`.
- `src/SweetCascade/Assets/Tests/EditMode/Conventions/ConventionsExampleTests.cs` —
  `Test_DomainAssembly_IsEngineFree()`. Reflects over the compiled `SweetCascade.Domain`
  assembly's referenced-assembly list and asserts neither `UnityEngine*` nor
  `UnityEditor*` is present. Deterministic (no seed/clock/IO), single `[Test]`,
  explicit Arrange/Act/Assert, engine-free (Domain.Tests asmdef only).
- `src/SweetCascade/Assets/Tests/PlayMode/SmokeExampleTests.cs` —
  `Test_PlayModeHarness_EntersPlayAndRunsOneFrame_AssertsTriviallyTrue()`. A
  `[UnityTest]` that yields exactly one frame and asserts `Application.isPlaying`.
  No named scene load (none exists yet in the project at this story's scope — see
  the file's doc comment); no external asset load; no frame-timing dependency beyond
  the single yield.
- EditMode coverage skeleton folders (`.gitkeep`, empty, ready for later epics):
  `Assets/Tests/EditMode/Board/`, `Specials/`, `Scoring/`, `Objectives/`, `Save/`.
  (`Rng/` and `Levels/` already exist and are populated by E02 — untouched here.)

## Local Test Runner status

**Not executed.** No Unity Editor is available in this authoring environment
(container has no Unity install/license) — consistent with the E02-001..005 stories'
documented caveat. The example tests are written and internally consistent with the
project's NUnit/Unity Test Framework conventions, but pass/fail has not been confirmed
by an actual Unity Test Runner execution (Window ▸ General ▸ Test Runner). This is a
manual editor-checklist item explicitly called out in the story's Implementation Notes
and remains outstanding pending a human Unity 6.3 editor pass (also required to
generate and commit the `.meta` files for the new asmdef/tests, per the story).

## game-ci gate status

**Blocked.** Same blocker as E02-001..005 and E01-004: CI execution of
`game-ci/unity-test-runner@v4 testMode: all` requires the `UNITY_LICENSE` secret
(or `UNITY_EMAIL`/`UNITY_PASSWORD`), which has not yet been added to the repository
(concern C8). Until that secret lands and E01 Story 004's gate activates, neither the
EditMode nor the PlayMode example test has an actual green CI run to point to. This
evidence doc will be updated with the real run link once the gate is live.

## Acceptance criteria coverage (self-assessed, unexecuted)

| AC | Status |
|---|---|
| EditMode `SweetCascade.Domain.Tests.asmdef` exists + EditMode skeleton folders (`Rng/ Board/ Specials/ Scoring/ Objectives/ Save/`) present | Done — asmdef pre-existed (Story 002); all six subfolders now present (`Rng/`, `Levels/` from E02; `Board/ Specials/ Scoring/ Objectives/ Save/` added this story) |
| PlayMode `SweetCascade.Game.Tests.asmdef` exists, references Game+Domain+Test Framework, `PlayMode/` folder present | Done (file-authored; not yet compiled) |
| Example EditMode conventions test exists and passes headlessly | Authored, unexecuted — see Local Test Runner status above |
| Example PlayMode smoke test exists and passes | Authored, unexecuted — see Local Test Runner status above |
| Both example tests run and pass under `game-ci/unity-test-runner@v4 testMode: all` | Blocked — see game-ci gate status above |
| Example tests are deterministic and isolated (no seeds, no clock, no IO) | Done by construction — reviewed inline in both files' doc comments |

## Caveat

Do not treat this document as a green CI run. It records what was authored and why
it is believed correct, not a confirmed pass. Update this file (or supersede it) the
moment a real Unity Test Runner or `game-ci` execution produces pass/fail results.
