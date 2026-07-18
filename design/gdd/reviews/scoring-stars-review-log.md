# Design Review Log: Scoring & Star Thresholds

Target document: `design/gdd/scoring-stars.md`

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

Declared "Depends On": Match-3 Board Engine (APPROVED — Revision 2), Special
Candies & Combo Matrix (Draft — this pass verdicts it APPROVED, see its own
log), Level Data Format (APPROVED — v1). All three ✓ exist.

Declared as depended-on-by / referenced: Level Objective & Move-Limit System
(✓ exists, this pass), Juice Layer (✓ exists on disk as Draft — stale
"not yet authored" wording, same pattern flagged in `special-candies.md`'s
log, not re-flagged here), Screen Flow (✓ exists, Draft), World Map (✓
exists, Draft), Booster Brewing Meta (correctly not yet authored), Save &
Persistence (✓ exists, Draft), `game-concept.md` (✓ exists, foundational).
No broken references.

### Formula Verification (worked examples recomputed by hand)

All nine formulas recomputed independently:

- **Formula 1** (chain multiplier) — trivial, correct.
- **Formula 2** (cascade step score) — all three worked examples
  reproduced exactly: A (`60`, `120`, total `180`), B (`220`), C (`1,640`).
- **Formula 3** (derived combo bonus table) — all five values re-derived
  from Formula 2's constants (`60/180/360/240/120`) — confirmed no
  independent second lookup, as claimed.
- **Formula 4** (`REFERENCE_SCORE_PER_MOVE(K)`) — **one genuine arithmetic
  error found and fixed** (see Fixes Applied below). `K=3` and `K=4` rows
  independently re-verified correct (`q²(3)=0.49327` ✓ exact;
  `q²(4)=0.67894` ✓ within normal hand-computation rounding tolerance).
- **Formula 5** (reference max score) — `25×160=4,000` ✓; `25×255=6,375` ✓.
- **Formula 6** (star thresholds) — both worked examples reproduced exactly
  (`2,500/3,200/3,900` and `3,984/5,100/6,216`), including the rounding
  step (`0.625×6,375=3,984.375→3,984`; `0.975×6,375=6,215.625→6,216`).
- **Formula 7** (star evaluation) — boundary-inclusive logic confirmed
  correct at all three thresholds.
- **Formula 8** (closest-miss ratio) — `2,100/2,500=0.84`, `round(84)=84` ✓.
- **Formula 9** (max plausible score bound) — `(20+180)×81×20×21/2 =
  200×81×210 = 3,402,000` ✓ exact; `×99 = 336,798,000` ✓ exact; confirmed
  ~6 orders of magnitude below the 64-bit signed int ceiling.

### Fixes Applied Directly (mechanical, per fix policy)

1. **Formula 4 arithmetic error (K=5 row).** The worked computation stated
   `q²=0.78296 → M=1.27720`. Recomputed by hand three ways (decimal
   expansion of `0.884736²`, exact-fraction `13824²/15625²`, and a
   `(0.88+0.004736)²` binomial expansion) — all three agree on
   `q²=0.782758 (0.78276 to 5 places)`, giving `M=1/0.782758≈1.27753`, not
   `1.27720`. The doc's own reciprocal check confirms the error: `0.78296 ×
   1.27720 ≈ 1.000005` (self-consistent with the wrong intermediate value),
   while the correct pair is `0.78276 × 1.27753 ≈ 1.000000`. **Fixed** `K=5`
   row's `q²` and `M` in place. Because `M(5)` is the anchor denominator for
   the `M(3)/M(5)` and `M(4)/M(5)` ratios, also recomputed and corrected
   those two ratio lines (`1.58733→1.58688`, `1.15325→1.15292`) for internal
   consistency. **No downstream design consequence**: recomputing
   `160×1.58688≈253.9` and `160×1.15292≈184.5` still round to the exact same
   published table (`255/185/160`) — verified by hand — so no other formula,
   worked example, or Tuning Knob value in this document (Formula 5, 6, or
   the L1/K=3 worked examples, all of which use the already-anchored,
   unaffected `REFERENCE_SCORE_PER_MOVE(5)=160` and the final rounded
   `255/185/160` table) requires any further change.

### Seam Handshake Audit (Focus Item — BLOCKING FINDING)

**`ScoreProvider.finalize_results(objectives_resolution: ObjectivesResolution)
-> ResultsData` — the two documents' proposed contracts do not compose.**

`level-objectives.md` § Detailed Rules 9 proposes that, at the resolving
`board_stabilized`, Level Objective calls this seam and Scoring "computes
and returns the fully-populated `ResultsData`... using `objectives_resolution`
as its raw input," with Level Objective then passing that return value
through **verbatim**, never inspecting its shape. This requires Scoring to
be the assembler of the *complete* `ResultsData` record, including
`outcome` and the *full* `closest_miss_summary` (both dimensions — score
proximity and objective-completion proximity).

This document's own § Detailed Rules 8 and 10 explicitly **decline that
responsibility**:
- § Detailed Rules 8: "This document does **not** define the full
  `closest_miss_summary` payload... that remains Level Objective & Move-Limit
  System's (#7) ownership."
- § Detailed Rules 10's `ResultsData` field table lists `outcome` and
  `closest_miss_summary.*` (beyond the score-progress fields) as owned by
  "Level Objective & Move-Limit System (#7)... Not this document's field."

Nowhere in this document is `finalize_results()` implemented, named, or even
referenced — this document's actual model is a **pull/compose** architecture
(Level Objective *reads* `score_earned`/`stars_earned`/`score_progress_ratio`
from Scoring and composes the final `ResultsData` itself), while
`level-objectives.md`'s proposed seam assumes a **push** architecture
(Objective hands its data to Scoring, Scoring returns the finished record).
These are not reconcilable as currently written — a programmer implementing
`finalize_results()` from this document alone would have no function to
call, and a programmer implementing it from `level-objectives.md` alone
would have Scoring silently drop `outcome` and half of
`closest_miss_summary` on the floor.

This is the same underlying disagreement as the `closest_miss_summary`
ownership question: `screen-flow.md` §11's original Declared Seams table
attributes `closest_miss_summary` to Level Objective; `level-objectives.md`'s
Open Questions table recommends correcting that attribution to Scoring;
this document explicitly reaffirms `screen-flow.md`'s original attribution
instead. **Three-way disagreement, one unresolved design decision** — logged
as blocking per this review's fix policy (design decisions are not resolved
unilaterally). See `level-objectives-review-log.md` for the mirrored entry.

**Recommended reconciliation direction (not applied — for the user/systems-
designer to decide):** the architecture this document already assumes
(Objective composes `ResultsData` by reading Scoring's contributed fields)
is more consistent with both documents' actual content than the
`finalize_results()` push model. If adopted, `level-objectives.md` §
Detailed Rules 9 would need to replace `ScoreProvider.finalize_results()`
with a narrower read-only query (e.g., `ScoreProvider.get_final_results() ->
{score_earned, stars_earned, score_progress_ratio, score_progress_percent}`,
mirroring the already-proposed `get_current_score()` pattern), with Level
Objective remaining the sole assembler of the complete `ResultsData` and
`closest_miss_summary`.

**Secondary, non-blocking gap in the same area:** `ScoreProvider.
get_current_score() -> int`, proposed by `level-objectives.md` § Detailed
Rules 3 for continuous `score_target` tracking, is also never named or
ratified anywhere in this document. Unlike `finalize_results()`, nothing in
this document actually *contradicts* it — Scoring's own model (a
continuously-updated internal `final_score` running total) is fully
compatible with exposing such a query, it is simply never formally declared
as an API surface the way Board Engine's Board State Query API is. Logged as
**advisory**: recommend this document add an explicit "Score Query API"
subsection analogous to `board-engine.md`'s precedent, confirming (or
amending) `get_current_score()`'s exact signature.

### Cross-Doc Consistency Checks (Focus Item)

1. **`match_cleared`-only, no double-count.** Verified independently against
   `level-objectives.md` § Detailed Rules 4 — both documents derive the same
   rule from the same Board Engine citations (overlap/union rule,
   seam-1-and-match-coexistence rule), and both correctly exclude
   `special_activated` from their respective counting/scoring logic for the
   identical reason. Consistent.
2. **`BOOTSTRAP` handling divergence — verified intentional, not an error.**
   This document explicitly **excludes** `BOOTSTRAP`-sourced `match_cleared`
   events from scoring (§ Detailed Rules 6); `level-objectives.md`
   explicitly **includes** them in `collect_color` tallying (§ Detailed
   Rules 4). Confirmed this is a deliberate, independently-justified
   divergence in both documents (score-inflation-prevention vs.
   genuine-tile-removal-counts-regardless-of-cause), not an oversight —
   both documents state their own rationale explicitly and neither
   contradicts the other's stated behavior.
3. **Level Data Format V18 / `REFERENCE_SCORE_PER_MOVE` supersession ask.**
   Cross-checked against the current `level-data-format.md`: it still
   carries the provisional flat `REFERENCE_SCORE_PER_MOVE=160` and V18 as
   Advisory, exactly as it said it would until this document was written.
   This document's asks (supersede with the `160/185/255` table; keep V18
   Advisory) are legitimate, correctly-scoped forward recommendations —
   not a contradiction, and appropriately left unedited here (out of this
   document's file-edit scope, matching the precedent set by
   `board-engine.md`'s own review).

### Pillar 2 / Nondeterminism Audit (Focus Item)

Every formula confirmed a pure function of already-resolved Board Engine
signals; RNG Service is explicitly listed as "Not a dependency" with
justification. No player-skill or session-state input anywhere. Star
threshold fractions are fixed, uniform constants applied identically to
every level — no per-level or per-player fudge factor. **Confirmed clean.**

### Required Before Implementation

1. **[BLOCKING]** `ScoreProvider.finalize_results()`'s proposed contract
   with `level-objectives.md` does not compose — see Seam Handshake Audit
   above. Must be resolved (either by Scoring accepting the full assembler
   role and implementing the seam, or by both documents converging on the
   pull/compose model this document already implies) before either document
   is implementation-ready.

### Recommended Revisions (Advisory)

1. Add an explicit "Score Query API" subsection confirming/amending
   `get_current_score()`'s signature (see Seam Handshake Audit).
2. `juice-layer.md` is now Draft, not "not yet authored" — low-priority,
   recommend a batch consistency pass (see `special-candies-review-log.md`).

### Nice-to-Have

- The "derived, not independently authored" framing of Formula 3 (the combo
  bonus table falls out of Formula 2 as a consequence, not a second lookup)
  is a strong anti-drift design pattern, worth reusing elsewhere.

### Scope Signal

Rough scope signal: **L** (multi-system integration — 3 dependencies feeding
in, 2 forward-declared seams into Level Objective, 9 formulas; the blocking
seam-contract item may require a small ADR-adjacent reconciliation note, not
a full ADR). Producer should verify before sprint planning.

### Verdict: NEEDS REVISION

Blocking items: 1 | Recommended (advisory): 2 | Nice-to-have: 1
Mechanical fixes applied this pass: 1 (Formula 4 K=5 arithmetic correction,
propagated to 2 dependent ratio lines; zero downstream design impact)
Prior verdict resolved: First review (N/A)

**Summary**: This document's own formulas are sound — eight of nine formula
blocks were exactly correct on hand-recomputation, and the one arithmetic
slip found (a K=5 intermediate value) does not change any published table
or downstream conclusion. The `match_cleared`-only scoring model is
rigorous and consistent with `level-objectives.md`'s independently-derived
identical rule. The one blocking issue is architectural, not mathematical:
this document and `level-objectives.md` propose incompatible ownership
models for `ResultsData`/`closest_miss_summary` construction, centered on a
`finalize_results()` seam this document never implements or acknowledges.
This must be reconciled — likely in Scoring's favor of the pull/compose
model it already assumes — before implementation begins.

---

## Re-review (Revision 2) — 2026-07-18

**Scope**: Focused re-review of the Revision 2 seam reconciliation between
`scoring-stars.md` and `level-objectives.md`, verifying resolution of the
single shared blocking item from the 2026-07-18 review (the
`ScoreProvider.finalize_results()` push seam did not compose across the two
documents). This is not a full re-review of every section/formula — see the
entry above for the complete first-pass analysis, which remains valid for
everything outside the reconciled seam.
**Reviewer**: game-designer (self-authored analysis), user-directed re-review.
**Prior verdict**: NEEDS REVISION (2026-07-18) — 1 blocking item (seam-composition
mismatch), 2 recommended (advisory), 1 nice-to-have.

### Verification Items

1. **Push seam fully retired as a live contract.** VERIFIED. `finalize_results`
   appears in this document exactly three times: the Revision 2 changelog
   (historical — "dropped the proposed ... push seam"), § Detailed Rules 10a's
   "replacing the previously-proposed `ScoreProvider.finalize_results(...)`"
   framing (historical, describing what the ratified seam replaces), and the
   deliberate negative-test acceptance criterion
   `test_scoring_never_receives_objectives_resolution` (confirms
   `finalize_results()` does not exist in implementation). No live contract,
   no implementation reference, no acceptance criterion exercises it as a real
   seam. **PASS**.
2. **Converged pull contract identical in both docs, field-by-field.** VERIFIED.
   `get_current_score() -> int` and `get_score_results() -> ScoreResults` are
   declared in this document's § Detailed Rules 10a with the exact field list
   `{final_score: int, stars_earned: int, score_progress_ratio: float,
   score_progress_percent: int}`. `level-objectives.md` § Detailed Rules 3
   consumes `get_current_score()` with matching name/signature/"live, never
   cached" semantics; § Detailed Rules 9 consumes `get_score_results()` with
   matching name/signature and "called exactly once, at the resolving
   `board_stabilized`" timing (this document's own contract permits "any time
   at or after," so `level-objectives.md`'s narrower actual call site is
   compatible, not contradictory). Field-by-field, `level-objectives.md`'s
   assembled `ResultsData` consumes all four `ScoreResults` fields exactly
   once each (`final_score→score_earned`, `stars_earned→stars_earned`,
   `score_progress_ratio`/`score_progress_percent→closest_miss_summary.*`) —
   no phantom fields, none dropped. The resulting `ResultsData` outer shape
   (`level_id`, `outcome`, `score_earned`, `stars_earned`,
   `closest_miss_summary`) matches `screen-flow.md` §11's declared seam table
   exactly (5 fields, same names, same types). **PASS**.
3. **`closest_miss_summary` ownership consistency.** VERIFIED. This document's
   § Detailed Rules 8 and its Dependencies row for Game UI/Screens Flow both
   attribute full `closest_miss_summary` assembly to Level Objective & Move-Limit
   System (#7); `level-objectives.md` § Detailed Rules 9 and its Cross-References
   table confirm the same. Cross-checked directly against `screen-flow.md`
   §11's Declared Seams table (line 364): `closest_miss_summary`'s seam owner
   is listed as "Level Objective & Move-Limit System (#7)" — unchanged and
   original, no correction was ever needed there. All three documents agree.
   **PASS**.
4. **Revision 2 changelog and Open Questions resolutions match what's on disk.**
   VERIFIED. This document's changelog (lines 12–21) claims: seam dropped,
   pull contract ratified in § Detailed Rules 10a, Level Objective is sole
   assembler — all three claims independently confirmed by direct inspection
   of § Detailed Rules 10a and § Detailed Rules 8/10. The Open Questions row
   on `closest_miss_summary`'s final shape is correctly marked "Partially
   resolved" (ownership settled; the objective-completion dimension's exact
   shape remains open, correctly deferred to `level-objectives.md`'s own Open
   Questions) rather than over-claiming full resolution. **PASS**.
5. **No acceptance criterion tests the dropped push model (except the
   deliberate negative test); Dependencies row for Level Objective reflects
   the pull model.** VERIFIED. The "`ScoreResults` construction and Score
   Query API" acceptance criteria block contains six tests: five exercise the
   pull seams (`get_score_results()`/`get_current_score()` correctness) and
   one (`test_scoring_never_receives_objectives_resolution`) is the explicitly
   labeled negative test confirming the push seam's absence — no criterion
   asserts push-model behavior as a positive contract. This document's
   Dependencies row for Level Objective & Move-Limit System describes both
   pull seams and the sole-assembler model correctly, with no residual
   push-model language. **PASS**.

### Mechanical Fixes Applied This Pass

None required — no stragglers found. The `*Status:*` header was updated per
the re-review task's explicit instruction, not as a straggler fix.

### Verdict: APPROVED (re-review, 2026-07-18)

Blocking items resolved: 1 of 1 (the `finalize_results()` seam-composition
mismatch, confirmed retired and replaced by the ratified pull/compose model
on both documents). Recommended (advisory) items from the prior pass are
addressed as a side effect (Score Query API subsection now exists at
§ Detailed Rules 10a). No new blocking or advisory items surfaced by this
focused re-review.

**Summary**: The seam reconciliation composes correctly. `scoring-stars.md`
and `level-objectives.md` now declare and consume an identical two-seam pull
contract (`get_current_score()`, `get_score_results()`), the `ResultsData`
field mapping is complete and phantom-free against both `ScoreResults` and
`screen-flow.md` §11's declared seam, `closest_miss_summary` ownership is
consistent across all three documents, and both changelogs accurately
describe what is on disk. This document's `*Status:*` header is updated to
`Reviewed — APPROVED (re-review, 2026-07-18)`.
