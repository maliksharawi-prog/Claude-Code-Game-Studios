# Epic: Project Scaffold & CI Activation

> **Epic ID**: E01
> **Layer**: Foundation (infrastructure)
> **GDD**: — (governed by `.claude/docs/technical-preferences.md` + `docs/architecture/architecture.md` §5 Assembly Architecture)
> **Architecture Module**: Assembly layout (SweetCascade.Domain / .Game / .UI / .Editor / .Tests), URP Render Graph project template, Domain-purity CI guard, game-ci gate
> **Status**: Ready
> **Stories**: 6 stories created 2026-07-18 (see Stories table below)

## Scope

Stand up the Unity 6.3 LTS project shell that every other epic compiles and tests inside:
the five assembly definitions with the compiler-enforced one-way dependency direction
(Domain ← Game ← UI; Editor/Tests reference downward), the `noEngineReferences` Domain
asmdef, the URP Render Graph project template (Compatibility Mode is read-only in 6.3), the
Input System package as the default input backend, the UI Toolkit runtime, and the CI
pipeline that runs Edit + Play tests plus the Domain-purity grep guard as a blocking gate.
This epic writes no gameplay logic — it delivers the container, the guardrails, and the
green CI signal that make the Domain slices (E02) buildable and provable from day one.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-001: Engine Selection — Unity 6.3 LTS | URP Render Graph path, Input System, UI Toolkit, C# — the pinned engine reality | HIGH |
| ADR-004: Deterministic RNG & Domain-Purity CI Guard | The CI check that greps compiled Domain for `UnityEngine`/`UnityEditor` symbols and fails the build if any appear | LOW |
| ADR-J (pending): CI build pipeline | `game-ci/unity-test-runner@v4`, `UNITY_LICENSE` secret, export templates — **not yet written**; stories touching the full build/export matrix are advisory-blocked until it exists | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-perf-002 | URP Render Graph path only; Input System only; UI Toolkit; domain assembly zero-`UnityEngine` (CI-guarded) | ADR-004 (CI guard) + arch §5 ✅ |

## Depends On

- **None** — this is the root epic. Every other epic depends on E01.

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **HIGH — URP Render Graph is mandatory in 6.3.** `RenderGraphSettings.enableRenderCompatibilityMode`
  is read-only `false`. The project template MUST be authored on the Render Graph path from
  day one — there is no fallback and staying on Compatibility Mode is not an option (≤6.2 is unsupported).
- **HIGH — Input System is the default backend.** Legacy `Input` Manager is deprecated and
  forbidden (technical-preferences.md). Set the project's Active Input Handling to the new
  Input System at scaffold time.
- **UI Toolkit runtime** enabled; USS is linted downstream (E07) against 6.3's stricter parser.
- **Domain-purity guard** — the `SweetCascade.Domain.asmdef` sets no engine assembly
  references + `noEngineReferences: true`; the CI grep (ADR-004) enforces zero `UnityEngine`/
  `UnityEditor` symbols. This is the architectural backbone of Pillar 2 (§5.2).

## Definition of Done

This epic is complete when:
- The five asmdefs exist with the acyclic dependency direction enforced (Domain references
  nothing above it and no engine assembly; Game → Domain; UI → Game); a deliberate upward
  reference fails compilation.
- The URP Render Graph project template renders an empty scene at 60fps in the editor with
  no Compatibility-Mode warnings.
- The Domain-purity CI guard (ADR-004) fails the build on an injected `UnityEngine` symbol
  in Domain and passes on clean Domain code.
- `game-ci/unity-test-runner@v4` runs Edit + Play test assemblies as a blocking PR/main gate
  (empty suites are acceptable; the wiring is the deliverable) per `.github/workflows/`.
- All acceptance criteria from `.claude/docs/technical-preferences.md` (engine, input,
  rendering, forbidden patterns) are satisfied by the project settings.

## Stories

| # | Story | Type | Status | ADR |
|---|-------|------|--------|-----|
| 001 | Unity 6.3 project shell — URP Render Graph, portrait player settings, package manifest | Integration | Ready | ADR-001 |
| 002 | Five assembly definitions + one-way dependency direction + Domain `noEngineReferences` | Integration | Ready | ADR-004 |
| 003 | Domain-purity CI denylist scan (L2) + injected-violation self-test | Integration | Ready | ADR-004 |
| 004 | game-ci test-runner gate activation — guard removal, license documented, first green run | Integration | Ready | ADR-004 |
| 005 | Edit/Play Mode test skeletons + example conventions test | Integration | Ready | ADR-004 |
| 006 | Editor tooling folder layout + manifest-generator stub placement | Config/Data | Ready | ADR-006 |

**Critical path**: 001 → 002 → 003 → 004 (with 005 also feeding 004's first green run). 006 is off the critical path.

**Epic-level gap flagged**: **ADR-J (CI build pipeline)** is referenced by this epic but is not yet written. No E01 story's *implementation* is governed by it — the full build/export matrix (IL2CPP/WebGL player builds) and ADR-004's **L3** cross-backend golden-vector run are deliberately scoped OUT of Story 004 and deferred to ADR-J (E11/release). Write ADR-J before those items enter sprint.

## Next Step

Run `/story-readiness production/epics/project-scaffold-ci/story-001-unity-project-urp-render-graph.md`,
then `/dev-story` to begin. Work stories in dependency order (each story's `Depends on:` field lists
its prerequisites). Recommended pre-work already partly done by `/test-setup`
(`tests/` + `.github/workflows/tests.yml` exist as a guarded skeleton; Story 004 activates them).
