namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// The bug-repro session record (rng-service.md §3). Sufficient on its own to reproduce a
    /// session's entire draw sequence: replaying <c>(PrimaryId, InstanceId)</c> through a fresh
    /// <see cref="IRngService.StartLevelSession"/>/<see cref="IRngService.StartDailySession"/>
    /// call reproduces <see cref="MasterSeed"/> exactly (<c>test_session_log_replay</c>).
    /// </summary>
    /// <param name="PrimaryId">
    /// <c>level_id</c> for a level session, or <c>daily_challenge_id</c> for a daily session.
    /// <c>0</c> for a <see cref="IRngService.StartTestSession"/> session (no level/daily
    /// identity applies — test injection bypasses F1/F2 entirely).
    /// </param>
    /// <param name="InstanceId">
    /// <c>attempt_number</c> for a level session, or <c>calendar_date_utc</c> for a daily
    /// session. <c>0</c> for a test session (see <see cref="PrimaryId"/>).
    /// </param>
    /// <param name="MasterSeed">The session's F1/F2-derived (or directly-injected, for a test session) root seed.</param>
    /// <param name="AlgorithmVersion">Which mix32-finalizer version produced this seed (rng-service.md Tuning Knobs) — <c>"v1"</c> at MVP.</param>
    /// <param name="SessionStartTimestampIso">ISO 8601 UTC, supplied by the injected <see cref="IClock"/> — never read directly in Domain.</param>
    /// <remarks>Story: E02-003. Governing doc: <c>design/gdd/rng-service.md</c> §3 (session log table).</remarks>
    public readonly record struct RngSessionLog(
        long PrimaryId,
        long InstanceId,
        uint MasterSeed,
        string AlgorithmVersion,
        string SessionStartTimestampIso);
}
