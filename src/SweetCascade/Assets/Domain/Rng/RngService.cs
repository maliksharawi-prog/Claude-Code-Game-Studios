using System;
using System.Collections.Generic;
using System.Text;

namespace SweetCascade.Domain.Rng
{
    /// <summary>
    /// Reference implementation of <see cref="IRngService"/> (ADR-004). Pure C#, zero engine
    /// surface, single-threaded, constructor-injected with the only external seam it needs
    /// (<see cref="IClock"/>, timestamp-only, never a draw input).
    /// </summary>
    /// <remarks>
    /// Story: E02-001/002/003. Governing docs: <c>design/gdd/rng-service.md</c>;
    /// <c>docs/architecture/adr-004-deterministic-rng.md</c>.
    /// </remarks>
    public sealed class RngService : IRngService
    {
        private readonly IClock _clock;
        private readonly Dictionary<string, RngStream> _streams = new Dictionary<string, RngStream>();

        private uint _masterSeed;
        private long _primaryId;
        private long _instanceId;
        private string _sessionStartTimestampIso = string.Empty;

        /// <summary>Constructs the service with its injected clock seam (the Game layer supplies the concrete <c>SystemClock</c>).</summary>
        public RngService(IClock clock)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        /// <inheritdoc/>
        public void StartLevelSession(int levelId, int attemptNumber)
        {
            uint masterSeed = Mix32.Avalanche(Mix32.Combine(unchecked((uint)levelId), unchecked((uint)attemptNumber)));
            BeginSession(masterSeed, levelId, attemptNumber);
        }

        /// <inheritdoc/>
        public void StartDailySession(int dailyChallengeId, int calendarDateUtc)
        {
            uint masterSeed = Mix32.Avalanche(Mix32.Combine(unchecked((uint)dailyChallengeId), unchecked((uint)calendarDateUtc)));
            BeginSession(masterSeed, dailyChallengeId, calendarDateUtc);
        }

        /// <inheritdoc/>
        public void StartTestSession(uint masterSeed)
        {
            // Test injection bypasses F1/F2 entirely (rng-service.md §3 item 3). No level/daily
            // identity applies, so the session log's PrimaryId/InstanceId are 0 — there is no
            // "attempt" or "calendar day" a test session replays against.
            BeginSession(masterSeed, primaryId: 0, instanceId: 0);
        }

        private void BeginSession(uint masterSeed, long primaryId, long instanceId)
        {
            _masterSeed = masterSeed;
            _primaryId = primaryId;
            _instanceId = instanceId;
            _sessionStartTimestampIso = _clock.UtcNowIso();

            // A fresh session fully re-derives every stream's state (rng-service.md §1) --
            // no state carries over from a previous session.
            _streams.Clear();
            foreach ((string name, int id) in StreamRegistry.All)
            {
                uint streamSeed = Mix32.Avalanche(Mix32.Combine(masterSeed, unchecked((uint)id)));
                _streams[name] = new RngStream(streamSeed);
            }
        }

        /// <inheritdoc/>
        public double NextFloat(string streamName) => ResolveStream(streamName).NextFloat();

        /// <inheritdoc/>
        public int NextInt(string streamName, int minValue, int maxValue) =>
            ResolveStream(streamName).NextInt(minValue, maxValue);

        /// <inheritdoc/>
        public T NextColor<T>(string streamName, IReadOnlyList<T> activeColors) =>
            ResolveStream(streamName).NextColor(activeColors);

        /// <inheritdoc/>
        public T[] Shuffle<T>(string streamName, IReadOnlyList<T> source) =>
            ResolveStream(streamName).Shuffle(source);

        /// <inheritdoc/>
        public string ForkStream(string parentStreamName, string label)
        {
            if (label is null) throw new ArgumentNullException(nameof(label));
            RngStream parent = ResolveStream(parentStreamName);

            string child = parentStreamName + "/" + label;
            if (_streams.ContainsKey(child))
            {
                return child; // Idempotent (rng-service.md Edge Cases): continue the existing child, never reset it.
            }

            // FNV-1a-32 over the label's UTF-8 bytes -- deterministic and portable. NEVER
            // string.GetHashCode() (randomized per-process since .NET Core; forbidden in Domain).
            uint h = RngConstants.FnvOffsetBasis;
            foreach (byte b in Encoding.UTF8.GetBytes(label))
            {
                unchecked
                {
                    h ^= b;
                    h *= RngConstants.FnvPrime;
                }
            }

            // Derived from the parent's INITIAL seed, not its current state, so a fork is
            // independent of how many draws the parent has already consumed (ADR-004 §3).
            uint childSeed = Mix32.Avalanche(Mix32.Combine(parent.InitialSeed, h));
            _streams[child] = new RngStream(childSeed);
            return child;
        }

        /// <inheritdoc/>
        public RngSessionLog GetSessionLog() =>
            new RngSessionLog(_primaryId, _instanceId, _masterSeed, RngConstants.AlgorithmVersion, _sessionStartTimestampIso);

        private RngStream ResolveStream(string streamName)
        {
            if (streamName is null) throw new ArgumentNullException(nameof(streamName));
            if (!_streams.TryGetValue(streamName, out RngStream stream))
            {
                throw new ArgumentException(
                    $"Unknown RNG stream '{streamName}'. Start a session first, and use a registered stream name or one returned by ForkStream.",
                    nameof(streamName));
            }

            return stream;
        }

        /// <summary>
        /// Test-only seam: exposes the live <see cref="RngStream"/> resolved for
        /// <paramref name="streamName"/>, so Edit-Mode tests can assert the F3-derived sub-seed
        /// and raw-draw sequence directly (e.g. golden-vector verification) without adding a
        /// player-facing accessor to <see cref="IRngService"/>. <c>internal</c> — reachable only
        /// from <c>SweetCascade.Domain.Tests</c> via <c>InternalsVisibleTo</c>
        /// (<c>Assets/Domain/AssemblyInfo.cs</c>); never part of the public contract.
        /// </summary>
        internal RngStream DebugGetStream(string streamName) => ResolveStream(streamName);
    }
}
