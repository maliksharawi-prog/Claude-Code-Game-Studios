# Epic: Content — 10 MVP Levels, Manifest & Balance

> **Epic ID**: E10
> **Layer**: Content
> **GDD**: `design/gdd/level-data-format.md` (schema authored against) · `design/gdd/scoring-stars.md` (star thresholds) · `design/gdd/world-map.md` (node list)
> **Architecture Module**: `assets/data/levels/<region_code>/*.asset` (authored LevelData ScriptableObjects) + `assets/data/level_manifest.asset` (generated) — exercises the E02 schema, validation, and manifest tool on real content
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories content-levels-manifest`

## Scope

The 10 hand-authored MVP levels (one region) that make the game playable and worth returning
to, plus the generated manifest and the balance pass that ties star thresholds to the scoring
formulas. Authors 10 `LevelData` ScriptableObjects (grid layout, candy palette, objective
config, move limit, blockers, star thresholds) under the E02 schema, runs the V1–V19 validation
suite + V8 flood-fill on each, generates the append-only `level_manifest.asset` via the E02
tool, runs W6–W7 cross-validation over the real content, and validates each level's 1/2/3-star
thresholds against `REFERENCE_SCORE_PER_MOVE(K)`. This epic **owns no TR-IDs** — TRs define the
*format, validation, and scoring systems* (E02/E04); this epic is the *content* authored against
them and the proof those systems hold on real data.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-006: Level Manifest Generation & Cross-Validation | Run the generation tool + W6–W7 over the 10 authored levels; append-only ordinals | LOW |
| ADR-002: Addressables | The 10 LevelData assets are addressable by `level_id` (address==level_id, A2/A3) | LOW |
| arch §6 | LevelData schema shape the assets conform to | — |

## TR-IDs Owned

- **None.** This epic consumes E02 (LevelData format, validation, manifest tool) and E04
  (scoring/star thresholds). Its value is authored content + the balance validation that proves
  those systems on real data, not a distinct technical requirement.

## Depends On

- **E02** (LevelData schema, `Validate()` / V1–V19, manifest generation tool + W6–W7).
- **E03** (BoardModel to validate each level is playable/solvable and reshuffle-safe).
- **E04** (scoring/star thresholds to balance the 1/2/3-star targets against).

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **LOW** — authoring ScriptableObjects in the Inspector and running the manifest generation
  tool use stable Editor APIs (`AssetDatabase`, `IPreprocessBuildWithReport`) — not post-cutoff.
- **Content watch (systems-index):** the Match-3 Board Engine feel ceiling was validated only in
  HTML/CSS in the prototype — these 10 levels are the first real in-engine content and feed the
  Vertical Slice feel checkpoint. Keep levels seed-reproducible (RNG session) so balance is
  deterministic.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- 10 `LevelData` assets exist under `assets/data/levels/<region>/`, each passing the full
  V1–V19 validation suite (incl. V8 flood-fill connectivity) with zero blocking failures.
- `level_manifest.asset` is generated with append-only ordinals; W6–W7 cross-validation passes
  (every authored `level_id` ↔ manifest ordinal, no orphans).
- Each level's star thresholds validate against `REFERENCE_SCORE_PER_MOVE(K)`; a smoke check
  confirms all 10 are completable and their 3-star target is achievable.
- Balance evidence recorded in `production/qa/` (smoke check); level-data-format.md,
  scoring-stars.md, and world-map.md content-facing ACs are met.

## Next Step

Run `/create-stories content-levels-manifest`.
