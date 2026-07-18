# Design Review Log: Juice Layer — VFX & Audio Hooks

Target document: `design/gdd/juice-layer.md`

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
Tuning Knobs, Acceptance Criteria — all present, plus Cross-References and
Open Questions.

### Dependency Graph

Declared "Depends On": Match-3 Board Engine (APPROVED), Special Candies &
Combo Matrix (header said "not yet authored" — **stale**, actually Draft,
fixed), Scoring & Star Thresholds (same staleness, fixed), Touch & Input
System (APPROVED), Save & Persistence (Draft), Game UI/Screens Flow (Draft).
All referenced systems exist on disk; no broken references remain after
this pass's fixes (3 additional stale "not yet authored" mentions found and
fixed in body text at §5 and §11, and in the Dependencies table).

### Fixes Applied Directly (mechanical reconciliation)

1. **Formula 1 arithmetic error (Inter-Step Beat Deceleration Curve).**
   Independently recomputed `inter_step_beat_ms(20) = 60 + 160 × 0.72¹⁹`
   three ways (direct 19-step iterative multiplication, repeated-squaring
   decomposition `0.72^19 = 0.72^16 × 0.72^2 × 0.72^1`, and a
   logarithm/exponent cross-check) — all three converge on
   `0.72¹⁹ ≈ 0.0019468`, giving `60 + 160×0.0019468 ≈ 60.31ms`, **not**
   the document's stated `≈60.16ms`. This is a genuine, reproducible
   arithmetic error, not a rounding-convention difference (the gap is
   `0.15ms`, well outside any plausible rounding tolerance). Fixed in
   both locations: the Formula 1 worked example, and the matching
   Acceptance Criteria regression-pin bullet (`≈60.16` → `≈60.31`). This
   does **not** change the document's qualitative conclusion ("already
   indistinguishable from the floor by the time a cascade reaches Board
   Engine's own defensive depth cap") — `60.31ms` vs. the `60ms` floor is
   still sub-millisecond, imperceptible in practice.
2. **Stale cross-references** (5 locations): Special Candies & Combo
   Matrix and Scoring & Star Thresholds both now exist as Draft; updated
   the header and 4 body/table mentions that still said "not yet
   authored."
3. **Fizz mount point list incomplete.** §11's "Hard boundary — Fizz never
   renders on the live board" paragraph listed only Pre-Level Card,
   Results Win, Results Lose as Fizz's mount points — omitting World Map,
   which both `characters-and-tone.md` and `screen-flow.md` §8 list as a
   fourth (idle-companion-only, no line pool at MVP) mount point. The
   omission didn't break the paragraph's actual claim (World Map is also
   a non-board screen, so the "never on the live board" rule held
   regardless), but it was an incomplete enumeration relative to the two
   source documents. Fixed — added World Map with its MVP scoping note.
4. Status header updated to this verdict.

### Formula Verification (worked examples recomputed by hand)

- **Formula 1**: chain_index 1/2/4 all verified correct
  (220, 175.2, ≈119.7); chain_index=20 had the error above (fixed).
- **Formula 2** (fall duration + total presentation time): worst-case
  clamp (`45×8+80=440→400`) ✓; 4-link cascade worked example recomputed
  step-by-step (`fall_reveal_ms(3)=215` for all 4 steps; beats from
  Formula 1 summed with `MATCH_POP_DURATION_MS=200` per step: `635.0 +
  590.2 + 557.9 + 534.7 = 2317.8`; `total = 150+2317.8+150 = 2617.8ms`) —
  every intermediate and final number matches the document exactly.
- **Formula 3** (pitch escalation): `chain_index=1→0 semitones`,
  `=4→6 semitones (×1.414, verified √2≈1.41421)`, `=7→12 semitones
  (×2.0, cap binds for the first time)`, `=10→12 (still capped, uncapped
  would be 18)` — all verified correct.
- **Formula 4** (particle LOD): full worked example (`n_cells=81,
  chain_index=8`) recomputed end to end — `chain_boost=2.05`,
  `requested_particles=1328.4→1328`, `estimated_draw_calls=ceil(66.4)=67`
  (LOD Tier 2, 50%), `final_particles=664` (normal) / `332` (reduced
  motion), post-degradation draw-call check `ceil(664/20)=34 < 40` — all
  match exactly.
- **Formula 5** (input-lock composition): see Contract Coherence below —
  substantively revised, not merely verified.
- **Formula 6** (flash-safety): `pulse_gap_ms = (45+80)+60 = 185`,
  `pulse_frequency_hz = 1000/385 ≈ 2.6 ≤ 3` — verified correct.

5 of 6 formulas verified correct with zero changes (Formulas 2–4, 6, and
Formula 1's first three worked-example values); Formula 1's
`chain_index=20` case had a genuine arithmetic error (fixed); Formula 5
required a substantive cross-document merge (fixed, detailed below).

### Contract Coherence — Task Focus Items

**Shadow Board Model vs. Board Engine's deferred-replay guarantees — is
every event the Shadow Board Model needs actually in the signal catalog
with sufficient payload?** Traced every Reveal Step in §3's table back to
its source Board Engine signal(s): `pieces_spawned(source=BOOTSTRAP)`,
`match_cleared.cleared_pieces`, `special_spawned`,
`pieces_spawned(source=CASCADE_REFILL)` all carry full `PieceSnapshot`
identity per `board-engine.md`'s Revision 2 payload-sufficiency guarantee
(independently confirmed against that document's own worked 2-step-cascade
walkthrough). Gravity itself needs no dedicated signal — it is re-derived
deterministically from Board Engine's own published, static-per-level
segment/compaction algorithm. `board_reshuffled`'s payload
(`attempts_used` only) is the one exception, resolved via a single
justified live query, contingent on the input-lock guarantee below. **Net
finding: yes, every event the Shadow Board Model needs is present with
sufficient payload**, contingent only on the input-lock composition fix
below actually landing (it now has).

**`piece_id` gap — adjudicated, confirmed advisory, not blocking.** This
document's own Cross-References already flagged the gap between
`board-engine.md`'s stated `piece_id` rationale ("so the Juice Layer can
animate one persistent visual object through gravity moves") and the
actual `PieceSnapshot` schema (`{cell, color, special_type}` — no
`piece_id`), and had already concluded it was non-blocking. Independently
re-derived this conclusion rather than accepting it at face value: gravity
compaction (`board-engine.md` § Detailed Rules 8–9) preserves relative
order within a segment — pieces never reorder, only shift toward the
segment's own bottom. Because the Shadow Board Model already maintains an
*ordered* list of a segment's occupants (not a bag), position-preserving
compaction alone is sufficient to determine "which old cell's occupant
maps to which new cell" with zero ambiguity, even for two pieces sharing
the same `color`/`special_type`. A persistent ID is therefore never needed
for *correctness* — only for the *implementation nicety* of reusing one
continuous sprite/node object across a fall rather than recreating it at
its landing position. Confirmed: **advisory, not blocking** — matches
this document's own prior self-assessment; added one clarifying sentence
to the Cross-References row documenting the independent re-derivation
rather than treating it as merely restated.

**Input-lock composition — the central finding of this pass (shared with
`screen-flow.md`'s review; see that log for the full write-up).** This
document's own §10/Formula 5 explicitly stated its 3-term extension to
`screen-flow.md`'s Formula 5 (`AND NOT juice_input_lock`) was "a
recommended follow-up to `screen-flow.md`, not made there, per this
document's own file-edit scope." Verified this was true as of the start of
this review: `screen-flow.md`'s actual Formula 5 had 2 terms
(`base_state==GAMEPLAY AND board_engine_ready AND NOT overlay_is_active`),
while this document's own Formula 5 had 3 different terms (missing
`base_state` entirely, and using the symbol `board_engine_ready` rather
than `board-engine.md`'s and `touch-input.md`'s actual term,
`board_input_enabled`). Two documents, same formula name, two genuinely
different definitions — a real composition gap, not merely a documentation
staleness issue. **Fixed directly in both documents** (both in this
review's scope): `screen-flow.md`'s Formula 5 now canonically defines
`effective_board_input_enabled` as a 4-term composition including
`juice_input_lock`; this document's own Formula 5 was updated to match
exactly (renamed `board_engine_ready`→`board_input_enabled`, added the
`base_state==GAMEPLAY` term, added a note that this section now restates
the canonical formula rather than proposing an unmade extension), and its
own worked example and Acceptance Criteria bullet were updated to match.
This closes the loop this document itself flagged as open, and
retroactively validates the reshuffle live-query safety argument in §2
(which explicitly depended on the input-lock guarantee actually holding
end-to-end) — marked Resolved in Cross-References and Open Questions.

**Combo-slot cross-check (§11 Declared Seams vs. `special-candies.md`'s
actual combo matrix).** Independently read `special-candies.md`'s Swap-
Triggered Combo Matrix (§5): exactly 4 cells at MVP (Bomb+regular,
Bomb+Bomb, Bomb+Striped "jackpot," Striped+Striped), with `WRAPPED`
explicitly reserved/structurally unreachable until Vertical Slice.
Cross-checked against this document's §11 combo-slot table
(`combo_stripe_stripe`, `combo_stripe_bomb`, `combo_bomb_bomb` at MVP;
three `WRAPPED` combos at Vertical Slice) — the three MVP special-×-special
combo slots match `special-candies.md`'s three special-×-special combos
exactly (Bomb+regular is a single-special activation, not a "combo" slot,
correctly excluded from this table), and the three Vertical Slice slots
correctly anticipate `WRAPPED`'s eventual arrival. No drift found; noted
the confirmation in the Dependencies table row.

### Required Before Implementation

None remaining — the one genuinely blocking-grade issue found (the input-
lock composition gap) was resolved directly in this pass, and the Formula 1
arithmetic error was corrected.

### Recommended Revisions (Advisory)

1. **[Cross-doc]** `board-engine.md` should add `piece_id` to
   `PieceSnapshot` at its next revision, closing the quality-of-
   implementation gap between its own stated rationale and its actual
   schema (already tracked in this document's Open Questions).
2. **[Content]** The cascade-callout placeholder copy ("Sweet!" /
   "Delicious!" / "Spectacular!") should be authored to final quality by
   `narrative-director` before MVP content lock (already tracked in this
   document's own Open Questions).

### Nice-to-Have

- The Reveal Queue / Replay Scheduler model (§3) — capturing Board
  Engine's synchronous signal burst atomically, then draining it at a
  separately-tuned tempo — is a clean, testable translation of
  `board-engine.md`'s "logic resolves instantly; presentation paces
  reveals" contract into a concrete data structure with its own
  regression-pinned Acceptance Criteria. Good reference pattern for any
  future presentation-layer system consuming a synchronous event burst.
- The LOD degradation ladder (Formula 4) stacking two independent
  multiplicative reductions (performance LOD, accessibility preference)
  rather than treating either as authoritative is a well-reasoned design
  choice, and the worked example against Board Engine's own documented
  worst case (81-cell full-board clear) is a genuinely useful stress test,
  not just an illustrative number.

### Senior Verdict [game-designer, lean-mode self-review]

This document does real, careful work translating a synchronous logic
contract into a paced presentation layer, and it is unusually good at
citing its own limits precisely (the reshuffle live-query exception, the
`piece_id` gap, the LOD ladder's "heuristic, not exact enumeration"
caveat). The one genuine defect found — a real arithmetic error in
Formula 1's deepest-cascade worked example — was independently
reproducible three separate ways and has now been corrected in both
places it appeared. The more significant finding was structural, not
arithmetic: this document had already correctly diagnosed and designed
the fix for the three-way input-lock composition gap, but had explicitly
declined to make it, citing file-edit scope. Because this review has write
authority over both this document and `screen-flow.md`, the correct action
was to close that loop rather than leave it as a standing "recommended,
not made" note — which is what was done. The `piece_id` adjudication this
document performed on itself held up under independent re-derivation.

### Scope Signal

Rough scope signal: **L** (6 formulas, a full presentation-layer state
model with its own queue/scheduler, 6 forward/mutual GDD dependencies, and
a 4-way cross-document signal composition it now shares canonical
ownership of with `screen-flow.md`).

### Verdict: APPROVED

Blocking items: 0 (the one blocking-grade issue — the input-lock
composition gap — was resolved directly in this pass) | Recommended
(advisory): 2 | Nice-to-have: 2
Mechanical fixes applied in this pass: 1 arithmetic error (2 locations),
5 stale cross-references, 1 incomplete Fizz mount-point list, Formula 5
substantively revised (4-term composition, symbol rename, worked example
and AC update, Cross-References/Open Questions marked Resolved)
Prior verdict resolved: First review (N/A)
