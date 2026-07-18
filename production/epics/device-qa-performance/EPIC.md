# Epic: Device QA & Performance Validation

> **Epic ID**: E11
> **Layer**: QA / Performance (cross-cutting)
> **GDD**: `.claude/docs/technical-preferences.md` (performance budgets) · `docs/architecture/architecture.md` §9
> **Architecture Module**: cross-cutting validation of the assembled MVP build — on-device performance profiling against the ≤100 draw-call / 60fps / ≤400MB budget, and the QA sign-off that gates Production → Polish
> **Status**: Ready
> **Stories**: Not yet created — run `/create-stories device-qa-performance`

## Scope

The final validation epic that proves the assembled MVP holds its budgets on real hardware and
passes QA sign-off. Re-profiles the arch §9.3 draw-call accounting (a design-time estimate) on
2022-era mid-range Android during a heaviest-cascade, confirms 60fps / 16.6ms frame budget and
≤400MB memory ceiling, validates the VFX 40-call sub-budget + LOD ladder under real load,
verifies touch latency (≤1 frame) and ≥44px targets on device, and runs the full QA plan across
the 10 levels (no S1/S2 bugs in delivered features). This epic **owns no TR-IDs** — it is the
*validation gate* for the perf requirements engineered in E08 (TR-perf-001) and the runtime
delivered across E05–E10; its output is the go/no-go evidence for the Production → Polish gate.

## Governing ADRs

| ADR | Decision Summary | Engine Risk |
|-----|-----------------|-------------|
| ADR-J (pending): CI build pipeline | `game-ci/unity-test-runner@v4` Edit+Play gating, iOS Mac build step, export templates, `UNITY_LICENSE` — **not yet written** | — |
| arch §9 | The draw-call / frame / memory budget being validated (QQ-05 re-profile) | — |

## TR-IDs Owned

- **None.** This epic validates TR-perf-001 (owned/engineered by **E08**) and TR-perf-002
  (owned by **E01**) on device, and exercises the runtime TRs from E05–E10 end-to-end. It is a
  validation gate, not a distinct requirement.

## Depends On

- **E05, E06, E07, E08, E09, E10** — the full assembled, content-complete MVP build. This epic
  runs last, after every other epic delivers.

## Engine-Risk Notes (per `docs/engine-reference/unity/VERSION.md`)

- **MEDIUM — on-device profiling.** The ≤100 draw-call accounting (arch §9.3) is explicitly a
  **design-time estimate** flagged for on-hardware re-profiling at the Vertical Slice feel
  checkpoint (**QQ-05**). The 5-shared-mesh + GPU-instancing + one-instanced-well-draw assumptions
  must be confirmed on the SRP Batcher / GPU Resident Drawer path on target silicon.
- **CI (ADR-J pending):** the game-ci runner, iOS Mac build step, and `UNITY_LICENSE` secret are
  a prerequisite for the automated device/build matrix — write ADR-J before the CI-gated stories.
- **Feel ceiling (systems-index High-Risk):** the Match-3 Board Engine cascade feel and 60fps
  target were validated only in HTML/CSS in the concept prototype — this epic is the real
  in-engine feel + performance checkpoint before a Production commit. Treat it as the gate, not
  the concept prototype.

## Definition of Done

This epic is complete when:
- All stories are implemented, reviewed, and closed via `/story-done`.
- A heaviest-cascade frame on 2022-era mid-range Android profiles ≤100 draw calls at 60fps /
  ≤16.6ms with ≤400MB resident; the VFX 40-call sub-budget + LOD ladder hold under real load.
- Touch latency (≤1 frame) and ≥44px targets are confirmed on a physical touch device.
- The full QA plan passes across all 10 levels with no open S1 or S2 bugs in delivered features;
  performance evidence + QA sign-off (APPROVED or APPROVED WITH CONDITIONS) recorded in
  `production/qa/`.
- The Production → Polish gate has its required performance + QA evidence.

## Next Step

Run `/create-stories device-qa-performance` (schedule as the closing epic; requires ADR-J for CI-gated stories).
