# Juice Layer — VFX & Audio Hooks

*Status: Draft — awaiting /design-review*
*Created: 2026-07-18*
*Last Updated: 2026-07-18*
*Layer: Presentation · Priority: MVP · Phase: MVP · Category: Feel*
*Author: systems-designer*
*Depends On: Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED), Special
Candies & Combo Matrix (`design/gdd/special-candies.md`, not yet authored — forward
dependency), Scoring & Star Thresholds (`design/gdd/scoring-stars.md`, not yet
authored — forward dependency), Touch & Input System (`design/gdd/touch-input.md`,
APPROVED), Save & Persistence (`design/gdd/save-persistence.md`, Draft), Game
UI/Screens Flow (`design/gdd/screen-flow.md`, Draft)*
*Depended On By: Game UI/Screens Flow (Draft — hosts the Results Win screen this
document's star-ceremony content plays on; recommended consumer of this
document's `juice_input_lock` signal), Booster Brewing Meta (Phase 2, forward —
ingredient-harvest VFX seam), Events/Theming Engine (Phase 3, forward — region
ambient theme seam)*
*Source: `design/gdd/systems-index.md` · `design/gdd/board-engine.md` (APPROVED) ·
`design/art/art-bible.md` · `design/gdd/game-concept.md` ·
`prototypes/sweet-cascade-concept/REPORT.md` · `design/gdd/screen-flow.md` (Draft) ·
`design/gdd/touch-input.md` (APPROVED) · `.claude/docs/technical-preferences.md` ·
`design/gdd/save-persistence.md` (Draft) · `design/narrative/characters-and-tone.md`*

---

## Overview

The Juice Layer is the Presentation-layer system that turns Match-3 Board
Engine's synchronous, already-resolved event stream into everything the
player actually perceives about a swap: the slide, the pop, the fall, the
escalating cascade callouts, the sound, and the buzz in their hand. Board
Engine resolves an entire move — every cascade step, all the way to a
settled board — in a single logic frame with zero artificial delay
(`board-engine.md` § Detailed Rules 13); this document owns the *opposite*
half of that contract: pacing the *reveal* of a move Board Engine already
knows in full, at a tempo tuned for legibility and feel rather than logic
speed. Concretely, this document owns the replay scheduler and its
acceleration curve for long cascades, the full feedback vocabulary mapping
every Board Engine signal to a visual/audio/haptic response, the audio asset
spec sound-designer builds against, the haptics map and its global toggle,
the reduced-motion/accessibility mode, and the performance guardrails
(particle budgets, draw-call ceiling, LOD degradation ladder) that keep a
worst-case cascade inside the project's frame budget. It does not decide
*what* happened (Board Engine), *how many points* a match is worth
(Scoring & Star Thresholds), *what a special candy does* (Special Candies &
Combo Matrix), or *which screen the player is on* (Game UI/Screens Flow) —
only how those already-decided facts are revealed.

---

## Player Fantasy

Per `game-concept.md`'s MDA priority table, **Sensation is the #1 target
aesthetic** for Sweet Cascade — juicy cascade VFX, satisfying pop sounds, and
haptics are named first, ahead of Challenge, Submission, Fellowship,
Discovery, Fantasy, and Expression. This document exists to deliver that
priority mechanically. The one-line fantasy: **the board applauds you.**
Every response this system produces must feel like a *reaction* to
something the player earned, never like noise the game is generating on its
own initiative — this is Pillar 1 (Every Swap Sparkles) expressed through
Pillar 2's lens (Clever, Never Cheated): juice amplifies earned moments, it
never manufactures excitement the board's actual state doesn't support
(`art-bible.md`'s "Color tells the truth" principle, applied to VFX: "if a
VFX or UI color would make a player believe something happened that didn't
... cut it").

Concretely, this system exists to protect four guarantees:

1. **Every swap gets an instant, honest acknowledgment.** The moment a
   player's finger leaves the screen, something visible happens — the
   optimistic slide begins the instant `swap_started` fires, before Board
   Engine has even reported whether the swap was valid (`board-engine.md`'s
   own Player Fantasy explicitly calls this out as the mechanism that makes
   an optimistic slide possible). A missed match still costs the player
   nothing beyond a same-weight, non-punishing revert — never a buzz, alarm,
   or "wrong" cue (`characters-and-tone.md`'s "never punishing" voice pillar,
   extended here from copy into sound and haptics).
2. **Feedback intensity is proportional, never manufactured.** A plain
   match-3 pop is quiet and quick. A four-link cascade gets a screen pulse,
   an escalating callout, and a pitch-raised pop. A bomb detonation gets the
   one hero-frame-animation budget item at MVP. The player should be able to
   *feel* how big a moment is without reading a number, and that feeling
   must always correspond to something Board Engine's own `chain_index`,
   `trigger_source`, and `special_type` data actually says happened — this
   document invents zero drama the underlying event stream doesn't support.
3. **The player is never locked out longer than the moment deserves.** A
   deep cascade is exciting, not a wait. The replay scheduler's acceleration
   curve (Formulas) guarantees a long chain's *pacing* tightens as it goes,
   so a ten-link cascade never drags at the same tempo as its first link,
   while every individual pop still gets its full, unhurried visual quality
   — nothing about the deceleration compromises what any single pop looks
   like.
4. **The core feedback loop survives accessibility settings intact.** A
   player with `reduced_motion_enabled` on still gets the particle-burst pop
   that tells them a match landed — they lose screen-shake and bloom, never
   the core "yes, that happened" signal (`art-bible.md` Accessibility:
   "preserving the core particle-burst feedback").

---

## Detailed Rules

### 1. Ownership Boundary & the Deferred-Replay Consumption Model

The Juice Layer is a **read-only subscriber** to Board Engine's signal
catalog (and, once authored, Special Candies' and Scoring's own forward
signals). It never calls back into Board Engine, never queries board state
to *decide* anything gameplay-relevant, and never mutates grid state — its
only outputs are rendered visuals, played audio, fired haptic pulses, and
one presentation-owned boolean signal (`juice_input_lock_changed`, § 10).
This is the direct implementation of `board-engine.md` § Detailed Rules 13's
closing statement: *"logic resolves instantly; presentation paces
reveals."*

Board Engine resolves an entire triggering event (a swap, a special
activation, or bootstrap) synchronously and emits its **full, ordered
signal burst for that move in one logic frame** — potentially many
`match_cleared`/`cascade_step_advanced` events back to back, faster than any
human could perceive them. The Juice Layer's first and most fundamental job
is to **capture that whole burst atomically** the instant it arrives, then
**replay it across many subsequent rendered frames** at a deliberately
slower, tuned tempo. Every downstream rule in this document — the Shadow
Board Model (§ 2), the Reveal Queue (§ 3), the acceleration curve
(Formula 1) — exists to make that capture-then-replay model concrete and
testable.

Because Board Engine's own `board_input_enabled` flag flips back to `true`
**synchronously, within the same logic frame**, the instant its resolution
loop reaches `Idle` — which happens *before* the Juice Layer has even begun
replaying a single frame of that move's visuals — this document also owns a
**second, independent input-lock signal** covering the gap between "Board
Engine is logically done" and "the player has visually seen the board
settle." This is formalized in § 10 and Formula 5; it is the mechanism the
task brief's "input locked during resolution replay" requirement resolves
to.

### 2. The Shadow Board Model

Because Board Engine's own board-state query API (`get_piece_at()`,
`get_grid_dimensions()`, etc., `board-engine.md` § Detailed Rules 8) is
**live** — it reflects the board's *current* state, not the historical
state at the instant of an event the Juice Layer may only be getting around
to presenting several seconds later, mid-replay — the Juice Layer never uses
those live queries to render a historical event. Instead, it maintains its
own **Shadow Board Model**: a presentation-side mirror of grid contents,
built and kept current **entirely from the subscribed event stream**, never
from a live query, with one narrow, deliberate exception (reshuffle, below).

**Construction.** The Shadow Board Model is initialized once per level
entry from `board_bootstrapped` (grid dimensions, `cell_mask`) and the
following `pieces_spawned(source=BOOTSTRAP)` event, whose `pieces` array
gives every playable cell's starting `(color, special_type)` — this is
exactly the payload-sufficiency guarantee `board-engine.md` § Detailed
Rules 13 describes, generalized here from "a per-color tally" to "the full
grid." From that point forward, every `match_cleared.cleared_pieces` entry
removes a cell from the shadow grid, every `special_spawned` and
`pieces_spawned` entry adds one, in the exact order those signals arrive.
The shadow grid is therefore always an exact, independently-derived replica
of what Board Engine's own grid looked like at each instant an event fired
— with zero live queries required for any of it.

**Gravity re-derivation.** Board Engine's signal catalog reports which
cells were *cleared* (`match_cleared`) and which cells received a *new*
piece via refill (`pieces_spawned`), but it does not emit a dedicated
"these surviving pieces moved from A to B" signal. The Juice Layer does not
need one: gravity compaction is a fully deterministic, already-published
algorithm (`board-engine.md` § Detailed Rules 8–9 — pieces settle toward
their own column segment's bottom row, stopping at a `VOID`, never falling
through; segments are read once at bootstrap from `cell_mask` and never
change mid-level, § Formula 3 there). The Juice Layer re-runs that identical
algorithm against its own shadow grid to compute exactly which surviving
piece animates from which old cell to which new cell within a cleared
step's affected segments — this requires no new Board Engine signal, only
independently applying a rule Board Engine already fully specifies.

**Reshuffle — the one live-query exception.** `board_reshuffled`'s payload
is `attempts_used: int` only — it does not carry per-cell reassignment data
(flagged as an explicit Open Question in `board-engine.md`, addressed and
resolved here — see Cross-References). Per Board Engine's own documented
ordering guarantee, `board_reshuffled` is always the **last board-mutating
event** before `board_stabilized`/`board_bootstrapped` fires — nothing else
changes grid state in between. Combined with this document's own input-lock
policy (§ 10), which guarantees no new player-triggered move can begin
until the Juice Layer's replay of the *current* move fully completes, this
means that by the time the Juice Layer's Reveal Queue (§ 3) actually reaches
its `RESHUFFLE_REVEAL` step, the **live** board state is still guaranteed to
be exactly the reshuffled state — nothing has had the opportunity to change
it since. The Juice Layer therefore performs one synchronous
`get_piece_at()` sweep at that specific moment, folds the result into its
shadow grid, and continues from there. This is a narrow, justified exception
to the "never query live state" rule, not a weakening of it — see
Cross-References for the full resolution and its one open contingency.

### 3. The Reveal Queue & Replay Scheduler

Every triggering event (a swap, a special activation, or bootstrap) produces
exactly one **Reveal Queue**: an ordered list of **Reveal Steps** the Juice
Layer derives from that move's captured signal burst (§ 1) and then drains
one at a time, at the tempo Formula 1/2 define. Only one Reveal Queue is
ever active at a time — because input stays locked for its full duration
(§ 10), Board Engine can never produce a second move's signal burst before
the first Queue finishes draining, so the Juice Layer never needs to merge,
interleave, or prioritize between two in-flight queues at MVP.

| Reveal Step | Derived From (Board Engine Signal(s)) | Presented When |
|---|---|---|
| `BOOTSTRAP_OPEN_REVEAL` | `pieces_spawned(source=BOOTSTRAP)` | Replaces `SWAP_REVEAL` as the first step, only for a level's opening batch |
| `SWAP_REVEAL` | `swap_started`, then `swap_accepted` or `swap_rejected` | Always first, for any player-triggered move |
| `ACTIVATION_REVEAL` | `special_activated` | Only present when `trigger_source = SPECIAL_ACTIVATION`; inserted after `SWAP_REVEAL`, before the first `CLEAR_REVEAL` |
| `CLEAR_REVEAL(i)` | `match_cleared(chain_index=i)` + any `special_spawned` entries for step `i` | Once per cascade step |
| `FALL_REVEAL(i)` | Derived — Shadow Board Model's gravity re-derivation (§ 2) for step `i`'s affected segments | Immediately after `CLEAR_REVEAL(i)` |
| `REFILL_REVEAL(i)` | `pieces_spawned(source=CASCADE_REFILL)` for step `i` | Immediately after `FALL_REVEAL(i)`, same step |
| `MILESTONE_REVEAL(i)` | `cascade_step_advanced(chain_index=i)`, `i ≥ 2` | Overlaid alongside `CLEAR_REVEAL(i)`, **only** when `trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}` — never for `BOOTSTRAP` (§ 5) |
| `RESHUFFLE_REVEAL` | `no_valid_moves_detected` → `board_reshuffled` | Only present when Board Engine's Reshuffling state fired this move; inserted after the final cascade step's `REFILL_REVEAL`, before `SETTLE_REVEAL` |
| `SETTLE_REVEAL` | `board_stabilized` (or `board_bootstrapped`, for the opening batch) | Always last; its completion clears `juice_input_lock` (§ 10, Formula 5) |

This table is a direct, one-to-one translation of the two ordering
guarantees Board Engine's own § Detailed Rules 7 already publishes verbatim
(the special-activation ordering guarantee and the bootstrap ordering
guarantee) — the Juice Layer trusts those guarantees unconditionally and
never re-derives or second-guesses signal order itself.

**Suspension.** If Screen Flow suspends gameplay mid-replay (an app
background or Pause tap, `screen-flow.md` T9/T8), the currently-queued
Reveal Queue is **fast-forwarded to completion** rather than paused
mid-animation: remaining `inter_step_beat_ms` and in-flight tween durations
are skipped, and every remaining step's end-state is applied immediately.
This mirrors `screen-flow.md`'s own Edge Case policy for a transition
interrupted mid-animation ("the transition completes instantly to its end
composite state rather than resuming mid-animation on foreground return")
and guarantees the player is never shown a half-settled board, whether they
return from backgrounding or resume from Pause.

### 4. Feedback Vocabulary Per Signal

The canonical mapping from a captured Board Engine signal to its Visual /
Audio / Haptic response. Audio event names are defined in full in § 6;
haptic patterns in § 7. Timing values referenced by name are defined in
Tuning Knobs and derived in the Formulas section.

| Signal / Reveal Step | Visual | Audio Event | Haptic | Duration Source |
|---|---|---|---|---|
| `swap_started` (`SWAP_REVEAL` begins) | Optimistic slide begins immediately — both pieces tween to each other's cell | `audio_swap_slide` | *(none — fires on resolution, not the tentative slide)* | `SWAP_SLIDE_DURATION_MS` |
| `swap_accepted` | Slide completes cleanly; no shake | `audio_swap_accept` (soft confirm, may layer under the first pop) | `haptic_swap_success` | *(marker into `CLEAR_REVEAL(1)`)* |
| `swap_rejected` | Slide completes, then a soft shake-wobble, then both pieces slide back to origin | `audio_swap_revert` (deliberately soft — never a buzzer/error tone) | *(none — zero penalty)* | `SWAP_REVERT_SHAKE_MS` |
| `special_activated` | Activation flourish plays at `piece_a`/`piece_b` before `cleared_pieces` pop | `audio_special_activate_stripe` or `audio_special_activate_bomb` (by activating piece's `special_type` family) | `haptic_special_medium` (stripe) / `haptic_special_strong` (bomb) | Folds into `ACTIVATION_REVEAL`, feeds `CLEAR_REVEAL(1)` |
| `match_cleared`, `chain_index = 1` | Squash-pop + recolorable burst particles per `cleared_pieces` (art bible Match/Pop VFX) | `audio_pop_base` at base pitch (Formula 3, `chain_index=1` → 0 semitones) | *(none — reserved for milestone steps, § 7)* | `MATCH_POP_DURATION_MS` |
| `match_cleared`, `chain_index ≥ 2` | Pop + burst (particle count scaled per Formula 4) + milestone callout overlay (§ 5) when `trigger_source ≠ BOOTSTRAP`; screen-color pulse when `chain_index ≥ BIG_CASCADE_CHAIN_THRESHOLD` | `audio_pop_base` pitch-shifted per Formula 3 + a milestone-tier chime at tier breakpoints (§ 5, § 6) | `haptic_cascade_tick` (only at milestone steps, § 7) | `MATCH_POP_DURATION_MS` + milestone overlay |
| `special_spawned` | Transform flourish — the exempted cell glows/shimmers in place rather than popping (this cell was never cleared) | `audio_special_spawn_stripe` / `audio_special_spawn_bomb` | `haptic_special_spawn` | `SPECIAL_SPAWN_FLOURISH_MS` |
| `pieces_spawned(source=CASCADE_REFILL)` (`FALL_REVEAL` + `REFILL_REVEAL`) | Surviving pieces (Shadow Board Model, § 2) drop with ease-out + landing bounce; newly-refilled pieces enter from their segment's top, staggered per column | `audio_refill_drop` (soft, staggered per column) | *(none)* | `fall_reveal_ms(distance)`, Formula 2 |
| `pieces_spawned(source=BOOTSTRAP)` (`BOOTSTRAP_OPEN_REVEAL`) | Whole-board staggered drop-in, column by column | `audio_bootstrap_drop` (one sweeping cue, not per-piece) | *(none)* | `BOOTSTRAP_STAGGER_PER_COLUMN_MS` × column count, capped (Tuning Knobs) |
| `cascade_step_advanced` | *(no independent visual — paired with the following `match_cleared` as the same reveal step)* | *(none independent)* | *(none independent)* | — |
| `cascade_ended` | *(queue proceeds toward `RESHUFFLE_REVEAL` or `SETTLE_REVEAL`; no additional visual)* | `audio_cascade_finale` only if `final_chain_index ≥ BIG_CASCADE_CHAIN_THRESHOLD` (optional embellishment) | *(none)* | — |
| `no_valid_moves_detected` | Brief pre-beat hush before the reshuffle swirl begins | `audio_reshuffle_cue` (short anticipatory intake) | *(none)* | Folds into `RESHUFFLE_REVEAL` |
| `board_reshuffled` | Readable swirl/scramble: affected pieces scale down, orbit briefly, scale back up in their new (live-queried, § 2) positions | `audio_reshuffle_resolve` (whoosh-settle) | *(none, or a single very soft tick — Tuning Knob toggle)* | `RESHUFFLE_ANIMATION_MS` |
| `board_stabilized` / `board_bootstrapped` (`SETTLE_REVEAL`) | A brief still-beat; no additional full-board flash or pulse (avoids competing with milestone pulses and flash-safety limits, § 8) | *(none — silence is the cue)* | *(none, unless this move ends the level — see § 11 star-ceremony seam)* | `BOARD_SETTLE_BEAT_MS` — `juice_input_lock` clears at the end of this step |

**Generic UI chrome** (Play, Retry, Pause, Settings, Map, and every other
button Screen Flow triggers) is out of the board-event vocabulary above but
still within this document's audio/haptic asset scope: every button press
plays `audio_ui_tap` and fires `haptic_ui_light`, regardless of which screen
hosts it — a single, consistent, reused pair per `art-bible.md`'s consistent
button language, never authored per-screen.

**Explicitly out of scope.** The move-counter's low-moves amber pulse
(`art-bible.md`: "Subtle amber pulse ... on the move counter only — never
the board itself") is HUD chrome owned by Level Objective & Move-Limit
System / Game UI/Screens Flow, not a board-event response — the Juice Layer
never touches it.

### 5. Cascade Milestone Callout Ladder

The concept prototype's validated escalating callouts ("Sweet! ×2 →
Delicious! ×4", `REPORT.md`) are the direct ancestor of this section. The
Juice Layer owns the **tier structure, thresholds, and timing/presentation**
of this ladder; the **exact copy** is a forward seam for `narrative-director`
to author against `characters-and-tone.md`'s voice rules (one-liners,
≤60 characters, no em dashes/semicolons/colons, earned exclamation points,
second person). Until that pool exists, this document ships with the
prototype-validated placeholder strings below as a safe MVP default.

**Trigger rule.** A milestone callout fires only for `MILESTONE_REVEAL`
steps (§ 3): `chain_index ≥ 2` **and** `trigger_source ∈ {SWAP_MATCH,
SPECIAL_ACTIVATION}`. A `BOOTSTRAP`-sourced cascade (§ Detailed Rules 2 and
8, `board-engine.md`) never triggers a callout, no matter how deep — a level
opening should never shout an escalation the player did nothing to earn
(Player Fantasy guarantee 2).

**Tier table:**

| Tier | `chain_index` Range | Placeholder Copy | Visual Escalation | Audio |
|---|---|---|---|---|
| 1 | 2–3 | "Sweet!" | Standard pop scale + particle boost (Formula 4) | `audio_cascade_milestone_1` |
| 2 | 4–5 | "Delicious!" | Screen-color pulse begins (`chain_index ≥ BIG_CASCADE_CHAIN_THRESHOLD`, art bible's 4+ rule) | `audio_cascade_milestone_2` |
| 3 (max) | 6+ | "Spectacular!" | Pulse + largest particle boost this ladder reaches (Formula 4's `CHAIN_BOOST_CAP`) | `audio_cascade_milestone_3` |

The displayed "×N" suffix (e.g. "Sweet! ×2") is a **presentation-layer
readout of `chain_index` itself**, not a claim about score value — Scoring &
Star Thresholds (forward dependency, not yet authored) owns the actual point
multiplier. If Scoring's eventual multiplier ever numerically diverges from
`chain_index`, the callout's "×N" readout should switch to read Scoring's
value instead; that substitution is a forward seam, not a redesign, since
the callout's timing/tier logic stays keyed to `chain_index` regardless of
what number it displays.

### 6. Audio Hook Map

A spec table for `sound-designer` — event names, triggers, and how many
distinct assets are needed. This section deliberately specifies **behavior
and asset count, never waveform content, mixing, or DSP** — that authorship
belongs entirely to `sound-designer`.

| Audio Event | Trigger | Assets Needed | Pitch/Variation | Notes |
|---|---|---|---|---|
| `audio_swap_slide` | `swap_started` | 1 | None | Fires on every swap attempt, regardless of outcome |
| `audio_swap_accept` | `swap_accepted` | 1 | None | Soft; may be omitted and folded into `audio_pop_base` if redundant (sound-designer's call) |
| `audio_swap_revert` | `swap_rejected` | 1 | None | Deliberately soft, non-punishing — never an error/buzzer tone |
| `audio_pop_base` | `match_cleared`, any `chain_index` | **1** | Pitch-shifted at runtime per Formula 3 | One asset covers the entire escalation range — do not author `N` discrete pre-pitched samples; runtime pitch-shift keeps the escalation math exact and the asset list small |
| `audio_special_spawn_stripe` | `special_spawned`, stripe family | 1 | None | |
| `audio_special_spawn_bomb` | `special_spawned`, bomb family | 1 | None | |
| `audio_special_activate_stripe` | `special_activated`, activating piece is a stripe | 1 | None | Pairs with the tween-driven directional line-sweep VFX |
| `audio_special_activate_bomb` | `special_activated`, activating piece is a bomb | 1 | None | Pairs with the radial-shockwave hero-frame-animation VFX — the "bomb hero moment" |
| `audio_cascade_milestone_1` / `_2` / `_3` | First entry into each callout tier (§ 5) | 3 | None (distinct per tier, layered atop `audio_pop_base`, not a replacement for it) | Tier 3 should read as the peak moment of the ladder |
| `audio_refill_drop` | `pieces_spawned(source=CASCADE_REFILL)` | 1 | Slight per-column playback stagger, no pitch change | Soft and non-fatiguing — plays many times per session |
| `audio_bootstrap_drop` | `pieces_spawned(source=BOOTSTRAP)` | 1 | None | One sweeping cue for the whole opening beat, not per-piece |
| `audio_reshuffle_cue` | `no_valid_moves_detected` | 1 | None | Short anticipatory intake sound |
| `audio_reshuffle_resolve` | `board_reshuffled` | 1 | None | Whoosh-settle, paired with the swirl VFX |
| `audio_cascade_finale` | `cascade_ended`, `final_chain_index ≥ BIG_CASCADE_CHAIN_THRESHOLD` | 1 | None | Optional embellishment tail |
| `audio_ui_tap` | Any Screen Flow chrome button press | 1 | None | Reused across every button, per art bible's consistent button language |
| `audio_star_ceremony_1` / `_2` / `_3` | Results Win, keyed to `stars_earned` (§ 11 seam) | 3 | None | Tier 3 pairs with the frame-animation starburst hero moment |
| `audio_region_ambient_hub` | Looping bed while `GAMEPLAY`/`WORLD_MAP` is active in Region 1 | 1 at MVP (+1 per future region) | None | Region 2–4 beds are a declared seam (§ 11), not built at MVP |

Global mute gates: `music_enabled` silences `audio_region_ambient_*` only;
`sfx_enabled` silences every other row in this table; the two flags are
independent (per `save-persistence.md`'s Settings sub-schema) — muting one
never silently mutes the other.

### 7. Haptics Map

| Trigger | Pattern | Relative Intensity | Notes |
|---|---|---|---|
| `swap_accepted` | Single light tick | Low | Fires once per accepted swap, regardless of the cascade depth that follows |
| `swap_rejected` | *(none)* | — | Zero penalty, per Player Fantasy guarantee 1 / `characters-and-tone.md`'s never-punishing rule |
| `match_cleared`, `chain_index` 1–3 | *(none)* | — | Reserved for milestone steps only (below) — avoids haptic spam on ordinary matches |
| `match_cleared`, `chain_index ≥ BIG_CASCADE_CHAIN_THRESHOLD` (4+) | Light-to-medium tick, scaling with callout tier (§ 5) | Low→Medium | Paired with the milestone callout |
| `special_spawned` | Light-medium tick | Low–Medium | A new special is born |
| `special_activated`, stripe | Medium tick | Medium | |
| `special_activated`, bomb | Strong tick | High | Matches the bomb's hero-frame-animation treatment |
| `board_reshuffled` | *(none, or a single very soft tick — Tuning Knob toggle)* | None/Very Low | Deliberately gentle — a reshuffle is neither an achievement nor a failure |
| Results Win, `stars_earned = 1` | Single pulse | Low | |
| Results Win, `stars_earned = 2` | Double pulse | Medium | |
| Results Win, `stars_earned = 3` | Multi-pulse celebration pattern | High | The standout haptic moment of an attempt — explicit task requirement |
| Generic UI tap | Very light tick | Very Low | Shared across every chrome button |

**Global toggle.** Every row above is gated by `haptics_enabled`
(`save-persistence.md`'s Settings sub-schema, already defined there) — when
`false`, every row becomes a no-op. On Web export, haptics have no effect
regardless of the flag's stored value (`save-persistence.md`'s own note),
so this is naturally moot there. The Juice Layer never writes this setting,
only reads it (Settings screen owns writes, per `screen-flow.md`'s Data
Contract).

### 8. Reduced-Motion & Accessibility Mode

`reduced_motion_enabled` (`save-persistence.md`'s Settings sub-schema,
storage owned there, visual meaning owned here per that document's own
note) is read **per Reveal Step, at the moment that step begins
presenting** — not cached once per Reveal Queue — so a setting change takes
effect starting with the very next step, never retroactively altering a
step already mid-animation (Acceptance Criteria).

When `true`:

- **Screen-shake**: fully disabled — a hard off, never scaled down. The
  camera never moves, regardless of cascade depth.
- **Screen-color pulse / bloom** (the cascade-combo full-screen pulse,
  `art-bible.md`'s "brief screen-color pulse that cycles through the
  matched candies' hues"): fully disabled — a hard off. This also trivially
  satisfies the flash-safety rule below, since a disabled pulse cannot
  flash.
- **Particle count**: multiplied by `REDUCED_MOTION_PARTICLE_MULTIPLIER`
  (Tuning Knobs) on top of whatever the LOD ladder (Formula 4) already
  applied — the two multipliers stack; LOD is a performance safety net,
  reduced motion is a player preference, and both can be active at once.
- **Positional slide/tween transitions** (swap slide, fall-drop): replaced
  by a shorter, opacity-based cross-fade at `REDUCED_MOTION_DURATION_SCALE`
  × the normal duration, rather than a full positional traversal — reduces
  vestibular-triggering motion while still communicating "this piece is now
  here."
- **The core particle-burst pop on a match is preserved**, never fully
  disabled, only reduced in magnitude — per `art-bible.md`'s explicit
  accessibility requirement, and Player Fantasy guarantee 4.
- **Audio and haptics are unaffected** — `reduced_motion_enabled` is
  specifically a visual-motion setting; audio and haptics have their own
  independent toggles (`sfx_enabled`, `music_enabled`, `haptics_enabled`)
  and are never silently coupled to it.

**Flash-safety rule (always on, independent of `reduced_motion_enabled`).**
No full-screen brightness/color pulse effect this document specifies may
exceed `FLASH_SAFETY_MAX_HZ` (3Hz), sourced from WCAG 2.3.1's general flash
threshold guideline. This is a hard floor the Juice Layer enforces
unconditionally, not a toggle — reduced motion additionally disables the
screen-pulse effect outright, but even with reduced motion **off**, its
cadence must satisfy Formula 6. The default Tuning Knob values are proven
to satisfy this by construction (Formula 6's worked example), so this
should structurally never fire in practice — defense in depth, mirroring
`board-engine.md` Formula 4's own "should structurally never fire" framing
for its touch-target proof.

**Color coding.** Per `art-bible.md`'s Color Coding Rule, particle color
always matches the source candy or special that triggered it — this
document never introduces a VFX color that isn't traceable to a real board
event (Pillar 2). Colorblind/icon-vs-color accessibility is owned by
`art-bible.md`/UX (the candies themselves are already double-coded by shape
and color) — this document adds nothing further on that axis beyond never
using color as a VFX's *sole* signal of what happened.

### 9. Performance Guardrails

Per `art-bible.md`'s VFX Performance Guardrail, every particle effect is
built from a shared, atlas-packed particle-sprite sheet (single texture,
ideally single material) so a heavy cascade with many simultaneous pops
stays within `.claude/docs/technical-preferences.md`'s global ≤100
draw-call ceiling. The exact `GPUParticles2D`/`CPUParticles2D`
implementation, pooling strategy, and atlas layout is `technical-artist`
scope — this document specifies the **visual/count target and its
degradation behavior**, never the implementation.

**Sub-budget rationale.** The global ≤100 draw-call ceiling must also cover
board tiles and UI chrome, which are not this document's concern. The Juice
Layer therefore claims a conservative sub-allocation,
`JUICE_VFX_DRAW_CALL_BUDGET` (default 40, Tuning Knobs), so the VFX layer is
never the system that blows the global ceiling even in Board Engine's own
documented worst case (`board-engine.md` Formula 5: `max_cells_per_step` up
to 81, on a 9×9 board with a fully degenerate single-step full-board clear).

**Degradation ladder.** When a step's estimated particle draw-call cost
would exceed the sub-budget, the Juice Layer degrades particle count in
discrete tiers (Formula 4) rather than either silently exceeding the
ceiling or refusing to render — every event still gets *some* particle
feedback, just proportionally less as concurrent load rises:

| LOD Tier | Trigger (estimated draw calls) | Particle Scale | Screen Pulse/Bloom |
|---|---|---|---|
| 0 (Full) | ≤ `JUICE_VFX_DRAW_CALL_BUDGET` (40) | 100% | Enabled |
| 1 | 41–60 | 75% | Enabled |
| 2 | 61–90 | 50% | Disabled |
| 3 (Floor) | > 90 | 25% | Disabled |

This ladder is a **heuristic approximation**, not an exact draw-call
enumeration — the same explicit caveat `board-engine.md` Formula 6 applies
to its own cascade-continuation probability model. It is expected to be
re-validated against a real profiled measurement once actual particle
assets and atlas layout exist (see Open Questions).

### 10. Input Lock Ownership (`juice_input_lock`) & Screen Flow Composition

Board Engine's own `board_input_enabled` flag becomes `true` again
synchronously, in the same logic frame the triggering swap was submitted —
long before the Juice Layer has replayed a single visible frame of that
move's cascade. Relying on that raw signal alone would let a player fire a
second `swap_request` while the *first* move's cascade is still visually
animating, which would both break the pacing this entire document exists to
protect and, per § 2, invalidate the Shadow Board Model's single-queue-at-a-
time assumption.

The Juice Layer therefore owns a second, independent signal:
`juice_input_lock_changed(locked: bool)`.

- `locked` becomes `true` the instant the Juice Layer begins draining a new
  Reveal Queue (its first Reveal Step is dequeued).
- `locked` becomes `false` the instant that Reveal Queue's final
  `SETTLE_REVEAL` step finishes presenting.

This is formalized as Formula 5. It is designed as a **direct extension**
of `screen-flow.md`'s own Formula 5 (`effective_board_input_enabled =
board_engine_ready AND NOT overlay_is_active`), adding `AND NOT
juice_input_lock` as a third, independently-owned veto term — following the
exact same composition pattern that document already establishes for
`overlay_is_active`. **This is a recommended follow-up to `screen-flow.md`,
not made there, per this document's own file-edit scope** (see
Cross-References for the one open contingency this dependency creates).

### 11. Declared Seams

The following are named, not designed, in this document — each is a table
slot or mount point a future system fills in, kept explicit now so no
future author has to guess where their content plugs in.

**Special Candies combo flourishes.** The special-×-special combo matrix
itself belongs entirely to `special-candies.md` (`systems-index.md`'s
system boundary for #5) — this document only reserves the presentation
slots the combo matrix will eventually trigger. `REPORT.md`'s own findings
are directly relevant here and are restated as design context for whoever
fills these slots: special-×-special chains were the prototype's biggest
emergent moments, but passive color-bomb detonation *during* a cascade
(rather than a direct player swap) risked feeling "unearned" — an
explicitly open design question `special-candies.md` must resolve, which
this document's Player Fantasy guarantee 2 (feedback proportional to what's
earned) directly constrains: a passively-triggered detonation should not
automatically receive the same hero-tier treatment as a player-swapped one
unless that combo's design explicitly justifies it.

| Combo Slot | Pair | MVP/VS Scope | Status |
|---|---|---|---|
| `combo_stripe_stripe` | Stripe × Stripe | MVP | Reserved |
| `combo_stripe_bomb` | Stripe × Color Bomb | MVP | Reserved |
| `combo_bomb_bomb` | Color Bomb × Color Bomb | MVP | Reserved |
| `combo_stripe_wrapped` | Stripe × Wrapped | Vertical Slice | Reserved |
| `combo_wrapped_wrapped` | Wrapped × Wrapped | Vertical Slice | Reserved |
| `combo_wrapped_bomb` | Wrapped × Color Bomb | Vertical Slice | Reserved |

**Region ambient themes.** `art-bible.md`'s Regional Modularity system
names an ambient-particle theme as one of a region's four swappable
elements; `game-concept.md` separately names "ambient music per region."
The Juice Layer eventually owns both the runtime ambient-particle renderer
and the ambient audio bed per region, parameterized by region identity. At
MVP only Region 1 (Candy Kingdom Hub) is filled in (`audio_region_ambient_hub`,
§ 6); Regions 2–4's beds and particle themes are reserved, unfilled slots
until Level Progression/World Map (#11) and Events/Theming Engine (#13)
exist.

**Star ceremony.** Ownership is split deliberately: Game UI/Screens Flow
owns *when and where* — the `RESULTS_WIN` base state and its entry
transition (`screen-flow.md` T15). The Juice Layer owns *what it looks and
sounds like* — the warm gold/white starburst overlay over the frozen board
(`art-bible.md`'s exact palette, `#fff4b8` / `#ffd93d`), the one additional
frame-by-frame hero-animation budget item this treatment earns alongside
color-bomb activation (`art-bible.md`'s Animation Standards), the three
`audio_star_ceremony_*` stingers (§ 6), and the escalating haptic pattern
(§ 7), all keyed to `ResultsData.stars_earned` (`screen-flow.md`'s declared
seam, itself sourced from Scoring & Star Thresholds, forward dependency).

**Booster harvest VFX.** Reserved slot (`booster_harvest_vfx_slot`) for
Booster Brewing Meta (#12, Phase 2, gated on founder approval of a friction
prototype per `systems-index.md`) — the visual feedback for an ingredient
being harvested from a match. Named only; not designed.

**Score popup.** Reserved slot (`score_popup_slot`) for Scoring & Star
Thresholds (#6, not yet authored) — the floating "+N" text popup on a
clear. This document's `CLEAR_REVEAL` timing (Formula 2) is the presentation
window that slot will animate within; the actual point value displayed is
entirely Scoring's authority.

**UI/modal transition animation language.** `screen-flow.md` § 10 bounds
screen and overlay transition *durations* (`MAX_SCREEN_TRANSITION_MS` =
300ms, `MAX_MODAL_TRANSITION_MS` = 200ms) but explicitly defers their visual
*treatment* to "Juice Layer/UX territory." This document claims the
tween-first animation *language* only (the same fade/scale/slide vocabulary
and easing family used for board juice, per `art-bible.md`'s Art Style
section) as a design-consistency note, operating inside those two
externally-owned ceilings — full per-screen transition choreography remains
`design/ux/` territory once that spec exists, and is not designed here.

**Hard boundary — Fizz never renders on the live board.** Per
`characters-and-tone.md` and `screen-flow.md` § 8, Fizz's mount points
(Pre-Level Card, Results Win, Results Lose) are exclusively non-board
screens Game UI/Screens Flow hosts. This is stated here explicitly as a
**non-seam** — a boundary, not a hook — precisely to prevent a future
addition from routing a mascot celebration through the Juice Layer's own
board-event pipeline. No signal in § 4's vocabulary table ever triggers
Fizz content, and none ever should.

---

## Formulas

### Formula 1 — Inter-Step Beat Deceleration Curve

The acceleration curve applies specifically to the **pause between chain
steps** (the inter-step "beat"), not to any individual pop/fall animation's
own duration — those stay at full, consistent visual quality throughout a
cascade (art bible's fixed timing targets, Tuning Knobs). This is the
deliberate design choice that resolves the task's "cap long cascades'
per-step time" requirement without ever making an individual pop look
rushed or cheapened.

**Named expression:**
```
inter_step_beat_ms(chain_index) = BEAT_FLOOR_MS +
    (BEAT_BASE_MS − BEAT_FLOOR_MS) × BEAT_DECAY_RATE ^ (chain_index − 1)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `chain_index` | int | `≥ 1` | Board Engine's own cascade-step counter (`board-engine.md` § Detailed Rules 6), passed through unmodified |
| `BEAT_BASE_MS` | float (constant) | Tuning Knobs, default 220 | The pause after the first cascade link — generous, so an early chain reads clearly |
| `BEAT_FLOOR_MS` | float (constant) | Tuning Knobs, default 60 | The asymptotic minimum pause a deep chain decays toward, never reaches exactly, never goes below |
| `BEAT_DECAY_RATE` | float (constant) | Tuning Knobs, default 0.72, `(0,1)` | Per-step multiplier applied to the *excess* over the floor |
| `inter_step_beat_ms(chain_index)` | float | `(BEAT_FLOOR_MS, BEAT_BASE_MS]` | The beat duration for this specific cascade step |

**Output range**: strictly monotonically decreasing in `chain_index`,
bounded above by `BEAT_BASE_MS` (at `chain_index = 1`) and asymptotically
approaching but never reaching `BEAT_FLOOR_MS` as `chain_index → ∞` — a
clean, always-positive floor with no clamping logic required.

**Worked example** (default constants): `chain_index = 1` →
`60 + 160 × 0.72⁰ = 220ms`. `chain_index = 2` → `60 + 160 × 0.72¹ ≈ 175.2ms`.
`chain_index = 4` → `60 + 160 × 0.72³ ≈ 119.7ms`. `chain_index = 20`
(`MAX_CASCADE_DEPTH`, `board-engine.md` Formula 6's recommended default) →
`60 + 160 × 0.72¹⁹ ≈ 60.16ms` — already indistinguishable from the floor by
the time a cascade reaches Board Engine's own defensive depth cap.

---

### Formula 2 — Total Cascade Presentation Time & Per-Step Fall Duration

**Named expression:**
```
fall_reveal_ms(distance_cells) = clamp(
    FALL_MS_PER_CELL × distance_cells + FALL_BOUNCE_BASE_MS,
    FALL_REVEAL_MIN_MS, FALL_BOUNCE_DURATION_CAP_MS)

step_total_ms(i, d_i) = MATCH_POP_DURATION_MS + fall_reveal_ms(d_i)
                         + inter_step_beat_ms(i)

total_presentation_ms(N) = SWAP_SLIDE_DURATION_MS
    + Σ_{i=1}^{N} step_total_ms(i, d_i)
    + BOARD_SETTLE_BEAT_MS
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `distance_cells` | int | `[0, rows−1]` | The longest single-piece fall distance within a cascade step's affected segments (Shadow Board Model, § 2) |
| `FALL_MS_PER_CELL` | float (constant) | Tuning Knobs, default 45 | Fall-travel time per cell of vertical distance |
| `FALL_BOUNCE_BASE_MS` | float (constant) | Tuning Knobs, default 80 | Fixed landing-bounce overhead added regardless of distance |
| `FALL_REVEAL_MIN_MS` | float (constant) | Tuning Knobs, default 120 | Floor — even a 0–1 cell fall gets a perceptible settle |
| `FALL_BOUNCE_DURATION_CAP_MS` | float (constant) | 400 (art bible-sourced, § Tuning Knobs) | Ceiling — "capped at 400ms so long cascades don't drag" |
| `fall_reveal_ms(distance_cells)` | float | `[FALL_REVEAL_MIN_MS, FALL_BOUNCE_DURATION_CAP_MS]` | Presentation duration for one step's fall+bounce |
| `N` | int | `[1, MAX_CASCADE_DEPTH]` | `final_chain_index` from `cascade_ended` |
| `d_i` | int | `[0, rows−1]` | Fall distance at step `i` |
| `total_presentation_ms(N)` | float | unbounded above (bounded in practice by `N`'s own rarity, `board-engine.md` Formula 6) | Full wall-clock time from swap to input unlock for a given move |

**Output range**: `fall_reveal_ms` is hard-clamped to
`[FALL_REVEAL_MIN_MS, FALL_BOUNCE_DURATION_CAP_MS]`. `total_presentation_ms`
is not clamped — it is a QA/tuning budget check, not a runtime cap (mirrors
`screen-flow.md` Formula 3's advisory-budget pattern), because Board
Engine's own Formula 6 already shows deep chains are exponentially rare, so
an explicit hard ceiling here would either never bind or would truncate a
genuinely earned deep cascade mid-celebration — exactly the failure mode
`board-engine.md`'s Tuning Knobs table flags for `MAX_CASCADE_DEPTH` itself.

**Worked example — fall duration at the schema's worst case** (a 9-row
segment, `distance_cells = 8`): `45 × 8 + 80 = 440ms`, clamped down to the
`400ms` cap.

**Worked example — total presentation time for a 4-link cascade** (the
concept prototype's own deepest observed chain, `REPORT.md`), using a
representative `d_i = 3` for every step: `fall_reveal_ms(3) = 45×3+80 =
215ms` (no clamping needed). Beats from Formula 1: `220.0, 175.2, 142.9,
119.7`. Per-step totals: `200+215+220.0=635.0`, `200+215+175.2=590.2`,
`200+215+142.9=557.9`, `200+215+119.7=534.7`. Sum of steps `= 2317.8ms`.
```
total_presentation_ms(4) = 150 (swap) + 2317.8 (4 steps) + 150 (settle)
                          ≈ 2617.8ms  ≈ 2.6 seconds
```
A four-link cascade — the deepest the prototype ever produced — resolves
its full presentation in roughly 2.6 seconds: long enough to read as a real
event, short enough to never feel like a wait.

---

### Formula 3 — Audio Pitch Escalation

**Named expression:**
```
pitch_semitones(chain_index) = min(PITCH_STEP_SEMITONES × (chain_index − 1),
                                    PITCH_CAP_SEMITONES)
pitch_multiplier(chain_index) = 2 ^ (pitch_semitones(chain_index) / 12)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `chain_index` | int | `≥ 1` | Board Engine's cascade-step counter |
| `PITCH_STEP_SEMITONES` | float (constant) | Tuning Knobs, default 2 | Semitone increase per additional cascade link |
| `PITCH_CAP_SEMITONES` | float (constant) | 12 (fixed, task-specified — one octave) | Hard ceiling, never exceeded |
| `pitch_semitones(chain_index)` | float | `[0, 12]` | Semitone offset from `audio_pop_base`'s authored pitch |
| `pitch_multiplier(chain_index)` | float | `[1.0, 2.0]` | Runtime playback-rate/pitch-scale value applied to `audio_pop_base` |

**Output range**: bounded, `[0, 12]` semitones / `[1.0, 2.0]×` playback
rate — a deep cascade's pop never exceeds one octave above the base pop,
per the task's explicit cap.

**Worked example**: `chain_index = 1` → `0` semitones, `×1.0` (unchanged).
`chain_index = 4` → `min(2×3, 12) = 6` semitones, `×1.414` (a tritone up).
`chain_index = 7` → `min(2×6, 12) = 12` semitones, `×2.0` (exactly one
octave — the cap binds here for the first time). `chain_index = 10` →
`min(2×9, 12) = 12` semitones (still capped; the uncapped value would have
been `18`).

---

### Formula 4 — Particle Emission & LOD Degradation

**Named expression:**
```
chain_boost(chain_index) = min(1 + CHAIN_BOOST_STEP × (chain_index − 1),
                                CHAIN_BOOST_CAP)
requested_particles(n_cells, chain_index) = n_cells × PARTICLES_PER_POP_BASE
                                             × chain_boost(chain_index)
estimated_draw_calls = ceil(requested_particles / PARTICLES_PER_BATCH)
lod_scale(estimated_draw_calls) = per the LOD Tier table (§ 9)
final_particles = round(requested_particles × lod_scale(estimated_draw_calls)
                         × reduced_motion_scale)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `n_cells` | int | `[1, rows×cols]` | Number of cells in this step's `match_cleared.cleared_pieces` |
| `PARTICLES_PER_POP_BASE` | int (constant) | Tuning Knobs, default 8 | Baseline particles per cleared cell, within art bible's "6–10 particles" range |
| `chain_boost(chain_index)` | float | `[1.0, CHAIN_BOOST_CAP]` | Per-cascade-link particle multiplier |
| `CHAIN_BOOST_STEP` | float (constant) | Tuning Knobs, default 0.15 | Growth rate of `chain_boost` per additional link |
| `CHAIN_BOOST_CAP` | float (constant) | Tuning Knobs, default 2.5 | Ceiling on the cascade particle multiplier |
| `requested_particles` | float | `≥ 0` | Pre-degradation particle count for this step |
| `PARTICLES_PER_BATCH` | int (constant) | Tuning Knobs, default 20 | Particles one atlas-batched draw call is assumed to cover |
| `estimated_draw_calls` | int | `≥ 0` | Heuristic draw-call cost estimate for this step's particles alone |
| `lod_scale` | float | `{1.0, 0.75, 0.5, 0.25}` | Degradation ladder scale (§ 9's table), selected by `estimated_draw_calls` |
| `reduced_motion_scale` | float | `{1.0, REDUCED_MOTION_PARTICLE_MULTIPLIER}` | `1.0` unless `reduced_motion_enabled` (§ 8) |
| `final_particles` | int | `≥ 0` | Actual particle count instantiated for this step |

**Output range**: `final_particles` is always a non-negative integer, no
larger than `requested_particles`, and stacks two independent multiplicative
reductions (performance LOD, accessibility preference) rather than treating
either as authoritative over the other.

**Worked example** (Board Engine Formula 5's worst case — an `81`-cell
full-board clear on a 9×9 board — at a deep `chain_index = 8`):
```
chain_boost(8) = min(1 + 0.15×7, 2.5) = min(2.05, 2.5) = 2.05
requested_particles = 81 × 8 × 2.05 = 1,328.4 → 1,328
estimated_draw_calls = ceil(1,328 / 20) = 67   → LOD Tier 2 (61–90 → scale 0.50)
final_particles (motion normal) = round(1,328 × 0.50 × 1.0) = 664
final_particles (reduced motion) = round(1,328 × 0.50 × 0.5) = 332
```
After degradation, the resulting draw-call estimate for the normal-motion
case is `ceil(664 / 20) = 34` — back under `JUICE_VFX_DRAW_CALL_BUDGET`
(40), confirming the ladder is self-correcting at this heuristic level even
against Board Engine's own most extreme documented single-step case.

---

### Formula 5 — Effective Input Lock Composition

**Named expression:**
```
juice_input_lock = true, from the first Reveal Step dequeued
                    for a move, until its SETTLE_REVEAL step completes

effective_board_input_enabled = board_engine_ready
                                 AND NOT overlay_is_active
                                 AND NOT juice_input_lock
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `board_engine_ready` | bool | `{true, false}` | Board Engine's own internal busy-state signal — becomes `true` synchronously the instant its resolution loop reaches `Idle`, independent of any presentation |
| `overlay_is_active` | bool | `{true, false}` | `screen-flow.md`'s own veto term — `true` whenever `PAUSE`/`SETTINGS` is open |
| `juice_input_lock` | bool | `{true, false}` | This document's veto term (above) |
| `effective_board_input_enabled` | bool | `{true, false}` | The value Touch & Input's Rule 4 busy-gate ultimately reads, per `screen-flow.md` § 7 |

**Output range**: boolean; `true` only when all three independently-owned
conditions hold simultaneously — the composition is a strict `AND`, so any
single system's "busy" veto is sufficient to keep input locked, and no
system needs to know about the others' internal state to correctly
contribute its own term.

**Worked example**: a player's swap has fully resolved inside Board Engine
(`board_engine_ready = true`, synchronously, the same frame the swap was
submitted); no overlay is open (`overlay_is_active = false`); the Juice
Layer is still on cascade step 3 of a 4-step Reveal Queue
(`juice_input_lock = true`). `effective_board_input_enabled = true AND
true AND false = false` — the player cannot fire a new swap yet, even
though Board Engine itself is already idle, precisely the scenario the task
brief's "input locked during resolution replay" requirement describes.
`effective_board_input_enabled` only flips `true` once `SETTLE_REVEAL`
completes and `juice_input_lock` clears.

---

### Formula 6 — Flash-Safety Frequency Check

**Named expression:**
```
pulse_frequency_hz = 1000 / (pulse_duration_ms + pulse_gap_ms)
constraint: pulse_frequency_hz ≤ FLASH_SAFETY_MAX_HZ
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `pulse_duration_ms` | float | `≥ 0` | Duration of one full-screen brightness/color pulse cycle |
| `pulse_gap_ms` | float | `≥ 0` | Gap before the next pulse could begin |
| `pulse_frequency_hz` | float | `≥ 0` | Effective flash rate |
| `FLASH_SAFETY_MAX_HZ` | float (constant) | 3 (fixed, WCAG 2.3.1-sourced) | Hard ceiling, not tunable upward |

**Output range**: the constraint must hold for every full-screen pulse
effect this document specifies, unconditionally, regardless of the
`reduced_motion_enabled` setting (§ 8).

**Worked example — the fastest-paced case the default Tuning Knobs
produce** (`chain_index` at or near `BEAT_FLOOR_MS`, minimum fall distance):
```
pulse_duration_ms ≈ MATCH_POP_DURATION_MS = 200
pulse_gap_ms ≈ fall_reveal_ms(1) + BEAT_FLOOR_MS = (45+80) + 60 = 185
pulse_frequency_hz = 1000 / (200 + 185) = 1000 / 385 ≈ 2.6Hz
```
`2.6Hz ≤ 3Hz` — the default cadence clears the flash-safety ceiling with
headroom by construction. Any future reduction to `BEAT_FLOOR_MS` or
`MATCH_POP_DURATION_MS` below this document's Tuning Knobs' safe ranges
must re-run this check before shipping.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|---|---|---|
| Board Engine force-stabilizes at `MAX_CASCADE_DEPTH` (some runs left uncleared, `board-engine.md` Edge Cases) | The Reveal Queue simply ends at `cascade_ended.final_chain_index`; nothing further is presented; no special-casing required | The queue-driven design (§ 3) handles this for free — Juice never needs to know *why* a cascade stopped, only where the event stream itself ends |
| A move produces exactly one clear with no chain continuation (`chain_index = 1` only) | No milestone callout, no screen pulse, no cascade haptic — just the base pop | Player Fantasy guarantee 2: a plain match-3 doesn't earn an escalation |
| Special-activation swap with zero overlapping color run (pure seam-1 trigger) | `ACTIVATION_REVEAL` plays for `special_activated.cleared_pieces`; sequencing trusts Board Engine's documented ordering guarantee verbatim (`special_activated` always fires before that move's first `match_cleared`) | Juice never re-derives or second-guesses signal order (§ 3) |
| Reshuffle occurs mid-move (post-cascade, pre-`Idle`) | `RESHUFFLE_REVEAL` plays as the final Reveal Step before `SETTLE_REVEAL`; the one live `get_piece_at()` sweep (§ 2) is safe because input lock guarantees no intervening mutation | Resolves `board-engine.md`'s open payload-sufficiency question for `board_reshuffled` — see Cross-References for the one contingency |
| Bootstrap's own automatic accidental cascade (`board-engine.md` § Detailed Rules 2 step 8) reaches a deep `chain_index` before the player has made a single move | The full acceleration curve (Formula 1) still applies to pacing, but **no milestone callout ever fires** (`trigger_source = BOOTSTRAP` is explicitly excluded, § 5) | A level opening should never congratulate the player for something they didn't do, while still avoiding an excessively long opening beat on a pathological level |
| Two rapid swipe/tap gestures during a locked replay | Both dropped by Touch & Input's own MVP default (`input_buffer_depth = 0`); Juice takes no independent action | Touch & Input's existing contract already covers this once `effective_board_input_enabled` correctly reflects `juice_input_lock` (Formula 5) |
| `reduced_motion_enabled` toggled while a Reveal Queue is mid-flight | Takes effect starting with the next Reveal Step processed; the step currently animating finishes as it started | Avoids a visual "pop" from a setting change altering an animation already in motion (§ 8) |
| `haptics_enabled` toggled `false` mid-replay | Same per-step-read policy as reduced motion — already-fired pulses aren't recalled, future ones in the same replay are suppressed | Consistency with § 8's policy |
| App backgrounds or Pause opens mid-replay (`screen-flow.md` T8/T9) | The current Reveal Queue fast-forwards to completion — remaining beats/tweens are skipped, each remaining step's end-state applies immediately, and `juice_input_lock` clears | Mirrors `screen-flow.md`'s own "completes instantly to its end composite state" policy for interrupted transitions (§ 3); the player never returns to a half-settled board |
| A `cell_mask` level with disconnected column segments (e.g. a heart or diamond shape) | Fall distances (Formula 2) are computed per-segment using Board Engine's own segment boundaries (`board-engine.md` Formula 3), read once at bootstrap and never recomputed mid-level | Shadow Board Model (§ 2) mirrors Board Engine's segmentation exactly, since it is static per level |
| A colorless special (color bomb, `color = COLOR_NONE`) is spawned or cleared | No color tint is applied to its particles/tween — it always uses its dedicated rainbow-gradient/white-glow art, never a recolored generic burst | `art-bible.md`: "intentionally colorless/all-color... never mistaken for a specific candy type"; Pillar 2's color-tells-the-truth rule |
| Board Engine defensively drops a malformed seam-3/seam-4 response (`board-engine.md` Edge Cases) | Entirely invisible to the Juice Layer — it only ever observes Board Engine's final, already-validated emitted signal, never a raw seam response | Strict separation of concerns; Board Engine's validation is the sole source of truth Juice trusts unconditionally |
| A `special_type` value the Juice Layer doesn't yet recognize (beyond MVP's striped/color-bomb vocabulary — not expected to occur before `special-candies.md` ships, but must not crash if it does) | Falls back to a generic "unknown special" flourish reusing the shared sparkle-star particle, rather than crashing or rendering nothing | Mirrors Board Engine's own "documented MVP no-op default" forward-compatibility philosophy for its own extension seams |
| `stars_earned` resolves to `0` on a `WIN` outcome (`screen-flow.md`'s flagged edge case for `collect_color`-only levels) | The star-ceremony seam falls back to a generic "level complete" stinger and starburst treatment — no star-count-specific celebration pattern or `audio_star_ceremony_N` variant | Mirrors `screen-flow.md`'s own fallback-to-general-Win-pool rule for Fizz; no 0-star asset exists by design |
| `sfx_enabled = false` but `haptics_enabled = true` (or any other single-channel mute combination) | Each of visual, audio, and haptic channels is gated independently by its own settings flag; muting one never silently disables another | Per `save-persistence.md`'s independently-stored Settings fields |
| A level's opening bootstrap batch is unusually large (many pre-placed pieces coincidentally forming a run, `board-engine.md` Edge Cases) | Treated identically to any other cascade for pacing purposes (Formula 1/2 apply); only the milestone-callout suppression rule differs from a player-triggered cascade | Consistent formula application regardless of `trigger_source`, except where this document explicitly carves out an exception (§ 5) |

---

## Dependencies

| System | Direction | Nature of Dependency |
|---|---|---|
| Match-3 Board Engine (`board-engine.md`, APPROVED) | Juice Layer depends on it | Consumes the full signal catalog (§ Detailed Rules 7) and its two documented ordering guarantees; `PieceSnapshot` payloads feed the Shadow Board Model (§ 2); `chain_index`/`trigger_source` drive pacing and milestone suppression; column-segment definitions (Formula 3 there) feed fall-distance derivation (Formula 2 here); `max_cells_per_step`/`MAX_CASCADE_DEPTH` (Formulas 5/6 there) are consumed as this document's own worst-case formula inputs (Formula 4 here). **This document fulfills the reciprocal note `board-engine.md`'s own Dependencies table requested** ("when authored, its Dependencies section must list this document"). |
| Special Candies & Combo Matrix (`special-candies.md`, not yet authored, forward) | Juice Layer will depend on it | `special_type` vocabulary beyond MVP's striped/color-bomb pair, and the special-×-special combo matrix that fills § 11's reserved combo slots. **Recommended**: its Dependencies section list this document when authored, confirming its combo outputs against § 11's named slots. |
| Scoring & Star Thresholds (`scoring-stars.md`, not yet authored, forward) | Juice Layer will depend on it (soft) | The eventual score-multiplier value for the cascade callout's "×N" readout (§ 5, currently a `chain_index` placeholder), and `stars_earned`/`score_earned` for the star-ceremony seam (§ 11) and the score-popup seam. **Recommended**: its Dependencies section list this document. |
| Touch & Input System (`touch-input.md`, APPROVED) | Mutual, routed through Game UI/Screens Flow | Touch & Input's Rule 4 busy-gate ultimately reads the composed `effective_board_input_enabled` value (`screen-flow.md` § 7, Formula 5 there); this document supplies the `juice_input_lock` term recommended for that composition (Formula 5 here). `touch-input.md` § 7 already names the Juice Layer as reduced-motion's owner and flags a future raw-drag-position signal as this document's forward scope — both discharged/acknowledged here. |
| Save & Persistence (`save-persistence.md`, Draft) | Juice Layer depends on it | Reads `haptics_enabled`, `reduced_motion_enabled`, `sfx_enabled`, `music_enabled` from the player profile's Settings sub-schema; never writes to any of them (Settings screen owns writes, per `screen-flow.md`'s Data Contract). **Recommended**: its Dependencies section list this document as a reader. |
| Game UI/Screens Flow (`screen-flow.md`, Draft) | Mutual | Screen Flow hosts the `RESULTS_WIN` screen this document's star-ceremony content plays on (§ 11); Screen Flow's Formula 5 is recommended to incorporate this document's `juice_input_lock` term (§ 10, Formula 5 here — not made in `screen-flow.md`, per this document's own file-edit scope); Screen Flow owns every Fizz mount point, which this document explicitly never renders inside (§ 11, hard boundary). **Recommended**: its Dependencies section list this document, and its Formula 5 be revised per § 10's proposal. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | Juice Layer depends on it (constants + rules) | Animation timing targets (swap/pop/fall/special-activation ranges), particle style and Color Coding Rule, screen-space effect threshold (`BIG_CASCADE_CHAIN_THRESHOLD = 4`), the shared atlas-packed-particle performance guardrail, the reduced-motion toggle concept, and the frame-animation hero-moment budget (color-bomb activation, star ceremony). |
| `design/narrative/characters-and-tone.md` (not a `design/gdd/` system) | Juice Layer depends on it (content constraints, forward) | Voice/length/punctuation rules the cascade-callout text pool (§ 5) must satisfy once `narrative-director` authors it; this document owns only the tier structure and a prototype-validated placeholder. |
| `prototypes/sweet-cascade-concept/REPORT.md` | Data dependency | Validated pacing/feel baseline: the escalating-callout language, the ×4 deepest observed chain (Formula 2's worked example), the special-×-special "biggest moments" and "unearned passive detonation" findings (§ 11). |
| `.claude/docs/technical-preferences.md` (not a `design/gdd/` system) | Juice Layer depends on it (constants + rules) | 60fps/16.6ms frame budget, the ≤100 draw-call ceiling (`JUICE_VFX_DRAW_CALL_BUDGET` is a sub-allocation of it, § 9), and the testing-standards boundary on what is unit-testable vs. advisory manual evidence. |

---

## Tuning Knobs

### Timing & Pacing

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `SWAP_SLIDE_DURATION_MS` | 150ms | 120–200ms | Slower, weightier swap read; eats into `total_presentation_ms` (Formula 2) | Snappier swap; below `art-bible.md`'s 120ms floor risks the slide reading as a cut, not a motion |
| `SWAP_REVERT_SHAKE_MS` | 130ms | 80–200ms | More legible "that didn't work" read | Faster revert cycle, but risks the shake reading as a glitch rather than a deliberate cue |
| `MATCH_POP_DURATION_MS` | 200ms | 150–250ms | More lingering pop; art-bible-sourced target is "≤200ms total" for this, the single most repeated animation in the game | Faster pop, protects pace on rapid-fire matches but risks feeling clipped |
| `FALL_MS_PER_CELL` | 45ms/cell | 30–60ms/cell | Slower, more readable falls; raises the `FALL_BOUNCE_DURATION_CAP_MS` clamp point sooner | Snappier falls; too low risks pieces appearing to teleport into place |
| `FALL_BOUNCE_BASE_MS` | 80ms | 50–120ms | More pronounced landing bounce | Less bounce; near 0 risks the landing reading as a hard stop |
| `FALL_REVEAL_MIN_MS` | 120ms | 80–180ms | Ensures even a 0–1 cell fall gets a perceptible settle | Below ~80ms risks a same-row refill reading as instant/invisible |
| `FALL_BOUNCE_DURATION_CAP_MS` | 400ms | 300–450ms (art-bible-sourced ceiling) | More headroom before the cap binds, at the cost of longer worst-case cascades | Tighter cap; below ~300ms risks a long fall visibly "snapping" to its landing |
| `BEAT_BASE_MS` | 220ms | 150–350ms | More generous early-chain pacing, reads clearer but slower | Faster early pacing; below ~150ms risks the very first cascade link feeling rushed |
| `BEAT_FLOOR_MS` | 60ms | 30–100ms | Deep chains stay more paced, less frantic | Deep chains compress further; below ~30ms risks the flash-safety margin (Formula 6) |
| `BEAT_DECAY_RATE` | 0.72 | 0.5–0.85 | Slower convergence to the floor — a mid-depth chain still feels "early" | Faster convergence — the floor is reached sooner, deep chains feel snappier sooner |
| `BOARD_SETTLE_BEAT_MS` | 150ms | 80–300ms | More time to register the final board state before input unlocks | Faster unlock, at the cost of the settle beat feeling clipped |
| `SPECIAL_ACTIVATION_DURATION_MS` | 320ms | 250–400ms (art-bible-locked "hero" range) | More weight to a special's activation | Less weight; below 250ms contradicts the art bible's hero-moment budget |
| `SPECIAL_SPAWN_FLOURISH_MS` | 180ms | 120–250ms | More noticeable "a special was just born" cue | Faster, risks the spawn reading as identical to a normal clear |
| `BOOTSTRAP_STAGGER_PER_COLUMN_MS` | 25ms | 10–50ms | More visible column-by-column stagger on level open | Less stagger; near 0 risks the whole board appearing to snap in at once |
| `RESHUFFLE_ANIMATION_MS` | 600ms | 400–900ms | More legible "the board changed on purpose" swirl | Faster reshuffle; too low risks it reading as a glitch, undermining the exact goal this beat exists for |

### Escalation & Cascade Feel

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `BIG_CASCADE_CHAIN_THRESHOLD` | 4 (art-bible-locked — "4+ chain links") | Not recommended to change without an `art-bible.md` revision | Fewer cascades reach screen-pulse/bloom treatment | More cascades trigger it, risking `art-bible.md`'s own "VFX fatigue" caution |
| `PITCH_STEP_SEMITONES` | 2 | 1–4 | Faster pitch climb, cap (Formula 3) reached sooner | Slower climb; deep chains sound more similar to shallow ones |
| `PITCH_CAP_SEMITONES` | 12 (fixed — task-specified octave cap) | Not tunable | N/A | N/A |
| `PARTICLES_PER_POP_BASE` | 8 | 6–10 (art-bible-sourced range) | Bigger base pop, more draw-call pressure per Formula 4 | Smaller base pop, more LOD headroom |
| `CHAIN_BOOST_STEP` | 0.15 | 0.05–0.3 | Faster particle-count growth per cascade link | Slower growth; deep chains look more similar to shallow ones |
| `CHAIN_BOOST_CAP` | 2.5 | 1.5–3.0 | Higher ceiling on cascade particle boost | Lower ceiling, tighter draw-call budget headroom |
| `SPECIAL_ACTIVATION_PARTICLE_MULTIPLIER` | 1.75 | 1.5–2.0 (art-bible-locked range: "roughly 1.5–2× the particle count") | Bigger special-activation bursts | Smaller bursts, risks specials not reading as "bigger events" per the art bible |

### Particles & Performance

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `JUICE_VFX_DRAW_CALL_BUDGET` | 40 | 20–60 | More particle headroom before LOD degrades, at the cost of the global ≤100 ceiling's remaining margin for board tiles/UI | Tighter VFX budget, LOD degrades sooner, more conservative against the global ceiling |
| `PARTICLES_PER_BATCH` | 20 | 10–40 | Fewer estimated draw calls per particle count (assumes more efficient atlas batching) — should track the actual `technical-artist` implementation, not be tuned freely | More estimated draw calls per particle count, more conservative |
| LOD tier thresholds/scales (§ 9 table) | 40/60/90 draw calls → 100%/75%/50%/25% | Thresholds and scales may shift together; always keep 4 monotonic tiers | Coarser degradation steps | Finer-grained degradation, more tiers possible if profiling data supports it |

### Accessibility & Safety

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `REDUCED_MOTION_PARTICLE_MULTIPLIER` | 0.5 | 0.25–0.75 | More particles preserved under reduced motion | Fewer particles; below 0.25 risks losing the "core particle-burst feedback" `art-bible.md` requires be preserved |
| `REDUCED_MOTION_DURATION_SCALE` | 0.7 | 0.5–0.9 | Longer reduced-motion transitions, closer to normal-motion feel | Shorter, snappier reduced-motion transitions |
| `FLASH_SAFETY_MAX_HZ` | 3 (fixed — WCAG 2.3.1-sourced) | **Not permitted above 3** | N/A | N/A — hard ceiling, not a tunable-up value |
| `haptic_reshuffle_soft_tick` | off (default) | `{on, off}` | Adds a very soft haptic to reshuffle — validate this doesn't read as alarming before enabling | Off is the MVP-validated default |

---

## Acceptance Criteria

**Unit-testable — replay logic, formulas, and composition** (`tests/unit/juice-layer/`,
BLOCKING per `coding-standards.md`'s Logic-tier rule; deterministic, driven by
a mocked Board Engine event stream, no real timers or RNG required):

- [ ] `test_reveal_queue_matches_board_engine_special_activation_ordering`:
      given a mocked signal burst reproducing `board-engine.md`'s own
      documented special-activation ordering guarantee, the derived Reveal
      Queue produces `SWAP_REVEAL → ACTIVATION_REVEAL → CLEAR_REVEAL(1) →
      FALL_REVEAL(1)/REFILL_REVEAL(1) → ... → SETTLE_REVEAL` in exactly that
      order (§ 3).
- [ ] `test_reveal_queue_matches_board_engine_bootstrap_ordering`: given a
      mocked signal burst reproducing `board-engine.md`'s documented
      bootstrap ordering guarantee (including an accidental cascade and a
      reshuffle), the derived Reveal Queue produces
      `BOOTSTRAP_OPEN_REVEAL → CLEAR_REVEAL(1) → ... → RESHUFFLE_REVEAL →
      SETTLE_REVEAL` in exactly that order, with **no** `MILESTONE_REVEAL`
      steps present anywhere in the sequence.
- [ ] `test_milestone_reveal_suppressed_for_bootstrap_trigger_source`: for
      any cascade step with `trigger_source = BOOTSTRAP`, regardless of
      `chain_index`, no `MILESTONE_REVEAL` step is ever produced (§ 5).
- [ ] `test_milestone_reveal_present_from_chain_index_2_for_player_moves`:
      for `trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}`, a
      `MILESTONE_REVEAL` step is produced for every `chain_index ≥ 2` and
      for no `chain_index = 1` step.
- [ ] `test_shadow_board_reconstruction_matches_worked_walkthrough`: replaying
      `board-engine.md` § Detailed Rules 13's own worked 2-step-cascade
      event sequence through the Shadow Board Model reproduces the exact
      documented final grid state (every cell's `color`/`special_type`) with
      **zero** live board queries performed.
- [ ] `test_shadow_board_gravity_rederivation_matches_segment_definition`:
      given a synthetic multi-segment column (mirroring `board-engine.md`
      Formula 3's worked example) and a mocked clear, the Shadow Board
      Model's independently-derived fall paths match Board Engine's own
      documented compaction rule (pieces settle toward their segment's own
      bottom, never crossing a `VOID`).
- [ ] `test_reshuffle_reveal_live_query_occurs_exactly_once_and_only_after_lock_confirmed`:
      confirms the Shadow Board Model's one permitted live query fires only
      while presenting `RESHUFFLE_REVEAL`, and only after asserting
      `juice_input_lock = true` has held continuously since the queue began
      (§ 2, § 10).
- [ ] Formula 1 regression: `inter_step_beat_ms(1) = 220`,
      `inter_step_beat_ms(2) ≈ 175.2`, `inter_step_beat_ms(4) ≈ 119.7`,
      `inter_step_beat_ms(20) ≈ 60.16`, using default Tuning Knobs, each
      within a `0.1ms` epsilon.
- [ ] Formula 2 regression: `fall_reveal_ms(8) = 400` (clamped),
      `total_presentation_ms(4) ≈ 2617.8` reproduces the documented worked
      example within a `1ms` epsilon.
- [ ] Formula 3 regression: `pitch_semitones(1) = 0`, `pitch_semitones(4) =
      6`, `pitch_semitones(7) = 12`, `pitch_semitones(10) = 12` (cap holds);
      `pitch_multiplier(7) = 2.0` exactly.
- [ ] Formula 4 regression: the documented worked example
      (`n_cells=81, chain_index=8`) reproduces `requested_particles = 1328`,
      `estimated_draw_calls = 67`, LOD Tier 2 selected, `final_particles =
      664` (normal motion) and `332` (reduced motion).
- [ ] Formula 5 regression: `effective_board_input_enabled` evaluates
      `false` when `juice_input_lock = true` regardless of the other two
      terms, and `true` only when all three of `board_engine_ready`,
      `NOT overlay_is_active`, and `NOT juice_input_lock` hold
      simultaneously.
- [ ] Formula 6 regression: the fastest-paced default-Tuning-Knob scenario
      reproduces `pulse_frequency_hz ≈ 2.6` and confirms
      `2.6 ≤ FLASH_SAFETY_MAX_HZ (3)`.
- [ ] `test_reduced_motion_takes_effect_next_step_not_current`: toggling
      `reduced_motion_enabled` mid-Reveal-Queue does not alter the
      currently-presenting step's already-computed duration/particle count,
      but does alter the very next step's.
- [ ] `test_haptics_disabled_suppresses_every_trigger`: with
      `haptics_enabled = false`, every row in § 7's table is confirmed to
      fire zero haptic calls across a full synthetic multi-step cascade plus
      a star-ceremony sequence.
- [ ] `test_audio_event_emission_matches_vocabulary_table`: for each signal
      in § 4/§ 6's tables, the exact documented `audio_*` event name fires
      exactly once per occurrence, with no signal producing an undocumented
      event.
- [ ] `test_suspend_fast_forwards_active_queue`: simulating a suspend signal
      (mirroring `screen-flow.md` T8/T9) mid-Reveal-Queue causes every
      remaining step to apply its end-state immediately (no remaining
      beats/tweens execute) and `juice_input_lock` clears by the end of the
      same call.

**Manual walkthrough — feel/UI** (`production/qa/evidence/`, ADVISORY per
`coding-standards.md`'s Testing Standards):

- [ ] On a mid-range Android reference device, a 4-link cascade (the
      prototype's validated deepest chain) feels punchy, not draggy —
      tester estimate matches Formula 2's ~2.6-second budget within
      perceptible tolerance.
- [ ] Reduced-motion mode play-through: no screen-shake or bloom pulse is
      present at any cascade depth; the core particle-burst pop remains
      visibly present at every match; a tester unfamiliar with the toggle
      correctly identifies "less motion, same information."
- [ ] Haptics felt correctly on a physical device for: swap success, swap
      revert (confirmed absent), special activation (stripe vs. bomb
      distinguishable by feel), reshuffle (confirmed gentle, not alarming),
      and the 3-star celebration pattern (confirmed distinct from 1-/2-star).
- [ ] Audio pitch escalation is audibly noticeable and pleasant, not
      grating, across a 4–6 link chain on both device speakers and
      headphones.
- [ ] Draw-call profiling capture during a manufactured worst-case cascade
      (mocked full-board clear at a deep `chain_index`) confirms total
      draw calls (board + UI + Juice VFX) stay ≤100, with the Juice VFX
      contribution specifically staying within `JUICE_VFX_DRAW_CALL_BUDGET`
      before LOD degradation is credited.
- [ ] A slow-motion capture of the fastest-paced deep cascade shows no
      discernible strobe/flicker effect to an unaided-eye reviewer.
- [ ] The bootstrap opening "candies drop into place" beat reads as an
      intentional flourish, not a stutter, on first level entry.
- [ ] The reshuffle swirl reads as "the board changed on purpose" in an
      unprompted tester observation — not "is this broken?"
- [ ] The escalating cascade-callout placeholder copy ("Sweet!" /
      "Delicious!" / "Spectacular!") is legible at a glance and does not
      obstruct the board during a fast-paced session.

**Data-driven compliance**:

- [ ] No constant defined in this document's Tuning Knobs (all four
      subtables) exists as a hardcoded literal in `src/` — every instance is
      sourced from a single data-driven config location, per
      `.claude/docs/coding-standards.md`.

---

## Cross-References

| This Document References | Target | Specific Element | Nature |
|---|---|---|---|
| `board_reshuffled` payload-sufficiency Open Question | `design/gdd/board-engine.md` | Open Questions table, row 1 ("Does `board_reshuffled`'s `attempts_used`-only payload need per-cell reassignment data...") | **Resolved here** (§ 2, § 3, Edge Cases): the input-lock policy (§ 10) guarantees no board mutation occurs between `board_reshuffled` firing and this document's (delayed) presentation of it, so one live `get_piece_at()` query at that specific moment is safe and sufficient — no payload expansion needed. **Contingent** on `screen-flow.md` actually adopting this document's recommended Formula 5 extension (§ 10) — see Open Questions below. |
| `piece_id` rationale vs. actual `PieceSnapshot` schema gap | `design/gdd/board-engine.md` | § Detailed Rules 1 (`Piece.piece_id`'s stated rationale: "Exists so the Juice Layer can animate one persistent visual object through gravity moves") vs. § Detailed Rules 7 (`PieceSnapshot = {cell, color, special_type}` — `piece_id` is never actually included in any signal payload) | **Flagged, not resolved here** — this document's Shadow Board Model (§ 2) and gravity re-derivation work correctly without `piece_id` (a freshly-instantiated sprite with identical `color`/`special_type` art is visually indistinguishable from a persistently-tracked one), so this is a quality-of-implementation gap, not a blocking one. Recommend `board-engine.md` add `piece_id` to `PieceSnapshot` in its next revision so a future implementation can track one continuous visual object through a fall rather than recreating it — see Open Questions. |
| `effective_board_input_enabled` composition | `design/gdd/screen-flow.md` | § 7 and Formula 5 (`board_engine_ready AND NOT overlay_is_active`) | This document's § 10/Formula 5 proposes extending that formula with a third term, `AND NOT juice_input_lock` — a recommended follow-up, not made in `screen-flow.md` itself, per this document's file-edit scope. |
| Reduced-motion ownership and future raw-drag-position signal | `design/gdd/touch-input.md` | § 7 Accessibility ("owned by the Juice Layer... noted here only to make the dependency boundary explicit"); § Detailed Rules 3 ("a separate, additive raw-position signal... future scope for the Juice Layer") | Both references are acknowledged and (for reduced motion) discharged here (§ 8); the raw-drag-position signal remains explicitly out of this document's MVP scope, flagged for a future revision if a "candy follows the finger" juice effect is ever pursued. |
| Animation timing targets, particle style, VFX standards | `design/art/art-bible.md` | Animation Style, VFX Standards, Accessibility sections | Consumed as authoritative external constants throughout Formulas and Tuning Knobs — this document never diverges from an already-approved art-bible value without flagging it explicitly. |
| Settings sub-schema fields | `design/gdd/save-persistence.md` | § 2 `Settings` sub-schema (`haptics_enabled`, `reduced_motion_enabled`, `sfx_enabled`, `music_enabled`) | Storage/persistence owned there; visual/audio/haptic *meaning* owned here, exactly as that document's own field notes state. |
| Validated pacing/feel baseline | `prototypes/sweet-cascade-concept/REPORT.md` | "If Proceeding" section; Lessons Learned | Data dependency — the ×4 deepest chain (Formula 2's worked example), the escalating-callout language (§ 5's placeholder), and the special-×-special / passive-detonation findings (§ 11) all trace back to this source, matching how `board-engine.md` cites the same report as design rationale. |
| Screen/modal transition duration ceilings | `design/gdd/screen-flow.md` | § 10 Transition Juice Boundaries (`MAX_SCREEN_TRANSITION_MS`, `MAX_MODAL_TRANSITION_MS`) | This document claims only the animation *language* within those externally-owned ceilings (§ 11); the ceilings themselves remain `screen-flow.md`'s constants, consumed not redefined. |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| Is this document's `board_reshuffled` live-query resolution (Cross-References, row 1) actually safe once `screen-flow.md` is reviewed, given it depends on Formula 5's `juice_input_lock` term being adopted there? If `screen-flow.md` ships without that extension, does `board-engine.md`'s Open Question reopen? | systems-designer | At `screen-flow.md`'s next review pass | — |
| Should `board-engine.md` add `piece_id` to `PieceSnapshot` so a future implementation can track one continuous visual object through a gravity fall, closing the gap between § Detailed Rules 1's stated rationale and § Detailed Rules 7's actual schema? | systems-designer | At `board-engine.md`'s next review pass | — |
| Should the cascade-callout text pool (§ 5) be authored by `narrative-director` before MVP content lock, or is the prototype-validated placeholder ("Sweet!" / "Delicious!" / "Spectacular!") acceptable to ship as-is? | narrative-director / game-designer | Before Vertical Slice content pass | — |
| Should `JUICE_VFX_DRAW_CALL_BUDGET` (40) and `PARTICLES_PER_BATCH` (20) be reconciled with an actual profiled measurement once real particle assets and atlas layout exist, mirroring `board-engine.md` Formula 6's own "heuristic, not exact enumeration" caveat? | technical-artist | Post-Vertical-Slice performance validation | — |
| Should the `haptic_reshuffle_soft_tick` toggle (Tuning Knobs) default to on or stay off, pending a playtest specifically checking whether any reshuffle haptic reads as alarming rather than gentle? | game-designer | At Vertical Slice playtest | — |
| Once Scoring & Star Thresholds (`scoring-stars.md`) is authored, should the cascade callout's "×N" readout switch from `chain_index` to an actual score multiplier if the two ever diverge numerically (§ 5)? | systems-designer (with Scoring's author) | At `scoring-stars.md` authoring | — |
| Do Region 2–4's ambient particle themes and audio beds (§ 11 declared seam) belong in this document's future revision, or in a dedicated per-region content spec once Level Progression/World Map (#11) and Events/Theming Engine (#13) exist? | game-designer | At Level Progression/World Map authoring | — |
