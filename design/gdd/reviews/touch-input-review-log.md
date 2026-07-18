# Design Review Log: Touch & Input System

Target document: `design/gdd/touch-input.md`

---

## Review — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review --depth lean`, autonomous remote session (no specialist
agent spawning, no AskUserQuestion, no approval waits).
**Reviewer**: game-designer (self-authored analysis; no `systems-designer`,
`qa-lead`, or `creative-director` subagents spawned — lean mode).
**Re-review**: No — first review.

### Completeness: 8/8 sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present. Player Fantasy includes an
unusually rigorous latency-budget table tying every gesture-classification
step to a frame-count guarantee, which is a strong implementability signal.

### Dependency Graph

Declared "Depends On" (header): none (Foundation layer, no prerequisite
GDDs — i.e., no other GDD needed to *exist first* for this one to be
authored).

Declared in the Dependencies section (fuller relationship graph, both
inbound data and outbound consumers):
- ✗ `design/gdd/board-engine.md` — NOT FOUND (correctly flagged "not yet
  authored"; this document is correctly authored ahead of its primary
  consumer).
- ✓ `design/gdd/level-data-format.md` — exists (this review batch).
- ✗ `design/gdd/screen-flow.md` — NOT FOUND (correctly flagged "not yet
  authored," soft runtime dependency).
- ✓ `design/art/art-bible.md` — exists; cited correctly for
  `visual_fill_ratio` (0.84).
- ✓ `.claude/docs/technical-preferences.md` — exists; cited correctly for
  `MIN_TOUCH_TARGET_PX` (44px) and the no-hover-only-interactions rule.

No broken references — every unwritten GDD is honestly labeled, and the
header's "no prerequisite GDDs" claim is consistent with the body's fuller
Dependencies section once read as "authoring order" vs. "full relationship
graph" (the same convention rng-service.md uses for its own forward
dependents).

### Fixes Applied Directly (mechanical reconciliation)

1. **Stale cross-reference** — Dependencies table's Level Data Format row
   said "`design/gdd/level-data-format.md`, not yet authored." Level Data
   Format is now authored (this review batch); removed the stale
   parenthetical.
2. **Implementability gap, same edit** — the same row referred to "board
   grid dimensions (`rows`, `cols`)" without ever stating how those map to
   Level Data Format's actual schema field names (`grid_width`,
   `grid_height`). Added the explicit mapping: `grid_height → board_rows`,
   `grid_width → board_cols` (consistent with this document's own §1
   row-major convention: row index increases downward, column index
   increases rightward).
3. **Status header** updated to reflect this review's verdict.

### Cross-System Consistency — Flagged Items Adjudicated

**Item: touch-input's cell-pitch/44px floor vs. level-data-format's board dimensions (9×9 max) on small phones — is the math compatible in portrait?**
No document currently *proves* this end-to-end; full write-up (including the
bounding calculation) lives in Level Data Format's review log, since the
schema's dimension range (V5, 3–9) is the input side of the question and
this document owns the output side (the 44px floor, Formula 3). Summary of
that calculation: using this document's own Formula 1 worked-example
assumption (an 8×8 board at `cell_size_px ≈ 130` on a "1080px-wide reference
canvas"), a 9-wide board yields `≈120px` cells on width alone, and even a
conservative "middle third" vertical allowance yields `≈78–89px` cells for a
9-tall board — both comfortably above the 44px floor on realistic target
devices. **Severity: Advisory, not blocking** — this document's own Rule 5 /
Formula 3 already includes a debug-build runtime assertion as a safety net,
and correctly assigns the *fix* (board scale/layout correction) to
`board-engine.md`, not to itself. The gap is that no document yet contains
the actual reference-viewport proof; this document correctly identifies
whose job that proof is (Match-3 Board Engine) but that GDD doesn't exist
yet. Recommend `board-engine.md` close this explicitly at authoring time,
before Vertical Slice.

**Item: other three flagged items (rng-service `level_id` type, `rng_seed` lifecycle composition, V18's `REFERENCE_SCORE_PER_MOVE`).**
Not applicable to this document — it has no seed, RNG, or scoring
surface. Fully adjudicated in `rng-service.md`'s and `level-data-format.md`'s
review logs.

### Formula Verification (worked examples recomputed by hand)

- **Formula 1** (`cell_size_px=130, threshold_ratio=0.35`): raw threshold
  `= 45.5px`; clamped to `[24, 60]` → unchanged (`45.5` already inside
  range) ✓ matches doc.
- **Formula 2** (origin `(3,4)`, drag from `(500,800)` to `(475,840)`):
  `dx=-25, dy=40`; `distance = √(625+1600) = √2225 ≈ 47.17` ✓ matches doc's
  `≈47.2` (and correctly exceeds the `45.5px` threshold from Formula 1);
  `|dx|=25 < |dy|=40` → `axis=vertical`; `delta_row=+1, delta_col=0`;
  `target=(4,4)` ✓ matches doc, correctly reading as "one row down."
- **Formula 3** (`cell_size_px=130, visual_fill_ratio=0.84`):
  `visual_size_px = 109.2px`; `hit_expansion_px = 20.8px`
  (`≈10.4px` per side) ✓ matches doc; constraint `130 ≥ 44` holds with
  headroom, consistent with the doc's own stated conclusion.

No arithmetic errors found. All three formulas are free of division
operators, so there is no divide-by-zero risk at any documented boundary
(including `cell_size_px → 0`, which is excluded by the `cell_size_px > 0`
precondition anyway).

### Required Before Implementation

None. All findings are advisory.

### Recommended Revisions (Advisory — not fixed directly, requires judgment)

1. **[Test coverage]** No Acceptance Criteria unit test pins the
   `AwaitingSecond(A)` → "swipe from any cell C (≠ A) to a valid neighbor D"
   transition, which is the one state-machine row that emits **two**
   intents in a defined order (`cancel()`, then `swap_request(C, D)`).
   Every other transition in §2's table has a corresponding dedicated AC
   bullet; this dual-intent, order-sensitive path does not. Recommend
   adding one, given this row is explicitly called out in the doc's own
   prose as "a deliberate production improvement over the concept
   prototype" — exactly the kind of behavior most worth regression-pinning.
2. **[Verification gap, shared with level-data-format.md]** No document
   proves the schema's full grid-dimension range (3–9) stays above the 44px
   floor on the smallest supported viewport. See adjudication above;
   recommend `board-engine.md` close this at authoring time.

### Nice-to-Have

- Stylistic note (shared with rng-service.md's log): this document frames
  its Level-Data-Format-derived inputs (grid dimensions) as "This depends on
  it (data only)," while rng-service.md frames structurally similar
  Level-Data-Format-derived inputs (`level_id`, `active_colors`) as
  explicitly "Not a dependency... opaque caller-supplied parameter." Both
  framings are self-consistent given each document's own header scoping
  (authoring-order dependency vs. full relationship graph), so this is not
  a defect — just a note that a shared house convention across Foundation
  docs would read more uniformly to a new team member skimming both files
  back to back.

### Senior Verdict [game-designer, lean-mode self-review]

This is a tight, implementation-ready spec. The gesture grammar, state
machine, and three formulas are unambiguous, the latency-budget table in
Player Fantasy is an unusually strong translation of "feel" into testable
frame-count guarantees, and the accidental-touch-rejection and
input-locking-during-cascade rules both show real attention to mobile
touch-input failure modes (OS-level touch cancellation, simultaneous
touch points, modal interruption) that are easy to hand-wave in a first
draft and weren't here. The `input_buffer_depth` tuning knob is a good
example of shipping the safe default (drop-while-busy, matching the
validated prototype) while explicitly reserving an experimental variant for
future playtesting rather than silently picking one. The two advisory items
found (missing AC test for the dual-intent path, and the not-yet-proven
44px/9×9 compatibility) are both real but neither blocks approval or MVP
implementation — the second is explicitly deferred to a not-yet-authored
document by this GDD's own design, which is the correct call.

### Scope Signal

Rough scope signal: **M** (self-contained gesture state machine, 3 formulas,
2 hard GDD dependencies — one inbound data dependency on Level Data Format,
one outbound consumer in Board Engine — bounded implementation surface with
low cross-system integration risk).

### Verdict: APPROVED

Blocking items: 0 | Recommended (advisory): 2 | Nice-to-have: 1
Prior verdict resolved: First review (N/A)
