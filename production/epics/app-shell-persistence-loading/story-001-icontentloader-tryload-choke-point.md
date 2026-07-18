# Story 001: IContentLoader — TryLoad Choke Point (Addressables Wrap)

> **Epic**: App Shell — Boot, Persistence IO & Content Loading (E06)
> **Status**: Ready
> **Layer**: Foundation (Game-side)
> **Type**: Integration
> **Estimate**: L (~1–2 sessions)
> **Manifest Version**: 2026-07-18
> **Last Updated**: —

## Context

**GDD**: `design/gdd/level-data-format.md` (§Edge Cases — load-loudly-never-partial)
**Requirement**: `TR-ldf-002` (LevelDataAsset SO + loading — the loading mechanism)
*(Requirement text lives in `docs/architecture/tr-registry.yaml` — read fresh at review time)*

**ADR Governing Implementation**: ADR-002: Addressables Grouping & Load Strategy
**ADR Decision Summary**: `SweetCascade.Game` exposes ONE type that touches `Addressables.*`; every load is wrapped so a Unity 6.2+ throw becomes a discriminated `LoadResult<T>` (`Ok`|`Fail`) — no exception ever escapes to a caller. Seven-group layout; resident set = Core ∪ Shared_BoardRig ∪ exactly one region.

**Engine**: Unity 6.3 LTS (6000.3.x) | **Risk**: HIGH
**Engine Notes**: Addressables 6.2+ **throws on load failure** (invalid key, missing dependency, corrupt bundle) — it does NOT return `null`. Every load MUST be wrapped (`TryLoad` try-catch **and** a `.Status` belt-and-suspenders check); reference-counted `Release` is required (verified `docs/engine-reference/unity/plugins/addressables.md`). LLM training covers ~Addressables 1.x return-null behavior — cross-reference the engine-reference before citing any API. **Verification Required**: a Play Mode test on the pinned 6.3 build asserting an invalid-address load returns `Ok == false` with no exception escaping.

**Control Manifest Rules (this layer — Game):**
- Required: `IContentLoader` is the ONLY code that may call `Addressables.*`; every load wrapped to a `LoadResult<T>`; `IContentHandle` stays opaque (no consumer reaches the underlying `AsyncOperationHandle`); resident-set invariant enforced (release the old region's three groups before loading the new region's).
- Forbidden: never call `Addressables.*` outside `IContentLoader`; never `Resources.Load()` / synchronous asset loading; never ship a Remote build/load path at MVP (`Remote_Events` stays empty/unbuilt); never one monolithic bundle or a per-level group.
- Guardrail: ≤120MB Addressables resident sub-budget; boot Core+Rig async load target < ~2s on mid-range Android; the loader releases a failed handle before returning (no leaked refcount).

---

## Acceptance Criteria

*From ADR-002 Validation Criteria + `level-data-format.md` load rules, scoped to this story:*

- [ ] `TryLoad<T>(address)` on an invalid address returns `Ok == false` with a set `ContentLoadError` and **no exception escaping** the loader (the 6.2+ throw is contained).
- [ ] `TryLoadByLabel<T>(label)` batch-loads a whole group and returns a discriminated result identically wrapped.
- [ ] Per-content fallback classification is exposed so callers can implement the fixed policy: Core/Rig failure = FATAL; `LevelData` = abort level (never a partial board); `Region_Theme` = Core default theme; `Audio_Region` = silent-degrade.
- [ ] Leak test: enter region A, enter region B, return to A — the Addressables refcount for A's groups returns to its post-first-load baseline (region-change releases correctly via `ReleaseLabel`).
- [ ] `IContentHandle` is opaque — no consumer outside `ContentLoader` can reach the underlying `AsyncOperationHandle`.
- [ ] Every failure logs a structured record `(address, label, group, ContentLoadError)` and releases the failed handle before returning.

---

## Implementation Notes

*Derived from ADR-002 Decision + Key Interfaces:*

- Implement `TryLoad<T>` as: call `Addressables.LoadAssetAsync<T>`, `await h.Task`, then check `h.Status != Succeeded || h.Result == null` → `Classify(h.OperationException)`, release if valid, return `Fail`; wrap the whole thing in `try/catch` because 6.2+ **throws** on failure — the catch lands the throw and returns `Fail`.
- `Classify(exception)` maps message/type to `ContentLoadError` and defaults to `Unknown` (the exact 6.3 exception surface is a Verification-Required item — keep it defensive).
- The loader is async-only (WebGL has no synchronous file access); all loads are awaited only at boot/region/level-entry points, on the main thread.
- No Domain code ever calls a load or sees an Addressables type — this contract lives entirely in Game, preserving the assembly boundary.
- The two manifests (`level_manifest.asset`, `world_map_manifest.asset`) are NOT loaded here — they load by direct serialized reference on the BootLoader (ADR-006; story 005).

---

## Out of Scope

*Handled by neighbouring stories / epics — do not implement here:*

- Story 002: the `LevelDataAsset` ScriptableObject + `ToDomain()` + address==`level_id` invariant (this story provides the load mechanism it resolves through).
- Story 005: the `BootLoader` sequence that pins Core/Rig and the FATAL boot-error screen.
- The Editor A1–A6 `AddressablesValidation` tool and CI gate (Editor & CI layer — not an E06 Game-side deliverable).
- Phase 3 remote (`Remote_Events`) build/download path — reserved, empty, unbuilt at MVP.

---

## QA Test Cases

*Integration story — Play Mode fault-injection + leak test (ADR-002 Validation Criteria).*

- **AC-1 (throw containment)**: Play Mode — `TryLoad<T>("invalid-address")` returns `Ok == false`, `Error` set, and no exception propagates (assert no throw escapes). Edge cases: missing dependency; a deliberately corrupt fixture bundle from `Editor_Fixtures`.
- **AC-2 (label batch)**: `TryLoadByLabel<T>("level-data:candy_kingdom_hub")` returns the resident batch as `Ok` with all assets; an invalid label returns `Fail`.
- **AC-3 (leak/refcount)**: Enter region A → B → A; assert A's group refcount returns to baseline after the round trip.
- **AC-4 (opaque handle)**: Compile/reflection check that no public API exposes `AsyncOperationHandle`.
- **AC-5 (fallback classification)**: Given each `ContentLoadError`, assert the caller-visible severity mapping matches ADR-002's per-content policy table.

*Verification: Play Mode fault-injection recording in `production/qa/evidence/`.*

---

## Test Evidence

**Story Type**: Integration
**Required evidence**:
- Integration: `tests/integration/content-loading/content_loader_tryload_test.cs` (Play Mode) — must exist and pass (BLOCKING)

**Status**: [ ] Not yet created

---

## Dependencies

- Depends on: E01 (Addressables package + assemblies + CI).
- Unlocks: Story 002 (LevelDataAsset resolves through this loader), Story 005 (BootLoader pins Core/Rig via this loader).
