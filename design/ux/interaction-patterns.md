# Interaction Pattern Library: Sweet Cascade

> **Status**: APPROVED (ux-review, 2026-07-18) — see `design/ux/ux-review-2026-07-18.md`
> **Author**: ux-designer
> **Last Updated**: 2026-07-18
> **Version**: 1.0
> **Engine**: Unity 6.3 LTS (6000.3.x) — UI Toolkit for HUD/menu chrome; world-space FX outside UI Toolkit (`.claude/docs/technical-preferences.md`)
> **Game**: Sweet Cascade (mobile match-3, portrait, one-handed)
> **Related Documents**:
> - `design/gdd/touch-input.md` (APPROVED) — gesture grammar, state machine, formulas
> - `design/gdd/screen-flow.md` (APPROVED, design-review lean) — state machine, overlay stack, back-button policy
> - `design/gdd/juice-layer.md` (APPROVED, design-review lean) — feedback vocabulary, audio/haptics map
> - `design/gdd/level-objectives.md` / `design/gdd/scoring-stars.md` (APPROVED) — HUD chip data models (full spec in `design/ux/hud.md`)
> - `design/art/art-bible.md` (Approved by founder 2026-07-17) — visual language, palette, typography
> - `design/narrative/characters-and-tone.md` (Draft — awaiting founder review) — Fizz voice, placement, line-length rules
> - `design/ux/accessibility-requirements.md` (APPROVED, companion document) — accessibility commitments per pattern
> - `design/ux/hud.md` (APPROVED, companion document) — HUD chip layout and data bindings
> - `prototypes/playable-slice/` — working reference implementation of most patterns below (throwaway code, not a spec source — see `.claude/rules/prototype-code.md`)

> **Purpose of this document**: This is a **formalization pass**, not a fresh design
> session. Every pattern below documents a decision already made and approved in the
> GDDs cited above. Nothing here re-opens a resolved design question; where a gap
> exists in the source material (a detail the GDDs left to "UX/Juice territory"), it
> is filled here and explicitly marked **[UX-authored — not GDD-sourced]** so implementers
> know which lines are binding game-design contract vs. which are this document's own call.

> **Engine-porting note**: Several cited GDDs (`touch-input.md`, `board-engine.md`,
> `screen-flow.md`) were authored while the project was pinned to Godot 4.6 (see
> `docs/engine-reference/unity/VERSION.md` § Project History) and contain
> Godot-specific implementation asides (GDScript, `gdUnit4`, Godot's `canvas_items`
> stretch mode, `TabContainer`/`LineEdit`, etc.). The project is now pinned to Unity
> 6.3 LTS (ADR-001, 2026-07-18). **The design-level contracts — formulas, thresholds,
> state machines, timing budgets — are engine-agnostic and unchanged.** This document
> translates Implementation Notes to the Unity 6.3 stack (Input System, UI Toolkit)
> and flags where a Godot-specific detail has no direct Unity equivalent yet decided.

---

## Overview

This is the reusable interaction pattern library for Sweet Cascade. Any screen or
system spec that needs a button, an overlay, a gesture, or a piece of read-only HUD
chrome should reference a pattern here by name rather than re-specifying press
states, audio, and accessibility from scratch. Sweet Cascade's input surface is
deliberately small — one board gesture grammar (two parallel paths), a handful of
chrome buttons, three overlay types, one map-node pattern, and one mascot
mount-point pattern — so this library is intentionally short rather than
padded out with unused generic controls (no `Dropdown`, `Slider`, or `Tab Bar`
patterns are defined here; Settings' actual control inventory is out of this task's
scope and should be added when `design/ux/settings.md` is authored).

**Platform/input scope for every pattern below** (`.claude/docs/technical-preferences.md`):
Touch (primary) and Mouse (editor/desktop + Web secondary target) only. **No gamepad
support.** No keyboard-driven gameplay input. Where a pattern's accessibility
subsection would normally specify keyboard/gamepad focus behavior, it instead states
"N/A — see `accessibility-requirements.md` § Known Intentional Limitations" rather
than silently omitting the section.

---

## Pattern Catalog Index

| Pattern Name | Category | Description | Source | Status |
|---|---|---|---|---|
| Swap Gesture — Swipe | Input | Drag past a distance threshold; snaps to dominant axis | `touch-input.md` §1–2, Formulas 1–2 | Stable (validated in playable slice) |
| Swap Gesture — Tap-Tap | Input | Tap to select, tap adjacent to swap, tap again to deselect | `touch-input.md` §1–2 | Stable (validated in playable slice) |
| Selection Highlight | Feedback | Visual state for an armed (`AwaitingSecond`) cell | `touch-input.md` §2; `juice-layer.md` §1 (not a motion effect) | Stable |
| Hover Ring (desktop/web-only) | Feedback | Additive-only soft highlight on mouse hover, no button held | `touch-input.md` §6 | Stable |
| Button (Primary CTA) | Input | Play, Retry, Resume — the one primary action per screen | `art-bible.md` Menu Layout Principles; `juice-layer.md` §4 generic UI chrome row | Draft |
| Button (Secondary) | Input | Back, Cancel, Map, Settings gear | `art-bible.md`; `juice-layer.md` §4 | Draft |
| Icon Button (Chrome) | Input | Pause icon, Settings gear — small persistent corner controls | `art-bible.md` HUD Density; `screen-flow.md` §8 (Fizz boundary applies to placement, not this pattern) | Draft |
| Overlay Open/Dismiss — Modal Card | Navigation/Modal | Pre-Level Card, Pause, Settings — card-based overlay on a dimmed host screen | `screen-flow.md` §1–4; `art-bible.md` Menu Layout Principles | Draft |
| No-Confirmation Exit — Quit to Map | Navigation | Deliberate exception: Quit to Map from Pause has **no** confirmation dialog | `screen-flow.md` T12, §3 | Stable (documented exception) |
| OS/Hardware Back Mapping | Navigation | Android back synonymous with each state's dismiss action | `screen-flow.md` §3 (full table) | Stable |
| World Map Node | Game-Specific | Locked/unlocked/star-badge level node | `screen-flow.md` §9, Formula 4; `art-bible.md` | Draft |
| Fizz Mount Point | Game-Specific | Non-blocking mascot line, auto-dismiss + tap-through | `screen-flow.md` §8; `characters-and-tone.md` §2 | Stable |
| Low-Moves Warning Pulse | Feedback | Amber pulse on the move counter only when moves are critically low | `level-objectives.md` §7, Formula 6; `art-bible.md` Semantic UI Accents | Draft — full spec in `hud.md` |
| Cascade Milestone Callout | Feedback | Escalating "Sweet! → Delicious! → Spectacular!" overlay on deep cascades | `juice-layer.md` §5 | Draft |
| Swap Revert (Soft Shake) | Feedback | Non-punishing feedback for an invalid swap | `juice-layer.md` §4 | Stable |

---

## Patterns

### Swap Gesture — Swipe

**Category**: Input
**Status**: Stable
**When to Use**: The board's primary drag-based swap gesture. Always ships alongside Tap-Tap, never alone.
**When NOT to Use**: Never gated behind a settings toggle or "advanced mode" — it is a first-class default path (`touch-input.md` §1).

**Interaction Specification**:

| State | Visual | Input | Response | Duration | Audio/Haptic |
|---|---|---|---|---|---|
| Gesture start | No visual change yet | Pointer/touch down inside a cell's hit-area (full `cell_size_px` pitch, never the 84%-scaled candy sprite — `touch-input.md` Formula 3) | Begins tracking `distance` from `(x_start, y_start)` | — | — |
| Below threshold | No visual change (this is still a candidate tap) | Pointer/touch moves, `distance < threshold_px` | No intent emitted | — | — |
| Threshold crossed (`gesture_is_swipe = true`) | Optimistic slide begins immediately — the two candies tween to each other's cells before Board Engine confirms validity | `distance ≥ threshold_px` (Formula 1) | `swap_request(cell_a, cell_b)` emitted the instant the threshold is crossed — resolution does not wait for release | `SWAP_SLIDE_DURATION_MS` = 150ms (`juice-layer.md` Tuning Knobs) | `audio_swap_slide` |
| Swap accepted | Slide completes cleanly, no shake, leads into `CLEAR_REVEAL` | Board Engine confirms match/activation | — | — | `audio_swap_accept` + `haptic_swap_success` (single light tick) |
| Swap rejected | Slide completes, soft shake-wobble, both pieces slide back to origin — see **Swap Revert** pattern below | Board Engine reports no match, no activation | Zero move consumed, zero penalty | `SWAP_REVERT_SHAKE_MS` = 130ms | `audio_swap_revert` (deliberately soft, never a buzzer/error tone) |
| Gesture lost mid-drag | If no threshold crossed and no prior selection: nothing. If a selection was pending: it clears (`cancel()`). | OS touch-cancel, app loses focus, release outside viewport | State returns to Idle | — | — |
| Gesture during locked input | Never begins (hard gate) | `effective_board_input_enabled = false` (`screen-flow.md` Formula 5 — composes Board Engine busy, `overlay_is_active`, and `juice_input_lock`) | No intent, no visual feedback, no queue (MVP default `input_buffer_depth = 0`) | — | — |

**Formula reference** (binding, `touch-input.md` Formula 1):
```
threshold_px = clamp(threshold_ratio × cell_size_px, threshold_min_px, threshold_max_px)
```
Defaults: `threshold_ratio = 0.35`, `threshold_min_px = 24`, `threshold_max_px = 60`. Dominant-axis
resolution (Formula 2) always snaps to exactly one of the 4 orthogonal neighbors — diagonal targets
are structurally impossible. Exact-tie (`|dx| == |dy|`) resolves to vertical.

**Accessibility**:
- Motor: A drag gesture excludes players using adaptive single-tap switches — this is why **Tap-Tap
  ships simultaneously, never as a fallback** (see below, and `accessibility-requirements.md` § Motor).
- No maximum hold duration exists anywhere in the gesture grammar — a slow, deliberate drag is still
  a valid swipe as long as it eventually crosses the threshold before release (`touch-input.md` §1).
- Touch target: hit-testing always uses the full `cell_size_px` pitch (≥44px, proven for the entire
  3–9 grid range — `board-engine.md` Formula 4), never the smaller 84%-scaled visible candy sprite.
- Keyboard/Gamepad: N/A — see `accessibility-requirements.md` § Known Intentional Limitations.

**Implementation Notes** (Unity 6.3): Use the Input System package's pointer/touch action with phase
tracking (`Started`/`Performed`/`Canceled`), driving a single unified gesture state machine shared by
touch and mouse (mirrors `touch-input.md` §6's "single unified pointer input stream" contract — left
mouse button down = pointer-down, drag = pointer-move, release = pointer-up). Track `distance` in the
same canvas-space coordinate system used for hit-testing (UI Toolkit panel coordinates, or the
board's world-space-to-canvas mapping — an implementation decision for `unity-specialist` in
coordination with `unity-ui-specialist`, not decided here). Compare squared distance against squared
threshold to avoid a per-frame `sqrt` call, per `touch-input.md`'s own non-normative performance note.

---

### Swap Gesture — Tap-Tap

**Category**: Input
**Status**: Stable
**When to Use**: The board's primary tap-based swap gesture — a first-class, always-available path,
never a settings-gated "accessibility mode" (`touch-input.md` §7). Must independently complete a full
level start to finish with zero swiping.
**When NOT to Use**: Never disabled or hidden, even if Swipe is also present.

**Interaction Specification** (state machine, `touch-input.md` §2):

| State | Trigger | Response | Next State |
|---|---|---|---|
| Idle | Tap cell A | `select_cell(A)` — selection highlight appears | AwaitingSecond(A) |
| AwaitingSecond(A) | Tap A again | `cancel()` — highlight clears | Idle |
| AwaitingSecond(A) | Tap adjacent cell B (Manhattan distance 1) | `swap_request(A, B)` | Idle |
| AwaitingSecond(A) | Tap non-adjacent cell C | `select_cell(C)` — highlight moves, no swap attempted | AwaitingSecond(C) |
| Any | Swipe begins from any cell, resolves to a valid swap | Any pending tap-tap selection is cleared first (`cancel()`), then `swap_request` fires | Idle |

**Timing**: Selection highlight appears/clears within ≤1 frame (≤16.6ms) of the tap — Touch & Input's
own contribution to latency is always ≤1 frame by design (`touch-input.md` § Player Fantasy).

**Accessibility**:
- This is the primary mechanism by which Sweet Cascade supports players using adaptive switches or
  other single-tap input devices who cannot perform a drag gesture at all (`touch-input.md` §7).
- No minimum or maximum hold duration — a slow, deliberate tap is still a tap.
- Same 44px hit-area floor as Swipe (identical hit-testing, same cell).
- Keyboard/Gamepad: N/A — see `accessibility-requirements.md` § Known Intentional Limitations.

**Implementation Notes** (Unity 6.3): Both gesture paths share one state machine instance (see Swap
Gesture — Swipe) — do not implement Tap-Tap as a separate input handler layered on top of Swipe;
`touch-input.md`'s state table is the single source of truth for how the two interleave.

---

### Selection Highlight

**Category**: Feedback
**Status**: Stable
**When to Use**: Visual acknowledgment that a cell is currently "armed" in Tap-Tap's `AwaitingSecond`
state.
**When NOT to Use**: Never persists past a resolved swap, cancel, or reselection — always exactly zero
or one cell highlighted at a time.

**Specification**: A simple binary visual state toggle (highlighted / not highlighted) on the selected
cell. Per `touch-input.md` §7, this is explicitly **not** a motion effect and is therefore **not**
subject to the `reduced_motion_enabled` toggle — it must remain visible regardless of that setting.
Appears/disappears within ≤1 frame of the triggering tap.

**Accessibility**: Must not rely on color alone (art bible's icon/color-never-alone rule extends to
any state-communicating overlay) — pair the highlight color with a shape change (e.g., an outline or
glow ring), not a color shift on the candy itself. **[UX-authored — not GDD-sourced: exact visual
treatment (ring vs. glow vs. lift) is `art-director` territory; this note constrains it to be
non-color-only.]**

---

### Hover Ring (Desktop/Web-Only)

**Category**: Feedback
**Status**: Stable
**When to Use**: Mouse pointer resting over a cell with no button held, on the Web build only.
**When NOT to Use**: **Never** as a required affordance. Never emits an intent. Never changes
state-machine state. Must be entirely absent with zero loss of information on touch devices, where it
never occurs (`touch-input.md` §6; `.claude/docs/technical-preferences.md`'s "no hover-only
interactions" rule).

**Specification**: A soft highlight ring appears while the mouse hovers a cell with no button pressed;
disappears on pointer-leave. Purely additive — removing it changes nothing about what the player can
do or understand.

---

### Button (Primary CTA)

**Category**: Input
**Status**: Draft
**When to Use**: The single most important action on a screen: Play (Pre-Level Card), Retry (Results
Win/Lose), Resume (Pause), Next Level (Results Win, conditional). Per `art-bible.md` Menu Layout
Principles: **one primary action per screen** — two equally-prominent CTAs is a hierarchy failure.
**When NOT to Use**: Any secondary/dismiss action (use Button — Secondary).

**Interaction Specification**:

| State | Visual | Input | Response | Audio/Haptic |
|---|---|---|---|---|
| Default | Full-saturation fill, CTA Red `#e63950` or context color, size + saturation communicate priority per art bible | — | — | — |
| Hovered (mouse, desktop/web only) | Brightness +15%, subtle scale (additive only) | Mouse over | — | — |
| Pressed | Scale down slightly, brightness −10% | Touch/click down | Action fires on release, not press-down, matching the pattern library convention that a player can drag off to cancel a press | `audio_ui_tap` + `haptic_ui_light` (`juice-layer.md` §4 generic UI chrome row — reused identically across every button, never authored per-screen) |
| Disabled | ~40% opacity, no press response | — | No response | — |

**Sizing**: ≥44×44px touch target (`art-bible.md`, restated `.claude/docs/technical-preferences.md`),
≥8px spacing from any adjacent interactive element.

**Accessibility**: Icon/label pairing where relevant — never icon-only for a CTA whose meaning isn't
self-evident. Keyboard/Gamepad: N/A — see `accessibility-requirements.md` § Known Intentional
Limitations.

**Implementation Notes** (Unity 6.3): UI Toolkit `Button` control (or a custom `VisualElement` if the
glossy-jelly button treatment needs custom USS beyond what a stock Button supports) with USS
pseudo-classes for `:hover`/`:active`/`:disabled`. Because there is no keyboard/gamepad focus
requirement, UI Toolkit's default focus ring may be suppressed for this project unless a future
screen-reader pass (see `accessibility-requirements.md`) requires it — do not remove the underlying
`AccessibilityRole`/focusable metadata, only the visual outline. Validate authored USS against Unity
6.3's stricter USS parser (`docs/engine-reference/unity/VERSION.md`).

---

### Button (Secondary)

**Category**: Input
**Status**: Draft
**When to Use**: Back, Cancel, Map, Settings gear — any alternative or dismiss action with lower
visual weight than the screen's Primary CTA.
**When NOT to Use**: The single most important action on a screen (use Primary). Never used for a
destructive/data-loss action — Sweet Cascade has no persistent-data-destroying player action in scope
at MVP (no save-delete, no purchase-reversal), so no **Button (Destructive)** pattern is defined here;
add one if a future feature introduces one.

**Interaction Specification**: Same state table shape as Primary CTA, lower visual weight (outline or
desaturated fill vs. Primary's full-saturation fill), same audio/haptic pair (`audio_ui_tap` +
`haptic_ui_light`), same 44px floor.

**Accessibility**: Same as Button (Primary CTA).

---

### Icon Button (Chrome)

**Category**: Input
**Status**: Draft
**When to Use**: Persistent, corner-anchored controls that are not the screen's primary or secondary
action — the Pause icon (Gameplay) and Settings gear (World Map, Pause). Positioned per `art-bible.md`
HUD Density: bottom edge/corner, within one-handed thumb reach, **never bottom-center** (avoids
confusion with an OS home-gesture area).

**Interaction Specification**: Identical press-state contract to Button (Primary/Secondary) at a
smaller visual footprint — same 44×44px floor still applies (icon buttons are the most common place a
touch target accidentally shrinks below the floor; do not let icon-only chrome round down). Same
`audio_ui_tap` + `haptic_ui_light` pair.

**[UX-authored — not GDD-sourced]**: Neither `screen-flow.md` nor `art-bible.md` locks which bottom
corner hosts the Pause icon. **Recommendation**: bottom-right, reasoning from Fitts's Law and the
one-handed-play majority-right-thumb assumption already implicit in `technical-preferences.md`'s
"one-handed play" note — this is the single most-pressed piece of chrome during active play (it is
the sole path to Pause) and should sit at the natural resting position of a right-thumb one-handed
grip. Flagged as an open question below for a left-handed mirroring option.

---

### Overlay Open/Dismiss — Modal Card

**Category**: Navigation/Modal
**Status**: Draft
**When to Use**: Pre-Level Card, Pause, Settings — every overlay in `screen-flow.md`'s Overlay Layer
(§1). All three share the same card-based visual language: a generously-rounded Patisserie Cream
(`#fff8ef`) card on a dimmed host screen (`art-bible.md` Menu Layout Principles).
**When NOT to Use**: Never for the board itself — the board is never dimmed or covered by a card
overlay; the only two things that suspend live gameplay are Pause/Settings opening on top of it or the
app losing OS focus (`screen-flow.md` §4).

**Interaction Specification**:

| State | Visual | Trigger | Response | Duration Ceiling |
|---|---|---|---|---|
| Opening | Host screen dims; card scales/fades in | Overlay-Push transition (T2, T6, T8, T9 auto, T13) | Card renders on top; host screen remains visible but non-interactive | ≤`MAX_MODAL_TRANSITION_MS` = 200ms (`screen-flow.md` Formula 2, Tuning Knobs) |
| Active | Card holds all input focus | — | Only the card's own controls are interactive | — |
| Dismissing | Card scales/fades out; host screen undims | Back/OS-back, or the card's own primary action (T5, T7, T10, T14) | Returns to the exact host state it opened from | ≤`MAX_MODAL_TRANSITION_MS` = 200ms |

**Overlay stack rule** (`screen-flow.md` §1, hard architectural constant): maximum stack depth **2**
(`OVERLAY_STACK_MAX_DEPTH`). Settings may open from World Map (depth 1) or from Pause (depth 2, i.e.
`GAMEPLAY` + `PAUSE` + `SETTINGS`). Settings is never opened directly from Pre-Level Card — Pre-Level
Card exposes exactly one primary action (Play) and one dismiss action (Back), per `art-bible.md`'s
one-primary-action rule.

**Suspension rule**: opening Pause or Settings on top of `GAMEPLAY` freezes the board (no simulation
steps, no gesture accepted) via `effective_board_input_enabled` (`screen-flow.md` Formula 5). An
in-progress gesture is cancelled immediately if a modal opens mid-gesture.

**Accessibility**:
- Focus trap equivalent: while a card is open, the host screen behind it is fully non-interactive —
  there is no tunneling input through to board or map content behind an open overlay.
- Every card has a clear, single dismiss action (Back/OS-back always maps to the same result per the
  table in the **OS/Hardware Back Mapping** pattern below) — never a card with no way out other than
  completing its primary action.
- Keyboard/Gamepad focus-trap semantics: N/A — see `accessibility-requirements.md` § Known Intentional
  Limitations (no keyboard/gamepad navigation exists to trap in the first place).

**Implementation Notes** (Unity 6.3): UI Toolkit `VisualElement` panel on a dedicated overlay layer
(sort order above gameplay HUD, below nothing — overlays are the topmost layer in this game's
z-order, see `design/ux/hud.md` § Layout Zones for the full z-order stack). Card enter/exit animation
uses UI Toolkit's transition system or a driven USS custom property, respecting
`REDUCED_MOTION_DURATION_SCALE` (0.7×, `juice-layer.md` Tuning Knobs) when the player's reduced-motion
setting is on — cross-fade only, no scale, per the reduced-motion behavior table in
`accessibility-requirements.md`.

---

### No-Confirmation Exit — Quit to Map

**Category**: Navigation
**Status**: Stable (documented, deliberate exception)
**When to Use**: The "Quit to Map" action inside the Pause overlay (`screen-flow.md` T12).
**When NOT to Use**: This is a narrow, named exception — do not generalize it to any other
data-affecting action without the same explicit reasoning `screen-flow.md` gives.

**Specification**: Tapping Quit to Map from Pause transitions directly to World Map with **no
confirmation dialog** — a deliberate deviation from the studio's usual destructive-action pattern
(compare `.claude/docs/templates/interaction-pattern-library.md`'s own `Button (Destructive)`
convention, which this project does not apply here). This is not treated as data loss requiring
confirmation because the in-progress attempt was never going to be saved regardless — `screen-flow.md`
explicitly states "level abandoned; no `LevelRecord` write" — nothing the player has already earned
(prior best stars/score, already-persisted progress) is ever at risk from this action.

**Accessibility/UX rationale** (`screen-flow.md` §3): a confirmation dialog is itself "a tax on the
player's time" that would work against the "never makes you wait" Player Fantasy guarantee this
document's source GDD protects. Because the action is safely reversible in the sense that matters
(nothing earned is lost), the standard confirm-before-destroy pattern does not apply.

---

### OS/Hardware Back Mapping

**Category**: Navigation
**Status**: Stable
**When to Use**: Every screen. Android hardware/gesture back (and any platform-equivalent system back
gesture) is synonymous with each state's designated dismiss action.

**Full policy table** (binding, `screen-flow.md` §3 — reproduced here for pattern-library
completeness; `screen-flow.md` remains the source of truth):

| Composite State | Back Result |
|---|---|
| Boot/Loading | Disabled — nothing to back out of |
| World Map (root) | OS default (app suspend/exit) — **not intercepted**, no custom "exit app?" dialog |
| Pre-Level Card | Closes card, returns to World Map |
| Settings (from Map) | Closes Settings, returns to World Map |
| Gameplay | Opens Pause |
| Pause | Closes Pause, resumes Gameplay |
| Settings (from Pause) | Closes Settings, returns to Pause |
| Results Win | Same as tapping Map |
| Results Lose | Same as tapping Map |

**Rationale for the un-intercepted root**: standard OS back-to-suspend behavior at the navigation root
is what players already expect and is unsurprising — inserting a custom confirmation here would be the
same "tax on time" the **No-Confirmation Exit** pattern above already argues against.

---

### World Map Node

**Category**: Game-Specific
**Status**: Draft
**When to Use**: Every tappable level entry on the World Map's MVP placeholder linear level list
(`screen-flow.md` §9).

**States**:

| State | Visual | Interaction |
|---|---|---|
| Unlocked, not yet played | Full-color node, no star badge | Tap opens Pre-Level Card (T2) |
| Unlocked, previously completed | Full-color node + star badge (0–3 stars earned, `best_stars`) | Tap opens Pre-Level Card, showing `best_stars`/`best_score` alongside the level's objectives |
| Locked | Dimmed/desaturated node, no badge | Tap is a no-op (T3) — **no dialog explaining why**; the disabled visual state itself communicates the lock, per `screen-flow.md`'s edge-case note that "a locked node's visual state already communicates why; no extra screen needed" |

**Unlock gate** (binding, `screen-flow.md` Formula 4, MVP placeholder): `is_unlocked(level) =
(display_number == 1) OR (best_stars(level_prev) >= MIN_STARS_TO_UNLOCK_NEXT)`, default
`MIN_STARS_TO_UNLOCK_NEXT = 1`. This is explicitly a placeholder pending Level Progression / World Map
(#11) — the same architectural node type persists when that system supersedes the placeholder content.

**Accessibility**: Lock state must not be color-only — a locked node needs a non-color signal too (a
padlock icon or reduced-contrast silhouette), consistent with the colorblind double-coding rule applied
project-wide. **[UX-authored — not GDD-sourced: exact lock-icon treatment is `art-director` territory;
this note constrains it to be non-color-only, extending the same rule `interaction-pattern-library.md`
template's `Grid Item`/`Inventory Slot` patterns already apply to locked states.]** Touch target ≥44px
per node, ≥8px spacing between adjacent nodes.

---

### Fizz Mount Point

**Category**: Game-Specific
**Status**: Stable
**When to Use**: Pre-Level Card (Level Intro pool, one line before Play), Results Win (Win pool /
Star Milestone sub-pool keyed to `stars_earned`), Results Lose (Lose pool, "so close!" energy always).
World Map hosts Fizz only as a persistent idle-animation companion at MVP — no line pool there yet
(`screen-flow.md` §8, `characters-and-tone.md` §2).
**When NOT to Use**: **Never** inside the board frame during active play — Gameplay, Pause, Settings,
and Boot/Loading are a hard "never" (`screen-flow.md` §8), not conditional, not overridden by any
future screen content.

**Specification**:
- Exactly one line, drawn from exactly one named pool per mount point, shown alongside the screen's
  primary action.
- **Non-blocking rule** (binding): the line auto-dismisses after `FIZZ_LINE_DISPLAY_MS` = 3,000ms
  (`screen-flow.md` Tuning Knobs) **and** is instantly dismissed by any tap on that screen's primary
  action (Play, Retry, Map). It never delays that action becoming tappable and never requires its own
  dismiss tap.
- If a pool has no content available for the current locale/build, the mount point renders **empty**
  — never a placeholder string or a broken/missing-string glyph ("silence is valid," per
  `characters-and-tone.md` and `screen-flow.md`'s own edge case).
- Line length: ≤60 characters including spaces/punctuation for repeated in-level lines (hard ceiling
  ~80 characters, flagged as an explicit exception if exceeded) — `characters-and-tone.md` § Reading
  Level & Length. No em dashes, semicolons, or colons. Second person. Exclamation points rationed to
  one per line. No emoji.

**Interaction**: Fizz's own text/portrait is **not independently tappable** — there is no
"tap Fizz for another line" interaction defined anywhere in source material; the only tap that affects
Fizz's line is the screen's own primary/secondary action dismissing it early. **[UX-authored — not
GDD-sourced: if a future pass wants Fizz to be independently interactive (e.g., tap-to-cycle lines),
that is a new pattern requiring its own design pass, not assumed here.]**

**Accessibility**: Text-only for launch scope (no VO implied) — this means Fizz's content is
inherently "captioned" by construction; no separate subtitle toggle is needed for mascot lines. See
`accessibility-requirements.md` § Auditory Accessibility for the full auditory scope statement.

---

### Low-Moves Warning Pulse

**Category**: Feedback
**Status**: Draft — full chip-level spec lives in `design/ux/hud.md`
**When to Use**: The move-counter chip only, when `is_low_moves` resolves true.

**Specification** (binding, `level-objectives.md` Formula 6 + `art-bible.md` Semantic UI Accents):
`is_low_moves = (moves_remaining ≤ LOW_MOVES_THRESHOLD) AND (NOT all_objectives_complete)`, default
`LOW_MOVES_THRESHOLD = 3`. Subtle amber (`#d97b2e`) pulse on the move counter chip **only** — never
the board itself. Deliberately distinct from Candy Citrus Orange (`#ff9f45`) so no candy reads as
"dangerous." The warning is suppressed the instant every objective is already complete (no needless
urgency flash immediately before a win screen).

**Accessibility**: Color is not the sole signal — the pulse must be paired with the numeric
moves-remaining value already always visible in the chip (never color-only). Pulse cadence must
satisfy the flash-safety ceiling (see `accessibility-requirements.md` § Flash Safety) — a "pulse," not
a "flash," and well under 3Hz by construction since it is a slow breathing animation, not a rapid
strobe. Respects `reduced_motion_enabled` — see the reduced-motion behavior table in
`accessibility-requirements.md` (numeric value remains the primary signal either way).

---

### Cascade Milestone Callout

**Category**: Feedback
**Status**: Draft
**When to Use**: `MILESTONE_REVEAL` steps only — `chain_index ≥ 2` **and** `trigger_source ∈
{SWAP_MATCH, SPECIAL_ACTIVATION}`. Never for a bootstrap-sourced cascade, however deep
(`juice-layer.md` §5).

**Tier table** (binding, placeholder copy pending `narrative-director` authoring against
`characters-and-tone.md`'s voice rules):

| Tier | `chain_index` Range | Placeholder Copy | Visual | Audio |
|---|---|---|---|---|
| 1 | 2–3 | "Sweet!" | Standard pop scale + particle boost | `audio_cascade_milestone_1` |
| 2 | 4–5 | "Delicious!" | Screen-color pulse begins (4+ chain rule) | `audio_cascade_milestone_2` |
| 3 (max) | 6+ | "Spectacular!" | Pulse + largest particle boost this ladder reaches | `audio_cascade_milestone_3` |

The "×N" suffix is a presentation-layer readout of `chain_index` itself, always matching the actual
score multiplier applied (`scoring-stars.md` §3 — the chain multiplier is linear and uncapped
specifically so the number the player reads and the number that pays out never diverge).

**Accessibility**: Screen-color pulse portion is fully disabled under `reduced_motion_enabled` (hard
off, not scaled down) — the callout text itself remains, since it is not a motion effect. See flash
safety ceiling and full reduced-motion table in `accessibility-requirements.md`.

---

### Swap Revert (Soft Shake)

**Category**: Feedback
**Status**: Stable
**When to Use**: Any `swap_rejected` event (no match, no activation).
**When NOT to Use**: Never treated as punishment — no error tone, no red flash, no move consumed.

**Specification**: Slide completes to the swapped position, a soft shake-wobble plays
(`SWAP_REVERT_SHAKE_MS` = 130ms), then both pieces slide back to their origin cells. Audio is
`audio_swap_revert`, deliberately soft — **never a buzzer/error tone**, per both `juice-layer.md` §4
and `characters-and-tone.md`'s "never punishing" voice pillar extended into sound design. No haptic
fires for a rejected swap (zero penalty).

---

## NEVER Patterns

These are hard rules across every screen and pattern in Sweet Cascade — a violation of any of these
is a defect, not a stylistic choice, regardless of which screen or feature introduces it.

1. **Never hover-only.** No interaction may require a hover state to discover or use. Hover is
   additive-only, desktop/web-only, and the game must be fully playable and fully understandable with
   zero loss of information when hover never occurs (as it never does on touch) —
   `.claude/docs/technical-preferences.md`; `touch-input.md` §6.
2. **Never swipe-only without the tap-tap path.** Swipe and Tap-Tap ship together, always, as two
   equally first-class paths — Tap-Tap is never a fallback, an "accessibility mode" toggle, or staged
   after swipe — `touch-input.md` §1, §7.
3. **Never blocking flavor text.** Fizz's lines (or any future flavor text) never delay a screen's
   primary action from becoming tappable, and always auto-dismiss even if the player never interacts
   with them — `screen-flow.md` §8's non-blocking rule.
4. **Never color as the sole signal.** Every state-communicating element (candy identity, lock state,
   warning state, error state, rarity, selection) pairs color with shape, icon, or numeric text —
   `art-bible.md` Accessibility; `characters-and-tone.md`.
5. **Never a confirmation-required destructive action shipped without a Confirmation Dialog** — except
   the one explicitly-named, explicitly-reasoned exception (**No-Confirmation Exit — Quit to Map**
   above). Any new destructive action must either use a confirmation pattern or carry the same explicit
   "nothing earned is actually at risk" reasoning in its own spec.
6. **Never a full-screen flash/pulse effect above 3Hz**, regardless of `reduced_motion_enabled` state
   (WCAG 2.3.1-sourced hard ceiling, `juice-layer.md` §8, Formula 6) — see
   `accessibility-requirements.md` § Flash Safety.
7. **Never a UI element inside the active board frame during gameplay other than the board itself and
   player input feedback** — no mascot, no banner, no modal renders over or interrupts a live board
   except the two named suspension events (Pause tap, app-background) — `screen-flow.md` §8;
   `art-bible.md` Visual Hierarchy.
8. **Never a touch target below 44×44px**, anywhere, for any interactive element — `board-engine.md`
   Formula 4 (proven for the full 3–9 grid range); `art-bible.md`; `touch-input.md` Formula 3.
9. **Never a UI element that silently diverges from actual game state** — no shimmer/glow implying a
   hint or shuffle that isn't actually available, no stale star count, no settings toggle that hasn't
   taken effect — `art-bible.md` Pillar 2 Compliance Note; `screen-flow.md` § Player Fantasy guarantee 3.

---

## Gaps & Patterns Needed

The following are named but **not yet fully specced** at the pixel/asset level — flagged for
`art-director` (visual) and `ui-programmer` (implementation feasibility) follow-up, not solved here:

- **Selection Highlight** exact visual treatment (ring/glow/lift) — constrained to non-color-only
  above, but the specific art direction is not this document's call.
- **Icon Button (Chrome)** corner placement (bottom-left vs. bottom-right for Pause) — a recommendation
  is given above, not a locked decision; see Open Questions.
- **World Map Node** locked-state iconography (padlock vs. desaturation-only) — needs a concrete asset
  decision from `art-director`.
- **Settings screen's own control inventory** (toggles, sliders for the four settings fields in
  `save-persistence.md`'s Settings sub-schema: `music_enabled`, `sfx_enabled`, `haptics_enabled`,
  `reduced_motion_enabled`, plus `colorblind_assist_enabled`) is out of this task's scope — author
  `design/ux/settings.md` referencing the studio's generic `Toggle` pattern
  (`.claude/docs/templates/interaction-pattern-library.md`) when that screen is speced.
- **Results screen layout** (star ceremony composition, closest-miss summary display) is HUD/UX
  territory not covered by this pattern library or by `design/ux/hud.md` (which is scoped to the
  in-gameplay HUD only) — flagged as a future `design/ux/results.md` need.

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| Which bottom corner hosts the Pause icon — is it fixed, or does a left-handed accessibility option mirror it to the opposite corner? | ux-designer / art-director | Before `design/ux/hud.md` footer implementation | Recommendation given above (bottom-right); not locked |
| Does Fizz ever become independently tappable (e.g., tap-to-cycle another line) in a future pass? | narrative-director / game-designer | Not scheduled | Out of scope at MVP — no pattern defined |
| Should locked World Map nodes get a tap-rejection micro-feedback (e.g., a tiny shake) instead of a pure no-op, to confirm the tap registered at all? | ux-designer / game-designer | Before Level Progression / World Map (#11) authoring | Not resolved — `screen-flow.md` T3 currently specifies a pure no-op |
