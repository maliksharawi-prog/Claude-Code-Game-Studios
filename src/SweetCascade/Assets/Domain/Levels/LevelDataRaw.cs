using System;
using System.Collections.Generic;

namespace SweetCascade.Domain.Levels
{
    /// <summary>
    /// The as-authored shape of a schema v1 level file, before optional-field defaulting and
    /// closed-enum dispatch (level-data-format.md §2). Every field here mirrors the schema's
    /// own required/optional split: required fields have no in-band "absent" representation here
    /// (presence validation is Story 006's V1–V19 <c>Validate()</c>, out of scope for this
    /// story); optional fields are nullable so <see cref="LevelData.FromRaw"/> can tell "absent"
    /// from "explicitly supplied" and apply the documented default.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-005 (leveldata-poco-schema). Governing doc: <c>design/gdd/level-data-format.md</c> §2/§3.
    /// </para>
    /// <para>
    /// <see cref="UnrecognizedOptionalFieldNames"/> exists to exercise the additive-field
    /// tolerance policy (§3 row 1: "a reader that doesn't yet recognize a newer optional field
    /// ignores it and logs a warning") from this pure-Domain story, ahead of any concrete
    /// authoring-format reader (the Game-layer <c>LevelDataAsset.ToDomain()</c>, E06) existing
    /// yet. A future reader populates this list with whatever field names it encountered but did
    /// not map to a known schema field; <see cref="LevelData.FromRaw"/> turns each into a
    /// warning, never a rejection.
    /// </para>
    /// </remarks>
    public sealed class LevelDataRaw
    {
        public int SchemaVersion { get; init; }

        public string LevelId { get; init; } = string.Empty;

        public string Region { get; init; } = string.Empty;

        public int DisplayNumber { get; init; }

        public int GridWidth { get; init; }

        public int GridHeight { get; init; }

        /// <summary>Row-major mask rows. Null =&gt; defaults to a full <see cref="GridWidth"/>x<see cref="GridHeight"/> rectangle (§2).</summary>
        public IReadOnlyList<string>? CellMask { get; init; }

        /// <summary>Null =&gt; defaults to an empty list (fully RNG-generated start, §2).</summary>
        public IReadOnlyList<PrePlacedPiece>? PrePlacedPieces { get; init; }

        public IReadOnlyList<string> ColorPool { get; init; } = Array.Empty<string>();

        public int MoveLimit { get; init; }

        public IReadOnlyList<ObjectiveRaw> Objectives { get; init; } = Array.Empty<ObjectiveRaw>();

        public int Star1Score { get; init; }

        public int Star2Score { get; init; }

        public int Star3Score { get; init; }

        /// <summary>Null =&gt; defaults to <see cref="LevelDataConstants.DefaultRngSeed"/> (-1, §2).</summary>
        public int? RngSeed { get; init; }

        /// <summary>
        /// Additive optional field (TR-ldf-004), added at schema v1.1 with <b>no</b>
        /// schema_version bump per §3's additive-field policy. Null/absent =&gt; safe default
        /// (no display-name override; callers fall back to <see cref="DisplayNumber"/>).
        /// </summary>
        public string? DisplayName { get; init; }

        /// <summary>
        /// Names of any fields a source reader encountered but did not recognize as part of
        /// this schema. Never causes rejection — see class remarks.
        /// </summary>
        public IReadOnlyList<string>? UnrecognizedOptionalFieldNames { get; init; }
    }

    /// <summary>
    /// The result of <see cref="LevelData.FromRaw"/>: the resolved <see cref="LevelData"/> plus
    /// any non-fatal warnings (e.g. unrecognized optional fields) collected while parsing.
    /// </summary>
    /// <param name="Data">The fully-resolved, defaults-applied level data.</param>
    /// <param name="Warnings">
    /// Human-readable warnings — never thrown as exceptions, never logged by Domain itself
    /// (Domain is engine-free; the caller decides how/whether to surface them, e.g. via
    /// <c>Debug.LogWarning</c> at the Game layer).
    /// </param>
    /// <remarks>Story: E02-005 (leveldata-poco-schema).</remarks>
    public sealed record LevelDataParseResult(LevelData Data, IReadOnlyList<string> Warnings);
}
