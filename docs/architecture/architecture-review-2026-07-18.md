# Architecture Review Report — Sweet Cascade

> Skill: `/architecture-review` (full mode, lean/autonomous) · Agent: technical-director
> Date: 2026-07-18 · Engine: Unity 6.3 LTS (6000.3.x) · C#

---

## Verdict: CONCERNS

**Foundation coverage is complete and internally coherent, but one genuine cross-ADR
contradiction (ADR-002 vs ADR-006, manifest load path) must be reconciled before the
BootLoader slice is coded.** No GDD technical requirement is left without an architectural
home; the five Foundation ADRs the master architecture mandated before coding (ADR-A…E)
all exist and are Accepted; the dependency graph is acyclic; every ADR's engine
assumptions check out against the pinned-version reference. The CONCERNS verdict is driven
by real-but-narrow issues, not by uncovered scope: one blocking-for-one-slice load-path
conflict, one latent tombstone-awareness gap in a validation hook, one stale seam
signature in the master doc, and two Presentation-tier requirements still awaiting their
deliberately-deferred feature ADRs. Most Foundation slices (RNG, Save codec, Event
catalog, Manifest generation) can proceed today.

---

## Inputs Loaded

- **GDDs reviewed:** 11 (all APPROVED 2026-07-18; cross-review `design/gdd/reviews/all-gdds-review-2026-07-18.md` = PASS)
- **ADRs reviewed:** 6 — ADR-001 (Engine Selection), ADR-002 (Addressables = arch §11 ADR-A),
  ADR-003 (Save serialization = ADR-B), ADR-004 (Deterministic RNG = ADR-C),
  ADR-005 (Event Bridge = ADR-D), ADR-006 (Level Manifest = ADR-E) — all Accepted 2026-07-18
- **Master architecture:** `docs/architecture/architecture.md` v1.0
- **Engine reference:** `docs/engine-reference/unity/` (VERSION, breaking-changes, deprecated-apis,
  plugins/addressables, current-best-practices)
- **Standards:** `.claude/docs/technical-preferences.md`; reference blueprint `visual-interface-blueprint.md`
- **`docs/consistency-failures.md`:** absent — no prior conflict-pattern history to fold in; no reflexion-log append performed.
- **Engine-specialist consultation (skill Phase 5):** SKIPPED — autonomous/no-subagent mode. The
  engine audit below is self-performed against the reference library; a `unity-specialist` second
  opinion is a recommended (non-blocking) follow-up before `/gate-check`.

---

## Traceability Summary

| Metric | Count |
|---|---|
| Total technical requirements | **42** |
| Covered (existing ADR or a master-architecture §§4–9 decision) | **40** |
| Partial (architectural home exists; concrete tech choice pending a deferred ADR) | **2** |
| Gaps (no architectural home at all) | **0** |

- All 13 Foundation/Core requirements are covered by an **Accepted ADR (002–006)**.
- The 2 Partials are both **Presentation-tier** (TR-jl-003 audio, TR-jl-004 particles) and are
  pending ADR-G / ADR-H, which the architecture correctly scheduled as "should-have before the
  relevant system is built," **not** before Foundation coding.
- Zero uncovered requirements → the FAIL trigger ("Foundation/Core requirement uncovered") does not fire.

---

## Traceability Matrix

Coverage key: ✅ Covered · ⚠️ Partial · ❌ Gap. "arch §" = a master-architecture decision the
architecture deemed sufficient without a dedicated ADR (and scheduled none).

| Req ID | GDD | Requirement (abbrev.) | Coverage | Status |
|---|---|---|---|---|
| TR-rng-001 | rng-service | Named independently-seeded streams; isolation | ADR-004 | ✅ |
| TR-rng-002 | rng-service | Platform-stable F1–F3 + mix32; **not** System.Random | ADR-004 | ✅ |
| TR-rng-003 | rng-service | next_float/int/color/shuffle, fork_stream, session log | ADR-004 | ✅ |
| TR-rng-004 | rng-service | level_id as resolved int (no runtime string-hash) | ADR-004 + ADR-006 | ✅ |
| TR-ldf-001 | level-data-format | Versioned schema, closed enums, migration policy | arch §6 (LevelData POCO) | ✅ |
| TR-ldf-002 | level-data-format | Inspector-editable, one file/level, region subdirs | ADR-002 + arch §6 | ✅ |
| TR-ldf-003 | level-data-format | V1–V19 validation suite; flood-fill V8 | arch §6 `Validate()` + ADR-006 (W8) | ✅ |
| TR-ldf-004 | level-data-format | Additive `display_name` (v1.1, no bump) | arch §6 / GDD schema | ✅ |
| TR-be-001 | board-engine | Headless synchronous deterministic state machine | ADR-005 (MoveResolver) + arch §7.1 | ✅ |
| TR-be-002 | board-engine | Four extension seams; no-op MVP defaults | arch §8.1 `ISpecialResolver` | ✅ |
| TR-be-003 | board-engine | Full event catalog w/ PieceSnapshot; input-enabled event | ADR-005 (catalog) | ✅ |
| TR-be-004 | board-engine | Manifest String→ordinal; termination caps | ADR-006 + arch §6 (caps) | ✅ |
| TR-be-005 | board-engine | Segment-scoped gravity/refill; reshuffle | arch §7 / board-engine GDD | ✅ |
| TR-sc-001 | special-candies | Combo matrix + F8 passive detonation via 4 seams | arch §8.1 seam contract | ✅ |
| TR-sc-002 | special-candies | Append-only `special_type`; WRAPPED=4 reserved | ADR-005 (`SpecialType` enum) | ✅ |
| TR-sc-003 | special-candies | Harvest Observation Point (per-color identity on clear) | ADR-005 (`PieceSnapshot`) | ✅ |
| TR-ss-001 | scoring-stars | step_score from match_cleared only; bonus; chain mult | ADR-005 (D3 ScoreKeeper) | ✅ |
| TR-ss-002 | scoring-stars | `long` accumulator; REFERENCE_SCORE_PER_MOVE(K) | ADR-005 (long) + arch §2 | ✅ |
| TR-ss-003 | scoring-stars | Pull API get_current_score / get_score_results | arch §8.3 + ADR-005 | ✅ |
| TR-lo-001 | level-objectives | Tracker registry, normalization, handler map | arch §6 (ObjectiveEvaluator) | ✅ |
| TR-lo-002 | level-objectives | Win/lose only at board_stabilized; last-move-cascade win | ADR-005 (D3/D4) | ✅ |
| TR-lo-003 | level-objectives | ResultsData; level_resolved; persist-before-event on WIN | ADR-005 (D4) | ✅ |
| TR-lo-004 | level-objectives | Move accounting on swap_accepted; progress events | ADR-005 (D3) | ✅ |
| TR-ti-001 | touch-input | Gesture→intent; swipe threshold + dominant-axis | arch §6 (InputRouter) | ✅ |
| TR-ti-002 | touch-input | Unified touch+mouse pointer; single-active-touch | arch §6 | ✅ |
| TR-ti-003 | touch-input | Reads effective gate; ≤1-frame latency; ≥44px | ADR-005 (D5) + arch §8.5 | ✅ |
| TR-jl-001 | juice-layer | Capture-then-replay: Shadow Board + Reveal Queue | ADR-005 (D1/D3 Mode B) | ✅ |
| TR-jl-002 | juice-layer | Owns juice_input_lock; gravity re-derivation from events | ADR-005 (D5 term 4) | ✅ |
| TR-jl-003 | juice-layer | Audio hook map (pitch-shift), haptics, reduced-motion/WCAG | arch §6 — audio tech pending **ADR-G** | ⚠️ |
| TR-jl-004 | juice-layer | VFX sub-budget=40, 4-tier LOD, atlas single-material | arch §9.3 budget — particle tech pending **ADR-H** | ⚠️ |
| TR-sf-001 | screen-flow | 2-layer state machine, 9 composites, T1–T21 | arch §6 (ScreenFlowController) | ✅ |
| TR-sf-002 | screen-flow | Formula 5 four-term composition; owns overlay/attempt | ADR-005 (D5) | ✅ |
| TR-sf-003 | screen-flow | Results waits on juice lock w/ ceiling; retry skips card | ADR-005 (D3) + arch §7.3 | ✅ |
| TR-sf-004 | screen-flow | Per-screen data contract (reads Save/LevelData) | arch §6/§8.4 | ✅ |
| TR-sp-001 | save-persistence | JSON at persistentDataPath; canonical bytes; FNV-1a | ADR-003 | ✅ |
| TR-sp-002 | save-persistence | Atomic A/B double-buffer; ladder; tamper detect+accept | ADR-003 | ✅ |
| TR-sp-003 | save-persistence | Monotonic merge; self-healing total_stars; migration | ADR-003 | ✅ |
| TR-sp-004 | save-persistence | record_level_completion/load/get/update/flush API | ADR-003 | ✅ |
| TR-wm-001 | world-map | Region/node graph; star-gated unlocks; MVP placeholder | ADR-002 + ADR-006 + arch §6 | ✅ |
| TR-wm-002 | world-map | W6–W7 manifest cross-validation | ADR-006 | ✅ |
| TR-perf-001 | technical-preferences | 60fps/16.6ms; ≤100 draw calls; ≤400MB; blob shadows | arch §9 + ADR-002 (memory) | ✅ |
| TR-perf-002 | technical-preferences | Render Graph only; Input System only; Domain zero-UnityEngine | ADR-004 (CI guard) + arch §5 | ✅ |

---

## Cross-ADR Conflict Detection

Prior conflict-pattern history: none (`docs/consistency-failures.md` does not exist).

### 🔴 CONFLICT-1 (BLOCKING for the BootLoader/manifest-load slice) — ADR-002 vs ADR-006: manifest load path

- **Type:** Integration-contract / data-load conflict.
- **ADR-002 claims:** `level_manifest.asset` (LevelManifest SO) and `world_map_manifest.asset`
  (WorldMapManifest SO) live in the **`Core_Bootstrap` Addressables group**, loaded at cold
  boot via `loader.TryLoadByLabel("core")` and pinned for the session (Group Layout row 1;
  Cold-Boot step 2). A core-group load failure is classified FATAL.
- **ADR-006 claims:** the two manifests are loaded as **direct serialized references on the
  BootLoader/config object, explicitly NOT via Addressables** — "`plugins/addressables.md`
  explicitly says DON'T use Addressables for assets needed immediately at startup," and doing
  so also sidesteps the 6.2+ throw-on-failure (Decision §Load path; Implementation Guidelines;
  Verification Required).
- **Impact:** BootLoader (blocked on *both* ADRs) receives contradictory instructions for the two
  boot-critical assets it must have at boot step 6, before any board bootstrap. Implementing
  per ADR-002 violates ADR-006's explicit decision *and* the very engine guidance both ADRs cite;
  implementing per ADR-006 leaves ADR-002's core group and A3 assumptions describing assets that
  are no longer Addressable.
- **Engine-reference adjudication:** `plugins/addressables.md` — "DON'T use Addressables for
  assets needed immediately at startup (use direct references)." The manifests are startup-immediate.
  **The reference favors ADR-006.** ADR-006 is also the more specific decision (it owns manifest
  generation and load) and reached the correct conclusion from the shared guidance that ADR-002 read differently.
- **Resolution options:**
  1. **(Recommended)** Amend ADR-002: remove `level_manifest.asset` + `world_map_manifest.asset`
     from the `Core_Bootstrap` group; load both by direct serialized reference per ADR-006.
     `Core_Bootstrap` retains UI Toolkit shells + core SFX. ADR-002's A3 editor check reads the
     manifest asset directly at editor time and is unaffected by the runtime load path.
  2. Amend ADR-006 to make the manifests Addressable in `core` (rejected — contradicts the engine
     guidance and reintroduces the 6.2+ throw on a boot-fatal path).
- **We'll know this is resolved when:** exactly one of the two ADRs is amended (via a superseding
  revision, not a silent edit), and the BootLoader boot-sequence spec names a single load
  mechanism for the two manifests.

### 🟠 CONFLICT-2 (latent; does not fire at MVP) — ADR-002 A3 not tombstone-aware vs ADR-006 `retired_ordinals`

- **Type:** Validation-hook coherence.
- **ADR-002 A3 (blocking CI gate) claims:** "every `level_id` in `level_manifest.asset` has an
  addressable entry."
- **ADR-006 claims:** removed levels are **tombstoned** — their `level_id` stays in `entries`
  (append-only, never deleted) while their `LevelDataAsset` file is deleted (so it is no longer
  addressable). W6/W7 were explicitly refined to operate on **non-retired** entries.
- **Impact:** The first time a level is tombstoned, A3 as literally worded would **false-fail**
  the build (a retired `level_id` in `entries` has no addressable), directly contradicting the
  state ADR-006 defines as correct. Does not surface at MVP (10 levels, zero tombstones), but it is
  a latent contradiction between two Accepted blocking gates.
- **Resolution:** Scope A3 (and any A3-adjacent manifest↔catalog check) to **non-retired** entries,
  mirroring ADR-006's W6/W7 language. One-line clause in ADR-002; record it in the same amendment as CONFLICT-1.

### Compose-checks that PASSED (task-specified cross-ADR checks)

- **ADR-005 event catalog ⨯ ADR-004 IRngService ⨯ board-engine contracts — COMPOSE.** ADR-005's
  `SpecialType` enum and ADR-004's `StreamRegistry` stream IDs are orthogonal; `BoardModel` draws
  the `board-refill` stream from `IRngService` and emits the §7 catalog through `IBoardEventSink`.
  ADR-005's byte-identical ordering guarantee explicitly (and correctly) leans on ADR-004 for
  platform-stable draws. ADR-005 implements board-engine §7's 14-signal catalog verbatim + 3
  feature events. No contradiction.
- **ADR-003 Domain codec ⨯ ADR-004 purity denylist — CODEC PASSES.** ADR-003's `CanonicalJsonWriter`/
  `SaveJsonReader`/`Fnv1a32` live in `Assets/Domain/Save/` and use BCL only. Against ADR-004's L2
  denylist: no `System.Random`/`new Random(`, no `UnityEngine`/`UnityEditor`, no
  `DateTime.Now/UtcNow/Today` in Domain (timestamps are injected as `long nowUtc`), no `Stopwatch`/
  `Guid.NewGuid`/`Environment.TickCount`. The `\bfloat\b` rule is path-scoped to
  `Assets/Domain/Rng/**` and the save schema is integer-only regardless. **Clean pass.**
  Both ADRs deliberately share the **same FNV-1a-32 constants** (`0x811C9DC5` / `0x01000193`) —
  intentional and documented (ADR-004 Ordering Note).
- **ADR-002 address==level_id ⨯ ADR-006 bijection — CONSISTENT (for live levels).** Both key off
  the stable string `level_id`; address==level_id (A2) makes each file addressable by its id, and
  bijection maps each file to exactly one non-retired entry. They compose cleanly; the only seam is
  the tombstone case (CONFLICT-2).

### Minor: master-architecture §8.1 seam signatures stale vs ADR-005 (advisory)

ADR-005 (D2) removes `ScoreKeeper` from `BoardModel` (scoring now observes the `MoveResolver`
sink; scoring-stars Rev 2 prices activations from `MatchCleared.ClearedPieces`). But master
architecture §8.1 still shows `ISpecialResolver.ActivationClears(..., ScoreKeeper score)` and
`ExpandChain(..., ScoreKeeper score)` threading `ScoreKeeper` through the seams. ADR-005 is
authoritative; the §8.1 seam signatures should drop the `ScoreKeeper` parameter when the Board
Engine / Special Candies slice is specced. This is the same family as ADR-005's two flagged
blueprint supersessions (`Cell` record-struct replaces the hand-rolled `R*16+C` hash;
`OnSpecialConsumed`/`_pendingBonus` accumulator superseded by the read-from-`ClearedPieces` rule).

---

## ADR Dependency Order

All `Depends On` fields collected; topological sort below. **No cycles.**

```
Foundation (root):
  ADR-001  Engine Selection (Unity 6.3 LTS)         [Depends On: none]
  ADR-006  Level Manifest generation                 [Depends On: none — foundational]

Depend on ADR-001 only:
  ADR-002  Addressables grouping & load strategy     [ADR-001]
  ADR-003  Save serialization & durability           [ADR-001]

Depend on ADR-001 (+ soft/derived deps):
  ADR-004  Deterministic RNG & purity guard          [ADR-001; SOFT ADR-006 for the resolved int level_id]
  ADR-005  Event Bridge & BoardEvent catalog         [ADR-001; ADR-004 for the byte-identical clause]
```

- **No unresolved dependencies:** every referenced ADR (001, 004, 006) is Accepted. ADR-004's
  dependence on ADR-006 is explicitly a **soft** dependency (RNG takes the ordinal as an opaque
  `int` and does not require ADR-006 Accepted first) — so even the 006→004→005 chain has no hard-blocking edge.
- **No cycle:** 006 *enables* 004 (supplies the ordinal); 004 does not depend back on 006 hard;
  005 depends on 004; nothing points back to 006. Acyclic.

### Recommended implementation order (Foundation slices)

1. **ADR-006** (manifest ordinal) and **ADR-004** (RNG `board-refill`) — the first gameplay slice
   (Board Engine bootstrap) needs both on frame one.
2. **ADR-005** (event catalog) — the Board Engine slice cannot be end-to-end tested without it.
3. **ADR-003** (Save codec) — parallelizable; shares no files with the above (shares only the FNV-1a-32 primitive by value).
4. **ADR-002** (Addressables) — for level-load + BootLoader; **reconcile CONFLICT-1 first** so the
   BootLoader manifest-load spec is unambiguous.

---

## GDD Revision Flags (Architecture → Design feedback)

**None.** No HIGH-risk engine finding contradicts a GDD assumption. The GDDs are engine-neutral
by design, and the master architecture's §2 "Godot → Unity residue sweep" already discharged every
Godot-specific mechanism to a 6.3 equivalent with no lost design contract (the cross-GDD advisory's
"no design depends on a Godot-only capability" is confirmed). The one design-adjacent widening
(score `int → long`) is already the GDD's position (scoring-stars §9), not a revision. Systems index left unchanged.

---

## Engine Compatibility Audit

**Engine:** Unity 6.3 LTS (6000.3.x). **ADRs with a titled Engine Compatibility section: 5 / 6**
(all of 002–006; ADR-001 carries its engine notes in Consequences, not a titled section).

- **Version consistency:** every ADR that names a version says Unity 6.3 LTS (6000.3.x). No stale-version references.
- **Post-cutoff API claims — all verified against the reference library:**
  - Addressables 6.2+ **throw-on-failure** (ADR-002) — confirmed (`breaking-changes.md` MEDIUM;
    `plugins/addressables.md`). The `IContentLoader.TryLoad` wrap that converts the throw into a
    discriminated result is the correct mitigation.
  - "Don't use Addressables for startup-immediate assets" (ADR-006 direct-reference manifest load)
    — confirmed (`plugins/addressables.md`). *This same line adjudicates CONFLICT-1 in ADR-006's favor.*
  - C# 9 `record` / `readonly record struct` (ADR-005) — confirmed (`current-best-practices.md`,
    VERSION.md "C# 9 support"). ADR-005's "verify the Domain asmdef compiles the catalog headlessly" is appropriate.
  - `Application.persistentDataPath` + `FileStream.Flush(true)` + WebGL IDBFS sync (ADR-003) —
    BCL/engine-stable; the WebGL flush primitive is correctly listed as Verification-Required, not assumed.
  - RNG (ADR-004) — **zero** post-cutoff surface by construction (pure BCL integer arithmetic); LOW risk is accurate.
  - `AssetDatabase.FindAssets` / `IPreprocessBuildWithReport` (ADR-006) — stable editor APIs, not post-cutoff.
- **Deprecated-API scan:** no ADR references any API in `deprecated-apis.md`. The architecture and
  ADRs explicitly avoid the deprecated paths (legacy `Input` class, UGUI `Canvas`/`Text`,
  `Resources.Load`, `Execute(ScriptableRenderContext, ref RenderingData)`, legacy Particle System)
  and route to the current replacements.
- **Post-cutoff API conflicts between ADRs:** none. ADR-002 (uses Addressables) and ADR-006
  (deliberately avoids it for manifests) do **not** disagree about the API's *behavior* — both
  agree it throws in 6.2+; they disagree only about the load *mechanism* for the manifests, which
  is CONFLICT-1 (a design conflict, not an engine-fact conflict).

---

## Architecture Document Coverage (Phase 6)

- **All 11 MVP systems** from `systems-index.md` appear in the architecture's layer map (§4) with
  clear ownership (§6). No orphaned architecture.
- **Out-of-MVP systems** (#12 Booster Brewing = Phase 2; #13 Events/Theming, #14 Social, #15
  Backend & Accounts = Phase 3) are correctly absent from MVP layers; the Phase-3 backend seam is
  reserved (not implemented) as ADR-I. Not orphaned — deferred by design.
- **Required-ADR completeness vs the first implementation slice:** architecture §11's
  "Must have before coding starts (Foundation & Core)" list = ADR-A/B/C/D/E, **all now written and
  Accepted** as ADR-002/003/004/005/006. The "Should have (Feature & Presentation)" ADRs (F UI-popups,
  G Audio, H Particles) and the "Can defer" ADRs (I analytics, J CI, ADR-001 retrofit) are **not**
  prerequisites for Foundation work. **Confirmed: none of F–J blocks Foundation coding.** The two
  ⚠️ Partials above are exactly the F/G/H-pending items and are Presentation-tier.

---

## Blocking Issues (must resolve before a clean PASS)

1. **Reconcile CONFLICT-1** (manifest load path, ADR-002 vs ADR-006) before the BootLoader /
   manifest-load slice is implemented. Recommended: amend ADR-002 to direct-reference the two
   manifests per ADR-006. *(Blocks only the BootLoader slice; does not block RNG/Save/Event/Manifest-gen slices.)*

## Non-blocking issues (should fix, do not gate Foundation coding)

2. Scope ADR-002 **A3** to non-retired entries (CONFLICT-2) — bundle into the CONFLICT-1 amendment.
3. Drop the `ScoreKeeper` parameter from master-architecture **§8.1** seam signatures to match ADR-005 (D2).
4. **ADR-G / ADR-H** (audio, particles) before the Juice Layer VFX/audio slice — closes the two ⚠️ Partials.
5. **ADR-001 retrofit** (advisory, QQ-10): add the titled ADR Dependencies / Engine Compatibility /
   GDD Requirements Addressed sections.
6. Consider a single shared `Domain` FNV-1a-32 primitive (currently defined twice — Save codec and
   RNG `ForkStream`) to remove a silent-drift vector; both are golden-vector-pinned today, so this is DRY hygiene, not a defect.

## Required ADRs (prioritized — for the deferred layers)

1. **ADR-G — Audio middleware vs Unity native** (unblocks TR-jl-003 audio).
2. **ADR-H — Particle strategy (Shuriken vs VFX Graph)** (unblocks TR-jl-004 VFX).
3. **ADR-F — UI Toolkit vs UGUI for board-adjacent FX** (score popups / HUD; QQ-06).
4. (Later) **ADR-J** CI pipeline, **ADR-I** analytics seam, **ADR-001** retrofit.

---

## Handoff

- **Immediate actions:** (1) reconcile CONFLICT-1 via an ADR-002 amendment; (2) begin the Board
  Engine bootstrap slice against ADR-004/005/006 (unblocked); (3) begin the Save codec slice
  against ADR-003 (unblocked, parallel).
- **Pre-gate checklist (Glob results):**
  - `tests/unit/` + `tests/integration/` — **❌** not present → run `/test-setup` before gate-check.
  - `.github/workflows/tests.yml` — **❌** not present → run `/test-setup`.
  - `design/ux/accessibility-requirements.md` / `design/ux/interaction-patterns.md` — **❌** not present → run `/ux-design`.
  - Because pre-gate items are ❌, **do not** run `/gate-check pre-production` yet — run `/test-setup` and `/ux-design` first.
- **Rerun trigger:** re-run `/architecture-review` after the ADR-002 amendment lands (verify
  CONFLICT-1/2 clear and the verdict lifts to PASS), and after each of ADR-F/G/H is written.

---

*Report written by /architecture-review (technical-director). Verdict is advisory; the founder/user
decides whether to proceed despite CONCERNS. Companion writes: `docs/architecture/tr-registry.yaml`
(42 stable TR-IDs registered) and `docs/architecture/architecture.md` Status header (updated to the CONCERNS verdict).*
