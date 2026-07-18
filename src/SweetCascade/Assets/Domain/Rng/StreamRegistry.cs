using System.Collections.Generic;

namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// The fixed, versioned, append-only stream registry (rng-service.md §2). <c>stream_id</c>
    /// values are assigned once and never reused or renumbered — because <c>stream_id</c> feeds
    /// Formula F3, renumbering would silently change every other stream's derived sequence.
    /// Every registered stream (whether or not it has a live consumer yet) receives a valid
    /// F3 sub-seed at every session start; this costs nothing and keeps numbering stable for
    /// the future consumer defined in that stream's owning system's GDD.
    /// </summary>
    /// <remarks>
    /// Story: E02-001. Governing doc: <c>design/gdd/rng-service.md</c> §2 (Stream Registry).
    /// </remarks>
    internal static class StreamRegistry
    {
        /// <summary>Match-3 Board Engine — gravity/refill candy selection, bootstrap fill, no-valid-moves reshuffle. Active (MVP).</summary>
        internal const string BoardRefillName = "board-refill";

        /// <summary>Special Candies &amp; Combo Matrix. Reserved (MVP infra ready) — this registry entry only reserves the stream; <c>special-candies.md</c> owns what it draws.</summary>
        internal const string SpecialDropName = "special-drop";

        /// <summary>Booster Brewing Meta (Phase 2, not yet designed). Reserved.</summary>
        internal const string HarvestName = "harvest";

        /// <summary>Events/Theming Engine (Phase 3, not yet designed). Reserved.</summary>
        internal const string EventsName = "events";

        internal const int BoardRefill = 1;
        internal const int SpecialDrop = 2;
        internal const int Harvest = 3;
        internal const int Events = 4;

        /// <summary>
        /// Every registered <c>(name, id)</c> pair, in registry order. <see cref="RngService"/>
        /// sub-seeds every entry here via Formula F3 at every session start, regardless of
        /// whether it has a live consumer.
        /// </summary>
        internal static readonly IReadOnlyList<(string Name, int Id)> All = new (string Name, int Id)[]
        {
            (BoardRefillName, BoardRefill),
            (SpecialDropName, SpecialDrop),
            (HarvestName, Harvest),
            (EventsName, Events),
        };
    }
}
