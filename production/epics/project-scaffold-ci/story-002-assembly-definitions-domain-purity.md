# Story 002: Five assembly definitions + one-way dependency direction + Domain `noEngineReferences`

> **Epic**: Project Scaffold & CI Activation (E01)
> **Status**: In Progress — file-based scaffold complete + verified 2026-07-18; awaiting human Unity editor pass (.meta/lock/URP-asset items) per qa-plan-sprint-01. See production/sprint-status.yaml.
> **Layer**: Foundation (infrastructure)
> **Type**: Integration
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: — (governed by `docs/architecture/architecture.md` §5 Assembly Architecture)
**Requirement**: `TR-perf-002`
*(This story satisfies the "domain assembly zero-`UnityEngine`" (L1 asmdef layer) and the compiler-enforced assembly-boundary portion of TR-perf-002. Read the current text fresh from `docs/architecture/tr-registry.yaml`.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity CI Guard (primary — defines the L1 asmdef purity layer)
**Governing ADRs (secondary)**: ADR-001 (C#/asmdef toolchain); dependency-direction authority is `architecture.md` §5.1 (compiler-enforced invariant).
**ADR Decision Summary**: Domain purity is enforced at three independent layers; **L1** is the `SweetCascade.Domain.asmdef` set to `noEngineReferences: true` with an empty `references` list, so any `UnityEngine`/`UnityEditor` symbol fails to compile. (L2 denylist scan = Story 003; L3 golden vectors = E02.)

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: LOW (asmdef JSON is stable pre-cutoff API)
**Engine Notes**: Assembly Definition files are engine-stable across 2022→6.3; no post-cutoff API. `noEngineReferences: true` blocks `UnityEngine`/`UnityEditor` but **not** BCL types like `System.Random` — that gap is closed by Story 003 (L2), by design.

**Control Manifest Rules (Domain + cross-cutting)**:
- Required: `SweetCascade.Domain.asmdef` sets `noEngineReferences: true`; zero `UnityEngine`/`UnityEditor` symbols in compiled Domain (CI-guarded).
- Required (cross-cutting): assembly dependency direction is one-way and compiler-enforced — `Domain` references nothing above it and no engine assembly; `Game → Domain`; `UI → Game` (UI may reference `Domain` downward for pure DTO/record types). No assembly ever references one above it.
- Forbidden: engine types (`UnityEngine.*`/`UnityEditor.*`) inside the Domain assembly.

---

## Acceptance Criteria

*From `architecture.md` §5.1/§5.3 and the E01 Definition of Done, scoped to this story:*

- [ ] Five assembly definitions exist at the §5.3 locations:
  - `Assets/Domain/SweetCascade.Domain.asmdef` — `noEngineReferences: true`, `references: []`, `allowUnsafeCode: false`.
  - `Assets/Game/SweetCascade.Game.asmdef` — references `SweetCascade.Domain`.
  - `Assets/UI/SweetCascade.UI.asmdef` — references `SweetCascade.Game` (and may reference `SweetCascade.Domain` downward for DTOs).
  - `Assets/Editor/SweetCascade.Editor.asmdef` — Editor-platform-only; may reference `Domain` and `Game` downward.
  - `Assets/Tests/EditMode/SweetCascade.Domain.Tests.asmdef` — references `SweetCascade.Domain` (test asmdef; Domain-only, headless). *(The PlayMode `SweetCascade.Game.Tests.asmdef` is created in Story 005; the fifth production-side asmdef counted here is Editor.)*
- [ ] The dependency direction is acyclic and one-way: Domain → (nothing), Game → Domain, UI → Game(+Domain), Editor → Game/Domain. No asmdef references one above it.
- [ ] `SweetCascade.Domain` compiles with `noEngineReferences: true`.
- [ ] A **deliberate upward reference fails compilation**: a temporary `using UnityEngine;` (or a `SweetCascade.Game` type reference) placed in a Domain file causes a compile error; removing it restores a clean build. This negative result is captured as evidence, then the probe is removed.
- [ ] Each asmdef sub-folder tree from §5.3 exists under its assembly (`Domain/` → `Board/ Specials/ Scoring/ Objectives/ Rng/ Levels/ Save/ Events/`; `Game/` → `Views/ Juice/ Input/ ScreenFlow/ Save/ Loading/ Bridge/`), empty/`.gitkeep` at this stage — real code lands in later epics.

---

## Implementation Notes

*Derived from ADR-004 §6 (L1) and `architecture.md` §5.1/§5.3:*

**File-based (no editor GUI required — asmdefs are JSON):**
- `SweetCascade.Domain.asmdef` (verbatim shape from ADR-004 §6 L1):
  ```json
  { "name": "SweetCascade.Domain", "noEngineReferences": true,
    "references": [], "autoReferenced": true, "allowUnsafeCode": false }
  ```
- `SweetCascade.Game.asmdef`: `"references": ["SweetCascade.Domain"]`, engine references allowed (default).
- `SweetCascade.UI.asmdef`: `"references": ["SweetCascade.Game", "SweetCascade.Domain"]`.
- `SweetCascade.Editor.asmdef`: `"references": ["SweetCascade.Game", "SweetCascade.Domain"]`, `"includePlatforms": ["Editor"]`.
- `SweetCascade.Domain.Tests.asmdef`: references `SweetCascade.Domain` + the Test Framework (`UnityEngine.TestRunner`, `UnityEditor.TestRunner`) with `"optionalUnityReferences": ["TestAssemblies"]` (or the 6.3 equivalent `defineConstraints`/testables entry).
- Create the §5.3 sub-folders with `.gitkeep`.

**Manual editor-checklist items (require the Unity 6.3 editor):**
- [ ] Open the project so Unity compiles the assemblies and generates `.asmdef.meta` GUIDs; commit the `.meta` files (asmdef references resolve by name, but Unity still tracks meta GUIDs).
- [ ] Run the deliberate-upward-reference probe: add `using UnityEngine;` to a throwaway `Assets/Domain/_PurityProbe.cs`, confirm the Domain assembly fails to compile in the Console, screenshot the error, then delete the probe file. (This is the L1 proof for the E01 DoD.)

**Note on the "five asmdefs":** §5.1 names five production-relevant assemblies (Domain, Game, UI, Editor, plus the two test asmdefs). This story delivers Domain, Game, UI, Editor, and the EditMode Domain.Tests asmdef; the PlayMode Game.Tests asmdef is delivered in Story 005 so both test skeletons land with their example tests together.

---

## Out of Scope

*Handled by neighbouring stories — do not implement here:*

- Story 001: project creation, URP, packages, player settings.
- Story 003: the L2 CI denylist `ripgrep` scan (catches `System.Random` etc. that L1 cannot).
- Story 005: the PlayMode `SweetCascade.Game.Tests.asmdef` + example EditMode/PlayMode tests.
- Story 006: the `Editor/` sub-folder layout (ManifestGen/LevelValidation/LevelPreview) + manifest-generator stub.
- No RNG, board, save, or manifest **logic** — only the empty engine-free container asmdefs. (RNG/save/manifest implementations are E02.)

---

## QA Test Cases

*Authored at story creation (lean mode). The assembly boundary is proven by a compile-time negative test, which cannot be a "passing" automated test — it is a documented build-failure probe plus a positive clean-compile.*

**Manual check — AC: five asmdefs + one-way direction**
- Setup: inspect the five `.asmdef` files and their `references` arrays.
- Verify: Domain `references: []` + `noEngineReferences: true`; Game → Domain; UI → Game(+Domain); Editor → Game/Domain (Editor-platform only); Domain.Tests → Domain. No file references an assembly above it.
- Pass condition: the reference graph is acyclic and strictly downward; Domain references neither an engine assembly nor any project assembly.

**Test — AC: Domain compiles pure; upward reference fails (L1 proof)**
- Given: `SweetCascade.Domain.asmdef` with `noEngineReferences: true` and clean Domain sources.
- When: the project compiles.
- Then: Domain compiles with zero errors (positive), AND a temporary `using UnityEngine;` in a Domain file produces a compile error naming the unresolved namespace (negative).
- Edge cases: also confirm referencing a `SweetCascade.Game` type from Domain fails to compile (not only the `using UnityEngine` case); confirm removing the probe returns to a clean build.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: Integration test OR documented playtest. Evidence is a documented build verification: `production/qa/evidence/story-002-asmdef-boundary-evidence.md` capturing (a) the clean Domain compile and (b) the screenshotted compile error from the deliberate `using UnityEngine;` upward-reference probe. This is the L1 layer of the three-layer purity guard; L2 (Story 003) and L3 (E02 golden vectors) complete it.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (needs the Unity project + `Assets/` tree).
- Unlocks: Story 003 (denylist scan needs the `Assets/Domain/` tree), Story 005 (test asmdefs reference Domain), Story 006 (Editor sub-folders live under the Editor asmdef).
