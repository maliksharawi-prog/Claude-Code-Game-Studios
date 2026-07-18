# Epic: Juice — VFX, Audio & Haptics

> **Epic ID**: E09
> **Layer**: Presentation
> **GDD**: `design/gdd/juice-layer.md` (sensory layer)
> **Architecture Module**: `SweetCascade.Game` — the sensory juice hooks: VFX (ClearBurst, pooled pop particles, atlas single-material) under the 40-call sub-budget with 4-tier LOD, the AudioDirector (music/SFX bus, runtime pitch-shift, mute gating), and the haptics map + reduced-motion / WCAG 3Hz flash-safety
> **Status**: Ready — **2 owned TRs are ADR-blocked** (ADR-G / ADR-H not yet written)
> **Stories**: Not yet created — run `/create-stories juice-vfx-audio-haptics`

## Scope

The sensory feel layer that hooks onto the E05 reveal pipeline and E08 materials to deliver
Pillar 1 ("Every Swap Sparkles"). Delivers: the VFX bursts (ClearBurst ≤1/step, pooled pop
particles ≤24, atlas-packed single material) within the `JUICE_VFX_DRAW_CALL_BUDGET=40`
sub-budget and the 4-tier LOD degradation ladder (guaranteeing VFX alone can never breach the
global ≤100 ceiling); the **AudioDirector** (music/SFX bus, runtime pitch-shift of
`audio_pop_base`, mute gating driven by Save settings); and the haptics map plus the
accessibility feel guarantees (reduced-motion honoring, WCAG 3Hz flash-safety). The replay
*mechanism* (Reveal Queue, Shadow Board, input-lock) is E05; this epic is purely additive
sensory content hooked to that pipeline.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-G (pending): Audio middleware vs Unity native | `AudioSource`/`AudioMixer` + runtime pitch-shift vs FMOD/Wwise; mute-bus routing — **not yet written** (QQ-07) | MEDIUM |
| ADR-H (pending): Particle strategy | Shuriken vs VFX Graph (6.3 GPU-event instancing) under the 40-call sub-budget — **not yet written** (QQ-08) | MEDIUM |
| arch §9.3 | VFX sub-budget + 4-tier LOD; emissive-only bloom interplay (E08) | — |
| arch §6 | JuiceDirector / AudioDirector ownership; audio hook map, haptics map | — |

## TR-IDs Owned

| TR-ID | Requirement | ADR Coverage |
|-------|-------------|--------------|
| TR-jl-003 | Audio hook map (runtime pitch-shift), haptics map, reduced-motion + WCAG 3Hz flash-safety | arch §6 — audio tech **pending ADR-G** ⚠️ Partial |
| TR-jl-004 | VFX sub-budget (`JUICE_VFX_DRAW_CALL_BUDGET=40`), 4-tier LOD degradation, atlas single-material | arch §9.3 budget — particle tech **pending ADR-H** ⚠️ Partial |

> ⚠️ **2 requirements in this epic have no concrete-tech ADR yet.** The architectural home
> exists (arch §6/§9.3) and these are the two Presentation-tier Partials from
> `/architecture-review`. Stories for the concrete audio backing (TR-jl-003) and particle
> backing (TR-jl-004) will be **Blocked until ADR-G / ADR-H are Accepted**. This does **not**
> block E01–E06 Foundation/Core/runtime work. Run `/architecture-decision "Audio middleware
> vs Unity native"` and `"Particle strategy (Shuriken vs VFX Graph)"` before this epic's sprint.

## Depends On

- **E01** (assemblies + CI).
- **E03** (the BoardEvent stream the hooks react to).
- **E05** (the Reveal Queue / replay pipeline that schedules juice beats; JuiceDirector core).
- **E08** (materials + emissive bloom the FX composite against; BurstAdditive material).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **MEDIUM — Particles/VFX.** Legacy Particle System is deprecated in favour of VFX Graph; 6.3
  adds **VFX Graph GPU-event instancing** (relevant to cascade FX at scale). Shuriken is still
  supported for pooled 2D-style bursts — the choice is **ADR-H** (pending).
- **HIGH — URP Bloom** feeds the emissive-only FX (ClearBurst / BombOrb) — shares E08's Bloom
  Volume; do not let non-emissive FX bloom.
- **MEDIUM — Audio.** `AudioSource` runtime pitch-shift of `audio_pop_base` + `AudioMixer` mute
  bus vs middleware is **ADR-G** (pending). Haptics via `Handheld.Vibrate` / platform haptics.
- **Accessibility (blocking-adjacent):** reduced-motion honoring + WCAG 3Hz flash-safety are
  correctness requirements, not polish — verify against `design/ux/accessibility-requirements.md`
  (run `/ux-design` first).

## Definition of Done

This epic is complete when:
- ADR-G and ADR-H are Accepted, and all stories are implemented, reviewed, and closed via `/story-done`.
- VFX stays within the 40-call sub-budget across the heaviest cascade; the 4-tier LOD ladder
  demonstrably degrades under load without breaching the global ≤100 ceiling.
- AudioDirector pitch-shifts the pop escalation and honors mute settings; haptics fire per the map.
- Reduced-motion and WCAG 3Hz flash-safety are verified on a settings toggle.
- Visual/Feel stories have screenshot/video + lead sign-off in `production/qa/evidence/`;
  juice-layer.md acceptance criteria are met.

## Next Step

Run `/architecture-decision` for ADR-G and ADR-H first, then `/create-stories juice-vfx-audio-haptics`.
