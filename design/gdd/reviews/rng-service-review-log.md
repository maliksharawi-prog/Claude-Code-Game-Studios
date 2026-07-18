# Design Review Log: RNG Service

Target document: `design/gdd/rng-service.md`

---

## Review — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review --depth lean`, autonomous remote session (no specialist
agent spawning, no AskUserQuestion, no approval waits).
**Reviewer**: game-designer (self-authored analysis; no `systems-designer`,
`qa-lead`, or `creative-director` subagents spawned — lean mode).
**Re-review**: No — first review.

### Completeness: 8/8 sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present, in standard order, well
above minimum bar (Detailed Rules alone runs 6 subsections; Acceptance
Criteria has 7 test-category groupings).

### Dependency Graph

Declared "Depends On": none (Foundation layer — confirmed).

Declared as depended-on-by / referenced:
- ✓ `design/gdd/systems-index.md` — exists, confirms RNG Service's Foundation
  position and zero inbound dependencies.
- ✗ `design/gdd/board-engine.md` — NOT FOUND (correctly flagged in-doc as
  "to be confirmed when authored").
- ✗ `design/gdd/special-candies.md` — NOT FOUND (correctly flagged as
  reserved/not yet authored).
- ✗ `design/gdd/booster-brewing.md` — NOT FOUND (correctly flagged Phase 2,
  forward dependency).
- ✗ `design/gdd/events-theming.md` — NOT FOUND (correctly flagged Phase 3,
  forward dependency).
- ✓ `design/gdd/level-data-format.md` — exists; explicitly and correctly
  documented as **not** a dependency (RNG Service consumes only opaque
  caller-supplied parameters that originate there, never the schema itself).

No broken references — every "not yet authored" system is honestly labeled
as such in the doc's own text.

### Fixes Applied Directly (mechanical reconciliation)

1. **Wrong field name** — the doc referenced Level Data Format's active-color
   field as `candy_palette` in two places (§5 prose and Formula F5's variable
   table). Level Data Format's actual schema field is `color_pool`. Fixed
   both occurrences to the correct name.
2. **Stale cross-reference** — §5 said the 5-color launch default is "owned
   by `level-data-format.md` when that document is authored." Level Data
   Format is now authored (this review batch); updated to point at the
   concrete field and rule (`color_pool`, V11: 3–5 unique entries from the
   5-color canonical roster in `design/art/art-bible.md`).
3. **Tuning Knobs clarification** — the "Active color pool size" row stated
   a Safe Range of "3 – 8" without noting that Level Data Format's actual
   authored-content ceiling is 5 (V11, bounded by the art bible's 5-candy
   roster). Added a clarifying note that 3–8 is RNG Service's technical
   ceiling only; 6–8 is unused headroom until the candy roster grows.
4. **Status header** updated to reflect this review's verdict.

### Cross-System Consistency — Flagged Items Adjudicated

**Item: `level_id` integer assumption vs. Level Data Format's actual String ID scheme.**
Level Data Format's `level_id` field is confirmed to be a String
(`<region_code>-<3-digit-sequence>`, e.g. `candy_kingdom_hub-001`) — exactly
the case rng-service.md's §3 and Edge Cases table already anticipated and
handle ("must be resolved to a fixed, versioned ordinal integer via a level
manifest before calling `start_level_session()`... never through a runtime
string-hash function"). **This reconciles cleanly** — no contradiction. The
one gap was that Level Data Format's own Dependencies/Cross-References
tables didn't acknowledge this handoff explicitly; that's been fixed on the
Level Data Format side (see its review log). The open item is *ownership* of
the level manifest itself — logged as advisory below, not a defect in this
document.

**Item: `rng_seed` field vs. RNG Service's three seed entry points.**
This is a **real composition gap**, logged as advisory (see below) — not
fixed directly because closing it requires a design decision (add a fourth,
production-grade literal-seed entry point vs. repurpose an existing one vs.
route all fixed-seed levels through `start_daily_session` only). Full detail
in Level Data Format's review log, where the field lives; cross-referenced
here because it also constrains this document's own future API surface.

**Item: touch-input's 44px floor vs. Level Data Format's 9×9 max board.**
Not directly applicable to RNG Service (no touch/rendering surface) —
adjudicated in the Level Data Format and Touch & Input review logs.

**Item: V18's `REFERENCE_SCORE_PER_MOVE` vs. RNG/scoring assumptions.**
Adjudicated in Level Data Format's review log (the field lives there), but
worth noting from RNG Service's side: this document's own Tuning Knobs
rationale states that *fewer* active colors materially increase match/cascade
frequency ("Fewer colors = near-guaranteed matches on almost every swap").
That's directly relevant context for evaluating whether a single flat
`REFERENCE_SCORE_PER_MOVE` constant (calibrated against one 5-color
prototype run) generalizes to levels authored with 3–4 colors. Flagged as
advisory in Level Data Format's log.

### Formula Verification (worked examples recomputed by hand)

All six formulas' worked examples were independently recomputed:

- **F1** (`level_id=1007, attempt_number=3`): `combine() = 1,547,274,724` ✓ matches doc, and matches the bug-repro logging table's example value — internally consistent across two sections.
- **F2** (`daily_challenge_id=42, calendar_date_utc=20650`): `combine() = 653,539,216` ✓ matches doc.
- **F3** (`master_seed=500, stream_id=1`): `combine() = 73,026,539` ✓ matches doc.
- **F4** (`min=1, max=6, next_float=0.42`): `result = 3` ✓ matches doc.
- **F5** (`active_colors` 5-element array, `next_float=0.83`): `result = "purple"` (index 4) ✓ matches doc.
- **F6** (Fisher–Yates on `[A,B,C,D]`): step-by-step swap trace reproduces `[C,D,A,B]` after 3 draws ✓ matches doc exactly, including the `i=1` no-op swap.

No arithmetic errors found. This is unusually clean for a first-pass GDD.

### Required Before Implementation

None. All findings are advisory.

### Recommended Revisions (Advisory — not fixed directly, requires judgment)

1. **[Phase-3 readiness]** RNG Service's API has no production (non-test)
   entry point that accepts an explicit literal seed alongside
   `level_id`/`attempt_number` for bug-repro logging purposes.
   `start_test_session(master_seed)` is documented as "Test/debug entry
   point... for gdUnit4 seed injection," and `start_daily_session()` derives
   its seed purely from `(daily_challenge_id, calendar_date_utc)` — neither
   composes with Level Data Format's per-level `rng_seed >= 0` field as
   currently specified. Not blocking (unused at MVP; `rng_seed = -1` on all
   10 launch levels), but should be resolved before Phase 3 (Events/Theming
   Engine or any daily/event level authoring) begins. See Level Data
   Format's review log for the primary write-up.
2. **[Arithmetic robustness]** Formula F1/F2's `combine()` step
   (`level_id × K1 + attempt_number × K2`) can produce an intermediate
   product that exceeds signed 64-bit integer range at the formula's own
   declared maximum input (`level_id` up to `2^32−1`): `(2^32−1) ×
   2,654,435,761 ≈ 1.14 × 10^19`, versus a signed-64-bit ceiling of
   `≈9.22 × 10^18`. In practice this is very unlikely to matter (real
   `level_id` values will be in the tens-to-low-thousands range for the
   foreseeable content roadmap), but the doc doesn't currently give
   `combine()` the same explicit implementation contract it gives `mix32`
   (deterministic, avalanching, platform-stable). Recommend adding one
   sentence specifying that the multiply-and-mod step must use unsigned
   64-bit (or wider) arithmetic to guarantee identical results across
   Godot's export targets for the full declared input range — mirroring the
   `mix32` contract's existing platform-stability language.
3. **[Test coverage]** No Acceptance Criteria test explicitly pins the
   `min_value == max_value` boundary case for `next_int` (range_size = 1).
   The formula handles it correctly by inspection, but it's untested.

### Nice-to-Have

- Stylistic note: RNG Service explicitly frames Level-Data-Format-derived
  inputs (`level_id`, `active_colors`) as "opaque caller-supplied
  parameters, not a dependency" (Dependencies §), while Touch & Input frames
  structurally similar Level-Data-Format-derived inputs (grid dimensions) as
  "This depends on it (data only)." Both framings are internally consistent
  with each document's own header metadata, so this isn't a defect — but a
  shared house style for "consumes data that originates elsewhere without
  reading the schema" vs. "has a soft data dependency" would help future
  Foundation-layer GDDs read more uniformly.

### Senior Verdict [game-designer, lean-mode self-review]

This is a strong Foundation-layer document. The Honest Randomness Contract
(§4) does real work translating Pillar 2 into an enforceable, testable API
boundary — the "no hidden bias parameter" acceptance criteria are exactly
the kind of falsifiable check that makes a design pillar mean something
mechanically, not just rhetorically. All six formulas check out arithmetically
against their worked examples, which is a good signal of authoring rigor.
The one wrong field name (`candy_palette` vs. `color_pool`) was a real
cross-doc bug that would have confused an implementer cross-referencing both
files; now fixed. Remaining items are genuinely advisory: the `rng_seed`
composition gap is real but explicitly scoped to unused-at-MVP Phase 3
content, and the overflow note is a belt-and-suspenders robustness
recommendation, not a known-broken path at realistic input scale.

### Scope Signal

Rough scope signal: **L** (single self-contained Foundation utility, but 6
formulas plus 4 forward-dependent systems and a probable ADR need — e.g.
final `mix32` algorithm selection is explicitly flagged as a
lead-programmer/technical-director decision). Producer should verify before
sprint planning.

### Verdict: APPROVED

Blocking items: 0 | Recommended (advisory): 3 | Nice-to-have: 1
Prior verdict resolved: First review (N/A)
