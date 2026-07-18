# Match-3 Board Engine

*Status: Reviewed — APPROVED (re-review, 2026-07-18)*
*Created: 2026-07-18*
*Last Updated: 2026-07-18*
*Layer: Core · Priority: MVP · Phase: MVP · Category: Gameplay*
*Author: systems-designer*
*Depends On: RNG Service (`design/gdd/rng-service.md`, APPROVED), Touch & Input System (`design/gdd/touch-input.md`, APPROVED), Level Data Format (`design/gdd/level-data-format.md`, APPROVED)*
*Depended On By: Special Candies & Combo Matrix, Scoring & Star Thresholds, Level Objective & Move-Limit System, Juice Layer — VFX & Audio Hooks, Game UI/Screens Flow (all not yet authored — forward dependencies, per `design/gdd/systems-index.md`)*
*Source: `design/gdd/systems-index.md` · `prototypes/sweet-cascade-concept/REPORT.md` + `prototype.html` · `design/art/art-bible.md` · `.claude/docs/technical-preferences.md`*

**Revision 2 changelog (2026-07-18, resolves `reviews/board-engine-review-log.md`):**
- **Blocking 1 resolved** — every piece-reporting signal payload now carries full piece identity `(cell, color, special_type)`; added the `pieces_spawned` signal (per-piece cell + color + source `bootstrap|cascade_refill`); § Detailed Rules 13's deferred-replay guarantee extended with a worked 2-step-cascade walkthrough deriving per-color tallies from events alone.
- **Blocking 2 resolved** — seam 3 return type extended to `Map[cell, SpecialSpawn]` with `SpecialSpawn = {special_type, color: int | null}`; `null` → colorless special (`COLOR_NONE = -1`, color bomb); § Detailed Rules 1's colorless-support claim now holds; worked examples updated.
- **Advisories** — `Run` structure now carries `color` directly (§4); `swap_anchor_cells` defined as empty for cascade steps 2+ (run-middle anchoring applies); seam 4 calling semantics pinned (Board Engine iterates to fixpoint or `MAX_CHAIN_EXPANSION_ITERATIONS`; extensions never recurse internally); spawn+clear composite combos logged in Open Questions with a v2 step-plan sketch.

---

## Overview

The Match-3 Board Engine is the Core-layer simulation that owns everything
about the board's moment-to-moment truth: grid state, swap validation and
execution, match detection, clear/gravity/refill, and the cascade resolution
loop that chains them together until the board settles. It is the single
bottleneck system in Sweet Cascade's dependency graph — every Feature-layer
system (Special Candies & Combo Matrix, Scoring & Star Thresholds, Level
Objective & Move-Limit System, Juice Layer) extends or observes it, never the
reverse — so this document is deliberately conservative about what it claims
to own: special-candy creation rules and the special-×-special combo matrix
belong to `special-candies.md`; point values belong to `scoring-stars.md`;
objectives, blockers, and move-limit enforcement belong to
`level-objectives.md`. What Board Engine owns instead is the *mechanism*: a
fully deterministic, headless-testable state machine that turns a validated
`swap_request` into a resolved board state, exposing four synchronous
extension seams for Special Candies to plug into and a signal catalog every
downstream system observes, with zero artificial delay anywhere in its own
logic — presentation pacing is entirely the Juice Layer's job. This document
also formally resolves two handoffs left open by its Foundation-layer
dependencies: the `level_id` String→integer resolution RNG Service requires
(§ Detailed Rules 2), and the 44px touch-target proof across the full 3–9
grid range that both `touch-input.md`'s and `level-data-format.md`'s review
logs flagged as advisory and deferred here (§ Formulas, Formula 4).

---

## Player Fantasy

Everything the player *feels* about Sweet Cascade's core loop is downstream
of a promise this document exists to keep mechanically true: **the board
never lies, and it never hesitates.** This is the board engine's
contribution to both of the game's pillars at once.

**Pillar 1 (Every Swap Sparkles) — instant, honest resolution.** Board
Engine's own contract has zero artificial delay anywhere: the moment a
`swap_request` is accepted, the *entire* resolution — every cascade step,
every clear, every refill, all the way to a settled board — is computed
synchronously, within the same logic frame. Nothing the player perceives as
"pacing" (the 200ms slide, the 240ms pop, the escalating "Sweet! ×2 →
Delicious! ×4" callouts the concept prototype validated) is Board Engine
holding anything back — it is the Juice Layer choosing how fast to *replay*
a sequence of events Board Engine already knows in full. If a swap ever
*feels* slow, that is a presentation-layer bug, never a logic-layer one —
this document's Detailed Rules §13 draws that line explicitly so it is
testable, not just aspirational.

**Pillar 2 (Clever, Never Cheated) — cascades read as earned, not lucky.**
The concept prototype's riskiest-assumption test (`REPORT.md`) was whether
cascades feel like a reward for a clever swap rather than noise. Two of its
validated learnings are load-bearing rules in this document, not flavor:
special-candy spawns anchor at the *swapped* cell (§ Detailed Rules 3, seam
3) because a mid-run spawn "reads as random," and every cascade step
increments a visible `chain_index` (§ Detailed Rules 6) so the player can
trace *why* the board kept going instead of experiencing it as a black box.
An invalid swap — one that produces no match and triggers no special
activation — costs the player nothing beyond an instant, silent revert
(§ Detailed Rules 5): no move consumed, no score lost, no visible penalty.
The player should never suspect the board is rigged against them, because
structurally it cannot be: every draw this engine consumes comes from RNG
Service's Honest Randomness Contract (`rng-service.md` §4), and every rule
in this document — match detection, gravity, refill, reshuffle — is a fixed,
inspectable procedure with no runtime knob that reads player performance.

**A board that never gets stuck.** A player who runs out of legal moves
should never feel abandoned by the game mid-level. Board Engine detects this
condition every time the board would otherwise return to Idle and
transparently reshuffles (§ Detailed Rules 11) before handing control back —
the player experiences a brief "reshuffling…" beat, never a dead board.

---

## Detailed Rules

### 1. Board Model

**Coordinate system.** Row-major `(row, col)`: row `0` is the top, increasing
downward; column `0` is the left, increasing rightward. This is identical to
Touch & Input's convention (`touch-input.md` §1), so every `select_cell` and
`swap_request` coordinate maps directly onto this grid with zero
translation. Level Data Format's `grid_height` maps to this document's
`rows`; `grid_width` maps to `cols` (same mapping `touch-input.md`'s review
log already established for its own consumption of these fields).

**Cell state** — a three-value enum per grid position:

| State | Meaning |
|---|---|
| `VOID` | Not part of the level's playable shape (`cell_mask` character `'0'`). Never occupied, never targeted by gravity/refill/swap. |
| `EMPTY` | Playable but momentarily unoccupied. This state only exists transiently, between the Clearing and Refilling resolution states (§ Detailed Rules 6) — it never persists into an `Idle` frame. Any `EMPTY` cell still present when the resolution loop reaches `Idle` is a contract violation (see Acceptance Criteria). |
| `OCCUPIED` | Playable and holding exactly one `Piece`. |

**Piece.** Every occupied cell holds exactly one Piece, defined as:

| Field | Type | Range | Description |
|---|---|---|---|
| `piece_id` | int | monotonically increasing, unique for the piece's lifetime | Assigned at spawn (bootstrap or refill), retired at clear. Exists so the Juice Layer can animate one persistent visual object through gravity moves rather than treating a cell's occupant as replaced every frame — mirrors the concept prototype's persistent-DOM-element tile model (`prototype.html`'s `makeTile`/`place` pattern). |
| `color` | int | `[-1, pool_size-1]` | Index into the level's `color_pool` (Level Data Format), OR the reserved sentinel `COLOR_NONE = -1`, meaning "colorless." A piece with `color = -1` is permanently excluded from every color-based run comparison in match detection (§ Detailed Rules 4). Board Engine defines and enforces this sentinel itself — it does not need to know *why* a piece is colorless to exclude it correctly, which keeps colorless-special support (e.g., a color-bomb type) entirely inside Special Candies' scope: seam 3's `color` override on its `SpecialSpawn` return value (§ Detailed Rules 3) is the one mechanism needed to assign `COLOR_NONE` to a spawned piece, with no further Board Engine schema change required. |
| `special_type` | int | `{0} ∪ opaque non-zero values` | `SPECIAL_NONE = 0` is the only value Board Engine itself assigns or interprets. Any non-zero value is opaque data Board Engine stores and forwards in signal payloads but never reads the meaning of — its full vocabulary (`STRIPE_H`, `STRIPE_V`, `BOMB`, etc.) is owned entirely by `special-candies.md` (not yet authored). Level Data Format v1 has no pre-placed-special field, so every piece placed at bootstrap has `special_type = SPECIAL_NONE`; non-zero values only ever arise via the special-spawn seam (§ Detailed Rules 3, seam 4) during play. |
| `row`, `col` | int | within grid bounds | The piece's current position. Always kept in sync with the grid's own `OCCUPIED` cell record — Board Engine never allows a piece's stored coordinates and the grid's record of where it sits to diverge, even transiently within one logic tick. |

### 2. Level Bootstrap & the Level Manifest

**Manifest ownership (resolves `level-data-format.md`'s and `rng-service.md`'s
open handoff).** RNG Service's `start_level_session(level_id: int,
attempt_number: int)` requires an integer `level_id`, but Level Data Format's
`level_id` field is a String (`<region_code>-<3-digit-sequence>`, e.g.
`candy_kingdom_hub-001`) that must never be resolved via runtime string
hashing (`rng-service.md` §3, Edge Cases — hash stability is not guaranteed
across Godot's export targets). **Match-3 Board Engine owns the Level
Manifest** — it is the only system that directly calls
`start_level_session()`, so it is the natural, single owner of the
resolution table.

- **File**: `assets/data/level_manifest.tres`, a custom `LevelManifest`
  Resource holding one field: `entries: Array[String]`, an ordered,
  **append-only** list of every `level_id` ever authored, in the order it
  was first added.
- **Formula**: `manifest_index = 1 + entries.find(level_id)` — the level's
  1-based position in the array (see Formula 1). Index `0` is never a valid
  `manifest_index`; it is reserved as an "unresolved" sentinel for defensive
  logging.
- **Append-only invariant**: exactly mirroring RNG Service's own Stream
  Registry philosophy (`rng-service.md` §2) — a `manifest_index` is assigned
  once, at first authoring, and is **never reused or renumbered**, even if a
  level is later removed from rotation. Renumbering would silently change
  every affected level's entire RNG draw sequence retroactively.
- **Update process**: a new entry is appended to `entries` at the same point
  a new `.tres` level file passes Level Data Format's validation suite
  (`level-data-format.md` §5, step 4) and is merged — this is a recommended
  addition to that document's Authoring Workflow, flagged here rather than
  edited there directly (see Cross-References).
- **Validation**: a companion gdUnit4 test under `tests/unit/board-engine/`
  asserts every `level_id` under `assets/data/levels/` appears in
  `entries` exactly once, and `entries` contains no duplicate strings.

**Bootstrap procedure** — executed once per level entry, fully ordered for
determinism (this ordering is also what `rng-service.md`'s Edge Cases table
requires this document to specify, since multiple bootstrap steps draw from
the same `board-refill` stream):

1. Resolve `level_id` (String) → `manifest_index` (int) via the Level
   Manifest (Formula 1).
2. Call `RNG_Service.start_level_session(level_id=manifest_index,
   attempt_number)`. `attempt_number` is a caller-supplied parameter Board
   Engine does not own or persist (matching RNG Service's own explicit
   non-ownership stance, `rng-service.md` §3) — for MVP, before Game
   UI/Screens Flow exists, the caller is the Level Preview harness
   (`level-data-format.md` §5), defaulting to `attempt_number = 1` and
   incrementing on each explicit in-harness retry.
3. Read `grid_width`, `grid_height` from Level Data Format; allocate the
   grid.
4. Read `cell_mask` (defaults to a full rectangle if absent, per
   `level-data-format.md` §2); mark every cell `VOID` or (initially)
   `EMPTY` accordingly.
5. Compute column segments from the mask (Formula 3) — used by every
   gravity/refill operation for the rest of the session.
6. Place every `pre_placed_pieces` entry: `color` = the entry's
   `candy_type` mapped to its index in `color_pool`; `special_type =
   SPECIAL_NONE` (Level Data Format v1 has no pre-placed-special support).
   These cells become `OCCUPIED` and are excluded from step 7.
7. Fill every remaining `EMPTY`, playable cell in row-major order (row 0 →
   `rows-1`, left to right within each row) via the **retry-until-no-match**
   algorithm: draw `RNG_Service.next_color("board-refill", color_pool)`;
   if placing that color at this cell would create an immediate run of ≥3
   with already-placed neighbors (checking only up/left, since later cells
   aren't placed yet — identical to the concept prototype's
   `createsMatchAt`), redraw. Redraw up to `BOOTSTRAP_MAX_RETRIES_PER_CELL`
   times (default 100, Tuning Knobs); if every retry still produces a
   match, accept the last-drawn color unconditionally (this fallback is
   deterministic — same seed always produces the same retry count and the
   same fallback outcome — and is expected to be reached, if ever, only on
   a pathological `color_pool`/`cell_mask` combination Level Data Format's
   V7/V11 rules make vanishingly unlikely). Immediately upon completing
   this fill — the full board now populated by steps 6 (pre-placed) and 7
   (RNG-filled) combined — emit one `pieces_spawned(pieces, source =
   BOOTSTRAP)` (§ Detailed Rules 7) whose `pieces` array contains a
   `PieceSnapshot` for every playable cell on the board, in the same
   row-major order used to fill them. This is what lets the Juice Layer
   render the level's opening "candies drop into place" beat, and any other
   consumer recover every initial cell's `color`/`special_type` (step 8's
   automatic bootstrap cascade, immediately below, can mutate the board
   again before the very next signal, so this snapshot — not a later query
   — is each initial piece's only recoverable record).
8. Run one Matching pass (§ Detailed Rules 4) against the fully-populated
   board. Because pre-placed pieces (step 6) are never filtered for
   accidental matches — the retry-until-no-match guarantee in step 7 only
   protects RNG-filled cells — a level whose `pre_placed_pieces` happen to
   coincidentally form a run is a legal, if unusual, authoring outcome. If
   this pass finds any runs, resolve them through the standard cascade
   pipeline (§ Detailed Rules 6) with `trigger_source = BOOTSTRAP`, exactly
   like any other cascade — no special-cased logic is needed, this reuses
   the same Clearing→Falling→Refilling→Matching loop. This consumes zero
   player moves and is never attributed to a `chain_index` associated with
   a swap.
9. If, after all clears from step 8 settle, `has_available_move()` (§
   Detailed Rules 11) is false, trigger Reshuffling (§ Detailed Rules 11)
   with `trigger_source = BOOTSTRAP` before proceeding.
10. Emit `board_bootstrapped(rows, cols, cell_mask, manifest_index)`, set
    `board_input_enabled = true`, and transition to `Idle`.

Board Engine reads exactly five Level Data Format fields at bootstrap:
`grid_width`, `grid_height`, `cell_mask`, `pre_placed_pieces`,
`color_pool`. It does **not** read `move_limit`, `objectives`, or the star
threshold fields — those are Level Objective & Move-Limit System's scope
per `systems-index.md`'s system boundaries. `level-data-format.md`'s
Dependencies table has been reconciled to this position: it now explicitly
states Board Engine does **not** read `move_limit` (see Cross-References).

### 3. Extension Seams (Special Candies Handoff)

Board Engine defines four synchronous extension points. Each has a
documented **MVP default (no-op) behavior**, so Board Engine is fully
functional and headless-testable in complete isolation, with zero
dependency on `special-candies.md` existing. This is what keeps the
dependency arrow one-directional (Special Candies depends on Board Engine,
never the reverse, per `systems-index.md`).

| Seam | Signature | Called | MVP Default (no consumer registered) | Purpose |
|---|---|---|---|---|
| 1. Activation check | `is_special_activation_swap(piece_a, piece_b) -> bool` | During swap validity determination (Formula 2), for every `swap_request` | Always returns `false` | Lets a swap be valid *without* producing a color match — e.g., swapping a color-bomb with a regular candy. |
| 2. Activation clears | `resolve_special_activation_clears(piece_a, piece_b) -> Set[cell]` | Only when seam 1 returned `true` for this swap | Never called (seam 1 already gates it) | Returns the cell set the activation clears, bypassing normal run detection for this step's trigger. |
| 3. Special spawns | `resolve_special_spawns(runs, swap_anchor_cells) -> Map[cell, SpecialSpawn]`, where `SpecialSpawn = {special_type: int, color: int \| null}` | Every cascade step, immediately after Matching, before Clearing finalizes the clear set | Returns an empty map | Exempts specific cells from clearing and transforms them into specials instead. `color` lets the spawned piece override its color: an explicit `color_pool` index produces a **colored** special (e.g., striped — the resolver typically echoes the triggering run's own `color`, now directly available on `Run`, § Detailed Rules 4); the literal value `null` produces a **colorless** special (e.g., a color bomb — Board Engine assigns `COLOR_NONE = -1`). `swap_anchor_cells` — the cells involved in the triggering swap — are passed through so spawn anchoring can prefer them, per the concept prototype's validated finding that spawning at the swapped cell (not mid-run) reads as deliberate rather than random. See "`swap_anchor_cells` for cascade steps 2+" below for its value on non-triggering steps. |
| 4. Chain expansion | `expand_special_chain_reaction(cleared_set) -> Set[cell]` | Every cascade step, after seam 3 — called **repeatedly by Board Engine** against the growing clear set until a call returns its input unchanged (fixpoint) or `MAX_CHAIN_EXPANSION_ITERATIONS` (Tuning Knobs) is reached, whichever comes first | Returns the input set unchanged (no expansion) on its first (and, since the input is already stable, only) call | Lets specials caught within any clear set (not only the triggering swap's) recursively trigger further clears — e.g., a stripe clears its row, a bomb caught mid-cascade clears a color. This is the mechanism behind the "special-×-special chains were the best moments" finding in `REPORT.md`. See "Seam 4 calling semantics" below. |

**`swap_anchor_cells` for cascade steps 2+ (resolves an advisory finding
from the 2026-07-18 design review).** `swap_anchor_cells` is populated only
for the step that directly follows a triggering `swap_request`:

- `chain_index = 1` **and** `trigger_source ∈ {SWAP_MATCH,
  SPECIAL_ACTIVATION}`: `swap_anchor_cells = {cell_a, cell_b}`, the two
  cells named in the triggering swap — regardless of whether validity came
  from a normal run match or a seam-1 activation.
- `chain_index = 1` **and** `trigger_source = BOOTSTRAP`: `swap_anchor_cells
  = {}` (empty set) — bootstrap's automatic cascade pass (§ Detailed Rules
  2, step 8) has no originating swap to anchor to.
- `chain_index ≥ 2`, **any** `trigger_source`: `swap_anchor_cells = {}`
  (empty set), always. By the second cascade step, the triggering swap's
  cells have already cleared or moved at least once (gravity has run), so
  they carry no meaningful anchor relationship to the *new* run(s) a later
  step detects — Board Engine never fabricates or carries forward a stale
  anchor. A seam-3 resolver that wants deterministic anchoring for chain
  steps 2+ must define its own fallback rule entirely within its own logic
  (e.g., a run's first cell in scan order, or its center cell) — Board
  Engine takes no position on what that fallback should be, consistent with
  its policy of owning mechanism, not special-candy behavior.

**Seam 4 calling semantics (resolves an advisory finding from the
2026-07-18 design review).** Board Engine, not the seam-4 resolver, owns
the recursive fixpoint loop. Each individual call to
`expand_special_chain_reaction(cleared_set)` is expected to perform exactly
**one single-pass expansion** (e.g., "these newly-caught bombs in the
current set detonate their radius") and return the resulting superset — it
is never required to recurse internally. Board Engine re-invokes the seam
against its own growing result: call 1 receives the raw clear set (after
seam 3); if the returned set is a strict superset of the input, Board
Engine calls again with that superset as the new input; this repeats until
either a call returns its input unchanged (a fixpoint — no further
expansion available) or the call count for this cascade step reaches
`MAX_CHAIN_EXPANSION_ITERATIONS` (Tuning Knobs, default `10`), whichever
comes first. **Termination guarantee.** This gives Board Engine two
independent, complementary iteration caps that together guarantee the
resolution loop always halts, even against an adversarial or buggy seam-4
resolver: `MAX_CHAIN_EXPANSION_ITERATIONS` bounds *within-step* seam-4
calls (this section), and `MAX_CASCADE_DEPTH` (Formula 6) bounds
*across-step* cascade iterations. Both are pure iteration counters Board
Engine enforces unconditionally, independent of whatever
`special-candies.md` implements — see Formula 6 and Edge Cases for the
shared force-stabilize failure mode both caps use when reached.

**Worked example (seam 3's color override).** A match-4 run of `color = 1`
("citrus") at cells `(5,2)-(5,5)` fires seam 3 with `swap_anchor_cells =
{(5,3),(5,4)}` (the swapped pair). A registered resolver wants `(5,3)` (the
anchor-preferred cell) to become a **striped** candy of the same color, and
returns `{(5,3): {special_type: STRIPE_H, color: 1}}` — Board Engine
exempts `(5,3)` from the clear set and spawns a piece there with `color =
1, special_type = STRIPE_H`, emitting `special_spawned(cell=(5,3),
special_type=STRIPE_H, color=1, source_run=...)` (§ Detailed Rules 7). For
a match-5 elsewhere, the same resolver instead wants a **color bomb** and
returns `{(anchor_cell): {special_type: BOMB, color: null}}` — Board Engine
spawns a piece there with `color = COLOR_NONE (-1), special_type = BOMB`,
permanently excluded from future color-based run comparisons (§ Detailed
Rules 4), delivering genuinely colorless behavior with no Board Engine
schema change beyond this already-specified contract.

**Defensive validation of seam responses.** Because "Board Engine owns ALL
validity judgment" is a hard project rule (not just a Board Engine
preference), every seam response is validated before use: any cell returned
by seam 3 or seam 4 that is not part of the current step's actual board
state (i.e., not a currently `OCCUPIED`, in-bounds cell) is silently
dropped and logged as a warning — a misbehaving or future-buggy Special
Candies implementation can never corrupt Board Engine's own state integrity
(see Edge Cases). The same policy applies to a `SpecialSpawn.color` value
that is present but is neither `null` nor a valid index into the level's
`color_pool`: it is treated as malformed, Board Engine falls back to the
exempted cell's originating run's `color` (the same colored-special
behavior as if the resolver had correctly echoed it), and a warning is
logged (see Edge Cases).

**Resolution order within one cascade step**: Matching → seam 3 (special
spawns) → seam 4 (chain expansion, applied iteratively to the union of
matched-run cells minus spawn-exempted cells, per the calling semantics
above) → Clearing. Seam 1 and seam 2 apply only at the swap-trigger
boundary, before the first Matching pass of a move.

### 4. Match Detection (MVP Policy: Runs-Only)

**Rule**: Board Engine detects only straight-line runs — horizontal or
vertical, length ≥ `MIN_RUN_LENGTH` (fixed at `3`, not a tuning knob; genre
convention this document does not treat as configurable). This matches the
concept prototype's `findRuns()` exactly and `REPORT.md`'s explicit MVP
scope note ("no T/L-shape specials — runs of 4/5 only").

**Algorithm**: for each row, scan left to right; for each column, scan top
to bottom; a "run" is a maximal sequence of cells whose `color` values are
equal and not `COLOR_NONE`. Any run with length ≥ 3 is recorded as
`{orientation, length, cells, color}` — `color` is the run's shared value
(every cell in a run has the same color, by definition of what makes a run
a run), included directly on the `Run` structure so a downstream consumer
(seam 3, Special Candies, Scoring, Level Objective) never has to look up an
individual cell to know which color a run represents. This addition also
gives seam 3 a ready-made source color for a colored special spawn (§
Detailed Rules 3's worked example).

**Overlap/intersection union rule.** When a horizontal run and a vertical
run share a cell in the same cascade step (an L or T intersection), Board
Engine unions their cell sets into exactly **one** combined clear set for
that step — the shared cell is never double-counted or cleared twice, and
exactly one `match_cleared` signal fires for that step regardless of how
many individual runs contributed to it.

**MVP decision: T/L-shape *recognition* is not a Board Engine concern, now
or as a future versioned extension.** Board Engine's `runs` payload
(orientation, length, and cell list per run, passed whole into seam 3)
already contains every piece of information needed to detect that two runs
intersect in an L or T pattern — a downstream consumer can compute that
purely from the raw run list Board Engine already emits. Therefore
`special-candies.md`, when authored, can implement L/T-shape → Wrapped
Candy classification entirely inside its own seam-3 resolver logic, with
**zero changes to this document or a `schema_version`-style bump** required
later. This is a stronger position than "defer to a v2 extension" — it is
"the data is already sufficient, so there is nothing to extend." Justified
by: (a) it exactly matches the concept prototype's validated MVP scope; (b)
it keeps Board Engine's match-detection algorithm simple, fast, and
provably correct in isolation; (c) it avoids Board Engine needing to know
what an "L shape" *means* gameplay-wise (a Special Candies concern), only
what a straight run *is* (a Board Engine concern).

### 5. Swap Rules

**Structural precondition (defensive, always checked, regardless of what
Touch & Input guarantees).** A `swap_request(cell_a, cell_b)` is rejected
immediately, with zero board mutation, if either cell is out of grid
bounds, `VOID`, or not currently `OCCUPIED`, or if the two cells are not
Manhattan-adjacent (distance ≠ 1). Touch & Input's own contract already
guarantees adjacency (`touch-input.md` §3: "`swap_request` is emitted
**only** for two cells with Manhattan distance exactly 1"), but per the
"Board Engine owns ALL validity judgment" rule, this is re-checked here,
never trusted from the caller.

**Validity determination** (Formula 2): a structurally valid swap is
*executed* (the two pieces' positions swap in the grid) and then judged
valid or invalid by whether it produces at least one run (§ Detailed Rules
4) OR seam 1 (`is_special_activation_swap`) returns `true`. This ordering —
execute first, judge second — deliberately mirrors the concept prototype's
`modelSwap()`-then-check pattern, because it is what lets the Juice Layer
animate an optimistic slide (§ Detailed Rules 7, `swap_started`) before the
outcome is known, without Board Engine itself needing to model a
"pretend" swap separately from a "real" one.

**Invalid swap (revert).** If the swap is invalid, the two pieces are
swapped back to their original positions — a full revert, zero net grid
change, zero move consumed, zero score effect. `swap_rejected` fires (§
Detailed Rules 7) and the resolution loop returns directly to `Idle`. This
is the mechanical guarantee behind the Player Fantasy claim "invalid swaps
cost nothing."

**Valid swap.** If the swap is valid, one move is considered consumed (via
the `swap_accepted` signal — Board Engine does not track or enforce a move
*limit*, only reports that a move was spent; see § Detailed Rules 2's
scope note) and the resolution loop proceeds into Matching.

**Seam-1-and-match coexistence.** If a swap both triggers `runs.length > 0`
*and* seam 1 returns `true` (e.g., swapping two specials that also happen
to align 3 colors), the step's clear set is the union of both: seam 2's
activation clears ∪ the normally-detected run cells. Nothing is lost to
either path.

### 6. Resolution Loop (State Machine)

| State | Entry Condition | What Happens | `board_input_enabled` |
|---|---|---|---|
| `Bootstrapping` | Level load begins | § Detailed Rules 2's ten-step procedure | `false` |
| `Idle` | Bootstrap complete, or a prior resolution loop fully stabilized with a legal move available | Awaiting a `swap_request` | `true` |
| `Swapping` | A structurally valid `swap_request` is received | Execute the model swap; determine validity (Formula 2); if invalid, revert and return to `Idle` | `false` |
| `Matching` | A valid swap just executed, or a cascade step's Refilling just completed | Run match detection (§ Detailed Rules 4); if zero runs and this wasn't the swap-trigger step, cascade is over — proceed to `Idle`-pending (reshuffle check, § Detailed Rules 11) | `false` |
| `Clearing` | Matching found ≥1 run, or seam 1 triggered an activation | Apply seam 3 (spawns) and seam 4 (chain expansion); pop the finalized clear set; increment `chain_index` | `false` |
| `Falling` | Clearing complete | Gravity compaction, per column segment (§ Detailed Rules 8) | `false` |
| `Refilling` | Falling complete | Draw new pieces into every still-`EMPTY` cell, per column segment (§ Detailed Rules 9); on completion, emit one `pieces_spawned(pieces, source = CASCADE_REFILL)` (§ Detailed Rules 7) covering every cell filled during this pass | `false` |
| *(loop)* | Refilling complete | Return to `Matching` — this is the cascade loop | `false` |
| `Reshuffling` | The board would return to `Idle` but `has_available_move()` is `false` | § Detailed Rules 11's reshuffle algorithm | `false` |

**Cascade counter (`chain_index`) semantics.** `chain_index` starts at `1`
for the first clear of a move (the swap-triggered clear, or the activation
clear) and increments by exactly `1` for every subsequent pass through
Clearing within the *same* move's resolution loop. It resets to `1` at the
start of the next `Swapping`→`Clearing` sequence. This is the mechanical
backbone of the escalating "Sweet! ×2 → Delicious! ×4" callouts the concept
prototype validated — Board Engine emits the number; the Juice Layer (and
eventually Scoring & Star Thresholds) decides what to display or how many
points it's worth.

**`trigger_source` enum.** Every cascade sequence is attributed to exactly
one of: `SWAP_MATCH` (a normal player swap produced a run), `SPECIAL_ACTIVATION`
(seam 1 triggered), or `BOOTSTRAP` (§ Detailed Rules 2, step 8). This value
is carried on `match_cleared` and `cascade_ended` payloads (§ Detailed
Rules 7) so downstream systems (Scoring, in particular) can distinguish
"this cascade cost the player a move" from "this cascade was free
bootstrap cleanup."

**RNG draw ordering contract** (satisfies `rng-service.md`'s Edge Case:
"call order alone determines the sequence, and that order must be
documented in `board-engine.md`'s implementation"). All `board-refill`
stream draws happen in exactly this order, and only in these four
situations:

1. **Bootstrap fill** (§ Detailed Rules 2, step 7): one `next_color()` call
   per unfilled playable cell, visited in row-major order, with the
   retry-until-no-match sub-loop consuming additional draws per cell as
   needed.
2. **Bootstrap reshuffle**, if triggered (§ Detailed Rules 2, step 9): `shuffle()`
   calls per § Detailed Rules 11.
3. **Per-cascade-step refill** (Refilling state): for each column, left to
   right (`col = 0` to `cols-1`); within each column, for each of its
   segments (Formula 3), top to bottom; within a segment, for each
   `EMPTY` cell after gravity compaction, top to bottom — one
   `next_color()` call per cell, **with no retry filtering** (§ Detailed
   Rules 9 explains why this differs from bootstrap).
4. **Mid-game reshuffle**, if triggered (Reshuffling state): `shuffle()`
   calls per § Detailed Rules 11.

Within a single move's resolution, situation 3 repeats once per cascade
step, strictly in the order the loop visits `Refilling`. No other code
path in Board Engine consumes `board-refill`.

### 7. Signal Catalog

All signals are fire-and-forget (Godot signal convention, snake_case past
tense per `.claude/docs/technical-preferences.md`). Payload field names are
snake_case. None of these signals are ever emitted with a delay — they fire
synchronously, in the order listed below, as the resolution loop passes
through each state (§ Detailed Rules 6).

**Shared payload type: `PieceSnapshot` (resolves a BLOCKING finding from
the 2026-07-18 design review — see Cross-References).** Every signal that
reports a piece being cleared, spawned, or placed carries that piece's full
identity inline, as a `PieceSnapshot`:

```
PieceSnapshot = { cell: (int, int), color: int, special_type: int }
```

`color` and `special_type` use the exact same ranges as `Piece`'s own
fields (§ Detailed Rules 1): `color ∈ [-1, pool_size-1]` (`-1 = COLOR_NONE`)
and `special_type ∈ {0} ∪ opaque non-zero values`. A `PieceSnapshot` is a
**value snapshot at the instant the event fired**, not a live reference —
it never changes after the signal is emitted, which is what makes it safe
for a consumer to store and read back later during deferred replay (see §
Detailed Rules 13's payload-sufficiency guarantee). `Run` (§ Detailed Rules
4) already carries its own `color` field for the same reason.

| Signal | Payload | Fires When |
|---|---|---|
| `board_bootstrapped` | `rows: int, cols: int, cell_mask: Array[String], manifest_index: int` | End of `Bootstrapping`, before first `Idle` |
| `board_input_enabled_changed` | `enabled: bool` | Every transition into or out of `Idle` — this is the exact boolean Touch & Input's `board_input_enabled` contract reads (`touch-input.md` §4) |
| `pieces_spawned` | `pieces: Array[PieceSnapshot], source: enum{BOOTSTRAP, CASCADE_REFILL}` | **(New — closes the missing refill-placement gap.)** Once after Bootstrapping's combined pre-placed + RNG fill (`source = BOOTSTRAP`, § Detailed Rules 2, step 7); once per completed `Refilling` state, for every cascade step of every move or bootstrap-triggered cascade (`source = CASCADE_REFILL`, § Detailed Rules 9). Never fires with an empty `pieces` array (a `Refilling` state with nothing to fill would mean the prior `Falling` left no `EMPTY` cells, which is only possible if the step cleared zero cells — Clearing never runs with an empty clear set). |
| `swap_started` | `piece_a: PieceSnapshot, piece_b: PieceSnapshot` | Instant the model swap executes, before validity is known (§ Detailed Rules 5) — each `PieceSnapshot` captures that piece's `color`/`special_type` immediately **before** the swap; `piece_a.cell`/`piece_b.cell` carry what were previously bare `cell_a`/`cell_b` |
| `swap_rejected` | `cell_a, cell_b, reason: enum{NOT_ADJACENT, NO_MATCH_NO_ACTIVATION}` | The swap reverts — no piece was cleared, spawned, or placed, so bare cells (not `PieceSnapshot`) remain sufficient here | 
| `swap_accepted` | `cell_a, cell_b, trigger_source: enum{SWAP_MATCH, SPECIAL_ACTIVATION}` | The swap is valid and a move is spent — piece identity for this move is already fully carried by the preceding `swap_started` |
| `special_activated` | `piece_a: PieceSnapshot, piece_b: PieceSnapshot, cleared_pieces: Array[PieceSnapshot]` | Seam 1 returned `true` for this swap; fires immediately **before** the first `match_cleared` of that move. `piece_a`/`piece_b` mirror `swap_started`'s pre-swap snapshot; `cleared_pieces` replaces the former bare `cleared_cells` with full pre-clear identity for every cell seam 2 cleared |
| `match_cleared` | `chain_index: int, cleared_pieces: Array[PieceSnapshot], run_data: Array[Run], trigger_source: enum` | Every completed Clearing state. `cleared_pieces` replaces the former bare `cleared_cells` — each entry is that cell's piece identity **immediately before** it cleared |
| `special_spawned` | `cell: (int,int), special_type: int, color: int, source_run: Run` | Once per cell exempted and transformed by seam 3, during the same Clearing pass as the `match_cleared` it belongs to. `color` is the spawned piece's resolved color (§ Detailed Rules 3's `SpecialSpawn.color` — either the explicit override or, for a malformed/omitted response, the source run's own color) |
| `cascade_step_advanced` | `chain_index: int` | Every time Matching (after a Refilling) finds ≥1 new run and the loop continues |
| `cascade_ended` | `final_chain_index: int, total_cells_cleared: int, trigger_source: enum` | Matching finds zero runs and the move's cascade sequence is complete. `total_cells_cleared` remains an aggregate count, not a piece list — every individual piece already appeared in this move's `match_cleared` events |
| `no_valid_moves_detected` | *(none)* | A stabilized board has zero legal moves, before Reshuffling begins |
| `board_reshuffled` | `attempts_used: int` | Reshuffling succeeds. Reshuffle reassigns existing pieces' `(color, special_type)` across cells rather than clearing/spawning/placing new ones (§ Detailed Rules 11) — it is deliberately out of this revision's scope; see Open Questions |
| `board_stabilized` | *(none)* | The full loop for one triggering event (swap, activation, or bootstrap) returns to `Idle` |

**Ordering guarantee for a special-activation move**: `swap_started` →
`swap_accepted` → `special_activated` → `match_cleared(chain_index=1,
trigger_source=SPECIAL_ACTIVATION)` → `pieces_spawned(source=CASCADE_REFILL)`
→ … (normal cascade loop continues from `chain_index=2`) → `cascade_ended`
→ `board_stabilized`.

**Ordering guarantee for bootstrap**: `pieces_spawned(source=BOOTSTRAP)` →
*(if step 8 finds an accidental match)* `match_cleared(chain_index=1,
trigger_source=BOOTSTRAP)` → `pieces_spawned(source=CASCADE_REFILL)` → …
(cascade loop, `trigger_source=BOOTSTRAP` throughout) → `cascade_ended` →
*(if step 9 triggers a reshuffle)* `no_valid_moves_detected` →
`board_reshuffled` → `board_bootstrapped` → `board_input_enabled_changed(true)`.

### 8. Board State Query API (Synchronous)

Board-state **queries** are plain synchronous function calls, never
signals — this is the "board-state queries" ownership `systems-index.md`
assigns to Board Engine.

| Function | Returns | Description |
|---|---|---|
| `get_grid_dimensions()` | `{rows: int, cols: int}` | — |
| `get_cell_state(row, col)` | `enum{VOID, EMPTY, OCCUPIED}` | — |
| `get_piece_at(row, col)` | `Piece \| null` | `null` for `VOID`/`EMPTY` cells |
| `is_playable_cell(row, col)` | `bool` | `true` for `EMPTY` or `OCCUPIED` cells, `false` for `VOID` |
| `get_column_segments(col)` | `Array[Segment]` | Formula 3's precomputed result for one column |
| `is_board_input_enabled()` | `bool` | Mirrors the last `board_input_enabled_changed` payload |
| `get_current_chain_index()` | `int` | `0` when `Idle`; the live `chain_index` during any active resolution |

### 9. Gravity & Column Segmentation

**Column segments** (Formula 3) generalize gravity/refill to boards whose
`cell_mask` carves voids into the middle of a column, not only at its
top/bottom edges (Level Data Format's Player Fantasy explicitly cites
non-rectangular shapes — hearts, bottles, diamonds — as a deliberate
authoring goal). A **segment** is a maximal contiguous run of playable
cells within one column, bounded above and below by either a `VOID` cell or
a board edge. Gravity and refill both operate **independently within each
segment**; a piece never crosses from one segment into another, even within
the same physical column.

**MVP decision: pieces stop at a void; they never fall through it.** When
gravity compacts a segment, pieces settle toward that segment's own bottom
row (which may or may not coincide with the board's global bottom row).
**Justification**: (a) it is the simplest, most predictable mental model —
a `VOID` cell reads as "there is no floor here," the same way a masked-out
board shape is a physical cutout, not a portal, matching how players read
Candy Crush-genre blocked cells; (b) it avoids ambiguity in refill logic
about how far past multiple stacked voids a piece should "reach" to find a
landing spot; (c) it requires no new mechanic to explain to the player — a
void is simply absent floor. **Fall-through voids is explicitly flagged as
a future tuning/design consideration** (Tuning Knobs, `GRAVITY_MODE`), not
implemented at MVP, reserved for a possible future "portal tile" mechanic
that would need its own design pass before being enabled.

**Refill spawn point.** New pieces enter each segment from that segment's
own top row, not necessarily the top of the physical board — a segment
below a void that has no direct path to the board's global top edge still
refills correctly, because refill is segment-scoped, not board-scoped (see
§ Detailed Rules 9).

### 10. Refill

**The bootstrap/cascade asymmetry is deliberate and load-bearing —
document it precisely, per the task's explicit requirement.**

- **Bootstrap fill** (§ Detailed Rules 2, step 7) uses the
  **retry-until-no-match** algorithm: a color is redrawn if placing it
  would create an immediate run. This guarantees the board the player
  first sees never starts with a "free" match sitting on it.
- **Cascade-step refill** (Refilling state, mid-resolution) uses **plain
  uniform draws** — `next_color("board-refill", color_pool)` — with **no
  retry, no filtering, no avoidance of a resulting match.** A refilled cell
  is fully allowed to coincidentally complete a new run with its neighbors.
  **This is not a bug or an oversight — it is the cascade mechanic itself.**
  Every subsequent Matching pass in the resolution loop exists specifically
  to detect exactly this outcome. The concept prototype validated this
  behavior directly (`REPORT.md`: "valid swaps score and consume moves...
  specials spawn and chain... a full simulated game scored 3,960 with a ×4
  cascade chain") and it is the entire reason a single swap can produce a
  multi-step chain at all.

**Refill mechanics**: for each column, left to right; within each column,
for each segment (Formula 3), top to bottom; within a segment, for each
still-`EMPTY` cell after gravity compaction, top to bottom — draw one
`next_color()`, assign `color = drawn color`, `special_type =
SPECIAL_NONE`, a new `piece_id`, and mark the cell `OCCUPIED`. This exact
traversal order is what RNG Service's Edge Cases require this document to
pin down (§ Detailed Rules 6).

**Presentation boundary.** Board Engine's logical model has no concept of
"a piece visually enters from above the screen" — it only knows a cell
transitioned from `EMPTY` to `OCCUPIED` with a specific final `(row, col)`.
How the Juice Layer animates that piece's entry path (sliding from the top
of the screen, from just above its own segment, or any other visual
treatment) is entirely presentation-layer scope and is not constrained by
this document (§ Detailed Rules 13).

### 11. No-Valid-Move Detection & Reshuffle Policy

**`has_available_move()`.** For every `OCCUPIED` cell, for each of its two
forward neighbors (right, down — checking both directions from every cell
covers all adjacent pairs exactly once), simulate the swap, check whether
either swapped position would now be part of a run ≥3, then revert the
simulation. Additionally, for every cell whose `special_type != SPECIAL_NONE`,
check seam 1 (`is_special_activation_swap`) against each of its four
neighbors — if any returns `true`, a move is available via that special
even without a color match (mirrors the concept prototype's `special ===
'bomb'` short-circuit, generalized through the seam so Board Engine still
needs no knowledge of *which* special types this applies to). Returns
`true` on the first available move found; `false` only if every check
exhausts with no result.

**Reshuffle algorithm.** Reshuffling is entered whenever a stabilized board
(post-bootstrap or post-cascade) would otherwise return to `Idle` but
`has_available_move()` is `false`:

1. Collect every currently `OCCUPIED` piece's `(color, special_type)` pair
   into a flat list, traversed in row-major order (row `0` → `rows-1`, left
   to right within each row — the same fixed traversal convention Bootstrap
   Fill uses, § Detailed Rules 2 step 7), so the mapping from list index
   back to grid position is unambiguous and reproducible (positions are not
   part of what's shuffled — only the *assignment* of piece attributes to
   positions is shuffled).
2. Call `RNG_Service.shuffle("board-refill", list)` (Fisher–Yates, per
   `rng-service.md` Formula F6) to produce a candidate reassignment.
3. Apply the candidate to the board — each candidate-list index maps back to
   the same row-major-ordered cell it was collected from in step 1; check
   two conditions: (a) zero runs currently exist (§ Detailed Rules 4), and
   (b) `has_available_move()` is `true`.
4. If both hold, the reshuffle succeeds — emit `board_reshuffled(attempts_used)`
   and proceed to `Idle`. If either fails, repeat from step 2.
5. Retry up to `RESHUFFLE_MAX_TRIES` (default `60`, matching the concept
   prototype's own retry cap) times. If every attempt fails, fall back to a
   **full board regeneration**: discard all current pieces and re-run § Detailed
   Rules 2's bootstrap fill algorithm (steps 3–8) with fresh `board-refill`
   draws. This fallback is itself fully deterministic (same seed and same
   prior draw history always produce the same fallback trigger and the
   same regenerated board) and is expected to be reached, if ever, only on
   a pathological `color_pool`/`cell_mask` combination.

This reshuffle **never** consumes a player move and is invisible to Level
Objective & Move-Limit System's move counter.

### 12. Determinism Contract

**Master acceptance criterion**: given an identical level file, an
identical master seed (or an identical `(level_id, attempt_number)` pair
resolving to one via RNG Service Formula F1), and an identical ordered
sequence of `swap_request`/`cancel` intents, Board Engine's final board
state and its full emitted signal sequence (names, payloads, and order) are
**byte-identical** across any two runs, on any supported export target
(iOS/Android/Web). This holds because: (a) match detection, gravity, and
the resolution state machine are pure functions of board state with no
external randomness; (b) every RNG-consuming operation draws from the
`board-refill` stream in the single fixed call order documented in § Detailed
Rules 6; (c) Board Engine's own logic never reads wall-clock time, frame
count, or any other non-reproducible input. This is the property every
gdUnit4 test in `tests/unit/board-engine/` is built against (see Acceptance
Criteria).

### 13. Logic/Presentation Separation Boundary

**The entire resolution loop for one triggering event — a swap, a special
activation, or bootstrap — completes synchronously, within the same logic
frame it was triggered in, regardless of how many cascade steps it
contains (up to `MAX_CASCADE_DEPTH`, Formula 6).** Board Engine's public
contract contains no `await`, no timer, no per-frame yield — it is a
stateless-per-call classifier over board state, exactly mirroring Touch &
Input's own "always ≤1 frame" contract (`touch-input.md`, Player Fantasy).
This is what makes Board Engine headless-testable with plain synchronous
gdUnit4 assertions: a test calls `swap_request(a, b)` and can immediately
assert on the final board state and the full signal list, with no need to
simulate multiple engine frames or await anything.

**What this means for the Juice Layer**: it never blocks or paces Board
Engine. It receives the full, already-resolved ordered signal stream for a
move (potentially many `match_cleared`/`cascade_step_advanced` events in a
single synchronous burst) and is entirely responsible for **its own**
wall-clock pacing of how it *reveals* that sequence — replaying the swap
slide, the pop animation, the fall, the escalating callouts, at whatever
tempo feels right, without ever needing to ask Board Engine to "wait." Any
perceived pacing in the finished game is 100% a Juice Layer decision. This
directly implements the task's frame-budget strategy: **logic resolves
instantly; presentation paces reveals.**

**Payload sufficiency guarantee for deferred replay (resolves a BLOCKING
finding from the 2026-07-18 design review).** Because every signal that
reports a piece being cleared, spawned, or placed carries that piece's full
identity inline as a `PieceSnapshot` (§ Detailed Rules 7) — never merely a
bare cell coordinate requiring a live-board lookup to recover — any
consumer that stores a move's full ordered signal stream and replays it
**later**, at its own pace, after the live board has already progressed
through further cascade steps or subsequent moves, can reconstruct **purely
from the stored events**: (a) the exact visual state (`color`,
`special_type`) of every cleared, spawned, or placed piece at the
historical instant its event fired, and (b) any aggregate derived from that
data (e.g., a per-color tally of tiles cleared), with **zero synchronous
queries back into live board state**. This is what makes the deferred-
replay model this section describes actually implementable for the
`collect_color` objective (`level-objectives.md`) and for Juice Layer
rendering beyond a move's first cascade step — both of which the
pre-revision signal catalog structurally foreclosed. The worked walkthrough
below demonstrates the guarantee end to end.

**Worked walkthrough (2-step cascade, per-color tally derived only from
events).** `color_pool = ["red","blue","green","yellow","purple"]`
(indices 0–4). Column 4 of an in-progress board reads, top to bottom:
`row0=blue(1), row1=red(0), row2=green(2), row3=red(0), row4=red(0)`. The
player swaps `(1,4)` and `(2,4)` (red and green) — after the swap, column 4
reads `row0=blue, row1=green, row2=red, row3=red, row4=red`: a new vertical
run of three reds at rows 2–4.

1. `swap_started(piece_a={cell:(1,4),color:0,special_type:0},
   piece_b={cell:(2,4),color:2,special_type:0})` — the pre-swap snapshot
   (`piece_a` is cell `(1,4)`'s pre-swap occupant, red(0); `piece_b` is cell
   `(2,4)`'s pre-swap occupant, green(2) — matching the column's stated
   pre-swap state above).
2. `swap_accepted(cell_a=(1,4), cell_b=(2,4), trigger_source=SWAP_MATCH)`.
3. `match_cleared(chain_index=1, cleared_pieces=[
   {cell:(2,4),color:0,special_type:0}, {cell:(3,4),color:0,special_type:0},
   {cell:(4,4),color:0,special_type:0}], run_data=[{orientation:VERTICAL,
   length:3, cells:[(2,4),(3,4),(4,4)], color:0}], trigger_source=SWAP_MATCH)`
   — **3 red tiles**, entirely recoverable from this one event's own payload.
4. Gravity compacts the two surviving pieces (blue, green) to the bottom of
   the segment; rows 0–2 become `EMPTY` and refill. The (unfiltered, per §
   Detailed Rules 10) draws happen to be red, red, red:
   `pieces_spawned(pieces=[{cell:(0,4),color:0,special_type:0},
   {cell:(1,4),color:0,special_type:0}, {cell:(2,4),color:0,special_type:0}],
   source=CASCADE_REFILL)`.
5. The next Matching pass finds a new vertical run at rows 0–2 (all red):
   `cascade_step_advanced(chain_index=2)`, then `match_cleared(chain_index=2,
   cleared_pieces=[{cell:(0,4),color:0,special_type:0},
   {cell:(1,4),color:0,special_type:0}, {cell:(2,4),color:0,special_type:0}],
   run_data=[{orientation:VERTICAL, length:3, cells:[(0,4),(1,4),(2,4)],
   color:0}], trigger_source=SWAP_MATCH)` — **3 more red tiles**.
6. Gravity/refill settles the column with no further match —
   `pieces_spawned(..., source=CASCADE_REFILL)` for the three non-matching
   replacement draws — Matching finds zero runs,
   `cascade_ended(final_chain_index=2, total_cells_cleared=6,
   trigger_source=SWAP_MATCH)`, `board_stabilized`.

A consumer that stores this entire event sequence — whether it processes
each event live or replays a stored copy later, after the board has already
moved on to further cascade steps or subsequent moves — computes **"6 red
tiles cleared this move"** by summing every `cleared_pieces` entry across
both `match_cleared` events where `color == 0`, and renders both clears'
exact sprites/positions from `cleared_pieces` and both fills' exact
sprites/positions from the two `pieces_spawned` events — all without a
single synchronous query back into live board state. This is the concrete
mechanism behind `collect_color`'s runtime tally (`level-objectives.md`,
forward dependency) and the Juice Layer's ability to correctly render any
cascade step of a move, not only its first.

---

## Formulas

### Formula 1 — Level Manifest Resolution

**Named expression:**
```
manifest_index = 1 + INDEX_OF(level_manifest.entries, level_id)
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `level_id` | String | non-empty, format `<region_code>-<3-digit-sequence>` | Level Data Format's stable string identifier |
| `level_manifest.entries` | Array[String] | length ≥ 1, append-only | Board Engine's ordered registry of every `level_id` ever authored |
| `INDEX_OF` | function | — | 0-based array position of `level_id` within `entries`; undefined if absent (see Edge Cases) |
| `manifest_index` | int | `[1, length(entries)]` | The integer passed to `RNG_Service.start_level_session(level_id=manifest_index, ...)` |

**Output range**: `manifest_index` is a strictly positive integer, unique
per `level_id`, monotonically growing in count as new levels are authored,
and — per the append-only invariant — never reused once assigned, even if
the level is later retired.

**Worked example**: `level_manifest.entries = ["candy_kingdom_hub-001",
"candy_kingdom_hub-002", "candy_kingdom_hub-003", ...,
"candy_kingdom_hub-010"]` (the 10 MVP levels, in authored order).
`level_id = "candy_kingdom_hub-003"` → `INDEX_OF(...) = 2` (0-based) →
`manifest_index = 1 + 2 = 3`.

---

### Formula 2 — Swap Validity Determination

**Named expression:**
```
is_adjacent(a, b)     = (|row_a - row_b| + |col_a - col_b|) == 1
would_match(a, b)     = |find_runs(after_swap(a, b))| > 0
is_special_swap(a, b) = is_special_activation_swap(piece_a, piece_b)      // seam 1
is_valid_swap(a, b)   = is_adjacent(a, b) AND (would_match(a, b) OR is_special_swap(a, b))
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `a`, `b` | (int, int) | in-bounds grid coordinates | The two cells named in a `swap_request` |
| `row_a, col_a, row_b, col_b` | int | within grid bounds | Coordinate components of `a`, `b` |
| `after_swap(a, b)` | function | — | The board state that results from exchanging `a`'s and `b`'s pieces |
| `find_runs(...)` | function | — | § Detailed Rules 4's run-detection algorithm applied to a given board state |
| `piece_a`, `piece_b` | Piece | — | The pieces currently occupying `a` and `b`, before the swap |
| `is_special_activation_swap` | function (seam 1) | `{true, false}` | MVP default `false` (§ Detailed Rules 3) |
| `is_valid_swap` | bool | `{true, false}` | The final validity determination |

**Output range**: Boolean gate. `is_valid_swap = true` triggers the
resolution loop's Clearing state; `false` triggers an instant revert.

**Worked example**: `a = (3, 4)`, `b = (4, 4)` (vertically adjacent,
`is_adjacent = true`). `piece_a.color = 2` (yellow), `piece_b.color = 1`
(orange), neither is a special (`is_special_swap = false`). After the swap,
suppose row 3 now reads `[..., color=2, color=2, color=1, color=2, ...]`
at columns 3–5 with `piece_b`'s new color (1) sitting at `(3,4)` — no run
of 3 forms there or anywhere else affected by the swap → `would_match =
false`. `is_valid_swap = true AND (false OR false) = false` → the swap
reverts.

---

### Formula 3 — Column Segmentation (Gravity/Refill Regions)

**Named expression:**
```
segments(col) = split( playable_cells(col), boundary = VOID )
```

A **segment** is a maximal contiguous run of playable (`EMPTY` or
`OCCUPIED`) cells within one column, bounded above/below by either a
`VOID` cell or a board edge.

| Symbol | Type | Range | Description |
|---|---|---|---|
| `col` | int | `[0, cols-1]` | The column being segmented |
| `playable_cells(col)` | ordered list | length `[0, rows]` | Every non-`VOID` row index in `col`, top to bottom |
| `segments(col)` | Array[Segment] | length ≥ 0 | One or more `{top_row, bottom_row}` ranges |

**Output range**: A column with zero voids produces exactly one segment
spanning `[0, rows-1]`. A column entirely void produces zero segments (no
gravity/refill ever occurs there). A column with `N` internal void cells
produces up to `N+1` segments.

**Worked example**: a 5×5 board, `cell_mask` with column 2's row 2 void
(`row0: 11111`, `row1: 11111`, `row2: 11011`, `row3: 11111`, `row4:
11111`). `segments(2)`: playable rows `[0, 1, 3, 4]` → `[{top_row:0,
bottom_row:1}, {top_row:3, bottom_row:4}]` — **two segments**, each height
2. `segments(0)`, `segments(1)`, `segments(3)`, `segments(4)`: playable
rows `[0,1,2,3,4]` (no voids) → one segment each, `{top_row:0,
bottom_row:4}`. Gravity in column 2's segment `{0,1}` compacts pieces
toward row 1 (its *own* bottom, not the board's global bottom at row 4);
gravity in segment `{3,4}` compacts toward row 4, which happens to coincide
with the board's global bottom in this example.

---

### Formula 4 — Touch-Target Floor Proof Across the Full Grid Range (3–9)

This formula formally closes the advisory item both `touch-input.md`'s and
`level-data-format.md`'s review logs flagged and deferred to this document:
proving the 44px touch-target floor (`MIN_TOUCH_TARGET_PX`,
`.claude/docs/technical-preferences.md`) holds for **every** schema-legal
`grid_width`/`grid_height` combination in Level Data Format's `[3, 9]`
range (V5), not just the reference 8×8 level.

**Coordinate-system assumption (stated explicitly, flagged as a Board
Engine rendering assumption in Tuning Knobs and Dependencies).** This proof
is computed in the same canvas-space coordinate system `touch-input.md`'s
own Formulas 1–3 already use: a fixed `1080 × 1920` reference canvas
(`design/art/art-bible.md`'s portrait reference resolution). Board
Engine's rendering assumes a Godot project stretch configuration (`stretch
mode = canvas_items`, `aspect = keep`) that guarantees this exact
`1080 × 1920` logical coordinate space is always fully visible on any
supported physical device, with letterbox/pillarbox bars absorbing any
aspect-ratio mismatch rather than cropping or rescaling the logical canvas.
Under this guarantee, the proof below holds for *any* physical device —
"smallest supported viewport" reduces to a coordinate-space property, not a
per-device recomputation. (This exact stretch-mode configuration is a
`godot-specialist`/`technical-director` implementation decision this
document assumes but does not own — see Dependencies. If a different
stretch mode is ultimately chosen, this proof must be re-validated against
the new coordinate contract.)

**Named expression:**
```
available_width_px  = CANVAS_WIDTH_PX  − 2 × BOARD_SIDE_MARGIN_PX
available_height_px = CANVAS_HEIGHT_PX − BOARD_TOP_ALLOCATION_PX − BOARD_BOTTOM_ALLOCATION_PX

cell_size_px(gw, gh) = min( available_width_px / gw, available_height_px / gh )

constraint: cell_size_px(gw, gh) ≥ MIN_TOUCH_TARGET_PX   for every (gw, gh) ∈ [3,9] × [3,9]
```

| Symbol | Type | Range | Source | Description |
|---|---|---|---|---|
| `CANVAS_WIDTH_PX` | int | fixed `1080` | `design/art/art-bible.md` | Reference canvas width |
| `CANVAS_HEIGHT_PX` | int | fixed `1920` | `design/art/art-bible.md` | Reference canvas height |
| `BOARD_SIDE_MARGIN_PX` | int | `40` (Tuning Knobs) | Board Engine (this document) | Horizontal margin reserved on each side of the board |
| `BOARD_TOP_ALLOCATION_PX` | int | `640` (Tuning Knobs) | Board Engine, sourced from `art-bible.md`'s "top third of screen: HUD" rule (`1920 / 3 = 640`) | Vertical space reserved above the board for HUD |
| `BOARD_BOTTOM_ALLOCATION_PX` | int | `200` (Tuning Knobs) | Board Engine, conservative reservation for bottom-corner chrome + OS gesture-bar safe area (`art-bible.md`'s "bottom edge/corners: interactive chrome") | Vertical space reserved below the board |
| `gw`, `gh` | int | `[3, 9]` each | Level Data Format V5 | The level's `grid_width`, `grid_height` |
| `available_width_px` | int | derived, `1000` | — | Horizontal pixel budget for the board |
| `available_height_px` | int | derived, `1080` | — | Vertical pixel budget for the board |
| `cell_size_px(gw, gh)` | float | derived | — | The rendered edge length of one board cell — this is the exact value `touch-input.md`'s Formulas 1 and 3 consume as an external input |
| `MIN_TOUCH_TARGET_PX` | float constant | fixed `44` | `.claude/docs/technical-preferences.md` | The hard floor |

**Proof of full-range coverage (not a spot check).** `cell_size_px(gw, gh)
= min(available_width_px / gw, available_height_px / gh)` is monotonically
**non-increasing** in both `gw` and `gh` for fixed, positive
`available_width_px` and `available_height_px`: each individual term (a
positive constant divided by `gw` or `gh` respectively) is strictly
decreasing in its own variable, and the minimum of two coordinate-wise
non-increasing functions is itself non-increasing in each argument —
increasing `gw` or `gh` (holding the other fixed) can only decrease or hold
constant the result, never increase it. Over the rectangular integer domain
`[3,9] × [3,9]`, a function that is jointly non-increasing in both arguments
attains its **global minimum at the domain's upper-right corner**,
`(gw, gh) = (9, 9)` — the corner-minimum conclusion holds whether the
function is strictly or only weakly decreasing at any given point, since the
proof only requires that no other point in the domain can produce a
*smaller* value than the corner. Therefore, verifying the constraint holds
at exactly `(9, 9)` is **sufficient** to guarantee it holds for every other
`(gw, gh)` pair in the schema-legal range — this is a complete proof, not a
sampled worked example.

**Worked example (the binding case, `gw = 9, gh = 9`):**
```
available_width_px  = 1080 − 2×40 = 1000
available_height_px = 1920 − 640 − 200 = 1080

cell_size_px(9, 9) = min(1000/9, 1080/9) = min(111.11, 120.00) = 111.11 px

111.11 ≥ 44   ✓  (headroom ratio ≈ 2.52×)
```

Every other schema-legal combination produces a strictly larger
`cell_size_px` by the monotonicity argument above — for example, `gw=8,
gh=8` (the reference level): `min(1000/8, 1080/8) = min(125.0, 135.0) =
125.0px`, closely consistent with `touch-input.md`'s own illustrative
`cell_size_px ≈ 130px` worked example for the same 8×8 case (the small
delta comes from that document's example being an illustrative rounded
figure, whereas this formula supplies the authoritative, margin-derived
value — see Cross-References). The full 3–9 range therefore clears the
44px floor with a minimum headroom of **2.52×** at the schema's most
extreme dimensions, and does so **before** any runtime assertion is ever
needed — this formula supersedes `touch-input.md`'s Rule 5 debug-build
assertion from "the only safety net" to "a defense-in-depth check that
should structurally never fire."

---

### Formula 5 — Maximum Simultaneous Clear (Per-Step Draw-Call Bound)

**Named expression:**
```
max_cells_per_step = rows × cols
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `rows`, `cols` | int | `[3, 9]` each | The level's grid dimensions |
| `max_cells_per_step` | int | `[9, 81]` | The absolute ceiling on how many cells could clear in a single Clearing state (the fully degenerate case: the entire board matches at once) |

**Output range**: Deterministic, board-geometry-driven, no probability
involved — this is a true worst case, not a heuristic. Bounded above by
`81` (the `9×9` schema maximum).

**Worked example**: `rows = cols = 9` → `max_cells_per_step = 81`. This is
the number Board Engine's `match_cleared` signal could, in the absolute
worst theoretical case, carry in `cleared_pieces` for one event — the value
against which the Juice Layer and the `≤100 draw calls during heaviest
cascade` budget (`.claude/docs/technical-preferences.md`) must be
cross-checked (via candy-sprite atlas batching, a Juice Layer
implementation concern, not this document's). Board Engine's own
obligation here ends at emitting the correct, complete `cleared_pieces`
list every time, however large.

---

### Formula 6 — Cascade Continuation Probability & Safety-Cap Derivation

**No deterministic upper bound on cascade *depth* (number of chained
steps) exists from board geometry alone** — this is the honest, correct
answer, not a limitation of this analysis. Because Refilling draws are
uniform-random (§ Detailed Rules 9) and every cascade step can, by chance,
produce a new run that continues the chain, there is no finite ceiling
derivable purely from `rows`, `cols`, and `color_pool` size. A defensive
engineering cap (`MAX_CASCADE_DEPTH`) is therefore required — not optional
— to guarantee the resolution loop (§ Detailed Rules 6) always terminates.
This formula derives a *justified, low-risk default* for that cap using an
approximate per-step continuation probability.

**Named expression (heuristic approximation, not exact combinatorial
enumeration — explicitly caveated):**
```
p_continue(K, w) = 1 − (1 − 1/K²)^w
P(depth > N)     ≈ p_continue(K, w)^N
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `K` | int | `[3, 5]` | The level's `color_pool` size (Level Data Format V11) |
| `w` | int | `[3, rows×cols]` | Number of cells refilled at the current cascade step; bounded above by the full board (Formula 5). `w = 3` (the minimum legal run size) is used below as the conservative, empirically-typical "sustained" step size — the concept prototype's own measured cascades were dominated by minimal 3-cell clears, with its single deepest chain (×4) never approaching a full-board clear |
| `1/K²` | float | — | Simplified per-refilled-cell heuristic: the probability that one freshly-drawn cell coincidentally completes a run with two already-fixed same-colored neighbors (treats each refilled cell as an independent trial — an intentional simplification, not exact enumeration of every possible run geometry) |
| `p_continue(K, w)` | float | `(0, 1)` | Approximate probability at least one of the `w` refilled cells triggers a new run |
| `N` | int | `≥ 0` | Candidate cascade depth |
| `P(depth > N)` | float | `(0, 1)` | Approximate probability the chain continues past step `N` |

**Output range**: `p_continue` and `P(depth > N)` are both bounded
probabilities in `(0, 1)`; this section's output is a *recommended tuning
value* (`MAX_CASCADE_DEPTH`), not a hard mathematical ceiling.

**Worked example** (worst realistic case for match frequency: `K = 3`
minimum color pool, `w = 3` minimum clear):
```
p_continue(3, 3) = 1 − (1 − 1/9)^3 = 1 − (0.8889)^3 = 1 − 0.7023 ≈ 0.2977   (~29.8%)

P(depth > 8)  ≈ 0.2977^8 ≈ 0.2977 × 0.2977 × ... (8 times) ≈ 0.0000617   (~0.0062%)
```

By depth 8, the approximate probability of a chain continuing is already
under `0.01%` even at the game's most match-prone legal configuration
(`K=3`). **Recommended default: `MAX_CASCADE_DEPTH = 20`** (Tuning Knobs) —
more than double the depth at which this heuristic already predicts
negligible continuation probability, and roughly 5× the concept
prototype's single deepest observed chain (×4, `REPORT.md`). When
`MAX_CASCADE_DEPTH` is reached, the resolution loop force-stabilizes:
any remaining detected runs at that point are **not** cleared, the loop
proceeds directly to the post-cascade `has_available_move()` check (§
Detailed Rules 11), and an error-level diagnostic is logged. Reaching this
cap during real play is not expected to ever occur and would indicate
corrupted level data or an implementation bug, never intended design — see
Edge Cases.

**Sibling termination cap (seam 4).** `MAX_CASCADE_DEPTH` bounds the number
of *across-step* cascade iterations (Matching → Clearing → Falling →
Refilling, repeated). It has an independent, complementary sibling,
`MAX_CHAIN_EXPANSION_ITERATIONS` (Tuning Knobs, default `10`), which bounds
the number of *within-step* seam-4 (`expand_special_chain_reaction`) calls
Board Engine issues while resolving one Clearing state to a fixpoint (§
Detailed Rules 3, "Seam 4 calling semantics"). Both caps are pure iteration
counters enforced unconditionally by Board Engine — together they guarantee
the resolution loop always halts, regardless of how many cascade steps
occur or how large a single step's special-chain expansion grows, even
against an adversarial or buggy `special-candies.md` implementation.

---

### Formula 7 — Worst-Case RNG Draw Budget Per Move

**Named expression:**
```
max_draws_per_move = max_cells_per_step × MAX_CASCADE_DEPTH
```

| Symbol | Type | Range | Description |
|---|---|---|---|
| `max_cells_per_step` | int | `[9, 81]` | Formula 5's output |
| `MAX_CASCADE_DEPTH` | int | `20` (default, Tuning Knobs) | Formula 6's derived safety cap |
| `max_draws_per_move` | int | `[180, 1620]` | Absolute worst-case count of `board-refill` draws a single player move could ever consume |

**Output range**: Bounded above by `1620` at the schema's most extreme
configuration (`9×9` board, `MAX_CASCADE_DEPTH=20`, assuming every one of
the 20 permitted steps degenerately clears and refills the entire board —
itself already excluded by Formula 6's probability analysis, making this a
deliberately pessimistic double-worst-case ceiling).

**Worked example**: `max_cells_per_step = 81` (Formula 5's 9×9 worst case),
`MAX_CASCADE_DEPTH = 20` → `max_draws_per_move = 81 × 20 = 1,620` draws.
Cross-checked against RNG Service's own Acceptance Criteria performance
bar (`rng-service.md`: "Filling an 8×8 board (64 draws) plus one
worst-case full-board cascade refill (≤64 additional draws)... completes
in under 1ms") and its non-functional requirement ("at least 10^9 draws
per stream with no measurable statistical degradation or performance
cliff") — `1,620` draws is roughly `25×` RNG Service's own single-frame
example and a negligible fraction of its supported throughput, confirming
Board Engine's entire draw budget for one move, in the absolute worst
theoretical case, stays comfortably inside the `16.6ms` frame budget
(`.claude/docs/technical-preferences.md`).

---

## Edge Cases

| Scenario | Expected Behavior | Rationale |
|---|---|---|
| `swap_request` for two cells with Manhattan distance ≠ 1 | Rejected before any board mutation (`swap_rejected`, `reason=NOT_ADJACENT`) | Touch & Input's own contract already guarantees adjacency, but Board Engine re-checks defensively — "Board Engine owns ALL validity judgment" applies even to inputs the caller is trusted to have already validated |
| `swap_request` received while `board_input_enabled = false` | Ignored entirely — no state change, no signal | Defense in depth alongside Touch & Input's own busy-drop behavior; Board Engine never processes two swaps concurrently, since resolution is single-threaded and synchronous |
| `swap_request` targets a `VOID` or `EMPTY` cell | Rejected (`swap_rejected`, `reason=NOT_ADJACENT` — structurally malformed input is treated the same as a non-adjacency failure, since neither should ever reach Board Engine from a correctly-behaving caller) | A `VOID`/`EMPTY` cell has no piece to swap; accepting this would corrupt grid state |
| A swap's clear set includes an L or T intersection of a horizontal and vertical run | The two runs' cell sets are unioned into exactly one clear set; the shared cell is cleared exactly once; exactly one `match_cleared` fires for the step | Prevents double-counting a shared cell and keeps the per-step signal contract simple (one `match_cleared` per Clearing state, always) |
| Seam 3 (`resolve_special_spawns`) returns a cell not in the current step's raw clear set | The returned entry is dropped and logged as a warning; Board Engine's own clear set is unaffected | A misbehaving or future-buggy Special Candies implementation can never corrupt Board Engine's grid-state integrity |
| Seam 3's `SpecialSpawn.color` is present but is neither `null` nor a valid index into the level's `color_pool` (or the field is missing entirely) | Treated as malformed; Board Engine falls back to the exempted cell's originating run's `color` (the same result as a correctly-echoed colored special) and logs a warning | Same defensive-validation principle as the row above, applied to the new color-override field (§ Detailed Rules 3) — a misbehaving resolver can degrade to "colored, not colorless" but can never assign an out-of-range or otherwise invalid color |
| Seam 4 (`expand_special_chain_reaction`) returns a cell that is `VOID` or already `EMPTY` | The returned cell is dropped and logged as a warning | Same defensive-validation principle as the row above |
| Seam 4 does not reach a fixpoint (its returned set keeps growing) within `MAX_CHAIN_EXPANSION_ITERATIONS` (default 10) calls for one cascade step | Board Engine stops calling seam 4, uses the last-returned set as that step's finalized clear set, and logs an error-level diagnostic | Guarantees within-step termination against a pathological or buggy chain-expansion resolver, mirroring `MAX_CASCADE_DEPTH`'s force-stabilize pattern one level down (§ Detailed Rules 3, Formula 6); not expected to trigger during genuine play |
| No seam consumer is registered at all (e.g., an isolated gdUnit4 test, or MVP before Special Candies ships) | All four seams behave per their documented MVP defaults (§ Detailed Rules 3) — Board Engine functions as a pure runs-only match-3 engine with zero specials | This is what makes Board Engine fully headless-testable and shippable in complete isolation from `special-candies.md` |
| `pre_placed_pieces` coincidentally form a run at bootstrap | Not filtered or avoided (unlike RNG-filled cells); resolved via one automatic cascade pass at bootstrap, `trigger_source = BOOTSTRAP`, before the board reaches `Idle` — consumes zero player moves | Pre-placed pieces are trusted level-author intent, never silently altered by an avoidance algorithm; reusing the standard cascade pipeline requires no special-cased logic |
| Bootstrap's retry-until-no-match loop exhausts `BOOTSTRAP_MAX_RETRIES_PER_CELL` for a cell | The last-drawn color is accepted unconditionally at that cell, even if it creates a match (resolved by the same bootstrap cascade pass, above) | Deterministic given a seed; expected to be reached only on a pathological `color_pool`/`cell_mask` combination Level Data Format's V7/V11 rules make vanishingly unlikely |
| Reshuffle exhausts `RESHUFFLE_MAX_TRIES` (default 60) without finding a valid matchless, movable arrangement | Hard fallback: full board regeneration via the bootstrap fill algorithm with fresh `board-refill` draws | Deterministic (same seed and prior draw history always trigger and resolve this fallback identically); guarantees the resolution loop never returns a genuinely stuck board to the player |
| `MAX_CASCADE_DEPTH` (default 20) is reached mid-resolution | The loop force-stabilizes: any runs still detected at that point are left uncleared; proceed directly to the post-cascade `has_available_move()`/reshuffle check; an error-level diagnostic is logged | Guarantees loop termination against a pathological data or logic-bug scenario; per Formula 6, reaching this cap during genuine play is not expected |
| A `cell_mask` produces a column with zero playable cells (an entirely `VOID` column, e.g. a diamond shape's outer corner) | `segments(col)` returns an empty list; gravity, refill, and swaps never operate on that column | Trivial consequence of Formula 3's definition, stated explicitly for implementation clarity |
| Two `swap_request`s arrive in the same logic frame (should not happen given Touch & Input's single-active-gesture contract, but defensively) | Processed strictly in arrival order; the first flips `board_input_enabled` to `false` synchronously, so the second is dropped by the "ignored while busy" rule above | Board Engine is single-threaded and synchronous; there is no code path where two swaps are ever mid-resolution simultaneously |
| A level's `EMPTY` cell state is still present when the resolution loop reaches `Idle` | Contract violation — this must never occur; treated as an implementation defect, not a supported runtime state, and asserted against in debug builds | `EMPTY` is defined (§ Detailed Rules 1) as existing only transiently between Clearing and Refilling; an `Idle` board is always fully `OCCUPIED` (or `VOID`) by definition |
| A swap simultaneously satisfies both a normal run match and seam 1's special-activation check | Both clear sets are unioned (§ Detailed Rules 5) — nothing from either path is lost | Simple, non-lossy resolution avoiding an arbitrary priority rule between two legitimately valid triggers |
| A level's `level_id` is not yet present in the Level Manifest (a newly-authored level whose manifest entry wasn't appended) | Bootstrap fails loudly — the level does not load, and the failure is logged as an error | Mirrors Level Data Format's own "load fails loudly, never partially" philosophy (`level-data-format.md` Edge Cases) for the same reason: a silent fallback (e.g., auto-appending) would make manifest state depend on load order, breaking the append-only determinism guarantee |

---

## Dependencies

| System | Direction | Nature of Dependency |
|---|---|---|
| RNG Service (`design/gdd/rng-service.md`, APPROVED) | Board Engine depends on it | Consumes the `board-refill` stream (stream_id 1) for bootstrap fill, cascade-step refill, and both reshuffle paths, in the fixed call order documented in § Detailed Rules 6. Calls `start_level_session(level_id=manifest_index, attempt_number)` at bootstrap, resolving `level_id` via the Level Manifest this document owns (Formula 1). |
| Touch & Input System (`design/gdd/touch-input.md`, APPROVED) | Bidirectional | Board Engine depends on it for `select_cell`/`swap_request`/`cancel` intents (Board Engine ignores `select_cell`/`cancel`, since selection-state is entirely Touch & Input's own internal concern — only `swap_request` reaches this document). Touch & Input, in turn, depends on Board Engine for the `board_input_enabled` boolean it gates all gesture recognition against (`board_input_enabled_changed` signal, § Detailed Rules 7) and for `cell_size_px`, which Board Engine's rendering computes per level (Formula 4) and Touch & Input's own Formulas 1 and 3 consume as an external runtime input. **Recommended follow-up** (not made here, per this document's file-edit scope): `touch-input.md`'s Dependencies section should be updated to cite this document as the authoritative source of `cell_size_px`, rather than treating it as an opaque externally-supplied value. |
| Level Data Format (`design/gdd/level-data-format.md`, APPROVED) | Board Engine depends on it | Reads exactly `grid_width`, `grid_height`, `cell_mask`, `pre_placed_pieces`, `color_pool` at bootstrap (§ Detailed Rules 2). Board Engine has no move-limit-driven behavior — move-limit enforcement is Level Objective & Move-Limit System's scope per `systems-index.md`. `level-data-format.md`'s Dependencies table now correctly reflects this (its Match-3 Board Engine row states "Does NOT read `move_limit`"), reconciling what was previously a flagged discrepancy. |
| Special Candies & Combo Matrix (`design/gdd/special-candies.md`, not yet authored) | Will depend on Board Engine | Implements all four extension seams (§ Detailed Rules 3) and subscribes to `match_cleared`/`special_spawned`/`pieces_spawned`/`cascade_ended` for its own bookkeeping (e.g., harvested-ingredient tallies by color) — `match_cleared.cleared_pieces` and `special_spawned.color` (§ Detailed Rules 7, Revision 2) directly support per-color tallies without a re-query. Board Engine has zero dependency on it — every seam has a documented MVP no-op default. **Reciprocal note**: when authored, its Dependencies section must list this document and confirm its seam implementations against the signatures in § Detailed Rules 3. |
| Scoring & Star Thresholds (`design/gdd/scoring-stars.md`, not yet authored) | Will depend on Board Engine | Consumes `match_cleared` (`chain_index`, `cleared_pieces`, `trigger_source`) and `cascade_ended` (`final_chain_index`) to compute point values — Board Engine emits the *count/identity* and the *chain depth*, never a point value itself. **Reciprocal note**: when authored, its Dependencies section must list this document. |
| Level Objective & Move-Limit System (`design/gdd/level-objectives.md`, not yet authored) | Will depend on Board Engine | Consumes `swap_accepted` (a move was spent — the *only* move-related fact Board Engine reports) and `match_cleared.cleared_pieces`' per-piece `color` (for `collect_color` objective tallies, derivable purely from the event stream per § Detailed Rules 13's deferred-replay guarantee) — reads `move_limit` directly from Level Data Format, not through Board Engine. **Reciprocal note**: when authored, its Dependencies section must list this document. |
| Juice Layer — VFX & Audio Hooks (`design/gdd/juice-layer.md`, not yet authored) | Will depend on Board Engine | Subscribes to the full signal catalog (§ Detailed Rules 7), including `pieces_spawned` for fall-in rendering, to drive all presentation pacing (§ Detailed Rules 13). **Reciprocal note**: when authored, its Dependencies section must list this document. |
| Game UI/Screens Flow (`design/gdd/screen-flow.md`, not yet authored) | Will depend on Board Engine (soft) | Expected future supplier of `attempt_number` at level bootstrap, and co-owner (alongside Touch & Input) of external `board_input_enabled` gating during pause/results modals. For MVP, the Level Preview harness (`level-data-format.md` §5) fills this role. **Reciprocal note**: when authored, its Dependencies section must list this document. |
| `design/art/art-bible.md` (not a `design/gdd/` system) | Board Engine depends on it (constants only) | Supplies `CANVAS_WIDTH_PX`/`CANVAS_HEIGHT_PX` (1080×1920) and the "top third of screen: HUD" layout rule consumed in Formula 4. |
| `.claude/docs/technical-preferences.md` (not a `design/gdd/` system) | Board Engine depends on it (constants + rules only) | Supplies `MIN_TOUCH_TARGET_PX` (44px, Formula 4), the `≤100 draw calls` budget (Formula 5), the `16.6ms` frame budget (§ Detailed Rules 13, Formula 7), and the determinism/gdUnit4 testing rule this entire document is built to satisfy. |

---

## Tuning Knobs

| Parameter | Current Value | Safe Range | Effect of Increase | Effect of Decrease |
|---|---|---|---|---|
| `MAX_CASCADE_DEPTH` | 20 | 10 – 50 | More headroom above realistic play (Formula 6), but a pathological infinite-loop scenario (corrupted data/bug) costs proportionally more logic-frame time before the safety cap intervenes | Less headroom; a legitimately deep, exciting real chain (unlikely per Formula 6, but not impossible) risks being truncated mid-celebration, which would read as a bug to the player |
| `MAX_CHAIN_EXPANSION_ITERATIONS` | 10 | 5 – 30 | More headroom for a single cascade step's seam-4 chain-expansion loop to reach a fixpoint (§ Detailed Rules 3) before force-stabilizing — negligible perf cost per extra iteration, since each is a single synchronous seam call | Fewer iterations risks truncating a legitimately deep single-step special-×-special chain (e.g., several bombs chained together within one clear set) before it fully resolves, which would read as a bug, not a safety net, to the player |
| `RESHUFFLE_MAX_TRIES` | 60 (matches concept prototype) | 20 – 200 | More attempts to find a valid shuffle before falling back to full regeneration — negligible perf cost per attempt, marginal robustness gain | Fewer attempts increases how often the (more visually disruptive) full-regeneration fallback triggers on a difficult `color_pool`/`cell_mask` combination |
| `BOOTSTRAP_MAX_RETRIES_PER_CELL` | 100 | 20 – 500 | More attempts to avoid a match-on-placement at level start; negligible perf cost (bootstrap runs once per level entry, not per frame) | Fewer attempts increases how often a level starts with an unavoidable pre-existing match (resolved automatically per § Detailed Rules 2 step 8, but a visibly "free" first cascade is a slightly worse first impression) |
| `GRAVITY_MODE` | `stop_at_void` (fixed at MVP) | `{stop_at_void, fall_through_void}` | `fall_through_void` (not implemented) would let a candy above a void drop past it into a lower segment — flagged as a future "portal tile" design direction, not validated, not built | N/A — `stop_at_void` is the only implemented mode |
| `BOARD_SIDE_MARGIN_PX` | 40 | 16 – 80 | More breathing room around the board frame; shrinks `cell_size_px` for every grid size (Formula 4), tightening the 44px headroom margin | Less margin grows `cell_size_px`, increasing headroom, but risks the board frame crowding screen edges |
| `BOARD_TOP_ALLOCATION_PX` | 640 (`= CANVAS_HEIGHT_PX / 3`, per `art-bible.md`'s HUD layout rule) | 480 – 800 | More HUD space above the board; shrinks vertical `cell_size_px` budget | Less HUD space risks HUD/board visual crowding, independent of the touch-target proof |
| `BOARD_BOTTOM_ALLOCATION_PX` | 200 | 120 – 320 | More safe-area/chrome margin at the bottom; shrinks vertical `cell_size_px` budget | Less margin risks bottom-corner chrome (pause/settings) crowding the board or an OS gesture-bar safe area |
| `MIN_TOUCH_TARGET_PX` | 44 (locked floor, inherited from `.claude/docs/technical-preferences.md` via `touch-input.md`) | Not tunable below 44 | N/A | **Not permitted below 44px** — hard floor this document proves is always cleared (Formula 4), never a value this document itself adjusts |

---

## Acceptance Criteria

**Determinism (the master gate — all BLOCKING, `tests/unit/board-engine/`)**

- [ ] `test_identical_seed_identical_final_state`: two runs with identical
      `(level_id, attempt_number)` (or an identical injected `master_seed`
      via `start_test_session`) and an identical ordered intent sequence
      produce byte-identical final grid state (every cell's `color`,
      `special_type`, `piece_id`-relative ordering).
- [ ] `test_identical_seed_identical_signal_sequence`: the same two runs
      above emit an identical ordered list of signal names + payloads.
- [ ] `test_different_attempt_number_different_bootstrap`: bootstrapping
      the same `level_id` with `attempt_number=1` vs. `attempt_number=2`
      produces different initial board states (per RNG Service Formula F1).

**Board Model & Bootstrap**

- [ ] `test_bootstrap_reads_only_five_fields`: a mocked Level Data Format
      resource asserts Board Engine's bootstrap call touches
      `grid_width`, `grid_height`, `cell_mask`, `pre_placed_pieces`,
      `color_pool` and no other field.
- [ ] `test_bootstrap_never_leaves_empty_cell`: after bootstrap completes
      and the board reaches `Idle`, every playable cell is `OCCUPIED`
      (zero `EMPTY` cells).
- [ ] `test_bootstrap_rng_fill_never_creates_immediate_match`: for any
      cell filled by the retry-until-no-match algorithm (not
      pre-placed), no run of ≥3 exists through it at the moment it is
      placed, given retries remain under `BOOTSTRAP_MAX_RETRIES_PER_CELL`.
- [ ] `test_pre_placed_pieces_placed_before_rng_fill`: a level with
      `pre_placed_pieces` set has those exact cells populated with the
      exact specified `candy_type`/color after bootstrap, unaltered by
      the RNG fill pass.
- [ ] `test_bootstrap_accidental_match_resolves_via_cascade`: a
      synthetic level whose `pre_placed_pieces` form a run resolves that
      run automatically at bootstrap (`trigger_source=BOOTSTRAP`) before
      reaching `Idle`, with zero moves consumed.

**Level Manifest (Formula 1)**

- [ ] `test_manifest_resolution_matches_position`: a manifest with `N`
      known entries resolves each `level_id` to its correct 1-based
      `manifest_index`, matching Formula 1's worked example exactly.
- [ ] `test_manifest_missing_level_id_fails_loudly`: bootstrapping a
      `level_id` absent from the manifest fails with a logged error and
      does not load the level (see Edge Cases).
- [ ] `test_manifest_no_duplicate_entries`: a data-driven test over the
      real `assets/data/level_manifest.tres` fails if any `level_id`
      string appears more than once.

**Swap Validity (Formula 2)**

- [ ] `test_swap_non_adjacent_cells_rejected`: `swap_request` for two
      cells with Manhattan distance ≠ 1 always emits `swap_rejected(reason=NOT_ADJACENT)`
      with zero grid mutation.
- [ ] `test_swap_no_match_reverts`: a swap producing zero runs and no
      special activation reverts the grid to its exact pre-swap state,
      emits `swap_rejected(reason=NO_MATCH_NO_ACTIVATION)`, and consumes
      no move.
- [ ] `test_swap_producing_match_is_accepted`: a swap producing ≥1 run
      emits `swap_accepted(trigger_source=SWAP_MATCH)` and proceeds into
      Matching.
- [ ] `test_swap_seam_1_activation_bypasses_match_requirement`: with a
      mocked seam-1 resolver returning `true`, a swap producing zero
      color runs is still accepted (`trigger_source=SPECIAL_ACTIVATION`).
- [ ] `test_swap_union_of_match_and_activation_clears`: with seam 1
      returning `true` AND the swap also producing a normal run, the
      resulting clear set is the union of both, with no cell lost or
      duplicated.

**Match Detection (§ Detailed Rules 4)**

- [ ] `test_horizontal_run_of_three_detected`, `test_vertical_run_of_three_detected`:
      minimal positive cases.
- [ ] `test_run_of_two_not_detected`: `MIN_RUN_LENGTH` boundary,
      confirming length-2 sequences never register as a run.
- [ ] `test_colorless_piece_never_matches`: a piece with `color = -1`
      (`COLOR_NONE`) is never part of any detected run, regardless of
      its neighbors' colors.
- [ ] `test_l_shape_intersection_unions_into_one_clear_set`: a
      synthetic board with an intersecting horizontal + vertical run
      produces exactly one `match_cleared` event whose `cleared_pieces`
      contains the shared cell exactly once.

**Extension Seams (§ Detailed Rules 3)**

- [ ] `test_seams_default_to_noop_with_no_consumer`: with no seam
      resolver registered, `is_special_activation_swap` always returns
      `false`, `resolve_special_spawns` always returns an empty map, and
      `expand_special_chain_reaction` always returns its input unchanged
      — confirming Board Engine functions as a pure runs-only engine in
      isolation.
- [ ] `test_seam_3_invalid_cell_response_dropped`: a mocked seam-3
      resolver returning a cell outside the current clear set is
      dropped and logged, with Board Engine's own clear set unaffected.
- [ ] `test_seam_3_color_override_null_produces_colorless_piece`: a mocked
      seam-3 resolver returning `{special_type: BOMB, color: null}` for an
      exempted cell results in a spawned piece with `color = COLOR_NONE
      (-1)`, and that piece is confirmed excluded from run detection on a
      subsequent Matching pass (§ Detailed Rules 4).
- [ ] `test_seam_3_color_override_explicit_value_produces_colored_piece`:
      a mocked seam-3 resolver returning `{special_type: STRIPE_H, color:
      <valid color_pool index>}` results in a spawned piece with exactly
      that `color`.
- [ ] `test_seam_3_invalid_color_falls_back_to_run_color`: a mocked seam-3
      resolver returning a `color` that is neither `null` nor a valid
      `color_pool` index (or omits the field) results in the spawned
      piece's `color` falling back to the exempted cell's originating
      run's `color`, with a warning logged.
- [ ] `test_seam_4_called_iteratively_to_fixpoint`: a mocked seam-4
      resolver that adds extra cells across multiple successive calls (a
      strict superset each time) results in Board Engine calling it
      repeatedly — not once — until a call returns its input unchanged,
      with every added cell present in the same step's `match_cleared`
      payload, confirming the calling semantics in § Detailed Rules 3.
- [ ] `test_seam_4_exceeds_iteration_cap_force_stabilizes`: a mocked
      seam-4 resolver engineered to always grow its returned set (never
      reaching a fixpoint) is force-stopped at exactly
      `MAX_CHAIN_EXPANSION_ITERATIONS` calls, uses the last-returned set
      as the step's finalized clear set, and logs an error-level
      diagnostic, without hanging the resolution loop.
- [ ] `test_special_spawned_fires_for_seam_3_exempted_cells`: a mocked
      seam-3 resolver exempting one cell from a run results in exactly
      one `special_spawned` signal for that cell, carrying its resolved
      `color` (per the `SpecialSpawn.color` tests above), and that cell is
      absent from the same step's `match_cleared.cleared_pieces`.
- [ ] `test_swap_anchor_cells_empty_for_cascade_steps_beyond_first`: a
      mocked seam-3 resolver asserts `swap_anchor_cells` equals the
      triggering swap's two cells at `chain_index = 1`, and equals the
      empty set for every `chain_index ≥ 2` step of the same move (§
      Detailed Rules 3).

**Signal Payload Completeness (§ Detailed Rules 7, 13 — Revision 2)**

- [ ] `test_swap_started_carries_piece_snapshots`: `swap_started`'s
      `piece_a`/`piece_b` `PieceSnapshot`s match the board's actual
      pre-swap `color`/`special_type` at each cell exactly.
- [ ] `test_match_cleared_cleared_pieces_carries_full_identity`: every
      entry in `match_cleared.cleared_pieces` matches that cell's actual
      `color`/`special_type` immediately before it cleared, for a
      multi-run, multi-color clear set in a single step.
- [ ] `test_special_activated_carries_piece_snapshots`: `special_activated`'s
      `piece_a`/`piece_b`/`cleared_pieces` follow the same identity
      guarantees as `swap_started`/`match_cleared` for a seam-1/seam-2
      triggered move.
- [ ] `test_pieces_spawned_fires_once_for_bootstrap`: bootstrap emits
      exactly one `pieces_spawned(source=BOOTSTRAP)` whose `pieces` array
      covers every playable cell (pre-placed and RNG-filled combined),
      each with the correct `color`/`special_type`, fired before step 8's
      Matching pass.
- [ ] `test_pieces_spawned_fires_per_refilling_state`: each completed
      `Refilling` state (mid-move cascade or bootstrap's own automatic
      cascade) emits exactly one `pieces_spawned(source=CASCADE_REFILL)`
      whose `pieces` array covers exactly the cells filled during that
      pass, no more and no fewer.
- [ ] `test_deferred_replay_color_tally_matches_live_tally`: for a
      synthetic multi-step cascade (modeled on the § Detailed Rules 13
      worked walkthrough), a per-color tally computed by summing
      `cleared_pieces` entries across a **stored and later-replayed** copy
      of the full signal stream exactly matches a tally computed **live**
      during synchronous emission — confirming the deferred-replay
      guarantee holds with zero synchronous board queries.

**Gravity & Column Segmentation (Formula 3)**

- [ ] `test_segmentation_matches_worked_example`: Formula 3's 5×5
      worked example (a single mid-column void) reproduces the
      documented two-segment result exactly.
- [ ] `test_gravity_stops_at_void_never_falls_through`: a piece
      directly above a `VOID` cell never moves into or past that void
      during gravity compaction.
- [ ] `test_gravity_independent_per_segment`: clearing cells in one
      segment of a column never affects piece positions in a different
      segment of the same column.
- [ ] `test_fully_void_column_never_touched`: a column with zero
      playable cells never participates in any gravity, refill, or
      swap operation.

**Refill — The Bootstrap/Cascade Asymmetry (§ Detailed Rules 9, 10)**

- [ ] `test_cascade_refill_allows_immediate_match`: a mocked
      `board-refill` sequence engineered to produce a matching run on
      refill is **not** filtered or retried — the resulting run is
      detected on the very next Matching pass and continues the
      cascade (`cascade_step_advanced` fires).
- [ ] `test_bootstrap_fill_never_allows_immediate_match_for_rng_cells`:
      confirms the opposite guarantee for step 7's bootstrap-only
      retry-until-no-match algorithm (already covered above, restated
      here for the asymmetry pairing).
- [ ] `test_refill_draw_order_matches_documented_contract`: given a
      mocked `board-refill` stream, refill draws occur in exactly the
      column-then-segment-then-row order documented in § Detailed
      Rules 6/10.

**No-Valid-Move Detection & Reshuffle (§ Detailed Rules 11)**

- [ ] `test_has_available_move_detects_horizontal_and_vertical_candidates`:
      positive cases for both swap directions producing a hypothetical
      match.
- [ ] `test_has_available_move_false_on_synthetic_deadlock_board`: a
      hand-constructed board with genuinely zero legal moves returns
      `false`.
- [ ] `test_special_piece_always_counts_as_available_move`: with a
      mocked seam 1 always returning `true` for a given piece, any cell
      adjacent to that piece registers as an available move regardless
      of color.
- [ ] `test_reshuffle_never_produces_immediate_match`: every successful
      reshuffle result has zero detected runs before entering `Idle`.
- [ ] `test_reshuffle_guarantees_available_move`: every successful
      reshuffle result has `has_available_move() == true`.
- [ ] `test_reshuffle_exhausts_to_full_regeneration`: with a mocked
      `shuffle()` engineered to never satisfy both reshuffle conditions
      within `RESHUFFLE_MAX_TRIES`, the fallback full-regeneration path
      triggers exactly once and produces a valid, movable board.
- [ ] `test_reshuffle_consumes_no_player_move`: a mid-game reshuffle
      never emits `swap_accepted` and never affects any external move
      counter.

**Cascade Resolution & Safety Cap (Formula 6)**

- [ ] `test_chain_index_increments_per_cascade_step`: a synthetic
      multi-step cascade (mocked `board-refill` sequence engineered to
      chain 3 times) produces `match_cleared` events with
      `chain_index = 1, 2, 3` in order.
- [ ] `test_cascade_ended_reports_correct_final_chain_index`: the same
      scenario's `cascade_ended.final_chain_index` equals `3`.
- [ ] `test_max_cascade_depth_force_stabilizes`: a mocked
      `board-refill` sequence engineered to always produce a new run
      (pathological/adversarial test fixture) is force-stabilized at
      exactly `MAX_CASCADE_DEPTH` steps, with an error-level diagnostic
      logged and the loop still reaching `Idle` (or `Reshuffling`)
      rather than hanging.

**Logic/Presentation Separation (§ Detailed Rules 13)**

- [ ] `test_full_resolution_completes_within_one_synchronous_call`: a
      test that triggers `swap_request` and immediately (same call
      stack, no `await`, no frame yield) asserts on the fully-resolved
      final board state and complete signal list.
- [ ] `test_no_timer_or_await_in_public_api`: interface inspection
      confirms no public Board Engine method returns a signal/promise
      requiring the caller to wait across frames.

**Touch-Target Floor Proof (Formula 4)**

- [ ] `test_cell_size_at_9x9_meets_floor`: `cell_size_px(9, 9)`
      reproduces Formula 4's worked example (`111.11px`) and is `≥ 44`.
- [ ] `test_cell_size_monotonically_decreasing`: for a sample of
      `(gw, gh)` pairs across `[3,9]×[3,9]`, `cell_size_px` is verified
      non-increasing as either dimension increases, empirically
      confirming the monotonicity argument the proof relies on.
- [ ] `test_cell_size_never_below_floor_for_any_schema_legal_dimension`:
      an exhaustive loop over all 49 `(gw, gh)` combinations in
      `[3,9]×[3,9]` asserts `cell_size_px(gw, gh) ≥ MIN_TOUCH_TARGET_PX`
      for every one.

**Performance**

- [ ] A worst-case single-move resolution (9×9 board, `MAX_CASCADE_DEPTH`
      cascade steps, each a full-board clear+refill) completes within
      the `16.6ms` frame budget on the target mid-range mobile profile,
      cross-checked against Formula 7's `1,620`-draw ceiling and RNG
      Service's own sub-1ms-per-64-draws performance AC.
- [ ] No hardcoded gameplay values: `MAX_CASCADE_DEPTH`,
      `RESHUFFLE_MAX_TRIES`, `BOOTSTRAP_MAX_RETRIES_PER_CELL`,
      `BOARD_SIDE_MARGIN_PX`, `BOARD_TOP_ALLOCATION_PX`,
      `BOARD_BOTTOM_ALLOCATION_PX` are all sourced from data-driven
      config, never literals in `src/` (per `.claude/docs/coding-standards.md`).

---

## Cross-References

| This Document References | Target | Specific Element | Nature |
|---|---|---|---|
| `level_id` String→integer resolution requirement | `design/gdd/rng-service.md` | §3, Edge Cases ("must be resolved... via a level manifest") | Resolved here (§ Detailed Rules 2, Formula 1) — this document is the manifest's owner |
| RNG draw call-order documentation requirement | `design/gdd/rng-service.md` | Edge Cases table, "that order must be documented in `board-engine.md`'s implementation" | Resolved here (§ Detailed Rules 6) |
| 44px touch-target floor vs. 3–9 grid range compatibility | `design/gdd/touch-input.md` review log; `design/gdd/level-data-format.md` review log | Both logs' "Recommended Revisions — Advisory" items requesting this document close the gap | Resolved here (Formula 4) |
| `cell_size_px` authoritative source | `design/gdd/touch-input.md` | Formulas 1 and 3, which consume `cell_size_px` as an externally-supplied runtime value | This document is that authoritative source (Formula 4); `touch-input.md` itself is not edited here |
| `move_limit` field non-consumption | `design/gdd/level-data-format.md` | Dependencies table's Match-3 Board Engine row | Resolved — this document does not read `move_limit`, and `level-data-format.md`'s Dependencies table has been reconciled to state this explicitly (previously flagged here as a discrepancy; confirmed fixed as of this review) |
| Reference tuning values (8×8 board, 5 colors, cascade behavior) | `prototypes/sweet-cascade-concept/REPORT.md` | "If Proceeding" section; Lessons Learned (spawn anchoring, special-×-special chains) | Data dependency (prototype, not a GDD — cited as design rationale throughout Detailed Rules and Formulas) |
| Level Manifest addition to the authoring workflow | `design/gdd/level-data-format.md` | §5, Authoring Workflow | Recommended follow-up (§ Detailed Rules 2) — a new step appending to `level_manifest.tres` at validation time; not made in that document here |

---

## Open Questions

| Question | Owner | Deadline | Resolution |
|---|---|---|---|
| Does `board_reshuffled`'s `attempts_used`-only payload need per-cell reassignment data (e.g., an array of `{cell, color, special_type}`) to satisfy the same deferred-replay payload-sufficiency guarantee (§ Detailed Rules 13) Revision 2 added to `match_cleared`/`pieces_spawned`/`special_spawned`? | systems-designer | At `juice-layer.md` authoring, if reshuffle needs deferred-replay-safe rendering rather than a live synchronous `get_piece_at()` query (which remains valid immediately after `board_reshuffled` fires, since no other board-mutating signal intervenes before the following `board_input_enabled_changed(true)`) | — |
| Should `GRAVITY_MODE`'s `fall_through_void` alternative ever be built as a real level-design mechanic ("portal tiles"), or should it be removed from Tuning Knobs entirely as speculative? | game-designer | Revisit at Alpha content planning, once more non-rectangular `cell_mask` levels exist to evaluate demand | — |
| Should Board Engine's `BOARD_SIDE_MARGIN_PX`/`BOARD_TOP_ALLOCATION_PX`/`BOARD_BOTTOM_ALLOCATION_PX` constants move to a future `design/ux/hud.md` once that UX spec exists, to avoid two sources of truth for screen layout? | game-designer / ux-designer | At `design/ux/hud.md` authoring, if/when it supersedes these provisional values | — |
| Does `level-data-format.md`'s Dependencies table need a follow-up correction removing `move_limit` from Board Engine's listed field consumption (§ Detailed Rules 2's flagged discrepancy)? | systems-designer | At next `level-data-format.md` review pass | **Resolved** — confirmed during this review (2026-07-18) that `level-data-format.md`'s Dependencies table already states Board Engine does not read `move_limit`; no further action needed. |
| Should the Level Manifest (`level_manifest.tres`) formally become a step in `level-data-format.md`'s §5 Authoring Workflow, rather than living only in this document? | systems-designer / game-designer | Before the first non-MVP level batch is authored (Alpha content planning) | — |
| Is Formula 6's `p_continue` heuristic worth replacing with an empirical measurement (instrumented cascade-depth histogram from real playtest sessions) once Vertical Slice levels exist, rather than relying on the analytical approximation? | systems-designer | At Vertical Slice, once real play data exists | — |
| **v2 seam-extension sketch — spawn+clear composite combos** (review advisory, 2026-07-18): future combo types that must atomically spawn a special AND clear additional cells in the same step (e.g., a wrapped-candy double-blast, or bomb×stripe converting a full row into striped candies before detonating them) are not expressible through the current seam 2/3/4 signatures, which separate spawning from clearing. Sketch for v2 if needed: replace seam 3's return with a step-plan object `{spawns: Map[cell, SpecialSpawn], extra_clears: Set[cell], transforms: Map[cell, SpecialSpawn]}` applied atomically between Matching and Clearing — additive change, no event-stream redesign required since `pieces_spawned`/`match_cleared` payloads already carry full piece identity. Do not build until the Special Candies GDD demands a combo that needs it. | systems-designer (with Special Candies author) | At Special Candies & Combo Matrix design, if a composite combo is specced | — |
