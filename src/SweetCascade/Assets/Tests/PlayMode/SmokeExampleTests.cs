using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace SweetCascade.Game.Tests.Smoke
{
    /// <summary>
    /// Minimal Play-Mode smoke test proving the Play-Mode test harness executes end-to-end
    /// under <c>game-ci/unity-test-runner@v4</c> (<c>testMode: all</c>) — the counterpart to
    /// <c>Assets/Tests/EditMode/Conventions/ConventionsExampleTests.cs</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E01-005 (edit-play-test-skeletons). Governing docs: <c>tests/README.md</c>
    /// ("PlayMode/ — Integration tests"); <c>docs/architecture/control-manifest.md</c> Editor
    /// &amp; CI Layer Rules; ADR-004 §5 (Edit Mode headless / Play Mode integration split).
    /// </para>
    /// <para>
    /// No gameplay scene exists yet at this story's scope (<c>Assets/Scenes/</c> is still an
    /// empty placeholder folder — Story 001), so this test does not load one by name. Instead it
    /// proves the harness itself: the runner enters Play Mode, a single frame executes, and a
    /// trivially-true condition holds — exactly the AC-2 "loads the empty scene" case for a
    /// project with no scene yet. Once a real bootstrap scene lands, later integration stories
    /// (E03+) should load it explicitly rather than relying on the implicit empty scene every
    /// Play Mode test starts in.
    /// </para>
    /// <para>
    /// Deterministic and isolated by construction: no <c>System.Random</c>, no wall-clock read,
    /// no external asset load, and exactly one <c>yield return null</c> — no frame-timing
    /// dependency beyond that single frame boundary (per this story's QA Test Cases edge case).
    /// </para>
    /// </remarks>
    [TestFixture]
    public class SmokeExampleTests
    {
        [UnityTest]
        public IEnumerator Test_PlayModeHarness_EntersPlayAndRunsOneFrame_AssertsTriviallyTrue()
        {
            // Arrange
            bool wasPlayingBeforeYield = Application.isPlaying;

            // Act
            yield return null; // advance exactly one frame — proves the runner actually ticks.

            // Assert
            Assert.That(wasPlayingBeforeYield, Is.True, "Play Mode tests must execute inside Play Mode.");
            Assert.That(Application.isPlaying, Is.True);
        }
    }
}
