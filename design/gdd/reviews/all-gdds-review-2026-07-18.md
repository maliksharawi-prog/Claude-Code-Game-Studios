# Holistic Cross-GDD Review — 2026-07-18

*Method: /review-all-gdds, autonomous — Phase 2 (Consistency, checks 2a–2f) and
Phase 3/4 (Design Theory + Scenario Walkthroughs, checks 3a–3g) run as parallel
reviewers over all 11 MVP-tier GDDs, followed by a same-day reconciliation pass.*
*Registry caveat: `entities.yaml` held only 2 constants at review time — checks
ran on full-text reads. Recommend `/consistency-check` to populate the registry.*

## Verdict

| Stage | Verdict |
|---|---|
| Phase 2 (Consistency) at review time | **FAIL** — 1 blocking, 8 warnings |
| Phase 3/4 (Design Theory + Scenarios) at review time | **CONCERNS** — same 1 blocking (independently found), 5 warnings, theory sound |
| After reconciliation pass (commit `c5384e0`) | **PASS — cleared for /create-architecture** |

## The blocking finding (both passes converged)

**No document called `Save & Persistence.record_level_completion()`** — as
literally specified, a won level was never persisted; the Results screen would
celebrate a "New Best!" that silently reverted on relaunch. Three sibling docs
had each flagged the gap without closing it. **Resolved**: `level-objectives.md`
§Detailed Rules 9 now calls it on WIN (never LOSE), strictly before emitting
`level_resolved`, with acceptance tests; resolution rows closed in
save-persistence / screen-flow / world-map.

## Reconciliation ledger (all applied, commit `c5384e0`)

1. ✅ BLOCKING: win-persistence call + Dependencies row + ACs (level-objectives) + 3 resolution rows
2. ✅ DECISION: T15/T16 Results transitions wait for `juice_input_lock` clear; `RESULTS_TRANSITION_SAFETY_CEILING_MS = 15,000` (≥ juice worst-case ≈14.1s) — protects the final-move jackpot cascade
3. ✅ level-data-format: +Touch Input, +Screen Flow dependent rows
4. ✅ board-engine: +World Map dependent row (W6–W7 manifest cross-validation)
5. ✅ save-persistence: +Juice Layer dependent row (Settings reader)
6. ✅ touch-input: +Juice Layer row; §4 gate description updated to the ratified 4-term composition; `cell_size_px` source attributed to board-engine Formula 4
7. ✅ level-data-format: RSPM superseded by scoring-stars Formula 4 table (K=3→255, K=4→185, K=5→160); safe-range ceiling 250→260; V18 keyed to RSPM(K)
8. ✅ scoring-stars Edge Cases: bootstrap-instant-WIN path added (win at moves_used=0, final_score=0, stars=0)
9. ✅ rng-service Edge Cases: attempt_number resets to 1 on relaunch (screen-flow §7 ownership); bug-repro honesty note
10. ✅ special-candies §9: REQUIRED brewing constraint — ingredient yield capped/curved, never 1:1 of `cleared_pieces` (Bomb+Bomb reports 64 cells)
11. ✅ level-data-format Open Questions: saw-tooth pacing validation decision logged for Vertical Slice

## Tracked, deliberately not fixed now

| Item | Owner / When |
|---|---|
| Bomb-combo pricing (1,640/swap) vs greedy-bot-calibrated star_3 — one jackpot can trivialize 3-star bar | Empirical recalibration at Vertical Slice (scoring-stars Open Questions) |
| `rng_seed` authored-fixed-seed field has no production API in rng-service | Phase 3 daily-challenge work |
| Brewing must feed (not compete with) the star loop | booster-brewing.md authoring gate, Phase 2 |
| Godot residue in GDD engine notes (aggregated: Resources/.tres, signals, gdUnit4, user://, particles) — no design depends on a Godot-only capability | Swept by /create-architecture per ADR-001 |
| Registry population | /consistency-check after architecture |

## Design-theory highlights (no action needed)

Single star-based progression currency (no loop competition) · decision-load and
feedback-load time-separated by `juice_input_lock` · specials ladder is healthy
transitive escalation, not a dominant strategy · honest difficulty structurally
enforced (no performance-reading RNG anywhere) · zero anti-pillar violations ·
all 11 Player Fantasy sections converge on one identity. Pillars 3/4 are
dormant at MVP by documented scope-tiering, not drift. Scenario walkthroughs:
final-move jackpot trace CLEAN post-fix; force-quit/relaunch trace CLEAN
("derive everything, store nothing" held across 4 docs).
