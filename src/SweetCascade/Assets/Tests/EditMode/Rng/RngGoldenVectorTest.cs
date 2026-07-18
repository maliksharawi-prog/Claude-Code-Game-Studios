using System.Collections.Generic;
using NUnit.Framework;
using SweetCascade.Domain.Rng;

namespace SweetCascade.Domain.Tests.Rng
{
    /// <summary>
    /// Story E02-004's dedicated byte-for-byte regression suite (ADR-004 §5) — the "standing
    /// regression gate": every section of <c>golden/rng_golden_v1.json</c> (and its additive
    /// extension, <c>golden/rng_golden_v1_draws.json</c>) is asserted against the LIVE
    /// implementation in one consolidated file, so a single drifted constant anywhere in
    /// <c>Assets/Domain/Rng/**</c> fails exactly this suite.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-004 (rng-golden-vector-suite). Governing docs:
    /// <c>docs/architecture/adr-004-deterministic-rng.md</c> §5–§6.
    /// </para>
    /// <para>
    /// Relationship to the other Edit-Mode test files: <see cref="Mix32_Tests"/>,
    /// <see cref="RngStream_Tests"/>, and <see cref="RngService_Tests"/> already exercise most of
    /// these same golden values at the scenario/behaviour level (determinism, isolation, bounds,
    /// idempotency, etc.) — this file's job is narrower and more literal: prove EVERY value in
    /// both golden fixtures round-trips byte-for-byte against the live implementation, as one
    /// single, holistic "does the fixture still match reality" gate (ADR-004 §5's "byte-for-byte
    /// regression suite" deliverable). Overlap with the other files is intentional, not
    /// redundant — losing either kind of coverage would leave a gap.
    /// </para>
    /// <para>
    /// AC-3 ("regression alarm — a deliberately altered constant fails the suite") is structural,
    /// not a separate test: because every assertion below compares the LIVE implementation
    /// against <see cref="GoldenVectors"/>' embedded constants, any drift in
    /// <c>Assets/Domain/Rng/**</c> (e.g. a changed <c>RngConstants</c> value) fails one or more
    /// of the tests in this file by construction — that IS the regression alarm.
    /// </para>
    /// <para>
    /// AC-4 ("blocking-gate wiring" — <c>game-ci/unity-test-runner@v4</c> running this suite as a
    /// required PR check) is CI/workflow infrastructure owned by Story E01-004, not this file;
    /// per <c>production/sprint-status.yaml</c> that story is blocked externally on the
    /// <c>UNITY_LICENSE</c> secret. This suite is ready to be wired into that gate the moment
    /// E01-004 unblocks — nothing here needs to change for that to happen.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class RngGoldenVectorTest
    {
        private static uint BoardRefillStreamSeed(uint masterSeed) =>
            Mix32.Avalanche(Mix32.Combine(masterSeed, (uint)StreamRegistry.BoardRefill));

        // ------------------------------------------------------------------
        // Anchors (rng_golden_v1.json "anchors").
        // ------------------------------------------------------------------

        [Test]
        public void Test_Fixture_Anchors_MatchLiveImplementation()
        {
            Assert.That(Mix32.Avalanche(0u), Is.EqualTo(GoldenVectors.AnchorMix32Zero));
            Assert.That(Mix32.Combine(1007u, 3u), Is.EqualTo(GoldenVectors.AnchorCombine1007_3));
            Assert.That(Mix32.Combine(42u, 20650u), Is.EqualTo(GoldenVectors.AnchorCombine42_20650));
            Assert.That(Mix32.Combine(500u, 1u), Is.EqualTo(GoldenVectors.AnchorCombine500_1));
        }

        // ------------------------------------------------------------------
        // seed_derivation (F1/F2/F3).
        // ------------------------------------------------------------------

        [Test]
        public void Test_Fixture_F1F2SeedDerivation_MatchesLiveImplementation()
        {
            var levelService = new RngService(new FakeClock());
            levelService.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            Assert.That(levelService.GetSessionLog().MasterSeed, Is.EqualTo(GoldenVectors.F1MasterSeedLevel1007Attempt3));

            var dailyService = new RngService(new FakeClock());
            dailyService.StartDailySession(GoldenVectors.F2DailyChallengeId, GoldenVectors.F2CalendarDateUtc);
            Assert.That(dailyService.GetSessionLog().MasterSeed, Is.EqualTo(GoldenVectors.F2MasterSeedDaily42Day20650));
        }

        [Test]
        public void Test_Fixture_F3StreamSeeds_MatchLiveImplementation_ForBothPinnedMasterSeeds()
        {
            var serviceFor500 = new RngService(new FakeClock());
            serviceFor500.StartTestSession(500u);
            foreach ((int _, string streamName, uint expectedSeed) in GoldenVectors.F3StreamSeedsForMasterSeed500)
            {
                Assert.That(serviceFor500.DebugGetStream(streamName).InitialSeed, Is.EqualTo(expectedSeed), streamName);
            }

            var serviceForF1Anchor = new RngService(new FakeClock());
            serviceForF1Anchor.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            foreach ((int _, string streamName, uint expectedSeed) in GoldenVectors.F3StreamSeedsForMasterSeedF1Anchor)
            {
                Assert.That(serviceForF1Anchor.DebugGetStream(streamName).InitialSeed, Is.EqualTo(expectedSeed), streamName);
            }
        }

        // ------------------------------------------------------------------
        // next_raw_64 / next_int_0_4_64 / next_color_5pool_64 / next_float_8 -- all 5 pinned seeds.
        // ------------------------------------------------------------------

        private static readonly (uint MasterSeed, uint[] Raw64, int[] Int0To4_64, string[] Color5Pool64, double[] Float8)[] PinnedSeedTables =
        {
            (GoldenVectors.MasterSeedZero, GoldenVectors.NextRaw64_Zero, GoldenVectors.NextInt0To4_64_Zero, GoldenVectors.NextColor5Pool64_Zero, GoldenVectors.NextFloat8_Zero),
            (GoldenVectors.MasterSeedOne, GoldenVectors.NextRaw64_One, GoldenVectors.NextInt0To4_64_One, GoldenVectors.NextColor5Pool64_One, GoldenVectors.NextFloat8_One),
            (GoldenVectors.MasterSeedMaxUint32, GoldenVectors.NextRaw64_MaxUint32, GoldenVectors.NextInt0To4_64_MaxUint32, GoldenVectors.NextColor5Pool64_MaxUint32, GoldenVectors.NextFloat8_MaxUint32),
            (GoldenVectors.MasterSeedF1Anchor, GoldenVectors.NextRaw64_F1Anchor, GoldenVectors.NextInt0To4_64_F1Anchor, GoldenVectors.NextColor5Pool64_F1Anchor, GoldenVectors.NextFloat8_F1Anchor),
            (GoldenVectors.MasterSeedF2Anchor, GoldenVectors.NextRaw64_F2Anchor, GoldenVectors.NextInt0To4_64_F2Anchor, GoldenVectors.NextColor5Pool64_F2Anchor, GoldenVectors.NextFloat8_F2Anchor),
        };

        [Test]
        public void Test_Fixture_NextRaw64_AllPinnedSeeds_MatchLiveImplementation()
        {
            foreach ((uint masterSeed, uint[] golden, _, _, _) in PinnedSeedTables)
            {
                var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
                var actual = new uint[golden.Length];
                for (int i = 0; i < golden.Length; i++)
                {
                    actual[i] = stream.NextRaw();
                }

                Assert.That(actual, Is.EqualTo(golden), $"master_seed={masterSeed}");
            }
        }

        [Test]
        public void Test_Fixture_NextInt0To4_64_AllPinnedSeeds_MatchLiveImplementation()
        {
            foreach ((uint masterSeed, _, int[] golden, _, _) in PinnedSeedTables)
            {
                var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
                var actual = new int[golden.Length];
                for (int i = 0; i < golden.Length; i++)
                {
                    actual[i] = stream.NextInt(0, 4);
                }

                Assert.That(actual, Is.EqualTo(golden), $"master_seed={masterSeed}");
            }
        }

        [Test]
        public void Test_Fixture_NextColor5Pool64_AllPinnedSeeds_MatchLiveImplementation()
        {
            foreach ((uint masterSeed, _, _, string[] golden, _) in PinnedSeedTables)
            {
                var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
                var actual = new string[golden.Length];
                for (int i = 0; i < golden.Length; i++)
                {
                    actual[i] = stream.NextColor(GoldenVectors.ColorPool5);
                }

                Assert.That(actual, Is.EqualTo(golden), $"master_seed={masterSeed}");
            }
        }

        /// <summary>
        /// AC-1's "NextFloat entries use the exact round-trip double formatting so re-reads
        /// reproduce the stored value" — <see cref="GoldenVectors"/>' <c>double</c> constants were
        /// emitted with the same digits Python's <c>repr()</c> produced (shortest exact
        /// round-trip decimal), so an exact equality (not a tolerance) is the correct assertion.
        /// </summary>
        [Test]
        public void Test_Fixture_NextFloat8_AllPinnedSeeds_MatchLiveImplementation_ExactRoundTrip()
        {
            foreach ((uint masterSeed, _, _, _, double[] golden) in PinnedSeedTables)
            {
                var stream = new RngStream(BoardRefillStreamSeed(masterSeed));
                for (int i = 0; i < golden.Length; i++)
                {
                    Assert.That(stream.NextFloat(), Is.EqualTo(golden[i]), $"master_seed={masterSeed} index={i}");
                }
            }
        }

        // ------------------------------------------------------------------
        // shuffle -- len_20/len_0/len_1/len_2, including the documented draws_consumed.
        // ------------------------------------------------------------------

        [Test]
        public void Test_Fixture_Shuffle_AllBoundaryLengths_MatchLiveImplementation()
        {
            RngStream Fresh() => new RngStream(BoardRefillStreamSeed(GoldenVectors.ShuffleMasterSeed));

            Assert.That(Fresh().Shuffle<int>(GoldenVectors.ShuffleLen20Input), Is.EqualTo(GoldenVectors.ShuffleLen20Result));
            Assert.That(Fresh().Shuffle<int>(GoldenVectors.ShuffleLen0Input), Is.EqualTo(GoldenVectors.ShuffleLen0Result));
            Assert.That(Fresh().Shuffle<int>(GoldenVectors.ShuffleLen1Input), Is.EqualTo(GoldenVectors.ShuffleLen1Result));
            Assert.That(Fresh().Shuffle<int>(GoldenVectors.ShuffleLen2Input), Is.EqualTo(GoldenVectors.ShuffleLen2Result));
        }

        [Test]
        public void Test_Fixture_Shuffle_DrawsConsumed_MatchesDocumentedCountsPerLength()
        {
            AssertDrawsConsumed(GoldenVectors.ShuffleLen20Input, GoldenVectors.ShuffleLen20DrawsConsumed);
            AssertDrawsConsumed(GoldenVectors.ShuffleLen0Input, GoldenVectors.ShuffleLen0DrawsConsumed);
            AssertDrawsConsumed(GoldenVectors.ShuffleLen1Input, GoldenVectors.ShuffleLen1DrawsConsumed);
            AssertDrawsConsumed(GoldenVectors.ShuffleLen2Input, GoldenVectors.ShuffleLen2DrawsConsumed);

            static void AssertDrawsConsumed(int[] input, int expectedDraws)
            {
                var streamA = new RngStream(GoldenVectors.ShuffleMasterSeed);
                streamA.Shuffle<int>(input);
                uint followUpRaw = streamA.NextRaw();

                var streamB = new RngStream(GoldenVectors.ShuffleMasterSeed);
                for (int i = 0; i < expectedDraws; i++)
                {
                    streamB.NextRaw();
                }

                uint expectedNextRaw = streamB.NextRaw();
                Assert.That(followUpRaw, Is.EqualTo(expectedNextRaw), $"length {input.Length}");
            }
        }

        // ------------------------------------------------------------------
        // fork -- (seed, parent="board-refill", label="probe") -> childSeed + first 16 draws.
        // ------------------------------------------------------------------

        [Test]
        public void Test_Fixture_Fork_ChildSeedAndFirst16Draws_MatchLiveImplementation()
        {
            var service = new RngService(new FakeClock());
            service.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);

            string child = service.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);
            RngStream childStream = service.DebugGetStream(child);

            Assert.That(childStream.InitialSeed, Is.EqualTo(GoldenVectors.ForkChildSeed));

            var draws = new uint[GoldenVectors.ForkFirst16Draws.Length];
            for (int i = 0; i < draws.Length; i++)
            {
                draws[i] = childStream.NextRaw();
            }

            Assert.That(draws, Is.EqualTo(GoldenVectors.ForkFirst16Draws));
        }

        // ------------------------------------------------------------------
        // F4/F5 integer-refinement reframing (AC "documents the boundary reframing").
        // ------------------------------------------------------------------

        /// <summary>
        /// ADR-004 §2's F4/F5 reframing: the multiply-shift range map can never produce an index
        /// equal to <c>range</c> -- the maximum index is structurally <c>range - 1</c>. This is
        /// NOT a divergence from the GDD's "clamp the boundary" language; it is the exact integer
        /// equivalent of <c>floor(next_float * range)</c>, restated so there is nothing left to
        /// clamp at runtime. See <see cref="Mix32_Tests"/>'s dedicated boundary-case tests for the
        /// full sweep across ranges; this test re-affirms the single sharpest case (max raw draw,
        /// max representable range) directly against the golden anchor's own board-refill values.
        /// </summary>
        [Test]
        public void Test_Fixture_F4F5BoundaryReframing_MaxRawNeverEqualsRange()
        {
            uint[] ranges = { 1u, 5u, 1u << 31, uint.MaxValue };
            foreach (uint range in ranges)
            {
                uint idx = RngStream.MultiplyShiftIndex(uint.MaxValue, range);
                Assert.That(idx, Is.EqualTo(range - 1));
                Assert.That(idx, Is.LessThan(range));
            }

            // And directly on the golden board-refill sequence itself: every NextInt(0,4) index
            // derived from next_raw_64 must land in [0,4], never 5.
            foreach (int value in GoldenVectors.NextInt0To4_64_F1Anchor)
            {
                Assert.That(value, Is.InRange(0, 4));
            }
        }
    }
}
