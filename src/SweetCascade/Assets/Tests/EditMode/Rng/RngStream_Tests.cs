using System;
using System.Collections.Generic;
using NUnit.Framework;
using SweetCascade.Domain.Rng;

namespace SweetCascade.Domain.Tests.Rng
{
    /// <summary>
    /// Edit-Mode unit tests for <see cref="RngStream"/> — the SplitMix32 per-stream generator
    /// and its draw API (<c>NextRaw</c>/<c>NextInt</c>/<c>NextColor</c>/<c>NextFloat</c>/<c>Shuffle</c>).
    /// Tested in isolation from <see cref="RngService"/> (stream-name dispatch, session lifecycle,
    /// fork/log are <c>RngService_Tests.cs</c>'s concern).
    /// </summary>
    /// <remarks>
    /// Story: E02-001 (SplitMix32/<c>NextRaw</c>), E02-002 (draw API). Governing docs:
    /// <c>design/gdd/rng-service.md</c> Formulas F4–F6; <c>docs/architecture/adr-004-deterministic-rng.md</c> §1–§2.
    /// Golden sequences are sourced from <see cref="GoldenVectors"/> (generated from
    /// <c>golden/rng_golden_v1.json</c> + <c>golden/rng_golden_v1_draws.json</c> — never
    /// hand-typed here).
    /// </remarks>
    [TestFixture]
    public class RngStream_Tests
    {
        /// <summary>
        /// Reconstructs the F3-derived "board-refill" sub-seed for <paramref name="masterSeed"/>
        /// exactly as <c>rng_reference.py</c>'s <c>fresh_board_refill()</c> and
        /// <c>RngService.BeginSession</c> both do — <c>Mix32.Avalanche(Mix32.Combine(masterSeed,
        /// streamId))</c> — so a directly-constructed <see cref="RngStream"/> here matches the
        /// golden <c>next_raw_64</c>/<c>next_int_0_4_64</c>/<c>next_color_5pool_64</c>/
        /// <c>next_float_8</c> tables, which are all keyed by "master seed" but actually seeded
        /// with this sub-seed.
        /// </summary>
        private static uint BoardRefillStreamSeed(uint masterSeed) =>
            Mix32.Avalanche(Mix32.Combine(masterSeed, (uint)StreamRegistry.BoardRefill));

        // ------------------------------------------------------------------
        // SplitMix32 step sequence vs golden draw vectors (byte-for-byte, all 5 pinned seeds).
        // ------------------------------------------------------------------

        private static void AssertRawSequenceMatchesGolden(uint masterSeed, uint[] golden)
        {
            var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
            var actual = new uint[golden.Length];
            for (int i = 0; i < golden.Length; i++)
            {
                actual[i] = stream.NextRaw();
            }

            Assert.That(actual, Is.EqualTo(golden));
        }

        [Test]
        public void Test_NextRaw_MatchesGolden_ForMasterSeedZero() =>
            AssertRawSequenceMatchesGolden(GoldenVectors.MasterSeedZero, GoldenVectors.NextRaw64_Zero);

        [Test]
        public void Test_NextRaw_MatchesGolden_ForMasterSeedOne() =>
            AssertRawSequenceMatchesGolden(GoldenVectors.MasterSeedOne, GoldenVectors.NextRaw64_One);

        [Test]
        public void Test_NextRaw_MatchesGolden_ForMasterSeedMaxUint32() =>
            AssertRawSequenceMatchesGolden(GoldenVectors.MasterSeedMaxUint32, GoldenVectors.NextRaw64_MaxUint32);

        [Test]
        public void Test_NextRaw_MatchesGolden_ForF1AnchorMasterSeed() =>
            AssertRawSequenceMatchesGolden(GoldenVectors.MasterSeedF1Anchor, GoldenVectors.NextRaw64_F1Anchor);

        [Test]
        public void Test_NextRaw_MatchesGolden_ForF2AnchorMasterSeed() =>
            AssertRawSequenceMatchesGolden(GoldenVectors.MasterSeedF2Anchor, GoldenVectors.NextRaw64_F2Anchor);

        // ------------------------------------------------------------------
        // NextRaw/NextInt/NextRange determinism: same seed -> same sequence.
        // ------------------------------------------------------------------

        [Test]
        public void Test_NextRaw_SameSeed_ProducesIdenticalSequence()
        {
            var streamA = new RngStream(GoldenVectors.MasterSeedF1Anchor);
            var streamB = new RngStream(GoldenVectors.MasterSeedF1Anchor);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(streamA.NextRaw(), Is.EqualTo(streamB.NextRaw()), $"draw #{i}");
            }
        }

        [Test]
        public void Test_NextInt_SameSeed_ProducesIdenticalSequence()
        {
            var streamA = new RngStream(GoldenVectors.MasterSeedF1Anchor);
            var streamB = new RngStream(GoldenVectors.MasterSeedF1Anchor);
            for (int i = 0; i < 100; i++)
            {
                Assert.That(streamA.NextInt(0, 99), Is.EqualTo(streamB.NextInt(0, 99)), $"draw #{i}");
            }
        }

        // ------------------------------------------------------------------
        // Draw counter increments: each draw API call consumes exactly the documented number of
        // raw draws, verified by comparing where the "next" draw lands vs a parallel raw-only stream.
        // ------------------------------------------------------------------

        [Test]
        public void Test_NextInt_ConsumesExactlyOneRawDraw()
        {
            uint seed = GoldenVectors.MasterSeedF1Anchor;

            var streamA = new RngStream(seed);
            int nextIntResult = streamA.NextInt(10, 20);
            uint followUpRaw = streamA.NextRaw();

            var streamB = new RngStream(seed);
            uint firstRaw = streamB.NextRaw();
            uint secondRaw = streamB.NextRaw();

            Assert.That((uint)(nextIntResult - 10), Is.EqualTo(RngStream.MultiplyShiftIndex(firstRaw, 11u)),
                "NextInt(10,20) should be the multiply-shift of the FIRST raw draw.");
            Assert.That(followUpRaw, Is.EqualTo(secondRaw),
                "exactly one raw draw should have been consumed by NextInt -- the stream's next raw draw " +
                "must be the SECOND raw value, not a repeat of the first.");
        }

        [Test]
        public void Test_NextColor_ConsumesExactlyOneRawDraw()
        {
            uint seed = GoldenVectors.MasterSeedF1Anchor;

            var streamA = new RngStream(seed);
            streamA.NextColor(GoldenVectors.ColorPool5);
            uint followUpRaw = streamA.NextRaw();

            var streamB = new RngStream(seed);
            streamB.NextRaw();
            uint secondRaw = streamB.NextRaw();

            Assert.That(followUpRaw, Is.EqualTo(secondRaw));
        }

        [Test]
        public void Test_Shuffle_ConsumesExactlyLengthMinusOneRawDraws()
        {
            uint seed = GoldenVectors.MasterSeedF1Anchor;
            int[] input = { 10, 20, 30, 40, 50 };

            var streamA = new RngStream(seed);
            streamA.Shuffle<int>(input);
            uint followUpRaw = streamA.NextRaw();

            var streamB = new RngStream(seed);
            for (int i = 0; i < input.Length - 1; i++)
            {
                streamB.NextRaw();
            }

            uint expectedNextRaw = streamB.NextRaw();

            Assert.That(followUpRaw, Is.EqualTo(expectedNextRaw));
        }

        [Test]
        public void Test_Shuffle_Length0Or1_ConsumesZeroRawDraws()
        {
            uint seed = 777u;
            foreach (int[] input in new[] { Array.Empty<int>(), new[] { 42 } })
            {
                var streamA = new RngStream(seed);
                streamA.Shuffle<int>(input);
                uint afterShuffle = streamA.NextRaw();

                var streamB = new RngStream(seed);
                uint expected = streamB.NextRaw();

                Assert.That(afterShuffle, Is.EqualTo(expected), $"input length {input.Length}");
            }
        }

        // ------------------------------------------------------------------
        // Range mapping never returns >= range (F4/F5 structural clamp), across many draws and
        // several (min,max) pairs -- the multiplied-out equivalent of "10,000 draws never leave bounds".
        // ------------------------------------------------------------------

        [Test]
        public void Test_NextInt_NeverReturnsValueOutsideBounds_AcrossManyDrawsAndRangePairs()
        {
            (int Min, int Max)[] pairs =
            {
                (0, 4), (10, 20), (-50, 50), (7, 7), (0, 0), (-1000, 1000), (1, 2),
            };
            uint[] seeds = { 0u, 1u, uint.MaxValue, 0xDEADBEEFu, 42u };

            foreach (uint seed in seeds)
            {
                foreach ((int min, int max) in pairs)
                {
                    var stream = new RngStream(seed);
                    for (int i = 0; i < 2000; i++)
                    {
                        int value = stream.NextInt(min, max);
                        Assert.That(value, Is.InRange(min, max), $"seed={seed} range=[{min},{max}] draw={i}");
                    }
                }
            }
        }

        [Test]
        public void Test_NextInt_MinEqualsMax_AlwaysReturnsMinDeterministically()
        {
            var stream = new RngStream(GoldenVectors.MasterSeedZero);
            for (int i = 0; i < 50; i++)
            {
                Assert.That(stream.NextInt(7, 7), Is.EqualTo(7));
            }
        }

        [Test]
        public void Test_NextInt_InvalidRange_ThrowsImmediately_NoSilentSwap()
        {
            var stream = new RngStream(1u);
            Assert.Throws<ArgumentException>(() => stream.NextInt(10, 5));
        }

        [Test]
        public void Test_NextColor_EmptyPool_ThrowsImmediately_NoDefaultSubstituted()
        {
            var stream = new RngStream(1u);
            Assert.Throws<ArgumentException>(() => stream.NextColor(Array.Empty<string>()));
        }

        // ------------------------------------------------------------------
        // NextInt(0,4) / NextColor(5-pool) golden byte-for-byte sequences, all 5 pinned seeds.
        // ------------------------------------------------------------------

        private static void AssertNextIntZeroToFourMatchesGolden(uint masterSeed, int[] golden)
        {
            var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
            var actual = new int[golden.Length];
            for (int i = 0; i < golden.Length; i++)
            {
                actual[i] = stream.NextInt(0, 4);
            }

            Assert.That(actual, Is.EqualTo(golden));
        }

        [Test]
        public void Test_NextInt0To4_MatchesGolden_ForMasterSeedZero() =>
            AssertNextIntZeroToFourMatchesGolden(GoldenVectors.MasterSeedZero, GoldenVectors.NextInt0To4_64_Zero);

        [Test]
        public void Test_NextInt0To4_MatchesGolden_ForMasterSeedOne() =>
            AssertNextIntZeroToFourMatchesGolden(GoldenVectors.MasterSeedOne, GoldenVectors.NextInt0To4_64_One);

        [Test]
        public void Test_NextInt0To4_MatchesGolden_ForMasterSeedMaxUint32() =>
            AssertNextIntZeroToFourMatchesGolden(GoldenVectors.MasterSeedMaxUint32, GoldenVectors.NextInt0To4_64_MaxUint32);

        [Test]
        public void Test_NextInt0To4_MatchesGolden_ForF1AnchorMasterSeed() =>
            AssertNextIntZeroToFourMatchesGolden(GoldenVectors.MasterSeedF1Anchor, GoldenVectors.NextInt0To4_64_F1Anchor);

        [Test]
        public void Test_NextInt0To4_MatchesGolden_ForF2AnchorMasterSeed() =>
            AssertNextIntZeroToFourMatchesGolden(GoldenVectors.MasterSeedF2Anchor, GoldenVectors.NextInt0To4_64_F2Anchor);

        /// <summary>
        /// Data-consistency check within the golden fixtures themselves: <c>next_int_0_4_64</c>
        /// must equal <see cref="RngStream.MultiplyShiftIndex"/> applied to the corresponding
        /// <c>next_raw_64</c> value with <c>range=5</c>, at every index, for every pinned seed.
        /// Passing this proves <c>NextInt</c> really is "one raw draw, multiply-shifted" — not a
        /// coincidentally-matching but differently-implemented sequence.
        /// </summary>
        [Test]
        public void Test_NextInt0To4_EqualsMultiplyShiftIndexOfNextRaw_ForEveryPinnedSeed()
        {
            AssertConsistent(GoldenVectors.NextRaw64_Zero, GoldenVectors.NextInt0To4_64_Zero);
            AssertConsistent(GoldenVectors.NextRaw64_One, GoldenVectors.NextInt0To4_64_One);
            AssertConsistent(GoldenVectors.NextRaw64_MaxUint32, GoldenVectors.NextInt0To4_64_MaxUint32);
            AssertConsistent(GoldenVectors.NextRaw64_F1Anchor, GoldenVectors.NextInt0To4_64_F1Anchor);
            AssertConsistent(GoldenVectors.NextRaw64_F2Anchor, GoldenVectors.NextInt0To4_64_F2Anchor);

            static void AssertConsistent(uint[] raw, int[] nextInt)
            {
                for (int i = 0; i < raw.Length; i++)
                {
                    uint idx = RngStream.MultiplyShiftIndex(raw[i], 5u);
                    Assert.That((int)idx, Is.EqualTo(nextInt[i]), $"index {i}");
                }
            }
        }

        private static void AssertNextColorMatchesGolden(uint masterSeed, string[] golden)
        {
            var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
            var actual = new string[golden.Length];
            for (int i = 0; i < golden.Length; i++)
            {
                actual[i] = stream.NextColor(GoldenVectors.ColorPool5);
            }

            Assert.That(actual, Is.EqualTo(golden));
        }

        [Test]
        public void Test_NextColor_MatchesGolden_ForMasterSeedZero() =>
            AssertNextColorMatchesGolden(GoldenVectors.MasterSeedZero, GoldenVectors.NextColor5Pool64_Zero);

        [Test]
        public void Test_NextColor_MatchesGolden_ForMasterSeedOne() =>
            AssertNextColorMatchesGolden(GoldenVectors.MasterSeedOne, GoldenVectors.NextColor5Pool64_One);

        [Test]
        public void Test_NextColor_MatchesGolden_ForMasterSeedMaxUint32() =>
            AssertNextColorMatchesGolden(GoldenVectors.MasterSeedMaxUint32, GoldenVectors.NextColor5Pool64_MaxUint32);

        [Test]
        public void Test_NextColor_MatchesGolden_ForF1AnchorMasterSeed() =>
            AssertNextColorMatchesGolden(GoldenVectors.MasterSeedF1Anchor, GoldenVectors.NextColor5Pool64_F1Anchor);

        [Test]
        public void Test_NextColor_MatchesGolden_ForF2AnchorMasterSeed() =>
            AssertNextColorMatchesGolden(GoldenVectors.MasterSeedF2Anchor, GoldenVectors.NextColor5Pool64_F2Anchor);

        // ------------------------------------------------------------------
        // NextFloat golden sequences (off the hot path, but pinned by the fixture too).
        // ------------------------------------------------------------------

        private static void AssertNextFloatMatchesGolden(uint masterSeed, double[] golden)
        {
            var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
            for (int i = 0; i < golden.Length; i++)
            {
                Assert.That(stream.NextFloat(), Is.EqualTo(golden[i]).Within(0.0), $"index {i}");
            }
        }

        [Test]
        public void Test_NextFloat_MatchesGolden_ForMasterSeedZero() =>
            AssertNextFloatMatchesGolden(GoldenVectors.MasterSeedZero, GoldenVectors.NextFloat8_Zero);

        [Test]
        public void Test_NextFloat_MatchesGolden_ForF1AnchorMasterSeed() =>
            AssertNextFloatMatchesGolden(GoldenVectors.MasterSeedF1Anchor, GoldenVectors.NextFloat8_F1Anchor);

        // ------------------------------------------------------------------
        // Extra NextInt ranges (rng_golden_v1_draws.json) -- widens the determinism/oracle proof
        // beyond the (0,4) table to a wide range, a negative range, and a degenerate range.
        // ------------------------------------------------------------------

        [Test]
        public void Test_NextInt_ExtraRange10To20_MatchesGolden()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ExtraRangeMasterSeed));
            var actual = new int[GoldenVectors.NextIntRange10To20.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = stream.NextInt(GoldenVectors.Range10To20Min, GoldenVectors.Range10To20Max);
            }

            Assert.That(actual, Is.EqualTo(GoldenVectors.NextIntRange10To20));
        }

        [Test]
        public void Test_NextInt_ExtraRangeMinus50To50_MatchesGolden()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ExtraRangeMasterSeed));
            var actual = new int[GoldenVectors.NextIntRangeMinus50To50.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = stream.NextInt(GoldenVectors.RangeMinus50To50Min, GoldenVectors.RangeMinus50To50Max);
            }

            Assert.That(actual, Is.EqualTo(GoldenVectors.NextIntRangeMinus50To50));
        }

        [Test]
        public void Test_NextInt_ExtraRange7To7_MatchesGolden_DegenerateRange()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ExtraRangeMasterSeed));
            var actual = new int[GoldenVectors.NextIntRange7To7.Length];
            for (int i = 0; i < actual.Length; i++)
            {
                actual[i] = stream.NextInt(GoldenVectors.Range7To7Min, GoldenVectors.Range7To7Max);
            }

            Assert.That(actual, Is.EqualTo(GoldenVectors.NextIntRange7To7));
        }

        // ------------------------------------------------------------------
        // Shuffle: golden permutation + boundary lengths; multiset preservation; input purity.
        // ------------------------------------------------------------------

        [Test]
        public void Test_Shuffle_Len20_MatchesGoldenPermutation()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ShuffleMasterSeed));
            int[] result = stream.Shuffle<int>(GoldenVectors.ShuffleLen20Input);
            Assert.That(result, Is.EqualTo(GoldenVectors.ShuffleLen20Result));
        }

        [Test]
        public void Test_Shuffle_Len0_MatchesGolden()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ShuffleMasterSeed));
            int[] result = stream.Shuffle<int>(GoldenVectors.ShuffleLen0Input);
            Assert.That(result, Is.EqualTo(GoldenVectors.ShuffleLen0Result));
        }

        [Test]
        public void Test_Shuffle_Len1_MatchesGolden()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ShuffleMasterSeed));
            int[] result = stream.Shuffle<int>(GoldenVectors.ShuffleLen1Input);
            Assert.That(result, Is.EqualTo(GoldenVectors.ShuffleLen1Result));
        }

        [Test]
        public void Test_Shuffle_Len2_MatchesGolden()
        {
            var stream = new RngStream(BoardRefillStreamSeed(GoldenVectors.ShuffleMasterSeed));
            int[] result = stream.Shuffle<int>(GoldenVectors.ShuffleLen2Input);
            Assert.That(result, Is.EqualTo(GoldenVectors.ShuffleLen2Result));
        }

        [Test]
        public void Test_Shuffle_PreservesMultiset_ForSeveralLengths()
        {
            var stream = new RngStream(GoldenVectors.MasterSeedOne);
            foreach (int[] input in new[]
                     {
                         Array.Empty<int>(),
                         new[] { 42 },
                         new[] { 1, 2 },
                         new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 },
                     })
            {
                int[] result = stream.Shuffle<int>(input);
                var sortedInput = (int[])input.Clone();
                var sortedResult = (int[])result.Clone();
                Array.Sort(sortedInput);
                Array.Sort(sortedResult);

                Assert.That(sortedResult, Is.EqualTo(sortedInput), $"length {input.Length}");
            }
        }

        [Test]
        public void Test_Shuffle_DoesNotMutateInput_ForSeveralLengths()
        {
            var stream = new RngStream(GoldenVectors.MasterSeedOne);
            foreach (int[] input in new[]
                     {
                         Array.Empty<int>(),
                         new[] { 42 },
                         new[] { 1, 2 },
                         new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 },
                     })
            {
                var original = (int[])input.Clone();
                int[] result = stream.Shuffle<int>(input);

                Assert.That(input, Is.EqualTo(original), $"input mutated for length {input.Length}");
                Assert.That(result, Is.Not.SameAs(input), $"Shuffle must return a NEW array, length {input.Length}");
            }
        }

        // ------------------------------------------------------------------
        // Distribution fairness: test_uniform_color_distribution (Story E02-002 named AC).
        // ------------------------------------------------------------------

        /// <summary>
        /// Story E02-002's named AC: over 100,000 <c>NextColor</c> draws on a 5-element pool,
        /// each color's frequency must be within +/-2% of the ideal 20% share, AND a chi-square
        /// goodness-of-fit test against the uniform distribution must not reject uniformity at
        /// p &gt; 0.01 (chi-square critical value for df=4, alpha=0.01, is 13.277). Uses a fixed
        /// (never <c>System.Random</c>-derived — hard rule) seed so the result is fully
        /// deterministic and reproducible.
        /// </summary>
        [Test]
        public void Test_UniformColorDistribution_100000Draws_WithinTwoPercentBand_AndChiSquarePasses()
        {
            const int drawCount = 100_000;
            const double expectedShare = 1.0 / 5.0;
            const double toleranceBand = 0.02; // +/-2% of the 20% share
            const double chiSquareCriticalValueDf4Alpha01 = 13.277;

            // Arrange
            var stream = new RngStream(0xC0FFEEu);
            var counts = new Dictionary<string, int>();
            foreach (string color in GoldenVectors.ColorPool5)
            {
                counts[color] = 0;
            }

            // Act
            for (int i = 0; i < drawCount; i++)
            {
                string drawn = stream.NextColor(GoldenVectors.ColorPool5);
                counts[drawn]++;
            }

            // Assert
            double expectedCount = drawCount * expectedShare;
            double chiSquare = 0.0;
            foreach (string color in GoldenVectors.ColorPool5)
            {
                double observed = counts[color];
                double share = observed / drawCount;

                Assert.That(share, Is.InRange(expectedShare - toleranceBand, expectedShare + toleranceBand),
                    $"color '{color}': observed share {share:P2} outside the +/-2% band around 20%.");

                double diff = observed - expectedCount;
                chiSquare += (diff * diff) / expectedCount;
            }

            Assert.That(chiSquare, Is.LessThan(chiSquareCriticalValueDf4Alpha01),
                $"chi-square statistic {chiSquare:F3} exceeds the df=4, alpha=0.01 critical value " +
                $"{chiSquareCriticalValueDf4Alpha01} -- fails to confirm uniformity at p>0.01.");
        }
    }
}
