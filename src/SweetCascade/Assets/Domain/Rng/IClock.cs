namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// Purity-preserving clock seam (ADR-004 §4/§6). Domain never reads the wall clock itself —
    /// reading the system's UTC-now value directly is forbidden anywhere in Domain, CI-guarded.
    /// The concrete <c>SystemClock</c> implementation (which does read the wall clock) lives in
    /// <c>SweetCascade.Game</c> and is constructor-injected into <see cref="RngService"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Story: E02-003 (rng-stream-isolation-fork-session-log). Governing docs:
    /// <c>design/gdd/rng-service.md</c> §3 (bug-repro session log);
    /// <c>docs/architecture/adr-004-deterministic-rng.md</c> §4/§6.
    /// </para>
    /// <para>
    /// Used only to timestamp <see cref="RngSessionLog"/> — never fed into a draw or a seed
    /// (the seed derivation formulas F1–F3 take no clock input at all).
    /// </para>
    /// </remarks>
    public interface IClock
    {
        /// <summary>Current UTC time as an ISO 8601 string. Never called from a draw/seed path — timestamp-only.</summary>
        string UtcNowIso();
    }
}
