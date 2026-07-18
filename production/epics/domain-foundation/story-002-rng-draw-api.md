# Story 002: RNG draw API — NextFloat / NextInt / NextColor / Shuffle (F4–F6)

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/rng-service.md` (§6 API surface, Formulas F4/F5/F6, Edge Cases)
**Requirement**: `TR-rng-003`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity Guard
**ADR Decision Summary**: F4/F5 are implemented as the **exact integer equivalent** of `floor(next_float × range)` — the multiply-shift `(uint)(((ulong)NextRaw() × range) >> 32)` — which removes float from the hottest, most determinism-critical path and makes the F4/F5 boundary clamp structurally impossible. `Shuffle` is F6 (Fisher–Yates) verbatim, consuming exactly `length − 1` raw draws.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (pure BCL integer arithmetic)
**Engine Notes**: `NextFloat` is defined byte-stably in `double` (`raw × 2^-32`, exact in IEEE-754 because `raw < 2^32 ≤ 2^53`) and is deliberately **off** the board-refill path. No 32-bit float, `Mathf`, or `Math.Floor` on the draw path.

**Control Manifest Rules (Domain layer)**:
- Required: `NextInt`/`NextColor` use the exact integer multiply-shift `(raw × range) >> 32`, never `floor(next_float × range)` — this is the normative core path, not an approximation.
- Required: `IRngService` implements `rng-service.md` §6 1:1 (`NextFloat`, `NextInt`, `NextColor`, `Shuffle`).
- Forbidden: 32-bit `float`/`Mathf`/`MathF`/`Math.Floor` on the RNG core/draw path (`Assets/Domain/Rng/**`); a rejection loop or variable per-`next_int` draw count (would break F6's `length − 1` contract).
- Guardrail: one add + one finalizer + one 64-bit multiply-shift per draw, all integer.

---

## Acceptance Criteria

*From GDD `design/gdd/rng-service.md` (Distribution Fairness, Input Validation) and ADR-004 §2, scoped to this story:*

- [ ] `NextInt(stream, min, max)` returns a uniform integer in `[min, max]` inclusive via `min + (int)(((ulong)NextRaw() × (uint)(max−min+1)) >> 32)`, consuming **exactly one** raw draw.
- [ ] `NextColor<T>(stream, activeColors)` returns `activeColors[idx]` via the same multiply-shift over `activeColors.Count`, uniformly.
- [ ] `Shuffle<T>(stream, source)` returns a **new** array (never mutates the input), Fisher–Yates high-to-low with `NextInt(0,i)` per step, consuming exactly `source.Count − 1` raw draws.
- [ ] `test_uniform_color_distribution`: over 100,000 `NextColor` draws on a 5-element pool, each color's frequency is within ±2% of 20% (chi-square goodness-of-fit p > 0.01).
- [ ] `test_next_int_bounds_respected`: 10,000 draws across several `(min,max)` pairs never return a value outside `[min,max]`.
- [ ] `test_shuffle_preserves_multiset` and `test_shuffle_does_not_mutate_input`: for arrays of length 0, 1, 2, and 20 — the result is a permutation (same multiset), and the input array is unchanged.
- [ ] `test_next_int_invalid_range_errors`: `min > max` raises immediately (no silent swap).
- [ ] `test_next_color_empty_pool_errors`: an empty color pool raises immediately (no default color substituted).
- [ ] `test_boundary_rounding_clamped`: reframed — the multiply-shift can never yield `range` (max `idx == range − 1` by construction), so the F4/F5 "only silent clamp" is satisfied structurally; document this reframing in the test.

---

## Implementation Notes

*Derived from ADR-004 Implementation Guidelines §2:*

- `NextInt` and `NextColor` are the ADR-004 §2 snippets verbatim, including the loud `ArgumentException` on `min > max` (GDD Edge Case: no swap) and empty pool.
- `Shuffle` is the ADR-004 §2 listing: copy-in first (`for k: r[k]=source[k]`), then Fisher–Yates `for i = len-1 downto 1 { j = NextInt(stream,0,i); swap }`; return the copy. Length 0/1 consume zero draws.
- `NextFloat(stream) => NextRaw(ref S(stream)) * (1.0/4294967296.0)` — `double`, off the board path, kept for a future caller only.
- Draws dispatch by `string streamName` (a registry lookup only — the string is never fed to the generator); resolve the `StreamState` via the per-stream dictionary from Story 003 (this story consumes that seam — coordinate ordering so `NextRaw(ref S(stream))` resolves the correct state).
- All draw arithmetic stays `unchecked uint`/`ulong` in `Assets/Domain/Rng/**`.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: the `Mix32`/`SplitMix32`/`Combine` primitives, `NextRaw`, and F1–F3 seed derivation.
- Story 003: `StreamState` lifetime/isolation, `ForkStream`, session log — this story assumes `S(streamName)` resolves a valid state.
- Story 004: the golden-vector fixture that pins these draw sequences byte-for-byte.

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: uniform int bounds (`test_next_int_bounds_respected`).
  - Given: several `(min,max)` pairs.
  - When: 10,000 `NextInt` draws per pair.
  - Then: every result is within `[min,max]`; the max index equals `range − 1` and never `range`.
  - Edge cases: `min == max` returns `min` deterministically; single draw consumed per call.

- **AC-2**: uniform color distribution (`test_uniform_color_distribution`).
  - Given: a fixed test seed and a 5-element pool.
  - When: 100,000 `NextColor` draws.
  - Then: each color within ±2% of 20%; chi-square p > 0.01.

- **AC-3**: shuffle multiset & purity (`test_shuffle_preserves_multiset`, `test_shuffle_does_not_mutate_input`).
  - Given: arrays of length 0, 1, 2, 20.
  - When: `Shuffle` runs.
  - Then: result is a permutation of the input multiset; input array unchanged; draws consumed `== length − 1`.

- **AC-4**: input validation (`test_next_int_invalid_range_errors`, `test_next_color_empty_pool_errors`).
  - Given: `min > max`, and an empty color pool.
  - When: `NextInt`/`NextColor` called.
  - Then: each raises an exception/assertion immediately; no value or default is returned.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/rng-service/rng_draw_api_test.cs` — must exist and pass. In-project: `src/SweetCascade/Assets/Tests/EditMode/Rng/` (ADR-004 §5), headless Mono via game-ci.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 001** (needs `NextRaw`, the constants, and the session seed lifecycle). Transitively **E01 Story 002/003**.
- Unlocks: Story 003 (fork/isolation reuse the draw API), Story 004 (golden vectors include `NextInt(0,4)`, `NextColor`, `NextFloat`, and `Shuffle` sequences).
