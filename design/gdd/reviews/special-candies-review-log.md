# Design Review Log: Special Candies & Combo Matrix

Target document: `design/gdd/special-candies.md`

---

## Review — 2026-07-18 — Verdict: APPROVED

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

Declared "Depends On": Match-3 Board Engine (APPROVED — Revision 2), RNG
Service (APPROVED), Level Data Format (APPROVED) — all three ✓ exist, all
three ✓ APPROVED.

Declared as depended-on-by / referenced:
- ✓ `design/gdd/scoring-stars.md` — exists (reviewed in this same pass, see
  its own log).
- ✓ `design/gdd/level-objectives.md` — exists (reviewed in this same pass).
- ✓ `design/gdd/juice-layer.md` — **exists on disk** (Draft, per
  `systems-index.md`'s Progress Tracker), but this document's header and
  § Detailed Rules 10 still describe it as "not yet authored." Stale, but
  not a broken reference — logged as advisory (Nice-to-Have), not fixed here
  because the same staleness pattern exists symmetrically in `juice-layer.md`
  itself (which still calls `special-candies.md`/`scoring-stars.md` "not yet
  authored") and in `booster-brewing.md`'s forward references. Fixing one
  side asymmetrically would not resolve the underlying pattern; recommend a
  dedicated `/consistency-check` pass once all Phase-MVP docs are authored.
- ✓ `design/gdd/booster-brewing.md` (Phase 2, gated) — correctly described
  as not yet authored; this is accurate (file does not exist).
- ✓ `design/art/art-bible.md`, `prototypes/sweet-cascade-concept/REPORT.md`
  — both exist, cited correctly.

No broken references.

### Seam Handshake Audit vs. `board-engine.md` (Focus Item)

Confirmed all four extension seams against `board-engine.md` § Detailed
Rules 3's exact, APPROVED (Revision 2) signatures:

| Seam | Board Engine (approved) | This document | Match? |
|---|---|---|---|
| 1 | `is_special_activation_swap(piece_a, piece_b) -> bool` | § Detailed Rules 5 | ✓ exact |
| 2 | `resolve_special_activation_clears(piece_a, piece_b) -> Set[cell]` | § Detailed Rules 5 | ✓ exact |
| 3 | `resolve_special_spawns(runs, swap_anchor_cells) -> Map[cell, SpecialSpawn]` | § Detailed Rules 2 & 4 | ✓ exact (correctly uses the Revision-2 `SpecialSpawn{special_type, color}` composite, not the pre-fix `Map[cell, special_type]` shape) |
| 4 | `expand_special_chain_reaction(cleared_set) -> Set[cell]` | § Detailed Rules 6 | ✓ exact |

No seam is extended, narrowed, or reinterpreted. This document also
correctly avoids requesting Board Engine's Open-Questions-logged "v2
composite spawn+clear seam" for the Bomb+Striped combo, redesigning Formula
7 to fit the existing `Set[cell]` return type instead — confirmed a genuinely
smaller, compliant implementation, not a workaround masking a real gap.
`swap_anchor_cells` empty-for-cascade-step-2+ handling (Formula 2) is
correctly pinned to Board Engine's Revision-2 resolution. **No gap found.**

### Formula Verification (worked examples recomputed by hand)

All nine formulas recomputed independently:

- **Formula 1** (spawn type/color mapping) — categorical, no arithmetic to
  verify; correct.
- **Formula 2** (anchor resolution) — both worked examples reproduced
  exactly: `mid_offset(4)=1`, `run_middle_cell=(5,3)` / `(2,2)`; overlap
  tie-break distances correct.
- **Formula 3** (cluster precedence) — both worked examples' cell-count
  arithmetic confirmed (`4+3-1=6`, `3+3-1=5`).
- **Formula 4** (Bomb+Color) — `13+1=14` exact case ✓; `E[monochrome]=64/5=
  12.8` ✓.
- **Formula 5** (Bomb+Bomb) — trivial, `64` cells ✓.
- **Formula 6** (Striped+Striped) — all three table rows (8/16/15 cells)
  independently re-derived from swap direction × orientation pairing; all
  correct, including the worked example (rows 2 and 3 disjoint → 16 cells).
- **Formula 7** (Bomb+Striped "jackpot") — recomputed the birthday-problem
  estimate independently: `(7/8)^12.8 ≈ 0.181` (verified via
  `ln(0.875)×12.8 = -1.7092`, `e^{-1.7092} ≈ 0.181`) → `8×(1-0.181)=6.55` →
  `×8 ≈ 52` cells (~82% of board). Matches the document exactly.
- **Formula 8** (passive target-color) — worked example's `argmax` (lemon,
  14) trivially correct.
- **Formula 9** (creation-frequency heuristic) — `p_accidental(4,5)=1/125=
  0.008` and `p_accidental(5,5)=1/625=0.0016` both confirmed.

**Zero arithmetic errors found in any formula.**

### Cross-Doc Consistency Checks (Focus Items)

1. **`collect_color`'s "single-count, match_cleared-only" rule** — verified
   `level-objectives.md` § Detailed Rules 4 independently derives the exact
   same non-double-counting argument this document's § Detailed Rules 9
   (Harvest Observation Point) anticipates, citing the same Board Engine
   overlap/union and seam-1-and-match-coexistence rules. Consistent.
2. **Level Data Format v2 pre-placed-specials ask** (§ Dependencies,
   `level-data-format.md` row) — verified against the current
   `level-data-format.md`: it already anticipates exactly this extension
   point verbatim ("Extend `pre_placed_pieces` entries with an optional
   `special_type` field... once that vocabulary exists," Out-of-Scope
   table). This document's concrete proposal (an optional
   `special_type: String` field, `{"striped_h","striped_v","color_bomb"}`,
   no `schema_version` bump required) is a correctly-scoped, ready-to-apply
   answer to that anticipated row. Not a contradiction — a legitimate
   forward ask, correctly deferred (not edited here, out of this document's
   file-edit scope).
3. **RNG usage claim ("zero draws at MVP")** — cross-checked against
   `rng-service.md`'s Stream Registry: `special-drop` is `stream_id 2`,
   status "Reserved (MVP infra ready)... `special-candies.md` owns what it
   draws and why." This document's confirmation ("nothing, at MVP") closes
   that open item cleanly, consistent on both sides.

### Pillar 2 / Nondeterminism Audit (Focus Item)

Walked every rule for hidden RNG or invisible skill-reading: creation
eligibility (pure function of run length/orientation), anchoring (pure
function of run geometry + swap cells), cluster precedence ties (fixed,
documented tie-break order — swap-anchor preference, then lexicographic
cell), and passive bomb detonation's target-color rule (deterministic
argmax over live board state, fixed lowest-index tie-break). **No RNG
consumption and no player-skill/session-state input found anywhere in this
document** — confirmed consistent with its own § Detailed Rules 8 claim and
with `rng-service.md`'s Honest Randomness Contract.

### Fixes Applied Directly (mechanical, per fix policy)

1. **Stale Dependencies rows (Scoring & Level Objective).** Both rows
   speculated (pre-authoring) that the downstream document would consume
   "`special_activated`/`match_cleared`'s `cleared_pieces`" for point/tally
   purposes. Now that both documents are authored, each **explicitly and
   deliberately reads `match_cleared` only**, excluding `special_activated`
   specifically to avoid double-counting (`scoring-stars.md` § Detailed
   Rules 1; `level-objectives.md` § Detailed Rules 4). Updated both rows to
   state the now-confirmed reality, updated their status from "not yet
   authored" to "Draft," and closed the "Reciprocal note" language (both
   downstream documents do in fact list this document in their own
   Dependencies tables — confirmed).

### Required Before Implementation

None. This document is implementation-ready as written.

### Recommended Revisions (Advisory)

1. **[Cross-doc, low priority]** `juice-layer.md`/`booster-brewing.md`
   "not yet authored" references are stale for `juice-layer.md` specifically
   (it is now Draft). Recommend a batch `/consistency-check` pass across all
   Phase-MVP GDDs once the full set has been authored, rather than a
   piecemeal fix here.
2. **[Already logged by the document itself, reconfirmed valid]** The
   deliberate MVP scope cut on solo Striped swap-activation and the passive
   Color Bomb detonation policy are both design decisions, not
   implementability gaps — both are already correctly flagged in this
   document's own Open Questions for a Vertical Slice revisit. No new
   finding; reconfirmed as sound reasoning on recomputation.

### Nice-to-Have

- The "Ingredient-Harvest Observation Point" (§ Detailed Rules 9) is a
  clean example of naming a future integration point without designing it
  — worth reusing as a template elsewhere in this project.

### Scope Signal

Rough scope signal: **L** (multi-system integration — 4 board-engine seams,
9 formulas, 3 forward dependencies; no new ADR required). Producer should
verify before sprint planning.

### Verdict: APPROVED

Blocking items: 0 | Recommended (advisory): 2 | Nice-to-have: 1
Mechanical fixes applied this pass: 2 (stale Dependencies rows for Scoring
and Level Objective)
Prior verdict resolved: First review (N/A)

**Summary**: This document is implementation-ready. All four Board Engine
extension seams are implemented exactly per Board Engine's APPROVED
Revision-2 signatures, all nine formulas recompute correctly by hand with
zero arithmetic errors, the RNG/determinism claim holds under audit, and
both forward cross-doc asks (Level Data Format v2, Scoring/Level Objective
signal consumption) are legitimate, correctly-scoped, and now independently
confirmed by the two now-authored downstream documents. Two stale
Dependencies rows (written before Scoring/Objectives existed) were corrected
in place. No blocking issues found.
