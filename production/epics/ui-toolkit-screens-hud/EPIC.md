# Epic: UI Toolkit Screens & HUD

> **Epic ID**: E07
> **Layer**: Presentation
> **GDD**: `design/gdd/screen-flow.md` (per-screen data contract) · `design/gdd/world-map.md` (view)
> **Architecture Module**: `SweetCascade.UI` — HUD / Screens (UXML/USS layout of chips, buttons, overlays, Results star-ceremony host) and WorldMapView (MVP placeholder linear node list, lock/star badges)
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories ui-toolkit-screens-hud`

## Scope

The player-facing UI Toolkit layer: the UXML/USS for the in-level HUD (objective chips, moves
counter, buttons, footer), the overlay screens (Pre-Level Card, Pause, Results with its star
ceremony host), and the **WorldMapView** — the MVP placeholder linear node list with star-gated
lock/unlock badges (Formula 4 unlock gate), reading the manifest + `get_profile`. Implements the
**per-screen data contract** (each screen reads Save/LevelData/ResultsData and never writes level
results directly) against the ScreenFlowController (E05) that owns transitions. Detailed per-screen
layout is UX Designer territory (`design/ux/`, currently ❌ — run `/ux-design` first).

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-001: Engine Selection | UI Toolkit is the runtime UI path (UGUI deprecated for new screens) | HIGH |
| ADR-002 + ADR-006 | WorldMapView reads the manifest (direct-reference) + region/node data | LOW |
| arch §6 / §8.4 | Per-screen data contract; HUD/Screens ownership; ObjectiveDisplayModel / ResultsData binding | — |
| ADR-F (pending) | UI Toolkit vs UGUI for board-adjacent FX (score popups) + USS-strict / `AccessibilityRole` compliance approach (QQ-06) — **advisory**; TRs here are covered by arch §, but the popup-rendering decision affects HUD/E09 | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-sf-004 | Per-screen data contract (reads Save/LevelData; never writes level results directly) | arch §6 / §8.4 ✅ |
| TR-wm-001 | Region/node graph; star-gated unlocks; MVP placeholder linear list in `WORLD_MAP` base state | ADR-002 + ADR-006 + arch §6 ✅ |

## Depends On

- **E01** (assemblies + UI Toolkit runtime + CI).
- **E04** (`ResultsData` for the Results screen; ObjectiveDisplayModel).
- **E05** (ScreenFlowController owns transitions; screens are hosted by it).
- **E06** (`get_profile` / SaveService for star counts + unlock state; manifest for the node list).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **HIGH — UI Toolkit USS parser is stricter in 6.3.** Previously-tolerated invalid USS now
  raises validation errors — **lint USS during implementation** (verified VERSION.md, `modules/ui.md`).
- **HIGH — `AccessibilityRole` is a standard enum in 6.3** (converted from a flags enum): no
  bitwise combining; single roles only. Affects the screen-reader/accessibility pass.
- `UIDocument` + `VisualElement` / `Button` / `Label` retained-mode UI; overlays instantiated
  only when active (draw-call budget, arch §9.3).
- **Pre-req gap:** `design/ux/accessibility-requirements.md` + `design/ux/interaction-patterns.md`
  are ❌ (architecture-review handoff) — run `/ux-design` before this epic's stories.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- All USS lints clean against 6.3's strict parser; `AccessibilityRole` usage is single-enum
  (no bitwise) and passes the accessibility pass.
- The HUD binds live to `objective_progressed` / `moves_remaining_changed`; the Results screen
  hosts the star ceremony from `ResultsData`; WorldMapView renders the linear node list with
  correct lock/star badges from the profile.
- Screens honor the per-screen data contract (read-only w.r.t. level results).
- UI stories have manual-walkthrough / interaction-test evidence with lead sign-off in
  `production/qa/evidence/`; screen-flow.md and world-map.md view ACs are met.

## Next Step

Run `/create-stories ui-toolkit-screens-hud`.
