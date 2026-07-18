# Scoring & Star Thresholds

*Status: Reviewed — APPROVED (re-review, 2026-07-18)*
*Cross-GDD sync, 2026-07-18: extended the `final_score = 0` Edge Cases row
to cover the legitimate bootstrap-cascade instant-WIN path at
`moves_used = 0`.*
*Created: 2026-07-18*
*Last Updated: 2026-07-18*
*Layer: Feature · Priority: MVP · Phase: MVP · Category: Progression*
*Author: systems-designer*
*Depends On: Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED — Revision 2), Special Candies & Combo Matrix (`design/gdd/special-candies.md`, Draft), Level Data Format (`design/gdd/level-data-format.md`, APPROVED — v1; this document proposes a Tuning Knobs/V18 update, see Cross-References)*
*Depended On By: Level Objective & Move-Limit System (`design/gdd/level-objectives.md`, Revised — Revision 2 — pulls this document's ratified Score Query API, § Detailed Rules 10a), Juice Layer — VFX & Audio Hooks (#8, not yet authored), Game UI/Screens Flow (`design/gdd/screen-flow.md`, Draft — indirectly contributes to its declared `ResultsData` seam via Level Objective's assembly), Level Progression / World Map (`design/gdd/world-map.md`, Draft — soft/indirect, reads only persisted `best_stars`), Booster Brewing Meta (#12, Phase 2, gated) (all per `design/gdd/systems-index.md`)*
*Source: `design/gdd/systems-index.md` · `design/gdd/board-engine.md` §§ Detailed Rules 6–7, 13, Formula 5–6 · `design/gdd/special-candies.md` §§ Detailed Rules 1, 5–6, Formulas 1, 4–8 · `design/gdd/level-data-format.md` §2, §4 (V16–V18), Formula A–B · `prototypes/sweet-cascade-concept/REPORT.md` · `design/gdd/screen-flow.md` § Detailed Rules 11 · `design/gdd/game-concept.md` Pillar 2, Flow State Design*

*Revision 2 Changelog (2026-07-18):* Dropped the proposed
`ScoreProvider.finalize_results()` push seam. Ratified the pull/compose
model: this document now exposes `get_current_score() -> int` and
`get_score_results() -> ScoreResults` (§ Detailed Rules 10a); Level
Objective & Move-Limit System (#7) is the sole `ResultsData` assembler,
pulling both seams and composing the full record itself. Resolves
`scoring-stars-review-log.md` Required Before Implementation #1 (blocking
seam-composition mismatch) and Recommended Revisions #1 (unratified
`get_current_score()`); mirrored in `level-objectives-review-log.md`
Required Before Implementation #1 and Recommended Revisions #1.

---

## Overview

Scoring & Star Thresholds is the Feature-layer system that converts Match-3
Board Engine's and Special Candies & Combo Matrix's already-resolved event
stream into the two numbers every downstream system treats as ground truth:
a level's final score and its 0–3 star rating. It owns exactly three things:
the authoritative point formula — a per-tile base value, escalated by a
linear chain multiplier, plus a single unified per-piece special-activation
bonus rule that prices every cell of Special Candies' combo matrix without a
second, independently-authored lookup table; the star-threshold-setting
framework designers use to derive `star_1_score`/`star_2_score`/`star_3_score`
for any level from its `move_limit` and `color_pool` size, superseding Level
Data Format's provisional flat `REFERENCE_SCORE_PER_MOVE = 160` constant with
a value derived per color-pool size (cascade frequency modeled honestly from
match probability, for the schema-legal 3/4/5-color range); and the
closest-miss metric and Score Query API (§ Detailed Rules 10a) that supply
Level Objective & Move-Limit System's `ResultsData` assembly for Game
UI/Screens Flow's declared seam. Every
formula in this document is a pure, deterministic function of signals Board
Engine and Special Candies already emit — this document consumes zero RNG
and reads exactly one signal (`match_cleared`) to compute score. Score
accrues logically the instant an event resolves; how that accrual is
*displayed* on screen (a counting-up HUD number, floating popups) is Juice
Layer's presentation concern — this document only defines what number is
correct at each instant, never how it is revealed.

---

## Player Fantasy

Scoring & Star Thresholds exists to make two promises mechanically true:
**every point on screen was earned, and every threshold was set on purpose.**

**Score as applause, not accounting.** Per `game-concept.md`'s Core Fantasy —
"the game amplified [the player's clever swap] into something spectacular" —
a score number is the game's own hands clapping. The chain multiplier
(Formula 1) makes a deep cascade's score visibly, proportionally bigger than
a shallow one, in lockstep with the exact `chain_index` the Juice Layer's
escalating "Sweet! ×2 → Delicious! ×4" callouts already display
(`board-engine.md` § Detailed Rules 6) — the number on screen and the label
the player reads are always the same integer, never a hidden translation the
player has to trust. Building a match-4 or match-5 and later firing it, or
combining two specials into a jackpot, pays out a visibly larger bonus than
an ordinary match-3 (Formula 2's Activation Bonus) — the escalation ladder
`special-candies.md`'s Player Fantasy describes ("spot-and-build," "the
bigger reach," "the jackpot") has a matching escalation in the number that
appears when each rung pays off, so the *feeling* of "I set that up" and the
*number* that rewards it never disagree.

**Thresholds that invite one more try, never demand it.** Star thresholds are
the mechanical backbone of `game-concept.md`'s "three-starring old levels"
retention hook and Level Progression / World Map's entire unlock economy
(`world-map.md` Formula 1). This document's star-threshold framework
(Formula 6) sets `star_1_score` at a genuinely completion-feasible baseline —
reaching it should feel like "I won," not "I need to grind" — while
`star_2_score` and `star_3_score` climb toward a percentile a skilled,
deliberate player can reach through better play, not luckier RNG. A 3-star
level is meant to feel like a real accomplishment worth bragging about, never
an unfair ask; the framework's fractions are validated end-to-end against the
concept prototype's own measured greedy-bot ceiling (§ Formulas, Formula 6)
so "near-optimal" has a real, checkable number behind it, not a designer's
guess.

**Honest difficulty, inspectable math.** Per Pillar 2 ("Clever, Never
Cheated"), a star threshold in Sweet Cascade is never an arbitrary number a
designer typed in to make a level feel harder — it is the deterministic
output of a formula keyed off two already-authored, player-invisible-but-
inspectable level properties (`move_limit`, `color_pool` size). Two levels
with the same move count and color pool get proportionally comparable
thresholds; a level that *feels* stingier always traces back to a visible
authored lever (fewer moves, more colors), never to a hidden per-level
fudge factor.

**"So close" must be true.** Per `game-concept.md`'s Flow State Design
("failure shows 'closest miss' feedback so it feels educational, not
punishing"), the closest-miss metric this document defines (Formula 8) is
built to be emotionally honest, not just softening copy — it measures actual
proximity to the very first star, the threshold that most directly answers
"was I close to something real?" A player told they were "so close" must
have been, in a number they could have closed with one or two better swaps,
never a comforting lie dressed up as a percentage.

---

## Detailed Rules

### 1. The Canonical Scoring Signal — `match_cleared` Only

**Scoring reads exactly one Board Engine signal to compute points:
`match_cleared`.** No other signal in Board Engine's or Special Candies'
catalogs — `special_activated`, `pieces_spawned`, `special_spawned`,
`cascade_step_advanced`, `cascade_ended`, `swap_accepted`,
`board_reshuffled` — contributes a point value of its own. This is a
deliberate, load-bearing design decision, not an oversight, for two
reasons:

1. **Avoiding double-counting.** For a special-activation move
   (`board-engine.md`'s "Ordering guarantee for a special-activation move"),
   `special_activated` fires with `cleared_pieces` covering only what seam 2
   itself returned — *before* seam 3 (spawns) and seam 4 (chain expansion)
   are applied — while the `match_cleared` that immediately follows at
   `chain_index = 1` carries the step's **finalized** clear set: seam 2's
   activation clears, unioned with any coexisting normal match
   (`board-engine.md` § Detailed Rules 5, "Seam-1-and-match coexistence"),
   further expanded by seam 4. `match_cleared.cleared_pieces` is therefore
   always a superset of (or equal to) `special_activated.cleared_pieces` for
   the same step. Summing both would double- (or triple-) count every cell
   `special_activated` also reported.
2. **`match_cleared` alone is already sufficient.** Every
   `PieceSnapshot` in `cleared_pieces` carries that cell's piece identity
   "immediately before it cleared" (`board-engine.md` § Detailed Rules 7) —
   including `special_type`. A cell that was a `COLOR_BOMB` or `STRIPE_H`/
   `STRIPE_V` piece at the instant it cleared shows exactly that in its
   snapshot, regardless of whether it cleared via a direct swap activation
   (seam 1/2), a normal match (a Striped candy matched into a fresh run of
   3+), or a passive chain catch (seam 4). This document's Activation Bonus
   rule (Formula 2) reads this field directly — it needs no other signal to
   correctly price every combo-matrix cell, every solo activation, and every
   passively-caught special, uniformly, regardless of trigger mechanism.

**Concretely, only `match_cleared` events with `trigger_source ∈ {SWAP_MATCH,
SPECIAL_ACTIVATION}` ever contribute score.** `trigger_source = BOOTSTRAP`
events are explicitly excluded (§ Detailed Rules 6). A level's `final_score`
is the running sum of every non-`BOOTSTRAP` `match_cleared` event's
`step_score` (Formula 2) since the current attempt began.

### 2. Per-Piece Activation Bonus — The Unified Combo-Pricing Mechanism

Rather than authoring a second, hand-maintained lookup table keyed by
Special Candies' combo-matrix cells (Bomb+Color, Bomb+Bomb, Bomb+Striped,
Striped+Striped), this document prices **every** special activation —
solo, combo, or passively chain-caught — with one rule: **every
`PieceSnapshot` in a `match_cleared` event's `cleared_pieces` whose
`special_type != SPECIAL_NONE` contributes a flat Activation Bonus, in
addition to its own tile's base value** (Formula 2). Because a combo-matrix
cell like Bomb+Bomb clears both bomb pieces' own cells as part of its clear
set (`special-candies.md` Formula 5), and Bomb+Striped clears both the bomb's
own cell and every Striped-colored cell it sweeps (Formula 7), the combo
matrix's entire pricing structure falls out of this one rule as a
**consequence**, not a second implementation: a step with two activated
`COLOR_BOMB` pieces in its `cleared_pieces` (Bomb+Bomb) automatically prices
at twice a solo bomb's Activation Bonus; a step with one bomb and one stripe
(Bomb+Striped) automatically prices at the sum of both bonuses. § Formulas,
Formula 3 tabulates the resulting derived values for designer/QA reference,
but that table is not an independently-tunable second source of truth — it
is what Formula 2 already produces for each matrix cell.

This uniformity is deliberate: it means a **passively-caught** special (a
Striped candy's row swept up mid-cascade by another special's chain
reaction, or a bomb caught via seam 4's target-color rule,
`special-candies.md` § Detailed Rules 6, Formula 8) is priced **identically**
to a directly swap-activated one. Scoring never needs to know or care how a
special came to activate — only that it did.

### 3. Chain Multiplier — Linear, Not Diminishing

**Decision: the chain multiplier is linear (`chain_multiplier(n) = n`),
uncapped by this document, matching the concept prototype's validated "20
pts-per-tile × cascade multiplier" model (`REPORT.md`, "If Proceeding").**
This was evaluated against a diminishing alternative (e.g., `sqrt(n)` or a
fixed cap such as `min(n, 5)`) and linear was chosen for four reasons:

1. **Legibility with the Juice Layer's own callouts.** `board-engine.md`'s
   `chain_index` is the exact integer the concept prototype's escalating
   "Sweet! ×2 → Delicious! ×4" callouts display. A linear multiplier keeps
   the number the player reads on the callout and the multiplier actually
   applied to their score identical — a diminishing curve would silently
   decouple the two (a "×4" callout that only pays out 2× actual value reads
   as a broken promise, a direct Pillar 2 violation: the game showing the
   player one number while paying another).
2. **Preserving the prototype's validated "best moment."** `REPORT.md`'s
   Lessons Learned names special-×-special chain reactions as the build's
   single best moment. Dampening deep chains' payout specifically to control
   inflation would blunt the exact mechanic the founder playtest flagged as
   most worth protecting.
3. **Inflation is already structurally bounded, without dampening the
   curve.** `board-engine.md` Formula 6 shows `P(depth > 8) ≈ 0.006%` even at
   the game's most match-prone legal configuration (`color_pool = 3`) — deep
   chains are already rare by construction, so linear growth's inflation
   risk is bounded by its own low frequency, not by artificially capping the
   multiplier every player experiences on every ordinary cascade.
4. **A hard ceiling already exists upstream, and this document does not
   duplicate it.** `MAX_CASCADE_DEPTH` (default `20`, `board-engine.md`
   Tuning Knobs) is Board Engine's own termination guarantee — no cascade
   can ever produce a `chain_index` beyond it. This document treats that cap
   as an inherited constant, never re-declaring or re-tuning its own
   separate ceiling (§ Detailed Rules 9, Formula 9's anti-inflation bound).

`CHAIN_MULTIPLIER_CAP` is named as a **reserved, currently-unset Tuning
Knob** — a documented, ready-to-flip lever (e.g., `min(n,
CHAIN_MULTIPLIER_CAP)`) in case Vertical Slice telemetry reveals real
top-end score inflation the `MAX_CASCADE_DEPTH`-derived bound (Formula 9)
alone doesn't adequately address. It is not enabled at MVP.

### 4. Score Accrual & Display Boundary (the Juice Seam)

**Score accrues logically the instant its `match_cleared` event fires —
synchronously, within Board Engine's own resolution frame
(`board-engine.md` § Detailed Rules 13).** This document's `final_score` is
always the true, complete, already-computed number by the time a move's
`board_stabilized` fires; there is no "pending" or "animating" score state
in this document's own model. Because Board Engine's resolution loop is
fully synchronous, a consumer (the Juice Layer) receives the **complete**
ordered event burst for an entire move before it needs to render anything —
it already knows the true final score delta before it starts animating
toward it.

**The counting-up HUD number and floating score popups are entirely Juice
Layer's presentation concern, not this document's.** This document's
contract ends at: "here is the exact `step_score` this `match_cleared` event
is worth" (Formula 2) — how fast the HUD's visible number ticks upward
toward the new total, whether a popup appears at the swapped cell or the
run's center, and how long it lingers are all `juice-layer.md` (#8, not yet
authored) territory, mirroring the identical logic/presentation split
`board-engine.md` § Detailed Rules 13 already draws for cascade pacing.

**Per-event popup values (what number Juice shows, not how).** For any
`match_cleared` event, the value Juice should render for that step's popup
is exactly `step_score` (Formula 2) — the same number this document sums
into `final_score`. For a `special_activated`-preceded, combo-matrix-driven
step, Juice may want to show its popup at the moment `special_activated`
fires (earlier in the ordered burst than the following `match_cleared`) —
this is a pure event-correlation detail, not a timing problem: because the
full burst is available synchronously before any presentation begins, Juice
can freely look ahead to the immediately-following `match_cleared` sharing
the same `chain_index` to source that step's value, even though this
document treats `match_cleared` as the sole authoritative scoring signal
(§ Detailed Rules 1).

### 5. Move-Remaining End Bonus — Ruled Out for MVP

**Decision: Sweet Cascade's MVP scoring model does not convert leftover
moves into bonus points, unlike Candy Crush Saga's leftover-move
finisher.** This was evaluated and explicitly rejected for MVP, not merely
deferred without reason:

1. **Scoring has no data to compute it with.** Board Engine explicitly does
   not track or enforce a move limit at all (`board-engine.md` § Detailed
   Rules 5: "Board Engine does not track or enforce a move *limit*, only
   reports that a move was spent"). "Moves remaining" is data only Level
   Objective & Move-Limit System (#7, not yet authored) will own. Adding a
   move-remaining bonus to this document's formula today would require this
   document to read state from a system that doesn't exist yet and, worse,
   would create a **Scoring → Objective** dependency where
   `systems-index.md`'s dependency graph only ever has Objective depend on
   Scoring (§ Circular Dependencies: "Objective reads the live score...
   Scoring never needs Objective's internal state" — a strictly
   one-directional read this document must not reverse).
2. **Not part of the approved MVP scope.** `game-concept.md`'s MVP
   Definition lists "Star ratings + instant retry" but never a
   leftover-move conversion mechanic.
3. **A genuine pacing tension, not a free win.** A move-remaining bonus
   rewards racing through objectives efficiently, which can work against
   deliberate cascade-building — the exact behavior this document's
   Activation Bonus (§2) is designed to reward. Introducing it without
   careful tuning risks pulling players toward "finish fast" over "set up
   the biggest cascade," a tension this document is not resourced to
   resolve at MVP.

**If ever added**, this is Level Objective & Move-Limit System's (#7) call
to make and own as its own end-of-level event (since it alone knows moves
remaining at win time) — not a change to this document's core per-tile
formula. Logged for #7's authoring (see Open Questions).

### 6. BOOTSTRAP Cascades Never Score

**`match_cleared` events with `trigger_source = BOOTSTRAP` always contribute
`0` to `final_score`, regardless of how many cells they clear or what
specials they involve.** Bootstrap's own automatic cascade pass
(`board-engine.md` § Detailed Rules 2, step 8 — resolving a pre-placed
piece's accidental match at level start) "consumes zero player moves and is
never attributed to a `chain_index` associated with a swap." Awarding score
for a cascade the player did nothing to cause would violate this document's
Player Fantasy ("every point on screen was earned") and would let a level
author accidentally (or deliberately) inflate a level's opening score
through `pre_placed_pieces` placement — a silent, invisible advantage Pillar
2 explicitly forbids. This document's Formula 2 is defined only over
`trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}`; a `BOOTSTRAP`-sourced
event is outside its domain entirely, not merely zero-valued by
coincidence.

### 7. Star Threshold Framework — How Designers Set Thresholds

**Star thresholds are never hand-typed by a designer from intuition alone —
they are the deterministic output of a level's own `move_limit` and
`color_pool` size, fed through the framework below (Formula 6).** The
framework's job is to answer, for any schema-legal level, "what does
completion look like, what does good play look like, and what does
near-perfect play look like?" in a single, reusable, inspectable formula:

- **`star_1_score` — the completion-feasible baseline.** When a level has a
  `score_target` objective, `star_1_score` is set equal to it — the
  strongest possible form of "if you can win, you can 1-star" (already a
  structural guarantee via Level Data Format's V17). For a `collect_color`-
  only level (no `score_target` to anchor to), `star_1_score` falls back to
  a fraction of the level's own `reference_max_score` (Formula 5),
  representing "roughly what an average, non-optimizing player accrues while
  playing to satisfy the objective."
- **`star_2_score` — solid, deliberate play.** A fraction of
  `reference_max_score` well above `star_1_score`, representing a player who
  is setting up occasional match-4/5s and taking advantage of a few
  cascades, without hunting every possible combo.
- **`star_3_score` — near-optimal play.** A fraction of `reference_max_score`
  close enough to it that 3-starring genuinely requires deliberate,
  cascade-aware play — validated end-to-end against the concept prototype's
  own measured greedy-bot performance (§ Formulas, Formula 6's worked
  example), which is the closest empirical proxy this project has for
  "skilled, not perfect" play.

The three fractions (`STAR_1_FRACTION`, `STAR_2_FRACTION`,
`STAR_3_FRACTION`) are fixed, named Tuning Knobs applied uniformly to every
level's own `reference_max_score` — never a per-level fudge, per Pillar 2's
"one authored lever" philosophy (the same pattern `world-map.md`'s Formula 1
uses for region star gates).

### 8. Closest-Miss Metric — Scope & Definition

**This document defines and computes exactly one dimension of "how close was
I": the score-to-first-star proximity ratio (Formula 8).** It does **not**
define the full `closest_miss_summary` payload `screen-flow.md`'s declared
seam names — that remains Level Objective & Move-Limit System's (#7)
ownership, since a level's `collect_color` or blocker-clearing objective
completion (a dimension this document has no visibility into) may need to
be surfaced alongside, or instead of, the score dimension for some lose
outcomes (see Edge Cases). This document contributes `score_progress_ratio`/
`score_progress_percent` to that payload via the ratified Score Query API
(§ Detailed Rules 10a); Level Objective & Move-Limit System (#7) is the
ratified sole assembler of the full `closest_miss_summary` (Revision 2 —
see changelog).

**Why score-to-star-1, not score-to-score-target or objective-completion-
percent.** `star_1_score` is always defined for every level, regardless of
objective type (Level Data Format V16 requires it unconditionally), making
it the one proximity denominator that works identically whether the
level's objective is `score_target` or `collect_color`. Framing "so close"
around distance to the very first star — the threshold that most directly
answers "was I basically about to succeed at all?" — is more emotionally
legible than a raw `score_target` number the player has no intuitive sense
of scale for: a player at 92% of `star_1_score` genuinely was one or two
good swaps away from earning *something*, which is the relatable "so close"
feeling `game-concept.md`'s Flow State Design calls for.

### 9. Anti-Inflation & Integer Safety Policy

**This document ties its own worst-case score ceiling directly to Board
Engine's existing hard caps — `MAX_CASCADE_DEPTH` and the board-geometry-
driven `max_cells_per_step` (`board-engine.md` Formula 5) — rather than
declaring an independent one.** Because `chain_multiplier` (§3) and
`MAX_CASCADE_DEPTH` are both inherited, unowned-by-this-document constants,
the absolute worst-case per-move and per-level score bounds (Formula 9) are
a direct, mechanical consequence of values Board Engine already owns and
enforces — this document adds no new cap of its own, only computes what the
existing caps imply for score.

**Integer policy.** `final_score` is stored as a native 64-bit signed
integer (GDScript's default `int` type in Godot 4.6). Formula 9 shows the
absolute degenerate worst-case bound for even the most extreme single move
sits many orders of magnitude below the 64-bit ceiling — no saturating
arithmetic, clamping, or overflow-guard logic is required anywhere in this
document's formulas.

### 10. `ScoreResults` — Fields This Document Contributes (Pulled, Not Pushed)

`screen-flow.md` § Detailed Rules 11 declares `ResultsData`'s consumption
contract and names `stars_earned`, `score_earned`, and
`closest_miss_summary` as fields this document (Scoring & Star Thresholds)
contributes. **This document does not assemble or emit `ResultsData`
itself.** Per the reconciled seam contract (Revision 2 — see changelog),
Level Objective & Move-Limit System (#7) is the sole `ResultsData`
assembler: at the resolving `board_stabilized`, it *pulls* this document's
contributed fields via `get_score_results()` (§ Detailed Rules 10a) and
composes them, together with its own `outcome` and objective-completion
data, into the full `ResultsData` record it emits with `level_resolved`.

This document's contribution, precisely:

| Field (as delivered via `get_score_results()`) | Type | Maps to `ResultsData` field | This document's contribution |
|---|---|---|---|
| `final_score` | int, `≥ 0` | `score_earned` | The attempt's `final_score` (§ Detailed Rules 1) at the instant Level Objective & Move-Limit System (#7) determines the level has resolved. |
| `stars_earned` | int, `{0,1,2,3}` | `stars_earned` | Formula 7's star evaluation, applied to `final_score` against this level's `star_1/2/3_score`. |
| `score_progress_ratio` | float, `[0.0, 1.0]` | `closest_miss_summary.score_progress_ratio` | Formula 8's output. |
| `score_progress_percent` | int, `[0, 100]` | `closest_miss_summary.score_progress_percent` | `round(100 × score_progress_ratio)` — a display-ready convenience derived from the same ratio. |

`outcome` and the objective-completion dimension of `closest_miss_summary`
are **not** this document's fields — Level Objective & Move-Limit System
(#7) computes `outcome` from its own win/lose evaluation and composes the
full `closest_miss_summary` (score dimension pulled from this document,
objective-completion dimension from its own `objectives_final`), per its
own § Detailed Rules 9 (Revision 2).

**`is_new_best_score` / a "best-score flag" is deliberately NOT part of
`ScoreResults`.** `screen-flow.md`'s own Formula 6 already derives
`is_new_best_stars`/`is_new_best_score` from a **pre-attempt snapshot** of
`get_profile()`, taken before this attempt begins, compared against this
attempt's `stars_earned`/`score_earned` once resolved — and that document's
own rationale explains precisely why a snapshot (not a redundant flag
computed elsewhere) is required: by the time any consumer could read a
"best" comparison, Save & Persistence's monotonic merge may have already
overwritten the live record with this attempt's result
(`save-persistence.md` §3). Adding a second, independently-computed
"best-score flag" to `ScoreResults` would risk a second, potentially-drifting
source of truth for the same fact Screen Flow already owns correctly. This
document exposes only the raw `final_score`/`stars_earned`/
`score_progress_ratio`/`score_progress_percent` values above.

**Per-objective completion is deliberately NOT part of this document's
`ScoreResults` contribution.** This document has no visibility into
`collect_color` tallies, blocker state, or any other objective-type-specific
progress — that is Level Objective & Move-Limit System's (#7) exclusive
scope, exactly the same "Scoring surface boundary" `world-map.md` § Detailed
Rules 3 already draws when describing its own read-only relationship to this
document's eventual output.

### 10a. Score Query API — The Ratified Pull Interface

**This document exposes exactly two synchronous, read-only query seams —
never signals, never a push-style "finalize" call — that Level Objective &
Move-Limit System (#7) pulls from.** Both are formally ratified by this
revision (Revision 2), resolving the advisory gap this document's own
review log flagged (`scoring-stars-review-log.md`, Recommended Revisions
#1) and the mirrored gap in `level-objectives-review-log.md`.

```
ScoreProvider.get_current_score() -> int
```

Always returns the exact live, authoritative running `final_score` total
(§ Detailed Rules 1) at the instant it is called — never a cached or stale
copy. Matches `level-objectives.md` § Detailed Rules 3's already-proposed
name and signature exactly; used for continuous `score_target` objective
tracking throughout play.

```
ScoreProvider.get_score_results() -> ScoreResults

ScoreResults = {
    final_score: int,              // ≥ 0 — this attempt's final_score (§ Detailed Rules 1)
    stars_earned: int,              // {0,1,2,3} — Formula 7
    score_progress_ratio: float,    // [0.0, 1.0] — Formula 8
    score_progress_percent: int,    // [0, 100] — Formula 8
}
```

Called exactly once, at the resolving `board_stabilized`, by Level
Objective & Move-Limit System (#7) as its sole means of pulling this
document's contributed `ResultsData` fields (§ Detailed Rules 10) —
replacing the previously-proposed `ScoreProvider.finalize_results(
objectives_resolution) -> ResultsData` push seam, which this document
never implemented and which would have required Scoring to assemble
fields (`outcome`, the objective-completion dimension of
`closest_miss_summary`) it has no visibility into (§ Detailed Rules 8).
`ScoreResults` is a plain data struct scoped entirely to fields this
document owns and computes; it carries no knowledge of `ResultsData`'s
full shape, `ObjectivesResolution`, or any Level Objective-owned data —
Scoring remains fully unaware of Level Objective's internal state,
preserving the one-directional read `systems-index.md`'s Circular
Dependencies section requires.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `get_current_score()` return value | int | `≥ 0` | Live running `final_score`. |
| `get_score_results()` return value | `ScoreResults` | struct, fields above | This attempt's finalized score-side contribution to `ResultsData`; valid to call any time at or after the resolving `board_stabilized`, since `final_score` is always complete by then (§ Detailed Rules 4). |

**Output range**: `get_current_score()` — unbounded non-negative int, per
Formula 2/9. `get_score_results()` — a fixed-shape struct whose fields are
individually bounded exactly as Formulas 7 and 8 already bound them.

**Worked example**: mid-cascade, `get_current_score()` called after the
first of a 2-step cascade returns `60` (Formula 2 Worked Example A's first
step); called again after the second step returns `180`. At the resolving
`board_stabilized` for a level with thresholds `2,500/3,200/3,900` and a
final `final_score = 2,100`, `get_score_results()` returns `{final_score:
2100, stars_earned: 0, score_progress_ratio: 0.84,
score_progress_percent: 84}` — reproducing Formula 8's worked example
exactly, now packaged as the pulled struct.

---

## Formulas

### Formula 1 — Chain Multiplier

**Named expression:**
```
chain_multiplier(chain_index) = chain_index
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `chain_index` | int | `[1, MAX_CASCADE_DEPTH]` (inherited from `board-engine.md`, default ceiling `20`) | Board Engine's own cascade-step counter, carried on every `match_cleared` payload. |
| `chain_multiplier(chain_index)` | int | `[1, MAX_CASCADE_DEPTH]` | The multiplier applied to a cascade step's base+bonus value (Formula 2). |

**Output range**: Bounded above only by Board Engine's own
`MAX_CASCADE_DEPTH` — this document declares no independent cap (§ Detailed
Rules 3, 9).

**Worked example**: `chain_multiplier(1) = 1`, `chain_multiplier(2) = 2`,
`chain_multiplier(4) = 4` (matching the concept prototype's own "×2 →
×4" escalating callout labels, `REPORT.md`).

---

### Formula 2 — Cascade Step Score

**Named expression:**
```
activation_bonus(special_type) =
    0                             if special_type == SPECIAL_NONE
    STRIPE_ACTIVATION_BONUS       if special_type ∈ {STRIPE_H, STRIPE_V}
    COLOR_BOMB_ACTIVATION_BONUS   if special_type == COLOR_BOMB

step_score(chain_index, cleared_pieces) =
    chain_multiplier(chain_index) ×
    ( TILE_BASE_VALUE × |cleared_pieces|
      + Σ_{p ∈ cleared_pieces} activation_bonus(p.special_type) )
```

Defined only for `match_cleared` events with `trigger_source ∈ {SWAP_MATCH,
SPECIAL_ACTIVATION}` (§ Detailed Rules 1, 6).

| Symbol | Type | Range | Description |
|---|---|---|---|
| `cleared_pieces` | Array[PieceSnapshot] | `board-engine.md` § Detailed Rules 7's payload | Every cell cleared this step, each carrying its pre-clear `special_type`. |
| `p.special_type` | int | `{SPECIAL_NONE, STRIPE_H, STRIPE_V, COLOR_BOMB}` (`special-candies.md` § Detailed Rules 1) | One cleared piece's identity immediately before it cleared. |
| `TILE_BASE_VALUE` | int (tuning constant) | `10–40`, default `20` | Base points per cleared cell, regardless of source — validated from the concept prototype's own "20 pts-per-tile" tuning (`REPORT.md`). |
| `STRIPE_ACTIVATION_BONUS` | int (tuning constant) | `30–120`, default `60` (`= 3 × TILE_BASE_VALUE`) | Flat bonus for each activated `STRIPE_H`/`STRIPE_V` piece found in `cleared_pieces`. |
| `COLOR_BOMB_ACTIVATION_BONUS` | int (tuning constant) | `90–360`, default `180` (`= 9 × TILE_BASE_VALUE`) | Flat bonus for each activated `COLOR_BOMB` piece found in `cleared_pieces`. |
| `chain_multiplier(chain_index)` | int | Formula 1 | — |
| `step_score` | int | `≥ 0`, unbounded above (bounded in practice by Formula 9) | This step's contribution to `final_score`. |

**Output range**: Always non-negative (`TILE_BASE_VALUE > 0`,
`activation_bonus ≥ 0`, `chain_multiplier ≥ 1`); unbounded above in the
formula's own domain, bounded in practice by Formula 9's derivation from
Board Engine's own caps.

**Worked example A — no specials** (reproduces `board-engine.md` § Detailed
Rules 13's own 2-step red-tile cascade walkthrough): `chain_index = 1`,
`cleared_pieces` = 3 entries, all `special_type = SPECIAL_NONE`:
`step_score = 1 × (20 × 3 + 0) = 60`. `chain_index = 2`, `cleared_pieces` = 3
more entries, all `SPECIAL_NONE`: `step_score = 2 × (20 × 3 + 0) = 120`.
Total for this move: `60 + 120 = 180`.

**Worked example B — solo Striped activation caught via seam 4**: a
`STRIPE_H` piece is matched into a fresh run of 3 and its row-sweep (seam 4)
adds the rest of its row to the clear set. `chain_index = 1`,
`cleared_pieces` = 8 entries total (its full row on an 8-wide board); exactly
1 entry carries `special_type = STRIPE_H` (the striped candy's own cell),
the other 7 carry `SPECIAL_NONE`:
```
step_score = 1 × (20 × 8 + 60 × 1) = 1 × (160 + 60) = 220
```

**Worked example C — Bomb+Bomb combo**: two `COLOR_BOMB` pieces are swapped
on a fully-occupied 8×8 board. Per `special-candies.md` Formula 5, the clear
set is every `OCCUPIED` cell (64 cells), including both bomb cells.
`chain_index = 1`, `cleared_pieces` = 64 entries; exactly 2 carry
`special_type = COLOR_BOMB`, the remaining 62 carry `SPECIAL_NONE`:
```
step_score = 1 × (20 × 64 + 180 × 2) = 1 × (1280 + 360) = 1640
```

---

### Formula 3 — Derived Combo-Matrix Activation Bonus Table

**Named expression:** the total Activation Bonus contribution for a step
is the sum of `activation_bonus(special_type)` (Formula 2) over every
activated special piece involved — this table tabulates that sum for every
row of `special-candies.md` § Detailed Rules 5's combo matrix, plus the two
solo-activation cases, for designer/QA reference. **This table is derived,
not independently authored** — it is what Formula 2 already computes; no
second lookup exists anywhere in the implementation.

| Activation | Specials involved (`special_type` values in `cleared_pieces`) | Derived Activation Bonus contribution |
|---|---|---|
| Solo Striped activation (matched normally or passively caught) | 1 × `STRIPE_H` or `STRIPE_V` | `60` |
| Solo Color Bomb activation (= Bomb + Color combo) | 1 × `COLOR_BOMB` | `180` |
| Bomb + Bomb combo | 2 × `COLOR_BOMB` | `360` |
| Bomb + Striped combo ("jackpot") | 1 × `COLOR_BOMB` + 1 × `STRIPE_H`/`STRIPE_V` | `240` |
| Striped + Striped combo | 2 × `STRIPE_H`/`STRIPE_V` | `120` |

| Symbol | Type | Range | Description |
|---|---|---|---|
| `STRIPE_ACTIVATION_BONUS` | int | `60` (Formula 2 default) | — |
| `COLOR_BOMB_ACTIVATION_BONUS` | int | `180` (Formula 2 default) | — |

**Output range**: `[60, 360]` for the MVP special roster (2 types, at most 2
activating pieces per step under the defined combo matrix); a passive chain
catching more than 2 specials in one step (e.g., a Striped candy's sweep
catching two further Striped candies) simply extends the same sum further —
no ceiling is declared here beyond Formula 9's overall step bound.

**Worked example**: Bomb+Bomb's `360` reproduces exactly the `180 × 2 =
360` Activation Bonus term embedded in Formula 2's Worked Example C
(`step_score = 1 × (1280 + 360) = 1640`), confirming the table is a
faithful summary, not a divergent second rule.

---

### Formula 4 — `REFERENCE_SCORE_PER_MOVE(color_pool_size)`

**This supersedes Level Data Format's provisional flat constant
(`REFERENCE_SCORE_PER_MOVE = 160`, `level-data-format.md` Formula A) with a
value derived per `color_pool` size, per that document's own explicit
deferral** ("provisional... owned authoritatively by Scoring & Star
Thresholds (#6) once written").

**Derivation (honest match-probability model, explicitly heuristic —
mirrors `board-engine.md` Formula 6's and `special-candies.md` Formula 9's
own caveated style).** A triggered cascade's steps form a geometric
process: after each step, with probability `p_continue(K)` (Board Engine's
own per-cell coincidental-match heuristic, § Detailed Rules 6, `w = 3`
refilled cells per step — the same conservative "sustained" step size
convention Formula 6 and Formula 9 both already use) the cascade continues
to another, more-highly-multiplied step; otherwise it stops. For a
geometric distribution over step count `N ≥ 1` with continuation
probability `p_continue` and stopping probability `q = 1 - p_continue`, the
expected value of the chain-weighted step sum `Σ_{n=1}^{N} n` (i.e., the
expected total of `chain_multiplier` values a single triggered cascade
accumulates) reduces to a clean closed form:

```
p_continue(K) = 1 − (1 − 1/K²)³                    (board-engine.md Formula 6, w=3)
q(K)          = 1 − p_continue(K)
M(K)          = E[ Σ_{n=1}^{N} n ] = 1 / q(K)²      (expected cascade "chain-weight" for one triggered move)

REFERENCE_SCORE_PER_MOVE(K) = REFERENCE_SCORE_PER_MOVE(5) × ( M(K) / M(5) )
```

`REFERENCE_SCORE_PER_MOVE(5) = 160` is used as the **empirical anchor**
(the concept prototype's own measured greedy-bot result: `3,960` points over
`25` moves `≈ 158.4/move`, rounded to `160`, `REPORT.md`) — not re-derived
from this coincidental-cascade model alone, which (by design) only captures
*coincidental* cascade continuation, not the *deliberate* match-4/5 setup
that `special-candies.md` Formula 9 concludes dominates real specials
creation. The model's **ratio** `M(K)/M(5)` — not its absolute output — is
what this document trusts, exactly the same "relative, not absolute"
caveat `board-engine.md` Formula 6 and `special-candies.md` Formula 9 both
already apply to their own heuristics.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `K` | int | `{3, 4, 5}` (`color_pool` size, `level-data-format.md` V11) | The level's color pool size. |
| `p_continue(K)` | float | `(0, 1)` | Per-step cascade continuation probability (Board Engine's own Formula 6). |
| `q(K)` | float | `(0, 1)` | Per-step stopping probability. |
| `M(K)` | float | `≥ 1` | Expected chain-weighted step sum for one triggered cascade at this `K`. |
| `REFERENCE_SCORE_PER_MOVE(K)` | int | `[100, 300]` | Recommended average score-per-move benchmark used only for star-threshold calibration (§ Formula 5) — never a runtime score cap. |

**Output range**: A small, closed lookup table (only 3 schema-legal `K`
values ever exist); not a continuous runtime function.

**Worked computation (all three schema-legal values):**
```
K=3: p_continue = 1-(1-1/9)³  = 1-(8/9)³   = 0.29767  → q=0.70233 → q²=0.49327 → M=2.02729
K=4: p_continue = 1-(1-1/16)³ = 1-(15/16)³ = 0.17603  → q=0.82398 → q²=0.67894 → M=1.47295
K=5: p_continue = 1-(1-1/25)³ = 1-(24/25)³ = 0.11526  → q=0.88474 → q²=0.78276 → M=1.27753   (anchor)

M(3)/M(5) = 1.58688  →  REFERENCE_SCORE_PER_MOVE(3) = 160 × 1.58688 ≈ 253.9 → rounds to 255
M(4)/M(5) = 1.15292  →  REFERENCE_SCORE_PER_MOVE(4) = 160 × 1.15292 ≈ 184.5 → rounds to 185
M(5)/M(5) = 1.00000  →  REFERENCE_SCORE_PER_MOVE(5) = 160 × 1.00000 = 160.0   (anchor, unrounded)
```

**Resulting table** (all rounded to the nearest 5, Tuning Knobs):

| `color_pool` size (`K`) | `REFERENCE_SCORE_PER_MOVE(K)` |
|---|---|
| 3 | 255 |
| 4 | 185 |
| 5 | 160 |

This confirms the task's own premise directionally and quantitatively:
cascade frequency — and therefore expected score density — rises
meaningfully as `color_pool` shrinks (a `59%` uplift at `K=3` vs. `K=5`),
which the old flat `160` constant silently ignored for any level not using
the 5-color reference pool. **Empirical calibration against real Vertical
Slice telemetry (a bottom-up simulated/playtested average of Formula 2
across many games, including its Activation Bonus terms — which this
top-down coincidental model does not attempt to capture) is explicitly
deferred**, matching the task's own instruction and the identical caveat
posture `board-engine.md` Formula 6 and `special-candies.md` Formula 9 both
already apply to their own heuristics (see Open Questions).

---

### Formula 5 — Reference Max Score (Supersedes Level Data Format Formula A)

**Named expression:**
```
reference_max_score(move_limit, color_pool_size) = move_limit × REFERENCE_SCORE_PER_MOVE(color_pool_size)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `move_limit` | int | `≥ 1` (`level-data-format.md` V12) | This level's authored move allotment. |
| `color_pool_size` | int | `{3,4,5}` | `len(level.color_pool)` (`level-data-format.md` V11). |
| `REFERENCE_SCORE_PER_MOVE(color_pool_size)` | int | Formula 4's table | — |
| `reference_max_score` | int | Unbounded, scales linearly with `move_limit` | Advisory upper bound used by the star-threshold framework (Formula 6) and by Level Data Format's V18 sanity check. |

**Output range**: Unbounded above, not clamped — an advisory calibration
input, never a runtime score cap (identical framing to the formula it
supersedes, `level-data-format.md` Formula A).

**Worked example — the L1 reference level** (`move_limit = 25`,
`color_pool_size = 5`): `reference_max_score = 25 × 160 = 4,000` — **unchanged
from the prior flat-constant model**, since `K=5` is this document's own
anchor value (§ Formula 4). The correction's effect is isolated to
non-5-color levels, which the old flat model mis-evaluated (see the K=3
worked example under Formula 6).

---

### Formula 6 — Star Threshold Framework

**Named expression:**
```
score_target_recommended = round( STAR_1_FRACTION × reference_max_score )

star_1_score =
    score_target                                        if a score_target objective is present
    round( STAR_1_FRACTION × reference_max_score )       otherwise (collect_color-only level)

star_2_score = round( STAR_2_FRACTION × reference_max_score )
star_3_score = round( STAR_3_FRACTION × reference_max_score )
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `reference_max_score` | int | Formula 5 | — |
| `STAR_1_FRACTION` | float (tuning constant) | `0.4–0.8`, default `0.625` | Completion-feasible baseline fraction. |
| `STAR_2_FRACTION` | float (tuning constant) | `0.6–0.9`, default `0.80` | Solid/deliberate-play fraction. |
| `STAR_3_FRACTION` | float (tuning constant) | `0.85–0.99`, default `0.975` | Near-optimal-play fraction. |
| `score_target_recommended` | int | — | Design guidance for setting a level's `score_target` objective value itself, not a schema field. |
| `star_1_score`, `star_2_score`, `star_3_score` | int | `star_1 < star_2 < star_3` by construction (all three fractions are fixed and strictly increasing) | The three authored schema fields (`level-data-format.md` §2). |

**Output range**: Because `0 < STAR_1_FRACTION < STAR_2_FRACTION <
STAR_3_FRACTION < 1` is fixed, `star_1_score < star_2_score < star_3_score`
holds automatically for any positive `reference_max_score` — Level Data
Format's V16 (strictly increasing) is satisfied by construction, with no
separate ordering check needed.

**Worked example 1 — the L1 reference level** (`move_limit = 25`,
`color_pool = 5`, `reference_max_score = 4,000` from Formula 5):
```
score_target_recommended = round(0.625 × 4,000) = 2,500
star_1_score = 2,500   (score_target objective present, set equal to it)
star_2_score = round(0.80  × 4,000) = 3,200
star_3_score = round(0.975 × 4,000) = 3,900
```
**These reproduce `2,500 / 3,200 / 3,900` exactly — validated, not
corrected**, for this specific level. `star_3_score = 3,900` sits at `97.5%`
of `reference_max_score`, closely under the concept prototype's own
measured greedy-bot ceiling (`3,960`, `≈99%`) — a real, checkable
"near-optimal" bar, not a designer's guess (§ Player Fantasy).

**Worked example 2 — a hypothetical K=3 level** (`move_limit = 25`,
`color_pool = 3`, `reference_max_score = 25 × 255 = 6,375` from Formula 5,
Formula 4's `K=3` value): this demonstrates why the old flat-`160`-constant
model was wrong for any level not using 5 colors:
```
score_target_recommended = round(0.625 × 6,375) = 3,984
star_1_score = 3,984
star_2_score = round(0.80  × 6,375) = 5,100
star_3_score = round(0.975 × 6,375) = 6,216
```
Under the old flat model, this same level would have incorrectly used
`reference_max_score = 4,000` (identical to the 5-color reference level,
despite its genuinely higher expected cascade density at `K=3`) — a
`59%` understatement of the real ceiling, which would have made a
correctly-tuned `star_3_score` near `6,216` fail the old V18 sanity check
for no real reason. This is the concrete bug this document's `K`-aware
Formula 4/5 fixes.

---

### Formula 7 — Star Evaluation

**Named expression:**
```
stars_earned(final_score, star_1_score, star_2_score, star_3_score) =
    3   if final_score >= star_3_score
    2   elif final_score >= star_2_score
    1   elif final_score >= star_1_score
    0   otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `final_score` | int | `≥ 0` | The attempt's total accrued score (§ Detailed Rules 1). |
| `star_1_score`, `star_2_score`, `star_3_score` | int | `level-data-format.md` schema fields | Strictly increasing (V16). |
| `stars_earned` | int | `{0, 1, 2, 3}` | This attempt's star result. |

**Output range**: A closed 4-value set. **Inclusive at every boundary** —
`final_score` exactly equal to a threshold earns that star (`>=`, never
strict `>`), matching `level-data-format.md`'s own inclusive framing
throughout its validation rules (e.g., V17's `<=`).

**Worked example**: L1 reference level's thresholds (`2,500 / 3,200 /
3,900`). `final_score = 3,200` (exactly on the `star_2_score` boundary) →
`stars_earned = 2` (not 1). `final_score = 3,960` (the prototype's measured
greedy-bot result) → `stars_earned = 3` (`3,960 ≥ 3,900`). `final_score =
1,800` → `stars_earned = 0` (`1,800 < 2,500`).

---

### Formula 8 — Closest-Miss Score Ratio

**Named expression:**
```
score_progress_ratio  = clamp( final_score / star_1_score, 0.0, 1.0 )
score_progress_percent = round( 100 × score_progress_ratio )
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `final_score` | int | `≥ 0` | As in Formula 7. |
| `star_1_score` | int | `> 0` | As in Formula 7. |
| `score_progress_ratio` | float | `[0.0, 1.0]` | Proximity to the first star, clamped. |
| `score_progress_percent` | int | `[0, 100]` | Display-ready convenience value. |

**Output range**: `[0.0, 1.0]` — clamped above at `1.0` even if
`final_score >= star_1_score`, so this metric never displays a
nonsensical ">100% close" on a LOSE outcome. This clamp is what makes the
edge case in § Edge Cases ("score met star_1 but the level still lost on a
non-score objective") safe rather than misleading by construction — a
capped `100%` reads as "you basically won on score," which is still true,
even though it does not by itself explain *why* the level was lost (see
that edge case's full resolution note).

**Worked example**: `final_score = 2,100`, `star_1_score = 2,500`:
```
score_progress_ratio = clamp(2,100 / 2,500, 0.0, 1.0) = clamp(0.84, 0.0, 1.0) = 0.84
score_progress_percent = round(100 × 0.84) = 84
```
A Results Lose screen showing "84% of the way to your first star" is a
concrete, checkable, honestly-close claim — not vague reassurance.

---

### Formula 9 — Max Plausible Score Bound & Integer Safety

**Named expression (absolute degenerate worst case, tied directly to Board
Engine's own caps — no independently-declared ceiling):**
```
max_step_score = ( TILE_BASE_VALUE + COLOR_BOMB_ACTIVATION_BONUS ) × max_cells_per_step × chain_index
max_move_score = Σ_{n=1}^{MAX_CASCADE_DEPTH} max_step_score(n)
                = ( TILE_BASE_VALUE + COLOR_BOMB_ACTIVATION_BONUS ) × max_cells_per_step
                  × MAX_CASCADE_DEPTH × (MAX_CASCADE_DEPTH + 1) / 2
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `TILE_BASE_VALUE`, `COLOR_BOMB_ACTIVATION_BONUS` | int | Formula 2 defaults, `20` and `180` | The most expensive per-cell combination the formula can ever produce (every cleared cell hypothetically being an activated Color Bomb — the single most valuable per-cell case). |
| `max_cells_per_step` | int | `[9, 81]`, Board Engine's own Formula 5 | Absolute worst-case cells clearable in one step (full-board clear). |
| `MAX_CASCADE_DEPTH` | int | `20` (default, Board Engine's own Tuning Knob) | Inherited cap on cascade steps per move — this document declares no separate one (§ Detailed Rules 3, 9). |
| `max_move_score` | int | Bounded | Absolute theoretical ceiling for one player move, at the schema's most extreme `9×9` grid. |

**Output range**: A deliberately pessimistic, structurally near-impossible
double-worst-case bound (every one of 20 cascade steps degenerately
re-clearing the entire 81-cell board, every cell an activated Color Bomb) —
used solely to prove integer safety, never enforced as a runtime clamp.

**Worked example** (`9×9` board, `max_cells_per_step = 81` per
`board-engine.md` Formula 5's own worked example):
```
max_move_score = (20 + 180) × 81 × 20 × 21 / 2 = 200 × 81 × 210 = 3,402,000
```
Even at a generous launch-scope upper `move_limit` of `99`, the absolute
per-level ceiling is `3,402,000 × 99 ≈ 336,798,000` — roughly `6` orders of
magnitude below GDScript's native 64-bit signed `int` ceiling
(`~9.22 × 10^18`). No overflow-guard, saturation, or clamping logic is
required anywhere in this document's implementation (§ Detailed Rules 9).

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|---|---|---|
| Score during a mid-game reshuffle | No score change — reshuffle "reassigns existing pieces' `(color, special_type)` across cells rather than clearing/spawning/placing new ones" (`board-engine.md` § Detailed Rules 11); it never fires `match_cleared`, so Formula 2 has nothing to sum. No special-cased "ignore reshuffle" logic is needed — Formula 2 simply never receives an event to process. | Matches the task's own explicit framing: "none — no clears." |
| `match_cleared` with `trigger_source = BOOTSTRAP` | Contributes `0` to `final_score`, unconditionally, regardless of cell count or specials involved. | § Detailed Rules 6 — bootstrap cascades consume zero player moves and must never be silently monetizable via `pre_placed_pieces` authoring. |
| A swap is judged invalid and reverts (`swap_rejected`) | Zero score effect — no `match_cleared` ever fires for a reverted swap. | Matches `board-engine.md`'s own Player Fantasy guarantee: "invalid swaps cost nothing," extended here to scoring explicitly. |
| `final_score` lands exactly on a star threshold (`star_1_score`, `star_2_score`, or `star_3_score`) | Inclusive — earns that star (Formula 7 uses `>=`, never strict `>`). | Matches `level-data-format.md`'s own inclusive validation framing (e.g., V17's `<=`) and the task's explicit instruction. |
| A single attempt's `stars_earned` is lower than a previously-recorded `best_stars` for the same level | Expected and correct — this document computes each attempt's `stars_earned` independently, with zero read access to any prior attempt's result. | "Star regression impossible" is a guarantee about the **persisted** `best_stars` record, owned entirely by Save & Persistence's monotonic merge (`save-persistence.md`) — not something this document enforces or even has visibility into. This document's only obligation is correctly computing *this* attempt's own value, mirroring `world-map.md`'s identical "Scoring surface boundary" stance toward Save & Persistence. |
| A level's `final_score >= star_1_score` (Formula 8's ratio would clamp to `100%`), yet the level still resolves as a `LOSE` (e.g., a `collect_color` objective was never satisfied despite plenty of off-target-color score accruing) | `score_progress_ratio` correctly clamps to `1.0` (`100%`) — a true statement about the score dimension specifically — but this document takes no position on whether that is the *right* headline number for this lose outcome. Level Objective & Move-Limit System (#7) must compose its own objective-completion dimension into the final `closest_miss_summary` for this case, since a `100%`-score-progress "so close" message would be misleading as the *sole* explanation for a loss driven by an unmet color-collection target. | Flagged explicitly as a scope-boundary edge case rather than silently producing a potentially-misleading single-dimension summary; this document supplies one honest, correct input, not the final composed message (§ Detailed Rules 8, Open Questions). |
| Two or more specials are caught and activate within the same cascade step (a chain of Striped/Bomb pieces catching each other via seam 4) | Each activated piece independently contributes its own `activation_bonus` to that step's `step_score` — Formula 2 sums over every `cleared_pieces` entry with `special_type != SPECIAL_NONE`, with no cap on how many can co-occur in one step (bounded only by Formula 9's overall step ceiling). | Directly reuses `special-candies.md` § Detailed Rules 6's own "no special bookkeeping needed" idempotency guarantee — this document adds no new per-step accounting logic beyond a plain sum. |
| A cascade step's clear set includes a piece that spawned earlier in the *same* step (a freshly-created special from seam 3) | Cannot occur — `special-candies.md`'s Edge Cases confirm "a special piece spawned by seam 3 this same step is [never] immediately eligible for its own seam-4 expansion in the same Clearing pass." This document inherits that guarantee without needing to re-derive or re-check it. | No double-scoring risk from a special activating in the very same step it was created. |
| A level's `move_limit` is exhausted or the level ends mid-cascade with an un-activated special still on the board | That special simply never contributes an `activation_bonus` this attempt — it earned its `TILE_BASE_VALUE` (if any of its cells cleared to create it) but not its activation payoff. `final_score` is whatever has legitimately accrued by the moment Level Objective & Move-Limit System (#7) determines resolution. | This document takes no position on *when* resolution is determined (§ Detailed Rules on simultaneous objective completion, below) — its formula is invariant to being asked to evaluate at any point. |
| Simultaneous multi-objective completion on the final move (e.g., both a `score_target` and a `collect_color` objective complete on the same swap) | This document computes `final_score`/`stars_earned` identically regardless of how many objectives completed simultaneously or in what order Level Objective & Move-Limit System (#7) resolves precedence between them — Formula 2/7 read only the accrued score at whatever instant they're asked to evaluate. | Precedence between simultaneous win conditions is explicitly Level Objective's future call (mirrors `screen-flow.md`'s own identical "Screen Flow does not resolve this ambiguity" edge case for the same underlying event). |
| A level's `color_pool` size is outside `{3, 4, 5}` | Cannot occur — `level-data-format.md` V11 hard-restricts `color_pool` to `3–5` unique entries; `REFERENCE_SCORE_PER_MOVE` (Formula 4) is defined only over this closed set and is never evaluated outside it. | Schema-enforced upstream; this document adds no defensive handling for an input its own dependency already forbids. |
| `final_score = 0` at level resolution | A structurally valid, if unusual, outcome, reachable two distinct ways: (a) zero valid swaps were ever accepted this attempt (e.g., an immediate quit-equivalent state, or a test harness asserting the empty case); or (b) a legitimate **instant WIN at `moves_used = 0`** — Board Engine's own bootstrap accidental-match cascade (`trigger_source = BOOTSTRAP`) satisfies every objective before the player's first swap (`level-objectives.md` Edge Cases, "Bootstrap's own accidental-match cascade... completes every objective"), while this document's own bootstrap-exclusion rule (row above) means that same cascade still contributes `0` to `final_score` regardless of how many cells it cleared. `stars_earned = 0` in both cases (Formula 7's `otherwise` branch, since `star_1_score > 0` always per V16) — the star floor applies identically whether the zero-score outcome is an abandoned attempt or a genuine bootstrap-triggered win. | `final_score` can never go negative (every term in Formula 2 is non-negative), so `0` is simply the natural floor, not a special case requiring dedicated logic; this document's scoring math is invariant to *why* a resolution instant produced zero. |

---

## Dependencies

| System | Direction | Nature of Dependency |
|---|---|---|
| Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED — Revision 2) | Scoring depends on it | Consumes exactly one signal, `match_cleared` (`chain_index`, `cleared_pieces`, `trigger_source`), as the sole input to Formula 2 (§ Detailed Rules 1). Also inherits `MAX_CASCADE_DEPTH` and `max_cells_per_step` (Formula 5) unmodified as the basis for this document's own anti-inflation bound (Formula 9) — no independent cap is declared. **This document fulfills the reciprocal note requested in `board-engine.md`'s Dependencies table** ("when authored, its Dependencies section must list this document"). |
| Special Candies & Combo Matrix (`design/gdd/special-candies.md`, Draft) | Scoring depends on it | Consumes the `special_type` vocabulary (`SPECIAL_NONE`, `STRIPE_H`, `STRIPE_V`, `COLOR_BOMB`, § Detailed Rules 1) as the domain of Formula 2's `activation_bonus` function, and cross-validates Formula 3's derived combo table against that document's combo matrix (§ Detailed Rules 5) and passive chain rules (§ Detailed Rules 6). Reads no signal from Special Candies directly — every combo/passive-catch effect is already fully reflected in Board Engine's own `match_cleared.cleared_pieces` (§ Detailed Rules 2). **This document fulfills the reciprocal note requested in `special-candies.md`'s Dependencies table.** |
| Level Data Format (`design/gdd/level-data-format.md`, APPROVED — v1) | Mutual — Scoring depends on it, and formally supersedes one of its provisional values | Reads `move_limit`, `color_pool` (size), and the presence/value of a `score_target` objective (Formula 5, 6). **Supersedes** the provisional `REFERENCE_SCORE_PER_MOVE` constant and Formula A (`level-data-format.md`'s own explicit deferral: "owned authoritatively by Scoring & Star Thresholds (#6) once written, at which point it should reconcile with or supersede these values") with Formula 4/5 of this document. Recommends (does not make) an update to that document's Tuning Knobs table and V18's status — see Cross-References; not edited here, per this document's own file-edit scope. |
| Level Objective & Move-Limit System (`design/gdd/level-objectives.md`, Revised — Revision 2) | Depends on Scoring | Reads `final_score` continuously via `get_current_score()` (§ Detailed Rules 10a) to evaluate `score_target` objective completion, and pulls `final_score`/`stars_earned`/`score_progress_ratio`/`score_progress_percent` once via `get_score_results()` (§ Detailed Rules 10a) at the resolving `board_stabilized` — the one-directional read `systems-index.md`'s Circular Dependencies section already resolves ("Scoring never needs Objective's internal state"). Level Objective is the sole `ResultsData` assembler, composing these pulled fields with its own `outcome` and objective-completion data into the final `closest_miss_summary` and `ResultsData` (Revision 2 reconciliation — see changelog). **Reciprocal note fulfilled**: `level-objectives.md`'s Dependencies section lists this document and confirms both seam signatures. |
| Juice Layer — VFX & Audio Hooks (#8, not yet authored) | Will depend on Scoring | Expected to render floating score popups using exactly this document's `step_score` (Formula 2) per `match_cleared` event, and to pace the HUD's counting-up display independently of this document's logical accrual timing (§ Detailed Rules 4) — mirroring `board-engine.md` § Detailed Rules 13's identical logic/presentation split. **Reciprocal note**: when authored, its Dependencies section must list this document. |
| Game UI/Screens Flow (`design/gdd/screen-flow.md`, Draft) | Mutual, indirect | Contributes `score_earned`/`stars_earned`/`closest_miss_summary.score_progress_ratio`/`score_progress_percent` to that document's declared `ResultsData` seam (§ Detailed Rules 11) — but only indirectly, via `get_score_results()` (§ Detailed Rules 10a), since Level Objective & Move-Limit System (#7), not this document, assembles and emits the actual `ResultsData` record (Revision 2 reconciliation). Also supplies the raw inputs `screen-flow.md`'s own Formula 6 (`is_new_best_stars`/`is_new_best_score`) needs — this document deliberately does not duplicate that comparison itself (§ Detailed Rules 10). **This document fulfills the reciprocal note requested in `screen-flow.md`'s Dependencies table.** |
| Level Progression / World Map (`design/gdd/world-map.md`, Draft) | It depends on this document (soft, indirect) | Consumes only the already-persisted `best_stars` (0–3) integer this document's output eventually feeds, via Save & Persistence — never this document's internal formula, per that document's own explicitly-stated "Scoring surface boundary" (`world-map.md` § Detailed Rules 3). This document adds no new obligation beyond confirming that boundary holds. |
| Booster Brewing Meta (#12, Phase 2, gated) | It depends on this document (named, not designed) | Anticipated to read per-color `cleared_pieces` tallies directly from Board Engine's `match_cleared` payload (the same Harvest Observation Point `special-candies.md` § Detailed Rules 9 names) — **never** this document's score formula. This document defines no ingredient/harvest-specific scoring variant; that boundary is logged explicitly as an Open Question for #12's eventual authoring. |
| RNG Service (`design/gdd/rng-service.md`, APPROVED) | Explicitly **not** a dependency | Every formula in this document is a pure, deterministic function of already-resolved board events — zero RNG streams are consumed, mirroring `special-candies.md` § Detailed Rules 8's identical "RNG Usage: None" stance and its own explicit justification (determinism directly serves Pillar 2). |
| Save & Persistence (`design/gdd/save-persistence.md`, Draft) | Explicitly **not** a direct dependency | This document computes `score_earned`/`stars_earned` but never reads or writes save data itself — persisting a completed level's result via `record_level_completion()` is Level Objective & Move-Limit System's (#7) exclusive responsibility, mirroring `screen-flow.md`'s own identical non-ownership stance toward that same call. |
| `prototypes/sweet-cascade-concept/REPORT.md` (prototype, not a GDD) | Scoring depends on it (design rationale + empirical anchor) | Source of `TILE_BASE_VALUE = 20` ("20 pts-per-tile"), the linear chain-multiplier decision ("× cascade multiplier"), and the `REFERENCE_SCORE_PER_MOVE(5) = 160` empirical anchor (`3,960` points / `25` moves) Formula 4 is calibrated against. Cited throughout as design rationale, never as a binding technical contract. |
| `design/gdd/game-concept.md` (foundational document) | Scoring depends on it (pillar/scope constants) | Supplies Pillar 2's "honest difficulty, inspectable math" framing (§ Player Fantasy, Detailed Rules 7), the Flow State Design's "closest miss" requirement (§ Detailed Rules 8, Formula 8), and the MVP Definition scope that explicitly excludes a move-remaining end bonus (§ Detailed Rules 5). |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `TILE_BASE_VALUE` | `20` | `10–40` | Every cleared cell (regardless of source) is worth more — raises `final_score` uniformly across the board, requiring a proportional retune of star fractions/`REFERENCE_SCORE_PER_MOVE` to keep thresholds meaningful. | Lowers per-cell value; risks making a plain match-3 feel under-rewarded relative to the fixed Activation Bonus constants, flattening the escalation ladder's felt contrast. |
| `STRIPE_ACTIVATION_BONUS` | `60` (`= 3 × TILE_BASE_VALUE`) | `30–120` | Firing a Striped candy feels more rewarded relative to a plain match; pushed too high, it risks over-valuing match-4 setup relative to the bigger, harder match-5/combo payoffs. | Under-rewards the deliberate setup cost of building a match-4, weakening Pillar 2's "the shape you built is the shape you get to cash in" framing. |
| `COLOR_BOMB_ACTIVATION_BONUS` | `180` (`= 9 × TILE_BASE_VALUE`, a fixed `3:1` ratio vs. `STRIPE_ACTIVATION_BONUS`) | `90–360` | Makes match-5/bomb activation feel like an even bigger one-swap payoff (matching `special-candies.md`'s own "single biggest one-swap payoff" framing); pushed too high, risks dwarfing combo-matrix bonuses that should feel like the true ceiling. | Under-rewards the genuinely rarer, harder-to-engineer match-5 setup relative to match-4. |
| Chain multiplier curve shape | `LINEAR` (`chain_multiplier(n) = n`), fixed for MVP | `{LINEAR, CAPPED_LINEAR}` — a binary shape choice, not a magnitude | N/A — a policy switch, not a value to tune up | Switching to `CAPPED_LINEAR` (enabling the reserved `CHAIN_MULTIPLIER_CAP` knob) dampens deep-chain score inflation at the cost of the "×N" callout/payout mismatch this document explicitly rejected at MVP (§ Detailed Rules 3) — preserved as a reversible lever only if Vertical Slice telemetry shows a real inflation problem `MAX_CASCADE_DEPTH`'s own bound (Formula 9) doesn't adequately address. |
| `CHAIN_MULTIPLIER_CAP` | Unset (reserved, inactive) | `5–20`, if ever enabled | Raising it (once enabled) narrows the gap versus uncapped linear, preserving more of the deep-chain escalation feel. | Lowering it more aggressively dampens deep-chain payout, reducing the exact "jackpot" feeling `REPORT.md` flagged as the prototype's best moment. |
| `STAR_1_FRACTION` | `0.625` | `0.4–0.8` | Raises the completion bar itself — a level's basic win condition (`score_target`) becomes proportionally harder relative to `reference_max_score`. | Lowers the completion bar, making a basic win easier relative to the level's own ceiling. |
| `STAR_2_FRACTION` | `0.80` | `0.6–0.9` | Demands more consistently deliberate play for 2 stars. | Makes 2-starring easier, closer to mere completion. |
| `STAR_3_FRACTION` | `0.975` | `0.85–0.99` | Pushes 3-starring closer to the theoretical ceiling, making it a rarer, more prestigious accomplishment (per `game-concept.md`'s "three-starring old levels" retention hook) — pushed to `0.99`+, risks reading as effectively unreachable rather than "near-optimal." | Makes 3-starring more attainable through ordinary good play, weakening its prestige and the replay incentive it's meant to create. |
| `REFERENCE_SCORE_PER_MOVE(3)` | `255` | `150–320` (pending Vertical Slice empirical calibration) | Loosens the `K=3` star-threshold ceiling, permitting higher absolute thresholds to pass the (Advisory) V18-style sanity check. | Tightens it, forcing more conservative `K=3` star thresholds. |
| `REFERENCE_SCORE_PER_MOVE(4)` | `185` | `120–260` (pending Vertical Slice empirical calibration) | As above, for `K=4`. | As above, for `K=4`. |
| `REFERENCE_SCORE_PER_MOVE(5)` | `160` (empirical anchor — see § Formula 4) | `100–250` (matches `level-data-format.md`'s originally-declared range for this specific value) | As above, for `K=5`; note this specific value is the anchor every other `K`'s value is derived relative to, so changing it rescales the entire table proportionally unless the other two values are independently re-tuned. | As above, for `K=5`. |

---

## Acceptance Criteria

**Formula correctness (BLOCKING per `coding-standards.md`'s Logic-tier
rule; deterministic, no live RNG — tests live under
`tests/unit/scoring-stars/`):**

- [ ] `test_chain_multiplier_linear`: `chain_multiplier(1)=1`,
      `chain_multiplier(2)=2`, `chain_multiplier(5)=5`,
      `chain_multiplier(20)=20`.
- [ ] `test_activation_bonus_lookup`: `activation_bonus(SPECIAL_NONE)=0`,
      `activation_bonus(STRIPE_H)=activation_bonus(STRIPE_V)=60`,
      `activation_bonus(COLOR_BOMB)=180`.
- [ ] `test_step_score_no_specials_matches_worked_example_a`: reproduces
      Formula 2's Worked Example A exactly (`60`, then `120`, summing to
      `180` across a 2-step cascade).
- [ ] `test_step_score_solo_stripe_matches_worked_example_b`: reproduces
      Formula 2's Worked Example B exactly (`220`).
- [ ] `test_step_score_bomb_plus_bomb_matches_worked_example_c`: reproduces
      Formula 2's Worked Example C exactly (`1,640`).
- [ ] `test_combo_matrix_derived_bonus_table`: for each of Formula 3's five
      rows, constructing a `cleared_pieces` fixture with exactly the stated
      special-piece composition reproduces the exact derived bonus
      (`60/180/360/240/120`) via Formula 2 alone — confirming no
      independent second lookup exists.
- [ ] `test_bootstrap_triggered_clears_score_zero`: a `match_cleared` event
      with `trigger_source = BOOTSTRAP` and a non-empty `cleared_pieces`
      contributes exactly `0` to `final_score`, regardless of cell count or
      special composition.
- [ ] `test_reshuffle_never_contributes_score`: a `board_reshuffled` event
      (with no accompanying `match_cleared`) leaves `final_score`
      unchanged.
- [ ] `test_invalid_swap_never_contributes_score`: a `swap_rejected` event
      leaves `final_score` unchanged.
- [ ] `test_reference_score_per_move_table_matches_worked_computation`:
      `REFERENCE_SCORE_PER_MOVE(3)=255`, `(4)=185`, `(5)=160`, reproducing
      Formula 4's worked computation exactly.
- [ ] `test_reference_max_score_l1_reference_level`: `move_limit=25,
      color_pool_size=5` reproduces `reference_max_score = 4,000` (Formula
      5).
- [ ] `test_reference_max_score_k3_hypothetical`: `move_limit=25,
      color_pool_size=3` reproduces `reference_max_score = 6,375` (Formula
      5's Worked Example 2 setup).
- [ ] `test_star_threshold_framework_l1_reference_level`: reproduces
      Formula 6's Worked Example 1 exactly —
      `score_target_recommended=2,500`, `star_1_score=2,500`,
      `star_2_score=3,200`, `star_3_score=3,900`.
- [ ] `test_star_threshold_framework_k3_hypothetical`: reproduces Formula
      6's Worked Example 2 exactly — `star_1_score=3,984`,
      `star_2_score=5,100`, `star_3_score=6,216`.
- [ ] `test_star_threshold_framework_collect_color_only_fallback`: a level
      with no `score_target` objective computes `star_1_score` via the
      `STAR_1_FRACTION × reference_max_score` fallback branch, not via a
      `score_target` reference.
- [ ] `test_star_thresholds_always_strictly_increasing`: a property-style
      test across a range of synthetic `(move_limit, color_pool_size)`
      pairs confirms `star_1_score < star_2_score < star_3_score` holds for
      every combination, by construction of the fixed fractions.
- [ ] `test_star_evaluation_boundary_inclusive`: `final_score` exactly
      equal to `star_2_score` yields `stars_earned = 2` (not `1`);
      `final_score` exactly equal to `star_3_score` yields `stars_earned =
      3`.
- [ ] `test_star_evaluation_all_tiers`: a table-driven test over
      `final_score` values below `star_1_score`, between each pair of
      thresholds, and above `star_3_score` reproduces `stars_earned = 0, 1,
      2, 3` respectively, matching Formula 7's worked example.
- [ ] `test_closest_miss_ratio_matches_worked_example`: `final_score=2,100,
      star_1_score=2,500` reproduces `score_progress_ratio=0.84,
      score_progress_percent=84` (Formula 8).
- [ ] `test_closest_miss_ratio_clamps_above_one`: `final_score >
      star_1_score` (a score-met-but-still-lost scenario) reproduces
      `score_progress_ratio = 1.0` exactly, never a value `> 1.0`.
- [ ] `test_max_move_score_bound_matches_worked_example`: Formula 9's
      worked example (`9×9` board, `MAX_CASCADE_DEPTH=20`) reproduces
      `max_move_score = 3,402,000` exactly, and confirms this value fits
      within a 64-bit signed integer with orders-of-magnitude headroom.
- [ ] `test_determinism_same_event_sequence_same_score`: two identical
      ordered sequences of mocked `match_cleared`/`cascade_ended` events
      (including at least one `BOOTSTRAP`-sourced event expected to
      contribute zero, one solo special activation, and one combo) produce
      byte-identical `final_score` — mirrors `board-engine.md`'s own master
      determinism gate.
- [ ] `test_zero_rng_consumption`: instrumenting every RNG Service stream
      across a full synthetic scoring pass confirms exactly `0` draws are
      consumed by this document's formulas, confirming § Detailed Rules'
      "RNG Usage: None" claim (mirrors `special-candies.md`'s own identical
      test).

**`ScoreResults` construction and Score Query API (BLOCKING,
`tests/unit/scoring-stars/`) — reassigned to the § Detailed Rules 10a
pull-seam boundary (Revision 2):**

- [ ] `test_get_score_results_final_score_matches_final_score`:
      `get_score_results().final_score` equals the exact `final_score` this
      document's Formula 2 accumulated for the attempt.
- [ ] `test_get_score_results_stars_earned_matches_formula_7`:
      `get_score_results().stars_earned` equals Formula 7's output for that
      same `final_score` against the level's authored thresholds.
- [ ] `test_get_score_results_closest_miss_score_progress_fields`:
      `get_score_results().score_progress_ratio`/`score_progress_percent`
      match Formula 8's output exactly for a LOSE-outcome fixture.
- [ ] `test_get_score_results_never_includes_best_score_flag`: interface
      inspection confirms `ScoreResults` contains no
      `is_new_best_score`/`is_new_best_stars`-equivalent field — confirming
      § Detailed Rules 10's deliberate omission holds in implementation,
      not only in the design doc.
- [ ] `test_get_current_score_returns_live_running_total`:
      `get_current_score()` called mid-cascade (between two `match_cleared`
      events of the same move) returns the exact `final_score` accumulated
      so far, never a value cached from before the most recent
      `match_cleared`.
- [ ] `test_scoring_never_receives_objectives_resolution`: interface
      inspection confirms this document exposes no seam accepting an
      `ObjectivesResolution`-shaped parameter (i.e., `finalize_results()`
      does not exist in this document's implementation) — confirming the
      dropped push seam is fully absent, not merely undocumented.

**Manual/QA walkthrough** (`production/qa/evidence/`, ADVISORY per
`coding-standards.md`'s Visual/Feel and Config/Data tiers):

- [ ] Once Juice Layer (#8) is authored, its floating score popups render
      exactly the `step_score` value this document's Formula 2 computes
      for each `match_cleared` event, with no independent point
      calculation duplicated inside Juice.
- [ ] On the L1 reference level, a full manual playthrough's final HUD
      score matches the sum this document's Formula 2 would compute from
      the same session's recorded event log, spot-checked at least once
      per Vertical Slice milestone.
- [ ] A designer authoring a new level can compute its recommended
      `score_target`/`star_1/2/3_score` values using only this document's
      Formula 5/6 and the level's own `move_limit`/`color_pool`, with no
      other GDD's formula needed.
- [ ] No gameplay value this document defines (`TILE_BASE_VALUE`,
      `STRIPE_ACTIVATION_BONUS`, `COLOR_BOMB_ACTIVATION_BONUS`,
      `STAR_1/2/3_FRACTION`, the `REFERENCE_SCORE_PER_MOVE` table) exists
      as a hardcoded literal anywhere in `src/` — every instance is
      spot-check traceable back to a single data-driven config location,
      per `coding-standards.md`'s data-driven rule.

---

## Cross-References

| This Document References | Target GDD | Specific Element Referenced | Nature |
|---|---|---|---|
| Supersession of `REFERENCE_SCORE_PER_MOVE` (flat `160`) and Formula A | `design/gdd/level-data-format.md` | Formula A, Tuning Knobs' `REFERENCE_SCORE_PER_MOVE` row | This document's Formula 4/5 is now authoritative; recommends that document's Tuning Knobs table be updated to reference the 3-value table (`160/185/255`) instead of a single flat value, and note that the `K=3` value (`255`) exceeds that table's previously-stated safe range (`100–250`) by `5` — not edited there directly, per this document's own file-edit scope (see Open Questions). |
| V18's Advisory-vs-Blocking status | `design/gdd/level-data-format.md` | §4 Validation Contract, V18; Open Questions table | This document recommends V18 **remain Advisory**, not be promoted to Blocking, because `reference_max_score` (Formula 5) is an explicitly heuristic, top-down calibration aid — not a hard combinatorial achievability bound — and promoting a heuristic sanity check to a hard gate risks false-positive rejection of legitimately-tuned levels once real telemetry diverges from the model. Resolves that document's own open question on this point (see Open Questions). |
| `special_type` vocabulary and combo-matrix definitions | `design/gdd/special-candies.md` | § Detailed Rules 1 (roster), § Detailed Rules 5 (combo matrix), § Detailed Rules 6 (passive chain rules), Formulas 4–8 | Formula 2's `activation_bonus` domain and Formula 3's derived combo table are built directly on this document's already-fixed vocabulary and combo definitions; no changes requested. |
| `match_cleared` signal payload (`chain_index`, `cleared_pieces`, `trigger_source`) | `design/gdd/board-engine.md` | § Detailed Rules 7 (Signal Catalog), § Detailed Rules 13 (Logic/Presentation Boundary) | This document's sole scoring input (§ Detailed Rules 1); no schema change requested. |
| `ResultsData` seam (`score_earned`, `stars_earned`, `closest_miss_summary`) | `design/gdd/screen-flow.md` | § Detailed Rules 11, Declared Seams | This document contributes `score_earned`/`stars_earned`/`closest_miss_summary.score_progress_ratio`/`score_progress_percent` via the ratified `get_score_results()` pull seam (§ Detailed Rules 10a); Level Objective & Move-Limit System (#7) is the sole `ResultsData` assembler (Revision 2 reconciliation, see changelog) and owns the full `closest_miss_summary`. |
| `best_stars` read-only consumption boundary | `design/gdd/world-map.md` | § Detailed Rules 3, "Scoring surface boundary" | Confirmed compatible — this document adds no new obligation on that boundary. |
| `TILE_BASE_VALUE=20`, linear chain multiplier, `REFERENCE_SCORE_PER_MOVE(5)=160` empirical anchor | `prototypes/sweet-cascade-concept/REPORT.md` | "If Proceeding" section (tuning values), Lessons Learned (special-×-special chains) | Data dependency (prototype, not a GDD) — cited as design rationale throughout Detailed Rules and Formulas. |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| Should `level-data-format.md`'s Tuning Knobs table be updated to reference this document's `REFERENCE_SCORE_PER_MOVE` table (`160/185/255`) instead of a single flat value, and should its stated safe range (`100–250`) widen to accommodate the `K=3` value (`255`)? | systems-designer | At `level-data-format.md`'s next review pass | — |
| Should Level Data Format's V18 be promoted from Advisory to Blocking now that this document supersedes `REFERENCE_SCORE_PER_MOVE` with an authoritative formula? | systems-designer | At `level-data-format.md`'s next review pass | **Recommended: remain Advisory** — see Cross-References for full rationale. Final call deferred to that document's own review pass. |
| Is `REFERENCE_SCORE_PER_MOVE(K)`'s top-down, coincidental-cascade-only derivation (Formula 4) worth replacing with a bottom-up empirical average — actually simulating/playtesting Formula 2 (including its Activation Bonus terms, which the coincidental model does not attempt to capture) across many real games at each `K` — once Vertical Slice levels exist? | systems-designer | At Vertical Slice, once real play data exists (mirrors the identical open item `board-engine.md` Formula 6 and `special-candies.md` Formula 9 both already flag for their own heuristics) | — |
| Should `CHAIN_MULTIPLIER_CAP` ever be enabled (switching from `LINEAR` to `CAPPED_LINEAR`), if Vertical Slice telemetry reveals real top-end score inflation `MAX_CASCADE_DEPTH`'s own bound (Formula 9) doesn't adequately address? | game-designer / creative-director | At Vertical Slice, once real in-engine cascade-depth telemetry exists | — |
| Should a move-remaining end bonus be added once Level Objective & Move-Limit System (#7) is authored, and if so, should it be a new formula #7 owns and calls into this document's constants for, or a full extension to this document's own Formula 2? | game-designer (with systems-designer) | At Level Objective & Move-Limit System (#7) authoring | — |
| What is the final, ratified shape of `closest_miss_summary` beyond this document's `score_progress_ratio`/`score_progress_percent` contribution — specifically, how should an objective-completion dimension be composed alongside the score dimension for the edge case where score already met `star_1_score` but the level still lost on an unmet non-score objective? | game-designer / systems-designer | At Level Objective & Move-Limit System (#7) authoring | **Partially resolved (Revision 2)**: ownership is settled — Level Objective & Move-Limit System (#7) is the sole `ResultsData`/`closest_miss_summary` assembler, composing this document's pulled `score_progress_ratio`/`score_progress_percent` (`get_score_results()`, § Detailed Rules 10a) alongside its own objective-completion data. The exact shape of that objective-completion dimension remains open — see `level-objectives.md`'s own Open Questions. |
| Should `STRIPE_ACTIVATION_BONUS`:`COLOR_BOMB_ACTIVATION_BONUS`'s fixed `3:1` ratio be validated or recalibrated once Vertical Slice provides real player score-feel data, rather than the current order-of-magnitude reasoning (setup-cost/rarity proxy)? | game-designer | At Vertical Slice, once real in-engine feel data exists | — |
| Does Booster Brewing Meta (#12, Phase 2, gated) ever need a distinct, brewing-specific score signal from this document (e.g., an ingredient-yield-weighted variant), or does it remain fully independent — reading only raw `cleared_pieces` color tallies directly from Board Engine, never this document's point formula? | game-designer / economy-designer | At Booster Brewing Meta (#12) authoring, once the friction prototype gate is passed | — |
