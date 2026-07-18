# Design Review Log: Match-3 Board Engine

Target document: `design/gdd/board-engine.md`

---

## Review — 2026-07-18 — Verdict: NEEDS REVISION

**Mode**: `/design-review --depth lean`, autonomous remote session (no specialist
agent spawning, no AskUserQuestion, no approval waits).
**Reviewer**: game-designer (self-authored analysis; no `systems-designer`,
`qa-lead`, or `creative-director` subagents spawned — lean mode).
**Re-review**: No — first review.
**Scope note**: This is the Core-layer bottleneck GDD every Feature-layer
system extends. Beyond the standard completeness/consistency pass, this
review specifically recomputed every worked formula, cross-checked API
usage against the three APPROVED Foundation GDDs, walked all four extension
seams against the four validated prototype behaviors, stress-tested the
determinism contract, and checked signal-payload sufficiency against every
named downstream consumer.

### Completeness: 8/8 sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present, plus Cross-References and
Open Questions (following the pattern `level-data-format.md`'s review log
recommended other Foundation/Core docs adopt). This is the largest and most
formula-dense GDD reviewed so far (7 formulas, 4 extension seams, an
11-state resolution loop, ~90 Acceptance Criteria bullets).

### Dependency Graph

Declared "Depends On": RNG Service, Touch & Input System, Level Data Format —
all three ✓ exist, all three ✓ APPROVED.

Declared as depended-on-by / referenced:
- ✗ `design/gdd/special-candies.md` — NOT FOUND (correctly flagged "not yet
  authored," forward dependency with reciprocal-note obligation stated).
- ✗ `design/gdd/scoring-stars.md` — NOT FOUND (correctly flagged).
- ✗ `design/gdd/level-objectives.md` — NOT FOUND (correctly flagged).
- ✗ `design/gdd/juice-layer.md` — NOT FOUND (correctly flagged).
- ✗ `design/gdd/screen-flow.md` — NOT FOUND (correctly flagged, soft
  dependency, Level Preview harness fills the role at MVP).
- ✓ `design/art/art-bible.md` — exists; cited correctly for canvas
  dimensions and HUD layout constants.
- ✓ `.claude/docs/technical-preferences.md` — exists; cited correctly.

No broken references — every unwritten GDD is honestly labeled.

### Fixes Applied Directly (mechanical reconciliation — logged per fix policy)

1. **Formula 6 arithmetic error.** The worked example computed
   `P(depth > 8) ≈ 0.2977^8` and reported `≈ 0.000039 (~0.004%)`. Recomputed
   by hand three independent ways (direct iterative multiplication,
   `(0.2977²)⁴` squaring, and a log/exp cross-check): the correct value is
   `≈ 0.0000617 (~0.0062%)` — roughly 1.6× larger than the doc's stated
   figure. This does **not** change the document's conclusion ("under 0.01%
   by depth 8" — `0.0000617 < 0.0001` still holds), so `MAX_CASCADE_DEPTH =
   20`'s justification is unaffected. Fixed the number in place.
2. **Formula 7 range error.** The `max_draws_per_move` variable table stated
   its output range as `[27, 1620]`. `MAX_CASCADE_DEPTH` is treated as fixed
   at its default (`20`) throughout this formula (its own table row lists
   "Range" as literally `20 (default, Tuning Knobs)`, not a variable span),
   so the only genuinely varying input is `max_cells_per_step` (Formula 5's
   `[9, 81]`). The correct range is therefore `[9×20, 81×20] = [180, 1620]`;
   `27` does not correspond to any derivable combination of this document's
   own stated inputs. Fixed to `[180, 1620]`.
3. **Formula 4 mathematical imprecision.** The monotonicity proof claimed
   `cell_size_px(gw, gh) = min(width/gw, height/gh)` is "strictly decreasing
   in both `gw` and `gh`." This is not quite correct: a `min()` of two
   single-variable-decreasing terms is only **non-increasing** (weakly
   monotonic) in each argument — whichever term isn't currently the binding
   minimum can decrease further without changing the output, producing flat
   regions. The corner-minimum conclusion `(gw,gh)=(9,9)` still holds
   perfectly (non-increasing is sufficient for a corner-minimum argument;
   strict decrease is not required), so **no finding changes** — this was a
   terminology fix only. Reworded to "monotonically non-increasing," with
   the corrected justification for why weak monotonicity is still
   sufficient for the corner-proof.
4. **Stale "flagged discrepancy" text (4 locations).** § Detailed Rules 2's
   closing paragraph, the Dependencies table's Level Data Format row, the
   Cross-References table, and the Open Questions table all described an
   open discrepancy: "`level-data-format.md`'s own Dependencies table
   currently lists `move_limit` as something Board Engine reads." I read
   the current `level-data-format.md` directly — its Dependencies table
   **already** states "Does NOT read `move_limit` — move-limit enforcement
   is Level Objective's scope per board-engine.md," matching this document's
   position exactly. The discrepancy no longer exists (per the task's own
   framing, this is "the just-corrected rule"). Updated all four locations
   to state the reconciliation is complete rather than pending.
5. **Reshuffle determinism gap.** § Detailed Rules 11 step 1 ("Collect every
   currently `OCCUPIED` piece's `(color, special_type)` pair into a flat
   list") did not specify a traversal order for building that list. Every
   other RNG-consuming traversal in this document (bootstrap fill,
   cascade-step refill) is meticulously order-pinned specifically because
   `rng-service.md`'s Edge Cases table requires "call order alone determines
   the sequence, and that order must be documented in `board-engine.md`'s
   implementation." An unordered list-collection step means two
   textually-compliant implementations could assign the same
   `RNG_Service.shuffle()` output to *different* physical cells for an
   identical seed and identical prior draw history — a real determinism gap
   relative to this document's own § Detailed Rules 12 master claim
   ("byte-identical... across any two runs"). Fixed by pinning the
   collection (and reapplication) order to the same row-major convention
   already established for bootstrap fill.

### Formula Verification (worked examples recomputed by hand)

- **Formula 1** (Level Manifest): `level_id="candy_kingdom_hub-003"` at
  0-based index 2 → `manifest_index = 3` ✓ matches doc.
- **Formula 2** (Swap Validity): worked example's `is_valid_swap = true AND
  (false OR false) = false` ✓ matches doc; boolean logic and adjacency
  check both correct.
- **Formula 3** (Column Segmentation): 5×5 board, column 2 with row 2 void
  → playable rows `[0,1,3,4]` → two segments `{0,1}` and `{3,4}` ✓ matches
  doc exactly.
- **Formula 4** (Touch-Target Floor): recomputed independently —
  `available_width_px = 1000`, `available_height_px = 1080`,
  `cell_size_px(9,9) = min(111.11, 120.0) = 111.11px`, headroom ratio
  `111.11/44 ≈ 2.52×` ✓ matches doc. The `8×8` cross-check
  (`min(125.0, 135.0) = 125.0px`) ✓ matches doc. **The monotonicity proof's
  conclusion is valid** (see Fix #3 above for the wording correction) — the
  corner-minimum argument is mathematically sound: for any `(gw₂,gh₂) ≥
  (gw₁,gh₁)` componentwise, `min(a₂,b₂) ≤ min(a₁,b₁)` whenever `a₂≤a₁` and
  `b₂≤b₁`, which holds here since each term is antitone in its own
  variable — so `(9,9)` really is the global minimum over `[3,9]×[3,9]`,
  and the exhaustive-sufficiency claim holds.
- **Formula 5** (Max Simultaneous Clear): `9×9 = 81` ✓ trivial, correct.
- **Formula 6** (Cascade Continuation Probability): `p_continue(3,3) = 1 −
  (8/9)³ = 1 − 512/729 = 1 − 0.70233 ≈ 0.29767` ✓ matches doc's `≈0.2977`.
  `P(depth>8)` had an arithmetic error — see Fix #1.
- **Formula 7** (Worst-Case RNG Draw Budget): `81 × 20 = 1,620` ✓ matches
  doc's worked example; range column had an error — see Fix #2.

**Summary: 2 real arithmetic/derivation errors found and fixed (Formula 6,
Formula 7), 1 mathematical-rigor wording issue found and fixed (Formula 4).
All worked-example conclusions the document actually relies on downstream
remain valid post-fix — no design recommendation changes as a result of any
formula fix.**

### Contract Coherence — RNG Service (`rng-service.md`, APPROVED)

- **API usage**: every call (`start_level_session(level_id, attempt_number)`,
  `next_color(stream_name, active_colors)`, `shuffle(stream_name, array)`)
  matches rng-service.md's documented signatures exactly, including
  parameter order and names.
- **Stream naming**: all four RNG consumption points (bootstrap fill,
  bootstrap reshuffle, cascade-step refill, mid-game reshuffle) draw
  exclusively from `"board-refill"` (stream_id 1), matching the Stream
  Registry exactly. Draw order across all four situations is fully pinned
  (§ Detailed Rules 6), satisfying rng-service.md's own explicit
  requirement that call order be documented here.
- **Real discrepancy found (not fixable in this file): `fork_stream()`
  anticipation vs. actual usage.** `rng-service.md`'s own Dependencies table
  states Board Engine "uses `fork_stream()` for the 'no valid moves'
  reshuffle mechanic," flagged there as "to be confirmed in
  `board-engine.md` when authored." This document's actual Reshuffle
  algorithm (§ Detailed Rules 11) uses `shuffle("board-refill", list)`
  directly — **not** a forked child stream. This is not a functional bug
  (determinism still holds; the full draw order is documented either way,
  and the stream-isolation guarantee is irrelevant to a single stream
  drawing from itself in a fixed order), but it means `rng-service.md`'s
  Dependencies table now describes a mechanism this document doesn't
  actually use. Logged as **advisory** — recommend `rng-service.md` be
  updated at its next review pass to say Board Engine consumes
  `board-refill` directly for reshuffle, not via `fork_stream()`. Not fixed
  here (out of this review's file-edit scope, symmetric to how this
  document itself declines to edit `level-data-format.md` or
  `touch-input.md` directly).

### Contract Coherence — Level Data Format (`level-data-format.md`, APPROVED)

- Confirmed exactly 5 fields read at bootstrap (`grid_width`, `grid_height`,
  `cell_mask`, `pre_placed_pieces`, `color_pool`); confirmed `move_limit`,
  `objectives`, and star fields are correctly excluded, and confirmed this
  now agrees word-for-word with `level-data-format.md`'s own Dependencies
  table (see Fix #4 above — this was the primary contract-coherence finding
  and it resolved cleanly in the document's favor).
- `pre_placed_pieces.candy_type` → `color` mapping (via index into
  `color_pool`) is consistent with the schema's field types.
- `cell_mask` VOID/playable handling (§1, Formula 3) is consistent with
  V6–V8's schema guarantees (single connected region, `'1'`/`'0'`
  characters, row/col count matching `grid_height`/`grid_width`).

### Contract Coherence — Touch & Input System (`touch-input.md`, APPROVED)

- **Three-intent contract**: Board Engine correctly consumes only
  `swap_request`; `select_cell`/`cancel` are correctly identified as
  Touch & Input's own internal concern with zero action required from Board
  Engine. This matches touch-input.md's own framing exactly.
- **Busy-state policy**: `board_input_enabled_changed(enabled: bool)`,
  fired "every transition into or out of `Idle`," is exactly the signal
  touch-input.md's Rule 4 reads to gate all gesture recognition. The
  Edge Cases table's "`swap_request` received while `board_input_enabled =
  false`" row (silently ignored) is consistent with — and defense-in-depth
  alongside — touch-input's own default drop-while-busy behavior
  (`input_buffer_depth = 0`). No gap found.
- **`cell_size_px` authority**: Board Engine's Formula 4 is correctly
  identified as the authoritative source touch-input.md's Formulas 1 and 3
  consume as an external input, with a correctly-scoped "recommended
  follow-up, not made here" note rather than an out-of-scope edit.

### Extension Seam Walkthrough (Task Focus Item #3)

Walked all four validated prototype behaviors (`REPORT.md`) through the
four seams:

1. **Striped candy on match-4** — fits cleanly. Seam 3 receives `runs`
   (with `orientation`, `length`, `cells`) and `swap_anchor_cells`; a
   length-4 run with anchor-preferring exemption logic is squarely what
   seam 3 is built for. No gap.
2. **Color bomb on match-5 — GAP FOUND (blocking).** § Detailed Rules 1
   explicitly frames `color = COLOR_NONE` as the mechanism that "keeps
   colorless-special support (e.g., a future color-bomb type) entirely
   inside Special Candies' scope with **no Board Engine change required**."
   But seam 3's signature is `resolve_special_spawns(runs,
   swap_anchor_cells) -> Map[cell, special_type]` — it returns **only**
   `special_type`, with no way to set or override the exempted cell's
   `color`. A spawned color-bomb piece would, per the seam's actual
   signature, keep whatever color its originating run had — contradicting
   the document's own colorless-support claim, and reintroducing exactly
   the failure mode `COLOR_NONE` exists to prevent (a "yellow bomb" could
   still incidentally join a normal same-color run in match detection,
   §4). See "Required Before Implementation" below.
3. **Special-×-special chains** — fits, via seam 1+2 for direct-swap
   combos and seam 4 for cascade-caught combos; seam 4's stated purpose
   ("a stripe clears its row... this is the mechanism behind the
   'special-×-special chains' finding") directly names this behavior.
   **Minor ambiguity (advisory)**: seam 4's purpose description says
   "recursively trigger further clears," but its call contract says "every
   cascade step... on the finalized raw clear set" (one call). It's
   workable either way (a black-box resolver can internally loop to a
   fixpoint within one call), but the document doesn't say which party —
   Board Engine or the seam-4 resolver — is responsible for multi-level
   chain recursion. Recommend clarifying at `special-candies.md` authoring
   time.
4. **Bomb passive detonation during cascades** — fits. Seam 4 gives
   `special-candies.md` full discretion over whether a bomb caught in a
   cascade-step clear set expands into a full detonation or not — meaning
   the prototype's flagged open design question ("can feel unearned") is
   correctly left as `special-candies.md`'s call, not foreclosed or forced
   by this document. No gap.

**Additional gap found while walking this list: `swap_anchor_cells` for
cascade steps beyond the first (advisory).** Seam 3 is called "every
cascade step" and always receives `swap_anchor_cells`, described as "the
cells involved in the triggering swap." By cascade step 2+, those two cells
have near-certainly already cleared or moved (gravity has run at least
once) — so what value this parameter holds for later steps (the stale
original cells? an empty set?) is not stated. A match-4/5 formed on a later
cascade step (the norm, not the exception, per Formula 6's whole premise)
has no defined anchor rule. Recommend `board-engine.md` state explicitly
what `swap_anchor_cells` is for chain steps 2+ (most likely: empty/absent,
requiring `special-candies.md` to define its own fallback anchor, e.g. a
run's first or center cell).

### Determinism Contract (Task Focus Item #4)

- All 4 RNG consumption points confirmed on the single named `board-refill`
  stream with pinned call order (see RNG Service coherence section above).
- Bootstrap retry-until-no-match loop: deterministic given seed (same seed
  → same retry counts → same fallback outcome). No gap.
- `MAX_CASCADE_DEPTH` force-stabilization: triggering condition and
  resulting behavior are both pure functions of already-deterministic prior
  draws. No gap.
- Reshuffle retry loop (`RESHUFFLE_MAX_TRIES`) and full-regeneration
  fallback: deterministic given seed. No gap.
- **Reshuffle list-collection order: real gap found and fixed** (see Fix #5
  above) — this was the one genuine hole in an otherwise fully
  order-pinned determinism contract.
- Seam calls (1–4) are fully order-pinned relative to Board Engine's own
  resolution loop states (§ Detailed Rules 3's "Resolution order within one
  cascade step"), which is sufficient for a future `special-candies.md` to
  build its own `special-drop`-stream determinism story on top of, per
  RNG Service's stream-isolation guarantee (consuming `special-drop` never
  perturbs `board-refill`, regardless of interleaving).

**Conclusion: the determinism contract (§ Detailed Rules 12) is achievable
as specified, once Fix #5 is applied** (it now is). Before this review, it
was not fully specified from the text alone — the reshuffle list-order gap
meant "byte-identical across any two runs" was true only if both runs
happened to use the same *unstated* convention, not guaranteed by the
document itself.

### Signal List Completeness (Task Focus Item #5) — Required Before Implementation

**Finding (BLOCKING): no signal in the entire catalog carries piece
`color` data, and no signal fires when a piece is placed during Refilling.**
This is the most significant finding of this review. Walked through what
each named downstream consumer (`systems-index.md`: Scoring, Level
Objective, Juice Layer) plausibly needs from the signal catalog alone,
given this document's own **Logic/Presentation Separation Boundary**
(§ Detailed Rules 13): consumers receive "the full, already-resolved
ordered signal stream... in a single synchronous burst" and replay it
**later**, at their own pace — meaning a consumer cannot fall back on a
synchronous `get_piece_at()` query to recover a cell's color at clear-time,
because by the time replay happens, the live board has already moved on to
its final post-move state (or beyond, if the player has already acted
again).

Concretely:
- `match_cleared`'s `cleared_cells` is bare `(row, col)` coordinates, and
  `run_data`'s `Run` structure is explicitly defined as exactly
  `{orientation, length, cells}` (§ Detailed Rules 4) — **no color field
  anywhere.** Level Objective's `collect_color` objective type
  (`level-data-format.md` — one of exactly 2 supported MVP objective types)
  requires "cumulative tiles of `color` cleared... via match or special,"
  which is **not implementable** from this payload as documented.
- This document's own Dependencies table (Special Candies row) states
  Special Candies "subscribes to `match_cleared`... for its own bookkeeping
  (e.g., harvested-ingredient tallies **by color**)" — the document itself
  anticipates a per-color consumption need its own signal payload doesn't
  support. This is internal corroborating evidence, not just an inferred
  need.
- The `piece_id` field is explicitly justified in § Detailed Rules 1 as
  existing "so the Juice Layer can animate one persistent visual object
  through gravity moves" — but no signal ever announces a `piece_id`'s
  creation with its `color`/`special_type`/cell. `board_bootstrapped`'s
  payload (`rows, cols, cell_mask, manifest_index`) carries no per-cell
  piece data either. For **bootstrap specifically** this is recoverable
  (a consumer can synchronously query `get_piece_at()` for all cells
  immediately upon receiving `board_bootstrapped`, since board state is
  stable and no other event can intervene before `Idle`) — but for
  **any piece placed during Refilling mid-cascade**, there is no
  recoverable path: by the time a deferred-replay consumer processes that
  historical moment, the board has already progressed through further
  cascade steps (or the whole move has resolved), and the query would
  return the wrong (later) state.
- Net effect: Juice Layer cannot render correct per-candy sprites/pop VFX
  for anything cleared after the first Matching pass of a move, Scoring
  cannot award (if ever desired) per-color bonuses, and Level Objective
  cannot implement `collect_color` at all — a core, required MVP objective
  type, not a hypothetical future one.

**Recommendation (not fixed here — requires a real payload-shape design
decision, e.g. a parallel `colors: Array[int]` alongside `cleared_cells`, a
`color` field added to `Run`, and/or a new `piece_placed(cell, piece_id,
color, special_type)` signal fired during Refilling): extend the signal
catalog so every signal referencing a cell also carries that cell's
piece-color data at the moment it was true, since deferred/replayed
consumption structurally forecloses recovering it via synchronous query
after the fact.**

### Required Before Implementation

1. **[BLOCKING]** Signal catalog lacks piece-color data on every
   cell-referencing signal (`match_cleared`, `swap_started`,
   `special_spawned`) and lacks any piece-placement signal during
   Refilling. Blocks `collect_color` objective implementation (core MVP
   scope) and correct Juice Layer rendering for anything beyond the first
   cascade step of a move. See Signal List Completeness above.
2. **[BLOCKING]** Seam 3 (`resolve_special_spawns`) signature
   (`Map[cell, special_type]`) provides no mechanism to set/override a
   spawned special's `color`, contradicting this document's own claim
   (§ Detailed Rules 1) that colorless-special support (color bomb) needs
   "no Board Engine change required." As specified, `special-candies.md`
   cannot implement a genuinely colorless color-bomb candy without either
   a Board Engine schema change or accepting a documented behavioral
   compromise (retained original color, with the match-detection
   side-effects that implies). See Extension Seam Walkthrough, item 2.

### Recommended Revisions (Advisory — not fixed directly, requires judgment)

1. **[Cross-doc]** `rng-service.md`'s Dependencies table should be updated
   to reflect that Board Engine uses `shuffle("board-refill", ...)`
   directly for reshuffle, not `fork_stream()` as currently anticipated
   there. (Not fixed here — out of this review's file-edit scope.)
2. **[Seam contract clarity]** State explicitly what `swap_anchor_cells`
   contains for cascade steps 2+ (most plausibly empty/absent once the
   triggering swap's cells are no longer relevant).
3. **[Seam contract clarity]** Clarify whether seam 4
   (`expand_special_chain_reaction`) is expected to internally recurse to a
   fixpoint within its single per-step call, or whether Board Engine is
   expected to call it repeatedly until the result stabilizes. Both are
   workable; the document should say which.
4. **[Forward-looking, not urgent]** More complex future combo types (e.g.,
   a color-bomb + striped "transform every candy of a color into a striped
   candy, then detonate them all" combo, common in genre convention) would
   need seam 2 or seam 4 to express a **spawn+clear composite**, not just a
   pure clear set. Current seam 2's `Set[cell]` return can't express "clear
   this AND spawn a special there instead." Not blocking for MVP's
   validated scope (simple stripe/bomb + basic combos), but worth a note
   for `special-candies.md` authors if genre-standard combo depth is
   pursued later.

### Nice-to-Have

- The Level Manifest ownership resolution (§ Detailed Rules 2) is a
  genuinely strong piece of cross-document handoff design — it closes a
  real gap both `rng-service.md` and its own review log correctly
  identified but explicitly deferred, and does so with a clean,
  append-only, single-owner model consistent with `rng-service.md`'s own
  Stream Registry philosophy. Worth using as a template for how future
  cross-document handoffs get resolved in this project.
- The Formula 4 touch-target proof (once the wording fix above lands) is a
  genuinely complete proof, not a spot check — a good example of closing
  an advisory item from two upstream review logs with real rigor rather
  than a hand-wave.

### Verdict: NEEDS REVISION

Blocking items: 2 | Recommended (advisory): 4 | Nice-to-have: 2
Mechanical fixes applied in this pass: 6 (Formula 6 arithmetic, Formula 7
range, Formula 4 wording, 4 stale move_limit cross-references treated as
one coordinated fix, reshuffle determinism ordering)
Prior verdict resolved: First review (N/A)

**Summary**: This is a rigorous, largely implementation-ready document —
every formula checks out after two small arithmetic corrections, the
determinism contract is sound once one ordering gap is closed, and the
cross-references to all three Foundation GDDs are accurate (the one stale
discrepancy found was already resolved on the other document's side and
just needed reconciling here). The two blocking items are both genuine and
well-scoped, not indicative of a deeper architectural problem: the signal
catalog needs piece-color data added to its cell-referencing payloads
(affects three downstream systems and is required for a core MVP objective
type), and seam 3's return signature needs a color-override mechanism (or
an explicit documented alternative) to deliver on this document's own
promise that color-bomb support requires no Board Engine schema change.
Both are additive fixes to existing structures, not a redesign of the
resolution loop, the four-seam model, or the determinism architecture —
all of which hold up well under adversarial recomputation.

---

## Re-review (Revision 2) — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review` focused re-review (targeted verification of
Revision 2's changes only, not a full re-read). **Reviewer**: game-designer
(self-authored analysis; no specialist subagents spawned for this focused
pass). **Re-review**: Yes — prior verdict was NEEDS REVISION on 2026-07-18
(2 blocking, 4 advisory).

### Item-by-item verification

1. **Blocking 1 (color data in event stream) — RESOLVED, verified.**
   Every piece-reporting signal now carries a `PieceSnapshot = {cell, color,
   special_type}` or equivalent inline fields: `swap_started`
   (`piece_a`/`piece_b`), `special_activated` (`piece_a`/`piece_b`/
   `cleared_pieces`), `match_cleared` (`cleared_pieces`), `special_spawned`
   (`cell`, `special_type`, `color`), and the new `pieces_spawned`
   (`pieces: Array[PieceSnapshot], source: enum{BOOTSTRAP, CASCADE_REFILL}`)
   covering bootstrap fill and every `Refilling` state. `swap_rejected`/
   `swap_accepted` correctly remain bare-cell — justified (no piece
   cleared/spawned/placed by those events). Recomputed the new worked
   2-step-cascade walkthrough (§ Detailed Rules 13) cell-by-cell against the
   stated pre-/post-swap board: the clear counts (3 + 3 = 6 red), the
   `pieces_spawned` refill contents, `chain_index` progression, and the
   final `cascade_ended(total_cells_cleared=6)` all check out arithmetically.
   **One data error found and fixed**: the walkthrough's `swap_started`
   line had `piece_a`/`piece_b` colors reversed relative to the stated
   pre-swap board (`piece_a` at cell `(1,4)` should snapshot red(0), not
   green(2); `piece_b` at `(2,4)` should snapshot green(2), not red(0)) —
   corrected in place, consistent with the document's own stated rule that
   `PieceSnapshot` captures pre-swap state. Per-color tallies are fully
   derivable from the event stream alone, satisfying the deferred-replay
   guarantee.
2. **Blocking 2 (seam 3 color override) — RESOLVED, verified.** Seam 3's
   signature is consistently `resolve_special_spawns(runs,
   swap_anchor_cells) -> Map[cell, SpecialSpawn]` with `SpecialSpawn =
   {special_type: int, color: int | null}` everywhere it is referenced: the
   seam table, the no-op default ("Returns an empty map"), § Detailed Rules
   1's colorless-support claim, the worked example (striped + color-bomb
   cases), the Edge Cases table's malformed-color fallback row, and the
   Acceptance Criteria (`test_seam_3_color_override_null_produces_colorless_piece`,
   `test_seam_3_color_override_explicit_value_produces_colored_piece`,
   `test_seam_3_invalid_color_falls_back_to_run_color`). Grepped for the old
   `Map[cell, special_type]` shape — zero stragglers found.
3. **Advisories — all four confirmed correctly resolved.**
   - `Run` now carries `color` directly (§ Detailed Rules 4); grepped for
     the old `{orientation, length, cells}` shape (no `color`) — zero
     stragglers.
   - `swap_anchor_cells` for cascade steps 2+ is explicitly pinned to the
     empty set (§ Detailed Rules 3), with a matching Acceptance Criterion
     (`test_swap_anchor_cells_empty_for_cascade_steps_beyond_first`).
   - Seam 4 calling semantics are fully specified: Board Engine owns the
     fixpoint loop, seam 4 performs one single-pass expansion per call,
     iteration is capped by `MAX_CHAIN_EXPANSION_ITERATIONS` — cross-checked
     against Formula 6's sibling-cap framing and the corresponding
     Acceptance Criteria. Consistent throughout.
   - The spawn+clear composite-combo item is logged in Open Questions with
     a concrete v2 step-plan sketch (`{spawns, extra_clears, transforms}`),
     correctly scoped as "not built until `special-candies.md` demands it."
   - Revision 2 changelog (document header) accurately describes all of the
     above; no overstated or understated claims found.
4. **Coherence sweep — 3 additional mechanical stragglers found and fixed
   directly (fix policy: mechanical, not design inadequacies):**
   - Formula 5's worked example referenced the pre-revision field name
     `cleared_cells` twice ("could... carry in `cleared_cells`", "emitting
     the correct, complete `cleared_cells` list") — corrected to
     `cleared_pieces` to match the current `match_cleared` payload.
   - The Match Detection Acceptance Criteria's
     `test_l_shape_intersection_unions_into_one_clear_set` referenced
     `cleared_cells` — corrected to `cleared_pieces`.
   - The `board_reshuffled` signal catalog row states its piece-reassignment
     scoping decision is "deliberately out of this revision's scope; see
     Open Questions," but no matching Open Questions entry existed — this
     was a dangling cross-reference. Added an Open Questions row
     documenting the deferred question (whether `board_reshuffled` needs
     per-cell reassignment data for deferred-replay parity with the other
     Revision 2 signals), owned by `systems-designer`, to be revisited at
     `juice-layer.md` authoring. This does not reopen Blocking 1 — the
     document's own scoping rationale (reshuffle is a rare, non-move,
     synchronously-recoverable board correction, unlike mid-cascade
     clears/spawns) is sound and unchanged; only the broken pointer to a
     nonexistent Open Questions entry was fixed.
   - `rng-service.md`'s Board Engine Dependencies row was independently
     re-verified: it states Board Engine "Consumes `board-refill` stream...
     AND for the 'no valid moves' reshuffle (direct draws with pinned
     row-major traversal — `board-engine.md` § Detailed Rules 11);
     `fork_stream()` is available but not currently used," which matches
     `board-engine.md` § Detailed Rules 11 exactly (`shuffle("board-refill",
     list)` over a row-major-collected list, no `fork_stream()` call). No
     discrepancy remains — this was already corrected on `rng-service.md`'s
     side before this re-review and required no further action.

### Fixes applied this pass (all mechanical, per fix policy)

1. `board-engine.md` Formula 5 worked example: `cleared_cells` → `cleared_pieces` (2 occurrences).
2. `board-engine.md` Acceptance Criteria (`test_l_shape_intersection_unions_into_one_clear_set`): `cleared_cells` → `cleared_pieces`.
3. `board-engine.md` § Detailed Rules 13 worked walkthrough: `swap_started` `piece_a`/`piece_b` color values corrected (were reversed relative to the stated pre-swap board).
4. `board-engine.md` Open Questions: added a row for `board_reshuffled`'s payload-sufficiency scoping decision, resolving the dangling "see Open Questions" cross-reference in the signal catalog.
5. `board-engine.md` header `*Status:*` line updated to `Reviewed — APPROVED (re-review, 2026-07-18)`.

### Verdict: APPROVED

Blocking items: 0 (both prior blockers confirmed genuinely resolved) |
Advisory items outstanding: 0 (all four confirmed resolved; one new
low-priority Open Questions item logged, not blocking) | Mechanical fixes
applied this pass: 4
Prior verdict resolved: Yes — both Blocking 1 and Blocking 2 from the
2026-07-18 first review are confirmed resolved with no remaining gaps; the
Revision 2 changelog's claims match the document's actual content
throughout.

**Summary**: Revision 2 correctly and completely resolves both blocking
findings from the first review — the signal catalog now carries full piece
identity on every cell-referencing event (enabling `collect_color` and
deferred-replay rendering), and seam 3's `SpecialSpawn.color` override
delivers genuinely colorless color-bomb support with no schema-change caveat
needed. All four advisory items are also cleanly resolved with matching
Acceptance Criteria. This re-review's coherence sweep caught four residual
mechanical stragglers (two stale field-name references, one worked-example
data-reversal error, one dangling cross-reference) — all fixed in place;
none indicate a deeper design gap. The document is implementation-ready.
