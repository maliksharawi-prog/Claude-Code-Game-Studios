using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SweetCascade.Domain.Rng;

namespace SweetCascade.Domain.Tests.Rng
{
    /// <summary>
    /// Edit-Mode unit tests for <see cref="RngService"/> — session lifecycle (F1/F2/F3 seed
    /// derivation, <see cref="IRngService.StartTestSession"/>), stream isolation, <c>ForkStream</c>,
    /// the bug-repro <see cref="RngSessionLog"/>, and the Honest Randomness Contract (no
    /// player-performance parameter anywhere on the surface).
    /// </summary>
    /// <remarks>
    /// Story: E02-001 (session lifecycle/F1–F3), E02-003 (isolation/fork/session log). Governing
    /// docs: <c>design/gdd/rng-service.md</c> §1–§4, §6; <c>docs/architecture/adr-004-deterministic-rng.md</c> §1, §3.
    /// Uses <see cref="FakeClock"/> (never the real wall clock — hard rule) and
    /// <see cref="GoldenVectors"/> (generated from the JSON fixtures — never hand-typed here).
    /// </remarks>
    [TestFixture]
    public class RngService_Tests
    {
        private static uint[] Take(uint[] source, int count)
        {
            var result = new uint[count];
            Array.Copy(source, result, count);
            return result;
        }

        // ------------------------------------------------------------------
        // AC-2: seed lifecycle F1/F2/F3.
        // ------------------------------------------------------------------

        [Test]
        public void Test_StartLevelSession_MasterSeed_MatchesF1Anchor()
        {
            var service = new RngService(new FakeClock());
            service.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);

            Assert.That(service.GetSessionLog().MasterSeed, Is.EqualTo(GoldenVectors.F1MasterSeedLevel1007Attempt3));
        }

        [Test]
        public void Test_StartDailySession_MasterSeed_MatchesF2Anchor()
        {
            var service = new RngService(new FakeClock());
            service.StartDailySession(GoldenVectors.F2DailyChallengeId, GoldenVectors.F2CalendarDateUtc);

            Assert.That(service.GetSessionLog().MasterSeed, Is.EqualTo(GoldenVectors.F2MasterSeedDaily42Day20650));
        }

        [Test]
        public void Test_StartTestSession_SetsMasterSeedDirectly_BypassingF1F2()
        {
            var service = new RngService(new FakeClock());
            service.StartTestSession(999u);

            RngSessionLog log = service.GetSessionLog();
            Assert.That(log.MasterSeed, Is.EqualTo(999u));
            Assert.That(log.PrimaryId, Is.EqualTo(0));
            Assert.That(log.InstanceId, Is.EqualTo(0));
        }

        [Test]
        public void Test_StartTestSession_TwoCallsWithSameSeed_YieldIdenticalStreamSubSeeds()
        {
            var serviceA = new RngService(new FakeClock());
            serviceA.StartTestSession(500u);

            var serviceB = new RngService(new FakeClock());
            serviceB.StartTestSession(500u);

            foreach ((string name, int _) in StreamRegistry.All)
            {
                Assert.That(serviceA.DebugGetStream(name).InitialSeed, Is.EqualTo(serviceB.DebugGetStream(name).InitialSeed), name);
            }
        }

        [Test]
        public void Test_StartTestSession_AllFourStreams_MatchGoldenF3SubSeeds_ForMasterSeed500()
        {
            var service = new RngService(new FakeClock());
            service.StartTestSession(500u);

            foreach ((int streamId, string streamName, uint expectedSeed) in GoldenVectors.F3StreamSeedsForMasterSeed500)
            {
                Assert.That(service.DebugGetStream(streamName).InitialSeed, Is.EqualTo(expectedSeed), $"{streamName} (id {streamId})");
            }
        }

        [Test]
        public void Test_StartLevelSession_AllFourStreams_MatchGoldenF3SubSeeds_ForF1AnchorMasterSeed()
        {
            var service = new RngService(new FakeClock());
            service.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);

            foreach ((int streamId, string streamName, uint expectedSeed) in GoldenVectors.F3StreamSeedsForMasterSeedF1Anchor)
            {
                Assert.That(service.DebugGetStream(streamName).InitialSeed, Is.EqualTo(expectedSeed), $"{streamName} (id {streamId})");
            }
        }

        // ------------------------------------------------------------------
        // AC-3: seed sensitivity (test_attempt_number_changes_seed / test_level_id_changes_seed).
        // ------------------------------------------------------------------

        [Test]
        public void Test_AttemptNumberChangesSeed()
        {
            var serviceAttempt1 = new RngService(new FakeClock());
            serviceAttempt1.StartLevelSession(1007, 1);

            var serviceAttempt2 = new RngService(new FakeClock());
            serviceAttempt2.StartLevelSession(1007, 2);

            Assert.That(serviceAttempt1.GetSessionLog().MasterSeed, Is.Not.EqualTo(serviceAttempt2.GetSessionLog().MasterSeed));
        }

        [Test]
        public void Test_LevelIdChangesSeed()
        {
            var serviceX = new RngService(new FakeClock());
            serviceX.StartLevelSession(1007, 1);

            var serviceY = new RngService(new FakeClock());
            serviceY.StartLevelSession(2008, 1);

            Assert.That(serviceX.GetSessionLog().MasterSeed, Is.Not.EqualTo(serviceY.GetSessionLog().MasterSeed));
        }

        // ------------------------------------------------------------------
        // AC-4: daily determinism & honesty.
        // ------------------------------------------------------------------

        [Test]
        public void Test_DailySeed_DeterministicAcrossCalls()
        {
            uint? previous = null;
            for (int i = 0; i < 5; i++)
            {
                var service = new RngService(new FakeClock());
                service.StartDailySession(42, 20650);
                uint seed = service.GetSessionLog().MasterSeed;

                if (previous.HasValue)
                {
                    Assert.That(seed, Is.EqualTo(previous.Value), $"call #{i}");
                }

                previous = seed;
            }

            Assert.That(previous, Is.EqualTo(GoldenVectors.F2MasterSeedDaily42Day20650));
        }

        [Test]
        public void Test_DailySeed_ExcludesPlayerData_InterfaceInspectionOfStartDailySession()
        {
            MethodInfo method = typeof(IRngService).GetMethod(nameof(IRngService.StartDailySession));
            Assert.That(method, Is.Not.Null);

            ParameterInfo[] parameters = method!.GetParameters();
            Assert.That(parameters.Length, Is.EqualTo(2),
                "StartDailySession must accept exactly (dailyChallengeId, calendarDateUtc) -- no player-identifying/performance parameter.");
            Assert.That(parameters[0].Name, Is.EqualTo("dailyChallengeId"));
            Assert.That(parameters[1].Name, Is.EqualTo("calendarDateUtc"));

            AssertNoForbiddenParameterNames(parameters);
        }

        // ------------------------------------------------------------------
        // AC-1 (Story 003): stream isolation.
        // ------------------------------------------------------------------

        [Test]
        public void Test_StreamIsolation_Interleaved100SpecialDropDraws_DoNotPerturbBoardRefillSequence()
        {
            const int boardRefillDrawCount = 20;
            const int specialDropDrawsPerStep = 5; // 20 * 5 = 100 total special-drop draws (AC-1's literal "100").

            var control = new RngService(new FakeClock());
            control.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            RngStream controlBoardRefill = control.DebugGetStream(StreamRegistry.BoardRefillName);
            var controlSequence = new uint[boardRefillDrawCount];
            for (int i = 0; i < boardRefillDrawCount; i++)
            {
                controlSequence[i] = controlBoardRefill.NextRaw();
            }

            var interleaved = new RngService(new FakeClock());
            interleaved.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            RngStream boardRefill = interleaved.DebugGetStream(StreamRegistry.BoardRefillName);
            RngStream specialDrop = interleaved.DebugGetStream(StreamRegistry.SpecialDropName);
            var interleavedSequence = new uint[boardRefillDrawCount];
            var specialDropDrawsObserved = new List<uint>();
            for (int i = 0; i < boardRefillDrawCount; i++)
            {
                for (int j = 0; j < specialDropDrawsPerStep; j++)
                {
                    specialDropDrawsObserved.Add(specialDrop.NextRaw());
                }

                interleavedSequence[i] = boardRefill.NextRaw();
            }

            Assert.That(interleavedSequence, Is.EqualTo(controlSequence),
                "interleaving 100 special-drop draws must not change the board-refill sequence.");
            Assert.That(controlSequence, Is.EqualTo(Take(GoldenVectors.NextRaw64_F1Anchor, boardRefillDrawCount)),
                "the (uninterleaved) control board-refill sequence must itself match the independent oracle.");
            Assert.That(specialDropDrawsObserved.GetRange(0, GoldenVectors.SpecialDropRaw30_F1Anchor.Length),
                Is.EqualTo(GoldenVectors.SpecialDropRaw30_F1Anchor),
                "the special-drop draws consumed during interleaving must themselves match the independent oracle.");
        }

        // ------------------------------------------------------------------
        // AC-2 (Story 003): fork isolation & determinism.
        // ------------------------------------------------------------------

        [Test]
        public void Test_ForkStream_MatchesGoldenChildSeedAndFirst16Draws()
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

        [Test]
        public void Test_ForkStream_Deterministic_AcrossTwoFreshIdenticallySeededSessions()
        {
            var serviceA = new RngService(new FakeClock());
            serviceA.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            string childA = serviceA.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);

            var serviceB = new RngService(new FakeClock());
            serviceB.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            string childB = serviceB.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);

            for (int i = 0; i < 10; i++)
            {
                Assert.That(serviceA.NextFloat(childA), Is.EqualTo(serviceB.NextFloat(childB)), $"draw #{i}");
            }
        }

        [Test]
        public void Test_ForkStream_IsolatedFromParent_ChildDrawsDoNotPerturbParentSequence()
        {
            var control = new RngService(new FakeClock());
            control.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            RngStream controlParent = control.DebugGetStream(GoldenVectors.ForkParentStreamName);
            var controlParentSequence = new uint[10];
            for (int i = 0; i < 10; i++)
            {
                controlParentSequence[i] = controlParent.NextRaw();
            }

            var forked = new RngService(new FakeClock());
            forked.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            string child = forked.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);
            RngStream childStream = forked.DebugGetStream(child);
            for (int i = 0; i < 50; i++)
            {
                childStream.NextRaw(); // draw plenty from the child
            }

            RngStream forkedParent = forked.DebugGetStream(GoldenVectors.ForkParentStreamName);
            var forkedParentSequence = new uint[10];
            for (int i = 0; i < 10; i++)
            {
                forkedParentSequence[i] = forkedParent.NextRaw();
            }

            Assert.That(forkedParentSequence, Is.EqualTo(controlParentSequence));
        }

        [Test]
        public void Test_ForkStream_ChildSequence_UnaffectedByForkTiming_BeforeOrAfterParentDraws()
        {
            var forkBeforeDraws = new RngService(new FakeClock());
            forkBeforeDraws.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            string childBefore = forkBeforeDraws.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);
            RngStream parentAfterFork = forkBeforeDraws.DebugGetStream(GoldenVectors.ForkParentStreamName);
            for (int i = 0; i < 30; i++)
            {
                parentAfterFork.NextRaw(); // draws happen AFTER forking
            }

            var forkAfterDraws = new RngService(new FakeClock());
            forkAfterDraws.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);
            RngStream parentBeforeFork = forkAfterDraws.DebugGetStream(GoldenVectors.ForkParentStreamName);
            for (int i = 0; i < 30; i++)
            {
                parentBeforeFork.NextRaw(); // draws happen BEFORE forking
            }

            string childAfter = forkAfterDraws.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);

            RngStream childBeforeStream = forkBeforeDraws.DebugGetStream(childBefore);
            RngStream childAfterStream = forkAfterDraws.DebugGetStream(childAfter);
            for (int i = 0; i < 10; i++)
            {
                Assert.That(childBeforeStream.NextRaw(), Is.EqualTo(childAfterStream.NextRaw()), $"draw #{i}");
            }
        }

        // ------------------------------------------------------------------
        // AC-3 (Story 003): fork idempotency.
        // ------------------------------------------------------------------

        [Test]
        public void Test_ForkStream_Idempotent_SecondCallReturnsSameContinuingChild_NotAReset()
        {
            var service = new RngService(new FakeClock());
            service.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);

            string child1 = service.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);
            RngStream childStream = service.DebugGetStream(child1);
            uint firstDraw = childStream.NextRaw();
            uint secondDraw = childStream.NextRaw();

            string child2 = service.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkLabel);
            Assert.That(child2, Is.EqualTo(child1), "a second ForkStream with identical args must return the SAME child stream name.");

            RngStream childStreamAgain = service.DebugGetStream(child2);
            uint thirdDraw = childStreamAgain.NextRaw();

            Assert.That(new[] { firstDraw, secondDraw }, Is.EqualTo(new[] { GoldenVectors.ForkFirst16Draws[0], GoldenVectors.ForkFirst16Draws[1] }));
            Assert.That(thirdDraw, Is.EqualTo(GoldenVectors.ForkFirst16Draws[2]),
                "continuing the SAME child stream -- the 3rd draw must be the golden sequence's 3rd value, not a reset back to the 1st.");
        }

        // ------------------------------------------------------------------
        // fork_extra: label / parent distinctness (rng_golden_v1_draws.json).
        // ------------------------------------------------------------------

        [Test]
        public void Test_ForkStream_DifferentLabel_ProducesDistinctChild_MatchesGolden()
        {
            var service = new RngService(new FakeClock());
            service.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);

            string child = service.ForkStream(GoldenVectors.ForkParentStreamName, GoldenVectors.ForkExtraLabelProbe2);
            RngStream childStream = service.DebugGetStream(child);

            Assert.That(childStream.InitialSeed, Is.EqualTo(GoldenVectors.ForkExtraBoardRefillProbe2ChildSeed));
            Assert.That(childStream.InitialSeed, Is.Not.EqualTo(GoldenVectors.ForkChildSeed),
                "a different label must derive a different child seed than \"probe\".");

            var draws = new uint[GoldenVectors.ForkExtraBoardRefillProbe2Draws.Length];
            for (int i = 0; i < draws.Length; i++)
            {
                draws[i] = childStream.NextRaw();
            }

            Assert.That(draws, Is.EqualTo(GoldenVectors.ForkExtraBoardRefillProbe2Draws));
        }

        [Test]
        public void Test_ForkStream_SameLabelDifferentParent_ProducesDistinctChild_MatchesGolden()
        {
            var service = new RngService(new FakeClock());
            service.StartLevelSession(GoldenVectors.F1LevelId, GoldenVectors.F1AttemptNumber);

            string child = service.ForkStream(GoldenVectors.ForkExtraSpecialDropStreamName, GoldenVectors.ForkLabel);
            RngStream childStream = service.DebugGetStream(child);

            Assert.That(childStream.InitialSeed, Is.EqualTo(GoldenVectors.ForkExtraSpecialDropProbeChildSeed));
            Assert.That(childStream.InitialSeed, Is.Not.EqualTo(GoldenVectors.ForkChildSeed),
                "the same label off a DIFFERENT parent must derive a different child seed.");

            var draws = new uint[GoldenVectors.ForkExtraSpecialDropProbeDraws.Length];
            for (int i = 0; i < draws.Length; i++)
            {
                draws[i] = childStream.NextRaw();
            }

            Assert.That(draws, Is.EqualTo(GoldenVectors.ForkExtraSpecialDropProbeDraws));
        }

        // ------------------------------------------------------------------
        // AC-4 (Story 003): session log.
        // ------------------------------------------------------------------

        [Test]
        public void Test_SessionLog_ContainsReproFields_AfterStartLevelSession()
        {
            var clock = new FakeClock("2026-07-18T12:34:56Z");
            var service = new RngService(clock);
            service.StartLevelSession(1007, 3);

            RngSessionLog log = service.GetSessionLog();

            Assert.That(log.PrimaryId, Is.EqualTo(1007));
            Assert.That(log.InstanceId, Is.EqualTo(3));
            Assert.That(log.MasterSeed, Is.EqualTo(GoldenVectors.F1MasterSeedLevel1007Attempt3));
            Assert.That(log.AlgorithmVersion, Is.EqualTo("v1"));
            Assert.That(log.SessionStartTimestampIso, Is.EqualTo("2026-07-18T12:34:56Z"));
        }

        [Test]
        public void Test_SessionLog_Replay_ReproducesSameMasterSeed()
        {
            var first = new RngService(new FakeClock());
            first.StartLevelSession(1007, 3);
            RngSessionLog log = first.GetSessionLog();

            var replay = new RngService(new FakeClock());
            replay.StartLevelSession((int)log.PrimaryId, (int)log.InstanceId);

            Assert.That(replay.GetSessionLog().MasterSeed, Is.EqualTo(log.MasterSeed));
        }

        // ------------------------------------------------------------------
        // Honest Randomness Contract: no hidden bias parameter on any draw/fork/session function.
        // ------------------------------------------------------------------

        private static readonly string[] ForbiddenParameterNameSubstrings =
        {
            "player", "skill", "streak", "spend", "perform", "luck", "pity", "purchase", "iap", "rank", "history",
        };

        private static void AssertNoForbiddenParameterNames(ParameterInfo[] parameters)
        {
            foreach (ParameterInfo p in parameters)
            {
                string lowerName = p.Name!.ToLowerInvariant();
                foreach (string forbidden in ForbiddenParameterNameSubstrings)
                {
                    Assert.That(lowerName, Does.Not.Contain(forbidden),
                        $"parameter '{p.Name}' on {p.Member.Name} looks like a player-performance/bias input -- forbidden per the Honest Randomness Contract.");
                }
            }
        }

        [Test]
        public void Test_NoHiddenBiasParameter_OnAnyIRngServiceMethod()
        {
            foreach (MethodInfo method in typeof(IRngService).GetMethods())
            {
                AssertNoForbiddenParameterNames(method.GetParameters());
            }
        }
    }
}
