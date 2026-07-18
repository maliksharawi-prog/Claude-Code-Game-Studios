namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// The two normative RNG primitives from ADR-004 §1: <see cref="Combine"/> (GDD F1/F2/F3's
    /// <c>combine()</c>) and <see cref="Avalanche"/> (rng-service.md's <c>mix32()</c> — the
    /// MurmurHash3 <c>fmix32</c> finalizer). Every operation is <c>unchecked uint</c> arithmetic
    /// with logical (never arithmetic) right-shifts, so two independent implementations agree
    /// bit-for-bit (ADR-004 Summary — this is what lets <c>tools/ci/rng_reference.py</c> and
    /// this class produce byte-identical output).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-001. Governing docs: <c>design/gdd/rng-service.md</c> Formulas F1–F3;
    /// <c>docs/architecture/adr-004-deterministic-rng.md</c> §1.
    /// </para>
    /// <para>
    /// GDD-verified anchors (re-verified independently in <c>tools/ci/rng_reference.py</c>
    /// before the golden fixture was frozen — see that script's <c>--check-anchors</c> output):
    /// <c>Avalanche(0) == 0</c> (fmix32 fixes zero); <c>Combine(1007,3) == 1547274724</c>;
    /// <c>Combine(42,20650) == 653539216</c>; <c>Combine(500,1) == 73026539</c>.
    /// </para>
    /// <para>
    /// Named <c>Mix32</c> to mirror ADR-004 §1's function names 1:1 in the file/type name; the
    /// two members are named <see cref="Combine"/> and <see cref="Avalanche"/> (a C# type may
    /// not declare a method with the same name as its enclosing type) — <c>Avalanche</c> is the
    /// ADR/GDD's <c>mix32(x)</c> function precisely.
    /// </para>
    /// </remarks>
    internal static class Mix32
    {
        /// <summary>
        /// <c>(a*K1 + b*K2) mod 2^32</c> — GDD F1/F2/F3's <c>combine()</c>, verbatim. Used for
        /// master-seed derivation (F1/F2), stream sub-seed derivation (F3), and
        /// <see cref="RngService.ForkStream"/>'s child-seed derivation.
        /// </summary>
        internal static uint Combine(uint a, uint b)
        {
            unchecked
            {
                return a * RngConstants.K1 + b * RngConstants.K2;
            }
        }

        /// <summary>
        /// The MurmurHash3 <c>fmix32</c> avalanche finalizer — this IS rng-service.md's
        /// <c>mix32()</c>. Deterministic, strongly avalanching (flipping any single input bit
        /// flips ~50% of output bits), and platform-stable (uint logical shifts + unchecked
        /// multiplication only — no operation whose result depends on the runtime backend).
        /// </summary>
        internal static uint Avalanche(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= RngConstants.M1;
                x ^= x >> 13;
                x *= RngConstants.M2;
                x ^= x >> 16;
                return x;
            }
        }
    }
}
