# Design Review Log: Level Data Format

Target document: `design/gdd/level-data-format.md`

---

## Review — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review --depth lean`, autonomous remote session (no specialist
agent spawning, no AskUserQuestion, no approval waits).
**Reviewer**: game-designer (self-authored analysis; no `systems-designer`,
`qa-lead`, or `creative-director` subagents spawned — lean mode).
**Re-review**: No — first review.

### Completeness: 8/8 sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present, plus two well-justified
extra sections (Cross-References, Open Questions) that exceed the minimum
standard and materially help downstream authors.

### Dependency Graph

Declared "Depends On": none (Foundation layer — soft co-design dependency
only, per `systems-index.md`'s Circular Dependencies note).

Declared as depended-on-by / referenced:
- ✓ `design/gdd/rng-service.md` — exists (this review batch).
- ✗ `design/gdd/board-engine.md` — NOT FOUND (correctly flagged "not yet
  written").
- ✗ `design/gdd/special-candies.md` — NOT FOUND (correctly flagged, soft
  co-design).
- ✗ `design/gdd/scoring-stars.md` — NOT FOUND (correctly flagged, provisional
  constant ownership).
- ✗ `design/gdd/level-objectives.md` — NOT FOUND (correctly flagged).
- ✗ `design/gdd/world-map.md` — NOT FOUND (correctly flagged).
- ✗ `design/gdd/booster-brewing.md` — NOT FOUND (correctly flagged, Phase 2).
- ✗ `design/gdd/events-theming.md` — NOT FOUND (correctly flagged, Phase 3).
- ✓ `design/art/art-bible.md` — exists; cited correctly for the Base Candy
  Roster.
- ✓ `prototypes/sweet-cascade-concept/REPORT.md` — exists; cited correctly
  for reference-level tuning values.

No broken references — every unwritten GDD is honestly labeled.

### Fixes Applied Directly (mechanical reconciliation)

1. **Stale cross-reference** — Dependencies table's RNG Service row said
   "(#1, not yet written)." RNG Service is now authored (this review batch).
   Updated the row and added the missing detail that `level_id` (a String
   here) must be resolved to an integer ordinal via a level manifest before
   reaching RNG Service — this document only defines the field shape, not
   the resolution mechanism (expected owner: Match-3 Board Engine, per
   rng-service.md's caller model).
2. **Stale cross-reference** — Cross-References table's `rng_seed` row said
   `design/gdd/rng-service.md *(not yet written)*`. Fixed to point at the
   now-existing document and its actual formulas (F1–F3), and added the
   `level_id` resolution requirement to the same row for completeness.
3. **Open Questions tracking row added** — logged the `rng_seed` /
   Phase-3 production-seed-entry-point composition gap (see Advisory items
   below) as a new tracked question, consistent with this document's
   existing pattern of tracking forward-looking schema questions.
4. **Status header** updated to reflect this review's verdict.

### Cross-System Consistency — Flagged Items Adjudicated

**Item: `level_id` integer assumption (rng-service.md) vs. actual String ID scheme (this document).**
Confirmed: `level_id` here is a String, format `<region_code>-<3-digit-sequence>`.
This is exactly the case rng-service.md's §3/Edge Cases already anticipate
and handle correctly (resolve via a level manifest to a stable integer
ordinal, never via runtime string-hashing). **Reconciled** — fixed via the
cross-reference edits above. One item remains genuinely open and is logged
as advisory: *who* owns the level manifest itself (this document doesn't
define one; the natural owner given rng-service.md's caller model is
Match-3 Board Engine, not yet authored).

**Item: `rng_seed` field vs. RNG Service's seed lifecycle (level / daily / test entry points) — do they compose cleanly?**
**They do not currently compose cleanly, and this is a real gap** (not fixed
directly — requires a design decision). This document's `rng_seed` field
says: "`-1` = no fixed seed... Any value `>= 0` fixes the level's entire RNG
stream... reserved for Phase 3 daily-challenge/event levels." But
rng-service.md's API surface has exactly three entry points:
`start_level_session(level_id, attempt_number)` (derives seed via F1, no
literal-seed parameter), `start_daily_session(daily_challenge_id,
calendar_date_utc)` (derives seed via F2 from challenge+date, does not read
a per-level field at all), and `start_test_session(master_seed)` (accepts a
literal seed, but is explicitly documented as "Test/debug entry point...
for gdUnit4 seed injection," not a production path). There is currently no
documented way for a production caller to say "load this level's authored
`rng_seed` and use it as the literal `master_seed`" without either (a)
misusing the test-only entry point in production, losing the
`level_id`/`attempt_number` bug-repro logging fields it doesn't take, or (b)
RNG Service adding a fourth entry point. **Severity: Advisory, not
blocking** — the field is unused at MVP/launch (`rng_seed = -1` on all 10
launch levels, confirmed by this doc's own Edge Cases table and validated by
Acceptance Criteria), so no MVP implementation work is affected. Flagged
clearly so it isn't rediscovered mid-Phase-3 after content has already been
authored against an unusable seed field. Logged as a new Open Questions row
in this document (see Fixes Applied above).

**Item: touch-input's 44px floor vs. this document's 9×9 max board dimensions — is the math compatible in portrait?**
No document currently *proves* this. I ran a bounding calculation using
numbers already present in both docs: touch-input.md's own Formula 1 worked
example assumes a "1080px-wide reference canvas" for an 8×8 board at
`cell_size_px ≈ 130` (i.e., roughly full-width usage minus small margins —
`1080/8 ≈ 135`, close to the stated 130). Extending that to the schema's
maximum `grid_width = 9`: `1080/9 ≈ 120px` per cell on width alone —
comfortably above the 44px floor. The tighter dimension is height, since
touch-input.md notes the art bible places the board in "the middle third of
the portrait screen." Even a conservative ~700–800px vertical allowance for
a 9-row-tall board yields `≈78–89px` per cell — still comfortably above
44px. **Conclusion: very likely compatible on realistic target devices, but
this is my own back-of-envelope check, not a formally specified proof
anywhere in either document.** V5 (grid dimension bounds, 3–9) does not
cross-check against `MIN_TOUCH_TARGET_PX` at all, and touch-input.md's
Formula 3 only asserts the floor at runtime via a debug-build assertion —
neither document guarantees in advance that every schema-legal board size
is safe on the smallest supported viewport. **Severity: Advisory.**
Recommend `board-engine.md` (not yet authored, owns board scale/layout per
touch-input.md's own deferral) include an explicit reference-viewport
calculation proving the full V5 range stays above 44px on the smallest
officially supported device width before Vertical Slice.

**Item: Star-threshold advisory rule V18's `REFERENCE_SCORE_PER_MOVE` constant vs. RNG/scoring assumptions.**
V18 and Formula A are already correctly scoped as Advisory and explicitly
provisional pending Scoring & Star Thresholds (#6). Recomputed both worked
examples by hand — both check out exactly (`reference_max_score = 25 × 160
= 4,000`; the star-threshold chain `2,500 ≤ 2,500 < 3,200 < 3,900 ≤ 4,000`
all hold). The deeper consistency question: `REFERENCE_SCORE_PER_MOVE = 160`
is a **flat constant**, calibrated against one specific prototype
configuration (8×8 board, 5-color `color_pool`, 25 moves). RNG Service's own
Tuning Knobs rationale states that a smaller `color_pool` materially
increases match/cascade frequency ("Fewer colors = near-guaranteed matches
on almost every swap... cascades feel automatic/unearned"). Since this
document allows `color_pool` to range 3–5 (V11) and doesn't vary
`REFERENCE_SCORE_PER_MOVE` by pool size, a 3-color level's actual achievable
score-per-move is plausibly higher than the 5-color baseline the constant
was derived from — meaning V18's advisory ceiling may be systematically too
tight for lower-color-pool (typically earlier/easier onboarding) levels.
**Severity: Advisory** (V18 is non-blocking by design, and the constant is
explicitly provisional). Recommend Scoring & Star Thresholds (#6), when
authored, either parameterize the reference rate by `color_pool` size or
explicitly document why a flat constant remains acceptable.

### Formula Verification (worked examples recomputed by hand)

- **Formula A** (`move_limit = 25, REFERENCE_SCORE_PER_MOVE = 160`):
  `reference_max_score = 4,000` ✓ matches doc; cross-checks reasonably
  against the prototype's actual greedy-bot score of 3,960 (158.4 pts/move,
  rounded up to 160 per the doc's own stated rationale).
- **Formula B** (reference level: `score_target=2500, star_1=2500,
  star_2=3200, star_3=3900, reference_max_score=4000`): all four boolean
  clauses evaluate `true` ✓ matches doc, including the intentionally tight
  100-point (2.5%) margin between `star_3_score` and `reference_max_score`.

No arithmetic errors found.

### Required Before Implementation

None. All findings are advisory; MVP scope (10 levels, all `rng_seed = -1`)
is fully unaffected by every open item above.

### Recommended Revisions (Advisory — not fixed directly, requires judgment)

1. **[Design decision]** Resolve the `rng_seed` / RNG Service production
   entry-point composition gap before Phase 3 (Events/Theming Engine or any
   daily-challenge/event level) authoring begins. See adjudication above;
   tracked in this document's Open Questions table.
2. **[Design decision]** Decide whether `REFERENCE_SCORE_PER_MOVE` (or its
   Scoring & Star Thresholds successor) should vary by `color_pool` size.
3. **[Implementability gap]** `REFERENCE_SCORE_PER_MOVE`'s Tuning Knobs row
   calls it a "tuning constant" but doesn't state where it's stored as data
   (per `coding-standards.md`'s no-hardcoded-values rule). Recommend
   specifying an `assets/data/` location (or explicit ownership by the
   level-validation test script) once Scoring & Star Thresholds formalizes
   it.
4. **[Verification gap, shared with touch-input.md]** No document proves
   the full `grid_width`/`grid_height` range (3–9, V5) stays above
   `MIN_TOUCH_TARGET_PX` (44px) on the smallest supported viewport.
   Recommend `board-engine.md` close this gap explicitly.

### Nice-to-Have

- The Open Questions table is a genuinely good practice — recommend other
  Foundation/Core GDDs (starting with `board-engine.md`) adopt the same
  pattern for tracking forward-looking, not-yet-decided items instead of
  letting them live only in review logs.

### Senior Verdict [game-designer, lean-mode self-review]

This is the most structurally load-bearing of the three Foundation docs —
it's the shared contract seven other systems will eventually extend — and it
handles that responsibility well. The versioning policy (optional field vs.
closed-enum vs. breaking change) is exactly the right level of precision for
a schema multiple future authors will touch, and the validation table
(V1–V19) is implementable as written, with Blocking/Advisory severity
correctly assigned throughout (V18 in particular is a good example of
"flag it, don't gate on it" applied appropriately to a genuinely provisional
constant). The two stale cross-references were straightforward staleness
from parallel authoring within the same batch, now fixed. The `rng_seed`
composition gap and the color-pool-vs-reference-score question are the two
findings worth real attention, but both are correctly scoped as Phase 3/
post-MVP concerns that don't block this document's approval or MVP
implementation.

### Scope Signal

Rough scope signal: **L** (only 2 formulas, but this is the central shared
schema for 7+ future systems, requires a versioned migration policy, and
several of its provisional constants will need reconciliation with not-yet-
written GDDs — multi-system integration risk outweighs the low formula
count). Producer should verify before sprint planning.

### Verdict: APPROVED

Blocking items: 0 | Recommended (advisory): 4 | Nice-to-have: 1
Prior verdict resolved: First review (N/A)
