using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SweetCascade.Domain;

namespace SweetCascade.Domain.Tests.Conventions
{
    /// <summary>
    /// Copy-me template for every future Edit-Mode test in this project. It encodes, as working
    /// code, the rules a reviewer checks any new test file against — see
    /// <see cref="Test_DomainAssembly_IsEngineFree"/> below for the annotated example.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E01-005 (edit-play-test-skeletons). Governing docs: <c>tests/README.md</c> ("Test
    /// Naming Conventions (C#)"); <c>.claude/rules/test-standards.md</c>;
    /// <c>.claude/docs/coding-standards.md</c> Testing Standards;
    /// <c>docs/architecture/control-manifest.md</c> "Test Evidence by Story Type".
    /// </para>
    /// <para>
    /// <b>Naming</b>: files are named <c>[System][Feature]Tests.cs</c>; methods are named
    /// <c>Test_[Scenario]_[Expected]</c>.
    /// </para>
    /// <para>
    /// <b>Structure</b>: every test body has explicit <c>// Arrange</c> / <c>// Act</c> /
    /// <c>// Assert</c> comment blocks, in that order, even when a step is trivial.
    /// </para>
    /// <para>
    /// <b>Determinism, isolation, independence</b>: no <c>System.Random</c>/seeded RNG, no
    /// wall-clock/<c>DateTime</c> reads, no filesystem or network IO, no shared mutable state
    /// between tests, no dependency on execution order. This file's single assertion reflects
    /// over already-compiled assembly metadata only — no random seed, no clock, no IO — so it is
    /// safe to run in any order, any number of times, with an identical result every time.
    /// </para>
    /// <para>
    /// <b>Assembly boundary</b>: this file lives in <c>SweetCascade.Domain.Tests</c> (Edit Mode),
    /// which references <c>SweetCascade.Domain</c> only — no <c>UnityEngine</c>/
    /// <c>UnityEditor</c> symbol may appear here, mirroring the Domain assembly's own
    /// <c>noEngineReferences: true</c> rule (ADR-004 L1). Play Mode tests, which may use engine
    /// types, live in the separate <c>SweetCascade.Game.Tests</c> assembly — see
    /// <c>Assets/Tests/PlayMode/SmokeExampleTests.cs</c>.
    /// </para>
    /// <para>
    /// Intentionally does NOT author a real Domain-logic test (RNG golden vectors, board,
    /// scoring, objectives, save) — those suites are owned by E02+ and are already substantially
    /// built (see <c>Assets/Tests/EditMode/Rng/</c>, <c>Assets/Tests/EditMode/Levels/</c>). This
    /// file's job is narrower: prove the harness and conventions, not domain behaviour.
    /// </para>
    /// </remarks>
    [TestFixture]
    public class ConventionsExampleTests
    {
        /// <summary>
        /// A real, still-trivial example: reflects over the compiled <c>SweetCascade.Domain</c>
        /// assembly's own referenced-assembly list and asserts neither forbidden engine namespace
        /// is present — the same rule the asmdef's <c>noEngineReferences: true</c> flag (L1) and
        /// <c>tools/ci/domain-purity-scan.sh</c>'s ripgrep denylist (L2) already enforce, from a
        /// third, independent angle. See <c>docs/architecture/control-manifest.md</c> "Domain
        /// purity is enforced at three independent layers".
        /// </summary>
        [Test]
        public void Test_DomainAssembly_IsEngineFree()
        {
            // Arrange
            Assembly domainAssembly = typeof(DomainAssemblyMarker).Assembly;
            string[] forbiddenAssemblyNamePrefixes = { "UnityEngine", "UnityEditor" };

            // Act
            string[] referencedAssemblyNames = domainAssembly
                .GetReferencedAssemblies()
                .Select(referenced => referenced.Name)
                .ToArray();

            // Assert
            foreach (string prefix in forbiddenAssemblyNamePrefixes)
            {
                Assert.That(referencedAssemblyNames, Has.None.Match($"^{prefix}"),
                    $"SweetCascade.Domain must never reference an assembly starting with " +
                    $"'{prefix}' (ADR-004 L1 / control-manifest.md Domain Layer Rules).");
            }
        }
    }
}
