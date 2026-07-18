# Design Review Log: Save & Persistence

Target document: `design/gdd/save-persistence.md`

---

## Review — 2026-07-18 — Verdict: APPROVED

**Mode**: `/design-review --depth lean`, autonomous session (no specialist
agent spawning, no AskUserQuestion). Part of a coordinated 4-document pass
covering `save-persistence.md`, `screen-flow.md`, `world-map.md`, and
`juice-layer.md` together, with explicit cross-document contract checks.
**Reviewer**: game-designer (self-authored analysis).
**Re-review**: No — first review.

### Completeness: 8/8 sections present

Overview, Player Fantasy, Detailed Rules, Formulas, Edge Cases, Dependencies,
Tuning Knobs, Acceptance Criteria — all present, plus Open Questions.

### Dependency Graph

Declared "Depends On": none (Foundation layer — confirmed).

Declared as depended-on-by / referenced:
- ✓ `design/gdd/level-data-format.md` — exists, APPROVED.
- ✓ `design/gdd/screen-flow.md` — exists, Draft (was stale "not yet
  authored" before this pass — fixed).
- ✓ `design/gdd/world-map.md` — exists, Draft (same staleness, fixed).
- ✓ `design/gdd/level-objectives.md` — exists, Draft (same staleness,
  fixed) — see Required Before Implementation below for a real gap found
  on this reference, not just a stale label.
- ✗ `design/gdd/booster-brewing.md`, `events-theming.md`, `social-layer.md`
  — correctly flagged not-yet-authored, Phase 2/3.
- ✓ `.claude/docs/technical-preferences.md`, `design/art/art-bible.md` —
  exist, cited correctly.

### Fixes Applied Directly (mechanical reconciliation)

1. **Stale cross-references** — three Dependencies-table rows
   (`screen-flow.md`, `world-map.md`, `level-objectives.md`) said "not yet
   authored." All three now exist (Draft). Updated all three, and added
   fulfillment notes for the two whose reciprocal notes are confirmed
   satisfied (`screen-flow.md`, `world-map.md`).
2. **Missing header entry** — the document header's "Depended On By" list
   omitted Level Objective & Move-Limit System even though the body
   Dependencies table listed it. Added it to the header.
3. **Open Questions row resolved** — the row asking whether orphaned
   `level_records` pruning should be decided at `world-map.md` authoring is
   now answered: `world-map.md` § Detailed Rules 8 confirms "never pruned,"
   matching this document's own precedent. Marked Resolved.
4. **New Open Questions row added** — logging the `record_level_completion()`
   caller gap found below (not fixable here — cross-document).
5. Status header updated to this verdict.

### Formula Verification (worked examples recomputed by hand)

- **Formula 1 (FNV-1a 32-bit checksum)**: independently recomputed the full
  single-byte `'a'` (0x61) worked example byte-for-byte —
  `hash = OFFSET_BASIS XOR 97 = 2,166,136,228` ✓, then
  `(2,166,136,228 × 16,777,619) mod 2^32`, computed via modular
  decomposition (`16,777,619 = 2^24 + 403`) two independent ways, both
  converging on `3,826,002,220` (`0xE40C292C`) ✓ — matches the doc exactly
  and matches the published public FNV-1a-32 test vector for `"a"`. Fully
  verified, no error.
- **Formula 2 (Level Record Merge)**: worked example (`existing = {3,
  4100, 6, ...}`, replay scoring `{1, 2600}`) → `best_stars'=3,
  best_score'=4100, completion_count'=7` ✓ matches doc exactly.
- **Formula 3 (Total Stars Self-Healing)**: `3+2+3=8` corrects a stored `7`
  ✓ matches doc.
- **Formula 4 (A/B Slot Selection)**: counter-flip trace (`42/41` →
  primary A, next-target B → write → `42/43` → primary flips to B) ✓
  matches doc, logically sound.
- **Formula 5 (Storage Size Bound)**: `300 + 120×150 = 18,300` per slot,
  `×2 = 36,600` total ✓ matches doc's `~18KB`/`~36KB` claims. Independently
  re-measured the sample `LevelRecord` JSON fragment used to justify
  `PER_LEVEL_RECORD_BYTES ≈ 150`: character count came to ~147-148, not
  exactly the doc's stated `≈146` — a trivial discrepancy that doesn't
  change the rounded-up constant (`150`) or any downstream conclusion
  ("well under 400MB," "well under any browser's minimum quota"). Not
  fixed — within the stated "provisional, measured" tolerance, non-blocking.

No arithmetic errors found. This is the most rigorously verified checksum
implementation reviewed in this batch — a full byte-exact hand-recomputation
against a public test vector is a strong signal of authoring care.

### Contract Coherence Checks (Focus Item: task's cross-document handshakes)

**Derived-state contract vs. `world-map.md`'s "zero duplicated booleans"
claim.** Verified: `world-map.md` reads exactly `level_records` (presence =
completed) and `get_total_stars()`, introduces zero new fields, and this
document's schema already anticipated exactly this consumption pattern in
its own Dependencies table before `world-map.md` existed. Confirmed
coherent — no schema drift, no fix needed.

**"No mid-level resume" (§10) vs. `screen-flow.md`'s app-background
suspension handling (T9).** Verified these are two different, non-
contradictory mechanisms operating at different layers: this document's
"no mid-level resume" is a **disk-durability** guarantee (nothing about an
in-progress attempt is ever written to disk, so a process **kill** always
abandons it). `screen-flow.md`'s T9 (auto-Pause on backgrounding) is an
**in-memory** mechanism — while the app is merely backgrounded (process
alive, not killed), Board Engine's live grid state, RNG stream, and move
count all remain in memory and the player can Resume into the exact same
attempt. `screen-flow.md`'s own Edge Cases table explicitly distinguishes
"backgrounded (state preserved)" from "process killed (state discarded,
T21)" — confirmed this distinction is real, deliberate, and correctly
non-conflated in both documents. No coherence issue found.

**`record_level_completion()` caller — a real, unresolved gap (not fixable
here).** This document (§3, §11) and `screen-flow.md` (§5 Data Contract)
both assert "Level Objective & Move-Limit System... calls
`record_level_completion()` on a win." Independently read
`level-objectives.md` in full: its Dependencies table has **no row for
Save & Persistence at all**, and its § Detailed Rules 9 win-resolution flow
(build `ObjectivesResolution` → call Scoring's `finalize_results()` → emit
`level_resolved`) never mentions this call. As currently authored across
all three documents, **no system actually calls `record_level_completion()`
on a win** — a real, structural gap in the win→save pipeline, not a stale
label. `level-objectives.md` and `scoring-stars.md` are being reviewed by a
sibling agent in this same pass and are out of this review's file-edit
scope. Flagged prominently below and in this document's own Dependencies
table / Open Questions.

### Required Before Implementation

1. **[BLOCKING-ADJACENT, cross-document]** Confirm which system calls
   `record_level_completion(level_id, stars_earned, score_earned)` on a
   win. This document's own contract is internally complete and correct
   regardless of caller — not a defect in this document's own design — but
   the overall win→save pipeline is currently unconfirmed end-to-end
   because the presumed caller (`level-objectives.md`) does not confirm the
   call. Not blocking for **this** document's approval (its half of the
   contract is sound), but should be resolved with priority at
   `level-objectives.md`'s or `scoring-stars.md`'s next review pass — this
   is functionally more urgent than a routine forward-dependency note,
   since without it player progress is never durably saved.

### Recommended Revisions (Advisory)

None beyond the items already fixed directly above.

### Nice-to-Have

- The corruption-recovery ladder's "first launch is not corruption" rule
  (§6, rung 4) and the tamper-accept policy (§7) are both good examples of
  translating Pillar 2 into falsifiable, testable rules rather than
  aspirational language — worth using as a reference pattern for future
  Foundation-layer persistence work (e.g., a future cloud-sync layer).

### Senior Verdict [game-designer, lean-mode self-review]

This is a tight, implementation-ready Foundation document. Every formula
was independently recomputed and checked out exactly, including a full
byte-exact FNV-1a hand-verification against a public test vector — an
unusually strong rigor signal. The A/B double-buffer's self-healing
rotation and the tamper-accept policy are both well-reasoned, correctly
scoped to MVP's single-player context, and explicitly flag their own
Phase-3 revisit points. The one real substantive finding — the
`record_level_completion()` caller gap — is not a defect in this document;
it is a genuine hole on the *other* side of a contract this document
specifies completely and correctly. Approving this document while flagging
that gap prominently for the sibling review is the right call: blocking
this document's approval would not fix the gap, since the fix belongs in
`level-objectives.md`.

### Scope Signal

Rough scope signal: **M** (self-contained Foundation persistence system, 5
formulas, but only 2 genuine forward-consumer integration points that
matter at MVP — Screen Flow and World Map, both now Draft and already
consuming this document's exact existing shape with zero schema changes).

### Verdict: APPROVED

Blocking items: 0 (for this document specifically) | Cross-document items
requiring urgent follow-up elsewhere: 1 | Recommended (advisory): 0 |
Nice-to-have: 1
Mechanical fixes applied in this pass: 5 (3 stale cross-references, 1
missing header entry, 1 Open Questions resolution + 1 new Open Questions
row logging the caller gap)
Prior verdict resolved: First review (N/A)
