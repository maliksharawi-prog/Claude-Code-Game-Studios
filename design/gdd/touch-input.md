# Touch & Input System

*Status: Reviewed — APPROVED (design-review lean, 2026-07-18) — see `design/gdd/reviews/touch-input-review-log.md`*
*Cross-GDD sync, 2026-07-18: added a Juice Layer Dependencies boundary row;
§4's input-gate description now cites the ratified 4-term
`effective_board_input_enabled` composition (`screen-flow.md` Formula 5);
Board Engine's Dependencies row now notes it as `cell_size_px`'s
authoritative computed source (its Formula 4).*
*Created: 2026-07-17*
*Last Updated: 2026-07-17*
*Layer: Foundation · Priority: MVP · Phase: MVP · Category: Core*
*Depends On: — (none — Foundation layer, no prerequisite GDDs)*
*Consumed By: Match-3 Board Engine (`design/gdd/board-engine.md`, not yet authored)*
*Source: `design/gdd/game-concept.md` · `design/gdd/systems-index.md` · `prototypes/sweet-cascade-concept/REPORT.md` + `prototype.html`*

---

## Overview

The Touch & Input System is the Foundation-layer system that translates raw
pointer input — touch on mobile, mouse on the web build — into a small, typed
vocabulary of board intents consumed by the Match-3 Board Engine. It owns
gesture *recognition* only: tap detection, swipe-threshold and dominant-axis
resolution, and tap-tap select/swap/deselect sequencing. It has no knowledge
of candy colors, match legality, specials, or any board simulation state, and
it never mutates the board directly — per the systems-index boundary, "input
translates gestures into board intents; it does NOT validate swaps." Two
parallel, always-available input paths are supported end to end: **swipe-to-
swap** (drag past a threshold; direction snaps to the dominant axis) and
**tap-tap** (tap a cell to select it, tap an adjacent cell to swap, tap the
same cell to deselect). Both were validated together in the concept
prototype ("swipe-and-tap dual input worked with one input model for web +
touch" — `REPORT.md`) and must ship together, not staged, so that switch-
friendly play is never a second-class path. The same gesture grammar and
state machine serve touch and mouse, so the web build behaves identically to
mobile, with mouse hover permitted only as an additive, never-required
affordance.

---

## Player Fantasy

The player should never feel like they are operating an interface — they
should feel like they are directly touching candy. This is the input layer's
contribution to **Pillar 1: Every Swap Sparkles** — before a single particle
or pop sound plays, the *touch itself* must already feel alive: immediate,
forgiving of sloppy or slow input, and never punishing for a gesture the
game "almost" got right. Sweet Cascade's target player (25-45, casual, "zero
tutorial patience" per `game-concept.md`) will not read a tooltip explaining
why a swipe didn't register; the grammar has to be legible on first contact
purely through consistent, instant feedback. A missed or misjudged gesture
should feel like *nothing happened* — safe to simply try again — never like
an accidental wrong move that cost something.

Because the core fantasy hinges on the swap being "the spark that sets off a
chain reaction" (Core Fantasy, `game-concept.md`), any perceptible delay
between touch and visual acknowledgment breaks the spark before the first
candy even pops. Concretely, that means:

| Action | Max Input-to-Response Latency | Frame Budget @ 60fps | Notes |
|--------|-------------------------------|----------------------|-------|
| Tap selects a cell (highlight appears) | ≤16.6ms | ≤1 frame | Highlight render is queued the same frame the tap is classified as a selection |
| Tap deselects (`cancel`) | ≤16.6ms | ≤1 frame | Symmetric with selection so both feel equally instant |
| Swipe crosses threshold → `swap_request` emitted | ≤16.6ms | ≤1 frame | Intent fires the instant the threshold math (Formula 1) resolves true; Input never buffers or debounces its own emission |
| Reselect (tap a third, non-adjacent cell) | ≤16.6ms | ≤1 frame | Old highlight clears and the new one appears in the same frame |

Touch & Input's own contribution to any user-visible response is **always**
≤1 frame by design — it is a stateless-per-frame classifier with no
artificial delay anywhere in its contract. Total end-to-end feel (candies
visibly swapping, popping, cascading) additionally depends on Match-3 Board
Engine and the Juice Layer, which are out of this document's scope (see
Dependencies).

Forgiveness is structural, not cosmetic: every tap and swipe is
deterministically resolved into exactly one of three intents (or none — see
Edge Cases), so "the game didn't get what I meant" is never an unexplained
failure mode. An invalid swap (no match, determined downstream by Board
Engine) still costs the player nothing beyond a brief revert — Input's job is
simply to guarantee the player's *gesture* was read correctly, even when the
resulting *move* wasn't useful.

---

## Detailed Rules

### 1. Gesture Grammar

- **Tap**: pointer/touch down inside a valid cell's hit-area, followed by
  pointer/touch up *before* the swipe threshold (Formula 1) is ever crossed.
  There is no maximum hold duration — a slow, deliberate press is still a
  tap. This is a deliberate accessibility choice for a casual, older-skewing
  audience who may not press-and-release quickly.
- **Swipe**: pointer/touch down inside a valid cell's hit-area, followed by
  movement that crosses the swipe threshold (Formula 1) *before* release.
  The gesture resolves the instant the threshold is crossed — Input does
  not wait for release to decide a swipe occurred, matching the validated
  prototype (`pointermove` resolves the swap directly; `prototype.html`).
- **Drag-cancel**: any gesture that ends before it can be classified — lost
  touch, release outside the viewport, or an interrupting modal/overlay.
  Resolves to no intent, or to `cancel` if a prior selection existed (see
  Edge Cases 1 and 7).
- **Deselect**: an explicit tap on the cell that is currently selected.
  Always resolves to `cancel`.

Grid coordinate convention: row index increases downward (screen y-down),
column index increases rightward (screen x-right) — the standard 2D
screen/grid convention, stated explicitly to remove ambiguity from Formula 2.

### 2. Input State Machine

Touch & Input holds exactly one piece of internal state: whether a cell is
currently selected (tap-tap mode is "armed"). This is purely gesture-
interpretation state — it is **not** board state, and it is not shared with
or owned by the Board Engine.

| State | Trigger | Emitted Intent(s) | Next State |
|-------|---------|--------------------|------------|
| Idle | Tap on cell A | `select_cell(A)` | AwaitingSecond(A) |
| Idle | Swipe from A to a valid in-bounds neighbor B | `swap_request(A, B)` | Idle |
| Idle | Swipe from A, resolved target out of bounds | *(none)* | Idle |
| AwaitingSecond(A) | Tap on A again | `cancel()` | Idle |
| AwaitingSecond(A) | Tap on a cell B adjacent to A (Manhattan distance 1) | `swap_request(A, B)` | Idle |
| AwaitingSecond(A) | Tap on a cell B that is neither A nor adjacent to A | `select_cell(B)` | AwaitingSecond(B) |
| AwaitingSecond(A) | Swipe from any cell C to a valid in-bounds neighbor D | `cancel()`, then `swap_request(C, D)` | Idle |
| AwaitingSecond(A) | Swipe from any cell, resolved target out of bounds | `cancel()` | Idle |
| Any | New gesture begins while `board_input_enabled = false` | *(none)*, or queued — see Rule 4 | unchanged |
| Any | Gesture interrupted/lost mid-flight | `cancel()` if a selection was pending, else none | Idle |
| Any | Board/screen loses focus (pause, overlay, level end) | *(none — no listener to notify)*; local selection is cleared | Idle |

The "swipe from any cell C" rows (not "swipe from A") are a deliberate
production improvement over the concept prototype: in the prototype, a swipe
starting on a *different* tile than the current tap-selection left the old
selection highlighted (`selected` was never cleared by the drag code path).
Production always clears a stale selection the moment *any* swipe resolves,
regardless of where it started, so the highlight can never desync from
reality.

### 3. Input → Intent Contract

- Touch & Input emits exactly three intent types: `select_cell(cell)`,
  `swap_request(cell_a, cell_b)`, `cancel()`.
- `cell`, `cell_a`, `cell_b` are grid coordinates `(row, col)` only — never
  candy IDs, colors, or special-candy flags. Touch & Input has no access to,
  and makes no assumptions about, what occupies a cell.
- `swap_request(cell_a, cell_b)` is emitted **only** for two cells with
  Manhattan distance exactly 1. Input guarantees adjacency and nothing
  else — it never checks color match, special-candy combo legality, move
  availability, or any other board rule. That determination belongs
  entirely to Match-3 Board Engine (systems-index boundary).
- Touch & Input holds no persistent board state. Its only inputs from
  outside itself are: (a) board grid dimensions (`rows`, `cols`) and
  per-cell screen layout, needed for bounds-checking swipe targets (Formula
  2) and hit-testing (Formula 3); and (b) a single `board_input_enabled`
  boolean signal (Rule 4).
- Intents are fire-and-forget events (e.g., a Godot signal per intent type).
  Touch & Input does not wait for or read a return value from the Board
  Engine — whether a `swap_request` succeeds, fails, or triggers a cascade
  is entirely downstream and invisible to this system.
- **Explicitly out of contract**: Touch & Input does not expose a
  continuous drag position stream. It only emits the three discrete,
  resolved intents above. A "candy visually follows the finger before the
  threshold is crossed" juice effect, if ever desired, is future scope for
  the Juice Layer and would require a *separate*, additive raw-position
  signal — not part of this system's MVP contract. Flagged here to prevent
  scope creep being assumed later.

### 4. Input Locking During Cascade Resolution

- The board exposes a single gate boolean Touch & Input's Rule 4 reads
  before accepting any gesture. In production this is the fully-composed
  `effective_board_input_enabled = (base_state==GAMEPLAY) AND
  board_input_enabled AND NOT overlay_is_active AND NOT juice_input_lock`
  (`screen-flow.md` Formula 5, ratified 2026-07-18) — Board Engine's own
  raw busy signal (`board_input_enabled`) is only one of its four
  independently-owned terms; Screen Flow composes `overlay_is_active` and
  Juice Layer composes `juice_input_lock` on top of it. Touch & Input
  itself is unaware of, and does not need to know, which system owns which
  term — it simply reads whatever boolean it is handed and treats it
  identically regardless of source. While that composed value is false
  ("busy": cascade, gravity, refill, or any non-idle simulation state, an
  open overlay, or an in-flight Juice Layer reveal replay), Touch & Input's
  **default MVP behavior is to drop every gesture that starts during that
  window entirely** — no intent, no visual feedback, no queuing. This
  matches the validated concept prototype exactly (`busy` flag gates all
  pointer handlers in `prototype.html`) and is the simplest, safest, most
  testable default: the player never fires an action against board state
  that is still changing underneath them.
- **Tuning-knob variant** (`input_buffer_depth = 1`, **not** the MVP
  default): while busy, Input tracks gestures normally up through producing
  a fully-resolved `swap_request`. Instead of dropping it, the most recent
  fully-resolved `swap_request` is held in a single-slot buffer — any
  earlier buffered request is overwritten, never queued as a growing list.
  The instant `board_input_enabled` becomes true again, the buffered
  `swap_request` is emitted automatically and the buffer is cleared. Bare
  `select_cell`/`cancel` intents are **never** buffered — a selection alone
  has no gameplay value once nothing was actually swapped. This is an
  explicitly experimental "expert flow" affordance for later playtesting
  (see Tuning Knobs); it is off by default because it has not been
  validated and risks a swap firing against board state the player didn't
  see settle.
- A gesture already in progress when `board_input_enabled` flips to false
  is never abandoned mid-flight by this rule: a *new* gesture cannot begin
  while busy (gated at pointer-down), and `board_input_enabled` only
  transitions to false as a *consequence* of a swap this system itself just
  triggered — by which point that triggering gesture has already fully
  resolved. There is no window in which an in-progress gesture is caught by
  a busy transition it didn't cause.

### 5. Touch Ergonomics

- Hit-testing always uses the full board-cell pitch (`cell_size_px`,
  Formula 3), never the smaller visual candy sprite footprint (84% of the
  pitch, per the candy "chip" sizing convention in `design/art/art-bible.md`
  and mirrored in the concept prototype's `.tile .chip { width: 84% }`).
  This guarantees zero dead zones between adjacent candies and keeps the
  tappable region larger than what's visually drawn — satisfying "effective
  target ≥44px" even where the rendered candy art itself is smaller.
- `cell_size_px` must never be allowed to render below `MIN_TOUCH_TARGET_PX`
  (44px, per `.claude/docs/technical-preferences.md`) on any supported
  viewport. This is a hard constraint Touch & Input asserts against at
  runtime (fails loudly in debug builds). Correcting the underlying board
  scale/layout to satisfy it belongs to Match-3 Board Engine's rendering,
  not to Input (see Formula 3 and Dependencies) — Input's job is to
  hit-test correctly against whatever layout it is handed, not to fix that
  layout.
- Touch & Input does not own or constrain *where* the board sits on screen
  (that is a layout/UX decision — the art bible places the board in the
  middle third of the portrait screen for one-handed reach). This system's
  ergonomics responsibility is strictly hit-testing accuracy and target
  sizing within whatever rect it is given.
- **Accidental-touch rejection**:
  1. *Single active touch only* — while a gesture is in progress, any
     additional simultaneous touch points are ignored outright; only the
     first/primary contact drives the active gesture until it resolves or
     is lost.
  2. A gesture may only ever *begin* inside a valid cell's hit region —
     touches starting on HUD chrome, background, or margin never enter the
     board input state machine, enforced by the owning UI node's hit-region
     boundary rather than by re-checking coordinates inside this system.
  3. No dedicated palm/edge heuristic beyond (1) and (2) is implemented.
     The board is laid out away from the physical screen bezel per the art
     bible's HUD density rules, and OS-level edge-swipe gestures are
     consumed by the platform before reaching the app on both export
     targets. This is a deliberate scope boundary, not an oversight —
     inventing custom palm-rejection heuristics would duplicate work the
     OS already does reliably.

### 6. Mouse Parity (Web Build)

- The web build consumes the identical gesture grammar and state machine
  (Rules 1-4) from a single unified pointer input stream — position plus
  phase (began / moved / ended / cancelled) — regardless of whether the
  underlying event originated from touch or mouse. Left mouse button down =
  pointer-down; drag with the button held = pointer-move; button release =
  pointer-up. (The exact Godot input-event wiring — e.g. whether to rely on
  built-in touch/mouse emulation project settings or handle both event
  types explicitly — is an implementation decision for
  `godot-specialist`/`lead-programmer`; this document specifies the
  contract, not the API.)
- Hover (mouse pointer over a cell with no button held) is permitted to
  drive an **additive-only** visual affordance (e.g., a soft highlight
  ring) owned by the presentation/Juice layer. Hover **never** emits an
  intent, never changes state-machine state, and is never required to
  understand or play the game — per `technical-preferences.md`'s "no
  hover-only interactions" rule. The game must be fully playable with zero
  loss of information when hover affordances are absent, as they always
  are on touch devices.
- Right-click, middle-click, and scroll-wheel have no defined behavior in
  this system (out of scope; reserved).

### 7. Accessibility

- Tap-tap is a first-class, always-available input path — never a fallback
  or a settings-gated "accessibility mode." It ships simultaneously with
  swipe, validated turn-for-turn against it in the concept prototype. This
  directly serves players using adaptive switches or other single-tap
  input devices who cannot perform a drag gesture at all.
- Reduced-motion preferences (dampened screen-shake, reduced particle
  bloom) are owned by the Juice Layer / the art bible's proposed
  reduced-motion toggle, not by Touch & Input — this system's own feedback
  (a selection highlight appearing/disappearing) is a simple state toggle,
  not a motion effect, and is unaffected by that setting. Noted here only
  to make the dependency boundary explicit.

---

## Formulas

### Formula 1: Swipe Trigger Threshold

**Named expression:**
```
distance      = sqrt((x_current - x_start)^2 + (y_current - y_start)^2)
threshold_px  = clamp(threshold_ratio * cell_size_px, threshold_min_px, threshold_max_px)
gesture_is_swipe = distance >= threshold_px
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| x_start, y_start | float | canvas-space px | Pointer position at gesture start (pointer-down), in the game's stretched viewport coordinate space |
| x_current, y_current | float | canvas-space px | Current pointer position during an active drag |
| distance | float | 0 – unbounded (practically ≤ screen diagonal) | Euclidean displacement since gesture start |
| cell_size_px | float | > 0 | Rendered edge length of one board cell (full pitch) in canvas-space px at runtime, computed per level from board dimensions + viewport |
| threshold_ratio | float | 0.20 – 0.50 | Fraction of one cell's edge length a drag must travel before being classified as a swipe (tuning knob) |
| threshold_min_px | float | 16 – 32 | Floor clamp on the threshold, in canvas-space px |
| threshold_max_px | float | 40 – 90 | Ceiling clamp on the threshold, in canvas-space px |
| threshold_px | float | [threshold_min_px, threshold_max_px] | The actual distance a drag must exceed for this level/viewport |
| gesture_is_swipe | bool | {true, false} | Output — true the first frame `distance` reaches or exceeds `threshold_px` |

**Output range**: `threshold_px` is always clamped to
`[threshold_min_px, threshold_max_px]`, independent of screen DPI or board
grid size — this is the production fix for the concept prototype's fixed
24px constant, which was tuned against one small HTML demo board and is not
resolution-independent. Expressing the threshold as a *ratio of the actual
rendered cell pitch* (clamped to sane absolute bounds) keeps swipe feel
consistent whether the board renders at 90px or 150px cells, and across
different device pixel densities, as long as hit-testing and drag tracking
both happen in the same canvas-space coordinate system (Godot's stretched
viewport coordinates, not raw OS pixels).

**Worked example**: 8×8 board, `cell_size_px = 130` (consistent with the art
bible's ~90–130px candy render size at 84% fill on a 1080px-wide reference
canvas), `threshold_ratio = 0.35` → raw = `0.35 * 130 = 45.5`. Clamped to
`[24, 60]` → unchanged. `threshold_px ≈ 45.5px`. A drag reaching 46px of
displacement in any direction sets `gesture_is_swipe = true` on that frame.
*(Cross-check: the prototype's fixed 24px threshold against its ~46–60px
CSS-pixel cells implied a felt ratio of roughly 0.40–0.52 — the production
default of 0.35 sits just below that range, erring slightly toward
swipe-friendliness; treat as a starting point pending in-engine feel
validation, per `REPORT.md`'s explicit flag that HTML feel must be
re-validated in Godot.)*

**Implementation note (non-normative)**: to avoid a per-frame `sqrt` call
during an active drag, compare squared distance against squared threshold —
`(x_current-x_start)^2 + (y_current-y_start)^2 >= threshold_px^2` — which is
mathematically identical and cheaper on mobile hardware. This is a
performance suggestion for implementation, not part of the design contract.

---

### Formula 2: Dominant-Axis Resolution

**Named expression:**
```
dx = x_current - x_start
dy = y_current - y_start        (measured at the instant gesture_is_swipe becomes true)

axis = horizontal, if |dx| > |dy|
       vertical,   otherwise                      (tie-break: |dx| == |dy| resolves to vertical)

(delta_row, delta_col) = (0, sign(dx))  if axis = horizontal
                          (sign(dy), 0)  if axis = vertical

target = (row_origin + delta_row, col_origin + delta_col)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| dx, dy | float | unbounded (typically ± screen width/height) | Canvas-space displacement components at the moment the swipe threshold is crossed |
| axis | enum | {horizontal, vertical} | Which grid axis the swipe commits to |
| delta_row, delta_col | int | {-1, 0, 1} | Single-step grid offset from the origin cell; exactly one of the pair is non-zero |
| row_origin, col_origin | int | [0, board_rows-1], [0, board_cols-1] | Grid coordinates of the cell where the gesture began |
| target | (int, int) | grid coordinates | The 4-directional neighbor nominated as the swap partner; may fall outside board bounds (see Edge Cases) |

**Output range**: `target` is always exactly one of the origin cell's four
orthogonal (N/S/E/W) neighbors — diagonal targets are structurally
impossible, since exactly one of `delta_row`/`delta_col` is ever non-zero.
`target` may be out-of-bounds at board edges; that is a defined edge case
(no `swap_request` emitted), not a formula failure.

**Worked example**: origin cell `(row_origin=3, col_origin=4)`. Pointer
moves from `(x_start=500, y_start=800)` to `(x_current=475, y_current=840)`
at the exact frame `distance` first reaches `threshold_px` (≈45.5px per
Formula 1's worked example; check: `sqrt(25² + 40²) = sqrt(2225) ≈ 47.2px ≥
45.5px` ✓). `dx = 475-500 = -25`, `dy = 840-800 = 40`. `|dx|=25`, `|dy|=40`
→ `|dx| > |dy|` is false → `axis = vertical`. `delta_row = sign(40) = +1`,
`delta_col = 0`. `target = (3+1, 4+0) = (4, 4)` — one row down from origin,
consistent with the perceived downward swipe direction.

---

### Formula 3: Effective Touch Target / Hit-Area Resolution

**Named expression:**
```
hit_size_px       = cell_size_px                                (always the full layout pitch)
visual_size_px    = cell_size_px * visual_fill_ratio             (the drawn candy sprite)
hit_expansion_px  = hit_size_px - visual_size_px                 (extra tappable margin, both sides combined)

constraint: cell_size_px >= MIN_TOUCH_TARGET_PX   (must hold for every supported viewport)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| cell_size_px | float | > 0 | Computed layout pitch of one board cell at runtime for the current viewport/board size |
| visual_fill_ratio | float | 0 – 1 (currently 0.84, set by `design/art/art-bible.md` chip sizing, not owned by this system) | Fraction of the cell pitch the candy sprite visually occupies, leaving a gutter so adjacent candies never touch |
| visual_size_px | float | derived, px | On-screen rendered candy size |
| hit_size_px | float | derived (= cell_size_px), px | The actual tappable region edge length used for hit-testing — always the full pitch, never the shrunk sprite |
| hit_expansion_px | float | ≥ 0, px | How much larger the tap region is than the visible candy, combined across both sides (per-side expansion = `hit_expansion_px / 2`) |
| MIN_TOUCH_TARGET_PX | float constant | 44 (locked, per `technical-preferences.md`) | The hard floor for any interactive element's effective size |

**Output range**: `hit_size_px` must be ≥ `MIN_TOUCH_TARGET_PX` (44px) at
all times — this is a hard floor, not a soft target. `hit_expansion_px` is
always ≥ 0 given `visual_fill_ratio ∈ (0, 1]`.

**Worked example**: `cell_size_px = 130px` (8×8 board, 1080px-wide
reference canvas), `visual_fill_ratio = 0.84` → `visual_size_px = 109.2px`,
`hit_size_px = 130px` (unchanged — full pitch), `hit_expansion_px = 20.8px`
(≈10.4px extra tappable margin per side beyond the visible candy). The
constraint `130 ≥ 44` holds with comfortable headroom. **Failure-case
example**: a hypothetical dense grid where `cell_size_px` computes to
`40px` on some viewport — the constraint `40 ≥ 44` fails; per Rule 5, this
is asserted in debug builds and must be resolved by increasing overall
board scale (letterboxing/margin reduction), never by shrinking the hit
grid below the floor. This scenario is not expected on the MVP's fixed 8×8
board but is guarded against for any future grid-size variation Level Data
Format may introduce.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|--------------------|-----------|
| Swipe resolves to a target cell outside board bounds | No `swap_request` emitted; any pending selection is cleared via `cancel()`; state returns to Idle | Prevents dead/stuck input; closes a stale-highlight gap present in the concept prototype |
| Swipe is exactly diagonal (`\|dx\| == \|dy\|` at the instant the threshold is crossed) | Tie-break resolves to the vertical axis (Formula 2) | Deterministic, unit-testable default; matches the validated concept prototype; true float-exact ties are vanishingly rare in practice |
| Tap lands on the currently selected cell | Emits `cancel()`; no swap attempted | Gives the player a clear "undo my selection" affordance with no dedicated cancel button — essential for a zero-tutorial-patience audience |
| Tap lands on a cell that is neither the current selection nor 4-directionally adjacent to it | Emits `select_cell(new_cell)`, replacing the prior selection; no swap is ever attempted between non-adjacent cells | Forgiving "change my mind" behavior; Input never lets an obviously-illegal far-apart swap reach the Board Engine |
| Gesture starts while `board_input_enabled = false` and `input_buffer_depth = 0` (MVP default) | Gesture never enters the state machine — no intent, no state change, no visual feedback | Matches the validated prototype; simplest and safest to reason about and unit-test |
| Gesture starts while `board_input_enabled = false` and `input_buffer_depth = 1` (tuning-knob variant) | Only a fully-resolved `swap_request` is buffered (single slot, overwritten by any later one during the same busy window); bare taps/selections are never buffered; the buffered swap auto-emits the instant `board_input_enabled` returns true | Experimental expert-flow affordance; off by default and requires its own playtest before enabling |
| Pointer/touch is lost mid-gesture (OS touch-cancel, notification pull-down, app loses focus, mouse released outside the browser viewport) | If no threshold had been crossed and no prior selection existed, nothing is emitted; if a selection was pending, `cancel()` is emitted; state always returns to Idle | Mobile touch cancellation is common; the state machine must never get stuck in a non-Idle state |
| A second simultaneous touch point appears while a gesture is already in progress | Ignored entirely; only the first/primary contact drives the active gesture until it resolves or is lost | Sweet Cascade is a single-finger interaction; prevents an off-hand brush during one-handed portrait play from corrupting a gesture |
| Gesture pointer-down originates outside any board cell's hit region (HUD, background, margin) | Never enters the board input state machine; no intent emitted | Enforced by the owning UI node's hit-region boundary, not by re-checking coordinates inside this system |
| Pause menu, results overlay, or any modal opens mid-gesture | Any in-progress gesture is cancelled immediately (as above); state machine resets to Idle | Prevents stale drag/selection state from leaking behind or across screens |
| Computed `cell_size_px` for the current level/viewport falls below `MIN_TOUCH_TARGET_PX` (44px) | Formula 3's constraint fails; Touch & Input asserts/logs the violation in debug builds but does not itself resize the board | Layout/scale correction belongs to Match-3 Board Engine's rendering, not to Input |
| Web build: mouse hover with no button pressed | Never emits an intent or changes state-machine state under any circumstance; may only drive an additive, non-required visual affordance owned by the presentation layer | Enforces "no hover-only interactions" (`technical-preferences.md`) |

---

## Dependencies

| System | Direction | Nature of Dependency |
|--------|-----------|-----------------------|
| Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED) | Mutual | Consumes `select_cell`, `swap_request(cell_a, cell_b)`, and `cancel` as the sole trigger for any board mutation; owns all swap validation, match detection, and simulation state, and owns/drives the `board_input_enabled` busy signal this document reads. **Reciprocal note fulfilled** — `board-engine.md`'s own Dependencies section lists this document and confirms how it consumes these three intents. Board Engine is also the authoritative computed source of `cell_size_px` (its Formula 4) — this document's Formulas 1 and 3 consume it only as an externally-supplied runtime value, never computing it themselves. |
| Level Data Format (`design/gdd/level-data-format.md`) | This depends on it (data only) | Supplies board grid dimensions needed for bounds-checking swipe targets (Formula 2) and computing `cell_size_px` (Formulas 1 and 3) at level load — Level Data Format's `grid_height` maps to this document's `board_rows`, and `grid_width` maps to `board_cols` (row-major convention, §1). Touch & Input reads dimensions only — never objective, blocker, or candy-palette data. |
| Game UI/Screens Flow (`design/gdd/screen-flow.md`, not yet authored) | Soft runtime dependency | Expected to co-own (alongside Board Engine) toggling `board_input_enabled` false during pause menus, results overlays, and any modal (Rule 4, Edge Case: modal mid-gesture). **Reciprocal note**: when `screen-flow.md` is authored, its Dependencies section must list this document and the `board_input_enabled` contract. |
| Juice Layer — VFX & Audio Hooks (`design/gdd/juice-layer.md`, APPROVED) | Not a dependency — boundary note only | Owns reduced-motion preference behavior (dampened screen-shake, particle bloom) per this document's own § 7; Touch & Input's selection-highlight feedback is a simple state toggle, not a motion effect, and is unaffected by that setting. Listed here only to make the dependency boundary explicit. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | This depends on it (constant only) | Supplies `visual_fill_ratio` (0.84, candy chip sizing) consumed in Formula 3. |
| `.claude/docs/technical-preferences.md` (not a `design/gdd/` system) | This depends on it (constant + rule only) | Supplies `MIN_TOUCH_TARGET_PX` (44px, hard floor) and the "no hover-only interactions" rule enforced in Rule 6. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|---------------|------------|---------------------|----------------------|
| `threshold_ratio` | 0.35 (35% of cell pitch) | 0.20 – 0.50 | Swipes need more travel to register — fewer accidental swipes from a jittery tap, but dragging can start to feel "sticky" or unresponsive | Swipes trigger on very short movement — feels snappier, but taps risk being misread as swipes |
| `threshold_min_px` | 24px | 16 – 32px | Protects against over-sensitive swipe detection on small/dense grids; too high can make dense-grid swipes feel unresponsive | Risk of hand-tremor or accidental micro-movement during a tap being misread as a swipe on small cells |
| `threshold_max_px` | 60px | 40 – 90px | Guards against unresponsive-feeling swipes on very large cells; too high noticeably delays swipe recognition | Can clip `threshold_ratio`'s natural output on large cells, forcing swipes to fire earlier than the ratio alone would produce |
| `input_buffer_depth` | 0 (off — drop input while busy; MVP default, validated) | {0, 1} | 1 = buffer one resolved swap for expert/rapid play; adds perceived responsiveness but risks a swap firing against board state the player didn't see settle — unvalidated, needs its own playtest | N/A — 0 is already the minimum and the MVP-validated default |
| `MIN_TOUCH_TARGET_PX` | 44px (locked floor, `technical-preferences.md`) | 44 – 56px | Extra-forgiving targets for accessibility, at the cost of more adjacent-cell mis-taps on dense boards | **Not permitted below 44px** — hard floor, not a tunable-down value |

---

## Acceptance Criteria

**Unit-testable — state machine / intent emission** (`tests/unit/input/`,
BLOCKING per testing standards; deterministic synthetic pointer-event
sequences, no RNG or timers required):

- [ ] Given a pointer-down + pointer-up on cell A with total travel <
      `threshold_px` and no prior selection, exactly one intent is emitted:
      `select_cell(A)`.
- [ ] Given the same sequence again on cell A (state = AwaitingSecond(A)),
      exactly one intent is emitted: `cancel()`.
- [ ] Given `select_cell(A)` then a tap on adjacent cell B (Manhattan
      distance 1), exactly one intent is emitted: `swap_request(A, B)`, and
      internal state returns to Idle.
- [ ] Given `select_cell(A)` then a tap on non-adjacent cell C (distance >
      1, C ≠ A), exactly one intent is emitted: `select_cell(C)`, and state
      becomes AwaitingSecond(C).
- [ ] Given a pointer-down on cell A followed by movement exceeding
      `threshold_px` with `|dx| > |dy|`, the emitted `swap_request` targets
      the horizontal neighbor in the `sign(dx)` direction; with `|dy| >=
      |dx|` (including the exact-tie case `dx == dy`), it targets the
      vertical neighbor in the `sign(dy)` direction.
- [ ] Given a swipe whose resolved target falls outside `[0, rows) ×
      [0, cols)`, no `swap_request` is emitted, and any pending selection
      is cleared via `cancel()`.
- [ ] Given `board_input_enabled = false` at pointer-down and
      `input_buffer_depth = 0` (default), no intent is ever emitted for
      that gesture.
- [ ] Given `board_input_enabled = false`, `input_buffer_depth = 1`, and a
      gesture that fully resolves into a `swap_request` while busy, the
      intent is emitted exactly once, exactly on the frame
      `board_input_enabled` transitions back to true — and a second such
      gesture during the same busy window replaces (does not queue
      alongside) the first.
- [ ] `swap_request` is never emitted for two cells with Manhattan distance
      ≠ 1, for any input sequence.
- [ ] Formulas 1, 2, and 3 each reproduce the documented worked-example
      outputs given their documented worked-example inputs (regression-pin
      the numbers in this GDD).

**Manual walkthrough — UI/feel** (`production/qa/evidence/`, ADVISORY per
testing standards):

- [ ] On a physical touch device, tap-tap alone (no swiping at all)
      completes a full level start to finish.
- [ ] On a physical touch device, swipe-to-swap alone (no tapping-to-select)
      completes a full level start to finish.
- [ ] Selection highlight visibly appears within one visible frame of a
      tap; no perceptible delay is reported by the tester.
- [ ] On the web build, mouse click-drag-release reproduces identical swap
      behavior to a touch swipe of equivalent travel distance, and mouse
      hover with no click never changes game state.
- [ ] Rapidly tapping/swiping during an active cascade produces zero
      visible glitches, stuck highlights, or double-fired swaps (validates
      Rule 4's default drop-while-busy behavior).
- [ ] Every board cell hit-tests correctly out to its full pitch, including
      the gutter area between visually adjacent candies (no dead zones),
      verified by tapping near cell boundaries across at least 3 cell
      pairs.

**Performance and data-driven compliance**:

- [ ] Gesture classification and intent emission add no more than 0.1ms of
      frame time on mid-range mobile target hardware — well inside the
      16.6ms/60fps budget (`technical-preferences.md`) — verified by
      profiling during an active drag.
- [ ] No hardcoded values: `threshold_ratio`, `threshold_min_px`,
      `threshold_max_px`, `input_buffer_depth`, and `MIN_TOUCH_TARGET_PX`
      are all sourced from data-driven config, not literals in code (per
      `.claude/docs/coding-standards.md`).
