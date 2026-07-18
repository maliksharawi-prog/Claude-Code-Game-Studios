# Story 004: game-ci test-runner gate activation — guard removal, license documented, first green run

> **Epic**: Project Scaffold & CI Activation (E01)
> **Status**: Ready
> **Layer**: Foundation (infrastructure / CI)
> **Type**: Integration
> **Estimate**: 2 days
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: — (governed by `.claude/docs/technical-preferences.md` Testing + `docs/architecture/architecture.md` §5.1)
**Requirement**: `TR-perf-002`
*(This story satisfies the "CI-guarded" delivery of TR-perf-002 — the `game-ci` blocking gate that actually runs Edit + Play test assemblies plus the L2 purity job. Read the current text fresh from `docs/architecture/tr-registry.yaml`.)*

**ADR Governing Implementation**: ADR-004: Deterministic RNG & Domain-Purity CI Guard (primary — §6 wiring: `domain-purity` before the Unity build; golden-vector gate on Mono for PRs)
**Governing ADRs (secondary)**: ADR-001 (game-ci/unity-test-runner is the chosen CI runner).
**ADR-J (pending — CI build pipeline): NOT YET WRITTEN.** Per the epic, "stories touching the full build/export matrix are advisory-blocked until it exists." This story is deliberately scoped to the **test-runner + purity gate** only; the IL2CPP/WebGL **player-build/export matrix** (and ADR-004's L3 cross-backend golden run) are **out of scope** and deferred to ADR-J. This keeps the story Ready, not Blocked. See the epic-level gap note.
**ADR Decision Summary**: CI runs `game-ci/unity-test-runner@v4` as a blocking gate on every PR and push to `main`; the license-free `domain-purity` scan runs before it and fails fast.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: MEDIUM (game-ci activation + license secret is post-cutoff infra; the runner version and activation flow must be verified against current game-ci docs)
**Engine Notes**: `game-ci/unity-test-runner@v4` requires a `UNITY_LICENSE` secret (or `UNITY_EMAIL`/`UNITY_PASSWORD` for personal-license activation). The existing `tests.yml` already guards on `ProjectSettings/ProjectVersion.txt` and skips with a notice until the project lands — Story 001 makes that file exist, so this story flips the guard from "skip" to "run".

**Control Manifest Rules (Editor & CI layer)**:
- Required: CI runs `game-ci/unity-test-runner@v4` as a blocking gate on every PR and every push to `main`.
- Required: the `domain-purity` L2 job runs before the Unity build and blocks on any hit.
- Required: the golden-vector RNG suite is blocking on every PR (Mono) — *becomes real in E02; wired here as an empty/ready gate*.
- Guardrail: never disable or skip a failing test to force CI green.

---

## Acceptance Criteria

*From the E01 Definition of Done, ADR-004 §6, and `tests/README.md`, scoped to this story:*

- [ ] `game-ci/unity-test-runner@v4` runs **both** Edit Mode and Play Mode test assemblies as a **blocking** gate on every PR and every push to `main`. Empty/near-empty suites are acceptable — the *wiring* is the deliverable (E01 DoD).
- [ ] The `tests.yml` guard step's behaviour is documented and its **removal criterion is explicit**: once `src/SweetCascade/ProjectSettings/ProjectVersion.txt` exists (Story 001), the guard resolves to `project_exists=true` and the runner executes instead of skipping. Document whether the guard is kept (defensive) or removed, and why.
- [ ] The `UNITY_LICENSE` secret requirement is documented as a one-time repo-settings prerequisite (already noted in `tests/README.md`); the workflow reads `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` from secrets and never hardcodes credentials.
- [ ] The `domain-purity` job (Story 003) is sequenced **before** the Unity job (`needs:`), so a purity violation fails the pipeline without consuming a Unity license.
- [ ] A **first green run** is demonstrated end-to-end: purity scan green → test-runner runs Edit+Play (empty suites pass) → pipeline green on a PR.
- [ ] The **deliberately-failing purity probe** is demonstrated: a throwaway branch injecting `System.Random` into `Assets/Domain/` turns the pipeline **red** at the `domain-purity` step (before Unity runs), proving the gate blocks. The branch is then discarded.

---

## Implementation Notes

*Derived from ADR-004 §6, `tests/README.md`, and the existing `.github/workflows/tests.yml`:*

**File-based (no editor GUI required):**
- Update `.github/workflows/tests.yml`: add `domain-purity` as a job that the `test` job `needs:` (or as a preceding step), keep the project-existence guard, and confirm `testMode: all` (Edit + Play). Pin the runner to `@v4`.
- Document the guard-removal criterion inline in the workflow comment and in `tests/README.md` "Next Steps" (item 2 already flags the license secret).
- Prepare a short branch/PR to exercise the first green run and the red purity-probe run; capture the Actions logs as evidence, then discard the probe branch.

**Manual checklist items (require repo admin / external setup — flag as prerequisites, not code):**
- [ ] **Repo admin:** add the `UNITY_LICENSE` secret (or `UNITY_EMAIL`/`UNITY_PASSWORD` + activation) in GitHub repo settings. Without it the game-ci job cannot activate Unity — this is an external dependency, not a code change. If the secret is not yet available, the Edit+Play run stays blocked on activation while the license-free `domain-purity` gate is fully functional; document this partial-activation state.
- [ ] Observe the first green Actions run and the red purity-probe run in the GitHub UI; screenshot both.

**Scope boundary (ADR-J):** do NOT add player-build, IL2CPP, or WebGL export jobs, and do NOT wire ADR-004's L3 (IL2CPP + WebGL byte-for-byte golden run) here — those await ADR-J and belong to E11/release. This story delivers only the test-runner + L2 purity gate on Mono/editor.

---

## Out of Scope

*Handled by neighbouring stories or deferred — do not implement here:*

- Story 003: the `domain-purity` scan script + self-test (this story only sequences and observes it in the pipeline).
- Story 005: authoring the example Edit/Play tests the runner executes (this story wires the runner; the suites can be empty and still satisfy the DoD).
- **ADR-J / E11**: the full build/export matrix, IL2CPP/WebGL player builds, and ADR-004 L3 cross-backend golden-vector runs — deferred until ADR-J is written.
- **E02**: making the golden-vector PR gate non-empty (needs real RNG code).

---

## QA Test Cases

*Authored at story creation (lean mode). CI behaviour is verified by observing real Actions runs — a documented CI verification, plus a positive/negative pair.*

**Test — AC: pipeline goes green end-to-end**
- Given: Story 001 (project exists), Story 002 (asmdefs), Story 003 (purity job), and empty/near-empty Edit+Play suites, with `UNITY_LICENSE` present.
- When: a PR is opened against `main`.
- Then: `domain-purity` passes, `game-ci/unity-test-runner@v4` runs Edit+Play and passes, the required check is green and blocking.
- Edge cases: a push directly to `main` triggers the same gate; if `UNITY_LICENSE` is absent, document that the purity gate still runs and the Unity job fails at activation (expected, not a false pass).

**Test — AC: purity probe turns the pipeline red before Unity runs**
- Given: a throwaway branch adding `new System.Random()` to a file under `Assets/Domain/`.
- When: CI runs on that branch.
- Then: the `domain-purity` job fails and blocks; the Unity job does not run (fails fast, no license consumed).
- Edge cases: confirm the red is at the purity step specifically; discard the branch afterward.

**Manual check — AC: license + guard documented**
- Setup: read `tests.yml` and `tests/README.md`.
- Verify: the `UNITY_LICENSE` prerequisite and the guard-removal criterion are both documented; secrets are referenced via `${{ secrets.* }}`.
- Pass condition: an outside developer can follow the docs to activate CI with no tribal knowledge.

---

## Test Evidence

**Story Type**: Integration
**Required evidence**: Integration test / documented CI run — `production/qa/evidence/story-004-ci-activation-evidence.md` capturing (a) the first green pipeline (purity + Edit+Play) and (b) the red purity-probe run. Both are observed Actions runs; screenshots + run URLs.

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (project must exist to flip the guard), Story 002 (asmdefs the runner compiles), Story 003 (the `domain-purity` job it sequences), Story 005 (the Edit/Play example tests the runner executes — empty suites acceptable, but the test asmdefs must exist).
- Unlocks: None within E01 — this is the epic's green-CI keystone. Every later epic's PRs run through this gate.
- External dependency: `UNITY_LICENSE` repo secret (repo admin action).
