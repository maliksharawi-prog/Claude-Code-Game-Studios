namespace SweetCascade.Domain.Levels
{
    /// <summary>
    /// Schema v1's closed objective-type enum (level-data-format.md §2 "Objective Types") —
    /// exactly two members at v1. Appending a new member requires a <c>schema_version</c> bump
    /// per §3's closed-enum versioning policy; never insert/reorder/remove an existing member.
    /// </summary>
    /// <remarks>Story: E02-005 (leveldata-poco-schema).</remarks>
    public enum ObjectiveType
    {
        /// <summary><c>score_target {target:int}</c> — live score &gt;= target before move_limit is exhausted.</summary>
        ScoreTarget = 0,

        /// <summary><c>collect_color {color:string, count:int}</c> — cumulative cleared tiles of color &gt;= count before move_limit is exhausted.</summary>
        CollectColor = 1,
    }

    /// <summary>
    /// Base type for a schema v1 level objective. All objectives in a level's <c>objectives</c>
    /// list must be satisfied (logical AND) before <c>move_limit</c> is exhausted to win. List
    /// order is preserved end-to-end (Game UI/Screens Flow reads order for badge display).
    /// </summary>
    /// <param name="Type">Discriminates which typed subtype this instance is.</param>
    /// <remarks>Story: E02-005 (leveldata-poco-schema).</remarks>
    public abstract record Objective(ObjectiveType Type);

    /// <summary>Win when live score &gt;= <see cref="Target"/> before move_limit is exhausted.</summary>
    /// <param name="Target">Must be &gt; 0 (validated in Story 006, V14 — not enforced here).</param>
    public sealed record ScoreTargetObjective(int Target) : Objective(ObjectiveType.ScoreTarget);

    /// <summary>Win when cumulative cleared tiles of <see cref="Color"/> &gt;= <see cref="Count"/> before move_limit is exhausted.</summary>
    /// <param name="Color">Expected to be a member of the level's color_pool (validated in Story 006, V15 — not enforced here).</param>
    /// <param name="Count">Must be &gt; 0 (validated in Story 006, V15 — not enforced here).</param>
    public sealed record CollectColorObjective(string Color, int Count) : Objective(ObjectiveType.CollectColor);

    /// <summary>
    /// The as-authored shape of one <c>objectives[]</c> entry, before type-dispatch — the
    /// schema's flat per-type param shape (<c>{type, target}</c> or <c>{type, color, count}</c>).
    /// <see cref="LevelData.FromRaw"/> maps <see cref="Type"/> of
    /// <see cref="LevelDataConstants.ObjectiveTypeScoreTarget"/>/<see cref="LevelDataConstants.ObjectiveTypeCollectColor"/>
    /// to the matching typed <see cref="Objective"/> subtype.
    /// </summary>
    /// <param name="Type">The wire-format closed-enum string. Any value other than the two v1 constants is a hard failure — see <see cref="LevelData.FromRaw"/>'s remarks.</param>
    /// <param name="Target"><c>score_target</c>'s param.</param>
    /// <param name="Color"><c>collect_color</c>'s color param.</param>
    /// <param name="Count"><c>collect_color</c>'s count param.</param>
    /// <remarks>Story: E02-005 (leveldata-poco-schema).</remarks>
    public sealed record ObjectiveRaw(string Type, int? Target = null, string? Color = null, int? Count = null);
}
