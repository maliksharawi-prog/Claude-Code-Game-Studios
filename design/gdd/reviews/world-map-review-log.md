# World Map — Design Review Log

## Review — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review --depth lean` (autonomous, no specialist spawning)
**Re-review**: No — first review of this document.
**Context docs read**: `board-engine.md` (APPROVED), `level-data-format.md`
(APPROVED), `save-persistence.md` (APPROVED), `scoring-stars.md` (Revised —
awaiting re-review), `level-objectives.md` (Revised — awaiting re-review),
`screen-flow.md` (APPROVED), `design/narrative/characters-and-tone.md`,
`design/art/art-bible.md`, `design/gdd/game-concept.md`,
`design/gdd/systems-index.md`.

---

### Completeness: 8/8 required sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — plus Cross-References and Open Questions
(non-required, additive). No missing sections.

### Dependency Graph

| Referenced System | File Exists? |
|---|---|
| Level Data Format | ✓ `design/gdd/level-data-format.md` (APPROVED) |
| Save & Persistence | ✓ `design/gdd/save-persistence.md` (APPROVED) |
| Match-3 Board Engine | ✓ `design/gdd/board-engine.md` (APPROVED) |
| Scoring & Star Thresholds | ✓ `design/gdd/scoring-stars.md` (Revised, awaiting re-review) |
| Level Objective & Move-Limit System | ✓ `design/gdd/level-objectives.md` (Revised, awaiting re-review) |
| Game UI/Screens Flow | ✓ `design/gdd/screen-flow.md` (APPROVED) |
| Events/Theming Engine | ✗ not yet authored — expected (Phase 3, forward dependency, correctly flagged as such) |
| `design/art/art-bible.md`, `design/narrative/characters-and-tone.md`, `design/gdd/game-concept.md` | ✓ all exist |

No broken references. The one "missing" file (`events-theming.md`) is a
correctly-flagged forward dependency, not a defect.

---

### Formula Recomputation

All worked examples recomputed independently; all check out exactly as
written.

**Formula 1 — Region Star Gate**, `REGION_GATE_PERCENT = 0.6`:
- MVP scope (1 region, 10 levels): `stars_required_to_unlock(0) = 0`. Gate
  mechanism present, correctly unexercised. ✓
- Launch scope (4 × 30 levels):
  - Region 1: `max_stars_before = 3×30 = 90`; `round(0.6×90) = 54` ✓
  - Region 2: `max_stars_before = 3×60 = 180`; `round(0.6×180) = 108` ✓
  - Region 3: `max_stars_before = 3×90 = 270`; `round(0.6×270) = 162` ✓
- Replay-incentive sanity check: 30 levels × 1★ floor = 30 stars (short of
  54); reaching 54 requires raising 12/30 levels (40%) from 1★→3★, or a
  realistic ~2★ average (60 stars) clears the gate with **zero** dedicated
  replay. Math confirmed correct. (See mechanical fix below — this
  calculation's "1★ floor" premise required a caveat.)

**Formula 4 — Display Number**: `frosted_peak` level at `sequence_index=4`,
region order `[candy(30), frosted(30), sundrop(30), thistleberry(30)]`:
`1 + 30 + 4 = 35` ✓.

**Formula 5 — Region Completion %**: 10-level region, summed `best_stars =
24`: `100 × 24/30 = 80%` ✓.

**Formula 6 — World Completion %**: 120 levels, `get_total_stars() = 180`:
`100 × 180/360 = 50%` ✓. `= 360` → `100%`, `profile_fully_completed = true` ✓.

**Pillar check — "no pay-gates / no grind" vs. 60% cumulative gate**:
Confirmed achievable without dedicated 3-star grinding at both content
scopes. MVP's single region has a `0` gate (inert by construction). At
launch scope, a realistic ~2★ average per region (60/90/120... cumulative)
clears every regional gate (54/108/162) well before the region is finished,
with the margin *growing* region over region. No grind is required by the
math as authored. **No issue found — formula validated as fit for the
stated pillar.**

---

### Cross-Checks

**W6–W7 manifest cross-validation vs. Board Engine.** Confirmed coherent and
non-circular. `board-engine.md` §2 owns `assets/data/level_manifest.tres`
(`entries: Array[String]`, append-only, RNG-ordinal); `world-map.md` W6/W7
cross-validate `world_map_manifest.tres`'s `level_sequence` entries against
that file's field name and shape exactly. The dependency is one-directional
(World Map → Board Engine, validation/CI-time only) — Board Engine's own
document contains **zero** references to World Map or
`world_map_manifest.tres` (confirmed via full-text search), so there is no
runtime or design-time cycle.

One bidirectionality gap exists, but it is **not** a defect in this
document: per `design/CLAUDE.md`'s bidirectionality rule, `board-engine.md`
does not yet list World Map in its own "Depended On By" header or
Cross-References. `world-map.md` already self-flags this correctly (§
Detailed Rules 2's "Recommended follow-up" note, and an existing Open
Question) rather than editing `board-engine.md` out-of-scope. **Advisory,
owned by `board-engine.md`'s next review pass, not by this document.**

**Derived-state claim vs. Save & Persistence's actual schema.** Confirmed:
`level_records: Dictionary<level_id, LevelRecord>`, `LevelRecord.best_stars
(int, 0-3)`, and `get_total_stars()` all exist exactly as world-map.md
describes them (`save-persistence.md` §2, §11). Zero schema changes are in
fact required. The "orphaned records never pruned" claim matches
`save-persistence.md`'s own resolved Open Question exactly (line 868 there).

**"Completed with 0 stars impossible" claim — FOUND FALSE, FIXED.** This
document originally asserted a `COMPLETED` level "always carries a
`best_stars` value of 1–3 (never 0 — Level Data Format's V17 guarantees any
win awards at least 1 star)." This is incorrect as a universal claim: V17
only applies "if the level has at least one `score_target` objective"
(`level-data-format.md` V17 + its own Edge Cases row, which explicitly
labels the `collect_color`-only exception "intentional, not a gap").
`scoring-stars.md`'s Formula 7 and Edge Cases table independently confirm
`stars_earned = 0` is reachable on a `WIN` outcome for such levels, and
`screen-flow.md`'s own Edge Cases table already carries the identical
caveat ("`ResultsData.stars_earned` resolves to `0` on a `WIN` outcome...").
**Verdict**: mechanical fix — this was a false claim contradicted by
already-established, cross-referenced fact elsewhere in the approved/revised
doc set, not a new design decision. **Fixed directly in `world-map.md`**
(Node States table, Replay-incentive sanity check caveat, new Open Question
entry). Confirmed the fix does not change any formula's output: Formula 2
keys off record *presence*, not star value; Formula 5/6 already treat "no
record" and "record with `best_stars = 0`" identically, so no other formula
or worked example needed revision.

**`record_level_completion()` claim — verified accurate, not invented.**
This document never claims to *call* `record_level_completion()` itself
(explicitly: "**Zero writes** — World Map never calls
`record_level_completion()`"). It correctly names an **existing** API
defined in `save-persistence.md` §11 (`record_level_completion(level_id,
stars_earned, score_earned)`) and correctly attributes the calling
responsibility to Level Objective & Move-Limit System. Cross-checked against
`level-objectives.md` directly: that document, despite now existing
(Revised, awaiting re-review), **does not yet reference Save & Persistence
anywhere** — no dependency listed, no call site described. So world-map.md's
own hedge ("expected to own... flagged as not yet confirmed by
`level-objectives.md` itself") was already accurate and is **not** a naming
slip or an invented contract. Per fix policy this required no correction to
the claim itself — only a status-label update (see below) and a
strengthened cross-reference noting exactly what is unconfirmed, so a future
reader doesn't have to re-derive it. **Not blocking** — genuinely a gap in
`level-objectives.md`, out of this document's edit scope, already correctly
tracked.

**Region names/order vs. narrative doc.** Confirmed exact match: Candy
Kingdom Hub (resident: Marzi) → Frosted Peak → Sundrop Grove → Thistleberry
Hollow, same order in both `world-map.md`'s Player Fantasy/Formula 1 worked
example and `characters-and-tone.md`'s Region 1–4 sections.

---

### Mechanical Fixes Applied (this session)

All of the following were stale status labels or one substantive factual
correction — no design decisions were required, so all were fixed directly
per fix policy:

1. Header `Depends On`: `save-persistence.md` `Draft` → `APPROVED`;
   `scoring-stars.md` `Draft` → `Revised — seam reconciled, awaiting
   re-review`.
2. Header `Depended On By`: `screen-flow.md` `Draft` → `APPROVED`.
3. Dependencies table: `save-persistence.md` `Draft` → `APPROVED`;
   `board-engine.md` `NEEDS REVISION` → `APPROVED` (stale relative to its
   own re-review — contradicted this document's own header, which already
   said APPROVED); `scoring-stars.md` and `level-objectives.md` `not yet
   authored` → their actual current status (both now exist, both Revised);
   `screen-flow.md` `not yet authored` → `APPROVED`, with reciprocal-note
   language updated from "when authored, should..." to "fulfilled" for the
   three systems (`scoring-stars.md`, `screen-flow.md`) that have, in fact,
   already added the requested reciprocal note.
4. Detailed Rules §3: same stale-status corrections for `scoring-stars.md`
   and `level-objectives.md`, plus a strengthened note on exactly what
   remains unconfirmed in `level-objectives.md`.
5. **Node States table (`COMPLETED` row)**: corrected the false "never 0"
   claim; documented the `collect_color`-only exception and confirmed it
   does not affect Formula 2/5/6.
6. **Formula 1's Replay-incentive sanity check**: added a caveat that the
   "1 star minimum per completion" premise holds for `score_target`-bearing
   levels (the common case), not universally.
7. Added one new Open Questions row tracking whether content authoring
   should guarantee every level carries a `score_target` objective so the
   win-floor guarantee is universal rather than conditional.
8. Status header updated from `Draft — awaiting /design-review` to
   `Reviewed — APPROVED`.

---

### Required Before Implementation

None. No blocking items remain for this document.

### Recommended Revisions

None outstanding — the one substantive finding (the false 0-star claim) was
resolved directly during this review.

### Specialist Disagreements

N/A — lean mode, no specialists spawned.

### Nice-to-Have

- When `board-engine.md` next gets a review pass, add the reciprocal
  cross-manifest test-entry acknowledgment this document already requests
  (Open Questions, existing entry — not new).
- When `level-objectives.md` next gets a review pass, confirm it adds Save &
  Persistence as a dependency and explicitly states it calls
  `record_level_completion()` — this document's hedge should then be
  tightened from "not yet confirmed" to a firm citation.
- `level-data-format.md` line ~320/392 still describes World Map as "not
  yet written" — harmless (forward-looking language written before this doc
  existed) but worth a one-line freshen at that document's own next pass.

---

### Scope Signal

Rough scope signal: **M** (moderate complexity — 1 owned manifest resource,
6 formulas, 6+ cross-document dependencies, no new ADR required; producer
should verify before sprint planning).

---

### Senior Verdict

Lean-mode autonomous review (no `creative-director` synthesis spawned).
Based on full formula recomputation, dependency-graph validation, and
targeted cross-document fact-checking: the document's mathematics are
correct at both content scopes, the two-manifest architecture is coherent
and non-circular, the derived-state model is fully supported by Save &
Persistence's actual schema, and the one factual defect found (a false
universal "never 0 stars" claim) has been corrected in place without
altering any formula's behavior. Remaining gaps (board-engine.md's missing
reciprocal note, level-objectives.md's unconfirmed save-call) are owned by
those other documents' next review passes and are already correctly
tracked, not silently ignored, by this document.

**Verdict: APPROVED**

Blocking items: 0 | Recommended: 0 | Mechanical fixes applied: 8
