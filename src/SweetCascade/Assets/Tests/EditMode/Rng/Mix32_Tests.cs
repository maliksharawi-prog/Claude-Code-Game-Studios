using System;
using NUnit.Framework;
using SweetCascade.Domain.Rng;

namespace SweetCascade.Domain.Tests.Rng
{
    /// <summary>
    /// Edit-Mode unit tests for the two normative RNG primitives (ADR-004 §1) —
    /// <see cref="Mix32.Combine"/> and <see cref="Mix32.Avalanche"/> — plus the F4/F5
    /// multiply-shift range map (<see cref="RngStream.MultiplyShiftIndex"/>) both draw APIs sit on.
    /// </summary>
    /// <remarks>
    /// Story: E02-001 (rng-primitives-seed-derivation), E02-002 (rng-draw-api — F4/F5 boundary
    /// reframing). Governing docs: <c>design/gdd/rng-service.md</c> Formulas F1–F5;
    /// <c>docs/architecture/adr-004-deterministic-rng.md</c> §1–§2. Golden anchors are sourced from
    /// <see cref="GoldenVectors"/> (generated from <c>golden/rng_golden_v1.json</c> — never
    /// hand-typed in this file).
    /// </remarks>
    [TestFixture]
    public class Mix32_Tests
    {
        // ------------------------------------------------------------------
        // AC-1: normative primitives & GDD anchors hold in code.
        // ------------------------------------------------------------------

        /// <summary>fmix32 fixes zero — the finalizer's defining property (ADR-004 §1).</summary>
        [Test]
        public void Test_Avalanche_Zero_ReturnsZero()
        {
            Assert.That(Mix32.Avalanche(0u), Is.EqualTo(GoldenVectors.AnchorMix32Zero));
        }

        [Test]
        public void Test_Combine_Anchor1007And3_Returns1547274724()
        {
            Assert.That(Mix32.Combine(1007u, 3u), Is.EqualTo(GoldenVectors.AnchorCombine1007_3));
        }

        [Test]
        public void Test_Combine_Anchor42And20650_Returns653539216()
        {
            Assert.That(Mix32.Combine(42u, 20650u), Is.EqualTo(GoldenVectors.AnchorCombine42_20650));
        }

        [Test]
        public void Test_Combine_Anchor500And1_Returns73026539()
        {
            Assert.That(Mix32.Combine(500u, 1u), Is.EqualTo(GoldenVectors.AnchorCombine500_1));
        }

        /// <summary>
        /// AC-1 edge case: "assert under a checked build config too (must not throw — arithmetic
        /// is unchecked)". <see cref="Mix32.Combine"/>'s body wraps its arithmetic in an explicit
        /// <c>unchecked</c> block, which overrides an outer <c>checked</c> context at the call
        /// site — this proves that override actually holds.
        /// </summary>
        [Test]
        public void Test_Combine_DoesNotThrow_UnderAnOuterCheckedContext()
        {
            Assert.DoesNotThrow(() =>
            {
                checked
                {
                    uint result = Mix32.Combine(uint.MaxValue, uint.MaxValue);
                    Assert.That(result, Is.TypeOf<uint>());
                }
            });
        }

        /// <summary>
        /// AC-1 edge case: "confirm Combine wraps mod 2^32 for max-range inputs". Recomputed
        /// independently via 64-bit arithmetic plus an explicit <c>% 2^32</c>, rather than
        /// re-invoking the primitive under test, to confirm the uint-overflow wraparound really
        /// is congruent mod 2^32 — not merely "didn't throw".
        /// </summary>
        [Test]
        public void Test_Combine_WrapsModulo2Pow32_ForMaxRangeInputs()
        {
            const ulong twoPow32 = 4294967296UL;
            uint a = uint.MaxValue;
            uint b = uint.MaxValue;
            uint expected = (uint)(((ulong)a * RngConstants.K1 + (ulong)b * RngConstants.K2) % twoPow32);

            Assert.That(Mix32.Combine(a, b), Is.EqualTo(expected));
        }

        [Test]
        public void Test_Avalanche_IsDeterministic_RepeatedCallsAgree()
        {
            uint x = 0xDEADBEEFu;
            Assert.That(Mix32.Avalanche(x), Is.EqualTo(Mix32.Avalanche(x)));
        }

        // ------------------------------------------------------------------
        // Avalanche sanity: a single-bit input change flips ~half the output bits (tolerance band).
        // ------------------------------------------------------------------

        /// <summary>
        /// Fixed (never <c>System.Random</c>-derived — hard rule) base values used to probe
        /// avalanche behaviour, so the test result never varies run-to-run.
        /// </summary>
        private static readonly uint[] AvalancheBaseValues =
        {
            0x00000000u, 0x00000001u, 0xFFFFFFFFu, 0x12345678u, 0x9E3779B9u,
            0xDEADBEEFu, 0x0F0F0F0Fu, 0xF0F0F0F0u, 0x55555555u, 0xAAAAAAAAu,
            0x00010203u, 0x7FFFFFFFu, 0x80000000u, 0x0000FFFFu, 0xFFFF0000u,
            0x13579BDFu,
        };

        /// <summary>
        /// ADR-004 §1: fmix32 is "strongly avalanching (flipping any single input bit flips ~50%
        /// of output bits)". A single sample's Hamming distance can vary quite a bit for a 32-bit
        /// avalanche finalizer (this is not a cryptographic hash), so each sample is bounded
        /// loosely (never degenerate, never saturated) and the aggregate mean across all
        /// base-value × bit-position samples is asserted against a tolerance band around the
        /// ideal 16/32 (50%).
        /// </summary>
        [Test]
        public void Test_Avalanche_SingleBitFlip_FlipsApproximatelyHalfOutputBits()
        {
            int sampleCount = 0;
            long totalHammingDistance = 0;

            foreach (uint baseValue in AvalancheBaseValues)
            {
                uint baseMixed = Mix32.Avalanche(baseValue);
                for (int bit = 0; bit < 32; bit++)
                {
                    uint flipped = baseValue ^ (1u << bit);
                    uint flippedMixed = Mix32.Avalanche(flipped);
                    int hamming = PopCount(baseMixed ^ flippedMixed);

                    sampleCount++;
                    totalHammingDistance += hamming;

                    Assert.That(hamming, Is.InRange(1, 31),
                        $"base=0x{baseValue:X8} bit={bit}: Hamming distance {hamming} outside the non-degenerate [1,31] band.");
                }
            }

            double meanHammingDistance = (double)totalHammingDistance / sampleCount;

            // +/-25% of 32 bits around the ideal 16/32 => [8,24].
            Assert.That(meanHammingDistance, Is.InRange(8.0, 24.0),
                $"Mean Hamming distance {meanHammingDistance:F2}/32 across {sampleCount} samples outside the [8,24] tolerance band.");
        }

        private static int PopCount(uint value)
        {
            int count = 0;
            while (value != 0)
            {
                count += (int)(value & 1u);
                value >>= 1;
            }

            return count;
        }

        // ------------------------------------------------------------------
        // Lemire multiply-shift range map: boundary cases (F4/F5 "structurally impossible clamp").
        // ------------------------------------------------------------------

        [Test]
        public void Test_MultiplyShiftIndex_RangeOne_AlwaysReturnsZero()
        {
            Assert.That(RngStream.MultiplyShiftIndex(0u, 1u), Is.EqualTo(0u));
            Assert.That(RngStream.MultiplyShiftIndex(1u, 1u), Is.EqualTo(0u));
            Assert.That(RngStream.MultiplyShiftIndex(uint.MaxValue, 1u), Is.EqualTo(0u));
            Assert.That(RngStream.MultiplyShiftIndex(GoldenVectors.NextRaw64_F1Anchor[0], 1u), Is.EqualTo(0u));
        }

        [Test]
        public void Test_MultiplyShiftIndex_RangeTwoToThe31_RawZero_ReturnsZero()
        {
            const uint rangeTwoToThe31 = 1u << 31;
            Assert.That(RngStream.MultiplyShiftIndex(0u, rangeTwoToThe31), Is.EqualTo(0u));
        }

        [Test]
        public void Test_MultiplyShiftIndex_RangeTwoToThe31_RawMaxValue_ReturnsRangeMinusOne()
        {
            const uint rangeTwoToThe31 = 1u << 31;
            Assert.That(RngStream.MultiplyShiftIndex(uint.MaxValue, rangeTwoToThe31), Is.EqualTo(rangeTwoToThe31 - 1));
        }

        [Test]
        public void Test_MultiplyShiftIndex_RawZero_AlwaysReturnsZero_RegardlessOfRange()
        {
            uint[] ranges = { 1u, 2u, 5u, 100u, 1u << 31, uint.MaxValue };
            foreach (uint range in ranges)
            {
                Assert.That(RngStream.MultiplyShiftIndex(0u, range), Is.EqualTo(0u), $"range={range}");
            }
        }

        /// <summary>
        /// ADR-004 §2 / Story E02-002 "test_boundary_rounding_clamped" reframing: the
        /// multiply-shift can never yield <c>range</c> — the maximum index is structurally
        /// <c>range - 1</c>, even at the largest possible raw draw. This is not a runtime clamp;
        /// it falls directly out of the <c>(raw * range) &gt;&gt; 32</c> formula.
        /// </summary>
        [Test]
        public void Test_MultiplyShiftIndex_RawMaxValue_NeverReturnsRange_StructurallyClampedToRangeMinusOne()
        {
            uint[] ranges = { 1u, 2u, 5u, 100u, 1u << 31, uint.MaxValue };
            foreach (uint range in ranges)
            {
                uint idx = RngStream.MultiplyShiftIndex(uint.MaxValue, range);
                Assert.That(idx, Is.EqualTo(range - 1), $"range={range}");
                Assert.That(idx, Is.LessThan(range), $"range={range}");
            }
        }
    }
}
