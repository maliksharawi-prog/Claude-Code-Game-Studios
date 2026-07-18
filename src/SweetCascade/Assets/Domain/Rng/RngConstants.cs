namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// Centralized, GDD/ADR-fixed constant table for the RNG Service. Every mixing/combining
    /// constant used anywhere in <c>SweetCascade.Domain.Rng</c> is declared exactly once here —
    /// per the control-manifest.md Domain rule "every GDD/ADR constant lives in one centralized
    /// Domain config location" — no scattered literal may duplicate one of these values.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-001 (rng-primitives-seed-derivation). Governing docs:
    /// <c>design/gdd/rng-service.md</c> Formulas F1–F3, Tuning Knobs;
    /// <c>docs/architecture/adr-004-deterministic-rng.md</c> §1 ("The normative primitives").
    /// </para>
    /// <para>
    /// DO NOT change any value below without an <see cref="AlgorithmVersion"/> bump — a silent
    /// change here would silently invalidate every previously-logged bug-repro seed
    /// (rng-service.md Edge Cases — "algorithm version changes").
    /// </para>
    /// </remarks>
    internal static class RngConstants
    {
        /// <summary>
        /// <c>combine()</c>'s term for the "a" input (GDD F1/F2/F3) — Knuth's 32-bit
        /// multiplicative hash constant, ≈2^32/φ. Value: 2,654,435,761.
        /// </summary>
        internal const uint K1 = 0x9E3779B1u;

        /// <summary>
        /// <c>combine()</c>'s term for the "b" input (GDD F1/F2/F3) — an arbitrary odd 32-bit
        /// constant. Value: 40,503.
        /// </summary>
        internal const uint K2 = 0x9E37u;

        /// <summary>
        /// SplitMix32's Weyl increment (ADR-004's generator choice). NOTE: <c>GAMMA != K1</c>
        /// (0x…B9 vs 0x…B1) — do not confuse the two. Value: 2,654,435,769.
        /// </summary>
        internal const uint Gamma = 0x9E3779B9u;

        /// <summary>MurmurHash3 <c>fmix32</c> multiplier 1. Value: 2,246,822,507.</summary>
        internal const uint M1 = 0x85EBCA6Bu;

        /// <summary>MurmurHash3 <c>fmix32</c> multiplier 2. Value: 3,266,489,909.</summary>
        internal const uint M2 = 0xC2B2AE35u;

        /// <summary>
        /// FNV-1a-32 offset basis — the SAME primitive (by value) shared with the Save checksum
        /// (ADR-003), used here to hash <see cref="RngService.ForkStream"/> labels. Value:
        /// 2,166,136,261.
        /// </summary>
        internal const uint FnvOffsetBasis = 0x811C9DC5u;

        /// <summary>
        /// FNV-1a-32 prime — the SAME primitive (by value) shared with the Save checksum
        /// (ADR-003). Value: 16,777,619.
        /// </summary>
        internal const uint FnvPrime = 0x01000193u;

        /// <summary>
        /// <c>2^-32</c> as an exact IEEE-754 <see cref="double"/> — scales a raw uint32 draw
        /// into <c>[0,1)</c> for <see cref="RngStream.NextFloat"/>. Exact because
        /// <c>raw &lt; 2^32 &lt;= 2^53</c> and the scale is a power of two (ADR-004 §2).
        /// </summary>
        internal const double RawToUnitDouble = 1.0 / 4294967296.0;

        /// <summary>
        /// rng-service.md Tuning Knobs — the mix32-finalizer version tag written into every
        /// <see cref="RngSessionLog"/>. A finalizer/generator change is a disclosed version
        /// bump, never a silent edit.
        /// </summary>
        internal const string AlgorithmVersion = "v1";
    }
}
