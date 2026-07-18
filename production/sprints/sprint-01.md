# Sprint 1 — 2026-07-18 → scope-complete (nominal ~2 weeks, target 2026-08-01)

> **Sprint model: SCOPE-BOXED, not time-boxed.** This sprint closes when its committed
> scope (all of E01) lands with a green CI signal — not on a calendar date. The date above
> is a nominal ~2-week reference for reporting only; it is **not** a delivery deadline and
> **not** a velocity commitment. Capacity is a single AI-assisted implementer plus this
> session's agent throughput; the per-story "Est." figures are the relative-effort sizings
> carried verbatim from each story file, used for sequencing and relative sizing — not as a
> promised burn rate.
>
> **Review mode:** `lean` (no `production/review-mode.txt` → default; consistent with the
> 2026-07-18 pre-production gate-check). PR-SPRINT producer-gate subagent **skipped — lean
> mode**; the feasibility assessment is folded in below (this plan is authored by `producer`).

## Sprint Goal

**Unity project scaffolds, CI gate goes green, the Domain assembly proves its purity, and
the first golden-vectored domain code lands** — clearing pre-production blocker **B1** (no
sprint plan / no stories) and establishing the buildable, provable Foundation container
(E01) that every later epic compiles and tests inside.

## Capacity (scope-boxed)

- **Committed scope (Must Have):** E01 in full — 6 stories, ~10 ideal-days of relative-effort sizing.
- **Stretch scope (Should Have):** E02 RNG track (001–004) + LevelData POCO (005) — 5 stories, ~10 ideal-days.
- **Buffer:** ~20% of the committed Must Have effort is reserved for unplanned work — specifically
  the Unity-editor manual-checklist passes (URP asset creation, `.meta` generation, Test Runner
  verification, purity-probe screenshots) and `game-ci` activation surprises. These are held
  outside the Should-Have stretch, not on top of it.
- **Close criterion:** Must Have complete + CI green (L2 purity gate armed; game-ci Edit+Play
  wired and running as a blocking gate). Should Have is pulled opportunistically as E01's
  asmdefs land; it is explicitly droppable without failing the sprint.

## Tasks

### Must Have (Critical Path — E01 project-scaffold-ci, in full)

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| E01-001 | Unity 6.3 project shell — URP Render Graph, portrait player settings, package manifest (`TR-perf-002`, ADR-001). Story: `production/epics/project-scaffold-ci/story-001-unity-project-urp-render-graph.md` | unity-specialist (+ founder/editor manual pass) | 2 | None (root story) | Project at `src/SweetCascade/` pinned `6000.3.x`; 4 approved packages only; URP asset on Render Graph path (no Compatibility-Mode warning); Input System backend; portrait; empty scene renders 60fps. Full ACs in story file. |
| E01-002 | Five assembly definitions + one-way dependency direction + Domain `noEngineReferences` (L1) (`TR-perf-002`, ADR-004). Story: `.../story-002-assembly-definitions-domain-purity.md` | unity-specialist (+ editor manual pass) | 2 | E01-001 | 5 asmdefs at §5.3 locations; Domain `noEngineReferences:true`, `references:[]`; acyclic Domain→∅, Game→Domain, UI→Game(+Domain), Editor→Game/Domain; deliberate upward `using UnityEngine;` probe fails to compile (L1 proof), then removed. |
| E01-003 | Domain-purity CI denylist scan (L2) + injected-violation self-test (`TR-perf-002`, ADR-004). Story: `.../story-003-domain-purity-ci-denylist-scan.md` | unity-specialist | 1.5 | E01-002 | `domain-purity` CI job runs **before** the Unity build (license-free, fails fast); ripgrep ADR-004 denylist blocks on any hit; `// rng-purity-allow:` per-line escape honored; `float` path-scoped to `Assets/Domain/Rng/**`; blocking self-test proves the guard is armed, not vacuous. |
| E01-004 | game-ci test-runner gate activation — guard removal, license documented, first green run (`TR-perf-002`, ADR-004). Story: `.../story-004-game-ci-workflow-activation.md` | unity-specialist (+ **founder: `UNITY_LICENSE` secret**) | 2 | E01-001, E01-002, E01-003, E01-005 | `game-ci/unity-test-runner@v4` runs Edit+Play as a **blocking** gate on PR + push to `main` (empty suites OK); guard-removal criterion documented; `domain-purity` sequenced before Unity job; first green run demonstrated; purity probe turns pipeline red before Unity runs. **L2 gate is license-free; full Edit+Play green run needs `UNITY_LICENSE`.** |
| E01-005 | Edit/Play Mode test skeletons + example conventions test (`TR-perf-002`, ADR-004). Story: `.../story-005-edit-play-test-skeletons.md` | unity-specialist (+ editor manual pass) | 1.5 | E01-002 | PlayMode `SweetCascade.Game.Tests.asmdef` + EditMode coverage folders; one deterministic EditMode conventions test (headless, engine-free) **passes**; one PlayMode smoke test **passes**; both run under `game-ci testMode: all`; no seeds/clock/IO (non-flaky). |
| E01-006 | Editor tooling folder layout + manifest-generator stub placement — *off critical path* (`TR-perf-002`, ADR-006). Story: `.../story-006-editor-tooling-manifest-stub.md` | unity-specialist (+ editor manual pass) | 1 | E01-002 | `Editor/ManifestGen \| LevelValidation \| LevelPreview` folders; inert compile-safe `LevelManifestGenerator.Reconcile(bool)` stub (throws `NotImplementedException`, cites ADR-006, names E02/E03 owner); no `AssetDatabase` write, no build hook, no Addressables group for the manifests. |

**Critical path:** E01-001 → E01-002 → E01-003 → E01-004, with E01-005 also feeding E01-004's
first green run. **E01-006 is off the critical path** (Config/Data scaffold; can land any time after E01-002).

### Should Have (Stretch — E02 domain-foundation: RNG track 001–004 + LevelData POCO 005)

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| E02-001 | RNG primitives & seed derivation (Mix32 / SplitMix32 / F1–F3) (`TR-rng-002`, ADR-004). Story: `production/epics/domain-foundation/story-001-rng-primitives-seed-derivation.md` | unity-specialist | 2 | E01-002, E01-003 (soft: E01-004, E01-005) | `Mix32(0)==0`; GDD `Combine` anchors reproduce under `uint` wrap; F1/F2/F3 seed lifecycle; all 4 streams sub-seeded at session start; attempt/level sensitivity; daily determinism + no player-data parameter; `unchecked uint/ulong`, no `System.Random`/`float` on `Rng/**`. |
| E02-002 | RNG draw API — NextFloat / NextInt / NextColor / Shuffle (F4–F6) (`TR-rng-003`, ADR-004). Story: `.../story-002-rng-draw-api.md` | unity-specialist | 2 | E02-001 | Integer multiply-shift `(raw×range)>>32`, one raw draw per `NextInt`/`NextColor`; `Shuffle` returns new array (no mutation), consumes `len−1` draws; uniform-distribution + bounds + multiset + invalid-range/empty-pool error tests. |
| E02-003 | RNG stream isolation, ForkStream & bug-repro session log (`TR-rng-001`, `TR-rng-003`, ADR-004). Story: `.../story-003-rng-stream-isolation-fork-session-log.md` | unity-specialist | 2 | E02-001, E02-002 | Per-name `StreamState`; interleaving proves isolation; `ForkStream` from parent **initial** seed, FNV-1a-32 over UTF-8 label, idempotent per `(parent,label)`; session-log repro fields + replay; `IClock` injection (no `DateTime` in Domain); no hidden-bias parameter. |
| E02-004 | RNG golden-vector fixture & byte-for-byte regression suite (`TR-rng-002`, ADR-004). Story: `.../story-004-rng-golden-vector-suite.md` | unity-specialist | 2 | E02-001, E02-002, E02-003 (transitively E01-002/003/004) | `rng_golden_v1.json` frozen (F1/F2/F3 tables incl. anchors, 64 `NextRaw`, 64 `NextInt(0,4)`+`NextColor`, 8 `NextFloat`, `Shuffle [0..19]`+boundaries, fork childSeed+16 draws); `RngGoldenVectorTest.cs` asserts byte-for-byte under **Mono** as a **blocking PR gate**. IL2CPP/WebGL = release-matrix (ADR-J, deferred). **Standing determinism gate that unblocks E03.** |
| E02-005 | LevelData Domain POCO — schema v1, defaults, closed enums, additive `display_name` (`TR-ldf-001`, `TR-ldf-004`, arch §6). Story: `.../story-005-leveldata-poco-schema.md` | unity-specialist | 2 | E01-002 (soft: E01-005) | Full typed schema-v1 POCO; optional-field defaults (`cell_mask`→full rect, `pre_placed_pieces`→[], `rng_seed`→−1); closed objective enum (`score_target`/`collect_color`); additive `display_name` with **no** `schema_version` bump; `CURRENT_SCHEMA_VERSION==1`; no hardcoded gameplay literals. |

### Nice to Have

| ID | Task | Agent/Owner | Est. Days | Dependencies | Acceptance Criteria |
|----|------|-------------|-----------|-------------|-------------------|
| — | *None this sprint.* The remaining E02 stories (006 Validate, 007 flood-fill, 008–011 Save codec, 012–014 manifest tool) are **explicitly out of scope** for Sprint 1 — pull them in Sprint 2 once the RNG track and CI gate are proven. | — | — | — | — |

## Carryover from Previous Sprint

| Task | Reason | New Estimate |
|------|--------|-------------|
| — | First sprint of the project. No carryover. | — |

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|------------|
| **`UNITY_LICENSE` (+`UNITY_EMAIL`/`UNITY_PASSWORD`) secret not configured** (external, founder action) — blocks E01-004's full Edit+Play green run **and** E02-004's golden-vector **Mono CI** gate (the runner cannot activate Unity). The license-free **L2 `domain-purity` gate is unaffected**. | Medium (known gap C8 from the pre-production gate) | High (blocks the green-CI keystone) | Request the founder set the secret at sprint start. Sequence the license-free L2 purity job first so partial CI value lands immediately; document the partial-activation state per E01-004. RNG stories 001–003 can still be authored + run locally in Test Runner while the secret is pending. |
| **Unity-editor-only manual steps** (URP asset creation, `.meta`/packages-lock generation, Test Runner verification, upward-reference & purity-probe screenshots) cannot be executed headlessly by the AI implementer | High (inherent — flagged as manual-checklist items in stories 001, 002, 005, 006) | Medium | Stories flag each editor-required step explicitly. Batch them into a single founder/editor verification pass; capture evidence under `production/qa/evidence/`. Treat these as the primary consumer of the 20% buffer. |
| **`game-ci/unity-test-runner@v4` activation is post-cutoff infra** (E01-004 rated MEDIUM engine risk); LLM knowledge reliably covers only ~6.0/6.1, so the runner version/activation flow must be verified against current game-ci docs | Medium | Medium | Cross-reference `docs/engine-reference/unity/VERSION.md` + current game-ci docs before wiring. Keep the L2 purity gate independent of the Unity job so a runner hiccup never disables purity enforcement. |
| **RNG cross-backend determinism (IL2CPP/WebGL) deferred to ADR-J** — E02-004 proves **Mono only** this sprint; the byte-for-byte cross-backend proof is release-matrix | Low (in-scope Mono is well-specified) | Low now / Medium at release | Declare IL2CPP+WebGL as the release-matrix acceptance for `TR-rng-002`; write ADR-J before the export matrix / E11. Do not block the Mono gate on it. |
| **No QA plan for Sprint 1** — sprint-level test requirements undefined at start | High (none exists yet) | Medium (blocks the Production → Polish gate) | Run `/qa-plan sprint` before the last story implements. Partially mitigated: every story already carries per-story **QA Test Cases** authored at story-creation, so developers are not starting from a blank slate. See warning block below. |
| **Should-Have (E02) may not fully land** if E01 editor steps or the license slip — E02-004 is the gate E03 waits on; its slip slips the E03 start | Medium | Medium | E01 is the committed Must Have; E02 is droppable stretch. E02-001–003 are authorable/testable locally even before the license lands (only the CI green gate waits on it), so the RNG work can progress in parallel with license procurement. |

## Dependencies on External Factors

- **`UNITY_LICENSE` repo secret (founder / repo-admin action)** — the single hard external dependency
  this sprint. Required to activate `game-ci/unity-test-runner@v4` for the Edit+Play run (E01-004)
  and the RNG golden-vector Mono CI gate (E02-004). **Does not block** the license-free L2
  `domain-purity` scan, the L1 asmdef purity proof, or local Test Runner authoring of RNG/POCO code.
  Configure it as soon as E01-001 scaffolds `src/SweetCascade/` (per pre-production concern C8).
- **A Unity 6.3 LTS editor pass (founder / human-in-editor)** — needed to execute the manual-checklist
  items in E01-001/002/005/006 (URP asset creation, `.meta` + `packages-lock.json` generation, Test
  Runner verification, compile-probe screenshots). The AI implementer authors all file-based artifacts;
  the editor pass materializes and verifies them.
- **ADR-J (CI build pipeline) — not yet written.** Deferred by design. Its absence keeps the
  IL2CPP/WebGL export matrix and ADR-004's L3 cross-backend golden run out of this sprint; it does
  **not** block any Sprint-1 story (all are scoped to the Mono/editor test-runner + L2 gate).

## Definition of Done for this Sprint

**Sprint-goal DoD (E01 committed):**
- [ ] Five asmdefs exist with the compiler-enforced one-way dependency direction; a deliberate upward reference fails compilation (L1 proof captured, then removed).
- [ ] URP Render Graph project template renders an empty scene at 60fps in-editor with **no** Compatibility-Mode warning.
- [ ] The Domain-purity CI guard **fails** the build on an injected `UnityEngine`/`System.Random` symbol in Domain and **passes** on clean Domain (L2 self-test armed, blocking).
- [ ] `game-ci/unity-test-runner@v4` runs Edit + Play assemblies as a **blocking** PR/main gate (empty/near-empty suites acceptable — the wiring is the deliverable); first green run demonstrated *(full activation gated on `UNITY_LICENSE`; L2 purity gate green regardless)*.
- [ ] Domain purity proven at all three layers reachable this sprint: **L1** (asmdef `noEngineReferences`), **L2** (CI denylist + self-test), and — via the Should-Have RNG track — **L3** (golden vectors) for the code that lands.

**Sprint-goal DoD (E02 stretch, if pulled):**
- [ ] RNG reproduces its golden vectors byte-identically under Mono; stream-isolation and `ForkStream` are unit-proven; the golden-vector suite is a blocking PR gate.
- [ ] LevelData schema-v1 POCO lands with documented defaults, closed enums, and the additive `display_name` (no `schema_version` bump).
- [ ] All landed Logic stories (E02-001…005) have passing headless Edit Mode tests under `game-ci`.

**Process DoD (skill template):**
- [ ] All Must Have tasks completed and passing their acceptance criteria.
- [ ] QA plan exists (`production/qa/qa-plan-sprint-1.md`) — **see the No-QA-Plan warning below; run `/qa-plan sprint`.**
- [ ] All Logic/Integration stories have passing unit/integration tests (or documented editor smoke where a fresh scaffold has no headless surface — E01-001/006).
- [ ] Smoke check passed (`/smoke-check sprint`).
- [ ] QA sign-off report: APPROVED or APPROVED WITH CONDITIONS (`/team-qa sprint`).
- [ ] No S1 or S2 bugs in delivered features.
- [ ] Design/architecture documents updated for any deviations.
- [ ] Code reviewed and merged.

## Out of Scope (explicit)

- **All Presentation / Content / QA-perf epics: E07 ui-toolkit-screens-hud, E08 rendering-materials,
  E09 juice-vfx-audio-haptics, E10 content-levels-manifest, E11 device-qa-performance** — no stories pulled.
- **Deferred ADRs: ADR-F (UI Toolkit vs UGUI for board-adjacent FX), ADR-G (audio middleware vs native),
  ADR-H (particle strategy)** — not authored, and no story gated on them is in this sprint.
- **ADR-J (CI build pipeline)** and everything it governs: the full build/export matrix, IL2CPP/WebGL
  **player builds**, and ADR-004's **L3 cross-backend** golden-vector run. This sprint delivers only the
  Mono/editor test-runner + L2 purity gate.
- **The rest of E02:** stories **006** (`Validate()` V1–V19), **007** (V8 flood-fill), **008–011**
  (Save codec), **012–014** (LevelManifest POCO + cross-validator + generator tool) — deferred to Sprint 2.
- **E03+ core/feature gameplay** (board engine, specials, scoring, objectives, input, screen flow, app shell) — dependency-gated behind E01/E02.
- **Phase 2 / Phase 3 systems:** Booster Brewing Meta (#12), Events/Theming (#13), Social Layer (#14),
  Backend & Accounts (#15). Phase-3 backend seam is reserved as ADR-I (scope only, not implemented).

> ⚠️ **No QA Plan**: This sprint was planned without a QA plan. Run `/qa-plan sprint`
> before the last story is implemented. The Production → Polish gate requires a QA
> sign-off report, which requires a QA plan. (Partial mitigation: each story already carries
> per-story QA Test Cases authored at story creation.)

## Notes

- **Clears pre-production blocker B1.** `production/sprints/sprint-01.md` + `production/sprint-status.yaml`
  now exist and reference real story files that embed TR-ID + ADR. B2 (UX review) is already cleared
  (five UX docs APPROVED, `design/ux/ux-review-2026-07-18.md`). Re-run `/gate-check pre-production` to
  flip the gate to PASS.
- **No scope creep.** Sprint 1 pulls a strict **subset** of already-decomposed epic stories (E01 in full;
  E02 001–005). Nothing is added beyond the epics' defined scope, so `/scope-check` is not required for
  this sprint — but re-run it before Sprint 2 if additions surface.
- **Producer feasibility (inline, lean mode — PR-SPRINT subagent skipped): REALISTIC.** Every story is
  1–2 ideal-days (satisfies the 1–3 day rule); dependencies are explicit and acyclic; the critical path
  (E01-001→002→003→004, with 005 feeding 004) is clear. The one binding external risk is `UNITY_LICENSE`,
  which gates *CI activation* but not local authoring or the L2 purity gate — so work is not hard-blocked.
- **First-golden-vectored-code framing:** E02-004 is both the Should-Have capstone and the standing
  determinism gate E03 depends on. Prioritize the RNG track (E02-001→004) over the LevelData POCO (E02-005)
  within the stretch if throughput is constrained, because E02-004 is on E03's critical path and E02-005 is not.
- **Buffer discipline:** the 20% reserve is earmarked for the Unity-editor manual passes and game-ci
  activation — the two least-predictable, non-headless parts of E01.
