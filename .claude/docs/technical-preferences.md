# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->
<!-- Engine re-pinned 2026-07-18 per ADR-001 (Godot 4.6 → Unity 6.3 LTS, founder decision). -->

## Engine & Language

- **Engine**: Unity 6.3 LTS (6000.3.x) — pinned via ADR-001; see `docs/engine-reference/unity/VERSION.md`
- **Language**: C#
- **Rendering**: URP (mobile), Render Graph path ONLY (Compatibility Mode removed in 6.3); 3D "glass candy" target per `docs/architecture/visual-interface-blueprint.md`; bloom restricted to emissives
- **UI**: UI Toolkit for screens/HUD (validate USS against 6.3's stricter parser); world-space FX outside UI Toolkit
- **Physics**: None for core gameplay (board logic is grid-based, headless C# domain model — no physics dependency)

## Input & Platform

- **Target Platforms**: Mobile (iOS / Android) primary, Web (secondary — served by the HTML playable slice; Unity WebGL optional later)
- **Input Methods**: Touch, Mouse (editor/desktop testing) — Unity Input System package (legacy Input Manager deprecated)
- **Primary Input**: Touch (tap-to-select and swipe-to-swap per `design/gdd/touch-input.md`)
- **Gamepad Support**: None
- **Touch Support**: Full
- **Platform Notes**: Portrait orientation, one-handed play. All interactive elements ≥ 44px touch targets (44px floor proven in `board-engine.md` Formula 4). No hover-only interactions; hover is additive on desktop only.

## Naming Conventions (C#)

- **Classes**: PascalCase (e.g., `BoardModel`)
- **Public fields/properties**: PascalCase (e.g., `MoveCount`)
- **Private fields**: _camelCase (e.g., `_moveCount`)
- **Methods**: PascalCase (e.g., `ResolveMatches()`)
- **Events**: PascalCase past tense (e.g., `MatchCleared`, `CascadeEnded`) — mirrors the GDD signal catalog
- **Files**: PascalCase matching class (e.g., `BoardModel.cs`)
- **Prefabs/Scenes**: PascalCase (e.g., `FruitPiece.prefab`, `Gameplay.unity`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE for GDD-registry constants (e.g., `MAX_CASCADE_DEPTH`)

## Performance Budgets

- **Target Framerate**: 60 fps on mid-range mobile (e.g., 2022-era Android)
- **Frame Budget**: 16.6 ms
- **Draw Calls**: ≤ 100 during heaviest cascade — 5 shared fruit meshes with per-instance hue (GPU instancing / SRP Batcher), cell wells via one instanced draw, pooled FX
- **Shadows**: NO realtime shadows on mobile — blob-shadow decals per the visual blueprint; one key directional light
- **Memory Ceiling**: ≤ 400 MB on mobile

## Testing

- **Framework**: Unity Test Framework — Edit Mode for the headless C# domain layer (BoardModel/SpecialResolver/ScoreKeeper), Play Mode for integration
- **CI Runner**: `game-ci/unity-test-runner@v4` (GitHub Actions), blocking gate on PRs and pushes to main
- **Minimum Coverage**: All board-logic and scoring formulas unit-tested (match detection, cascade resolution, special-candy creation rules, star thresholds)
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)
- **Determinism rule**: All board RNG must be seedable (stream-based per `design/gdd/rng-service.md`) so match/cascade tests are reproducible

## Forbidden Patterns

- URP Compatibility Mode (removed in 6.3 — Render Graph only)
- Legacy Input Manager (deprecated — Input System package only)
- Bitwise-combined `AccessibilityRole` values (standard enum since 6.3)
- Engine types (`UnityEngine.*`) inside the domain-logic assembly — the board/scoring/objectives layer stays pure C# and headless-testable (board-engine.md logic/presentation boundary)

## Allowed Libraries / Addons

- Unity Input System, UI Toolkit, Addressables, Unity Test Framework (core kit)
- [Add others only when actively integrated — no speculative dependencies]

## Architecture Decisions Log

- ADR-001: Engine Selection — Unity 6.3 LTS (Accepted, 2026-07-18)

## Engine Specialists

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP materials — owns the glass-candy material set)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS/Jobs/Burst — not currently used), unity-addressables-specialist (asset loading, memory management)
- **Routing Notes**: Invoke primary for architecture and general C# review. Invoke shader specialist for the CandyGlass/GildedCream/BombOrb material set and rendering. Invoke UI specialist for all interface implementation. Invoke Addressables specialist for asset management. DOTS specialist only if a profiled need emerges (unlikely at this scope).

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, UI documents) | unity-ui-specialist |
| Scene / prefab files (.unity, .prefab) | unity-specialist |
| Native plugins | unity-specialist |
| General architecture review | unity-specialist |
