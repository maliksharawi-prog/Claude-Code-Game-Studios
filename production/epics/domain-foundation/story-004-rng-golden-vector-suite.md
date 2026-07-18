# Story 004: RNG golden-vector fixture & byte-for-byte regression suite

> **Epic**: Domain Foundation (E02)
> **Status**: Ready
> **Layer**: Foundation
> **Type**: Logic
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/rng-service.md` (Determinism ACs; Edge Cases — algorithm_version)
**Requirement**: `TR-rng-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity Guard
**ADR Decision Summary**: A checked-in fixture is the frozen ground truth for `algorithm_version = "v1"`. It is generated once by the reference implementation, reviewed against the GDD anchors and `Mix32(0)=0`, committed, and thereafter immutable — any code change that alters a single value fails the suite (the intended alarm). It is the standing regression gate and the acceptance evidence for platform-stable seed derivation (TR-rng-002).

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (Domain) / cross-backend proof is the point
**Engine Notes**: The identical fixture must assert byte-for-byte under **Mono** (editor, every PR — blocking), **IL2CPP** (standalone player build in CI), and **WebGL** (in-browser). The three-backend run is the acceptance proof for TR-rng-002. Values are stored as **decimal `uint` strings** (never raw bytes — sidesteps endianness).

**Control Manifest Rules (Editor & CI + Domain)**:
- Required: the golden-vector suite (`rng_golden_v1.json` + `RngGoldenVectorTest.cs`) is blocking on every PR (Mono) and on the release matrix (IL2CPP + WebGL) — byte-for-byte.
- Required: fixture generation is code-reviewed against the GDD `combine()` anchors and `Mix32(0)=0` before it is frozen.
- Forbidden: editing a frozen `v1` value to "fix" a test — a deliberate change is an `algorithm_version` bump (`rng_golden_v2.json`), never a silent edit; never disable/skip a failing test to force CI green.
- Guardrail: L3 of the three-layer purity guard — proves determinism, versus L1/L2 which only prove absence of forbidden tokens.

---

## Acceptance Criteria

*From ADR-004 §5 (golden-vector suite) and Validation Criteria, scoped to this story:*

- [ ] `rng_golden_v1.json` is checked in with, per ADR-004 §5 Format: seed-derivation tables for F1/F2/F3 (including the GDD anchors 1007/3, 42/20650, 500/1); the first **64** `NextRaw` outputs on `board-refill` for ≥5 pinned master seeds (including 0, 1, 0xFFFFFFFF, and the F1 anchor); the first 64 `NextInt(0,4)` and `NextColor` over a 5-element pool; 8 `NextFloat` draws (double, `"R"`/17-sig-digit round-trip); a `Shuffle` permutation for `[0..19]` plus length-0/1/2 boundaries; and the fork `(seed, parent="board-refill", label="probe") → childSeed` + its first 16 draws.
- [ ] `RngGoldenVectorTest.cs` reads the fixture and asserts every value byte-for-byte against the live implementation (Stories 001–003) under Mono.
- [ ] The suite is wired as a **blocking** Edit-Mode gate on every PR (Mono) via `game-ci/unity-test-runner@v4`.
- [ ] IL2CPP + WebGL cross-backend runs are declared as the release-matrix acceptance for TR-rng-002 (executed under the release pipeline — see Out of Scope for the ADR-J boundary).
- [ ] The fixture's `NextFloat` entries use the exact round-trip `double` formatting so re-reads reproduce the stored value.
- [ ] The test file documents the F4/F5 integer-refinement reframing (max index `== range − 1`, no runtime clamp) so a future GDD reviewer does not mistake it for a divergence.

---

## Implementation Notes

*Derived from ADR-004 Implementation Guidelines §5–§6 and Migration Plan step 4:*

- Location: `src/SweetCascade/Assets/Tests/EditMode/Rng/golden/rng_golden_v1.json` (fixture) + `RngGoldenVectorTest.cs` (NUnit reader/asserter).
- Generate the fixture **once** from the reference implementation, review it against `Combine(...)` GDD anchors and `Mix32(0)==0`, then freeze. Do not hand-compute values.
- Store all integer values as decimal `uint` strings keyed by input; store `NextFloat` doubles with `ToString("R")` (or 17 significant digits).
- The Mono PR gate is this story's blocking deliverable. The **IL2CPP standalone player build** and **WebGL in-browser** runs depend on the full build/export matrix (ADR-J, deferred per the E01 epic gap note) — declare them as the release-matrix acceptance and wire them when ADR-J lands; do not block this story's Mono gate on them.
- If any value drifts, the correct response is to investigate the regression, not to regenerate — regeneration is reserved for a deliberate `algorithm_version = "v2"` bump.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Stories 001–003: the primitives, draw API, isolation/fork/log that the fixture pins.
- **ADR-J (deferred)**: the full IL2CPP/WebGL player-build CI matrix and license/export wiring (flagged in the E01 epic as not-yet-written). This story wires the Mono PR gate and declares the cross-backend acceptance; it does not stand up the export matrix.

---

## QA Test Cases

*Authored at story creation (lean mode). The developer implements against these — do not invent new test cases during implementation.*

- **AC-1**: golden fixture completeness.
  - Given: `rng_golden_v1.json`.
  - When: parsed.
  - Then: it contains all ADR-004 §5 sections (F1/F2/F3 tables incl. anchors; 64 `NextRaw`; 64 `NextInt(0,4)` + `NextColor`; 8 `NextFloat`; `Shuffle` `[0..19]` + boundaries; fork childSeed + 16 draws).

- **AC-2**: byte-for-byte assertion (Mono).
  - Given: the frozen fixture + the live implementation.
  - When: `RngGoldenVectorTest` runs under Mono.
  - Then: every value matches exactly; `Mix32(0)==0` and the three `combine()` anchors are re-asserted from the fixture.
  - Edge cases: `NextFloat` doubles round-trip identically; `Shuffle` length-0/1/2 consume 0/0/1 draws respectively.

- **AC-3**: regression alarm.
  - Given: a deliberately altered constant in the implementation.
  - When: the suite runs.
  - Then: it fails (the intended alarm), proving the fixture is load-bearing.

- **AC-4**: blocking-gate wiring.
  - Setup: open a PR touching `Assets/Domain/Rng/`.
  - Verify: `game-ci/unity-test-runner@v4` runs the Edit-Mode golden suite as a required check.
  - Pass condition: a failing golden assertion blocks merge.

---

## Test Evidence

**Story Type**: Logic
**Required evidence**:
- Logic: `tests/unit/rng-service/rng_golden_vector_test.cs` + committed fixture `.../golden/rng_golden_v1.json` — must exist and pass under Mono. Cross-backend (IL2CPP/WebGL) acceptance is release-matrix (ADR-J). In-project: `src/SweetCascade/Assets/Tests/EditMode/Rng/golden/`.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: **Story 001, Story 002, Story 003** (the fixture pins the full RNG surface). Transitively **E01 Story 002/003/004** (asmdefs, denylist, game-ci gate).
- Unlocks: the Board Engine slice (E03) which consumes `board-refill` and relies on this fixture as the standing determinism gate.
