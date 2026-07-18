using System;
using System.Collections.Generic;

namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// One independently-seeded SplitMix32 stream (ADR-004 §1, rng-service.md §1). Holds both
    /// the stream's <em>initial</em> seed — used by <see cref="RngService.ForkStream"/> to
    /// derive a child independent of how many draws this stream has since consumed (ADR-004
    /// §3) — and its current mutable state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-001/002 (rng-primitives-seed-derivation / rng-draw-api). Governing docs:
    /// <c>design/gdd/rng-service.md</c> Formulas F4–F6; <c>docs/architecture/adr-004-deterministic-rng.md</c> §1–§2.
    /// </para>
    /// <para>
    /// Single-threaded by design (rng-service.md Edge Cases: "RNG Service has no concurrency
    /// model") — no locks, no <c>[ThreadStatic]</c>. <c>internal</c> — never part of the public
    /// <see cref="IRngService"/> surface; callers dispatch draws by stream name through
    /// <see cref="RngService"/>, which resolves the matching <see cref="RngStream"/> instance.
    /// </para>
    /// </remarks>
    internal sealed class RngStream
    {
        private uint _state;

        /// <summary>Constructs a stream with the given F3-derived (or ForkStream-derived) initial seed.</summary>
        internal RngStream(uint initialSeed)
        {
            InitialSeed = initialSeed;
            _state = initialSeed;
        }

        /// <summary>
        /// The stream's seed at session-start (or at fork time) — never mutated after
        /// construction. <see cref="RngService.ForkStream"/> reads this (not <see cref="_state"/>)
        /// so a fork is independent of how many draws this stream has already consumed.
        /// </summary>
        internal uint InitialSeed { get; }

        /// <summary>
        /// One atomic raw 32-bit draw: <c>state += GAMMA; return Mix32.Avalanche(state)</c>
        /// (ADR-004 §1's <c>NextRaw</c>). Every other draw operation on this stream consumes
        /// exactly one or more of these, in call order (no concurrency model).
        /// </summary>
        internal uint NextRaw()
        {
            unchecked
            {
                _state += RngConstants.Gamma;
                return Mix32.Avalanche(_state);
            }
        }

        /// <summary>
        /// The F4/F5 integer refinement (ADR-004 §2): the exact integer equivalent of
        /// <c>floor(next_float * range)</c>, computed as <c>(raw * range) >> 32</c> — never a
        /// 32-bit floating-point value, never <c>Math.Floor</c>. Extracted as its own internal static helper so
        /// the "boundary rounding is structurally impossible" claim
        /// (<c>test_boundary_rounding_clamped</c>, reframed per ADR-004 §2) is directly
        /// unit-testable in isolation: max index is always <c>range - 1</c>, even when
        /// <paramref name="raw"/> is <see cref="uint.MaxValue"/> — there is nothing to clamp.
        /// </summary>
        internal static uint MultiplyShiftIndex(uint raw, uint range)
        {
            return (uint)(((ulong)raw * range) >> 32);
        }

        /// <summary>
        /// Uniform integer in <c>[minValue, maxValue]</c> inclusive (GDD Formula F4), via
        /// <see cref="MultiplyShiftIndex"/>. Consumes exactly one raw draw. Throws immediately
        /// (no silent swap) when <paramref name="minValue"/> &gt; <paramref name="maxValue"/>
        /// (rng-service.md Edge Cases).
        /// </summary>
        internal int NextInt(int minValue, int maxValue)
        {
            if (minValue > maxValue)
            {
                throw new ArgumentException(
                    $"minValue ({minValue}) must be <= maxValue ({maxValue}).", nameof(minValue));
            }

            uint range = unchecked((uint)((long)maxValue - minValue + 1));
            uint idx = MultiplyShiftIndex(NextRaw(), range);
            return minValue + (int)idx;
        }

        /// <summary>
        /// Uniform pick from <paramref name="activeColors"/> (GDD Formula F5), via the same
        /// <see cref="MultiplyShiftIndex"/> as <see cref="NextInt"/>. Consumes exactly one raw
        /// draw. Throws immediately (no default substituted) when the pool is empty
        /// (rng-service.md Edge Cases).
        /// </summary>
        internal T NextColor<T>(IReadOnlyList<T> activeColors)
        {
            if (activeColors is null) throw new ArgumentNullException(nameof(activeColors));
            if (activeColors.Count == 0)
            {
                throw new ArgumentException("activeColors pool must not be empty.", nameof(activeColors));
            }

            uint idx = MultiplyShiftIndex(NextRaw(), (uint)activeColors.Count);
            return activeColors[(int)idx];
        }

        /// <summary>
        /// One uniform draw in <c>[0, 1)</c> as a byte-stable <see cref="double"/>
        /// (<c>raw * 2^-32</c>, exact in IEEE-754 double). Deliberately kept OFF the
        /// board-refill core draw path (ADR-004 §2/§4 — 32-bit floating-point arithmetic is forbidden on
        /// that path, and <c>double</c> is what the ADR keeps as the one off-path exception);
        /// this method exists only for a possible future non-hot-path caller per
        /// rng-service.md §6's <c>next_float</c> API entry.
        /// </summary>
        internal double NextFloat() => NextRaw() * RngConstants.RawToUnitDouble;

        /// <summary>
        /// Returns a uniformly-shuffled <b>copy</b> of <paramref name="source"/> — the input is
        /// never mutated. Fisher–Yates high-to-low (GDD Formula F6): for <c>i</c> from
        /// <c>length-1</c> down to 1, draw <c>j = NextInt(0, i)</c> and swap. Consumes exactly
        /// <c>source.Count - 1</c> raw draws (0 for length 0 or 1).
        /// </summary>
        internal T[] Shuffle<T>(IReadOnlyList<T> source)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));

            var result = new T[source.Count];
            for (int k = 0; k < result.Length; k++)
            {
                result[k] = source[k];
            }

            for (int i = result.Length - 1; i >= 1; i--)
            {
                int j = NextInt(0, i);
                (result[i], result[j]) = (result[j], result[i]);
            }

            return result;
        }
    }
}
