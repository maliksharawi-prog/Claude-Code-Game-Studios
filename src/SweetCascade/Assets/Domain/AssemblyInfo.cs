using System.Runtime.CompilerServices;

// Grants the Edit-Mode test assembly access to Domain's `internal` members (e.g.
// SweetCascade.Domain.Rng.Mix32/RngStream/StreamRegistry/RngConstants, and RngService's
// internal test-only DebugGetStream seam) so anchor/primitive-level tests can assert against
// them directly, without widening SweetCascade.Domain's PUBLIC surface (IRngService, RngService,
// RngSessionLog, IClock, and the Levels POCOs) beyond what the Game layer is meant to consume.
// See docs/architecture/adr-004-deterministic-rng.md §5 (golden-vector suite) and
// src/SweetCascade/Assets/Tests/EditMode/SweetCascade.Domain.Tests.asmdef ("name":
// "SweetCascade.Domain.Tests" -- must match this attribute's argument exactly).
[assembly: InternalsVisibleTo("SweetCascade.Domain.Tests")]
