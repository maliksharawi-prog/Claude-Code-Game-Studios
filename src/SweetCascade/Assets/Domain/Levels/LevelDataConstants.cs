using System.Collections.Generic;

namespace SweetCascade.Domain.Levels
{
    /// <summary>
    /// Centralized, GDD-fixed constant table for the Level Data schema
    /// (level-data-format.md §2/§3/Tuning Knobs; control-manifest.md "one centralized Domain
    /// config location" rule). No gameplay/schema constant below may be duplicated as a
    /// scattered literal anywhere else in the codebase.
    /// </summary>
    /// <remarks>Story: E02-005 (leveldata-poco-schema).</remarks>
    public static class LevelDataConstants
    {
        /// <summary>Schema v1's own version number (level-data-format.md §3).</summary>
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// The full set of <c>schema_version</c> values this build accepts (§3: "v1's currently
        /// supported version set is exactly {1}"). Append-only as future schema versions ship.
        /// </summary>
        public static readonly IReadOnlyList<int> SupportedSchemaVersions = new[] { CurrentSchemaVersion };

        /// <summary>V7's minimum playable-cell floor (level-data-format.md §4 / Tuning Knobs).</summary>
        public const int MinPlayableCells = 16;

        /// <summary><c>rng_seed</c>'s documented default when the optional field is absent (§2: "-1 = no fixed seed").</summary>
        public const int DefaultRngSeed = -1;

        /// <summary><c>cell_mask</c>'s playable-cell character (§2).</summary>
        public const char PlayableCellChar = '1';

        /// <summary><c>cell_mask</c>'s void/cutout-cell character (§2).</summary>
        public const char VoidCellChar = '0';

        /// <summary>The v1 closed enum's wire value for <see cref="ObjectiveType.ScoreTarget"/> (§2 Objective Types table).</summary>
        public const string ObjectiveTypeScoreTarget = "score_target";

        /// <summary>The v1 closed enum's wire value for <see cref="ObjectiveType.CollectColor"/> (§2 Objective Types table).</summary>
        public const string ObjectiveTypeCollectColor = "collect_color";

        /// <summary>
        /// The five-candy canonical roster (design/art/art-bible.md Base Candy Roster;
        /// level-data-format.md V11) that every <c>color_pool</c>/<c>candy_type</c> value must
        /// be drawn from.
        /// </summary>
        public static readonly IReadOnlyList<string> CanonicalCandyRoster = new[]
        {
            "strawberry", "citrus", "lemon", "apple", "grape",
        };
    }
}
