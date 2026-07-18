# Story 010: Determinism master gate — byte-identical final state & signal sequence

> **Epic**: Board Engine & Special Candies (Domain Core) — E03
> **Status**: Ready
> **Layer**: Core
> **Type**: Logic
> **Estimate**: M
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/board-engine.md` (Rev 2) — § Detailed Rules 12 (Determinism Contract), Acceptance Criteria "Determinism (the master gate)"
**Requirement**: `TR-be-001`, `TR-be-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-005: Event Bridge & BoardEvent Catalog (primary — the single authoritative byte-identical sequence; ordering guarantee clauses a–d)
**Governing ADRs (secondary)**: ADR-004 (platform-stable RNG makes the byte-identical clause hold across iOS/Android/Web).
**ADR Decision Summary**: The per-move sequence (names, payloads, order) is byte-identical under identical inputs across runs and across platforms because (a) BoardModel emission order is fixed by the §6 state machine + §7 ordering guarantees, (b) the logic-subscriber order is pinned, (c) all `board-refill` draws follow one fixed call order through a platform-stable RNG (ADR-004), (d) no Domain code reads wall-clock/frame count.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW
**Engine Notes**: Pure C#; determinism holds byte-identically across Mono/IL2CPP/WebGL (owned by ADR-004's three-backend golden-vector suite). Board tests are seeded and reproducible. No wall-clock/frame/entropy input anywhere in Domain. No post-cutoff API.

**Control Manifest Rules (Domain)**:
- Required: all board RNG is seedable (stream-based) so match/cascade tests are reproducible; tests are deterministic (no live seeds beyond the injected test seed, no time-dependent assertions).
- Forbidden: `DateTime.Now`/`UtcNow`, `Environment.TickCount`, `Stopwatch`, `Guid.NewGuid()` anywhere in Domain; `string.GetHashCode()`/`object.GetHashCode()` as a draw/seed input.

---

## Acceptance Criteria

*From `design/gdd/board-engine.md` Determinism master gate (all BLOCKING), scoped to this story:*

- [ ] `test_identical_seed_identical_final_state`: two runs with identical `(level_id, attempt_number)` (or an identical injected `master_seed` via `StartTestSession`) and an identical ordered intent sequence produce byte-identical final grid state (every cell's `color`, `special_type`, and `piece_id`-relative ordering).
- [ ] `test_identical_seed_identical_signal_sequence`: the same two runs emit an identical ordered list of `BoardEvent` names + payloads.
- [ ] `test_different_attempt_number_different_bootstrap`: bootstrapping the same `level_id` with `attempt_number = 1` vs `2` produces different initial board states (RNG Formula F1). *(Shared with Story 008; asserted here at the whole-sequence level.)*
- [ ] The determinism harness runs entirely headless in a plain .NET Edit-Mode runner against seeded fixtures; no wall-clock or frame input influences the result.

---

## Implementation Notes

*Derived from board-engine §12 + ADR-005 ordering guarantee + ADR-004:*

- Drive the whole system through `MoveResolver.ResolveBootstrap()` + a fixed ordered list of `ResolveSwap`/cancel intents; capture the concatenated `IReadOnlyList<BoardEvent>` and the final board snapshot.
- Compare two runs' event lists element-by-element (record value-equality) and compare final grid state cell-by-cell. Byte-identity means names, payloads, and order all match.
- Determinism depends on E02's platform-stable RNG (ADR-004); this story asserts the Domain-side reproducibility (identical seed → identical sequence). The cross-platform (Mono/IL2CPP/WebGL) byte-for-byte proof is ADR-004's golden-vector suite, run on the release matrix — reference it, do not duplicate it here.
- Use `StartTestSession` for an injected `master_seed` where a level file is not needed.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- The behaviours under test (Stories 001–009) — this story only asserts their reproducibility as a whole.
- ADR-004's three-backend golden-vector RNG suite (E02) — referenced, not re-implemented.
- Special-candies determinism (Story 015).

---

## QA Test Cases

*Authored at story creation. Implement against these — do not invent new cases during implementation.*

- **AC — identical final state** (`test_identical_seed_identical_final_state`)
  - Given: two runs, identical `(level_id, attempt_number)` (or injected seed) + identical ordered intents.
  - When: both fully resolve.
  - Then: byte-identical final grid state (color/special_type/relative piece_id ordering per cell).
- **AC — identical signal sequence** (`test_identical_seed_identical_signal_sequence`)
  - Given: the same two runs.
  - When: their event streams are captured.
  - Then: identical ordered list of event names + payloads.
- **AC — attempt-number divergence** (`test_different_attempt_number_different_bootstrap`)
  - Given: the same `level_id`, `attempt_number` 1 vs 2.
  - When: bootstrapped.
  - Then: the initial board states (and thus sequences) differ.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**: `tests/unit/board-engine/determinism_master_gate_test.cs` — must exist and pass (seeded, headless).

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 009 (`MoveResolver` ordered sequence), Story 008 (bootstrap + attempt_number), E02 (platform-stable `IRngService` / ADR-004 golden vectors).
- Unlocks: E04 (scoring/objectives determinism build on the byte-identical event stream), Story 015 (specials determinism).
