# Test Infrastructure

**Engine**: Unity 6.3 LTS (ADR-001)
**Test Framework**: Unity Test Framework (Edit Mode + Play Mode)
**CI**: `.github/workflows/tests.yml` (game-ci/unity-test-runner@v4)
**Setup date**: 2026-07-18 (re-pinned from Godot/gdUnit4 the same day — see ADR-001)

## Where tests live

Unity Test Framework tests live **inside the Unity project** once it is
scaffolded (expected `src/SweetCascade/`):

```
src/SweetCascade/Assets/Tests/
  EditMode/       # Headless domain-logic tests (BoardModel, SpecialResolver,
                  # ScoreKeeper, objectives, save serialization) — the bulk of
                  # coverage; these run without a scene and mirror the GDD
                  # acceptance criteria
  PlayMode/       # Integration tests (event replay onto views, input routing,
                  # save/load round-trips, full-level bot runs)
```

This repo-level `tests/` directory holds engine-independent artifacts:

```
tests/
  smoke/          # Smoke test list consumed by /smoke-check
  evidence/       # Screenshot logs and manual sign-off records (as they accrue)
```

## Running Tests

- **Locally**: Unity Editor → Window → General → Test Runner (Edit Mode / Play Mode),
  or CLI: `Unity -runTests -batchmode -projectPath src/SweetCascade -testPlatform EditMode`
- **CI**: every push to `main` and every PR runs both modes via game-ci; a failed
  suite blocks merging. Until the Unity project exists, the workflow skips with a
  notice (guard step) instead of failing.
- **CI prerequisite (one-time)**: add the `UNITY_LICENSE` secret (or
  `UNITY_EMAIL`/`UNITY_PASSWORD` for personal-license activation) in the repo
  settings — see game-ci activation docs.

## Test Naming Conventions (C#)

Per `.claude/rules/test-standards.md`, adapted to C#/NUnit:

- **Files**: `[System][Feature]Tests.cs` — e.g., `BoardModelCascadeTests.cs`
- **Methods**: `Test_[Scenario]_[Expected]` — e.g., `Test_HorizontalMatch4_SpawnsSameAxisStripe()`
- **Structure**: explicit Arrange / Act / Assert blocks
- **Fixtures**: factory methods or constant classes, never inline magic numbers
  (exception: boundary-value tests where the number IS the point)

## Story Type → Test Evidence Mapping

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** | Automated Edit Mode test — must pass | `Assets/Tests/EditMode/[System]/` | BLOCKING |
| **Integration** | Play Mode test OR playtest doc | `Assets/Tests/PlayMode/[System]/` | BLOCKING |
| **Visual/Feel** | Screenshot + lead sign-off | `tests/evidence/` | ADVISORY |
| **UI** | Manual walkthrough OR interaction test | `tests/evidence/` | ADVISORY |
| **Config/Data** | Smoke check pass | `production/qa/smoke-*.md` | ADVISORY |

## Determinism & Isolation Rules

- **Determinism**: same seed + same inputs → same result, every run. Board RNG is
  stream-seeded per `design/gdd/rng-service.md`; no time-dependent assertions.
- **Isolation**: each test arranges and tears down its own state; order-independent.
- **Independence**: Edit Mode tests never touch filesystem/network — the domain
  layer is pure C# with zero `UnityEngine` references (enforced by the
  domain-assembly rule in `technical-preferences.md`), so it also runs in a plain
  test runner outside Unity.

## Planned Edit Mode Coverage by System

Mirrors the GDD acceptance criteria (each GDD's Acceptance Criteria section is
the authoritative test list):

- `EditMode/Rng/` — stream isolation, seed lifecycle, distribution
- `EditMode/Board/` — match detection, cluster precedence, gravity segments, refill, reshuffle determinism, event payload completeness
- `EditMode/Specials/` — creation rules, full combo matrix, deterministic passive detonation, seam no-op compatibility
- `EditMode/Scoring/` — step formula, per-piece bonuses, star thresholds, RSPM(K) table
- `EditMode/Objectives/` — move matrix, unified collect counting, win/lose predicate, last-move-cascade win
- `EditMode/Save/` — round-trip fidelity, corruption ladder, migration fixtures

## Next Steps

1. Scaffold the Unity project (`src/SweetCascade/`, Unity 6.3 LTS, URP Render Graph) — Technical Setup phase
2. Add the game-ci license secret to the repo
3. Port the blueprint domain scripts (`docs/architecture/visual-interface-blueprint.md`) into an assembly-definition-isolated domain module + its Edit Mode tests
4. `/smoke-check` remains the pre-QA gate (`tests/smoke/critical-paths.md`)
