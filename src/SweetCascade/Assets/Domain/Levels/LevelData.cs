using System;
using System.Collections.Generic;

namespace SweetCascade.Domain.Levels
{
    /// <summary>
    /// The fully-resolved schema v1 level record (level-data-format.md §2) — every schema
    /// field, with optional fields already defaulted. Pure POCO: no engine type, no file IO.
    /// Constructed via <see cref="FromRaw"/> from a <see cref="LevelDataRaw"/> (or directly, by
    /// a future migration pipeline). The Game-layer <c>LevelDataAsset</c> (E06) wraps this as
    /// its <c>ToDomain()</c> return type.
    /// </summary>
    /// <remarks>
    /// Story: E02-005 (leveldata-poco-schema). Governing docs:
    /// <c>design/gdd/level-data-format.md</c> §2/§3; <c>docs/architecture/architecture.md</c> §6
    /// (Module Ownership — "LevelData schema (Domain POCO)"). V1–V19 <c>Validate()</c> rules and
    /// the V8 connectivity flood-fill are Story 006/007 — not implemented here.
    /// </remarks>
    public sealed class LevelData
    {
        /// <summary>Schema contract version this record was authored/parsed against. v1 files use 1.</summary>
        public int SchemaVersion { get; }

        /// <summary>
        /// Globally unique, stable identifier (format <c>&lt;region_code&gt;-&lt;3-digit-sequence&gt;</c>).
        /// Never reused. Save &amp; Persistence keys off this field, not <see cref="DisplayNumber"/>.
        /// </summary>
        public string LevelId { get; }

        /// <summary>Region slug this level belongs to.</summary>
        public string Region { get; }

        /// <summary>Player-facing level number (1-120 at launch scope). Independent of LevelId/Region ordering.</summary>
        public int DisplayNumber { get; }

        /// <summary>Board width in cells, schema range [3,9].</summary>
        public int GridWidth { get; }

        /// <summary>Board height in cells, schema range [3,9].</summary>
        public int GridHeight { get; }

        /// <summary>
        /// Row-major mask rows ('1' playable / '0' void). Defaults to a full
        /// <see cref="GridWidth"/>x<see cref="GridHeight"/> rectangle when the source omitted
        /// the field.
        /// </summary>
        public IReadOnlyList<string> CellMask { get; }

        /// <summary>Candies seeded at level start. Defaults to empty (fully RNG-generated board) when the source omitted the field.</summary>
        public IReadOnlyList<PrePlacedPiece> PrePlacedPieces { get; }

        /// <summary>
        /// 3-5 unique candy_type identifiers drawn from
        /// <see cref="LevelDataConstants.CanonicalCandyRoster"/> — defines which colors RNG
        /// Service may spawn on this level.
        /// </summary>
        public IReadOnlyList<string> ColorPool { get; }

        /// <summary>Number of swaps the player has to satisfy every objective. Schema minimum 1.</summary>
        public int MoveLimit { get; }

        /// <summary>
        /// All objectives (logical AND) to satisfy before move_limit is exhausted. List order is
        /// preserved (Game UI/Screens Flow badge display order).
        /// </summary>
        public IReadOnlyList<Objective> Objectives { get; }

        /// <summary>Score required for 1 star.</summary>
        public int Star1Score { get; }

        /// <summary>Score required for 2 stars. Must exceed Star1Score (validated in Story 006).</summary>
        public int Star2Score { get; }

        /// <summary>Score required for 3 stars. Must exceed Star2Score (validated in Story 006).</summary>
        public int Star3Score { get; }

        /// <summary>
        /// -1 = no fixed seed (RNG Service assigns a fresh seed per attempt — every MVP/launch
        /// level). &gt;=0 fixes the level's entire RNG stream. Defaults to -1 when the source
        /// omitted the field.
        /// </summary>
        public int RngSeed { get; }

        /// <summary>
        /// Additive optional field (TR-ldf-004) — added at schema v1.1 with <b>no</b>
        /// schema_version bump, demonstrating the additive-field migration policy
        /// (level-data-format.md §3 row 1). Null when the source omitted it or predates v1.1;
        /// callers fall back to <see cref="DisplayNumber"/>-based display in that case.
        /// </summary>
        public string? DisplayName { get; }

        /// <summary>
        /// Constructs an already-resolved <see cref="LevelData"/>. Prefer <see cref="FromRaw"/>
        /// when starting from an as-authored shape with optional fields to default.
        /// </summary>
        public LevelData(
            int schemaVersion,
            string levelId,
            string region,
            int displayNumber,
            int gridWidth,
            int gridHeight,
            IReadOnlyList<string> cellMask,
            IReadOnlyList<PrePlacedPiece> prePlacedPieces,
            IReadOnlyList<string> colorPool,
            int moveLimit,
            IReadOnlyList<Objective> objectives,
            int star1Score,
            int star2Score,
            int star3Score,
            int rngSeed,
            string? displayName)
        {
            SchemaVersion = schemaVersion;
            LevelId = levelId ?? throw new ArgumentNullException(nameof(levelId));
            Region = region ?? throw new ArgumentNullException(nameof(region));
            DisplayNumber = displayNumber;
            GridWidth = gridWidth;
            GridHeight = gridHeight;
            CellMask = cellMask ?? throw new ArgumentNullException(nameof(cellMask));
            PrePlacedPieces = prePlacedPieces ?? throw new ArgumentNullException(nameof(prePlacedPieces));
            ColorPool = colorPool ?? throw new ArgumentNullException(nameof(colorPool));
            MoveLimit = moveLimit;
            Objectives = objectives ?? throw new ArgumentNullException(nameof(objectives));
            Star1Score = star1Score;
            Star2Score = star2Score;
            Star3Score = star3Score;
            RngSeed = rngSeed;
            DisplayName = displayName;
        }

        /// <summary>
        /// Maps an as-authored <see cref="LevelDataRaw"/> into a fully-resolved
        /// <see cref="LevelData"/>: applies the three optional-field defaults (cell_mask,
        /// pre_placed_pieces, rng_seed), dispatches each objective's closed-enum <c>type</c> to
        /// its typed record, and surfaces (never throws for) any unrecognized optional field
        /// names as warnings. Pure function: no file IO, no engine type, no logging side effect
        /// (level-data-format.md §3; control-manifest.md Domain rules).
        /// </summary>
        /// <exception cref="ArgumentException">
        /// An <see cref="ObjectiveRaw"/>'s <see cref="ObjectiveRaw.Type"/> is not one of the v1
        /// closed-enum values (<c>score_target</c>/<c>collect_color</c>), or a recognized type
        /// is missing its required params. Unlike an unrecognized <em>optional field</em>, an
        /// unrecognized closed-enum value is never silently ignored or partially loaded
        /// (control-manifest.md Domain rule; level-data-format.md §3) — because an objective
        /// entry cannot be represented at all without knowing which typed record to build, this
        /// is enforced at parse time rather than deferred to Story 006's <c>Validate()</c>.
        /// </exception>
        public static LevelDataParseResult FromRaw(LevelDataRaw raw)
        {
            if (raw is null) throw new ArgumentNullException(nameof(raw));

            var warnings = new List<string>();

            IReadOnlyList<string> cellMask = raw.CellMask ?? BuildFullRectangleMask(raw.GridWidth, raw.GridHeight);
            IReadOnlyList<PrePlacedPiece> prePlacedPieces = raw.PrePlacedPieces ?? Array.Empty<PrePlacedPiece>();
            int rngSeed = raw.RngSeed ?? LevelDataConstants.DefaultRngSeed;

            var objectives = new List<Objective>(raw.Objectives.Count);
            foreach (ObjectiveRaw o in raw.Objectives)
            {
                objectives.Add(MapObjective(o));
            }

            if (raw.UnrecognizedOptionalFieldNames is { Count: > 0 } unrecognized)
            {
                foreach (string name in unrecognized)
                {
                    warnings.Add($"Unrecognized optional field '{name}' ignored (level-data-format.md §3 additive-field tolerance policy).");
                }
            }

            var data = new LevelData(
                schemaVersion: raw.SchemaVersion,
                levelId: raw.LevelId,
                region: raw.Region,
                displayNumber: raw.DisplayNumber,
                gridWidth: raw.GridWidth,
                gridHeight: raw.GridHeight,
                cellMask: cellMask,
                prePlacedPieces: prePlacedPieces,
                colorPool: raw.ColorPool,
                moveLimit: raw.MoveLimit,
                objectives: objectives,
                star1Score: raw.Star1Score,
                star2Score: raw.Star2Score,
                star3Score: raw.Star3Score,
                rngSeed: rngSeed,
                displayName: raw.DisplayName);

            return new LevelDataParseResult(data, warnings);
        }

        private static Objective MapObjective(ObjectiveRaw raw)
        {
            if (raw is null) throw new ArgumentNullException(nameof(raw));

            switch (raw.Type)
            {
                case LevelDataConstants.ObjectiveTypeScoreTarget:
                    if (raw.Target is not int target)
                    {
                        throw new ArgumentException(
                            $"Objective type '{LevelDataConstants.ObjectiveTypeScoreTarget}' requires 'target'.", nameof(raw));
                    }

                    return new ScoreTargetObjective(target);

                case LevelDataConstants.ObjectiveTypeCollectColor:
                    if (raw.Color is not string color || raw.Count is not int count)
                    {
                        throw new ArgumentException(
                            $"Objective type '{LevelDataConstants.ObjectiveTypeCollectColor}' requires 'color' and 'count'.", nameof(raw));
                    }

                    return new CollectColorObjective(color, count);

                default:
                    throw new ArgumentException(
                        $"Unrecognized objective type '{raw.Type}' — not a v1 closed-enum value " +
                        $"({LevelDataConstants.ObjectiveTypeScoreTarget}/{LevelDataConstants.ObjectiveTypeCollectColor}).",
                        nameof(raw));
            }
        }

        private static IReadOnlyList<string> BuildFullRectangleMask(int gridWidth, int gridHeight)
        {
            var rows = new string[Math.Max(gridHeight, 0)];
            string fullRow = new string(LevelDataConstants.PlayableCellChar, Math.Max(gridWidth, 0));
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = fullRow;
            }

            return rows;
        }
    }
}
