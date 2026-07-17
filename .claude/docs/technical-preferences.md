# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Godot 4.6 (pinned — see `docs/engine-reference/godot/VERSION.md`)
- **Language**: GDScript
- **Rendering**: 2D, Mobile renderer (Vulkan; GL Compatibility fallback for Web export)
- **Physics**: None required for core gameplay (board logic is grid-based, not physics-driven); engine default (Jolt) untouched

## Input & Platform

- **Target Platforms**: Mobile (iOS / Android) primary, Web (browser) secondary
- **Input Methods**: Touch, Mouse (web/desktop testing)
- **Primary Input**: Touch (tap-to-select and swipe-to-swap)
- **Gamepad Support**: None
- **Touch Support**: Full
- **Platform Notes**: Portrait orientation, one-handed play. All interactive elements ≥ 44px touch targets. No hover-only interactions. Web build must work with mouse using the same tap/swipe model.

## Naming Conventions

- **Classes**: PascalCase (e.g., `BoardController`)
- **Variables**: snake_case (e.g., `move_count`); functions snake_case (e.g., `resolve_matches()`)
- **Signals/Events**: snake_case past tense (e.g., `match_cleared`, `cascade_ended`)
- **Files**: snake_case matching class (e.g., `board_controller.gd`)
- **Scenes/Prefabs**: PascalCase matching root node (e.g., `BoardController.tscn`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `BOARD_WIDTH`)

## Performance Budgets

- **Target Framerate**: 60 fps on mid-range mobile (e.g., 2022-era Android)
- **Frame Budget**: 16.6 ms
- **Draw Calls**: ≤ 100 during heaviest cascade (batch candy sprites via atlas)
- **Memory Ceiling**: ≤ 400 MB on mobile

## Testing

- **Framework**: gdUnit4 (runner: `godot --headless --script tests/gdunit4_runner.gd`)
- **Minimum Coverage**: All board-logic and scoring formulas unit-tested (match detection, cascade resolution, special-candy creation rules, star thresholds)
- **Required Tests**: Balance formulas, gameplay systems, networking (if applicable)
- **Determinism rule**: All board RNG must be seedable so match/cascade tests are reproducible

## Forbidden Patterns

- [None configured yet — add as architectural decisions are made]

## Allowed Libraries / Addons

- [None configured yet — add as dependencies are approved]

## Architecture Decisions Log

- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

- **Primary**: godot-specialist
- **Language/Code Specialist**: godot-gdscript-specialist (all .gd files)
- **Shader Specialist**: godot-shader-specialist (.gdshader files, VisualShader resources)
- **UI Specialist**: godot-specialist (no dedicated UI specialist — primary covers all UI)
- **Additional Specialists**: godot-gdextension-specialist (GDExtension / native C++ bindings only)
- **Routing Notes**: Invoke primary for architecture decisions, ADR validation, and cross-cutting code review. Invoke GDScript specialist for code quality, signal architecture, static typing enforcement, and GDScript idioms. Invoke shader specialist for material design and shader code. Invoke GDExtension specialist only when native extensions are involved.

### File Extension Routing

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.gd files) | godot-gdscript-specialist |
| Shader / material files (.gdshader, VisualShader) | godot-shader-specialist |
| UI / screen files (Control nodes, CanvasLayer) | godot-specialist |
| Scene / prefab / level files (.tscn, .tres) | godot-specialist |
| Native extension / plugin files (.gdextension, C++) | godot-gdextension-specialist |
| General architecture review | godot-specialist |
