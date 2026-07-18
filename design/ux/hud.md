# HUD Design: Sweet Cascade

> **Status**: APPROVED (ux-review, 2026-07-18) — see `design/ux/ux-review-2026-07-18.md`
> **Author**: ux-designer
> **Last Updated**: 2026-07-18
> **Game**: Sweet Cascade (mobile match-3, portrait, one-handed)
> **Platform Targets**: Mobile (iOS/Android, primary), Web (secondary — HTML playable-slice)
> **Related GDDs**: `design/gdd/level-objectives.md` (APPROVED — chip data model), `design/gdd/scoring-stars.md`
> (APPROVED — score chip data), `design/gdd/juice-layer.md` (APPROVED — callout layer, low-moves pulse
> feedback), `design/gdd/board-engine.md` (APPROVED — Formula 4, board margins migrated below),
> `design/gdd/screen-flow.md` (APPROVED — which base state hosts the HUD)
> **Accessibility Tier**: See `design/ux/accessibility-requirements.md` (working assessment: Basic-to-Standard)
> **Style Reference**: `design/art/art-bible.md` § HUD Density & Layout, § Typography, § Visual Hierarchy

> **Scope boundary** (per the studio's HUD-design template): this document specifies the elements that
> overlay the board during active `GAMEPLAY` only — the three-chip header, the cascade callout layer,
> and the footer chrome. Pre-Level Card, Pause, Settings, World Map, and Results screens are overlay/
> menu content and belong to individual UX specs (not yet authored — see Open Questions), not this
> document. The test: if it renders while `base_state == GAMEPLAY`, it belongs here
> (`design/gdd/screen-flow.md` §7 confines the board itself to this same state).

---

## 1. HUD Philosophy

**What is this game's relationship with on-screen information?**

Sweet Cascade's HUD is a **minimal, always-legible instrument panel, not a dashboard.** The board is
the game; the HUD's entire job is to answer exactly three questions the player might ask mid-swipe
without looking away from the board for more than an instant — "how many moves do I have left,"
"what am I trying to do," and "how am I doing" — and nothing else. This directly implements
`level-objectives.md`'s own Player Fantasy commitment ("the player always knows exactly what they need
and exactly how close they are — never a mystery") and `art-bible.md`'s Visual Hierarchy, which ranks
"Critical HUD" fourth out of six layers, explicitly below the board and player-input feedback and
"positioned for at-a-glance reading without covering the board."

**Visibility principle**: **Default to SHOW**, but only for the three data points already committed
across the source GDDs (moves, objectives, score). This is not a contextual-HUD game — there is no
"combat" state that reveals more chrome and an "exploration" state that hides it; `GAMEPLAY` has
exactly one HUD configuration for its entire duration (see § 5, HUD States by Gameplay Context).

**The Rule of Necessity for this game**: *A HUD element earns its place when it is one of the three
data points Level Objective & Move-Limit System or Scoring & Star Thresholds already emit as a live
signal a player needs to plan their next swap around* (`objective_progressed`, `moves_remaining_changed`,
the running score total). This is a narrow, deliberately restrictive rule — it is also the rule that
keeps this document from re-litigating a "should the HUD show X" question for anything not already a
committed data model in an approved GDD. Anything not on that list (level title, region name, currency,
booster tray) is explicitly out of scope until a GDD defines it as a live signal.

---

## 2. Information Architecture

### 2.1 Full Information Inventory (from approved GDD signal catalogs)

| Information | Source Signal | GDD |
|---|---|---|
| Moves remaining, moves used, move limit, low-moves warning state | `moves_remaining_changed(moves_remaining, moves_used, move_limit, is_low_moves)` | `level-objectives.md` § Detailed Rules 6–7 |
| Per-objective progress (current, target, progress_fraction, completed) | `objective_progressed(objective_index, type, current, target, progress_fraction, completed)`, initial state from `ObjectiveDisplayModel` | `level-objectives.md` § Detailed Rules 5, 10 |
| Running score total | Live running `final_score` (`ScoreProvider.get_current_score()`), incremented per `match_cleared`'s `step_score` | `scoring-stars.md` § Detailed Rules 1, 4, 10a |
| Cascade milestone escalation ("Sweet!" → "Delicious!" → "Spectacular!") | `MILESTONE_REVEAL` Reveal Step, `chain_index ≥ 2` | `juice-layer.md` §§ 3, 5 |
| Pause access | Player-initiated only — no live signal | `screen-flow.md` T8 |

### 2.2 Categorization

| Item | Always Show | Contextual | On Demand | Reasoning |
|---|---|---|---|---|
| Moves remaining | **X** | | | Every swap decision is partly gated by "can I afford this move" — always decision-relevant |
| Objective progress (1–2 chips) | **X** | | | Per `level-objectives.md`'s Player Fantasy: "always visible, always numeric" — never gated behind a menu dig |
| Running score | **X** | | | Committed as continuously visible per `scoring-stars.md`'s "score as applause" framing — the player should never have to ask what their score is |
| Cascade milestone callout | | **X** — only during `MILESTONE_REVEAL` (`chain_index ≥ 2`, non-bootstrap) | | A transient celebration, not a persistent readout — never shown for an ordinary match-3 (`juice-layer.md` § 5) |
| Low-moves warning pulse | | **X** — only while `is_low_moves = true` | | Overlaid on the always-visible moves chip; not a separate element |
| Pause icon | **X** (persistent, footer) | | | Always reachable per `screen-flow.md`'s "no menu ever more than one tap deep" Player Fantasy guarantee |

**No "Hidden" category items exist in this HUD** — every piece of information this document's Rule of
Necessity admits is always either Always-Show or a directly-attached contextual overlay on an
Always-Show element. This is a deliberate consequence of Sweet Cascade's narrow, already-approved data
surface, not an oversight of a broader inventory.

---

## 3. Layout Zones

### 3.1 Board Margins — Migrated from `board-engine.md` Formula 4

**This section formally migrates `BOARD_SIDE_MARGIN_PX`, `BOARD_TOP_ALLOCATION_PX`, and
`BOARD_BOTTOM_ALLOCATION_PX` from `design/gdd/board-engine.md`'s Tuning Knobs table to this document**,
resolving that document's own named Open Question: *"Should Board Engine's `BOARD_SIDE_MARGIN_PX`/
`BOARD_TOP_ALLOCATION_PX`/`BOARD_BOTTOM_ALLOCATION_PX` constants move to a future `design/ux/hud.md`
once that UX spec exists, to avoid two sources of truth for screen layout?"* — Yes. This document is
now their canonical source. **Values are unchanged** — this is a migration of ownership, not a
redesign; `board-engine.md`'s Formula 4 proof (the 44px touch-target floor across the full 3–9 grid
range) remains valid verbatim, since the constants it depends on are unchanged.

| Constant | Value | Safe Range | Description |
|---|---|---|---|
| `CANVAS_WIDTH_PX` | 1080 (fixed reference) | — | Reference canvas width (`art-bible.md` portrait reference resolution) |
| `CANVAS_HEIGHT_PX` | 1920 (fixed reference) | — | Reference canvas height |
| `BOARD_SIDE_MARGIN_PX` | 40 | 16–80 | Horizontal margin reserved on each side of the board |
| `BOARD_TOP_ALLOCATION_PX` | 640 (`= CANVAS_HEIGHT_PX / 3`) | 480–800 | Vertical space reserved above the board — hosts this document's three-chip header (§ 4.1) |
| `BOARD_BOTTOM_ALLOCATION_PX` | 200 | 120–320 | Vertical space reserved below the board — hosts this document's footer chrome (§ 4.3) and absorbs the OS gesture-bar safe area |

```
available_width_px  = CANVAS_WIDTH_PX  − 2 × BOARD_SIDE_MARGIN_PX   = 1080 − 80  = 1000
available_height_px = CANVAS_HEIGHT_PX − BOARD_TOP_ALLOCATION_PX − BOARD_BOTTOM_ALLOCATION_PX
                     = 1920 − 640 − 200 = 1080
```

**Reciprocal edit for `board-engine.md`'s next review pass** (not made here — this document may not
edit files outside `design/ux/`): that document's Tuning Knobs table rows for these three constants
should be replaced with a cross-reference to `design/ux/hud.md` § 3.1, and its Open Questions table
row asking this exact question should be marked **Resolved**, pointing here.

### 3.2 Header Zone — Three-Chip Layout (`BOARD_TOP_ALLOCATION_PX`)

The header occupies the full `BOARD_TOP_ALLOCATION_PX` = 640px reference-canvas band at the top of the
screen, edge-aligned with the board's own side margins for visual consistency (same
`BOARD_SIDE_MARGIN_PX` = 40px each side, giving the header the same 1000px available content width as
the board below it).

```
0                                                          1080   (CANVAS_WIDTH_PX)
┌──────────────────────────────────────────────────────────┐  0
│ [40px margin] ┌────────┐ ┌──────────────┐ ┌────────┐ [40px margin]
│               │ MOVES  │ │  OBJECTIVES  │ │ SCORE  │              │  ~640px
│               │  chip  │ │  chip(s), 1–2│ │  chip  │              │  (BOARD_TOP_
│               └────────┘ └──────────────┘ └────────┘              │  ALLOCATION_PX)
├──────────────────────────────────────────────────────────┤
│                                                            │
│                        BOARD                              │  ~1080px
│                 (middle third+, available_height_px)       │
│                                                            │
├──────────────────────────────────────────────────────────┤
│                                            ┌──────┐         │  ~200px
│                                            │Pause │         │  (BOARD_BOTTOM_
│                                            └──────┘         │  ALLOCATION_PX)
└──────────────────────────────────────────────────────────┘  1920  (CANVAS_HEIGHT_PX)
```

**New constant introduced by this document** (not previously named in any source):

| Constant | Value (default) | Safe Range | Description |
|---|---|---|---|
| `HUD_CHIP_GAP_PX` | 16 | 8–32 | Horizontal gap between adjacent header chips |
| `HUD_SAFE_AREA_TOP_INSET_PX` | 32 | 16–64 | Additional inset from the very top edge, inside `BOARD_TOP_ALLOCATION_PX`, reserved for device notch/camera-cutout clearance (see § 7) |
| `HUD_SAFE_AREA_BOTTOM_INSET_PX` | 24 | 16–48 | Additional inset from the very bottom edge, inside `BOARD_BOTTOM_ALLOCATION_PX`, reserved for the OS gesture-bar safe area (see § 7) |

**Chip width at reference scale**: `(1000 − 2 × HUD_CHIP_GAP_PX) / 3 = (1000 − 32) / 3 ≈ 322.7px` per
chip, generous at the 1080-wide reference canvas. See § 7 for the 320px-viewport worst case.

### 3.3 Z-Order Stack (Layout Zone Depth, Bottom to Top)

Synthesizing `art-bible.md`'s Visual Hierarchy list with the overlay-stack contract in
`design/ux/interaction-patterns.md`, the canonical rendering order for everything visible during
`GAMEPLAY` (and the overlay states that can suspend it) is:

| Order (bottom → top) | Layer | Content | Notes |
|---|---|---|---|
| 0 | Background | Region gradient + ambient particles | Desaturated, slow-moving, never competes for attention |
| 1 | Board | Candies, blockers, board frame | The primary content layer |
| 2 | Player input feedback | Selection highlight, drag-in-progress tween | Sits visually above candies |
| 3 | **Callout layer** | Cascade milestone text overlay + screen-color pulse (§ 4.2) | Centered over the board's own rect, never over the header/footer chips — non-interactive, pass-through for input |
| 4 | **Header zone** | Moves / Objectives / Score chips (§ 4.1) | Always on top of callout/board content so chip text is never obscured by a screen-pulse |
| 5 | **Footer zone** | Pause icon (§ 4.3) | Same layer priority as header — persistent chrome always wins over transient board VFX |
| 6 | Overlay layer | Pre-Level Card / Pause / Settings modal cards + host-screen dim | Topmost — per `design/ux/interaction-patterns.md`'s Overlay Open/Dismiss pattern, nothing renders above an open overlay |

**Contrast requirement carried from this ordering**: because the header (layer 4) renders above the
callout layer (layer 3), a screen-color pulse (`juice-layer.md` § 4, cascade milestone escalation)
must never reduce chip text legibility below the contrast minimums in
`design/ux/accessibility-requirements.md` — flagged as an Open Question requiring a visual
verification pass once the pulse's actual opacity/blend mode is authored.

---

## 4. HUD Elements

### 4.1 Header Chips — Data Bindings

#### Moves Chip

- **Zone**: Header, left slot.
- **Data source**: `moves_remaining_changed(moves_remaining, moves_used, move_limit, is_low_moves)`
  (`level-objectives.md` § Detailed Rules 6), re-emitted once per `swap_accepted`.
- **Content displayed**: `moves_remaining` as a number, always. Compact format (icon + number, no word
  label) — see § 7 for why this is the default, not merely a narrow-viewport fallback.
- **Update behavior**: Event-driven, on every `moves_remaining_changed` emission — never polled.
- **Urgency state**: Amber (`#d97b2e`) pulse when `is_low_moves = true` (Formula 6:
  `moves_remaining ≤ LOW_MOVES_THRESHOLD` (default 3) **AND** not all objectives complete) — see
  `design/ux/interaction-patterns.md` § Low-Moves Warning Pulse for the full interaction spec, and
  `design/ux/accessibility-requirements.md` § Reduced Motion for its pulse-suppression behavior.
- **Initial state**: `moves_remaining = move_limit`, `is_low_moves = false`, set at every fresh
  `board_bootstrapped` (`level-objectives.md` § Detailed Rules 2's reset discipline — every tracker,
  including the move counter, starts fresh on every attempt, no carry-over state).

#### Objectives Chip(s) — 1 to 2 slots

- **Zone**: Header, center slot(s).
- **Data source**: `objective_progressed(objective_index, type, current, target, progress_fraction,
  completed)` per tracker (`level-objectives.md` § Detailed Rules 5), initial render from
  `ObjectiveDisplayModel` (§ Detailed Rules 10) at `board_bootstrapped`.
- **Content displayed**: per-objective icon (candy hue for `collect_color`, a distinct score icon for
  `score_target`) + `current`/`target` as a compact fraction, e.g. "🍓 18/20". `progress_fraction` is
  clamped `[0.0, 1.0]` for any progress-bar-style rendering (Formula 1) — the raw, potentially
  over-target `current` is preserved in the underlying data even when the displayed fraction caps at
  100%.
- **Objective count**: `level-data-format.md`'s schema floor is 1 objective; `level-objectives.md`'s
  `RECOMMENDED_MAX_OBJECTIVES_PER_LEVEL = 2` is a soft authoring guideline, not schema-enforced. This
  chip slot must render correctly for both 1 and 2 objectives — **[UX-authored — not GDD-sourced]**:
  when 2 objectives are authored, stack them vertically within the same center-slot width rather than
  splitting the header into 4 columns, to protect the header's overall 3-zone proportions at narrow
  viewports (see § 7). A level authored beyond the 2-objective soft guideline (schema-legal but
  discouraged) is **not specced by this layout** — flagged in Open Questions.
- **Update behavior**: Event-driven per `match_cleared` step that affects a given tracker — a
  multi-step cascade produces a **sequence** of updates (one per `chain_index`), so a consumer replaying
  at Juice Layer's own pace can animate the chip's progress climbing in lockstep with each visible
  clear rather than jump-cutting to the final total (`level-objectives.md` § Detailed Rules 5).
- **Completion state**: When `completed = true` for a chip, it should read as visually "done"
  (**[UX-authored — not GDD-sourced: exact treatment — checkmark, fill, desaturation — is
  `art-director` territory]**) but must never disappear from the header while other objectives remain
  incomplete, since the header's chip count should stay stable for the level's duration (no layout
  reflow mid-level).

#### Score Chip

- **Zone**: Header, right slot.
- **Data source**: The running `final_score` total (`ScoreProvider.get_current_score()`,
  `scoring-stars.md` § Detailed Rules 1, 10a) — the same live value Level Objective's `score_target`
  tracking already consumes, so the HUD display and any `score_target` objective's internal tracking
  are always reading the identical number, never two independently-drifting copies.
- **Content displayed**: The current score as a number, heavy display-weight typeface, minimum 28px at
  reference canvas (`art-bible.md` Typography — "the most time-critical reads in the HUD").
- **Update behavior**: Score accrues **logically** the instant a `match_cleared` event fires
  (synchronous, within Board Engine's resolution frame — `scoring-stars.md` § Detailed Rules 4), but
  the **visible counting-up animation** on this chip is Juice Layer's presentation concern, not this
  document's data-binding concern: the chip should tick upward toward the new total in step with each
  `CLEAR_REVEAL` Reveal Step's pacing (`juice-layer.md` §§ 3–4), not jump-cut instantly, so the visible
  number and the visible cascade always read as the same event. Per-event popup values ("+180" floating
  text) are a separate, already-named-but-not-designed Juice Layer seam (`juice-layer.md` § 11,
  `score_popup_slot`) — **out of this document's scope**, flagged in Gaps below.
- **Initial state**: `0` at every fresh `board_bootstrapped`, **except** the rare, legitimate bootstrap
  accidental-cascade case (`level-objectives.md` Edge Cases) — per `scoring-stars.md` § Detailed Rules
  6, `trigger_source = BOOTSTRAP` events never contribute score, so the chip correctly stays at `0`
  even through that edge case.

### 4.2 Callout Layer — Cascade Milestone Overlay

- **Zone**: Callout layer (z-order 3, § 3.3), centered over the board's rect only — never over header
  or footer chips.
- **Trigger**: `MILESTONE_REVEAL` Reveal Step — `chain_index ≥ 2` **and** `trigger_source ∈
  {SWAP_MATCH, SPECIAL_ACTIVATION}` (`juice-layer.md` § 5). Never fires for a bootstrap-sourced cascade.
- **Content**: Tier text ("Sweet!" / "Delicious!" / "Spectacular!", placeholder copy pending
  `narrative-director`) + a "×N" readout of `chain_index` — always matching the actual score
  multiplier applied that step (`scoring-stars.md` § 3's linear, uncapped chain multiplier is
  specifically designed so this number and the payout never diverge).
- **Screen-color pulse**: Begins at `chain_index ≥ BIG_CASCADE_CHAIN_THRESHOLD` (4, art-bible-locked).
  Governed by the flash-safety hard ceiling (≤3Hz, validated at ≈2.6Hz for the default cadence) and
  fully disabled under `reduced_motion_enabled` — full detail in
  `design/ux/accessibility-requirements.md`.
- **Non-interactive**: This layer never intercepts input — a player can swipe/tap through it (though in
  practice `effective_board_input_enabled` is already `false` for the layer's entire duration, since it
  only ever appears mid-Reveal-Queue while input is locked).
- **Full interaction/visual detail**: `design/ux/interaction-patterns.md` § Cascade Milestone Callout.

### 4.3 Footer Zone — Pause Icon

- **Zone**: Footer (`BOARD_BOTTOM_ALLOCATION_PX`), bottom-right corner (recommendation, not locked —
  see `design/ux/interaction-patterns.md` Open Questions), within one-handed thumb reach, never
  bottom-center (`art-bible.md` HUD Density & Layout).
- **Content**: A single icon button. No booster tray or other footer content exists at MVP (Booster
  Brewing Meta is Phase 2, gated — `screen-flow.md` § 11 names its `brewing_loadout_slot` mount point
  on Pre-Level Card, not this footer).
- **Interaction**: Tap opens Pause (`screen-flow.md` T8). Full press-state spec: `design/ux/
  interaction-patterns.md` § Icon Button (Chrome).
- **Sizing**: ≥44×44px touch target floor, restated per-element since Formula 4's proof is specific to
  the board grid, not chrome buttons (see `design/ux/accessibility-requirements.md` § Touch Target
  Floor).

---

## 5. HUD States by Gameplay Context

Sweet Cascade's `GAMEPLAY` base state has exactly **one** HUD configuration — there is no combat/
exploration split, no cinematic-mode HUD hide, and no HUD density change mid-level. The only states
this HUD transitions through are tied directly to `screen-flow.md`'s own composite-state machine:

| Context | Header (Moves/Objectives/Score) | Callout Layer | Footer (Pause) |
|---|---|---|---|
| Active play (`(B3,[])`, input unlocked) | Fully visible, live-updating | Hidden (idle) | Visible |
| Mid-cascade / Reveal Queue replay (`effective_board_input_enabled = false`, no overlay open) | Fully visible, live-updating in step with the replay (§ 4.1) | Visible if `MILESTONE_REVEAL` is active this step | Visible (still tappable — Pause remains reachable even mid-cascade per `screen-flow.md` T8, which itself locks input via the overlay veto term) |
| Pause open (`(B3,[O2])`) | Frozen at last-known values (board is frozen, no new signals fire) | Frozen/hidden — any in-flight Reveal Queue fast-forwards to completion before Pause's own transition, per `juice-layer.md` § 3's suspension rule | Replaced by the Pause overlay's own controls (this footer icon is now behind the overlay) |
| Settings open from Pause (`(B3,[O2,O3])`) | Same as Pause (unchanged, further behind) | Same | Same |
| Results transition pending (T15/T16, waiting on `juice_input_lock`) | Still visible, showing the final settled values | Final cascade's callout still completes normally — this is exactly what `screen-flow.md` § Detailed Rules 10a's wait-on-`juice_input_lock` rule protects | Visible until the Results base-state transition completes |
| Transition to Results Win/Lose | HUD unmounts entirely — Results is a different Base state with its own (not-yet-authored) screen spec | N/A | N/A |

**No leftover-moves "bonus play" state exists**: per `level-objectives.md` § Detailed Rules 9, the
level ends immediately at the objective-completing `board_stabilized`; the HUD never enters a
"level already won, playing out remaining moves" state.

---

## 6. Visual Budget

| Budget Constraint | Limit | Rationale |
|---|---|---|
| Maximum simultaneous header chips | 3 slots (Moves, Objectives, Score) + up to 2 stacked sub-chips within the Objectives slot | Fixed by the Rule of Necessity (§ 1) — this is not a growable inventory without a new GDD signal to justify it |
| Maximum footer elements | 1 (Pause icon) at MVP | Booster tray is a named, reserved, Phase-2-gated future addition, not built now |
| Maximum simultaneous callout overlays | 1 | Only one Reveal Queue is ever active at a time (`juice-layer.md` § 3) — the callout layer structurally cannot show two milestones at once |
| Header % of screen height | ≈33.3% (`BOARD_TOP_ALLOCATION_PX / CANVAS_HEIGHT_PX`) | Fixed by the migrated board-margin constants (§ 3.1) |
| Footer % of screen height | ≈10.4% (`BOARD_BOTTOM_ALLOCATION_PX / CANVAS_HEIGHT_PX`) | Fixed by the migrated board-margin constants |
| Minimum chip text size | 28px (numbers) / 24px (labels), reference canvas | `art-bible.md` Typography/Accessibility |
| Minimum chip touch target (Pause icon only — chips themselves are read-only, not interactive) | 44×44px | `.claude/docs/technical-preferences.md` |

---

## 7. Platform & Input Variants — the 320px-Width Worst Case

### 7.1 What "320px width" means here

Sweet Cascade's board and HUD are authored against a fixed **1080×1920 reference canvas**
(`board-engine.md` Formula 4's stated assumption, inherited by this document per § 3.1's migration).
The design's resolution-independence guarantee — "the proof holds for any physical device" — relies on
a **uniform-scale, letterbox/pillarbox contract**: the entire canvas scales by one factor to fit the
device viewport, preserving every element's *proportion* of the screen exactly, regardless of absolute
device size.

**320px is the classic smallest-supported web/mobile viewport width** (the historical "320 and up"
responsive-design floor, matching both the oldest still-occasionally-tested phone viewport width and —
more relevant to Sweet Cascade specifically — the minimum browser-window width a player could resize
the **Web secondary build** down to, per `.claude/docs/technical-preferences.md`'s Web platform note).
At this width, the uniform scale factor from the 1080-wide reference canvas is:

```
scale_factor = 320 / 1080 ≈ 0.2963
```

### 7.2 What stays true, and what becomes a real risk, at this scale

**Layout proportions are unaffected.** Because every element (chips, gaps, margins, text) scales by
the same factor, the header's 3-chip layout, the board's touch-target headroom (Formula 4's 2.52×
margin holds at *any* uniform scale, since it is a ratio, not an absolute), and the callout layer's
centering all remain structurally correct at 320px width. **This is not a layout-breaking risk.**

**Absolute physical legibility is a real, unresolved risk.** The same uniform scale applies to text
and touch targets, which — unlike layout proportions — have real-world ergonomic minimums that do not
scale with the canvas:
- The 44px `MIN_TOUCH_TARGET_PX` floor, scaled by 0.2963×, becomes **≈13px** in the same units at a
  320px-wide viewport — well below any real finger-touch ergonomic guidance if canvas units are
  treated as roughly 1:1 with physical device pixels/points.
- The 28px/24px text minimums scale to **≈8.3px/≈7.1px** at the same viewport — objectively illegible
  by any standard.

**This tension is not resolved by any existing source** — `board-engine.md`'s own resolution-
independence claim assumes physical device *screen size* stays roughly constant across "any physical
device" (true for phones of similar screen size but different pixel resolutions; not necessarily true
for a narrowed desktop browser window, which is the scenario 320px CSS-pixel width most plausibly
represents for this project's Web build).

### 7.3 Recommendation (not a locked engineering decision)

**[UX-authored — not GDD-sourced]**: rather than allowing pure uniform canvas scaling all the way down
to 320px (which the math above shows produces illegible text and unusable touch targets), this
document recommends:

1. **Define a `MIN_SUPPORTED_LOGICAL_WIDTH_PX` floor** (proposed default: 360px, matching common
   modern-Android minimum logical width) below which the uniform-scale contract stops. Below this
   floor, the Web build should show a "widen your browser window" notice rather than continue scaling
   down into illegibility; native mobile devices are not expected to report a logical width below this
   floor in 2026 (`docs/engine-reference/unity/VERSION.md`'s currency date), so this primarily protects
   the Web secondary target, not the mobile primary target.
2. **At or above that floor**, keep pure uniform scaling — the layout math already holds cleanly.
3. Regardless of the floor chosen, apply a **compact chip content format globally** (icon + number
   only, no word labels — e.g. "🍓 18/20" not "Strawberries: 18/20") as the header chips' *default*
   style at every width, not a narrow-viewport-only fallback. This both protects legibility headroom at
   the low end and keeps the header visually calm at the reference width.

**Concrete numbers at the recommended 360px floor** (if adopted): `scale_factor = 360/1080 ≈ 0.333`;
44px floor → ≈14.7px; 28px text → ≈9.3px. **This is still a meaningful legibility concern even at
360px** — flagged explicitly rather than presented as solved. The real fix is very likely an engine-
level decision (Unity 6.3's UI Toolkit `PanelSettings` scale mode — "Scale With Screen Size" vs. a
match-width/match-height blend vs. a hard minimum text-size floor that breaks pure uniform scaling in
exchange for reflow) that this document defers to `unity-specialist`/`unity-ui-specialist`, not decides
unilaterally. See Open Questions.

### 7.4 Compact Wireframe at 320px-Equivalent Proportions

```
┌────────────────────────────────┐
│ [12]┌────┐ ┌──────┐ ┌────┐ [12]│  ← header, compact icon+number format
│     │🔢 8│ │🍓18/20│ │⭐2100│     │     (chip content per § 7.3.3)
│     └────┘ └──────┘ └────┘     │
├────────────────────────────────┤
│                                │
│             BOARD              │
│                                │
├────────────────────────────────┤
│                          ┌──┐  │
│                          │⏸ │  │  ← footer, single icon
│                          └──┘  │
└────────────────────────────────┘
```

### 7.5 Safe-Area / Notch Handling

The migrated `BOARD_TOP_ALLOCATION_PX` (640px ≈ 33.3% of canvas height) and `BOARD_BOTTOM_ALLOCATION_PX`
(200px ≈ 10.4%) already substantially exceed the generic mobile safe-area convention (~15% top / ~10%
bottom) cited in the studio's HUD-design template — `board-engine.md`'s own rationale for the bottom
allocation explicitly names "OS gesture-bar safe area" as a factor already priced into that constant.
This document adds two additional, smaller insets **inside** those existing allocations
(`HUD_SAFE_AREA_TOP_INSET_PX` = 32px, `HUD_SAFE_AREA_BOTTOM_INSET_PX` = 24px, § 3.2) so chip/icon
content never sits flush against the outer edge of its already-generous zone.

**Static budget vs. runtime device data**: the values above are **static, design-time reservations**
sized to comfortably absorb typical notch/punch-hole/gesture-bar variance across common devices. They
are not a substitute for querying the platform's actual runtime safe-area at launch — flagged as an
implementation requirement (not a design decision) for `unity-specialist`: read Unity's device-reported
safe-area (the runtime API providing `Screen.safeArea`-equivalent data) and, if it ever exceeds this
document's static budget on a specific device, inset the header/footer further at runtime rather than
letting content clip under a notch.

---

## 8. Accessibility

Full commitments live in `design/ux/accessibility-requirements.md`. HUD-specific pointers:

- **Touch targets**: Pause icon ≥44×44px (§ 4.3); header chips are read-only and not subject to the
  touch-target floor, but their text must clear the size/contrast minimums in § 6 and
  `accessibility-requirements.md`.
- **Color-independent communication**: the low-moves pulse and any objective-completion visual state
  must pair with the always-present numeric value — never color-only (`accessibility-requirements.md`
  § Visual Accessibility).
- **Reduced motion**: the low-moves pulse and the callout layer's screen-color pulse both fully
  disable under `reduced_motion_enabled`; the callout's *text* and the core particle-burst pop remain
  (full table: `accessibility-requirements.md` § Reduced Motion — Complete Behavior Table).
- **Flash safety**: the callout layer's screen-color pulse is bound by the same unconditional 3Hz
  ceiling as every other full-screen pulse in the game (`accessibility-requirements.md` § Flash
  Safety) — validated at ≈2.6Hz for default tuning.
- **Screen reader**: not supported for this HUD at MVP — honest scope statement in
  `accessibility-requirements.md` § Screen Reader Intent applies identically here; no chip value change
  is announced to any assistive technology at MVP.

---

## 9. Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease | Source |
|---|---|---|---|---|---|
| `BOARD_SIDE_MARGIN_PX` | 40 | 16–80 | More breathing room around board/header; shrinks `cell_size_px` and header content width for every grid size | Less margin grows both, risks screen-edge crowding | Migrated from `board-engine.md` (§ 3.1) |
| `BOARD_TOP_ALLOCATION_PX` | 640 | 480–800 | More header space; shrinks board's vertical budget | Less header space, risks chip crowding | Migrated from `board-engine.md` |
| `BOARD_BOTTOM_ALLOCATION_PX` | 200 | 120–320 | More footer/safe-area space; shrinks board's vertical budget | Less footer space, risks Pause icon crowding the gesture-bar safe area | Migrated from `board-engine.md` |
| `HUD_CHIP_GAP_PX` | 16 | 8–32 | More visual separation between chips, less content width per chip | Tighter chips, more content width, risks chips reading as one merged block below a certain gap | New — this document |
| `HUD_SAFE_AREA_TOP_INSET_PX` | 32 | 16–64 | More notch/cutout clearance | Less clearance, risks content proximity to a physical notch on some devices | New — this document |
| `HUD_SAFE_AREA_BOTTOM_INSET_PX` | 24 | 16–48 | More gesture-bar clearance | Less clearance | New — this document |
| `MIN_SUPPORTED_LOGICAL_WIDTH_PX` (proposed) | 360 (recommended, not locked) | — | A higher floor protects legibility further but excludes more narrow-window Web players before showing the widen-window notice | A lower floor risks the illegibility documented in § 7.2 | New — this document, recommendation only (see Open Questions) |
| `LOW_MOVES_THRESHOLD` | 3 | 1–6 | Reference only — owned by `level-objectives.md` Formula 6, not this document | Reference only | `level-objectives.md` |
| `RECOMMENDED_MAX_OBJECTIVES_PER_LEVEL` | 2 | 1–3 | Reference only — owned by `level-objectives.md`, constrains this HUD's Objectives chip layout (§ 4.1) | Reference only | `level-objectives.md` |

---

## 10. Acceptance Criteria

- [ ] Moves chip, Objectives chip(s), and Score chip are all present and correctly positioned within
      `BOARD_TOP_ALLOCATION_PX` on every schema-legal grid size (3×3 through 9×9).
- [ ] Moves chip updates within one frame of every `moves_remaining_changed` emission; value always
      matches `level-objectives.md`'s own internal `moves_remaining` exactly (no display-layer drift).
- [ ] Objectives chip(s) correctly render both the 1-objective and 2-objective cases without layout
      reflow of the Moves or Score chip slots.
- [ ] A `collect_color` objective whose `current` exceeds `target_value` (e.g. 23/20) displays a
      clamped 100% progress state while the underlying `current` value used elsewhere (e.g. any future
      stats surface) remains the uncapped raw number (Formula 1, `level-objectives.md`).
- [ ] Score chip's displayed value, once a Reveal Queue fully completes, exactly equals
      `ScoreProvider.get_current_score()`'s live value at that instant — no drift between displayed and
      authoritative score.
- [ ] Low-moves pulse appears only when `is_low_moves = true` per Formula 6, and is visibly suppressed
      the instant `win_condition_met` becomes true even if `moves_remaining ≤ LOW_MOVES_THRESHOLD`.
- [ ] Cascade milestone callout never appears for a `trigger_source = BOOTSTRAP` cascade, regardless of
      its `chain_index` depth.
- [ ] All header/footer content renders within its documented zone with zero overlap at every
      schema-legal grid size and at the 320px-viewport worst case (§ 7).
- [ ] Pause icon meets the ≥44×44px touch-target floor and opens Pause within `MAX_MODAL_TRANSITION_MS`
      (200ms, `screen-flow.md` Formula 2) of being tapped.
- [ ] HUD unmounts cleanly on the Results Win/Lose base-state transition with no stale chip values
      visible after the transition completes.
- [ ] No full-screen pulse effect measured on the callout layer exceeds 3Hz on a physical mid-range
      Android reference device.

---

## 11. Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| **Reciprocal edit needed**: `board-engine.md`'s Tuning Knobs table and Open Questions table should be updated to cross-reference this document for `BOARD_SIDE_MARGIN_PX`/`BOARD_TOP_ALLOCATION_PX`/`BOARD_BOTTOM_ALLOCATION_PX` instead of restating the values. | systems-designer | `board-engine.md`'s next review pass | Not made here — this document may not edit files outside `design/ux/` |
| What Unity 6.3 UI Toolkit `PanelSettings` scale mode should Sweet Cascade use, and does it need a hard minimum-text-size floor (breaking pure uniform scaling) rather than scaling all the way down to very narrow viewports? | unity-specialist / unity-ui-specialist | Before HUD implementation begins | Not resolved — recommendation given in § 7.3, not locked |
| Should `MIN_SUPPORTED_LOGICAL_WIDTH_PX` (proposed 360px) be formally adopted, and does the Web build need a "widen your window" notice below it? | producer / ui-programmer | Before Web export QA pass | Not resolved |
| Does a level authored beyond the 2-objective soft guideline (schema-legal but discouraged) need its own header layout variant, or should `level-data-format.md`'s validation suite gain a hard cap instead? | systems-designer / ux-designer | Before Vertical Slice content-authoring retrospective | Not resolved — mirrors the identical open question already logged in `level-objectives.md` |
| Should the header include a level title/name at any point during active `GAMEPLAY` (art bible's HUD Density note mentions "level title" in the top-third zone, but no GDD signal currently drives one)? | game-designer / ux-designer | Before this document exits Draft | Not resolved — no signal exists to bind it to; excluded from this document's three-chip scope per § 1's Rule of Necessity |
| Exact per-event score-popup ("+180" floating text) placement and timing — named as `juice-layer.md`'s reserved `score_popup_slot` seam, not designed by this document | ux-designer / game-designer (Scoring & Star Thresholds' eventual popup design pass) | At Scoring & Star Thresholds' next presentation-detail pass | Not resolved — explicitly out of this document's scope (§ 4.1 Score Chip) |
| Does the callout layer's screen-color pulse opacity/blend need a dedicated contrast verification pass against the header's chip text (both render simultaneously per § 3.3's z-order)? | art-director / ux-designer | Before the pulse's final visual authoring | Not resolved |
