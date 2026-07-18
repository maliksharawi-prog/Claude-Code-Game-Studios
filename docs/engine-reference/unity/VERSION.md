# Unity Engine — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 6.3 LTS (6000.3.x) |
| **Release Date** | December 2025 |
| **Support Window** | LTS through December 2027 |
| **Project Pinned** | 2026-07-18 — ADR-001 (`docs/architecture/adr-001-engine-selection-unity.md`) |
| **Last Docs Verified** | 2026-07-18 |
| **LLM Knowledge Cutoff** | January 2026 (training reliably covers ~6.0/6.1) |

## Knowledge Gap Warning

The LLM's training data likely covers Unity up to ~2022 LTS (2022.3). The entire
Unity 6 release series (formerly Unity 2023 Tech Stream) introduced significant
changes that the model does NOT know about. Always cross-reference this directory
before suggesting Unity API calls.

## Post-Cutoff Version Timeline

| Version | Release | Risk Level | Key Theme |
|---------|---------|------------|-----------|
| 6.0 | Oct 2024 | HIGH | Unity 6 rebrand, new rendering features, Entities 1.3, DOTS improvements |
| 6.1 | Nov 2024 | MEDIUM | Bug fixes, stability improvements |
| 6.2 | Dec 2024 | MEDIUM | Performance optimizations, new input system improvements |
| 6.3 LTS | Dec 2025 | HIGH | First LTS since 6.0, production-ready DOTS, enhanced graphics features |

## Major Changes from 2022 LTS to Unity 6.3 LTS

### Breaking Changes
- **Entities/DOTS**: Major API overhaul in Entities 1.0+, complete redesign of ECS patterns
- **Input System**: Legacy Input Manager deprecated, new Input System is default
- **Rendering**: URP/HDRP significant upgrades, SRP Batcher improvements
- **Addressables**: Asset management workflow changes
- **Scripting**: C# 9 support, new API patterns

### New Features (Post-Cutoff)
- **DOTS**: Production-ready Entity Component System (Entities 1.3+)
- **Graphics**: Enhanced URP/HDRP pipelines, GPU Resident Drawer
- **Multiplayer**: Netcode for GameObjects improvements
- **UI Toolkit**: Production-ready for runtime UI (replaces UGUI for new projects)
- **Async Asset Loading**: Improved Addressables performance
- **Web**: WebGPU support

### Deprecated Systems
- **Legacy Input Manager**: Use new Input System package
- **Legacy Particle System**: Use Visual Effect Graph
- **UGUI**: Still supported, but UI Toolkit recommended for new projects
- **Old ECS (GameObjectEntity)**: Replaced by modern DOTS/Entities

## Unity 6.3-Specific Changes (verified 2026-07-18, official docs)

- **URP Render Graph is mandatory**: `RenderGraphSettings.enableRenderCompatibilityMode`
  is now read-only (`false`). Never author against Compatibility Mode — convert
  or stay ≤6.2 (unsupported). Project rule: Render Graph path from day one.
- **AccessibilityRole** converted from flags enum to standard enum — no bitwise
  combining; single roles only (affects screen-reader support work).
- **UI Toolkit USS parser stricter** — previously-tolerated invalid USS now
  raises validation errors; lint USS during UI implementation.
- **URP/HDRP unified shader compiler/API**; new URP Bloom filtering options
  (Kawase / Dual) — relevant to the glass-candy bloom pass (emissives only).
- HTTP/2 + gRPC support (relevant to Phase 3 backend); VFX Graph GPU-event
  instancing (relevant to cascade FX at scale).

## Project History

- 2026-07-12 → 2026-07-18: project was pinned to **Godot 4.6**
  (`docs/engine-reference/godot/VERSION.md`, retained for history).
  Superseded by ADR-001: founder decision (publishing path), 3D glass-candy
  art target, live-ops SDK ecosystem.

## Verified Sources

- Official docs: https://docs.unity3d.com/6000.0/Documentation/Manual/index.html
- Unity 6 release: https://unity.com/releases/unity-6
- Unity 6.3 LTS announcement: https://unity.com/blog/unity-6-3-lts-is-now-available
- Migration guide: https://docs.unity3d.com/6000.0/Documentation/Manual/upgrade-guides.html
- Unity 6 support: https://unity.com/releases/unity-6/support
- C# API reference: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/index.html
