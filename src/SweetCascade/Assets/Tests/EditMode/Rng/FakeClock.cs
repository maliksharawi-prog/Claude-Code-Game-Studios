using SweetCascade.Domain.Rng;

namespace SweetCascade.Domain.Tests.Rng
{
    /// <summary>
    /// Deterministic <see cref="IClock"/> test double — returns a fixed ISO 8601 string, never
    /// reads the real wall clock. Used to construct <c>RngService</c> instances in Edit-Mode
    /// tests without pulling a live timestamp into an assertion (ADR-004 §4/§6: Domain never
    /// reads the clock itself; this test double lives outside Domain, in the test assembly).
    /// </summary>
    internal sealed class FakeClock : IClock
    {
        private readonly string _fixedIso;

        internal FakeClock(string fixedIso = "2026-07-18T00:00:00Z")
        {
            _fixedIso = fixedIso;
        }

        public string UtcNowIso() => _fixedIso;
    }
}
