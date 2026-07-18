# Epic: Rendering & Glass-Candy Materials

> **Epic ID**: E08
> **Layer**: Presentation
> **GDD**: `docs/architecture/visual-interface-blueprint.md` (glass-candy target) · `.claude/docs/technical-preferences.md` (perf budgets) · `docs/architecture/architecture.md` §9
> **Architecture Module**: `SweetCascade.Game` render path — the 13-material glass-candy set, 5 shared fruit meshes (per-instance hue via `MaterialPropertyBlock`), one-instanced-draw 64-well grid, blob-shadow decals, emissive-only bloom, the BoardPresenter's material skinning
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories rendering-materials`

## Scope

The visual "glass candy" render layer that skins the E05 grey-box BoardPresenter to the
founder-approved target while holding the draw-call/memory budget. Delivers: the 13-material set
(CandyGlass instanced per-hue, GildedCream, GlassPanel, WellA/WellB, CreamStripe, BombOrb
emissive, BlobShadow, Gradient, HillSoft, Bokeh, Vignette, BurstAdditive, CreamChipUI) authored
as SRP-Batcher-compatible shared shader families; the 5 shared fruit meshes with per-instance
hue; the 64 wells as one GPU-instanced draw; blob-shadow decals (no realtime shadows); one
directional key light; orthographic straight-on camera (grid readability is the accessibility
contract, never sacrificed to angle); and the emissive-only bloom via a **URP Bloom Volume
override** (threshold above the LDR range — no custom pass needed at MVP). Owns the design-time
draw-call accounting (≈26–39, ≤100 ceiling); E11 re-profiles it on device.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-001: Engine Selection | URP, Render Graph path only; unified URP/HDRP shader compiler; new Bloom filtering (Kawase/Dual) | HIGH |
| ADR-002 (memory) | Shared meshes + materials + atlas + pooled prefabs resident budget (≤400MB) | LOW |
| arch §9 | Draw-call budget accounting, blob-shadow/one-light/emissive-bloom rendering decisions | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-perf-001 | 60fps / 16.6ms; ≤100 draw calls heaviest cascade; ≤400MB; no realtime shadows (blob decals); one directional light | arch §9 + ADR-002 (memory) ✅ |

## Depends On

- **E01** (URP Render Graph project template + assemblies).
- **E05** (BoardPresenter's pooled FruitPiece GameObjects + cell→transform mapping to skin).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **HIGH — URP Render Graph is mandatory.** If any custom pass is ever added (not needed at
  MVP — bloom is a Volume override), it uses `RecordRenderGraph(RenderGraph, ContextContainer)`,
  **never** the deprecated `Execute(ScriptableRenderContext, ref RenderingData)`.
- **HIGH — new URP Bloom filtering options (Kawase / Dual)** in 6.3 — relevant to the emissive
  bloom pass; unified URP/HDRP shader compiler (authored via `unity-shader-specialist`).
- GPU Instancing + SRP Batcher on fruit and well materials; per-instance hue via
  `MaterialPropertyBlock`. Legacy Particle System is out of scope here (juice FX is E09).
- **Validation gate (QQ-05):** the ≤100 draw-call accounting (arch §9.3) is a **design-time
  estimate** — it MUST be re-profiled on 2022-era mid-range Android at the Vertical Slice feel
  checkpoint (owned by **E11** / performance-analyst). This epic owns the budget; E11 proves it.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- The 13-material set is authored, SRP-Batcher-compatible, and applied; the board renders in the
  glass-candy target with per-instance hue, one-draw wells, blob shadows, one directional light,
  and emissive-only bloom (non-emissives do not bloom).
- The editor-profiled heaviest-cascade frame lands under the ≤100 draw-call ceiling with margin
  (device confirmation deferred to E11).
- Visual/Feel stories have screenshot + lead sign-off evidence in `production/qa/evidence/`;
  the visual-interface-blueprint target is met.

## Next Step

Run `/create-stories rendering-materials`.
