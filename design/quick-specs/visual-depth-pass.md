# Quick Design Spec — Visual Depth & Life Pass (Playable Slice)

*Created: 2026-07-18 · Source: founder direction ("dynamic lighting effects and
shadows... vibrant palette... fluid character animations and interactive
elements... environmental designs, textures and layers")*
*Status: Approved-by-direction — implementing in `prototypes/playable-slice/`;
carries forward into Godot production via juice-layer.md / art-bible.md.*

## Translation of the direction (2D match-3 idiom)

| Founder ask | 2D implementation | Governing rule |
|---|---|---|
| Dynamic lighting & shadows / depth | Per-fruit contact shadows; board inner bevel + cream frame; alternating cell wells; pulsing gold glow on Color Bomb; shimmer on striped fruits; radial light-burst at each clear step | art-bible Rendering Style (1-2 highlight shapes; gloss = light), juice-layer particle budgets |
| Vibrant palette / immersion | Layered environment behind the app: drifting soft color hills + rising bokeh sparkles + gentle vignette — all sub-board saturation so the board stays the star | art-bible Supporting Principle 3 (backgrounds never compete) |
| Fluid character animations | Fizz idle bob-and-tilt everywhere he appears; tap-Fizz interaction on map (bounce + new line + chime); star ceremony pop-in with stagger; landing squash on refilled fruits | characters-and-tone placement rules; juice-layer hero-moment budget |
| Interactive elements | Tile press-down feedback (≤1 frame), desktop hover brightness (additive only), springy buttons, pressable map nodes, dotted map path | touch-input (no hover-only), screen-flow transition budgets |
| Environmental textures/layers | Two parallax-drifting hill layers + bokeh field + vignette + board frame — Region 1 instance of the reskinnable region system | art-bible region gradient system (Pillar 3 seam) |

## Constraints honored

- CSS transform/opacity animations only on per-tile effects; `filter` limited to
  rare pieces (bombs on board ≤2 typical, striped few) — 60fps budget.
- `prefers-reduced-motion`: hills, bokeh, glows, shimmer, bob, squash all disabled.
- No full-screen flashes; burst overlay ≤1 per cascade step, ≤380ms (FLASH_SAFETY_MAX_HZ=3).
- Hover effects additive-only (web); all interactions work touch-first.
- Fizz never appears on the live board (narrative boundary).

## Acceptance (ADVISORY per testing standards — visual/feel)

- 23/23 regression suite still passes; zero console errors.
- Screenshot review: depth readable (shadows/wells/frame), board legibility
  unharmed, reduced-motion build loses all ambient motion.
- Cascade step remains ≤ juice-layer's step budget (no added JS in resolve loop
  beyond one burst div per step).
