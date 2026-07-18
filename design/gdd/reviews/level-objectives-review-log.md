# Design Review Log: Level Objective & Move-Limit System

Target document: `design/gdd/level-objectives.md`

---

## Review — 2026-07-18 — Verdict: NEEDS REVISION

**Mode**: `/design-review --depth lean`, autonomous, run jointly across three
tightly-coupled Feature-layer GDDs (`special-candies.md`, `scoring-stars.md`,
`level-objectives.md`) with a focused seam-handshake and formula-recomputation
pass. No specialist agents spawned (lean mode), no `AskUserQuestion`.
**Reviewer**: game-designer (self-authored analysis).
**Re-review**: No — first review.

### Completeness: 8/8 required sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present, plus Cross-References and
Open Questions.

### Dependency Graph

Declared "Depends On": Match-3 Board Engine (APPROVED — Revision 2), Level
Data Format (APPROVED), Scoring & Star Thresholds (Draft — reviewed in this
same pass, see its own log, verdict NEEDS REVISION). All three ✓ exist.

Declared as depended-on-by / referenced: Game UI/Screens Flow (✓ exists,
Draft), Booster Brewing Meta (correctly not yet authored), Special Candies &
Combo Matrix (✓ exists, this pass verdicts it APPROVED — correctly declared
as **zero direct coupling**, verified accurate), RNG Service (✓ exists,
APPROVED — correctly declared "No dependency"). No broken references.

### Formula Verification (worked examples recomputed by hand)

All six formulas recomputed independently:

- **Formula 1** (progress fraction) — `23/20=1.15 → clamp=1.0` ✓.
- **Formula 2** (completion predicate) — both worked examples (`2500≥2500=
  true`; `18≥20=false`) ✓.
- **Formula 3** (win evaluation AND) — `true AND false = false`, then
  `true AND true = true` ✓.
- **Formula 4** (outcome precedence) — simultaneous-case worked example
  confirms `WIN` fires unconditionally first; logic table (`WIN`/`LOSE`/
  `null` over all four quadrants) internally consistent ✓.
- **Formula 5** (move consumption) — binary `{0,1}` matrix, all four cases
  (`SWAP_MATCH`, `SPECIAL_ACTIVATION`, rejected, reshuffle) correctly mapped
  against `board-engine.md`'s own cited signal contract ✓.
- **Formula 6** (low-moves warning) — both worked examples (`3≤3 AND true=
  true`; `2≤3 AND NOT true=false`) ✓.

**Zero arithmetic errors found.** This document's formulas are logically
simple (booleans, clamped ratios, a precedence table) and every one checks
out exactly.

### Seam Handshake Audit (Focus Item — BLOCKING FINDING)

**`ScoreProvider.finalize_results(objectives_resolution: ObjectivesResolution)
-> ResultsData` (§ Detailed Rules 9) does not compose with `scoring-stars.md`
as authored.**

This document proposes calling this seam at the resolving `board_stabilized`
and having Scoring "compute and return the fully-populated `ResultsData`...
using `objectives_resolution` as its raw input," with this document then
emitting `level_resolved(outcome, results_data)` using that return value
**verbatim**, "never inspects or depends on `ResultsData`'s internal shape
beyond passing it through." This requires Scoring to own composing the
*complete* record, including `outcome` and the *full* `closest_miss_summary`
(both the score-proximity dimension and any objective-completion dimension).

`scoring-stars.md` — now authored — explicitly declines this role:
- Its § Detailed Rules 8 states outright that it does **not** define the
  full `closest_miss_summary` payload, calling that "Level Objective &
  Move-Limit System's (#7) ownership."
- Its § Detailed Rules 10 `ResultsData` field table lists `outcome` and
  `closest_miss_summary.*` (beyond the two score-progress fields) as
  **not this document's field** — owned by Level Objective.
- `finalize_results()` is never named, implemented, or referenced anywhere
  in `scoring-stars.md`. Its actual model is a pull/compose architecture:
  Level Objective is expected to *read* `score_earned`/`stars_earned`/
  `score_progress_ratio` from Scoring (matching Scoring's own Dependencies
  row: "Expected to read `final_score`/`stars_earned`... via this
  document's Formula 2/7") and assemble the final record itself.

This is the same disagreement as the ownership question this document's own
Open Questions table already flagged: `screen-flow.md` §11 originally
attributes `closest_miss_summary` to Level Objective; this document's Open
Questions table proposes reassigning that ownership to Scoring (via
`finalize_results()`); `scoring-stars.md`, now written, explicitly declines
the reassignment and reaffirms `screen-flow.md`'s original attribution.
**This document's own flagged open question is confirmed live and
unresolved, not settled by Scoring's authoring as hoped.** Logged as
blocking per this review's fix policy — a genuine, unresolved architecture
disagreement, not something this review resolves unilaterally. See
`scoring-stars-review-log.md` for the mirrored, fuller writeup of the
contract-composition failure.

**Recommended reconciliation direction (not applied — for the user/systems-
designer to decide):** drop the proposed `finalize_results()` push seam.
This document already has everything it needs to assemble the complete
`ResultsData`/`closest_miss_summary` itself: its own `ObjectivesResolution`/
`ObjectiveResult` structures (§ Detailed Rules 9) fully capture the
objective-completion dimension, and a narrower Scoring query (e.g.
`ScoreProvider.get_final_results() -> {score_earned, stars_earned,
score_progress_ratio, score_progress_percent}`, mirroring the pattern this
document already proposes for `get_current_score()`) would supply the score
dimension. This document would then remain the sole composer of the final
`ResultsData`, matching `screen-flow.md`'s original (undisputed-until-this-
document) attribution.

**Secondary, non-blocking gap in the same area:** `ScoreProvider.
get_current_score() -> int` (§ Detailed Rules 3) is likewise never formally
named or ratified in `scoring-stars.md`. This is not a contradiction — only
an unconfirmed proposal, since Scoring's internal `final_score` running
total is fully compatible with such a query — but it remains open. Logged as
**advisory**, tracked identically in `scoring-stars-review-log.md`.

### Cross-Doc Consistency Checks (Focus Item)

1. **`collect_color`'s unified counting rule vs. Scoring's identical
   single-signal rule.** Verified this document's § Detailed Rules 4
   independently derives the same "`match_cleared` only, never
   `special_activated`, to avoid double-counting" argument `scoring-stars.md`
   § Detailed Rules 1 states for its own point formula, both citing the same
   Board Engine overlap/union and seam-1-and-match-coexistence rules.
   **Consistent, symmetric, no double-count path in either document.**
2. **`BOOTSTRAP` handling divergence vs. Scoring — verified intentional.**
   This document explicitly **includes** `BOOTSTRAP`-sourced `match_cleared`
   events in `collect_color` tallying, with an explicit edge case ("a
   legitimate, if rare, instant WIN... `moves_used=0`") reasoned through in
   detail; `scoring-stars.md` explicitly **excludes** the same events from
   scoring. Confirmed both documents state their own rationale and neither
   contradicts the other — this is a deliberate, independently-justified
   per-system divergence (genuine tile-removal-counts vs.
   score-inflation-prevention), not an oversight.
3. **Zero coupling to Special Candies, confirmed.** This document's
   Dependencies table states "No direct dependency" to `special-candies.md`
   and never subscribes to `special_activated`. Cross-checked against
   `special-candies.md` § Detailed Rules 9 (Harvest Observation Point) and
   § Detailed Rules 10 (seam signatures unmodified) — confirmed this
   document's zero-coupling claim holds structurally: every cell any combo
   or passive chain clears is already inside the same `match_cleared` this
   document already consumes, with no seam call required.

### Pillar 2 / Nondeterminism Audit (Focus Item)

Every rule (progress tallying, move accounting, win/lose evaluation) is a
pure, deterministic function of already-resolved Board Engine signals and
Scoring's live score. RNG Service is explicitly listed as "No dependency."
The win/lose predicate applies identically to every attempt, including the
Bootstrap-instant-win edge case — explicitly defended as *not* a
special-cased exception, per Pillar 2's "applied transparently and
identically everywhere" standard. **No hidden nondeterminism or invisible
skill-reading found.**

### Fixes Applied Directly

None required — no mechanical errors found in this document (formulas,
citations, and internal cross-references all check out). All fixes from
this joint review pass were applied to `scoring-stars.md` and
`special-candies.md` (see their own logs); this document's only finding is
the shared architectural blocking item above, which is a design decision,
not a mechanical error, per fix policy.

### Required Before Implementation

1. **[BLOCKING]** `ScoreProvider.finalize_results()`'s proposed contract
   does not compose with `scoring-stars.md` as authored — see Seam
   Handshake Audit above. This document's own Open Questions table already
   flagged the underlying ownership question; it is now confirmed
   unresolved by Scoring's authoring, not settled by it.

### Recommended Revisions (Advisory)

1. `ScoreProvider.get_current_score()` remains unconfirmed by
   `scoring-stars.md` — recommend requesting an explicit "Score Query API"
   subsection there (tracked in `scoring-stars-review-log.md`).

### Nice-to-Have

- The `Tracking → Resolved` terminal-state guard (§ Detailed Rules 9) is a
  clean, structurally-enforced idempotency pattern for a one-shot resolution
  event — worth reusing as a template for other systems with a single
  irreversible per-attempt transition.

### Scope Signal

Rough scope signal: **L** (multi-system integration — 3 dependencies feeding
in, 2 forward-declared seams into Scoring, 6 formulas, a forward extension
registry for future objective types; the blocking seam-contract item may
require a small reconciliation note once resolved, not a full ADR).
Producer should verify before sprint planning.

### Verdict: NEEDS REVISION

Blocking items: 1 | Recommended (advisory): 1 | Nice-to-have: 1
Mechanical fixes applied this pass: 0 (none needed in this document)
Prior verdict resolved: First review (N/A)

**Summary**: This document's own logic is exactly correct — every formula
recomputes cleanly, the `match_cleared`-only counting rule is rigorous and
symmetric with Scoring's identical rule, the zero-coupling claim to Special
Candies holds structurally, and the deliberate `BOOTSTRAP`-inclusion
divergence from Scoring's `BOOTSTRAP`-exclusion is well-reasoned and
self-aware on both sides. The one blocking issue is the same architectural
mismatch flagged in `scoring-stars.md`'s review: this document's proposed
`finalize_results()` push seam assumes Scoring will assemble the complete
`ResultsData`/`closest_miss_summary`, but Scoring's own authored document
explicitly declines that role. This document's own Open Questions table
correctly anticipated this exact risk ("pending #7's ratification") — it
is now confirmed live, not resolved, and needs a joint decision before
implementation.
