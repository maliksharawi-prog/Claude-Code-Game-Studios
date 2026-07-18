# Special Candies & Combo Matrix

*Status: Reviewed — APPROVED (lean review, 2026-07-18)*
*Created: 2026-07-18*
*Last Updated: 2026-07-18*
*Layer: Feature · Priority: MVP · Phase: MVP · Category: Gameplay*
*Author: systems-designer*
*Depends On: Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED — Revision 2), RNG Service (`design/gdd/rng-service.md`, APPROVED — reserves the `special-drop` stream, unconsumed at MVP, see § Detailed Rules 8), Level Data Format (`design/gdd/level-data-format.md`, APPROVED — v2 extension proposed in § Dependencies)*
*Depended On By: Scoring & Star Thresholds, Level Objective & Move-Limit System, Juice Layer — VFX & Audio Hooks, Booster Brewing Meta (Phase 2 — Harvest Observation Point, § Detailed Rules 9) (all not yet authored — forward dependencies, per `design/gdd/systems-index.md`)*
*Source: `design/gdd/systems-index.md` · `design/gdd/board-engine.md` §§ Detailed Rules 3–4, 7, 13 · `prototypes/sweet-cascade-concept/REPORT.md` · `design/art/art-bible.md` · `design/gdd/level-data-format.md` · `design/gdd/rng-service.md` · `design/gdd/game-concept.md`*

---

## Overview

Special Candies & Combo Matrix is the Feature-layer system that gives Sweet
Cascade's board its escalation ladder — turning an ordinary 4-or-5-in-a-row
into a persistent, more powerful piece, and turning two specials swapped (or
caught in the same cascade) together into the "jackpot" moments the concept
prototype flagged as the build's best (`REPORT.md`, Emergent mechanics). It
is a pure extension of Match-3 Board Engine (`board-engine.md`, APPROVED
Revision 2): every rule in this document is expressed entirely through Board
Engine's four synchronous extension seams (activation-check,
activation-clears, special-spawns, chain-expansion) with **zero changes** to
that document's contract — Board Engine remains fully headless-testable in
complete isolation, exactly as it was authored to be. MVP scope covers two
special types — **Striped** (horizontal/vertical, from a straight match-4)
and **Color Bomb** (from a straight match-5) — plus the full swap-triggered
combo matrix between them (Bomb+Color, Bomb+Bomb, Bomb+Striped,
Striped+Striped) and the passive chain-reaction rules that let any of these
specials, once caught inside a later clear set, keep firing without a
player swap. **Wrapped Candy** (the L/T-shape special) is explicitly
deferred to Vertical Slice per `art-bible.md`'s own scope note; at MVP, an
L/T intersection whose arms never reach length 4 simply clears as two
ordinary runs, no spawn (§ Detailed Rules 3). This document also resolves
the prototype's flagged open design question — whether passive color-bomb
detonation "feels unearned" — with a deterministic, board-inspectable rule
rather than removing the mechanic (§ Detailed Rules 6), and names, without
designing, the Harvest Observation Point that Booster Brewing Meta (Phase 2)
will later read from (§ Detailed Rules 9).

---

## Player Fantasy

Special Candies & Combo Matrix is where Sweet Cascade's core promise — the
player feels "clever and lucky at the same time" (`game-concept.md`, Core
Fantasy) — becomes concrete and escalating, not a slogan repeated once per
swap. This document defines three rungs of a single legible ladder, plus one
deliberate repair to a fourth rung the prototype found broken.

1. **Spot-and-build (match-4 → Striped).** The moment a player notices they
   can line up *four* instead of three, the game hands them a durable,
   reusable tool — a piece that sits on the board holding a threat until
   they choose to cash it in. This is Pillar 2 ("Clever, Never Cheated")
   made literal: the same-axis creation rule (§ Detailed Rules 2) means the
   shape the player built *is* the shape they get to fire later — there is
   no hidden translation step between "what I did" and "what I earned."
2. **The bigger reach (match-5 → Color Bomb).** A five-in-a-row is a harder,
   riskier setup, and the reward matches it: a colorless, board-spanning
   piece whose activation (swapped into any candy) wipes an entire color.
   The jump from "clear 8 cells" to "clear a color" is the single biggest
   one-swap payoff in the MVP roster, and it stays fully inspectable — the
   player can always see, before committing the swap, exactly which color
   is about to vanish.
3. **The jackpot (special × special).** This is the moment `REPORT.md`
   named as the best in the whole build. Two specials swapped together (or
   caught together in a cascade) do not just add their effects — they
   compound into something neither piece could do alone: a color-wide wipe
   that *also* strikes every one of that color's own row or column
   (Bomb+Striped), a double line-clear (Striped+Striped), or the entire
   board going off at once (Bomb+Bomb). Every one of these combos is
   deterministic and fully defined by this document's Combo Matrix
   (§ Detailed Rules 5) — nothing about a jackpot moment is left to chance.
4. **The repaired rung (passive detonation, made learnable, not luck).** The
   one place this ladder could have broken is where the prototype found
   real friction: a color bomb triggered by an unrelated cascade catch, with
   no swap the player chose, read as "dramatic but... unearned"
   (`REPORT.md`, Lessons Learned). § Detailed Rules 6 resolves this not by
   muting the drama but by making it *learnable*: a passively-caught bomb
   still detonates, on a fixed, board-inspectable rule (it always takes the
   board's single most common color at that instant) — so an experienced
   player can deliberately engineer a cascade to catch a bomb for a
   guaranteed payoff, converting what read as luck into a fourth rung of the
   same ladder. This is the same governing principle `rng-service.md`
   states for the whole game: randomness amplifies skill, it never replaces
   it. Nothing in this document ever asks the player to trust an outcome
   they could not, in principle, trace back to a rule they could learn.

---

## Detailed Rules

### 1. MVP Special Roster & Vocabulary

Two special types ship at MVP. `special_type` values are opaque integers
from Board Engine's point of view (`board-engine.md` § Detailed Rules 1) —
this document is their sole owner and defines the full vocabulary here,
append-only, so a later addition never renumbers an existing value:

| Name | `special_type` value | Created by | `color` | Visual (per `art-bible.md`) | Clear behavior |
|---|---|---|---|---|---|
| `SPECIAL_NONE` | `0` | — (Board Engine's own default) | — | Regular candy | — |
| `STRIPE_H` | `1` | Horizontal match-4 | run's color (colored special) | 3 cream stripes running horizontally | Clears its entire row on activation |
| `STRIPE_V` | `2` | Vertical match-4 | run's color (colored special) | 3 cream stripes running vertically | Clears its entire column on activation |
| `COLOR_BOMB` | `3` | Straight match-5 (any orientation) | `COLOR_NONE (-1)` — colorless | Faceted rainbow-gradient orb, white glow ring | Clears every board cell matching an activation-supplied target color |
| `WRAPPED` *(reserved, not implemented)* | `4` | — (Vertical Slice scope, see § Detailed Rules 3) | — | Crinkled foil overlay (`art-bible.md`) | Not implemented at MVP |

`WRAPPED = 4` is reserved now (never assigned to `3` or reused later) purely
so a Vertical Slice addition never has to renumber `COLOR_BOMB` — the same
append-only discipline `rng-service.md`'s Stream Registry and
`board-engine.md`'s Level Manifest both use for the same reason.

### 2. Creation Rules — Eligibility & Orientation

**Eligibility is a pure function of run length** — deterministic, no RNG,
directly satisfying Pillar 2's "special creation rules must be
readable/predictable" (`game-concept.md`, Pillar 2 design test):

| `run.length` | Outcome |
|---|---|
| `3` | No spawn — plain match, clears normally (genre baseline) |
| `4` | Striped, orientation per the same-axis rule below |
| `≥ 5` | Color Bomb, colorless |

**Same-axis orientation rule.** A run's own orientation determines its
Striped candy's orientation *and* its clear axis: a **horizontal** run of 4
becomes `STRIPE_H` (clears its **row**); a **vertical** run of 4 becomes
`STRIPE_V` (clears its **column**). `Run.orientation` (`board-engine.md`
§ Detailed Rules 4) is the only input this rule needs — **swap direction
plays no part** in orientation, deliberately.

**Justification (readability, per the task's explicit evaluation
criterion).** The genre-authentic alternative — Candy Crush Saga's
perpendicular convention, where a horizontal match-4 yields a
*vertically*-striped candy and vice versa — is well-known to this game's
target audience (`game-concept.md`'s Target Player Profile lists Candy
Crush Saga as a current game they play), but it requires the player to
memorize a non-obvious inversion on top of an already-learned shape. The
same-axis rule requires zero inference: the line the player just built *is*
the line they get to fire later, with no translation step. This also keeps
`art-bible.md`'s own phrasing — "horizontal stripes clear the row, vertical
stripes clear the column" — true with zero indirection, and it is the
smaller, more auditable implementation (`Run.orientation` maps directly to
`special_type`, one branch, no inversion logic to get backwards). Per
Pillar 2's design test ("difficulty comes from visible level design, never
from a mechanic the player can't reason about"), and per `art-bible.md`'s
own "Color tells the truth" principle (state must be readable, not learned
by rote convention lookup), same-axis is this document's ruling. The
perpendicular alternative is explicitly rejected, not merely deferred — see
Cross-References.

**Why swap direction is excluded entirely.** `Run.orientation` is already a
complete, sufficient, and *simpler* input (one field, always populated,
already provided by Board Engine's `Run` structure) — adding swap direction
as a second input would only create scenarios where the two disagree (e.g.,
a vertical run of 4 completed by a *horizontal* swap that shifted one
candy sideways into an already-vertical near-run) with no readability
benefit and a real ambiguity-resolution cost. One input, one rule, always
consistent.

**Anchoring — swapped cell first, run-middle fallback.** Per the concept
prototype's validated finding ("spawn anchoring... at the swapped cell...
matters for making match-4s feel deliberate — spawning mid-run reads as
random," `REPORT.md`, Lessons Learned) and `board-engine.md`'s own seam-3
contract, the spawn cell is resolved by Formula 2. In short: if either of
the two cells named in the triggering swap (`swap_anchor_cells`) lies
within the winning run, the spawn anchors there; otherwise (cascade steps
`chain_index ≥ 2`, or a `BOOTSTRAP`-triggered `chain_index = 1`, both of
which report `swap_anchor_cells = {}` per `board-engine.md` § Detailed
Rules 3) the spawn anchors at the run's own geometric middle cell.

### 3. Wrapped Candy & T/L-Shape Deferral (MVP Scope Cut)

**Wrapped Candy is Vertical Slice scope, not MVP**, per `art-bible.md`'s
explicit tag ("PROPOSED — Vertical Slice/Alpha scope, not MVP") and the
concept prototype's own stated MVP boundary ("no T/L-shape specials — runs
of 4/5 only," `REPORT.md`, Shortcuts taken). This document does not design
Wrapped Candy's creation or activation rules.

Because Board Engine deliberately owns no notion of "T/L shape" — its
`runs` payload is a flat list of straight runs, and any L/T-intersection
recognition is explicitly left to a seam-3 consumer to compute from that
raw list if it chooses to (`board-engine.md` § Detailed Rules 4,
"T/L-shape *recognition* is not a Board Engine concern... now or as a
future versioned extension") — this document's MVP resolver simply **does
not compute that recognition at all**. It evaluates every run independently
by length alone (§ Detailed Rules 2) and resolves any same-step overlaps
between two or more runs purely through the length-based Cluster Precedence
rule (§ Detailed Rules 4), with no L/T-specific branch anywhere in the
logic.

**Concrete consequence, stated explicitly per the task requirement:** when
a swap creates an L/T intersection whose longest arm never reaches length
4 (the "pure" T/L case — e.g., two straight length-3 runs crossing at one
cell), **no special is spawned.** Both runs' cells clear as an ordinary
match — Board Engine's own overlap/intersection union rule (§ Detailed
Rules 4) already merges them into one clear set and fires exactly one
`match_cleared`, with zero seam-3 involvement needed. This is the entire
MVP behavior for a T/L shape: it reads to the player as a slightly bigger
plain match, never as a spawn, never as a missing reward — there is no
"almost got a Wrapped Candy" state to communicate, because MVP has no
concept of Wrapped Candy to almost-get.

### 4. Cluster Precedence (Same-Step Overlapping Runs)

A single cascade step can report multiple `Run` entries that share cells
(an L/T intersection) as well as multiple entries that share nothing at all
(two unrelated match-4s elsewhere on the board from one big cascade). This
rule resolves both cases with one formula (Formula 3):

- **Disjoint runs** (no shared cells) are entirely independent — each is
  evaluated and, if eligible, spawns on its own. Two separate match-4s in
  one cascade step correctly produce two separate Striped candies, each
  with its own `special_spawned` event, in the same Clearing pass (see
  Edge Cases, "two specials created same step").
- **A cluster** — one or more runs connected by shared cells — grants **at
  most one spawn**, chosen by the longest eligible (`length ≥ 4`) run in
  the cluster. This directly implements the precedence ladder the task
  specifies ("5-run beats 4-run beats T/L"): a length-5 run always outranks
  a length-4 run in the same cluster; a run of exactly 3 is never eligible
  regardless of what it intersects. If no run in the cluster reaches length
  4, the cluster grants **zero** spawns — this is the formal statement of
  § Detailed Rules 3's "pure T/L clears with no spawn" rule, expressed as a
  special case of the same general formula rather than a separate branch.
- **Ties** (two runs of equal, eligible length in the same cluster — a rare
  "big X" configuration) resolve by preferring the run containing a
  `swap_anchor_cells` cell (rewarding the run the player's actual swap
  touched), then by the run whose lexicographically smallest `(row, col)`
  cell is smaller (a fixed, deterministic, arbitrary-but-stable tiebreak —
  see Formula 3).
- The run that does **not** win the cluster's spawn still has every one of
  its cells clear normally — nothing about "losing" the precedence check
  removes a cell from the clear set; it only removes that run's *eligibility
  to name a spawn*. This requires no extra logic on Board Engine's side:
  this resolver's `resolve_special_spawns` return value simply omits those
  cells from its `Map[cell, SpecialSpawn]`, and Board Engine's own contract
  already treats every non-exempted cell as a normal clear.

### 5. Swap-Triggered Combo Matrix (Seams 1 & 2)

**Scope boundary — what needs seam 1/2 at all.** Striped candies retain a
real `color` (§ Detailed Rules 1), so they participate in Board Engine's
*normal* run detection exactly like any regular candy — a Striped candy
sitting among same-colored neighbors can be matched into a fresh run of 3+
with **zero seam-1/2 involvement**, and that match's clear (plus this
document's seam-4 expansion, § Detailed Rules 6) is entirely sufficient to
fire its line. Color Bomb, by contrast, is colorless (`COLOR_NONE`) and can
*never* participate in a normal run — it has no "back door" into activation
through ordinary matching, so it structurally requires seam 1/2 to be
usable at all. This is why the combo matrix below is exactly four cells,
not a full cross-product of every special-on-special/special-on-regular
pairing:

| Piece A | Piece B | Seam 1 result | Seam 2 clear set (Formula #) | Notes |
|---|---|---|---|---|
| `COLOR_BOMB` | Regular candy (any color) | `true` | Formula 4 — every cell of Piece B's color, plus the bomb's own cell | The baseline, unconditional bomb activation — always legal regardless of whether a normal match would also form |
| `COLOR_BOMB` | `COLOR_BOMB` | `true` | Formula 5 — every currently `OCCUPIED` cell | Independent of how many distinct colors exist on the board (see Edge Cases) |
| `COLOR_BOMB` | `STRIPE_H` / `STRIPE_V` | `true` | Formula 7 — every cell sharing the Striped piece's color, each contributing its own full row (`STRIPE_H`) or column (`STRIPE_V`) | The "jackpot" combo `REPORT.md` flagged as the best moment — redesigned to fit the current seam 2 signature, see Cross-References |
| `STRIPE_H` / `STRIPE_V` | `STRIPE_H` / `STRIPE_V` | `true` | Formula 6 — each piece fires its own line from its **post-swap** landing cell | Legal and defined regardless of whether the two colors match — color is irrelevant to this combo, only orientation and landing position matter |
| `STRIPE_H` / `STRIPE_V` | Regular candy, no resulting match | `false` | — (falls through to normal swap validity, § board-engine.md Formula 2) | Deliberate MVP scope cut — see below |
| `COLOR_BOMB` or `STRIPE_*` | `WRAPPED` | N/A — structurally unreachable at MVP | — | No MVP creation rule ever produces a `WRAPPED` piece (§ Detailed Rules 3); revisit at Vertical Slice |
| `WRAPPED` | `WRAPPED` | N/A — structurally unreachable at MVP | — | Same as above |

**Seam 1 dispatch rule, stated as a single deterministic function:**

```
is_special_activation_swap(piece_a, piece_b) =
    (piece_a.special_type == COLOR_BOMB) OR (piece_b.special_type == COLOR_BOMB)
    OR (piece_a.special_type ∈ {STRIPE_H, STRIPE_V} AND piece_b.special_type ∈ {STRIPE_H, STRIPE_V})
```

**Seam 2 dispatch rule** (only evaluated when seam 1 above returned `true`),
in priority order — both-bomb checked first since it would otherwise also
satisfy the "at least one bomb" bomb+color branch:

```
resolve_special_activation_clears(piece_a, piece_b) =
    Formula 5   if piece_a.special_type == COLOR_BOMB AND piece_b.special_type == COLOR_BOMB
    Formula 7   if exactly one of piece_a/piece_b is COLOR_BOMB AND the other is STRIPE_H or STRIPE_V
    Formula 4   if exactly one of piece_a/piece_b is COLOR_BOMB AND the other is a regular candy
    Formula 6   if piece_a.special_type ∈ {STRIPE_H,STRIPE_V} AND piece_b.special_type ∈ {STRIPE_H,STRIPE_V}
```

**Deliberate MVP scope cut — no "solo Striped activation via swap-with-
anything."** Many genre incumbents (including Candy Crush Saga) let a
player fire a Striped candy by swapping it with *any* adjacent regular
candy, even when that swap would not otherwise form a match. This document
does **not** implement that at MVP: swapping a Striped candy with a regular
candy that doesn't complete a run is judged invalid by Board Engine's
ordinary Formula 2 and reverts, exactly like swapping two regular candies
with no match. **Justification:** (a) the concept prototype's validated
behavior never described or tested a solo-swap-fire mechanic — extending
scope beyond what was validated is an unforced risk; (b) `game-concept.md`'s
MVP bullet reads "Special candies... plus their **basic combos**," which
this reads as special-on-special, not solo-manual-fire; (c) restricting
seam 1 to exactly `{any Bomb-involving swap, Striped+Striped}` is a smaller,
fully auditable implementation surface for an MVP; (d) nothing is lost
strategically — a player who wants to fire a Striped candy only needs to
set up *any* new run of 3 through it, which Board Engine's existing match
detection already handles at zero extra implementation cost, fully
preserving the "it sits there until I choose to cash it in" fantasy. This
scope cut is logged for a possible post-MVP revisit — see Open Questions.

### 6. Passive Chain-Reaction Rules (Seam 4)

**General algorithm.** Every call to `expand_special_chain_reaction(cleared_set)`
performs exactly one single, non-recursive pass (per `board-engine.md`'s
seam-4 calling contract — Board Engine itself owns the fixpoint loop, this
resolver never loops internally): for every cell in the current
`cleared_set` whose piece has a non-`SPECIAL_NONE` `special_type`, compute
that piece's own **line-or-target contribution** (below) and return
`cleared_set ∪ (the union of every contribution)`. Because a piece that is
already fully accounted for (all of its contribution cells are already in
the input set) contributes nothing new, Board Engine's own fixpoint
detection (its next call returns the same set unchanged) terminates the
loop naturally — no special "already processed" bookkeeping is needed in
this resolver at all.

| Caught special | Contribution |
|---|---|
| `STRIPE_H` | Every `OCCUPIED` cell in the piece's own row |
| `STRIPE_V` | Every `OCCUPIED` cell in the piece's own column |
| `COLOR_BOMB` | Every `OCCUPIED` cell matching the **target color** (Formula 8, resolved once per step against the pre-clear board state) |

This is the exact mechanism behind the concept prototype's validated "best
moment" — a Striped candy's row sweep can catch a second Striped candy sitting
in that row, whose own column sweep can catch a third, and so on, entirely
without further player input, bounded only by `MAX_CHAIN_EXPANSION_ITERATIONS`
(`board-engine.md` Tuning Knobs, default `10`; see § Edge Cases for the
interplay).

**Passive Color Bomb detonation — the deterministic resolution to the
prototype's "unearned" finding.** When a `COLOR_BOMB` piece is caught inside
a clear set via chain expansion (i.e., it was **not** one of the two pieces
in a direct swap-activation — that path is already fully specified by § 5's
combo matrix), it still detonates — it is never silently defused — using a
fixed, board-inspectable rule: it clears every cell matching the board's
**single most common color at that instant** (Formula 8). Because this rule
is deterministic and identical every time it is evaluated against the same
board state, it satisfies Pillar 2's readability bar exactly as written
("randomness amplifies skill; it doesn't replace it," `rng-service.md`
Player Fantasy) — an experienced player can deliberately engineer a cascade
to route a bomb into a clear set, knowing precisely what color it will take
out before it happens. See § Detailed Rules 6's Player Fantasy framing and
Formula 8's Edge Cases for the (structurally rare) case where no colored
piece exists on the board to target.

**Rejected alternative (documented, not chosen).** The alternative
recommendation was to **defuse** a passively-caught bomb into an ordinary
single-cell clear (no bonus effect at all) whenever it was not directly
swap-activated. This was rejected because it would remove the exact
mechanic `REPORT.md` called dramatic, trading away real Pillar-1 juice value
for a fairness concern the deterministic rule already fully resolves — the
"unearned" complaint was about *unpredictability*, not about the mechanic's
existence or scale, and the deterministic rule fixes the former while
preserving the latter.

### 7. Reshuffle Interaction

Special pieces survive a mid-game reshuffle intact. `board-engine.md`
§ Detailed Rules 11 defines reshuffle as reassigning existing pieces'
`(color, special_type)` pairs across cells, never clearing/spawning/placing
new ones — a not-yet-activated Striped or Color Bomb keeps its exact
identity and simply relocates to a new cell along with the shuffle. This
document defines no additional reshuffle-specific behavior; none is needed.

### 8. RNG Usage at MVP: None

**Decision: MVP consumes zero randomness from the `special-drop` stream
(stream_id 2, `rng-service.md` § Stream Registry).** Every rule in this
document — creation eligibility (Formula 1), anchoring (Formula 2), cluster
precedence (Formula 3), every combo's clear set (Formulas 4–7), and passive
detonation's target color (Formula 8) — is a pure, deterministic function of
board state and run geometry. None of them need a random draw to produce a
correct, well-defined result.

**Justification.** (a) **Determinism and readability directly serve Pillar
2** — every creation and combo rule in this document must be "readable/
predictable" (`game-concept.md`, Pillar 2 design test); introducing RNG into
*which* special spawns, or *what* a combo clears, would directly undercut
that. (b) The prototype's own flagged risk (passive bomb detonation feeling
"unearned") is resolved in this document specifically by *removing*
randomness from an outcome, not adding it — the opposite of what a
`special-drop` consumer would do. (c) `board-engine.md`'s determinism
contract (§ Detailed Rules 12: byte-identical final state and signal
sequence given an identical seed and intent sequence) extends trivially to
every rule in this document precisely because none of them touch RNG Service
at all — there is nothing to prove deterministic beyond what the formulas
themselves already guarantee.

**Stream status, recorded for `rng-service.md`'s own bookkeeping.**
`special-drop` (stream_id 2) remains **Reserved, unconsumed at MVP** — its
existing status in `rng-service.md`'s registry is confirmed correct and
unchanged by this document. Per that document's own stated policy ("a
registered stream... still receives a valid derived sub-seed at every
session start... zero cost, keeps `stream_id` numbering stable for the
future consumer"), this costs nothing and requires no action. A plausible
future consumer — e.g., a post-MVP "bonus chance for an extra special on a
big cascade" mechanic — is explicitly **not** part of this document's scope
and is logged only as a hypothetical in Open Questions.

### 9. Ingredient-Harvest Observation Point (Named, Not Designed)

Booster Brewing Meta (Phase 2, `systems-index.md` #12) will need per-color
harvest tallies derived from matches, including matches and clears this
document's specials and combos produce. This document names the mechanism
that will make that possible, without designing brewing itself, per the
task's explicit scope boundary:

- **The Harvest Observation Point** is not a new API, signal, or seam —
  it is a documented commitment that this document's implementation never
  strips per-piece color identity from any clear path it touches. Every
  cell this document's combos or passive chains clear is cleared through
  Board Engine's own `match_cleared`/`special_activated` signals
  (`board-engine.md` § Detailed Rules 7), whose `cleared_pieces` payload
  already carries full `PieceSnapshot` identity (`cell`, `color`,
  `special_type`) for every cleared cell, satisfying § Detailed Rules 13's
  deferred-replay guarantee.
- **Concretely:** a future Booster Brewing Meta tally can sum
  `cleared_pieces` entries keyed by `color` across every `match_cleared`
  and `special_activated` event of a move — including cells cleared by a
  Bomb+Color wipe, a Bomb+Striped jackpot, or a passive chain — with **zero
  API changes to this document or to Board Engine**, exactly the same
  mechanism `level-objectives.md`'s future `collect_color` objective will
  use (`board-engine.md` § Detailed Rules 13's worked walkthrough).
- **What this document does not do:** define ingredient types, yield
  multipliers, or any brewing-specific terminology. That remains entirely
  Booster Brewing Meta's scope when its Phase 2 gate is passed.

### 10. Seam Implementation Summary (Signature Confirmation)

Confirmed against `board-engine.md` § Detailed Rules 3's exact signatures,
reciprocally as that document's Dependencies table requires:

| Seam | Board Engine signature | This document's implementation |
|---|---|---|
| 1 | `is_special_activation_swap(piece_a, piece_b) -> bool` | § Detailed Rules 5's dispatch rule |
| 2 | `resolve_special_activation_clears(piece_a, piece_b) -> Set[cell]` | § Detailed Rules 5's dispatch rule, Formulas 4–7 |
| 3 | `resolve_special_spawns(runs, swap_anchor_cells) -> Map[cell, SpecialSpawn]` | § Detailed Rules 2 & 4, Formulas 1–3 |
| 4 | `expand_special_chain_reaction(cleared_set) -> Set[cell]` | § Detailed Rules 6, Formula 8 |

No seam's signature is extended, narrowed, or reinterpreted. In particular,
the Bomb+Striped combo (§ Detailed Rules 5) was deliberately **redesigned**
during this document's authoring to fit seam 2's existing `Set[cell]`
return type rather than requesting the v2 step-plan composite sketch
`board-engine.md`'s Open Questions flagged as a possible future need — see
Cross-References for the rejected composite-spawn alternative and why the
current seams already suffice.

---

## Formulas

### Formula 1 — Special Creation Eligibility & Type Mapping

**Named expression:**
```
spawn_type(run) =
    NONE                                    if run.length == 3
    (STRIPE_H if run.orientation == HORIZONTAL else STRIPE_V)   if run.length == 4
    COLOR_BOMB                              if run.length >= 5

spawn_color(run) =
    run.color        if spawn_type(run) ∈ {STRIPE_H, STRIPE_V}
    null (→ COLOR_NONE)   if spawn_type(run) == COLOR_BOMB
    n/a               if spawn_type(run) == NONE
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `run.length` | int | `≥ 3` (Board Engine's `MIN_RUN_LENGTH`) | Length of the straight run, as reported by `board-engine.md` §4 |
| `run.orientation` | enum | `{HORIZONTAL, VERTICAL}` | The run's own orientation, from `Run` |
| `run.color` | int | `[0, pool_size-1]` | The run's shared color, from `Run` (`board-engine.md` §4) |
| `spawn_type(run)` | enum | `{NONE, STRIPE_H, STRIPE_V, COLOR_BOMB}` | This document's closed 4-value output, deterministic function of `length` and `orientation` alone |
| `spawn_color(run)` | int or `null` | `[0, pool_size-1]` or `null` | The `SpecialSpawn.color` value passed to Board Engine's seam 3 |

**Output range**: a closed, 4-value categorical enumeration — never any
other value, and never probabilistic. `NONE` means "this run contributes
no spawn candidate" (it is not part of seam 3's returned map at all, and
its cells clear normally, subject to Formula 3's cluster resolution).

**Worked example**: a horizontal run `{cells: [(5,2),(5,3),(5,4),(5,5)],
length: 4, orientation: HORIZONTAL, color: 1}` (citrus) → `spawn_type =
STRIPE_H`, `spawn_color = 1`. A vertical run `{length: 5, orientation:
VERTICAL, color: 3}` (apple) elsewhere on the same board →
`spawn_type = COLOR_BOMB`, `spawn_color = null` → Board Engine assigns
`COLOR_NONE (-1)` per its own seam-3 contract.

---

### Formula 2 — Spawn Anchor Cell Resolution

**Named expression** (geometric, not array-index-dependent — computed from
the run's own coordinate bounds so it holds regardless of any particular
`Run.cells` storage order):

```
mid_offset(length) = floor((length - 1) / 2)

run_middle_cell(run) =
    (run.cells[0].row, min_col(run) + mid_offset(run.length))   if run.orientation == HORIZONTAL
    (min_row(run) + mid_offset(run.length), run.cells[0].col)   if run.orientation == VERTICAL

overlap(run, swap_anchor_cells) = run.cells ∩ swap_anchor_cells

anchor(run, swap_anchor_cells) =
    the sole element of overlap(run, swap_anchor_cells)                         if |overlap| == 1
    nearer-to-run_middle_cell element of overlap, tie -> lexicographically smaller (row, col)   if |overlap| == 2
    run_middle_cell(run)                                                         if |overlap| == 0
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `run.length` | int | `≥ 4` (only eligible runs reach this formula) | The winning run's length |
| `min_col(run)`, `min_row(run)` | int | within grid bounds | Minimum column/row among the run's cells (its "start" edge) |
| `mid_offset(length)` | int | `[1, 2]` for `length ∈ {4,5}` | Zero-based offset from the run's start edge to its middle cell |
| `swap_anchor_cells` | Set[cell] | `{}`, `{a}`, or `{a,b}` | Per `board-engine.md` § Detailed Rules 3: the triggering swap's two cells at `chain_index=1` + `trigger_source ∈ {SWAP_MATCH, SPECIAL_ACTIVATION}`; empty otherwise |
| `overlap(run, swap_anchor_cells)` | Set[cell] | `0–2` elements | Which swap-anchor cells (if any) fall within this run |
| `anchor(run, swap_anchor_cells)` | cell | a member of `run.cells` | The resolved spawn cell — always a real cell of the run, guaranteed to pass Board Engine's own defensive cell-membership validation |

**Output range**: exactly one cell, always drawn from `run.cells` — never
out of bounds, never a cell the run doesn't actually occupy.

**Worked example 1 (swap cell interior to the run, both anchors inside)**:
`run.cells = [(5,2),(5,3),(5,4),(5,5)]`, `length=4`, `orientation=HORIZONTAL`,
`swap_anchor_cells = {(5,3),(5,4)}` (the swap happened between the run's own
middle two cells). `mid_offset(4) = floor(3/2) = 1` →
`run_middle_cell = (5, 2+1) = (5,3)`. `overlap = {(5,3),(5,4)}`, size 2 →
compare distance to `(5,3)`: `(5,3)` is 0 away, `(5,4)` is 1 away → nearer
is `(5,3)` → **`anchor = (5,3)`**.

**Worked example 2 (no swap anchor available — cascade step 2+)**:
`run.cells = [(2,1),(2,2),(2,3),(2,4)]`, `length=4`, `orientation=HORIZONTAL`,
`swap_anchor_cells = {}` (empty, per `board-engine.md`'s rule for
`chain_index ≥ 2`). `mid_offset(4)=1` → **`anchor = (2, 1+1) = (2,2)`**.

---

### Formula 3 — Cluster Precedence Resolution

**Named expression:**
```
cluster(runs) = connected components of runs, where two runs are adjacent
                iff they share ≥1 cell (an L/T intersection)

eligible(cluster) = { r ∈ cluster : r.length >= 4 }

winning_run(cluster) =
    undefined                                                      if eligible(cluster) = ∅
    argmax_{r ∈ eligible(cluster)} r.length,
        ties -> prefer r with (r.cells ∩ swap_anchor_cells) ≠ ∅,
        further ties -> prefer r whose lexicographically smallest cell is smaller

spawn(cluster) =
    { anchor(winning_run(cluster), swap_anchor_cells) : spawn_type(winning_run(cluster)) at spawn_color(winning_run(cluster)) }   if winning_run(cluster) is defined
    ∅   otherwise
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `runs` | Array[Run] | one cascade step's full run list | Raw, pre-union runs Board Engine passes to seam 3 |
| `cluster(runs)` | Array[Set[Run]] | `≥ 0` clusters | Runs grouped by cell-sharing connectivity |
| `eligible(cluster)` | Set[Run] | `⊆ cluster` | Only runs with `length ≥ 4` |
| `winning_run(cluster)` | Run or undefined | — | The single run (if any) that earns this cluster's spawn |
| `spawn(cluster)` | `{cell: SpecialSpawn}` or `∅` | `0` or `1` entries | This cluster's contribution to seam 3's returned map |

**Output range**: **at most one** `SpecialSpawn` entry per cluster, always —
this is what guarantees an L/T intersection can never produce two competing
spawns at the same cell (see Edge Cases).

**Worked example 1 (5-run beats 4-run, intersecting)**: a swap creates
`Run A = {orientation: HORIZONTAL, length: 4, color: 1}` and
`Run B = {orientation: VERTICAL, length: 3, color: 1}`, sharing one cell.
`cluster = {A, B}`. `eligible = {A}` (only A reaches length 4) →
`winning_run = A` → `spawn = {anchor(A, ...): {STRIPE_H, color:1}}`. Run
B's remaining 2 cells (its 3rd is the shared cell, already counted under A)
clear normally with no spawn of their own. Total cells cleared this step:
`4 + 3 - 1 = 6`; total spawns: `1`.

**Worked example 2 (pure T/L, no arm reaches 4 — the MVP deferral case)**:
`Run A = {orientation: HORIZONTAL, length: 3}`, `Run B = {orientation:
VERTICAL, length: 3}`, sharing one cell. `cluster = {A,B}`,
`eligible(cluster) = ∅` → `winning_run` undefined → `spawn(cluster) = ∅`.
All `3+3-1=5` cells clear as one ordinary `match_cleared`, zero spawns —
this is the formal statement of § Detailed Rules 3's deferred-Wrapped
behavior.

---

### Formula 4 — Bomb + Color Clear Set

**Named expression:**
```
clear_set = {bomb_cell} ∪ { c ∈ board : board.color(c) == partner.color }
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `bomb_cell` | cell | the `COLOR_BOMB` piece's own position | Always included, even if it happens to share `partner.color`'s domain trivially |
| `partner` | Piece | the non-bomb swapped piece | Must have a valid `color ≠ COLOR_NONE` by construction (a swap only ever involves two currently `OCCUPIED`, therefore validly-colored-or-colorless-special, pieces) |
| `board.color(c)` | function | — | The color of the piece occupying cell `c`, or excluded if `c` is not `OCCUPIED` |
| `clear_set` | Set[cell] | `[1, playable_cells]` | Every matching-color cell plus the bomb cell itself |

**Output range**: bounded below by `1` (the degenerate case where no other
cell shares `partner.color`) and above by `playable_cells` (the degenerate
case where every playable cell happens to share that color).

**Worked example (exact)**: on an 8×8 board (`64` playable cells), suppose
the live board has exactly `13` cells of `partner.color` (citrus) at the
moment of activation → `clear_set` has exactly `14` cells (`13 + 1` bomb
cell), deterministically, from the real board state — no estimation needed
for this formula, since Board Engine always exposes the exact live count.

**Expected-value estimate (for design-time intuition only, not a runtime
formula)**: assuming a uniform color distribution across a 5-color pool
(`rng-service.md`'s launch default) on a full 64-cell board,
`E[monochrome_cells] = 64 / 5 = 12.8` → `E[clear_set] ≈ 12.8 + 1 = 13.8 ≈
14 cells`. Real boards deviate from uniform (matches preferentially remove
cells of the majority color as play progresses), so this is an
order-of-magnitude sanity check, not a guarantee.

---

### Formula 5 — Bomb + Bomb Clear Set

**Named expression:**
```
clear_set = { c ∈ board : cell_state(c) == OCCUPIED }
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `cell_state(c)` | enum | `{VOID, EMPTY, OCCUPIED}` | Board Engine's own per-cell state (`board-engine.md` § Detailed Rules 1) |
| `clear_set` | Set[cell] | `[2, playable_cells]` | Every currently occupied cell — includes both bomb cells themselves |

**Output range**: exact, not probabilistic — deliberately **independent of
how many distinct colors exist on the board** (see Edge Cases, "bomb
swapped with bomb when `<2` colors on board"). Lower bound `2` (a
degenerate board holding only the two swapped bombs).

**Worked example**: 8×8, full rectangle, no voids, board otherwise fully
occupied → `clear_set` = all `64` cells, every time, regardless of the
board's color composition at that instant.

---

### Formula 6 — Striped + Striped Clear Set

**Named expression:**
```
line(H, cell) = { c ∈ board : c.row == cell.row AND cell_state(c) == OCCUPIED }
line(V, cell) = { c ∈ board : c.col == cell.col AND cell_state(c) == OCCUPIED }

clear_set = line(piece_a.orientation, cell_b) ∪ line(piece_b.orientation, cell_a)
```

Each piece fires from its **post-swap landing cell** — `piece_a` (which
moved into `cell_b`) fires along its own orientation from `cell_b`;
`piece_b` (which moved into `cell_a`) fires along its own orientation from
`cell_a`. This matches what the player's eye tracks during the swap slide
(the two pieces visibly cross paths, then each detonates from where it now
sits) — a direct Pillar-2-motivated readability choice, not an arbitrary
one.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `piece_a.orientation`, `piece_b.orientation` | enum | `{HORIZONTAL, VERTICAL}` | Each Striped piece's own, already-fixed orientation from its original creation |
| `cell_a`, `cell_b` | cell | the swap's two cells | `cell_a` is `piece_a`'s pre-swap position (now `piece_b`'s landing cell), and vice versa |
| `line(H/V, cell)` | Set[cell] | `[1, cols]` or `[1, rows]` | A full row or column of `OCCUPIED` cells |
| `clear_set` | Set[cell] | see cases below | Union of the two lines |

**Output range — three distinct deterministic outcomes on an 8×8 board**,
depending on the orientation pairing and the swap's own direction (not
probabilistic — fully determined by the inputs):

| Swap direction | Orientation pairing | Result | Cell count (8×8) |
|---|---|---|---|
| Horizontal swap (same row) | Both `STRIPE_H` | Both lines are the *same* row | `8` |
| Horizontal swap (same row) | Both `STRIPE_V` | Two distinct, non-overlapping columns | `16` |
| Horizontal swap (same row) | One H, one V | One row ∪ one column, crossing at 1 cell | `15` |
| Vertical swap (same column) | Both `STRIPE_V` | Both lines are the *same* column | `8` |
| Vertical swap (same column) | Both `STRIPE_H` | Two distinct, non-overlapping rows | `16` |
| Vertical swap (same column) | One H, one V | One row ∪ one column, crossing at 1 cell | `15` |

**Worked example**: a vertical swap between two `STRIPE_H` candies at
`(2,4)` and `(3,4)` on an 8×8 board. Post-swap, the piece that was at
`(2,4)` now sits at `(3,4)` and fires its row (row 3, 8 cells); the piece
that was at `(3,4)` now sits at `(2,4)` and fires its row (row 2, 8 cells).
Rows 2 and 3 share no cells → `clear_set` has exactly `16` cells.

---

### Formula 7 — Bomb + Striped Clear Set ("Jackpot" Combo)

**Named expression:**
```
clear_set = ⋃_{c ∈ board : board.color(c) == partner.color} line(partner.orientation, c)
```

Every cell on the board sharing the Striped piece's color contributes its
**own** full row (if `partner.orientation == STRIPE_H`) or column (if
`STRIPE_V`) — not just the partner's own cell. This is the MVP-fitting
redesign referenced in § Detailed Rules 10: it reproduces the full dramatic
"every matching candy detonates its line" result using only seam 2's
existing `Set[cell]` return type, with no intermediate spawn step and no
composite v2 seam signature required.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `partner` | Piece | the swapped Striped piece | `partner.color` and `partner.orientation` are both already fixed from its original creation |
| `line(orientation, c)` | Set[cell] | `[1, cols]` or `[1, rows]` | Same function as Formula 6 |
| `clear_set` | Set[cell] | `[cols or rows, playable_cells]` | Union across every same-colored cell's contributed line |

**Output range**: lower-bounded by one full line (the partner's own
color's minimum possible presence is itself, contributing at least its own
line), upper-bounded by the full board.

**Expected-value estimate (design-time intuition, birthday-problem-style
approximation — explicitly caveated as an order-of-magnitude estimate, not
exact)**: for an 8×8 board, 5-color pool, `E[monochrome_cells] = 12.8`
(Formula 4). Approximating each monochrome cell as an independent uniform
draw among 8 rows (or columns):

```
E[distinct_lines_touched] = grid_dim × (1 − ((grid_dim−1)/grid_dim)^E[monochrome_cells])
E[clear_set]              = E[distinct_lines_touched] × grid_dim
```

```
E[distinct_lines_touched] = 8 × (1 − (7/8)^12.8) = 8 × (1 − 0.181) ≈ 6.55
E[clear_set]               ≈ 6.55 × 8 ≈ 52 cells   (~82% of a 64-cell board)
```

This is the formula-level confirmation of why the prototype and this
document both treat Bomb+Striped as the single biggest MVP payoff — even a
mid-size same-color cluster is likely to touch the majority of the board's
rows (or columns). **Caveat**: real boards are not uniformly distributed
(prior matches have already skewed the remaining color distribution), so
treat this as an order-of-magnitude estimate to validate against real
playtest data at Vertical Slice, not a runtime-enforced value.

---

### Formula 8 — Passive Chain Expansion: Target-Color Resolution

**Named expression:**
```
count(board_state, k) = |{ c ∈ board_state : cell_state(c) == OCCUPIED AND color(c) == k }|

target_color(board_state) =
    argmax_{k ∈ color_pool_indices} count(board_state, k),
        ties -> smallest k (lowest color_pool index)

passive_bomb_contribution(board_state) = { c ∈ board_state : color(c) == target_color(board_state) }
```

`board_state` is evaluated **once**, against the board as it stood at the
start of the current Clearing pass (before any cell in this step actually
clears) — stable and identical across every seam-4 call within the same
step, since Board Engine does not mutate grid occupancy until after the
seam-4 fixpoint loop completes. Two bombs caught in the same step
independently compute the identical `target_color`, so their contributions
are naturally idempotent under set union — no extra bookkeeping is needed
to avoid a conflicting or duplicated result.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `board_state` | board snapshot | — | The live board at the start of the current Clearing pass |
| `k` | int | `color_pool_indices` (`[0, pool_size-1]`) | A candidate color |
| `count(board_state, k)` | int | `≥ 0` | Live count of `OCCUPIED` cells of color `k` |
| `target_color(board_state)` | int or undefined | one of `color_pool_indices`, or undefined if no colored piece exists | The deterministic detonation target |
| `passive_bomb_contribution` | Set[cell] | `[0, playable_cells]` | Cells added to the clear set by this passively-caught bomb |

**Output range**: `target_color` is always a currently-present color
(guaranteed by construction, since `argmax` only ranges over colors with
`count ≥ 1`) except in the structurally rare case where the board holds
zero colored (non-special) `OCCUPIED` cells at all — see Edge Cases for
the defined fallback.

**Worked example**: mid-cascade live counts (specials excluded):
strawberry=`11`, citrus=`9`, lemon=`14`, apple=`10`, grape=`9` →
`argmax = lemon` (`14`) → `target_color = lemon` → the passively-caught
bomb's contribution is all `14` lemon cells, unioned into the step's
growing clear set.

---

### Formula 9 — Creation-Frequency Order-of-Magnitude Estimate

**Explicitly caveated as a heuristic extrapolation, not validated data** —
mirroring `board-engine.md`'s own Formula 6 disclaimer ("no deterministic
upper bound... a heuristic approximation, not exact combinatorial
enumeration"). No per-run-length distribution was captured by the concept
prototype (`REPORT.md` records only an aggregate score and a single
observed `×4` chain), so this formula extrapolates from Board Engine's own
per-cell coincidental-match heuristic (`1/K²` for a length-3 accidental
run, where `K` = color pool size) by one additional factor of `1/K` per
extra required length:

**Named expression:**
```
p_accidental(length, K) ≈ (1/K²) × (1/K)^(length − 3)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `length` | int | `{4, 5}` | The accidental run length being estimated |
| `K` | int | `[3, 5]` | `color_pool` size (`level-data-format.md` V11) |
| `p_accidental(length, K)` | float | `(0, 1)` | Approximate per-refill-opportunity probability of an *unintentional* run of exactly this length forming purely from cascade-refill coincidence |

**Output range**: a small probability, intentionally — this heuristic
exists to support a design conclusion, not to gate any runtime behavior.

**Worked example (`K = 5`, launch default)**:
```
p_accidental(4, 5) ≈ (1/25) × (1/5) = 1/125 = 0.008   (~0.8% per refill opportunity)
p_accidental(5, 5) ≈ (1/25) × (1/25) = 1/625 = 0.0016  (~0.16% per refill opportunity)
```

**Design conclusion (the actionable takeaway, not the raw numbers)**:
accidental cascade-refill coincidence is expected to be a **minor**
contributor to special creation at the launch 5-color pool — the large
majority of match-4/5s, and therefore the large majority of specials
created, are expected to come from **deliberate player setup**, not RNG
luck. This is a *good* outcome for Pillar 2 (the escalation ladder in
Player Fantasy should read as earned by planning, not granted by chance)
and is consistent with `board-engine.md`'s own Formula 6 conclusion that
deep, coincidence-driven chains are rare by design. **This estimate is
unvalidated against real play** — flagged for empirical replacement once
Vertical Slice levels exist, exactly as `board-engine.md`'s own Open
Questions flags its sibling Formula 6 for the same future validation.

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|---|---|---|
| A special spawn's anchor cell is simultaneously part of a *different* run's cell set (the L/T shared-cell case) | Cannot occur from this resolver's own output by construction — Formula 3 grants **at most one** `SpecialSpawn` per connected cluster, and that spawn's anchor is drawn from the single winning run. Board Engine's own defensive validation (`board-engine.md` § Detailed Rules 3, "any cell returned by seam 3... not part of the current step's actual board state... is silently dropped and logged") remains a second line of defense that this resolver is never expected to trigger. | Formula 3 is specifically designed to make cell-claim conflicts structurally impossible, not merely rare |
| Two specials are created in the same cascade step | Both spawn normally, each with its own `special_spawned` event in the same Clearing pass — this happens whenever a step contains two or more **disjoint** clusters (e.g., two unrelated match-4s surfaced by one big cascade). Each cluster's spawn is computed entirely independently via Formula 3. | Disjoint clusters share no cells and therefore share no resolution conflict; treating them independently is both correct and the simplest implementation |
| A swap creates a match-4/5 that, per Formula 3, loses the cluster precedence check to a longer run in the same cluster | The losing run's cells still clear normally — Formula 3 removing a run's *eligibility to name a spawn* never removes its cells from the clear set | Board Engine's clear set is the union of every run's cells regardless of spawn outcome (`board-engine.md` § Detailed Rules 4); this resolver's `SpecialSpawn` map only ever *exempts* cells, it never adds ones Board Engine wouldn't already clear |
| Bomb swapped with Bomb when the board has fewer than 2 distinct colors present | Formula 5's clear set (`every OCCUPIED cell`) is entirely color-independent — it is defined without reference to color diversity at all, so this scenario has no special case to handle. Only the Bomb+Color combo (Formula 4) needs a *specific* target color, and that combo's target is always well-defined by construction: a swap only ever involves two currently `OCCUPIED` pieces, and the non-bomb partner in a Bomb+Color swap always has a valid, non-`COLOR_NONE` color (a colorless piece can only be `COLOR_BOMB` itself, which routes to the Bomb+Bomb branch instead). | Explicitly resolves the task's flagged edge case: Bomb+Bomb simply has no color-count dependency to break |
| A Striped candy's row/column clear crosses a column boundary created by `cell_mask` voids (gravity segments) | No special handling is needed or implemented: Formula 6/7's `line(H/V, cell)` is defined as "every `OCCUPIED` cell sharing that row/column index," full stop — `VOID` cells are trivially excluded because they can never be `OCCUPIED`. Gravity *segments* (`board-engine.md` § Detailed Rules 9, Formula 3) are a per-**column** concept bounded by voids *within* that column; a horizontal row-clear crosses many columns, each with its own independent segment structure, so there is no coherent single "segment" a row-clear could even respect. | The segment concept simply does not apply to a cross-column line clear — stating this explicitly resolves the task's flagged concern by showing no new rule is needed, not by adding one |
| A cascade step's seam-4 fixpoint loop is engineered (e.g., a dense, mostly-special endgame board) to need many iterations to fully resolve a long chain of caught specials | Each of this resolver's seam-4 calls only ever adds contributions from specials *already present* in the input clear set — in the worst case, one previously-unprocessed special is newly caught per iteration, so a chain of `N` distinct specials needs at most `N` calls to reach a fixpoint. On a 9×9 board this could in principle approach `MAX_CHAIN_EXPANSION_ITERATIONS`'s default (`10`, `board-engine.md` Tuning Knobs) for a deliberately dense endgame configuration. If reached, Board Engine's own force-stabilize behavior applies unmodified (last-returned set finalizes, error-level diagnostic logged) — this resolver adds no override or exception to that contract. | Flagged in Tuning Knobs as a parameter worth revisiting once real dense-endgame boards exist to measure against; this resolver never assumes unlimited iterations |
| A special piece spawned by seam 3 this same step is immediately eligible for its own seam-4 expansion in the same Clearing pass | Never happens — Board Engine's resolution order (`board-engine.md` § Detailed Rules 3, "Matching → seam 3 → seam 4 → Clearing") removes seam-3-exempted cells from the clear set *before* seam 4 ever runs, so a freshly-created special is not part of the set seam 4 scans this step. It needs at least one further cascade step or player swap to be caught. | Matches the concept prototype's own model and standard genre convention — a special "sits and waits," it never immediately detonates itself |
| Two or more `COLOR_BOMB` pieces are caught in the same step's growing clear set via seam 4 | Each independently computes `target_color` against the same frozen `board_state` (Formula 8) and therefore always agrees — their contributions are identical sets, and set union is idempotent, so no conflicting or duplicated expansion occurs | `board_state` is fixed for the whole Clearing pass (Board Engine does not mutate grid occupancy mid-seam-4-loop), guaranteeing stability across repeated calls |
| A `COLOR_BOMB` is passively caught (seam 4) on a board with **zero** currently-`OCCUPIED` colored (non-special) cells — an all-specials board state | `target_color(board_state)` is undefined (Formula 8's `argmax` domain is empty) — the bomb contributes **no** expansion cells; it clears only as the single ordinary cell it already occupied within the input set, with no bonus effect this step | Formula 8's `argmax` is only ever evaluated over colors with `count ≥ 1`; an empty domain has no maximum, so the defined, safe fallback is "no additional contribution," never an error or a crash |
| A redundant seam-4 contribution re-adds cells already present in the growing clear set (e.g., a Striped candy's row overlaps cells another special already contributed) | No-op — set union is idempotent; Board Engine's own fixpoint detection (a call whose returned set equals its input) terminates the loop correctly regardless of how much redundancy occurred along the way | No special-cased "already contributed" bookkeeping is required in this resolver |
| Striped+Striped is swapped between two pieces of **different** colors | The combo fires exactly as Formula 6 defines — color plays no role in this combo's eligibility or clear set, only each piece's own orientation and its post-swap landing cell | Explicitly stated in § Detailed Rules 5's combo matrix to avoid any ambiguity about whether color must match |
| Bomb+Bomb is swapped when the only two `OCCUPIED` cells on the entire board are the two bombs themselves | Formula 5's clear set is exactly those two cells — a valid, if trivial, result; no special handling needed | Formula 5 is defined generically over `OCCUPIED` cells with no minimum-board-size assumption |
| A level's (future v2) `pre_placed_pieces` entry specifies a special candy at bootstrap, and that cell also happens to be part of an accidental bootstrap-time run (`board-engine.md` § Detailed Rules 2, step 8) | The special piece is treated exactly like any other piece for Board Engine's Matching pass — if its color (Striped) or lack thereof (Color Bomb, always excluded from color-based runs) makes it part of a detected run, it clears via that bootstrap cascade like anything else; if not, it survives bootstrap intact at its authored cell. This document adds no special-cased bootstrap behavior. | Consistent with `board-engine.md`'s own stated policy that pre-placed pieces are "trusted level-author intent, never silently altered," and requires no new logic since a pre-placed Striped candy is a normal, colored, matchable piece from move 1 |
| A level's `move_limit` is exhausted or a level ends mid-resolution with an un-activated special still on the board | Out of this document's scope — level end/win/lose evaluation belongs to Level Objective & Move-Limit System (`systems-index.md` #7, not yet authored); this document defines no end-of-level special-candy behavior | Matches this document's own scope boundary: it owns creation and combo *mechanics*, never win/lose evaluation |

---

## Dependencies

| System | Direction | Nature of Dependency |
|---|---|---|
| Match-3 Board Engine (`design/gdd/board-engine.md`, APPROVED — Revision 2) | Special Candies depends on it | Implements all four extension seams exactly per their documented signatures (§ Detailed Rules 10's confirmation table) and subscribes to no signal Board Engine doesn't already emit — `Run.orientation`/`Run.color` (§4), `swap_anchor_cells` (§3), and every `PieceSnapshot`-bearing signal (§7) are consumed as-is, with zero requested changes to that document's contract. **Reciprocal confirmation**: this satisfies `board-engine.md`'s own Dependencies table row for this document ("when authored, its Dependencies section must list this document and confirm its seam implementations against the signatures in § Detailed Rules 3") — done, § Detailed Rules 10. |
| RNG Service (`design/gdd/rng-service.md`, APPROVED) | Special Candies depends on it (reservation only) | Reserves the `special-drop` stream (stream_id 2) but consumes **zero** draws from it at MVP (§ Detailed Rules 8) — every rule in this document is deterministic. This confirms and closes `rng-service.md`'s own Dependencies table note ("this document only reserves the infrastructure, `special-candies.md` owns the semantics of what (if anything) it draws... to be confirmed in `special-candies.md` when authored") — confirmed: nothing, at MVP. |
| Level Data Format (`design/gdd/level-data-format.md`, APPROVED) | Special Candies depends on it (creation-context only) + proposes a v2 extension | Reads no new fields directly (creation is entirely runtime, driven by Board Engine's `runs`/`swap_anchor_cells`), but **proposes** the v2 extension `level-data-format.md` § Detailed Rules 6 already anticipated ("Extend `pre_placed_pieces` entries with an optional `special_type` field... once that vocabulary exists"). Concrete v2 ask, ready for that document's next revision pass: add an optional `special_type: String` field to each `pre_placed_pieces` entry, one of `{"striped_h", "striped_v", "color_bomb"}` (absent = regular candy, matching v1's existing default exactly — no behavior change for any existing level file). Validation addition needed at that time: if `special_type == "color_bomb"`, `candy_type` must be **absent** (color bombs are colorless, per § Detailed Rules 1's `COLOR_NONE` handling); if `special_type ∈ {"striped_h","striped_v"}`, `candy_type` remains **required**, exactly as v1, and supplies the striped piece's color. Per `level-data-format.md`'s own Versioning & Migration policy, this is a **new optional field with a documented default** — it requires **no `schema_version` bump**. This document deliberately does **not** specify the `locked: bool` flag that same anticipated-extension row mentions — locking a piece is a blocker/objective concept, out of this document's scope, and is flagged here as Level Objective & Move-Limit System's (#7) future territory, not Special Candies'. |
| Scoring & Star Thresholds (`design/gdd/scoring-stars.md`, Draft) | Scoring depends on Special Candies | **Confirmed, now that Scoring is authored**: Scoring consumes `match_cleared`'s `cleared_pieces` **only** — `special_activated` is deliberately never read for point totals, to avoid double-counting the same cell across both signals (`scoring-stars.md` § Detailed Rules 1). This document's `special_type` vocabulary (§ Detailed Rules 1) is the domain of Scoring's `activation_bonus` function; this document defines *which cells clear and why*, never a point value. **Reciprocal note fulfilled**: Scoring's own Dependencies section lists this document. |
| Level Objective & Move-Limit System (`design/gdd/level-objectives.md`, Draft) | Level Objective depends on Special Candies (zero direct coupling) | **Confirmed, now that Level Objective is authored**: Level Objective's `collect_color` tally also consumes `match_cleared`'s `cleared_pieces` **only**, for the identical double-counting reason (`level-objectives.md` § Detailed Rules 4) — it never subscribes to `special_activated` and never calls a seam on this document directly. Every cell this document's combos or passive chains clear is fully supported already by Board Engine's existing signal payloads (§ Detailed Rules 9's Harvest Observation Point framing applies identically here). **Reciprocal note fulfilled**: Level Objective's own Dependencies section lists this document, explicitly as a zero-direct-coupling row. |
| Juice Layer — VFX & Audio Hooks (`design/gdd/juice-layer.md`, not yet authored) | Will depend on Special Candies | Expected to key its "hero" activation animations and particle scale (`art-bible.md`'s VFX Standards: "specials always read as visually 'bigger events'... roughly 1.5–2× the particle count") off `special_activated`'s and `special_spawned`'s `special_type` field, and to render the Bomb+Striped jackpot's full multi-line clear from `special_activated.cleared_pieces` alone, deferred-replay-safe per `board-engine.md` § Detailed Rules 13. **Reciprocal note**: when authored, its Dependencies section must list this document. |
| Booster Brewing Meta (`design/gdd/booster-brewing.md`, Phase 2, not yet authored, gated on founder-approved friction prototype) | Will depend on Special Candies | Expected to read the Harvest Observation Point (§ Detailed Rules 9) for per-color ingredient tallies, including specials' and combos' clears. This document defines the observation point only; ingredient/recipe design remains entirely out of scope here. **Reciprocal note**: when authored, its Dependencies section must list this document. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | Special Candies depends on it (visual identity + one scope-cut confirmation) | Supplies the MVP special roster's visual language (§ Detailed Rules 1's table) and the explicit Vertical-Slice-scope tag on Wrapped Candy that this document's § Detailed Rules 3 formalizes into a mechanical deferral. |
| `prototypes/sweet-cascade-concept/REPORT.md` (prototype, not a GDD) | Special Candies depends on it (design rationale) | Source of the swapped-cell anchoring finding (Formula 2), the special-×-special "best moment" finding (§ Detailed Rules 6's general algorithm), and the passive-bomb "unearned" finding this document resolves (§ Detailed Rules 6). Cited throughout as design rationale, never as a binding technical contract. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `MIN_STRIPE_RUN_LENGTH` | `4` | Fixed at `4` — not a tuning knob, genre convention this document treats as a constant (mirrors `board-engine.md`'s own treatment of `MIN_RUN_LENGTH`) | N/A | N/A |
| `MIN_BOMB_RUN_LENGTH` | `5` | Fixed at `5` — same rationale as above | N/A | N/A |
| Passive Color Bomb detonation policy | `DETERMINISTIC_TARGET_COLOR` (§ Detailed Rules 6, Formula 8) | `{DETERMINISTIC_TARGET_COLOR, DEFUSE}` | N/A (not a magnitude — a binary policy choice) | Switching to `DEFUSE` removes passive detonation entirely (a caught bomb clears as a single ordinary cell); this was evaluated and explicitly rejected during this authoring pass (§ Detailed Rules 6, "Rejected alternative") but is preserved here as a reversible knob in case playtesting at Vertical Slice finds the deterministic rule still reads as excessive rather than learnable |
| Target-color tie-break rule (Formula 8) | Lowest `color_pool` index wins ties | Fixed convention, not a magnitude | N/A | N/A — could alternatively be "highest index" or "first color in `active_colors` order"; the specific tie-break is arbitrary but must stay fixed once chosen, since changing it would silently change every tied-detonation's target for existing recorded seeds |
| `MAX_CHAIN_EXPANSION_ITERATIONS` (owned by `board-engine.md`, cross-referenced here) | `10` (Board Engine default) | `5–30` (Board Engine's own safe range) | More headroom for a dense, many-specials passive chain (§ Edge Cases) to reach a genuine fixpoint before force-stabilizing | Less headroom risks truncating a legitimately long chain of caught specials on a special-dense endgame board — flagged as a parameter worth revisiting once real dense-endgame boards exist to measure against (this document does not own the value, only flags the interaction) |
| `special-drop` RNG stream consumption (`rng-service.md` stream_id 2) | `0` draws at MVP (§ Detailed Rules 8) | N/A — not a magnitude, a scope boundary | A future post-MVP mechanic (e.g., a bonus-chance-for-an-extra-special rule) could begin consuming this stream without any RNG Service change, since it is already reserved and seeded every session | N/A |

---

## Acceptance Criteria

All tests below live under `tests/unit/special-candies/`, follow
`coding-standards.md`'s naming/isolation/determinism rules, and — per
`technical-preferences.md`'s determinism rule — never use an unseeded RNG
draw, consistent with § Detailed Rules 8's finding that this document
consumes none.

**Seam Compatibility (BLOCKING — the no-op-default compatibility gate)**

- [ ] `test_board_engine_runs_unmodified_with_zero_specials_registered`: with
      no Special Candies resolver registered at all, `board-engine.md`'s own
      `test_seams_default_to_noop_with_no_consumer` suite passes unchanged —
      confirming this document's existence and design impose no requirement
      on Board Engine's isolated-mode behavior.
- [ ] `test_seam_signatures_match_board_engine_exactly`: interface inspection
      confirms this resolver's four implemented functions match
      `board-engine.md` § Detailed Rules 3's signatures exactly (parameter
      types, return types) — no seam is extended, narrowed, or reinterpreted.

**Creation Rules (Formulas 1–3)**

- [ ] `test_match3_run_produces_no_spawn`: a straight run of exactly 3
      produces zero entries in `resolve_special_spawns`'s returned map.
- [ ] `test_horizontal_match4_creates_stripe_h_same_axis`: a horizontal run
      of 4 produces `STRIPE_H` at its resolved anchor, with `color` equal to
      the run's own color — confirming the same-axis rule, not the
      perpendicular alternative.
- [ ] `test_vertical_match4_creates_stripe_v_same_axis`: mirror case for
      vertical runs, confirming `STRIPE_V`.
- [ ] `test_match5_creates_colorless_color_bomb`: a straight run of 5 (or
      more) produces `COLOR_BOMB` with `color = null` → Board Engine
      resolves it to `COLOR_NONE (-1)`, confirmed excluded from a
      subsequent Matching pass.
- [ ] `test_anchor_prefers_swap_cell_when_swap_anchor_available`: given
      `swap_anchor_cells` overlapping the winning run by exactly one cell,
      the spawn anchors at that cell (Formula 2, worked example 1's
      single-overlap variant).
- [ ] `test_anchor_tiebreak_both_swap_cells_interior_to_run`: reproduces
      Formula 2's worked example 1 exactly (both swap cells inside the run
      → nearer-to-middle wins).
- [ ] `test_anchor_falls_back_to_run_middle_when_no_swap_anchor`:
      reproduces Formula 2's worked example 2 exactly (`swap_anchor_cells
      = {}` at `chain_index ≥ 2` or `trigger_source = BOOTSTRAP`).
- [ ] `test_cluster_precedence_5_run_beats_4_run`: reproduces Formula 3's
      worked example 1 exactly — a length-5 and length-4 run intersecting
      in one cluster produce exactly one spawn, matching the length-5 run.
- [ ] `test_pure_tl_intersection_produces_zero_spawns`: reproduces Formula
      3's worked example 2 exactly — two length-3 runs intersecting produce
      zero spawns, all cells still clear via one `match_cleared`.
- [ ] `test_disjoint_clusters_each_spawn_independently`: a cascade step
      containing two unrelated, non-overlapping match-4s produces exactly
      two `special_spawned` events in the same Clearing pass, one per
      cluster.
- [ ] `test_no_cell_ever_claimed_by_two_spawns`: a property-style test over
      many synthetic multi-run cascade steps asserts the returned
      `Map[cell, SpecialSpawn]` never contains a cell reachable from two
      different clusters — confirming Formula 3's "at most one spawn per
      cluster" guarantee holds structurally, not just in the worked
      examples.

**Combo Matrix (Formulas 4–7) — one fixture-driven test per matrix cell**

- [ ] `test_bomb_plus_color_clears_bomb_cell_and_matching_color`: given a
      board fixture with a known count of `partner.color` cells, a Bomb+
      Color swap's clear set exactly equals that count plus the bomb cell
      (Formula 4).
- [ ] `test_bomb_plus_bomb_clears_every_occupied_cell`: a Bomb+Bomb swap's
      clear set exactly equals every `OCCUPIED` cell on a fixture board,
      regardless of how many distinct colors that fixture contains
      (Formula 5; includes a fixture with only 1 distinct color present, to
      directly cover the task's flagged `<2`-colors edge case).
- [ ] `test_striped_plus_striped_same_orientation_same_axis_swap_single_line`:
      reproduces Formula 6's "8 cells" case (e.g., horizontal swap, both
      `STRIPE_H`).
- [ ] `test_striped_plus_striped_same_orientation_cross_axis_swap_double_line`:
      reproduces Formula 6's "16 cells" case (e.g., horizontal swap, both
      `STRIPE_V`).
- [ ] `test_striped_plus_striped_mixed_orientation_cross`: reproduces
      Formula 6's "15 cells" case (one H, one V).
- [ ] `test_striped_plus_striped_ignores_color_mismatch`: two Striped
      pieces of *different* colors still combo per Formula 6 — the swap is
      valid and the clear set is computed identically to a same-color case.
- [ ] `test_bomb_plus_striped_clears_full_lines_for_every_matching_cell`:
      given a fixture with `N` cells of the Striped partner's color spread
      across `M` distinct rows (or columns, per orientation), the clear set
      exactly equals the union of those `M` full lines (Formula 7).
- [ ] `test_wrapped_combo_cells_structurally_unreachable`: interface/data
      inspection confirms no code path in this resolver's creation rules
      (Formulas 1–3) can ever produce a `WRAPPED`-typed piece at MVP,
      confirming the "N/A — structurally unreachable" combo-matrix rows are
      true by construction, not merely undocumented.

**Passive Chain Reaction (Formula 8, § Detailed Rules 6)**

- [ ] `test_passive_striped_chain_multi_step`: a synthetic board where one
      Striped candy's row-clear catches a second Striped candy, whose own
      column-clear catches a third, produces the full expected multi-step
      expansion within one Clearing pass, reproducing the prototype's
      validated "best moment" mechanic.
- [ ] `test_passive_bomb_targets_most_common_color`: reproduces Formula 8's
      worked example exactly — a bomb caught via seam 4 (not a direct
      swap-activation) clears every cell of the board's most-common color
      at that instant.
- [ ] `test_passive_bomb_tiebreak_lowest_color_index`: a fixture with two
      colors tied for most-common resolves to the lower `color_pool` index,
      deterministically and reproducibly across repeated runs.
- [ ] `test_passive_bomb_no_colored_pieces_no_expansion`: a fixture with
      zero colored `OCCUPIED` cells (all-specials board) results in the
      passively-caught bomb contributing zero expansion cells — no error,
      no crash.
- [ ] `test_two_passive_bombs_same_step_idempotent`: two bombs caught in the
      same growing clear set independently compute the identical
      `target_color` and produce no duplicated or conflicting expansion.
- [ ] `test_swap_activated_bomb_never_uses_passive_target_rule`: a bomb
      activated via a direct swap (seam 1/2) always uses its swap partner's
      color (Formula 4/7), never Formula 8's most-common-color rule — the
      two paths are confirmed mutually exclusive per move.
- [ ] `test_freshly_spawned_special_not_caught_same_step`: a special
      created by seam 3 in a given Clearing pass is confirmed absent from
      that same pass's seam-4 input set — it survives to the next step or
      move untouched.
- [ ] `test_seam4_single_call_never_recurses_internally`: interface/behavior
      inspection confirms one call to `expand_special_chain_reaction`
      performs exactly one pass and returns without internally looping —
      confirming Board Engine's calling contract is honored, not
      reimplemented.
- [ ] `test_striped_line_clear_excludes_void_cells_automatically`: a
      fixture with a `cell_mask` void inside a Striped candy's row/column
      confirms the clear set naturally excludes the void cell with no
      special-cased logic, per Formula 6/7's plain `OCCUPIED`-only
      definition.

**Determinism (BLOCKING, mirrors `board-engine.md`'s own master gate)**

- [ ] `test_identical_seed_identical_combo_outcomes`: two runs with an
      identical injected seed (`start_test_session`) and an identical
      ordered swap-intent sequence, including at least one of every combo
      matrix cell and one passive chain, produce byte-identical spawn
      locations, combo clear sets, and passive-detonation target colors.
- [ ] `test_zero_special_drop_stream_draws_at_mvp`: instrumenting
      `special-drop` (stream_id 2) across a full synthetic playthrough
      exercising every creation rule and every combo confirms exactly `0`
      draws are consumed, confirming § Detailed Rules 8's claim.

**Reshuffle Interaction**

- [ ] `test_special_survives_reshuffle_with_identity_intact`: a
      not-yet-activated Striped or Color Bomb piece present before a
      mid-game reshuffle retains its exact `(color, special_type)` pair at
      its new post-shuffle cell.

---

## Cross-References

| This Document References | Target | Specific Element | Nature |
|---|---|---|---|
| Rejection of the perpendicular (Candy-Crush-authentic) stripe-orientation convention | § Detailed Rules 2 | Same-axis rule justification | Explicit design decision, not an oversight — documented here so a future reviewer doesn't "fix" this document back toward genre convention without re-reading the readability rationale |
| Rejected v2 composite spawn+clear seam sketch | `design/gdd/board-engine.md` | Open Questions, "v2 seam-extension sketch — spawn+clear composite combos" | This document's Bomb+Striped combo (Formula 7) was deliberately redesigned to avoid needing that sketch — per the task's explicit instruction to "prefer redesigning the combo to fit current seams at MVP." The v2 sketch remains logged in `board-engine.md` for a hypothetical future combo (e.g., a Wrapped-involving composite at Vertical Slice) that genuinely cannot be expressed as a pure `Set[cell]` |
| Deliberate MVP scope cut — no solo Striped swap-activation | § Detailed Rules 5 | "Deliberate MVP scope cut" subsection | Flagged for a possible post-MVP revisit — see Open Questions |
| Level Data Format v2 extension ask (`special_type` on `pre_placed_pieces`) | `design/gdd/level-data-format.md` | § Detailed Rules 6, Out of Scope table's anticipated "Pre-placed special candies / locked pieces" row | This document supplies the concrete field shape that row anticipated; not written into that document here, per this document's own file-edit scope (Special Candies does not edit other GDDs directly) |
| Passive Color Bomb detonation policy decision | `prototypes/sweet-cascade-concept/REPORT.md` | Lessons Learned, "Passive color-bomb detonation during cascades is dramatic but can feel unearned" | Resolved in § Detailed Rules 6 with a deterministic rule (Formula 8), not by removing the mechanic — see that section's "Rejected alternative" subsection for the alternative considered and why it was not chosen |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| Should solo Striped swap-activation (swap a Striped candy with any adjacent regular candy to fire it, even without forming a match) be added post-MVP for genre parity with Candy Crush Saga / Royal Match? | game-designer | Revisit at Vertical Slice, once real playtest data shows whether players expect this from prior genre experience | — |
| Should the passive Color Bomb detonation policy (`DETERMINISTIC_TARGET_COLOR`, Formula 8) be validated against real Vertical Slice playtest feedback, in case the deterministic rule still reads as excessive rather than learnable once players experience it in-engine (not just in this document's reasoning)? | game-designer / creative-director | At Vertical Slice, once real in-engine feel data exists — the concept prototype's own HTML feel ceiling is explicitly not sufficient for this kind of judgment (`REPORT.md`, Recommendation) | — |
| Is Formula 9's creation-frequency heuristic worth replacing with an empirical per-run-length histogram from real Vertical Slice playtest sessions, mirroring `board-engine.md`'s own open item for its sibling Formula 6? | systems-designer | At Vertical Slice, once real play data exists | — |
| Should Wrapped Candy's eventual Vertical Slice design reuse this document's Cluster Precedence formula (Formula 3) directly — i.e., a pure T/L cluster with no arm reaching length 4 becomes Wrapped-eligible instead of spawning nothing — or does Wrapped need its own, different eligibility rule? | systems-designer (with game-designer) | At Wrapped Candy's own design pass, Vertical Slice | — |
| Once Wrapped Candy exists, do Bomb+Wrapped and Striped+Wrapped need the v2 composite spawn+clear seam sketch `board-engine.md` logged, or can they also be redesigned to fit the current seam 2 `Set[cell]` signature the way Bomb+Striped was in this document? | systems-designer | At Wrapped Candy's own design pass, Vertical Slice | — |
| Should a future post-MVP mechanic consume the reserved `special-drop` RNG stream (e.g., a declared, player-visible "guaranteed bonus special every N moves" counter, per `rng-service.md`'s Honest Randomness Contract's "Allowed" category)? | game-designer | Not before Vertical Slice; no current design need identified | — |
