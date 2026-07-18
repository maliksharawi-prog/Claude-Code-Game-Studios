# Design Review Log: Game UI / Screens Flow

Target document: `design/gdd/screen-flow.md`

---

## Review — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review --depth lean`, autonomous session (no specialist
agent spawning, no AskUserQuestion). Part of a coordinated 4-document pass
covering `save-persistence.md`, `screen-flow.md`, `world-map.md`, and
`juice-layer.md` together, with explicit cross-document contract checks —
this document sits at the center of that pass (it composes signals from
Board Engine, Touch & Input, Save & Persistence, and Juice Layer).
**Reviewer**: game-designer (self-authored analysis).
**Re-review**: No — first review.

### Completeness: 8/8 sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present, plus Cross-References and
Open Questions.

### Dependency Graph

Declared "Depends On": Level Data Format (APPROVED), Save & Persistence
(Draft), Touch & Input System (APPROVED), Match-3 Board Engine — header said
"Drafting," **stale**, actually APPROVED (fixed), Juice Layer (Draft, added
— mutual, was missing from the header entirely), Level Objective &
Move-Limit System / Scoring & Star Thresholds / Level Progression World Map
— header said "not yet authored" for all three, **stale**, all three now
exist as Draft (fixed).

All referenced systems now exist on disk; no broken references remain after
this pass's fixes.

### Fixes Applied Directly (mechanical reconciliation)

1. **Stale status labels** (header + body Dependencies table, 4 rows):
   Board Engine "Drafting"→APPROVED; Level Objective, Scoring & Star
   Thresholds, and World Map "not yet authored"→Draft. Removed hedging
   language ("Expected to emit...", "Reciprocal note required") that no
   longer applies now that the referenced documents exist and, in two of
   three cases, actually confirm the reciprocal note.
2. **Missing dependency**: Juice Layer was never listed in this document's
   header or Dependencies table despite `juice-layer.md` declaring a mutual
   relationship with this document (hosting Results Win, and proposing a
   Formula 5 extension). Added.
3. **Formula 1 arithmetic error inherited from cross-check**: none found —
   see Formula Verification below; this document's own formulas were all
   independently correct.
4. Status header updated to this verdict.

### Formula Verification (worked examples recomputed by hand)

- **Formula 1** (`nav_taps_total`): `2 + 1×1 + 1 = 4` ✓; floor case
  `n_retries=0` → `3` ✓; AC's `n_retries=2` → `2+1×2+1=5` ✓. All match.
- **Formula 2** (frame budget): `ceil(300/16.6667)=18` ✓,
  `ceil(200/16.6667)=12` ✓. Both match.
- **Formula 3** (retry latency): `250+900+250=1,400 ≤ 2,000` ✓, headroom
  `600ms` ✓ correct.
- **Formula 4** (unlock gate): `2 ≥ 1 → unlocked=true` ✓; `0 ≥ 1 →
  locked=false` ✓ both cases correct.
- **Formula 5** (input-lock composition): recomputed both worked examples
  — see Contract Coherence below, this formula was **substantively revised**
  during this review, not merely verified.
- **Formula 6** (new-best detection): `3>2=true, 3900>3400=true` ✓;
  non-improving case `2>2=false (should be 2 not >2, correctly false),
  3100>3400=false` ✓ both correct.

5 of 6 formulas verified correct with zero changes; Formula 5 required a
substantive fix (below), not an arithmetic error but a genuine contract gap.

### Contract Coherence — The Central Focus of This Review

**`effective_board_input_enabled` — three documents, one rule, and they did
NOT compose correctly before this pass (now fixed).** Traced the full
chain: `touch-input.md` Rule 4 gates on a single boolean it calls
`board_input_enabled`, expected to be "owned/driven by whichever system
tracks simulation busy-state." `board-engine.md` emits its own raw
`board_input_enabled_changed` signal (true only when its resolution loop is
Idle). This document's own §7 already establishes the correct
composition pattern: Screen Flow "never overwrites Board Engine's internal
signal, it composes an independent veto on top of it" (`overlay_is_active`).
`juice-layer.md` §10 then independently derives a *third* veto term,
`juice_input_lock` — necessary because Board Engine reports idle
**synchronously**, well before the Juice Layer has replayed a single visible
frame of a cascade, so relying on Board Engine's raw flag alone would let a
player fire a second swap mid-replay. Critically, `juice-layer.md` states
outright that this extension is "a recommended follow-up to `screen-flow.md`,
not made there, per this document's own file-edit scope."

**Before this review, this meant two documents each defined
`effective_board_input_enabled` with the identical name but different term
sets** — this document's Formula 5 (2 terms:
`base_state==GAMEPLAY AND board_engine_ready AND NOT overlay_is_active`)
vs. `juice-layer.md`'s Formula 5 (3 terms:
`board_engine_ready AND NOT overlay_is_active AND NOT juice_input_lock`,
missing the `base_state` term entirely). This is exactly the kind of
"same name, two definitions" contradiction that would leave an implementer
guessing which one is authoritative. There was also a second, independent
naming inconsistency: this document's own §7 prose called Board Engine's
raw signal `board_input_enabled` (matching `board-engine.md`'s and
`touch-input.md`'s actual terminology), while this document's own Formula 5
and `juice-layer.md`'s Formula 5 both used a different symbol,
`board_engine_ready`, for the exact same referent.

**Fixed directly, in both documents (both are in this review's scope):**
- This document's Formula 5 is now the single canonical definition:
  `(base_state==GAMEPLAY) AND board_input_enabled AND NOT overlay_is_active
  AND NOT juice_input_lock` — a 4-term composition, adopting Juice Layer's
  proposed extension.
- Renamed `board_engine_ready`→`board_input_enabled` throughout this
  document's Formula 5 (named expression, symbol table, both worked
  examples), matching `board-engine.md`'s and `touch-input.md`'s actual
  terminology.
- Added a second worked example demonstrating the `juice_input_lock` veto
  firing alone (no overlay open) — mirrors `juice-layer.md`'s own worked
  example for internal consistency.
- Updated the Acceptance Criteria bullet, the Touch & Input Dependencies
  row, and added two Cross-References rows documenting the adoption.
- `juice-layer.md` (also in this batch's scope) was updated in parallel to
  match — see its own review log for the reciprocal fixes (renamed symbol,
  added the `base_state` term, marked its Open Question item #1 resolved).

This also **retroactively validates** a safety argument `juice-layer.md`'s
§2 (Shadow Board Model) makes for its one permitted live `get_piece_at()`
query during reshuffle: that argument explicitly depends on "the Juice
Layer's own input-lock policy... guarantees no new player-triggered move
can begin until the Juice Layer's replay of the current move fully
completes" — which was **not actually guaranteed** while this document's
Formula 5 omitted the `juice_input_lock` term. It is guaranteed now.

**Advisory, not fixed (out of scope / not a genuine defect):**
`touch-input.md`'s own text ("owned/driven by whichever system tracks
simulation busy-state — expected to be Match-3 Board Engine") is now
understood to describe only the *raw* Board Engine term, not the final
composed value Touch & Input actually reads in production. `touch-input.md`
is APPROVED and out of this review's file-edit scope; flagged as a
recommended cross-reference clarification for its next review pass (does
not change its actual behavior — its own Dependencies section already
anticipated Screen Flow "co-owning" this toggle).

**`attempt_number` supply vs. `rng-service.md`'s documented policy —
verified coherent, imprecise citation only (advisory).** This document's §7
claims its reset/increment policy matches `rng-service.md`'s Tuning Knobs
row "verbatim." `rng-service.md`'s literal text says "Resets to 1 only on
fresh level entry **from the map**." This document's actual policy is
broader: it resets on T4 (from the map) **and** T18 (advancing to a new
level from Results Win, which does not pass through the map). Traced
whether this is a contradiction: it is not — T18's reset is a *correct,
necessary generalization* of `rng-service.md`'s intent (a genuinely new
level's attempt counter must start at 1 regardless of navigation path;
omitting the T18 reset would risk a new level inheriting a stale retry
count from whatever level preceded it). The "verbatim" claim is technically
imprecise phrasing rather than a functional bug. Not fixed (would require
editing `rng-service.md`, out of scope); logged as advisory for
`rng-service.md`'s next review pass to broaden its own phrasing to match
this document's now-canonical (and correct) policy.

**Fizz mount points vs. `characters-and-tone.md` — verified consistent.**
This document's §8 table (Pre-Level Card, Results Win, Results Lose, World
Map "none required at MVP," and an explicit "Never" for Gameplay/Pause/
Settings/Boot) is a complete, consistent enumeration of
`characters-and-tone.md`'s §2 placement rule (World Map, Pre-Level Card,
Results only — everything else implicitly never). No fix needed.

**`record_level_completion()` caller gap — see save-persistence.md's
review log for the full write-up.** This document's §5 Data Contract
independently makes the same claim save-persistence.md makes ("Level
Objective/Scoring calls `record_level_completion()`"), and `level-
objectives.md` does not confirm it. Flagged in this document's own
Dependencies table and Cross-References; not blocking this document's own
approval (its own contract — that Screen Flow itself never calls this API
directly — is correct and unaffected either way).

### Required Before Implementation

None specific to this document alone. The Formula 5 composition gap — the
one genuinely blocking-grade issue found — was resolved directly in this
pass (see Contract Coherence above), not left open.

### Recommended Revisions (Advisory)

1. **[Cross-doc]** `rng-service.md`'s Tuning Knobs phrasing for
   `attempt_number` reset policy should be broadened at its next review
   pass to explicitly cover "or advancing to a new level" alongside "fresh
   entry from the map."
2. **[Cross-doc]** `touch-input.md`'s Dependencies section should clarify,
   at its next review pass, that the `board_input_enabled` value it reads
   is the Screen-Flow-composed `effective_board_input_enabled`, not Board
   Engine's raw signal directly.
3. **[Cross-doc, shared with save-persistence.md]** Confirm which system
   calls `record_level_completion()` on a win — currently unconfirmed by
   any of the three documents that reference it.

### Nice-to-Have

- The Base Layer / Overlay Layer two-tier state model (§1) with an
  explicitly enumerated 9-member Legal Composite States set is a strong,
  testable design — the Acceptance Criteria's "no test sequence ever
  produces an out-of-set combination" bullet is exactly the right
  falsifiable claim for a state machine this size.
- The Retry Loop Pre-Level-Card-skip rule (§6) paired with Formulas 1 and 3
  is a good example of turning a Player Fantasy promise ("instant retry")
  into hard, regression-testable numeric budgets rather than a vibe.

### Senior Verdict [game-designer, lean-mode self-review]

This document is the composition point for four other systems' input-gating
signals, and that is exactly where this review found its one real,
load-bearing gap: two documents (this one and `juice-layer.md`) each
defined the same named quantity differently, with `juice-layer.md` already
correctly identifying the fix but explicitly declining to make it (citing
file-edit scope). Since this review has write authority over both
documents, the correct action was to close that loop directly rather than
merely flag it — which is what was done. Every other formula in this
document checked out on independent recomputation, the state machine is
exhaustively enumerable and testable, and the "backgrounded vs. killed"
distinction against `save-persistence.md`'s no-mid-level-resume rule is a
genuinely well-designed piece of cross-document coherence, not an accident.
The remaining open items (attempt_number citation precision,
`record_level_completion()` caller) are real but correctly scoped as
advisory / cross-document follow-ups that don't block this document's own
internal correctness.

### Scope Signal

Rough scope signal: **L** (9-state composite state machine, 6 formulas, 7+
cross-system dependencies including a 4-way signal composition it now
canonically owns — multi-system integration risk, though no new ADR
required).

### Verdict: APPROVED

Blocking items: 0 (the one blocking-grade issue found — Formula 5's
composition gap — was resolved directly in this pass) | Recommended
(advisory): 3 | Nice-to-have: 2
Mechanical fixes applied in this pass: stale status labels (4 rows + header),
1 missing dependency added, Formula 5 substantively revised (4-term
composition, symbol rename, 2nd worked example, AC bullet, 2
Cross-References rows, 1 Dependencies row rewrite)
Prior verdict resolved: First review (N/A)
