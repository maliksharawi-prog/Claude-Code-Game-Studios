# Story 001: Unity 6.3 project shell — URP Render Graph, portrait player settings, package manifest

> **Epic**: Project Scaffold & CI Activation (E01)
> **Status**: Ready
> **Layer**: Foundation (infrastructure)
> **Type**: Integration
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: — (E01 has no per-mechanic GDD; governed by `.claude/docs/technical-preferences.md` + `docs/architecture/architecture.md` §5 / §9)
**Requirement**: `TR-perf-002`
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time. This story satisfies the "URP Render Graph path only; Input System only; UI Toolkit" portion of TR-perf-002.)*

**ADR Governing Implementation**: ADR-001: Engine Selection — Unity 6.3 LTS
**ADR Decision Summary**: Unity 6.3 LTS (6000.3.x) + C#, URP on the Render Graph path (Compatibility Mode is read-only `false` in 6.3), Input System package as the default backend, UI Toolkit for screens/HUD.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: HIGH
**Engine Notes** (from `docs/engine-reference/unity/VERSION.md`, verified 2026-07-18):
- **URP Render Graph is mandatory.** `RenderGraphSettings.enableRenderCompatibilityMode` is read-only `false` — author the pipeline asset on the Render Graph path from day one; there is no fallback and ≤6.2 is unsupported.
- **Input System is the default backend.** Set Active Input Handling to the new Input System; the legacy `Input` Manager is deprecated/forbidden.
- **UI Toolkit runtime** enabled (USS is linted downstream in E07 against 6.3's stricter parser — not this story).
- LLM training reliably covers only ~6.0/6.1 — cross-reference the engine reference before citing any 6.2+ API name.

**Control Manifest Rules (Game / rendering layer)**:
- Required: URP, Render Graph path only; any future custom pass uses `RecordRenderGraph(RenderGraph, ContextContainer)`.
- Required: Input System package only (`EnhancedTouch`/`Pointer`/`Mouse`); portrait orientation, one-handed play.
- Forbidden: URP Compatibility Mode; legacy `Input` class; `UGUI`/`Canvas`/`Text`/`Image` for new screens.
- Guardrail: 60fps / 16.6ms on 2022-era mid-range Android; no realtime shadows (one directional key light).

---

## Acceptance Criteria

*From `.claude/docs/technical-preferences.md`, `architecture.md` §5.3/§9.1, and the E01 Definition of Done, scoped to this story:*

- [ ] A Unity project exists at `src/SweetCascade/` pinned to Unity 6.3 LTS (`ProjectSettings/ProjectVersion.txt` reports a `6000.3.x` editor version).
- [ ] `Packages/manifest.json` declares the four approved packages: Input System, UI Toolkit (`com.unity.ui`/module as applicable in 6.3), Addressables, and Test Framework — and no speculative/unapproved dependency.
- [ ] A URP pipeline asset + renderer exist and are assigned as the active Render Pipeline (Graphics + Quality settings). The pipeline is on the **Render Graph** path; `enableRenderCompatibilityMode` is `false` and no Compatibility-Mode warning appears.
- [ ] Active Input Handling is set to **Input System Package** (new) — not "Both", not legacy.
- [ ] Player Settings: **portrait** orientation locked (Default Orientation = Portrait, auto-rotation disabled/limited to portrait), mobile (iOS/Android) as primary target.
- [ ] An empty `Gameplay.unity` (or `Boot.unity`) scene renders in the editor at 60fps with one directional key light, no realtime shadows, and **no** Compatibility-Mode warning in the console.
- [ ] The `Assets/` root folder layout from `architecture.md` §5.3 is stubbed (`Domain/ Game/ UI/ Editor/ Art/ Materials/ Prefabs/ Scenes/ AddressableGroups/ Tests/`) — empty is fine; assembly definitions land in Story 002.

---

## Implementation Notes

*Derived from ADR-001, `architecture.md` §5.3/§9.1, and `VERSION.md`:*

**File-based (no editor GUI required):**
- Author `Packages/manifest.json` directly with the four approved packages pinned to their 6.3-compatible versions. Do not add Cinemachine, DOTS/Entities, Newtonsoft, or any networking package (control-manifest "Approved Libraries" — no speculative dependencies).
- Author `ProjectSettings/ProjectVersion.txt`, `ProjectSettings/ProjectSettings.asset` (orientation = portrait, Active Input Handling = Input System, mobile targets), and `ProjectSettings/GraphicsSettings.asset` / `QualitySettings.asset` to reference the URP asset. These are YAML; edit them as text where practical.
- Create the `Assets/` sub-folder skeleton (§5.3) as empty directories with `.gitkeep` so the tree is committable before the editor generates `.meta` files.

**Manual editor-checklist items (genuinely require the Unity 6.3 editor GUI):**
- [ ] Open the project once in Unity 6.3 LTS so the editor imports packages, generates `.meta` files, and materializes package-lock — commit the resulting `.meta` and `Packages/packages-lock.json`.
- [ ] Create the URP pipeline asset + Universal Renderer via `Assets ▸ Create ▸ Rendering ▸ URP Asset (with Universal Renderer)`; confirm it is authored on the Render Graph path and assign it in Graphics/Quality. (The URP asset YAML can be committed once generated, but its first creation is editor-driven.)
- [ ] Add the empty scene, one directional light, disable realtime shadows; open the Game view and confirm 60fps + a clean console (no Compatibility-Mode warning).

**Do not** stay on / re-enable Compatibility Mode — it is removed in 6.3 and forbidden.

---

## Out of Scope

*Handled by neighbouring stories or later epics — do not implement here:*

- Story 002: the five assembly definitions and their dependency direction.
- Story 005: Edit/Play Mode test assemblies + example tests.
- **E06** owns the seven-group **Addressables** layout — do **not** author any Addressables groups in this story (the Addressables *package* is added; groups are not). Never wire the level/world-map manifests into Addressables — they load via a direct serialized reference (ADR-002 as amended 2026-07-18 + ADR-006).
- **E08** owns the glass-candy material set and real rendering content; this story ships only an empty scene.
- No gameplay code, no domain logic.

---

## QA Test Cases

*Authored at story creation (lean mode — no qa-lead spawn). The developer implements against these; this story is verified largely by a documented editor smoke because a fresh empty project has nothing to assert headlessly.*

**Manual check — AC: Engine + packages pinned**
- Setup: open `src/SweetCascade/` in Unity 6.3 LTS.
- Verify: `ProjectSettings/ProjectVersion.txt` = `6000.3.x`; `Packages/manifest.json` lists Input System, UI Toolkit, Addressables, Test Framework and nothing unapproved; the editor resolves packages with no errors.
- Pass condition: version is 6.3 LTS and exactly the four approved packages (plus their transitive deps) resolve cleanly.

**Manual check — AC: URP Render Graph active, no Compatibility Mode**
- Setup: with the project open, inspect Graphics/Quality settings and the URP asset.
- Verify: a URP asset is the active pipeline; `enableRenderCompatibilityMode` is `false`; the console shows no Compatibility-Mode warning when the empty scene is loaded.
- Pass condition: Render Graph path confirmed, zero Compatibility-Mode warnings.

**Manual check — AC: Input System backend + portrait**
- Setup: Player Settings.
- Verify: Active Input Handling = Input System Package; Default Orientation = Portrait, auto-rotation restricted to portrait; iOS/Android are build targets.
- Pass condition: all three hold; legacy Input backend is not selected.

**Manual check — AC: empty scene renders at 60fps**
- Setup: enter Play (or Game view) on the empty scene with one directional light.
- Verify: stats overlay shows ~60fps in-editor; no realtime shadows; clean console.
- Pass condition: 60fps with a clean console.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: Integration test OR **documented playtest**. A fresh scaffold has no headless integration surface yet, so evidence is a documented editor smoke: `production/qa/evidence/story-001-project-scaffold-evidence.md` capturing the four manual checks above (settings screenshots + console-clean shot). PlayMode automated coverage begins in Story 005.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: None (root story of the root epic).
- Unlocks: Story 002 (assembly definitions need the project + `Assets/` tree).
