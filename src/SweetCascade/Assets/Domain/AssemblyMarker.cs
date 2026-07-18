namespace SweetCascade.Domain
{
    /// <summary>
    /// Placeholder marker type for the <c>SweetCascade.Domain</c> assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E01-002 (project-scaffold-ci). Governing docs:
    /// <c>docs/architecture/architecture.md</c> §5.1/§5.3 (assembly boundary)
    /// and <c>docs/architecture/adr-004-deterministic-rng.md</c> §6 Layer 1
    /// (<c>noEngineReferences: true</c>).
    /// </para>
    /// <para>
    /// This assembly is Sweet Cascade's pure-C# logic core. It must never
    /// reference the two forbidden engine namespaces (see ADR-004 §6) —
    /// enforced at three independent layers (asmdef compile failure, the CI
    /// denylist scan in
    /// <c>tools/ci/domain-purity-scan.sh</c>, and, for the RNG specifically, a
    /// golden-vector regression suite). This file exists only so the assembly
    /// compiles as a non-empty unit ahead of the real Domain slices (Board,
    /// Specials, Scoring, Objectives, Rng, Levels, Save, Events) delivered
    /// starting in E02. Replace or delete it once real Domain code lands.
    /// </para>
    /// </remarks>
    internal static class DomainAssemblyMarker
    {
    }
}
