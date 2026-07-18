# example_conventions_test.gd
#
# This file demonstrates the test naming and structure conventions for Sweet Cascade.
# It is a trivial example and should be replaced with real board-engine tests
# once implementation begins.
#
# Convention rules:
# - File name: [system]_[feature]_test.gd (e.g., board_engine_cascade_test.gd)
# - Function name: test_[scenario]_[expected] (e.g., test_cascade_resolved_when_matches_clear)
# - Each test should be isolated (set up, execute, assert, tear down)
# - Use dependency injection instead of singletons
# - Avoid hardcoded magic numbers (use named constants or fixtures)

class_name ExampleConventionsTest
extends GutTest

## Example constant used across tests (demonstrates no-hardcoding rule).
## This replaces inline magic numbers.
const EXPECTED_BOARD_WIDTH = 8
const EXPECTED_BOARD_HEIGHT = 8

## Setup runs before each test — tear down any state here if needed
func before_each() -> void:
	pass

## Teardown runs after each test
func after_each() -> void:
	pass

## Example test: trivial assertion to demonstrate structure
## Real tests will verify match detection, cascade resolution, etc.
##
## Naming: test_[scenario]_[expected]
## Expected: This assertion is always true (trivial for example purposes)
func test_board_dimensions_match_constants() -> void:
	assert_eq(EXPECTED_BOARD_WIDTH, 8, "Board width should be 8")
	assert_eq(EXPECTED_BOARD_HEIGHT, 8, "Board height should be 8")

## Example test: demonstrates isolation
## Each test can run in any order; they don't depend on execution order
func test_example_isolated_computation() -> void:
	var result = 2 + 2
	assert_eq(result, 4, "Basic math should work")

## Real tests will follow this pattern:
## 1. Set up test fixtures (factory functions, not magic numbers)
## 2. Execute the system under test
## 3. Assert the expected output
## 4. Tear down (cleanup if needed)
##
## WHEN YOU REPLACE THIS FILE:
## Delete this entire file and create real tests like:
##   - tests/unit/board/board_engine_match_test.gd
##   - tests/unit/board/board_engine_cascade_test.gd
##   - tests/unit/rng/rng_service_determinism_test.gd
##   - tests/unit/scoring/scoring_formula_test.gd
##
## Each test file should:
## - Import its system under test at the top
## - Use dependency injection (pass dependencies to constructors)
## - Test one system in isolation
## - Keep tests deterministic (seedable RNG for randomness)
## - Document what each test validates in a comment above the function
