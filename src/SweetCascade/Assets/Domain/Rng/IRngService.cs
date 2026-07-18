using System.Collections.Generic;

namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// Sweet Cascade's deterministic, seedable, stream-isolated randomness service
    /// (design/gdd/rng-service.md, ADR-004). Every board/cascade/refill draw in the game flows
    /// through this interface so that a logged seed can byte-for-byte reproduce an entire
    /// session on any platform (Mono/IL2CPP/WebGL).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-001/002/003. This surface maps 1:1 to rng-service.md §6 — no operation is
    /// added or removed.
    /// </para>
    /// <para>
    /// <b>Honest Randomness Contract</b> (rng-service.md §4): note that <b>no member below
    /// accepts a player-skill/streak/performance/spend parameter</b> — this is architecturally
    /// enforced, not just documented (verified by interface inspection in
    /// <c>test_daily_seed_excludes_player_data</c> /
    /// <c>test_no_hidden_bias_parameter_on_draw_functions</c>).
    /// </para>
    /// </remarks>
    public interface IRngService
    {
        /// <summary>
        /// Begins a session for a standard level attempt. <c>master_seed = F1(levelId,
        /// attemptNumber)</c>; every registered stream (<see cref="StreamRegistry"/>) is
        /// re-derived via F3; the session log (see <see cref="GetSessionLog"/>) is (re)written.
        /// A fresh call is a hard reset — no state carries over from a previous session.
        /// </summary>
        void StartLevelSession(int levelId, int attemptNumber);

        /// <summary>
        /// Begins a session for a shared-seed daily challenge. <c>master_seed =
        /// F2(dailyChallengeId, calendarDateUtc)</c> — deliberately no player-identifying or
        /// player-performance input (Honest Randomness Contract), so every player faces the
        /// identical board for the same challenge/date.
        /// </summary>
        void StartDailySession(int dailyChallengeId, int calendarDateUtc);

        /// <summary>
        /// Test/debug entry point: sets <c>master_seed</c> directly, bypassing F1/F2 entirely,
        /// for full deterministic control over test fixtures.
        /// </summary>
        void StartTestSession(uint masterSeed);

        /// <summary>
        /// One uniform draw in <c>[0, 1)</c> as a <see cref="double"/>. Deliberately off the
        /// board-refill core draw path (ADR-004 §2) — provided for a possible future
        /// non-hot-path caller per rng-service.md §6's <c>next_float</c> entry.
        /// </summary>
        double NextFloat(string streamName);

        /// <summary>
        /// Uniform integer in <c>[minValue, maxValue]</c> inclusive (GDD Formula F4). Throws
        /// immediately (no silent swap) if <paramref name="minValue"/> &gt;
        /// <paramref name="maxValue"/>.
        /// </summary>
        int NextInt(string streamName, int minValue, int maxValue);

        /// <summary>
        /// Uniform pick from <paramref name="activeColors"/> (GDD Formula F5). Throws
        /// immediately (no default color substituted) if the pool is empty.
        /// </summary>
        T NextColor<T>(string streamName, IReadOnlyList<T> activeColors);

        /// <summary>
        /// Returns a uniformly-shuffled <b>copy</b> of <paramref name="source"/> (GDD Formula
        /// F6, Fisher–Yates); never mutates <paramref name="source"/>.
        /// </summary>
        T[] Shuffle<T>(string streamName, IReadOnlyList<T> source);

        /// <summary>
        /// Deterministically derives — or, if already forked earlier this session with the same
        /// arguments, returns — a child stream scoped to <c>(parentStreamName, label)</c>. The
        /// child is derived from the parent's <em>initial</em> seed, so fork timing (before or
        /// after the parent has been drawn from) never changes the child's sequence. Idempotent:
        /// a second call with identical arguments returns the same, continuing child stream
        /// (never a reset).
        /// </summary>
        string ForkStream(string parentStreamName, string label);

        /// <summary>Returns the current session's bug-repro record (rng-service.md §3).</summary>
        RngSessionLog GetSessionLog();
    }
}
