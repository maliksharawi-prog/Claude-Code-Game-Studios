namespace SweetCascade.Domain.Levels
{
    /// <summary>
    /// One pre-placed regular candy seeded at level start (level-data-format.md §2
    /// <c>pre_placed_pieces</c>). The piece is a normal, swappable, matchable tile from move 1 —
    /// never locked or immovable in schema v1.
    /// </summary>
    /// <param name="Row">Row index, expected within <c>[0, grid_height)</c> — bounds are validated in Story 006 (V9), not here.</param>
    /// <param name="Col">Column index, expected within <c>[0, grid_width)</c> — bounds are validated in Story 006 (V9), not here.</param>
    /// <param name="CandyType">Expected to be a member of the level's <c>color_pool</c> — validated in Story 006 (V10), not here.</param>
    /// <remarks>Story: E02-005 (leveldata-poco-schema).</remarks>
    public sealed record PrePlacedPiece(int Row, int Col, string CandyType);
}
