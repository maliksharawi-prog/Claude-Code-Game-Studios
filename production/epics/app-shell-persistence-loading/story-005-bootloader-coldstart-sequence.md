# Story 005: BootLoader Cold-Start Sequence + FATAL Boot-Error + Recovery Notice

> **Epic**: App Shell — Boot, Persistence IO & Content Loading (E06)
> **Status**: Ready
> **Layer**: Foundation (Game-side)
> **Type**: Integration
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/save-persistence.md` (§6 recovery notice, §11 `load_profile`) · `design/gdd/screen-flow.md` (T1, T21 boot path)
**Requirement**: `TR-ldf-002` (manifest direct-reference + content load at boot) · `TR-sp-004` (`load_profile` in the boot sequence)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-002 (boot pin of Core/Rig + FATAL boot-error), ADR-003 (`LoadProfile`), ADR-006 (the two manifests load by direct serialized reference on the BootLoader).
**ADR Decision Summary**: Cold boot runs `Addressables.InitializeAsync` → pin `Core_Bootstrap` + `Shared_BoardRig` (Core/Rig failure = FATAL) → `SaveService.LoadProfile` (settings drive audio/juice) → construct RngService → load the two manifests via **direct serialized reference** (NOT Addressables) → ScreenFlow to `WORLD_MAP` (T1); a one-shot `recovery_notice` surfaces on first World Map entry if set.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: HIGH
**Engine Notes**: Uses `Addressables.InitializeAsync` (post-cutoff throw-on-failure semantics — via `IContentLoader`), `IPreprocessBuildWithReport` is NOT this story (Editor). The manifests use a **direct serialized reference**, not `Addressables.LoadAssetAsync` (which throws in 6.2+ per `plugins/addressables.md`) — this sidesteps the throw for boot-immediate assets. Async awaits happen only at this defined boot entry point, on the main thread.

**Control Manifest Rules (this layer — Game):**
- Required: the two manifests (`level_manifest.asset`, `world_map_manifest.asset`) load via a **direct serialized reference** on the BootLoader/config object — NOT via Addressables; Core/Rig load once at boot and pin for session; all boot async work is main-thread, awaited only at this entry point.
- Forbidden: never route the manifests through Addressables (`Core_Bootstrap` explicitly excludes both); never enter GAMEPLAY on a Core/Rig load failure; never `Resources.Load`.
- Guardrail: boot Core+Rig async load target < ~2s on mid-range Android (splash-masked, verification item); manifest resolve < 1ms (direct-reference, no async catalog).

---

## Acceptance Criteria

*From ADR-002 §Load Lifecycle + ADR-006 §Boot load path + `screen-flow.md` T1/T21, scoped to this story:*

- [ ] BootLoader executes the init order: `Addressables.InitializeAsync` → `TryLoadByLabel("core")` + `TryLoadByLabel("board-rig")` (pinned for session) → `SaveService.LoadProfile()` (settings drive audio/juice) → construct `RngService` → load the two manifests via direct serialized reference → hand control to `ScreenFlowController` at `WORLD_MAP` (T1).
- [ ] A Core/Rig load failure at boot shows a FATAL boot-error screen with a Retry action and never enters GAMEPLAY.
- [ ] The two manifests are obtained by direct serialized reference (not an Addressables load) — a missing/failed manifest never routes through the 6.2+ throw path.
- [ ] If the loaded profile has `recovery_notice_needed` set (both save slots were invalid), a one-time, dismissible, non-blocking notice appears on the first World Map entry and never reappears that session.
- [ ] A cold relaunch (T21) always re-derives from a fresh `load_profile()` and starts at `BOOT_LOADING → WORLD_MAP` — never a remembered composite state.

---

## Implementation Notes

*Derived from ADR-002 §Load Lifecycle, ADR-006 §Boot load path, ADR-003 §11:*

- Pin Core + Rig via `IContentLoader.TryLoadByLabel` (story 001); an `Ok == false` on either is the FATAL branch (boot-error screen + Retry) — do not proceed to profile/manifest/RNG.
- Load the two manifests as direct serialized references on the BootLoader/config object (per ADR-006 — "DON'T use Addressables for assets needed immediately at startup"; also sidesteps the 6.2+ throw). Convert `LevelManifestAsset.ToDomain()` → `LevelManifest` for RNG ordinal resolution.
- `LoadProfile` (story 004) is the single disk read; its settings drive the audio/juice config before gameplay is reachable; construct `RngService` after the profile is loaded.
- The one-shot `recovery_notice` is a handoff to `ScreenFlowController` (E05 story 008 surfaces it on T1); this story sets/passes the flag from the loaded profile.
- This BootLoader supersedes the MVP-only Level Preview harness as the production entry sequence; per-level `LevelData` still loads Addressably (story 002), not here.

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 001: the `IContentLoader` wrap (used to pin Core/Rig).
- Story 003/004: the SaveService write path and API (used for `LoadProfile`).
- Story 002: the per-level `LevelDataAsset` load (level-enter, not boot).
- E02: the RngService construction internals, the Domain `LevelManifest`/`SaveModel`, and the generated `level_manifest.asset` content (the BootLoader direct-references the asset E02 produces).
- E05 story 008: the `ScreenFlowController` state machine that renders the WORLD_MAP handoff and the recovery notice.
- The Editor pre-build manifest-staleness hook + A1–A6 CI gate (Editor & CI layer).

---

## QA Test Cases

*Integration story — Play Mode boot-sequence + fault-injection; deterministic.*

- **AC-1 (init order)**: Play Mode — assert the boot steps run in the ADR order and control reaches `ScreenFlowController` at `WORLD_MAP`.
- **AC-2 (FATAL on Core/Rig fail)**: Fault-inject a Core/Rig load failure; assert the boot-error/Retry screen shows and GAMEPLAY is never entered.
- **AC-3 (direct-reference manifests)**: Assert the two manifests are obtained without an `Addressables.LoadAssetAsync` call (no throw path); a null/missing serialized reference is caught as a boot error, not an escaped exception.
- **AC-4 (recovery notice one-shot)**: Given a loaded profile with `recovery_notice_needed = true`, the notice shows once on first World Map entry and not again that session; given it false, no notice.
- **AC-5 (cold relaunch T21)**: A simulated relaunch re-derives from `load_profile()` and starts at `BOOT_LOADING → WORLD_MAP`, discarding any prior composite state.

*Verification: Play Mode boot test + fault-injection recording + screenshot of the FATAL/boot-error screen in `production/qa/evidence/`.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/boot/boot_loader_sequence_test.cs` (Play Mode) — must exist and pass (BLOCKING)
- Advisory: `production/qa/evidence/boot-loader-fatal-screen-evidence.md` (boot-error screen capture)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: Story 001 (IContentLoader), Story 003 + Story 004 (SaveService `LoadProfile`); E02 (RngService, generated `level_manifest.asset`/`world_map_manifest.asset` the BootLoader direct-references, Domain `LevelManifest`), E05 (`ScreenFlowController` the BootLoader hands control to at WORLD_MAP + surfaces `recovery_notice`).
- Unlocks: the E06 Definition of Done (full boot → content → durable persistence chain); E07/E10 build on a booting app shell.
