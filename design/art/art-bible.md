# Art Bible: Sweet Cascade

## Document Status
- **Version**: 1.0
- **Last Updated**: 2026-07-17
- **Owned By**: art-director
- **Status**: Approved by founder (2026-07-17) — all `(PROPOSED)` items accepted as the working direction. Outstanding production tasks (not approval blockers): colorblind-simulation pass on the candy shape set; licensed display-font selection with `ui-programmer`.
- **Art Director Sign-Off (AD-ART-BIBLE)**: Not yet run — the director-gate review pass was skipped in the autonomous session; founder approval supersedes for now. Run the gate before Production commit if desired.
- **Source documents**: `design/gdd/game-concept.md` (PROCEED verdict, 2026-07-17), `.claude/docs/technical-preferences.md` (Godot 4.6 engine + performance budgets)

---

## Visual Identity Summary

Sweet Cascade is a glossy candy-shop-window world: saturated, rounded, and lit
like it's sitting behind patisserie glass. Every candy, button, and background
gradient looks good enough to eat — vibrant color-blocking, soft specular
gloss, and toy-like roundness throughout. The board is the stage and the
candies are the performers; the visual language exists to make every swap
look and feel like a small, honest celebration.

**One-line visual rule**: *If it's not glossy, saturated, and instantly
readable at arm's length, it doesn't belong in Sweet Cascade.*

### Supporting Principles

1. **Juice is drawn, not just animated** *(serves Pillar 1: Every Swap
   Sparkles)* — every static asset must already look mid-bounce: highlights,
   a hint of asymmetry, a pose that implies motion — so animation amplifies
   an already-alive object instead of trying to fake life into a dead one.
   *Design test*: if two candy designs are equally readable but one looks
   "ready to pop" and the other looks inert, ship the one that looks ready
   to pop.
2. **Color tells the truth** *(serves Pillar 2: Clever, Never Cheated)* —
   color and shape always communicate real game state; the palette never
   uses color to imply randomness, danger, or reward the game isn't
   actually delivering.
   *Design test*: if a VFX or UI color would make a player believe
   something happened that didn't (a fake "near miss" glow, a fake shuffle
   tell), cut it.
3. **The world redresses, the cast doesn't** *(serves Pillar 3: The World Is
   Alive)* — regions and seasonal events reskin backgrounds, frames, and
   decorative props; the 5 candy types and their special forms keep a
   stable identity across the entire game so a player's board-reading
   skill always transfers.
   *Design test*: if a proposed region or event reskin would require
   players to relearn candy identity, it is rejected regardless of how
   good it looks on its own.

---

## Reference Board

| Reference | Medium | What We're Taking | What We Diverge From |
| --------- | ------ | ------------------ | --------------------- |
| Candy Crush Saga | Game | Special-candy iconography clarity; board-legibility fundamentals (grid rhythm, tap/swipe feedback) | *(PROPOSED)* We render with more glass-like gloss and deeper highlight/shadow than CCS's flatter cartoon shading — see Rendering Style. |
| Royal Match | Game | Cascade "juice" pacing (squash/stretch timing), chunky satisfying UI button language | Royal Match's palette skews slightly muted/pastel; ours stays fully saturated to match the candy-shop-window brief. |
| Patisserie & candy-shop window displays | Photography / real-world reference | Wet-look specular highlights, jewel-like faceting on hard-candy shapes, warm case lighting | We simplify surface detail drastically — no photoreal reflections, 1-2 clean highlight shapes per piece only (production budget + small-screen readability). |
| Carnival / confetti poster illustration | Illustration | High-saturation gradient color-blocking for backgrounds and region theming | We avoid the poster style's busy layered typography and clutter — backgrounds stay simple 3-stop gradients so they never compete with the board. |
| Bubble wrap / pop-it toys | Physical object / tactile reference | The "round, compress-then-burst" shape language for match-pop VFX | Not chasing physical realism (no literal plastic texture) — it's a shape-and-timing reference only, translated into candy-colored particle bursts. |

---

## Color Palette

### Primary Palette

| Name | Hex | Usage |
| ---- | --- | ----- |
| Candy — Strawberry Red | `#ff5d73` | Candy type 1 (circle/gumdrop shape) |
| Candy — Citrus Orange | `#ff9f45` | Candy type 2 (hexagon/segment shape) |
| Candy — Lemon Yellow | `#ffd93d` | Candy type 3 (diamond/twist shape); also root of the Gold Reward family |
| Candy — Apple Green | `#7ddf64` | Candy type 4 (rounded-square/chew-cube shape) |
| Candy — Grape Purple | `#b47aea` | Candy type 5 (6-petal scallop/button shape) |
| CTA Red (UI) | `#e63950` | Primary buttons / confirm actions — a deepened derivative of Strawberry Red, never an exact match (see UI palette divergence rule below) |
| Hub Gradient — Royal Purple | `#7b2ff7` | Region 1 (Candy Kingdom Hub) background gradient, stop 1 |
| Hub Gradient — Magenta Pop | `#f107a3` | Region 1 background gradient, stop 2; also Region 1 UI trim/accent |
| Hub Gradient — Sunset Orange | `#ff8c42` | Region 1 background gradient, stop 3 |
| Patisserie Cream | `#fff8ef` | UI card/panel base, menu backgrounds, "paper" surfaces |
| Cocoa Brown | `#6b4226` | UI body text on cream, secondary button outline, world-map path line |
| Gold Reward (core / highlight) | `#ffd93d` / `#fff4b8` | Stars, star-ceremony overlay, reward accents |

**UI palette divergence rule**: UI chrome colors are deepened/desaturated
derivatives of the candy palette (roughly −15% lightness, −10% saturation
from the source candy hue) — close enough to feel unified with the board,
distinct enough that no UI element can ever be mistaken for a candy tile or
vice versa. `#ff5d73` (candy) vs. `#e63950` (CTA button) is the reference
example. *(PROPOSED — this divergence percentage is a starting rule, not a
locked formula; validate visually once real buttons are mocked up.)*

**Semantic UI accents** *(PROPOSED)*:

| Meaning | Hex | Note |
| ---- | --- | ---- |
| Warning / low moves | `#d97b2e` | Deliberately distinct from Candy Citrus Orange (`#ff9f45`) to avoid implying a specific candy is "dangerous" |
| Error / level failed | `#d94f5c` | Deliberately distinct from Candy Strawberry Red |
| Positive / success | `#3ecf8e` | Deliberately distinct from Candy Apple Green |
| Currency / premium | *TBD* | Not designed — monetization is explicitly deferred per `game-concept.md`; revisit when the monetization system is scoped |

### Emotional Color Mapping

| Game State | Dominant Colors | Mood |
| ---------- | ---------------- | ---- |
| Active board (default play) | Full saturated candy palette over a dimmed (≈70% brightness) region gradient | Cheerful, focused — candies pop forward, background recedes |
| Cascade / combo | Screen-color pulse cycling through the matched candies' hues; brief bloom increase | Euphoric, rewarded, "the board is applauding you" |
| Star ceremony (level complete) | Warm gold/white starburst overlay (`#fff4b8`, `#ffd93d`) over a frozen board | Triumphant, celebratory |
| Low-moves warning (≤3 moves, objective incomplete) | Subtle amber pulse (`#d97b2e`) on the move counter **only** — never the board itself | Gentle urgency, never panic or implied manipulation (Pillar 2) |
| Level failed | Board desaturated ~20%, cool blue-grey overlay (`#3a3a52`) behind a friendly retry prompt | Soft disappointment, non-punishing |
| World map / menus | Region-specific gradient background, Patisserie Cream (`#fff8ef`) UI cards | Inviting, browsable |
| Booster brewing (meta, Phase 2) | Warm kitchen/patisserie interior tones — copper (`#d4915d`), cream (`#fff8ef`) | Cozy, creative |
| Seasonal event active | One event accent color injected into UI chrome + board-frame trim; base region palette otherwise unchanged | Festive novelty without overwhelming the core palette |

### Regional & Seasonal Palette System

The palette is built as a **system**, not a fixed set of hex codes, so it can
absorb new regions and seasonal reskins (Pillar 3: The World Is Alive)
without repainting the candy cast.

**Formula**: every region background is a 3-stop linear gradient at 135°.
All three stops must sit at HSB saturation ≥65% and average lightness
≤60%, so the background always stays visibly behind the brighter,
higher-saturation candy layer (Visual Hierarchy rule).

- **Region 1 — Candy Kingdom Hub** *(MVP / Vertical Slice — the validated
  prototype gradient, now formalized as this region's identity rather than
  the game's single permanent background)*: `#7b2ff7 → #f107a3 → #ff8c42`.
  Trim/accent: `#f107a3`. Decorative props: candy-cane pillars, gumdrop
  hedges, striped awnings.
- **Region 2 — Frosted Peak** *(PROPOSED — Alpha-scope example only, to
  prove the system scales; not yet an approved region)*: `#14b8a6 →
  #4f6df5 → #a78bfa`. Trim/accent: `#14b8a6`. Decorative props: icicle
  candy shards, mint-swirl lampposts, sugar-snow drifts.
- **Regions 3 and 4**: not yet designed — defined when those regions enter
  production (Alpha scope per `game-concept.md`), following the same
  formula and the contrast rule below.

**Seasonal event overlay rule**: an event never replaces a region's base
gradient. It injects exactly ONE accent color into UI chrome (event banner,
currency icon, board-frame trim) and swaps only the decorative-prop set and
ambient-particle theme. Candy hues and shapes are never altered by an event
— this protects "the cast doesn't change" principle and keeps a player's
board-reading skill transferable across every event.

**Contrast checklist item**: whatever the region/event gradient, background
average lightness must stay low enough that all 5 candy hues maintain a
clear contrast margin against it. This must be verified visually (and
ideally with a contrast-ratio tool) per region before ship — flagged as a
production checklist item, not something guaranteed by this document alone.

---

## Art Style

### Rendering Style

2D stylized vector-illustration rendering with a **"glossy jelly" shading
model**: flat base color fill, one soft radial specular highlight
(consistent light direction across every asset — see below), a soft contact
shadow ellipse beneath each piece, and a thin (1–2px at 128px source)
darker rim-line along the shape's lower edge for weight. No painterly
brushwork, no pixel art, no full PBR/physically-based materials — gloss is
achieved with 1–2 flat highlight shapes, not real reflection maps. This
keeps assets cheap to produce, consistent across a large content pipeline
(120+ levels at Alpha scope), and instantly readable at small mobile sizes.

**Light direction consistency rule** *(PROPOSED)*: a single fixed key-light
direction (upper-left, ~315°) is used across ALL 2D assets — candies, UI
chrome, environment props — so the entire game reads as one consistent
"shop window" lighting. Never vary light direction per-asset; this is a
hard production constraint, not a per-artist style choice.

### Proportions

- All candy sprites share one square bounding box regardless of silhouette
  (128×128px source) so swap/drag/fall logic and grid math never need
  per-shape offsets.
- Each candy fills 80–85% of its bounding box, leaving a consistent gutter
  so adjacent pieces never visually touch or overlap, even at maximum
  squash during juice animation.
- Special candies (striped, color bomb, wrapped) occupy the **exact same**
  bounding box as base candies — never larger — so a board full of
  specials never breaks grid rhythm. Their identity is communicated
  through internal surface pattern only, never through size.
- Blockers occupy the same per-cell footprint, layered behind or around the
  candy (jelly = translucent layer under the tile; chocolate/licorice =
  overlay on top of an occupied or empty cell).
- There is no player-character avatar in Sweet Cascade (Anti-Pillar: NOT a
  story game). The "cast" is candies, specials, and blockers only.

### Level of Detail

- Candies must read correctly in under 0.3 seconds at a target on-screen
  size of roughly 90–130px on a 1080px-wide portrait reference canvas.
  *(PROPOSED assumption: this range covers a 7–9 column board; the exact
  grid size is a board-system GDD decision, not an art-bible decision.)*
  Every candy design must pass a **thumbnail test**: scaled to 48px, shape
  and color must still be identifiable.
- Max 1 internal surface detail per base candy (e.g., one seed fleck on
  strawberry, one segment line on the orange hexagon) — anything busier
  disappears at gameplay scale and adds render cost across hundreds of
  simultaneous on-screen instances during a big cascade.
- Environment/background art carries the **least** detail of any layer
  (soft gradient + very sparse, low-opacity decorative props) — it must
  never visually compete with the board for the player's eye.
- UI carries **moderate** detail — enough gloss/dimension to feel premium,
  but flat enough to stay legible at a glance during fast play.

### Visual Hierarchy

Ordered most to least visually prominent:

1. **Active board / candies** — brightest, most saturated, highest-contrast
   layer; everything else recedes around it.
2. **Player input feedback** — selection highlight on a tapped candy,
   drag-trail on swipe — sits visually above the candies themselves.
3. **Critical HUD** (move counter, objective tracker, star progress) —
   persistent, high-contrast against the background, positioned for
   at-a-glance reading without covering the board.
4. **Secondary HUD / chrome** (pause, settings, booster tray icon) —
   smaller, positioned in thumb-reachable corners, present but not
   competing for attention.
5. **Background gradient + ambient particles** — desaturated relative to
   the board, slow-moving, present only to establish region mood.
6. **Decorative environment props** (region flourishes, world-map
   dioramas) — lowest priority; visible only on non-board screens or as a
   thin frame around the board, never overlapping the active play area.

---

## Character Art Standards

Sweet Cascade has no traditional characters — there is no player avatar and
no story campaign (Anti-Pillar: NOT a story game). The "cast" is the candy
roster: 5 base candy types, their special forms, and the blockers that
obstruct them. Every rule below exists to make this cast instantly,
unambiguously readable during fast-paced play — this is the single most
important readability requirement in the game, since misreading a candy
costs a player a real move.

### Base Candy Roster
*(PROPOSED — the 5 hexes were validated in the concept prototype; the
shape assignments below are new and need a colorblind-simulation pass
before final art.)*

| Candy | Hex | Shape | Silhouette Notes |
| ----- | --- | ----- | ----------------- |
| Strawberry Red | `#ff5d73` | Circle (gumdrop) | Smooth, no corners — the "roundest" piece on the board; single dome highlight |
| Citrus Orange | `#ff9f45` | Hexagon (segment) | 6 softly rounded corners; faint radial segment lines |
| Lemon Yellow | `#ffd93d` | Diamond / rotated square (twist) | 4 points, slightly pinched waist like a wrapped hard candy |
| Apple Green | `#7ddf64` | Rounded square (chew cube) | Soft 90° corners; slight jelly wobble in idle animation |
| Grape Purple | `#b47aea` | 6-petal scallop (button) | Only piece with a scalloped/wavy outline — most visually distinct silhouette on the board |

Each type is double-coded by shape AND color so the roster remains
colorblind-accessible without relying on hue recognition alone.

### Special Candy Visual Language

- **Striped Candy** (match-4): base candy shape retained; 3 bold cream
  stripes (`#fff8ef`) added across the axis it clears — horizontal stripes
  clear the row, vertical stripes clear the column. Stripe orientation
  must be readable at 48px thumbnail size (cream against every candy hue
  passes this contrast test).
- **Color Bomb** (match-5): replaces the base shape entirely with a unique
  silhouette never used for a real candy type — a faceted swirled orb with
  a rainbow-gradient surface and a thin white glow ring. It is
  intentionally colorless/all-color so it is never mistaken for a specific
  candy type. This is the only piece on the board permitted a gradient
  fill instead of a flat color.
- **Wrapped Candy** *(PROPOSED — Vertical Slice/Alpha scope, not MVP)*:
  base candy shape with a crinkled foil/cellophane overlay pattern
  (twist-tie ends visible at 2 opposite edges); communicates "explodes in
  a radius" via the wrap looking like it's about to pop, not via a color
  change.
- **Rule**: special-candy identity is always carried by surface pattern
  and/or silhouette change, **never** by resizing or recoloring outside
  each piece's own hue family. A player must recognize "this is a striped
  strawberry" instantly, not "this is a bigger red thing."

### Blocker Visual Language
*(PROPOSED — blockers are named in `game-concept.md` as a level-objective
type but not yet mechanically designed. This establishes a starting visual
system for whichever system-design pass defines blocker mechanics.)*

- **Jelly**: translucent colored gelatin layer (60% opacity of the
  region's accent hue) sitting under/around a cell; thins and clears in
  visible "coats" as it's matched over, so progress is always visible
  without needing a counter.
- **Chocolate Block**: opaque, dark cocoa-brown (`#4a2c1a`) blob with a
  glossy top highlight (same lighting rule as candies); cracks appear and
  widen visually as adjacent matches damage it, so the player can see
  exactly how close it is to breaking.
- **Licorice Cage**: thin dark ropes (`#2a1f1a`) overlaid across a candy in
  a simple X or box pattern; ropes fray and snap one at a time — never
  disappear in a single invisible step (ties directly to Pillar 2: every
  state change must be visibly justified).

### Animation Style

- **Idle**: extremely subtle scale "breathing" loop (98%–102% scale, ~2s
  period) — present but must never read as an animation error or as
  urgent.
- **Swap**: 120–150ms ease-in-out slide with a slight squash (85%
  perpendicular scale) at the midpoint of the slide.
- **Match/Pop**: 40ms squash to 80% scale, then a snappy burst to 130%
  scale with particles released, fading out by 200ms total — this is the
  single most repeated animation in the game and must stay fast to protect
  pace (Pillar 1).
- **Fall/Refill**: ease-out drop matching the fall distance, with a
  10–15% squash-then-settle bounce on landing (bounce duration scales
  slightly with fall distance, capped at 400ms so long cascades don't
  drag).
- **Special activation** (striped/color bomb/wrapped): gets a dedicated,
  slightly longer "hero" animation (250–400ms) since these moments should
  read as bigger rewards than a normal match.

---

## Environment Art Standards

Sweet Cascade has no traditional 3D environments — everything is a 2D
layered scene: the board frame/tray, per-region background gradients,
ambient particles, and world-map dioramas.

### Board Frame & Tray

- The board sits inside a rounded-rect "serving tray" frame (large corner
  radius, ~24–32px at reference scale) with a soft drop shadow lifting it
  off the background — reinforces the patisserie display-case read.
- Each grid cell has a subtle inset "well" — a ~15–20% black translucent
  backing so the bright, saturated candies visually pop forward against it
  (Visual Hierarchy rule #1).
- A single diagonal glass-glare streak crosses the tray's top edge at low
  opacity (~10%) as a static "under glass" cue — subtle, decorative, never
  animated (animation here would distract from gameplay).

### Background & Region Theming

- Every region background is a 3-stop, 135° linear gradient (see Regional
  & Seasonal Palette System) rendered as a shader gradient, not a baked
  bitmap, to protect the memory budget (≤400MB) — exact implementation is
  a `technical-artist` coordination item.
- 1–2 layers of slow-drifting, low-opacity (≤15%) ambient particles (soft
  bokeh dots, drifting confetti, or region-themed motes) add "alive"
  atmosphere without competing with the board. Max 2 background layers
  total (gradient + 1 particle layer) to protect the draw-call budget.
- Decorative props (candy-cane pillars, gumdrop hedges, icicle shards,
  etc.) appear only at the board's outer frame edges or on non-gameplay
  screens (map, menus) — **never** inside the active play area.

### Regional Modularity (production system)

Each region is defined by exactly 4 swappable elements, so new regions and
seasonal reskins can be produced without touching candy or core UI art:

1. Background gradient triplet (3 hex stops)
2. UI trim/accent color (buttons, borders, map path)
3. 2–4 decorative prop silhouettes (frame-edge only)
4. Ambient particle theme (shape + color of drifting motes)

Seasonal events swap only elements 3 and 4, plus inject one accent color
into UI chrome. Elements 1 and 2 (the region's core gradient/trim) stay
stable, so a region always feels like "home" even during an event.

### World Map

- Node-based path (one path per region, gated sequentially), rendered as a
  ribbon in the region's trim color connecting level nodes.
- Level nodes are candy-shaped markers (using the 5 base candy silhouettes
  as node "buttons") with a star-progress ring around completed ones.
- Each region is represented on the map by a small diorama vignette using
  that region's decorative props, previewing the region before the player
  enters it.

---

## UI Art Standards

### Button Style

- **Primary CTA**: pill/rounded-rect, glossy top-highlight gradient fill
  from `#ff5d73` to `#e63950` (top-to-bottom), soft drop shadow, ~8–10%
  scale-down "press" animation on tap.
- **Secondary/neutral**: cream fill `#fff8ef`, 2px Cocoa Brown (`#6b4226`)
  outline/text, same rounded-pill shape, no gradient (visually recedes
  vs. primary).
- **Disabled**: 40% opacity, desaturated, no drop shadow (reads as "flat"
  / inactive at a glance).
- All buttons ≥44×44px touch target (per `.claude/docs/technical-preferences.md`),
  with ≥8px spacing between adjacent interactive elements.

### Typography
*(PROPOSED — style direction only; final licensed typeface selection is a
`ui-programmer`/producer decision.)*

- **Display / headers / score numbers**: rounded, heavy-weight geometric
  sans with a slight bounce/friendliness to the letterforms (style
  reference: Fredoka/Baloo-family feel) — used for level titles, score
  popups, star-ceremony text.
- **Body / UI labels**: a cleaner rounded sans at regular/medium weight for
  settings, objective text, and buttons — must stay legible at small sizes
  for the 25–45 casual audience.
- **Numbers** (move counter, score, timers) always use the heavy display
  weight, minimum 28px at the 1080×1920 reference canvas — these are the
  most time-critical reads in the HUD and must never use the thin weight.
- No thin/light weights anywhere in the UI — this audience skews toward
  needing higher legibility, and thin weights fail on small, sometimes
  outdoor-lit mobile screens.

### Iconography

- Flat-filled (not outline-only) rounded icon style matching the candy
  gloss language — one soft highlight per icon, consistent light direction
  with the rest of the game.
- Icon + label or icon + color pairing is mandatory everywhere — never
  icon-only or color-only for any state that matters (ties directly to
  Accessibility).

### HUD Density & Layout (portrait, one-handed)

- **Top third of screen**: read-only status (move counter, objective
  tracker, level title) — a thumb doesn't need to reach here, so it's the
  right place for information the player just glances at.
- **Middle**: the board — the overwhelming majority of screen real estate.
- **Bottom edge / corners**: interactive chrome (pause/settings, booster
  tray when it ships) — placed within one-handed thumb reach (bottom-left
  or bottom-right corner, never bottom-center where it could be confused
  with a system home-gesture area).
- No hover-only states anywhere (touch-only platform, per
  `.claude/docs/technical-preferences.md`) — every interactive element has
  a clear pressed/tapped state; no interaction is hover-dependent.

### Menu Layout Principles

- Card-based layout on the region gradient background: generously rounded
  Patisserie Cream cards (`#fff8ef`) hosting content, one primary action
  emphasized by size + saturated color, secondary actions smaller and
  desaturated.
- One primary action per screen — if a screen has two equally prominent
  CTAs, that's a hierarchy failure and should be revised.

### Pillar 2 Compliance Note

UI never fakes game state. No shimmer/glow "tell" implies a shuffle or hint
that isn't actually available; the move counter and objective trackers
always reflect literal, current game state with zero decorative lag or
animation that could read as manipulation.

---

## VFX Standards

### Particle Style

Chunky, candy-themed particle shapes only: small stars, sugar-crystal
specks, sparkle diamonds, and confetti squares — never generic smoke,
dust, or photorealistic sparks. A shared "sparkle star" and "sugar dust"
particle exist for universal reuse across all candy types (recolored
per-instance to match the source candy).

### Match / Pop VFX

- **Standard match**: the candy squashes then bursts into 6–10 particles
  tinted to its own hue, plus a brief white flash core at the burst
  origin — total VFX duration 150–250ms.
- **Cascade chains**: each additional link in a single cascade increases
  particle count and adds a brief screen-color pulse that cycles through
  the colors of the candies matched in that chain — visually communicates
  "this chain is getting bigger" without relying on a numeric readout
  alone.
- **Special activation**: striped candies get a fast directional
  beam/streak along their clear axis; color bombs get a radial shockwave
  ring plus a wider sparkle burst. Specials always read as visually
  "bigger events" than standard matches (roughly 1.5–2× the particle count
  and duration).

### Screen-Space Effects

- A subtle "sugar rush" bloom pulse and light camera-shake are reserved
  for big cascades only (4+ chain links, or a special-candy combo) —
  never on every single match, to avoid VFX fatigue across a long session
  (protects Pillar 1 without exhausting the player).
- All screen-space punch effects (shake, bloom pulse) must respect a
  reduced-motion accessibility toggle *(PROPOSED — see Accessibility)*
  that dampens or disables them while preserving the core particle-burst
  feedback.

### Color Coding Rule

VFX particle color always matches the source candy or special that
triggered it — this is a hard rule, not a style preference. Players must
always be able to trace "why did that happen" visually (Pillar 2: Clever,
Never Cheated).

### Performance Guardrail

All particle effects must be built from a shared, atlas-packed
particle-sprite sheet (single texture, single material where possible) so
a heavy cascade with many simultaneous pops stays within the ≤100 draw
call budget defined in `.claude/docs/technical-preferences.md`. Exact
GPUParticles2D/CPUParticles2D implementation, pooling, and atlas layout is
a `technical-artist` decision — this document specifies the visual target
only.

---

## Asset Production Standards

### Naming Convention

Base studio convention: `[category]_[name]_[variant]_[size].[ext]`

*(PROPOSED extension for this project)* — candies get their own category
prefix rather than being shoehorned into `char_`, since they are not
characters in the traditional sense:

- `candy_[type]_[state]_[variant].[ext]` — e.g. `candy_strawberry_idle_01.png`,
  `candy_strawberry_striped_h_01.png` (`h` = horizontal stripe),
  `candy_bomb_color_idle_01.png`
- `env_bg_[region]_gradient_full.png` (or `.tres` if implemented as a
  shader-gradient resource)
- `env_prop_[region]_[object]_[size].png` — e.g.
  `env_prop_hub_candycane-pillar_large.png`
- `ui_btn_[role]_[state].png` — e.g. `ui_btn_primary_default.png`,
  `ui_btn_primary_pressed.png`
- `vfx_[effect]_[variant]_[size].png` — e.g. `vfx_pop_strawberry_small.png`,
  `vfx_sparkle_star_loop_small.png`
- `blocker_[type]_[state]_[variant].png` — e.g.
  `blocker_chocolate_cracked_01.png`

### Texture Standards

| Category | Max Resolution | Format | Color Space |
| -------- | --------------- | ------ | ------------ |
| Candy & Special Pieces | 128×128 px source (atlas-packed) | PNG, RGBA8 straight alpha | sRGB |
| Blockers | 128×128 px source (atlas-packed, shares candy atlas where possible) | PNG, RGBA8 | sRGB |
| UI — buttons/cards | 256×256 px max source | PNG, RGBA8 | sRGB |
| UI — icons | 64×64 px source | PNG, RGBA8 | sRGB |
| Environment backgrounds | Prefer shader gradient (no baked texture); 1080×1920 max if a baked fallback is needed | PNG or shader resource | sRGB |
| Environment props (decorative) | 256×256 px max source | PNG, RGBA8 | sRGB |
| VFX particle sprites | 64×64 px source (32×32 for small sparkle/dust), atlas-packed | PNG, RGBA8 | sRGB |
| World map diorama art | 512×512 px max source | PNG, RGBA8 | sRGB |

All per-category atlas layouts, compression settings (e.g., ETC2/ASTC for
mobile export), and mip/import presets are `technical-artist`
responsibility — coordinate before the final export pipeline is locked.
This table specifies source-art targets only, sized to comfortably hit the
≤400MB memory ceiling and ≤100 draw-call budget when properly
atlas-batched.

### Animation Standards
*(PROPOSED direction)*

- Prefer procedural/tween-driven animation (scale, position, simple skew
  via Godot's Tween/AnimationPlayer) over hand-drawn frame-by-frame sprite
  sheets for all routine candy juice (idle, swap, pop, fall) — cheaper to
  produce and iterate across 120+ levels' worth of shared assets, and
  stays perfectly smooth at any frame rate.
- Reserve true frame-by-frame animation for a small number of "hero"
  moments only: color-bomb activation, star-ceremony celebration, and any
  unique event-exclusive effect — these justify the extra production cost.
- Timing targets (restated from Character Art Standards for production
  reference): swap 120–150ms, match pop ≤200ms total, fall/bounce ≤400ms
  capped regardless of fall distance, special activation 250–400ms.
- Squash/stretch ceiling: 20% max deformation on any candy piece (stay
  shape-readable), 30% max on VFX-only elements.

---

## Accessibility

- **Colorblind-safe**: every one of the 5 candy types is double-coded by
  shape AND color (see Base Candy Roster) — never rely on hue alone
  anywhere in the game. Before final art lock, run the full candy set
  through a protanopia/deuteranopia/tritanopia simulation pass as a
  production checklist item.
- Special candies are distinguishable by pattern/silhouette (stripe
  direction, unique bomb shape, wrap texture), never by hue shift alone.
- **Minimum text size** *(PROPOSED — validate with `ux-designer`)*: 28px
  at the 1080×1920 reference canvas for HUD numbers (moves, score,
  timers); 24px minimum for secondary labels and body copy. No text below
  24px anywhere in the game.
- **High contrast mode** *(PROPOSED toggle — confirm feasibility with
  `ui-programmer`)*: adds a 2px dark outline to every candy silhouette and
  dims the background gradient by an additional 15% to widen the
  candy-to-background contrast ratio.
- **Icon + color, never color alone**: every state-communicating icon
  (objective type, booster type, blocker type) ships with a label or is
  unambiguous by silhouette alone — color is always a reinforcement, never
  the sole signal.
- **Reduced motion** *(PROPOSED toggle)*: dampens/disables screen-shake
  and chromatic bloom pulses on big cascades while preserving the core
  particle-burst feedback, so players with motion sensitivity aren't
  excluded from the core juice.
- **Touch targets**: every interactive element (buttons, candy tiles'
  effective tap/swipe hit area) is ≥44×44px, restated from
  `.claude/docs/technical-preferences.md` — this is a hard floor, not a
  target to round down from.
