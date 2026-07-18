# ADR-001: Engine Selection — Unity 6.3 LTS (supersedes Godot 4.6 pin)

*Status: Accepted · Date: 2026-07-18 · Decider: Founder (with studio recommendation)*

## Context

The project was pinned to Godot 4.6 + GDScript at kickoff (2026-07-17, autonomous
default: best-in-class 2D, MIT-free, clean web export). Since then, three facts
changed the calculus:

1. **Founder decision**: "I will use Unity as it would be easy for me to publish
   the game later" (2026-07-18) — store publishing ease is a founder priority.
2. **Art direction pivoted to a polished 3D "glass candy" target** (founder-supplied
   concept render; `docs/architecture/visual-interface-blueprint.md`). Unity URP's
   mobile 3D pipeline is more production-proven on mid-range Android than Godot 4's
   mobile Vulkan renderer.
3. **Live-ops/social ambitions** (leaderboards, events, eventual F2P monetization
   per the concept doc) depend on the mobile SDK ecosystem — IAP, ads mediation,
   push, attribution, analytics — which is Unity-first across nearly every vendor.

## Decision

Adopt **Unity 6.3 LTS (6000.3.x)** with **C#** as the production engine and language.
6.3 LTS is Unity's recommended lock-in release for productions, supported until
December 2027.

## Alternatives considered

- **Stay on Godot 4.6 + GDScript**: zero licensing cost forever, lighter editor,
  better web export. Rejected because: iOS pipeline requires more manual work,
  third-party live-ops SDKs are largely community-maintained, and mobile 3D at
  our polish target carries more risk. Web remains covered by the HTML playable
  slice regardless.
- **Unity 6.0 LTS**: support ends October 2026 — too short a runway.
- **Unreal**: rejected at kickoff (mobile-first casual 2D/3D-lite is a poor fit;
  heaviest pipeline).

## Consequences

- `CLAUDE.md`, `.claude/docs/technical-preferences.md`, and the engine reference
  library re-pin to Unity 6.3 LTS; Godot reference docs remain in-tree for history.
- Test/CI scaffold switches from gdUnit4 to **Unity Test Framework** via
  `game-ci/unity-test-runner` (guarded until the Unity project exists).
- The engine-agnostic C# domain scripts in `visual-interface-blueprint.md` become
  the seed of the production implementation (they encode the approved GDD
  contracts already).
- All 11 approved GDDs are engine-neutral by design; their per-engine notes get
  swept during `/create-architecture` (Godot signal references become C# events /
  UnityEvents; determinism, seams, and formulas are unchanged).
- Knowledge-gap risk: LLM training covers ~Unity 6.0/6.1. Unity 6.2+ changes are
  tracked in `docs/engine-reference/unity/VERSION.md` (RenderGraph compatibility
  mode removal, AccessibilityRole enum change, USS parser strictness, unified
  URP/HDRP compiler). Agents must consult it before citing Unity APIs.
- Licensing: Unity Personal free below $200K revenue/200K installs; revisit at
  scale. The 2023 runtime-fee episode is noted as vendor-policy risk, mitigated
  by the LTS pin and the engine-neutral GDD layer.

## Rollback

The GDD layer is engine-neutral and the playable slice is engine-independent;
reverting to Godot means restoring the previous CLAUDE.md/technical-preferences
pins and the gdUnit4 scaffold (all in git history at commit `0e7828c` and prior).
