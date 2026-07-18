# Test Infrastructure

**Engine**: Godot 4.6  
**Test Framework**: GdUnit4  
**CI**: `.github/workflows/tests.yml`  
**Setup date**: 2026-07-18

## Directory Layout

```
tests/
  unit/           # Isolated unit tests (formulas, state machines, logic)
  integration/    # Cross-system and save/load tests
  evidence/       # Screenshot logs and manual test sign-off records
```

## Running Tests

### Locally (requires GdUnit4 addon installed)

```bash
# Run all unit and integration tests headlessly
godot --headless --script tests/gdunit4_runner.gd
```

### In CI

Tests run automatically on every push to `main` and on every pull request.
A failed test suite blocks merging. See `.github/workflows/tests.yml` for details.

## Test Naming Conventions

- **Files**: `[system]_[feature]_test.gd`
  - Example: `board_engine_cascade_test.gd`
- **Functions**: `test_[scenario]_[expected]`
  - Example: `func test_cascade_resolved_when_matches_clear() -> void:`
- **Fixtures**: Use factory functions or constant files, not inline magic numbers
  - Exception: boundary-value tests where the exact number IS the point

## Story Type → Test Evidence Mapping

| Story Type | Required Evidence | Location | Gate Level |
|---|---|---|---|
| **Logic** | Automated unit test — must pass | `tests/unit/[system]/` | BLOCKING |
| **Integration** | Integration test OR playtest doc | `tests/integration/[system]/` | BLOCKING |
| **Visual/Feel** | Screenshot + lead sign-off | `tests/evidence/` | ADVISORY |
| **UI** | Manual walkthrough OR interaction test | `tests/evidence/` | ADVISORY |
| **Config/Data** | Smoke check pass | `production/qa/smoke-*.md` | ADVISORY |

## Determinism & Isolation Rules

All tests must follow these principles:

- **Determinism**: Tests produce the same result every run — no random seeds, no time-dependent assertions. Board RNG must use seeded values for reproducibility (see `technical-preferences.md`).
- **Isolation**: Each test sets up and tears down its own state; tests must not depend on execution order
- **No hardcoded data**: Test fixtures use constant files or factory functions, not inline magic numbers
- **Independence**: Unit tests do not call external APIs, databases, or file I/O — use dependency injection

## Installing GdUnit4

> **TODO**: GdUnit4 addon will be installed when the Godot project scaffold lands (when `project.godot` is created).

1. Open Godot → AssetLib → search "GdUnit4" → Download & Install
2. Enable the plugin: Project → Project Settings → Plugins → GdUnit4 ✓
3. Restart the editor
4. Verify: `res://addons/gdunit4/` exists

## CI Behavior

The workflow in `.github/workflows/tests.yml`:

- Runs on every push to `main` and every pull request
- Detects if `project.godot` is missing and skips tests with a clear notice (no failure)
- Uploads test results as artifacts for review
- Blocks merging if tests fail
- Never skips or disables failing tests — fix the underlying issue

## Next Steps

1. **Install GdUnit4** via Godot AssetLib once `project.godot` is created
2. **Write your first test**: Create `tests/unit/[system]/[system]_[feature]_test.gd`
3. **Run smoke tests**: Use `/smoke-check` before every QA hand-off (or manually run tests in `production/qa/smoke-tests.md`)
4. **Run all tests**: `godot --headless --script tests/gdunit4_runner.gd`

## Planned Test Coverage by System (as features land)

These directories will grow as systems are implemented:

- `tests/unit/rng/` — RNG Service determinism and seeding
- `tests/unit/board/` — Match-3 Board Engine (match detection, cascades, refill)
- `tests/unit/special/` — Special Candies and Combo Matrix
- `tests/unit/scoring/` — Scoring formulas and star thresholds
- `tests/unit/objectives/` — Level Objectives and Move-Limit System
- `tests/integration/save/` — Save/Load round-trips
- `tests/integration/full_level/` — End-to-end level play tests
