# Level Objective & Move-Limit System

*Status: Reviewed — APPROVED (re-review, 2026-07-18)*
*Created: 2026-07-18*
*Last Updated: 2026-07-18*
*Layer: Feature · Priority: MVP · Phase: MVP · Category: Gameplay*
*Author: systems-designer*
*Depends On: Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED — Revision 2), Level Data Format (`design/gdd/level-data-format.md`, APPROVED), Scoring & Star Thresholds (`design/gdd/scoring-stars.md`, Revised — Revision 2 — this document pulls its ratified Score Query API, see § Detailed Rules 3, 9 and Dependencies)*
*Depended On By: Game UI/Screens Flow (`design/gdd/screen-flow.md`, Draft), Booster Brewing Meta (#12, Phase 2, gated, not yet authored)*
*Source: `design/gdd/systems-index.md` · `design/gdd/level-data-format.md` §§2–4 · `design/gdd/board-engine.md` §§ Detailed Rules 3, 4, 5, 6, 7, 11, 13 · `design/gdd/special-candies.md` §§ Detailed Rules 5, 6, 9, 10 · `design/gdd/screen-flow.md` §§7, 11 · `design/gdd/rng-service.md` · `design/gdd/game-concept.md` · `design/art/art-bible.md`*

*Revision 2 Changelog (2026-07-18):* Dropped the proposed
`ScoreProvider.finalize_results()` push seam from § Detailed Rules 9. Level
Objective is now formally the `ResultsData` assembler: at the resolving
`board_stabilized` it pulls Scoring's contributed fields via the ratified
`get_score_results() -> ScoreResults` seam and composes the full
`ResultsData` itself, alongside its own `outcome` and objective-completion
data, before firing `level_resolved`. `get_current_score()` (§ Detailed
Rules 3) is likewise now formally ratified. Resolves
`level-objectives-review-log.md` Required Before Implementation #1
(blocking seam-composition mismatch) and Recommended Revisions #1
(unratified `get_current_score()`); mirrored in
`scoring-stars-review-log.md` Required Before Implementation #1 and its
Seam Handshake Audit's secondary advisory gap.

---

## Overview

Level Objective & Move-Limit System is the Feature-layer system that owns
**runtime win/lose evaluation** for Sweet Cascade. It consumes Match-3 Board
Engine's signal stream, tracks each level's authored `objectives`
(`score_target`, `collect_color`, per `level-data-format.md` §2's closed v1
enum) toward completion, counts down the level's authored `move_limit`, and
declares the level resolved through a single `level_resolved` event that
Game UI/Screens Flow routes into the win or lose Results screen. This is the
system `level-data-format.md`'s Dependencies table names as `move_limit`'s
sole runtime owner ("Board Engine does **not** read `move_limit` — move-limit
enforcement is Level Objective's scope") and as `objectives`' sole runtime
interpreter ("this document owns only the data shape" — the runtime
semantics of each objective type belong here).

This document defines exactly two things, and deliberately nothing more at
MVP: **(a)** how each of the two MVP objective types tracks its own progress
using only Board Engine's already-published signal catalog and two small,
explicitly-declared query seams into Scoring & Star Thresholds — with
**zero new API surface required from Board Engine or Special Candies &
Combo Matrix**;
and **(b)** the single evaluation instant, `board_stabilized`, and only
`board_stabilized`, at which a level's outcome is decided — so that a
cascade triggered by the player's final legal move can complete an
objective and win the level in the same dramatic beat the cascade itself
animates. Blocker-clear and ingredient-based objective types, named only as
a future scope line in `systems-index.md`'s one-line summary for this
system, are explicitly **not** designed here; § Detailed Rules 11 names,
without designing, the extension seam a future objective type plugs into,
mirroring `level-data-format.md`'s own closed-enum-plus-version-bump
discipline.

---

## Player Fantasy

Everything in this document exists to protect one promise: **the player
always knows exactly what they need and exactly how close they are** —
never a mystery, never a surprise ambush, never a hidden countdown. This is
Pillar 2 ("Clever, Never Cheated") applied to the moment-to-moment framing
of an entire level, not just to a single swap.

- **Legibility of purpose.** From the instant the Pre-Level Card appears,
  through every HUD chip update during play, the player's objectives and
  progress toward them are always visible, always numeric, always
  literally traceable to a `.tres` file a designer authored (§ Detailed
  Rules 10, Objective Display Model). Sweet Cascade never asks a player to
  guess "am I doing well?" — the fraction on screen always answers that
  question directly.
- **The last-move win is the peak drama moment, not an accident of
  evaluation timing.** Because win/lose is evaluated exactly once, at
  `board_stabilized`, after a move's *entire* cascade sequence has already
  resolved (§ Detailed Rules 8), a single well-placed swap on the player's
  final move can trigger a multi-step chain that completes an objective on
  its second or third clear — and the game correctly recognizes that as a
  win in the same beat the cascade's own visual payoff lands. This is the
  single biggest "clever, not lucky" moment this system can produce, and it
  is a direct, provable consequence of the evaluation-ordering rule (§ Edge
  Cases, "objective completed by a cascade after the winning stabilization
  already triggered").
- **Fair, transparent lose.** Per `game-concept.md`'s Pillar 2 ("Players
  lose because they ran out of good moves, never because the game felt
  rigged"), losing in Sweet Cascade is always the deterministic outcome of
  a visible, authored number (`move_limit`) reaching zero against a visible,
  authored target the player could always see and measure themselves
  against. Nothing about this system's evaluation logic ever reads player
  performance, purchase history, or session history to adjust the outcome —
  the exact same rule applies identically to every attempt.
- **Gentle urgency, never panic.** The low-moves warning state (§ Detailed
  Rules 7) exists to build tension without implying manipulation —
  `art-bible.md`'s own semantic-accent design intent for this exact state
  reads "gentle urgency, never panic or implied manipulation (Pillar 2)."
  This document's `is_low_moves` predicate is the precise, inspectable rule
  behind that promise.
- **A loss that teaches, not punishes.** `game-concept.md`'s Recovery from
  Failure design goal is that "failure shows 'closest miss' feedback so it
  feels educational, not punishing." This system is the source of truth for
  what "close" means — the per-objective final progress state this
  document supplies at resolution (§ Detailed Rules 9) is the raw material
  a closest-miss summary is built from, wherever that summary is ultimately
  composed and rendered.

---

## Detailed Rules

### 1. Scope Boundary & MVP Objective Roster

This document owns exactly two runtime concerns: **objective progress
tracking** and **move-limit enforcement**, both evaluated purely from
signals Match-3 Board Engine already publishes (`board-engine.md` § Detailed
Rules 7) plus one small synchronous seam into Scoring & Star Thresholds (§
Detailed Rules 3). It owns no board mutation, no scoring math, no star
computation, and no rendering — those remain, respectively, Board Engine's,
Scoring's, Scoring's, and Game UI/Screens Flow's / Juice Layer's.

MVP ships exactly the two objective types `level-data-format.md` §2 already
defines as v1's closed enum:

| `type` | Runtime tracking source | Completion test |
|---|---|---|
| `score_target` | Scoring & Star Thresholds' live running score, via the score-provider seam (§ Detailed Rules 3) | `current_score >= target` |
| `collect_color` | Board Engine's `match_cleared.cleared_pieces`, tallied per the unified counting rule (§ Detailed Rules 4) | `tally >= count` |

A level's `objectives` array may contain any combination of these two types,
including duplicates of the same type with different parameters (e.g., two
separate `collect_color` entries for two different colors) — each array
entry gets its own independent tracker instance, keyed by array index, never
by type (§ Edge Cases).

**Not MVP scope**: blocker-clear objectives, bring-down-ingredient
objectives, and any objective type keyed to Booster Brewing Meta's future
ingredient-harvest yield. `level-data-format.md`'s Out-of-Scope table names
the extension points these would use (a `blocker_layer` field, a
`schema_version` bump); this document does not design their runtime
semantics, only the registry mechanism a future type would plug into (§
Detailed Rules 11).

### 2. Objective Tracker Model & Field Normalization

At every fresh bootstrap (`board_bootstrapped`, fired at the end of Board
Engine's Bootstrapping state — see § Detailed Rules 9 for exactly which
Screen Flow transitions trigger this), Level Objective initializes one
**tracker** per entry in the level's `objectives` array, in array order:

```
Tracker = {
    objective_index: int,       // 0-based position in objectives[]
    type: String,                // "score_target" | "collect_color"
    params: Dictionary,          // the objective's own authored params, passed through verbatim for display (§ Detailed Rules 10)
    target_value: int,           // normalized — see field-name note below
    current: int,                // starts at 0 for every objective, every attempt
}
```

**Field-name normalization (a deliberate, explicitly-flagged gotcha).**
`level-data-format.md`'s two objective types do **not** use the same
parameter name for "the number to reach": `score_target` uses
`params.target`, `collect_color` uses `params.count`. At tracker
initialization, both are read into the single normalized field
`target_value` above — every downstream rule in this document (progress
fraction, completion predicate, win evaluation) operates on `target_value`
and never re-reads `params.target`/`params.count` directly, so the type-name
divergence is resolved exactly once, at exactly one point, rather than
leaking into every formula that follows.

**Reset discipline.** Every tracker's `current` field always starts at
literal `0` on every bootstrap — including a Restart, a Retry, or advancing
to a fresh level via Next Level (`screen-flow.md`'s T4/T11/T17/T18, all of
which re-trigger Board Engine's full bootstrap procedure and therefore a
fresh `board_bootstrapped`). Level Objective holds no state that survives
across attempts; it is fully re-initialized every time, mirroring RNG
Service's own `attempt_number` reset discipline (`rng-service.md`,
"Resets to 1 only on fresh level entry from the map").

### 3. `score_target` Tracking — The Score-Provider Seam

`score_target`'s `current` value is never computed by this document — it is
always the live, authoritative running score Scoring & Star Thresholds
maintains. This document declares, and depends on, exactly one synchronous
query seam:

```
ScoreProvider.get_current_score() -> int
```

A plain, synchronous function call (never a signal) — mirroring the
project's established "board-state query" pattern (`board-engine.md` §
Detailed Rules 8's Board State Query API): always returns the exact live
running total at the instant it is called, never a cached or stale copy.
Level Objective calls this exactly once per relevant evaluation point (§
Detailed Rules 5 — once per `match_cleared` event that could have changed
score, and once more implicitly at `board_stabilized` since the last such
call already reflects the final value by then). **This is the "declared
score-provider seam" both this document and Scoring & Star Thresholds were
scoped against** (`systems-index.md`'s Circular Dependencies note: "Objective
reads the live score to check score-target completion... Scoring never
needs Objective's internal state... resolved as a one-directional read").
This exact function name and signature is now **formally ratified** by
Scoring & Star Thresholds § Detailed Rules 10a (Revision 2) — see this
document's Revision 2 changelog.

### 4. `collect_color` Tracking — The Unified Piece-Removal Counting Rule

**The unified rule, stated plainly:** every piece removed from the board —
whether by an ordinary 3-match, a Striped candy's row/column sweep, a Color
Bomb's activation wipe, a Bomb+Striped or Striped+Striped jackpot combo, or
a passively-caught special's chain-reaction detonation — appears **exactly
once**, carrying its full color identity, in that clearing step's single
`match_cleared.cleared_pieces` array. `collect_color` tallying therefore
needs to subscribe to exactly one signal: `match_cleared`. For every
`PieceSnapshot` entry in every `match_cleared` event's `cleared_pieces`
array (any `chain_index`, any `trigger_source`), every tracker whose
`type == "collect_color"` and whose `params.color` matches that entry's
`color` increments its `current` by exactly `1`.

**Why this is provably complete and never double-counts, cited directly
against Board Engine's own contract:**

1. **Completeness.** Board Engine's Resolution Loop enters `Clearing`
   "whenever Matching found ≥1 run, **or** seam 1 triggered an activation"
   (`board-engine.md` § Detailed Rules 6), and within a Clearing state,
   seam 3 (special spawns) and seam 4 (chain-reaction expansion) are both
   applied **before** the clear set is finalized and popped (§ Detailed
   Rules 3, "Resolution order within one cascade step"). Every cell any of
   these mechanisms clears is therefore already inside the *same* finalized
   clear set that step's single `match_cleared` reports — there is no clear
   path in Board Engine or Special Candies that bypasses this event.
2. **No double-counting.** Board Engine's own overlap/intersection union
   rule guarantees "the shared cell is never double-counted or cleared
   twice, and exactly one `match_cleared` signal fires for that step
   regardless of how many individual runs contributed to it" (`board-engine.md`
   § Detailed Rules 4). The "Seam-1-and-match coexistence" rule (§ Detailed
   Rules 5) confirms the same discipline extends to activation clears: when
   a swap both matches *and* activates, "the step's clear set is the union
   of both... nothing is lost to either path" — one union, one
   deduplicated set, one signal.
3. **`special_activated`'s `cleared_pieces` is deliberately not
   consumed for tallying.** `special_activated` fires *before* that same
   step's `match_cleared` and reports only seam 2's own contribution to
   the clear set — a subset of (or, in a pure-activation case, identical
   to) what the immediately-following `match_cleared(chain_index=1)` for
   that same step reports in full. Summing both signals' `cleared_pieces`
   for the same step would double-count every cell seam 2 cleared. Level
   Objective's tally therefore consumes **`match_cleared` only** — this is
   the entire unified counting rule, stated as an implementation
   constraint, not merely a design intent.

**Bootstrap-triggered clears count too — a deliberate decision, stated
explicitly.** `match_cleared` events with `trigger_source = BOOTSTRAP` (Board
Engine's own accidental-match cleanup pass, § Detailed Rules 2 step 8) are
**not** filtered out of `collect_color` tallying. `level-data-format.md` §2
defines `collect_color`'s win condition as "cumulative tiles of `color`
cleared (via match or special) `>= count`" with no `trigger_source`
carve-out, and a piece genuinely leaves the board with that color regardless
of what triggered its removal. This is a real, if rare, source of
"free" progress at level start — its edge-case consequence (a level that
could theoretically win before the first player swap) is addressed
explicitly in § Edge Cases, not treated as an oversight.

### 5. Progress Event Emission (`objective_progressed`)

Internal tracker state (`current`) updates **synchronously and instantly**,
within the same logic frame Board Engine's own signals fire in — there is
no delay anywhere in this document's own logic, exactly mirroring Board
Engine's "logic resolves instantly; presentation paces reveals" contract
(`board-engine.md` § Detailed Rules 13). What *is* deliberately paced is the
**signal emission granularity** a presentation-layer consumer (HUD, Juice
Layer) reads from — this is the "same deferred-replay discipline as
Scoring" the task requires, applied to objective progress specifically:

- `objective_progressed(objective_index, type, current, target, progress_fraction, completed)`
  fires **once per `match_cleared` event, for every tracker that event's
  clear set affected** — a `collect_color` tracker whose target color
  appears among that step's `cleared_pieces`, or a `score_target` tracker
  (queried fresh via `get_current_score()` on every `match_cleared`, since a
  clearing step is assumed to always be scoring-relevant).
- This means a multi-step cascade produces a **sequence** of
  `objective_progressed` events — one per `chain_index` — each carrying the
  progressively-updated total, not one event carrying only the final
  number. A consumer replaying a move's signal stream at its own pace (the
  same deferred-replay model `board-engine.md` § Detailed Rules 13
  guarantees for `cleared_pieces` payload sufficiency) can therefore animate
  a HUD progress bar climbing in lockstep with each visual clear, instead of
  jump-cutting straight to the move's final total.
- `objective_progressed` never fires for a tracker unaffected by a given
  step (e.g., a `collect_color(color="grape")` tracker does not re-emit on
  a step that only cleared `strawberry`), keeping the emission stream free
  of redundant no-op events.
- This signal is purely informational for presentation/HUD purposes — **it
  never drives win/lose evaluation**, which is reserved exclusively for
  `board_stabilized` (§ Detailed Rules 8).

### 6. Move Accounting — Consumption Rules & `moves_remaining`

Level Objective subscribes to exactly two Board Engine signals for move
accounting: `swap_accepted` and (implicitly, by *not* subscribing to it)
`swap_rejected` is confirmed to have zero effect. The consumption rule,
confirmed directly against `board-engine.md`'s own contract, is:

| Event | Board Engine signal | Moves consumed | Citation |
|---|---|---|---|
| Valid swap producing a color match | `swap_accepted(trigger_source=SWAP_MATCH)` | **1** | "If the swap is valid, one move is considered consumed (via the `swap_accepted` signal)..." (§ Detailed Rules 5) |
| Valid swap via special activation (e.g., Bomb+Color, Striped+Striped), whether or not it also matches | `swap_accepted(trigger_source=SPECIAL_ACTIVATION)` | **1** | Same rule — `is_valid_swap = is_adjacent AND (would_match OR is_special_swap)` (Formula 2); both `SWAP_MATCH` and `SPECIAL_ACTIVATION` are the only two values `swap_accepted.trigger_source` ever carries |
| Invalid swap (no match, no activation) — instant, silent revert | `swap_rejected(reason=NO_MATCH_NO_ACTIVATION)` | **0** | "Invalid swap (revert)... zero net grid change, zero move consumed..." (§ Detailed Rules 5) |
| Structurally invalid swap (non-adjacent, out of bounds, void/empty cell) | `swap_rejected(reason=NOT_ADJACENT)` | **0** | Same revert path, defensive precondition (§ Detailed Rules 5) |
| Mid-game reshuffle (board had zero legal moves) | `board_reshuffled` | **0** | "This reshuffle **never** consumes a player move and is invisible to Level Objective & Move-Limit System's move counter." (§ Detailed Rules 11) — Board Engine's own text already assumes this document's behavior |
| Bootstrap's own accidental-match cascade (§ Detailed Rules 2 step 8) | *(no `swap_accepted`/`swap_rejected` at all — bootstrap never calls `swap_request`)* | **0** | No move-accounting signal fires; nothing to consume |

`moves_remaining` initializes to `move_limit` (Level Data Format field) at
every bootstrap and decrements by exactly `1`, **synchronously and
immediately upon `swap_accepted`** — before that move's cascade even begins
to resolve, matching Board Engine's own framing that the move is
"considered consumed" at the instant `swap_accepted` fires, not after the
cascade settles. Level Objective emits
`moves_remaining_changed(moves_remaining, moves_used, move_limit,
is_low_moves)` once per `swap_accepted` (§ Formula 5 formalizes the
consumption function; `moves_used = move_limit - moves_remaining`).

### 7. Low-Moves Warning State

`is_low_moves` (Formula 6) is `true` whenever `moves_remaining` is at or
below `LOW_MOVES_THRESHOLD` (Tuning Knobs, default `3`) **and** not every
objective is already complete. The default threshold and its exact
semantics — "≤3 moves, objective incomplete" — are taken directly from
`art-bible.md`'s own already-drafted (PROPOSED) semantic UI accent
definition: a subtle amber pulse (`#d97b2e`) on the move counter only,
"gentle urgency, never panic or implied manipulation (Pillar 2)." This
document is the authoritative source of the boolean condition that art
bible's HUD state consumes; the color, pulse animation, and rendering
remain entirely Game UI/Screens Flow's and Juice Layer's territory.
`moves_remaining_changed`'s `is_low_moves` field is recomputed on every
emission (§ Detailed Rules 6).

### 8. Win/Lose Evaluation — Timing, Predicate & Precedence

**Evaluated only at `board_stabilized`, and at no other point.**
`board_stabilized` is Board Engine's own guarantee that "the full loop for
one triggering event — a swap, a special activation, or bootstrap —
returns to `Idle`" (`board-engine.md` § Detailed Rules 7) — meaning every
cascade step, every chain expansion, and any mid-move reshuffle for that
triggering event has already fully resolved before this signal ever fires.
Level Objective performs exactly one evaluation per `board_stabilized`:

1. **Win check first.** `win_condition_met` (Formula 3) — the logical AND
   of every tracker's completion predicate — is evaluated using each
   tracker's `current` value as it stands at this exact instant (already
   fully up to date, since every `match_cleared` of this move necessarily
   fired, and was already processed by § Detailed Rules 4/5's synchronous
   handlers, *before* `board_stabilized` could fire). If `true`: **outcome
   = WIN**, resolution ends here.
2. **Lose check second, only if win did not fire.** If
   `moves_remaining == 0`: **outcome = LOSE**.
3. **Otherwise: no outcome.** The level continues; Level Objective returns
   to awaiting the next `swap_accepted`/`match_cleared` sequence. No event
   fires for this stabilization beyond the already-emitted
   `objective_progressed`/`moves_remaining_changed` signals from earlier in
   the move.

**This ordering is what makes "win wins" on a simultaneous win-condition-met
AND moves-exhausted stabilization** (§ Edge Cases) — the win check is
always evaluated first, unconditionally, every time.

**This ordering is also what lets a final-move cascade win the level.**
Because the entire cascade (however many `chain_index` steps it contains)
completes before `board_stabilized` ever fires, and win evaluation reads
`current` values that are already fully updated by that point, a player's
last legal move producing a 3-step chain that only satisfies a
`collect_color` target on its third clear still resolves to WIN at that
move's single `board_stabilized` — not to LOSE because "the move that
finally hit zero moves happened first." Move consumption (§ Detailed Rules
6) and win/lose evaluation (this section) are deliberately decoupled in
timing: consumption happens the instant the swap is accepted;
evaluation happens only after the full cascade settles.

### 9. Level Objective's State Machine & the `level_resolved` / `ResultsData` Handoff

Level Objective holds a minimal, two-state machine per attempt:

| State | Entry | Behavior |
|---|---|---|
| `Tracking` | Every fresh bootstrap (`board_bootstrapped`) | Active: processes `match_cleared`, `swap_accepted`, and `board_stabilized` per §§ Detailed Rules 4–8 |
| `Resolved` | The `board_stabilized` at which § Detailed Rules 8 determines an outcome | Terminal for this attempt: `level_resolved` has already fired (below); every further Board Engine signal for this attempt is ignored (a defensive guard, mirroring Board Engine's own "silently dropped and logged as a warning" policy for out-of-contract seam responses, `board-engine.md` § Detailed Rules 3) |

**The `level_resolved` event — this document owns firing it.** Per this
document's own charter ("it consumes board events, tracks objective
progress and moves, and declares the level resolved"), Level Objective is
the system that fires `level_resolved`, resolving `screen-flow.md`'s own
ambiguous attribution ("Fired by the future Level Objective / Scoring
systems," T15/T16) in favor of Level Objective as the single emitter. The
signature matches `screen-flow.md`'s already-declared consumption contract
exactly:

```
level_resolved(outcome: enum{WIN, LOSE}, results_data: ResultsData)
```

**Constructing `results_data` — Level Objective is the sole assembler
(Revision 2 reconciliation).** Scoring & Star Thresholds still owns
*defining* the field names, types, and computation of every score-side
`ResultsData` field (`score_earned`, `stars_earned`,
`closest_miss_summary.score_progress_ratio`/`score_progress_percent`) —
this document never invents or recomputes those. What changed is who
*assembles* the record: Level Objective, not Scoring, composes the complete
`ResultsData` and fires `level_resolved` with it. This document's own,
narrower, fully-owned resolution summary remains the input that
composition uses for the objective-completion side:

```
ObjectivesResolution = {
    outcome: enum{WIN, LOSE},
    objectives_final: Array[ObjectiveResult],   // one per tracker, final state
    moves_used: int,
    moves_remaining: int,
}

ObjectiveResult = {
    objective_index: int,
    type: String,
    params: Dictionary,        // passthrough of the objective's authored params
    current: int,               // final, UNCAPPED value — may exceed target_value (§ Edge Cases)
    target_value: int,
    progress_fraction: float,   // clamped, see Formula 1
    is_complete: bool,
}
```

At the exact `board_stabilized` instant an outcome is determined, Level
Objective calls Scoring & Star Thresholds' ratified pull seam:

```
ScoreProvider.get_score_results() -> ScoreResults
```

(`scoring-stars.md` § Detailed Rules 10a; `ScoreResults = {final_score,
stars_earned, score_progress_ratio, score_progress_percent}` — every field
Scoring owns and computes.) Level Objective then composes `ResultsData`
itself:

```
ResultsData = {
    level_id: String,                     // pass-through, screen-flow.md §11
    outcome: enum{WIN, LOSE},              // = this ObjectivesResolution's outcome (Formula 4)
    score_earned: int,                     // = ScoreResults.final_score
    stars_earned: int,                     // = ScoreResults.stars_earned
    closest_miss_summary: {
        score_progress_ratio: float,       // = ScoreResults.score_progress_ratio
        score_progress_percent: int,       // = ScoreResults.score_progress_percent
        // objective-completion dimension: shape TBD (still open — see
        // Open Questions), composed from this attempt's own objectives_final
    },
}
```

and emits `level_resolved(outcome, results_data)` using this self-assembled
record — never a value passed through verbatim from Scoring, unlike the
dropped push model. This drops the previously-proposed
`ScoreProvider.finalize_results(objectives_resolution: ObjectivesResolution)
-> ResultsData` seam entirely: Scoring never receives `ObjectivesResolution`
and never assembles `outcome` or any part of `closest_miss_summary` it has
no visibility into (`scoring-stars.md` § Detailed Rules 8's declared scope
boundary). This also resolves `screen-flow.md` §11's `closest_miss_summary`
seam-ownership attribution exactly as originally declared there (Level
Objective) — no correction to `screen-flow.md` is needed. **Both
`get_current_score()` (§ Detailed Rules 3) and `get_score_results()`
(above) are now formally ratified contracts**, confirmed in
`scoring-stars.md` § Detailed Rules 10a (Revision 2) — see this document's
Revision 2 changelog.

**Session pacing decision: end immediately at the objective-completing
stabilization; no leftover-move play-out at MVP.** When
`win_condition_met` becomes `true` at a `board_stabilized` with moves still
remaining, the level ends **at that stabilization** — the player's
remaining `moves_remaining` are never spent, and no mechanism converts them
into bonus score at this document's level. This is a deliberate choice
between two genre patterns the task named explicitly: the concept
prototype's own simplified behavior (always play to `move_limit`
exhaustion) versus Candy Crush Saga's convention (end the instant the
objective completes). **This document adopts the Candy Crush convention**,
justified by:

1. **Session pacing.** Sweet Cascade's flow-state design intent
   (`game-concept.md`) favors tight, legible sessions on mobile; playing out
   moves that no longer affect the outcome is dead time with no stakes.
2. **The last-move-win drama moment (Player Fantasy) requires this.** If the
   level always played to `move_limit` exhaustion regardless of objective
   completion, there would be no meaningful "did my last cascade just win
   it?" moment — the win would already be a foregone conclusion several
   moves earlier, undercutting exactly the peak-drama beat this document's
   Player Fantasy section names as a deliberate design goal.
3. **Pillar 2 clarity.** The instant of victory is unambiguous and
   immediately communicated — no window where the player is technically
   "done" but the game keeps demanding more taps before confirming it.

This decision has one open interaction this document does not resolve
unilaterally: whether Scoring & Star Thresholds wants an "unused moves"
bonus-score formula that would reward not needing every move — flagged as
an Open Question for Scoring to decide, since any such formula is entirely
Scoring's mathematical territory, not this document's.

### 10. Objective Display Model (Pre-Level Card + HUD Chips)

Both consumption points render from the **same data shape** — this document
supplies data only; rendering, iconography, and layout are Game UI/Screens
Flow's and UX's territory entirely.

```
ObjectiveDisplayModel = {
    objective_index: int,
    type: String,
    params: Dictionary,       // e.g. {color: "strawberry"} for collect_color; {} for score_target
    current: int,
    target: int,               // = target_value, renamed for display-layer clarity
    progress_fraction: float,  // clamped [0.0, 1.0], Formula 1
    completed: bool,
}
```

- **Pre-Level Card** (`screen-flow.md` §2's Pre-Level Card state, populated
  from Level Data Format directly, before Board Engine even bootstraps):
  every tracker initializes with `current = 0`, `progress_fraction = 0.0`,
  `completed = false` — the Pre-Level Card always shows a level's
  objectives at their starting state, in `objectives[]`'s authored array
  order, exactly matching `level-data-format.md`'s own note that "Order is
  preserved and may be used by Game UI/Screens Flow to decide primary-badge
  display order."
- **In-level HUD chips**: updated live from every `objective_progressed`
  emission (§ Detailed Rules 5) during play, and from `moves_remaining_changed`
  for the move counter and its `is_low_moves` state (§ Detailed Rules 7).
- Neither consumption point ever receives a raw `Tracker` (the internal
  representation, § Detailed Rules 2) — always the display-facing,
  de-normalized `ObjectiveDisplayModel`/`objective_progressed` shape, keeping
  the internal `target_value` normalization (§ Detailed Rules 2) fully
  opaque to any presentation-layer consumer.

### 11. Extensibility — Plugging In a Future Objective Type

A future objective type (e.g., a clear-blockers type keyed to a future
Blockers extension of Board Engine, or a bring-down-ingredient type keyed to
Booster Brewing Meta) plugs in through an **Objective Handler Registry** —
an internal, code-side mapping from `objective.type` (string) to a handler
implementing three operations:

```
ObjectiveHandler = {
    initialize(params: Dictionary) -> TrackerState,
    on_event(event: BoardEngineSignal, state: TrackerState) -> TrackerState,
    get_progress(state: TrackerState) -> {current: int, target_value: int, is_complete: bool},
}
```

MVP registers exactly two handlers (`score_target`, `collect_color`, §§
Detailed Rules 3–4). A future type's addition requires:

1. **A `level-data-format.md` schema change** — a new value added to the
   `objectives[].type` closed enum, which per that document's own
   Versioning & Migration Rules is a closed-enum addition and therefore
   **requires a `schema_version` bump** (unrecognized closed-enum values are
   never safely ignorable).
2. **A new `ObjectiveHandler` registration** in this system, keyed to the
   new type string — with **zero changes** to the win-evaluation predicate
   itself (Formula 3), which is generically "AND over every tracker's
   `is_complete`," entirely agnostic to how any individual tracker computes
   that value.
3. **Whatever new signal source the handler needs.** A clear-blockers
   handler, for instance, would need a new blocker-clear signal from a
   not-yet-designed Board Engine extension (`level-data-format.md`'s
   Out-of-Scope table already names the extension point:
   `cell_mask`→per-cell enum, or a parallel `blocker_layer` field) — this
   document takes no position on that signal's shape, only on the fact that
   `on_event()` is where a new handler would consume it.

This mirrors `level-data-format.md`'s own closed-enum-plus-version-bump
discipline exactly, and — like Special Candies & Combo Matrix's Board
Engine seams — keeps the extension point one-directional: a new objective
type never requires this document's win-evaluation core (§ Detailed Rules
8) to change.

---

## Formulas

### Formula 1 — Progress Fraction

**Named expression:**
```
progress_fraction(current, target_value) = clamp(current / target_value, 0.0, 1.0)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `current` | int | `≥ 0`, unbounded above | The tracker's live or final accumulated value (score-provider read or collect_color tally) |
| `target_value` | int | `> 0` (guaranteed by `level-data-format.md` V14/V15) | The normalized completion target (§ Detailed Rules 2's field-name normalization) |
| `progress_fraction` | float | `[0.0, 1.0]` | Clamped display-facing progress ratio |

**Output range**: Clamped to `[0.0, 1.0]` — this is a **display** value
only; it is never used for the completion test itself (Formula 2 uses the
unclamped `current` directly), so an over-target `current` never causes a
display fraction to exceed 100%.

**Worked example**: `current = 23`, `target_value = 20` (a `collect_color`
objective whose target was exceeded by a large cascade) →
`23 / 20 = 1.15` → `clamp(1.15, 0.0, 1.0) = 1.0` (HUD shows 100%, never
115%). The uncapped `current = 23` is preserved separately for
`ObjectiveResult`'s stats field (§ Edge Cases, "collect target exceeded").

---

### Formula 2 — Objective Completion Predicate (Unified Across Types)

**Named expression:**
```
is_complete(tracker) = tracker.current >= tracker.target_value
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `tracker.current` | int | `≥ 0` | Unclamped current value — either a live score-provider read (`score_target`) or an unclamped tally (`collect_color`) |
| `tracker.target_value` | int | `> 0` | Normalized target (§ Detailed Rules 2) |
| `is_complete(tracker)` | bool | `{true, false}` | Per-objective completion state |

**Output range**: Boolean. **Deliberately identical in shape for both MVP
objective types** — the only difference between a `score_target` tracker
and a `collect_color` tracker is how `current` is *computed* (§§ Detailed
Rules 3–4), never how completion is *tested*. This economy is what lets a
future objective type (§ Detailed Rules 11) plug into the exact same
predicate with no special-casing.

**Worked example**: `score_target` tracker, `current = 2,500` (from
`get_current_score()`), `target_value = 2,500` → `2500 >= 2500 = true`.
`collect_color` tracker, `current = 18`, `target_value = 20` →
`18 >= 20 = false`.

---

### Formula 3 — Win Evaluation Predicate

**Named expression:**
```
win_condition_met(trackers) = ⋀_{i=1}^{n} is_complete(tracker_i)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `trackers` | Array[Tracker] | length `n ≥ 1` (guaranteed by `level-data-format.md` V13) | Every objective's tracker, in `objectives[]` array order |
| `n` | int | `≥ 1` | Objective count for this level |
| `is_complete(tracker_i)` | bool | `{true, false}` | Formula 2, per tracker |
| `win_condition_met` | bool | `{true, false}` | Logical AND across every tracker — `true` only if every single objective is complete |

**Output range**: Boolean. With `n = 1` this reduces to a single
`is_complete` check; with `n > 1` every objective must independently
satisfy Formula 2 — this is the direct implementation of
`level-data-format.md` §2's "All objectives in the list must be satisfied
(logical AND)."

**Worked example**: two objectives — `score_target` `is_complete = true`,
`collect_color(strawberry)` `is_complete = false` →
`win_condition_met = true AND false = false` (not yet won). If the second
objective's tally later reaches its target on a subsequent move's cascade,
`win_condition_met = true AND true = true` at that move's `board_stabilized`.

---

### Formula 4 — Level Outcome Resolution (Win/Lose Precedence)

**Named expression:**
```
outcome(trackers, moves_remaining) =
    WIN        if win_condition_met(trackers)
    LOSE       if NOT win_condition_met(trackers) AND moves_remaining == 0
    null       otherwise   (no resolution this stabilization — play continues)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `trackers` | Array[Tracker] | length ≥1 | As Formula 3 |
| `moves_remaining` | int | `[0, move_limit]` | Current move count, per § Detailed Rules 6 |
| `outcome(...)` | enum or `null` | `{WIN, LOSE, null}` | The evaluation result at one `board_stabilized` instant |

**Output range**: A closed 3-value result, evaluated fresh at every
`board_stabilized`. **`WIN` is checked first, unconditionally** — this is
the formal statement of "win wins" on a simultaneous win-condition-met AND
moves-exhausted stabilization (§ Edge Cases): the `LOSE` branch is only
ever reachable when the `WIN` branch's condition has already evaluated
`false`.

**Worked example (simultaneous case)**: a player's final move (`moves_remaining`
about to reach `0`) triggers a cascade whose last clear step both exhausts
`moves_remaining` to `0` **and** completes every objective.
`win_condition_met = true` → `outcome = WIN`, regardless of
`moves_remaining == 0` also being true — the `LOSE` branch is never
evaluated because the function returns at the first matching case.

---

### Formula 5 — Move Consumption Function

**Named expression:**
```
moves_consumed(trigger_source) =
    1   if trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}   // fires via swap_accepted
    0   if trigger_source ∈ {NOT_ADJACENT, NO_MATCH_NO_ACTIVATION}   // fires via swap_rejected
    0   for board_reshuffled (no trigger_source — a distinct signal, never consumes)
    0   for BOOTSTRAP (no swap_accepted/swap_rejected fires at all)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `trigger_source` | enum | `{SWAP_MATCH, SPECIAL_ACTIVATION, NOT_ADJACENT, NO_MATCH_NO_ACTIVATION}` or n/a | The reason value carried on `swap_accepted`/`swap_rejected`, per `board-engine.md` § Detailed Rules 7's Signal Catalog |
| `moves_consumed(...)` | int | `{0, 1}` | This is the full move-consumption matrix (§ Detailed Rules 6), expressed as a function |

**Output range**: Binary, `{0, 1}` — never any other value. `moves_remaining`
after event `e` is always `moves_remaining_before - moves_consumed(e)`.

**Worked example**: a swap between two regular candies that forms no run
and touches no special → `swap_rejected(reason=NO_MATCH_NO_ACTIVATION)` →
`moves_consumed = 0` → `moves_remaining` unchanged. The immediately
following swap, correctly aligning a match-3 → `swap_accepted(trigger_source=SWAP_MATCH)`
→ `moves_consumed = 1` → `moves_remaining` decrements by exactly `1`.

---

### Formula 6 — Low-Moves Warning Predicate

**Named expression:**
```
is_low_moves(moves_remaining, all_objectives_complete) =
    (moves_remaining <= LOW_MOVES_THRESHOLD) AND (NOT all_objectives_complete)
```

| Symbol | Type | Range | Description |
|--------|------|-------|-------------|
| `moves_remaining` | int | `[0, move_limit]` | Current move count |
| `LOW_MOVES_THRESHOLD` | int (tuning constant) | default `3`, safe range `1–6` | Tuning Knobs; sourced from `art-bible.md`'s own PROPOSED "≤3 moves, objective incomplete" semantic-accent spec |
| `all_objectives_complete` | bool | `{true, false}` | `= win_condition_met(trackers)`, Formula 3 |
| `is_low_moves` | bool | `{true, false}` | HUD warning-state gate |

**Output range**: Boolean. The `NOT all_objectives_complete` term
deliberately suppresses the warning once the level is about to resolve WIN
anyway — no reason to flash urgency at a player who has already won.

**Worked example**: `moves_remaining = 3`, `all_objectives_complete = false`
→ `3 <= 3 AND true = true` → warning shown. `moves_remaining = 2`,
`all_objectives_complete = true` (win about to resolve at this
stabilization) → `is_low_moves = false` — no needless warning flash
immediately before the win screen.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|----------|-------------------|-----------|
| Objective completed by a cascade **after** the winning stabilization already triggered | **Structurally impossible.** `board_stabilized` only fires once a move's *entire* cascade (all chain steps, plus any nested reshuffle) has fully resolved (`board-engine.md` § Detailed Rules 6–7); Level Objective evaluates win/lose exactly once per `board_stabilized` and transitions to the terminal `Resolved` state (§ Detailed Rules 9) the instant an outcome is determined, ignoring every subsequent Board Engine signal for that attempt. There is no code path by which a cascade step can occur "after" a `board_stabilized` within the same triggering event, and no further triggering event is possible once `Resolved` is entered. | Proven by Board Engine's own signal-ordering guarantee, not merely asserted — see § Detailed Rules 8. |
| `collect_color` target exceeded (e.g., a big cascade clears 23 of a 20-target color) | `is_complete` uses the unclamped `current` (`23 >= 20 = true`); the HUD-facing `progress_fraction` clamps to `1.0` (100%, never 115%, Formula 1); `ObjectiveResult.current` in the final `level_resolved` payload retains the full uncapped `23` for stats/analytics purposes. | Display legibility (never show >100% on a progress bar) must not destroy the raw stat a future analytics or Booster Brewing Meta harvest tally might want. |
| A "zero-progress possible" level — an objective whose target is authored unreachable within `move_limit` | Not caught by this document, by design. `level-data-format.md` V13–V15 only guarantee an objective's *type* is recognized and its *color* is a member of `color_pool` (or its `score_target` is `> 0`) — they cannot verify achievability against realistic cascade rates, which `level-data-format.md` §4 explicitly defers to a smoke check, not schema validation. This document's predicate (Formula 4) is correct regardless: an unreachable objective simply and correctly resolves LOSE once `moves_remaining` reaches `0`. | Matches `level-data-format.md`'s own closing note: achievability is a content-authoring/smoke-check concern, never a runtime-logic bug. |
| Simultaneous win-condition-met AND moves-exhausted on the same `board_stabilized` | **Win wins.** Formula 4 checks `WIN` unconditionally first; `LOSE` is only reachable when the `WIN` branch already evaluated `false`. | Directly resolves `screen-flow.md`'s own flagged ambiguity ("Screen Flow does not resolve this ambiguity — it strictly trusts the single `outcome` field") — this document is the one that must, and does. |
| Bootstrap's own accidental-match cascade (`trigger_source = BOOTSTRAP`) completes every objective before the player's first swap | **A legitimate, if rare, instant WIN at the very first `board_stabilized`**, with `moves_used = 0`. `collect_color`'s unified counting rule (§ Detailed Rules 4) deliberately does not exempt `BOOTSTRAP`-sourced clears, and win evaluation (§ Detailed Rules 8) applies identically to *every* `board_stabilized`, including the one that follows Bootstrapping. `LOSE` can never fire at this same instant, structurally: `move_limit >= 1` (`level-data-format.md` V12) guarantees `moves_remaining > 0` immediately after bootstrap, so the `moves_remaining == 0` branch of Formula 4 is unreachable here. | Per Pillar 2, the rule is applied transparently and identically everywhere — special-casing bootstrap out of an otherwise-uniform predicate would itself be a form of hidden manipulation the game's own design pillar forbids. Made structurally rare by `level-data-format.md`'s bootstrap retry-until-no-match fill algorithm (only *pre-placed* pieces can coincidentally form a run at bootstrap; RNG-filled cells cannot). |
| An invalid swap (instant silent revert) | Zero effect on `moves_remaining`, zero effect on any tracker's `current`, no `objective_progressed` or `moves_remaining_changed` emission. Level Objective does not subscribe to `swap_rejected` at all — there is nothing for it to do. | Board Engine's own contract: "no move consumed, no score lost, no visible penalty" (§ Player Fantasy). |
| A mid-move reshuffle (the board would return to `Idle` with zero legal moves) | Zero effect on `moves_remaining` and zero effect on every tracker — reshuffle reassigns existing pieces' `(color, special_type)` pairs across cells and "never [clears], spawn[s], [or] place[s] new ones" (`board-engine.md` § Detailed Rules 11), so it produces no `match_cleared` event for `collect_color` tallies to consume. | Explicitly confirmed by Board Engine's own text, which already states this document's expected behavior verbatim. |
| A level's only objective is `collect_color` (no `score_target`) | Formula 3's AND simply ranges over the one tracker that exists; no `score_target` term is present or implied. Score still accrues via Scoring throughout play (gating stars independently), but nothing in this document's win predicate ever references it for such a level. | Mirrors `level-data-format.md`'s own edge case: "Scoring and win-condition are parallel systems... decoupling them is intentional, not a gap." |
| Two objectives of the same `type` in one level (e.g., `collect_color(strawberry)` and `collect_color(grape)`) | Fully supported — each `objectives[]` entry gets its own independent `Tracker`, keyed by `objective_index` (array position), never by `type`. `match_cleared`'s tally-update loop (§ Detailed Rules 4) checks every `collect_color` tracker's own `params.color` independently per cleared piece; a single cleared `strawberry` piece increments only the `strawberry` tracker, never the `grape` one. | `level-data-format.md` places no uniqueness constraint on `objectives[].type`; array-index keying is the only design that correctly supports the schema as authored. |
| An objective's target color never appears in a single cascade for the entire level (structurally rare but legal — a small `color_pool`/aggressive-clearing combination could theoretically starve one color) | The tracker's `current` simply never advances past whatever it accumulated; if `moves_remaining` reaches `0` first, `outcome = LOSE` via the normal Formula 4 path. No special detection or player-facing message beyond the ordinary low-moves/objective-incomplete HUD state. | Not a bug — an unlucky or skilled-but-unlucky board state producing this outcome is exactly what Pillar 2's "lose because you ran out of good moves" is meant to allow, provided the level was authored feasibly (a smoke-check concern, not this document's). |
| A level authored at `move_limit = 1` (the schema floor, `level-data-format.md` V12) | The predicate evaluates identically to any other `move_limit` — a single `swap_accepted` decrements `moves_remaining` to `0`; the following `board_stabilized` evaluates Formula 4 exactly once, resolving WIN if the one move's full cascade completed every objective, LOSE otherwise. No special-case branch is needed for the minimum legal value. | Formula 4's precedence and Formula 5's consumption function are both defined generically over `moves_remaining`, with no implicit assumption of a "typical" `move_limit` magnitude. |
| `level_resolved` is emitted more than once for the same attempt (a defensive/regression concern, not an expected occurrence) | Prevented structurally by the `Tracking → Resolved` state machine (§ Detailed Rules 9): once `Resolved` is entered, every further Board Engine signal for that attempt is ignored, so a second evaluation of Formula 4 can never occur before the next fresh `board_bootstrapped` resets the state machine for a new attempt. | Mirrors Board Engine's own "defensive validation of seam responses" philosophy — a hard invariant is enforced structurally, not merely documented as an expectation. |

---

## Dependencies

| System | Direction | Nature of Dependency |
|--------|-----------|----------------------|
| Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED — Revision 2) | This depends on it | Subscribes to `board_bootstrapped` (tracker reset, § Detailed Rules 2), `swap_accepted`/`swap_rejected` (move accounting, § Detailed Rules 6), `match_cleared` (progress tracking, §§ Detailed Rules 4–5), `board_reshuffled` (confirms zero effect), and `board_stabilized` (win/lose evaluation, § Detailed Rules 8). Consumes **zero synchronous queries** from Board Engine — this document is purely signal-driven against Board Engine's existing contract, requiring **no new Board Engine API surface**. |
| Level Data Format (`design/gdd/level-data-format.md`, APPROVED) | This depends on it | Reads `objectives` (array, type, params — normalized per § Detailed Rules 2) and `move_limit` at every bootstrap to initialize trackers and the move counter. This document is the runtime interpreter `level-data-format.md`'s own Dependencies table names: "Reads `objectives` and `move_limit` to drive runtime win/lose evaluation; owns the runtime semantics of each objective type, while [Level Data Format] owns only the data shape." |
| Special Candies & Combo Matrix (`design/gdd/special-candies.md`, Draft) | **No direct dependency** — zero-coupling by design | Level Objective never calls any Special Candies seam and never subscribes to `special_activated` for tallying purposes (§ Detailed Rules 4 explains why). It depends only on Board Engine's `match_cleared` contract, which Special Candies is itself bound to preserve without modification ("No seam's signature is extended, narrowed, or reinterpreted," `special-candies.md` § Detailed Rules 10). This is the same mechanism Special Candies' own § Detailed Rules 9 (Harvest Observation Point) names as its "zero API changes" guarantee to this document. |
| Scoring & Star Thresholds (`design/gdd/scoring-stars.md`, Revised — Revision 2) | This depends on it — one-directional read, per `systems-index.md`'s Circular Dependencies resolution ("Objective reads the live score... Scoring never needs Objective's internal state") | Two ratified synchronous pull seams: `ScoreProvider.get_current_score() -> int` (§ Detailed Rules 3, continuous use for `score_target` tracking) and `ScoreProvider.get_score_results() -> ScoreResults` (§ Detailed Rules 9, called once per attempt at resolution to pull `final_score`/`stars_earned`/`score_progress_ratio`/`score_progress_percent`). **Reciprocal note fulfilled**: `scoring-stars.md` § Detailed Rules 10a lists both signatures; its Dependencies section lists this document. Replaces the previously-proposed `finalize_results()` push seam, dropped in the Revision 2 reconciliation (see changelog). |
| Game UI/Screens Flow (`design/gdd/screen-flow.md`, Draft) | Depended on by it (forward) | Consumes `level_resolved(outcome, results_data)` (T15/T16, driving the Results Win/Lose transition), `objective_progressed` and `moves_remaining_changed` (HUD chip/move-counter live updates), and the `ObjectiveDisplayModel` (§ Detailed Rules 10) for the Pre-Level Card. **This document fulfills the reciprocal note `screen-flow.md` requested**: "when authored, its Dependencies section must list this document and specify exactly how/when it emits `level_resolved`" — answered in § Detailed Rules 8–9. |
| Booster Brewing Meta (#12, Phase 2, gated, not yet authored) | Depended on by it (forward) | Anticipated to read the same `match_cleared`-derived per-color tally mechanism this document already uses for `collect_color` (per `special-candies.md` § Detailed Rules 9's Harvest Observation Point). `level-data-format.md`'s Open Questions flags whether `collect_color`'s tile-count semantics should formally reconcile with a future ingredient-harvest yield formula — this document's position (§ Detailed Rules 1, 4): they stay **decoupled**. `collect_color` counts raw cleared tiles of a color, full stop; any future yield-multiplier logic (e.g., a Striped clear yielding more ingredient than a plain match) is exclusively Booster Brewing Meta's scope to define on top of the same underlying `match_cleared` event stream, never a change to this document's counting rule. |
| RNG Service (`design/gdd/rng-service.md`, APPROVED) | No dependency | This document consumes zero randomness — every rule (progress tallying, move accounting, win/lose evaluation) is a pure, deterministic function of already-resolved Board Engine signals and Scoring's live score, mirroring Special Candies & Combo Matrix's own "RNG Usage: None" position (`special-candies.md` § Detailed Rules 8) for the same category of reason: determinism and readability directly serve Pillar 2. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|-----------|---------------|------------|---------------------|---------------------|
| `LOW_MOVES_THRESHOLD` | `3` | `1–6` | A higher threshold warns the player earlier (more moves remaining when the amber pulse first appears), giving more time to react but risking the warning feeling premature or nagging on levels with a generous `move_limit`. | A lower threshold delays the warning, preserving a calmer HUD for longer but risking the player being caught off-guard close to `move_limit` exhaustion. `1` is the practical floor — a threshold of `0` would make the warning fire only on the move that has already lost, too late to be actionable. |
| `BOOTSTRAP_CLEARS_COUNT_TOWARD_OBJECTIVES` | `true` (fixed design decision, not runtime-configurable at MVP) | `{true, false}` | N/A — currently hardcoded `true` per § Detailed Rules 4's unified counting rule; listed here as a consciously-chosen lever, not an oversight, should a future design pass want to special-case bootstrap clears out of `collect_color` tallying. | Setting to `false` would require adding a `trigger_source != BOOTSTRAP` filter to § Detailed Rules 4's tally loop — a small, contained change if ever needed, but not adopted at MVP because it would break the uniform-predicate argument in § Edge Cases. |
| `RECOMMENDED_MAX_OBJECTIVES_PER_LEVEL` | `2` (soft authoring guideline, not schema-enforced) | `1–3` | More simultaneous objectives increase level complexity and HUD chip crowding on a portrait mobile screen (`.claude/docs/technical-preferences.md`'s one-handed-play, ≥44px touch-target constraints apply to HUD chips too, per general project convention); useful for late-region variety levels. | Fewer objectives (the schema floor is `1`, per `level-data-format.md` V13) keep early-onboarding levels legible, matching `game-concept.md`'s "one concept at a time" curve. This is a design guideline only — `level-data-format.md`'s V13 does not enforce an upper bound, exactly mirroring that document's own precedent for soft, non-blocking authoring guidance (e.g., its `star_1`/`star_2`/`star_3` spacing guideline). |

---

## Acceptance Criteria

- [ ] A gdUnit4 test suite under `tests/unit/level-objectives/` asserts the
      full move-consumption matrix (§ Detailed Rules 6 / Formula 5): a
      `swap_accepted(trigger_source=SWAP_MATCH)` event decrements
      `moves_remaining` by exactly `1`; a
      `swap_accepted(trigger_source=SPECIAL_ACTIVATION)` event also
      decrements by exactly `1`; a `swap_rejected` event (either `reason`
      value) leaves `moves_remaining` unchanged; a `board_reshuffled` event
      leaves `moves_remaining` unchanged.
- [ ] A unit test asserts the unified `collect_color` counting rule: a
      fixture move producing (a) a plain 3-match, (b) a Striped candy's
      row-clear activation, and (c) a passively-caught Color Bomb's
      chain-reaction detonation — all within the same move's cascade —
      correctly sums every cleared cell of the target color exactly once
      across the move's `match_cleared` events, with **zero double-counting**
      from `special_activated`'s overlapping `cleared_pieces` payload.
- [ ] A unit test asserts a `match_cleared(trigger_source=BOOTSTRAP)` fixture
      correctly increments a `collect_color` tracker's tally before any
      player swap occurs.
- [ ] A truth-table test suite asserts Formula 4's full outcome space: all
      objectives complete + moves remaining > 0 → `WIN`; all objectives
      complete + `moves_remaining == 0` → `WIN` (not `LOSE` — win-precedence);
      not all objectives complete + `moves_remaining == 0` → `LOSE`; not all
      objectives complete + `moves_remaining > 0` → `null` (no resolution,
      play continues).
- [ ] A last-move-cascade win fixture: a level with `move_limit = 1` and a
      `collect_color` objective satisfiable only by a 2-step cascade (the
      target color does not reach its count until `chain_index = 2`) —
      asserts `level_resolved(WIN, ...)` fires exactly once, at the single
      `board_stabilized` that follows the full cascade, with
      `moves_used == 1`.
- [ ] A unit test asserts `level_resolved`'s payload construction
      (Revision 2 — reassigned to the pull/compose boundary): given a mock
      `ScoreProvider.get_score_results()` seam returning a stub
      `ScoreResults`, asserts the emitted `level_resolved` event's
      `ResultsData` argument correctly composes `score_earned`/
      `stars_earned`/`closest_miss_summary.score_progress_ratio`/
      `score_progress_percent` from that stub verbatim, `outcome` from this
      document's own Formula 4 result, and that `objectives_final` (this
      document's own tracker-derived resolution summary) correctly
      reflects every tracker's final `current`/`target_value`/
      `is_complete` state, `moves_used`, and `moves_remaining` — confirming
      Level Objective, not Scoring, performs the assembly.
- [ ] A unit test asserts `ScoreProvider.get_current_score()` is queried
      via a plain synchronous call (never a signal) at every relevant
      evaluation point (§ Detailed Rules 3, 5), and that a mocked
      mid-cascade change in its return value is reflected in the very next
      `objective_progressed` emission for a `score_target` tracker.
- [ ] A unit test asserts `objective_progressed` emission cadence: a
      3-step cascade clearing the tracked color on steps 1 and 3 (not step
      2) emits exactly two `objective_progressed` events for that tracker,
      not three, and each carries the correctly-incremented running
      `current` at that point in the sequence.
- [ ] A unit test asserts Formula 1's clamp behavior: a `collect_color`
      tracker with `current = 23`, `target_value = 20` reports
      `progress_fraction == 1.0` (never `1.15`) while `ObjectiveResult.current`
      in the final resolution payload retains the uncapped `23`.
- [ ] A unit test asserts the `Tracking → Resolved` terminal-state guard: after
      `level_resolved` fires once, a subsequent (test-injected, out-of-contract)
      `match_cleared` event for the same attempt produces no further
      `objective_progressed` emission and no second `level_resolved` emission.
- [ ] A unit test asserts Formula 6's low-moves suppression: given
      `moves_remaining <= LOW_MOVES_THRESHOLD` but `win_condition_met == true`
      at the same evaluation point, `is_low_moves` resolves `false`.
- [ ] A unit test asserts array-index-keyed tracker independence: a level
      with two `collect_color` objectives of different colors correctly
      attributes a single cleared piece's color to only the matching
      tracker, leaving the other tracker's `current` unchanged.
- [ ] No gameplay value this document defines (`LOW_MOVES_THRESHOLD`,
      objective targets, `move_limit` itself) exists as a hardcoded literal
      anywhere in `src/` outside this document's own tuning-constant
      definitions — every level-specific value remains spot-check
      traceable back to a `.tres` file, per `coding-standards.md`'s
      data-driven rule.

---

## Cross-References

| This Document References | Target GDD | Specific Element Referenced | Nature |
|---------------------------|-----------|------------------------------|--------|
| `objectives`/`move_limit` field shapes, closed-enum param names (`target` vs. `count`) | `design/gdd/level-data-format.md` | §2 Schema v1 Field Reference; V13–V15 | Data dependency |
| `move_limit`'s runtime-ownership handoff | `design/gdd/level-data-format.md` | Dependencies table row for Level Objective & Move-Limit System (#7) | Ownership handoff, reciprocated here |
| `collect_color` vs. Booster Brewing Meta ingredient-yield decoupling | `design/gdd/level-data-format.md` | Open Questions — "Should `collect_color`'s tile-count semantics formally reconcile with Booster Brewing Meta's future ingredient-harvest yield..." | **Resolved by this document** (§ Dependencies, Booster Brewing Meta row): stays permanently decoupled |
| `swap_accepted`/`swap_rejected` move-consumption semantics, `board_reshuffled`'s zero-move-cost guarantee | `design/gdd/board-engine.md` | § Detailed Rules 5 (Swap Rules), § Detailed Rules 11 (Reshuffle Policy) | Rule dependency |
| `match_cleared`'s deduplicated, unioned `cleared_pieces` payload; `PieceSnapshot`'s deferred-replay sufficiency guarantee | `design/gdd/board-engine.md` | § Detailed Rules 4 (overlap/union rule), § Detailed Rules 7 (Signal Catalog), § Detailed Rules 13 (worked 2-step cascade walkthrough) | Rule dependency — the unified counting rule's formal proof |
| `board_stabilized`'s "full loop returns to `Idle`" guarantee | `design/gdd/board-engine.md` | § Detailed Rules 6 (Resolution Loop), § Detailed Rules 7 (Signal Catalog) | Rule dependency — the win/lose evaluation-timing proof |
| No direct seam call into Special Candies; zero-coupling via Board Engine's preserved signal contract | `design/gdd/special-candies.md` | § Detailed Rules 9 (Harvest Observation Point), § Detailed Rules 10 (Seam Implementation Summary — "No seam's signature is extended, narrowed, or reinterpreted") | Rule dependency, confirms zero-coupling |
| `level_resolved(outcome, results_data)` signature; `ResultsData`'s declared field table; `closest_miss_summary`'s seam-ownership attribution | `design/gdd/screen-flow.md` | §11 Declared Seams; T15/T16 transition table | Ownership handoff — this document fires the event **and assembles the full `ResultsData` record** (Revision 2 reconciliation, § Detailed Rules 9), pulling `score_earned`/`stars_earned`/the score dimension of `closest_miss_summary` from Scoring via the ratified `get_score_results()` seam (`scoring-stars.md` § Detailed Rules 10a). Confirms `screen-flow.md`'s original `closest_miss_summary` attribution to this document — no correction needed there. |
| `attempt_number` reset/increment policy underlying every tracker-reset instant | `design/gdd/screen-flow.md` | §7 (`attempt_number` supply); `design/gdd/rng-service.md` (attempt-lifecycle policy) | Rule dependency |
| `LOW_MOVES_THRESHOLD` default value and rationale | `design/art/art-bible.md` | Semantic UI Accents table — "Warning / low moves," `#d97b2e`; Board/HUD States table — "Low-moves warning (≤3 moves, objective incomplete)" | Data dependency — this document's default matches an already-drafted PROPOSED spec exactly |
| Pillar 2 ("ran out of good moves, never rigged"), Recovery from Failure ("closest miss... educational, not punishing") | `design/gdd/game-concept.md` | Pillar 2 section; Player Journey → Recovery from Failure | Design-intent dependency |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|----------|-------|----------|-----------|
| Confirm (or amend) the exact names/signatures of the two proposed Scoring seams: `ScoreProvider.get_current_score() -> int` and `ScoreProvider.finalize_results(objectives_resolution) -> ResultsData`. | systems-designer | At Scoring & Star Thresholds (#6) authoring/review | **Resolved (Revision 2).** `get_current_score() -> int` ratified as proposed. `finalize_results()` dropped; replaced by `get_score_results() -> ScoreResults`, ratified in `scoring-stars.md` § Detailed Rules 10a. See this document's Revision 2 changelog. |
| Does Scoring & Star Thresholds want an "unused moves" bonus-score formula, given this document ends the level immediately at the objective-completing `board_stabilized` with no leftover-move play-out at MVP (§ Detailed Rules 9)? If so, does it need any additional data from `ObjectivesResolution` beyond `moves_remaining`? | game-designer / systems-designer | At Scoring & Star Thresholds (#6) authoring | — |
| `screen-flow.md` §11 currently attributes `closest_miss_summary`'s seam ownership to this document ("Level Objective & Move-Limit System (#7)"), while this document positions the underlying *metric* as more naturally Scoring's judgment call (it may need to weigh score-closeness against color-tally-closeness across multiple objective types). | systems-designer | At Scoring & Star Thresholds (#6) authoring, with a follow-up correction to `screen-flow.md` | **Resolved (Revision 2).** Level Objective remains `closest_miss_summary`'s sole assembler, composing Scoring's pulled score-progress fields (via `get_score_results()`) with its own objective-completion data (`objectives_final`). `screen-flow.md`'s original attribution to this document is confirmed correct; no correction needed there. See § Detailed Rules 9 and this document's Revision 2 changelog. The exact shape of the objective-completion dimension itself remains a separate, still-open question (see next row). |
| Blocker-clear and bring-down-ingredient objective types (§ Detailed Rules 11's named-not-designed extension seam) — no concrete design exists yet for either the Board Engine signal source or the `ObjectiveHandler` implementation. | game-designer | Revisit when a dedicated Blockers concept or Level Progression content need is scoped (post-MVP) | — |
| Should `is_low_moves`'s suppression-on-already-won behavior (Formula 6) extend to a symmetric suppression once `outcome == LOSE` is already determined, or is that moot because `level_resolved` fires in the same instant and the HUD is expected to transition away immediately? | game-designer / UI | At Game UI/Screens Flow's HUD chip visual implementation pass | — |
| `RECOMMENDED_MAX_OBJECTIVES_PER_LEVEL = 2` is a soft authoring guideline, not schema-enforced. Should `level-data-format.md`'s validation suite (V-rules) eventually gain an Advisory rule mirroring this, the way V18 advises on `star_3_score`? | systems-designer | At Vertical Slice content-authoring retrospective, once real multi-objective levels exist to evaluate against | — |
