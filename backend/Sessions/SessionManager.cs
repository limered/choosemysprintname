using System.Collections.Concurrent;
using Backend.Pokemon;

namespace Backend.Sessions;

public enum SessionPhase
{
    Lobby,
    Voting,
    TieBreaker,
    Finished,
}

public enum CastVoteResult
{
    Success,
    SessionNotFound,
    CandidateNotFound,
    AlreadyVoted,
    NotInVotingPhase,
}

public enum StartTimerResult
{
    Success,
    SessionNotFound,
    NotInLobby,
    InvalidDuration,
}

public enum ExtendTimerResult
{
    Success,
    SessionNotFound,
    NotRunning,
}

public sealed class Session
{
    public required Guid Id { get; init; }
    public required char Letter { get; init; }
    public required IReadOnlyList<PokemonCandidate> Candidates { get; init; }

    private readonly TimeProvider _timeProvider;

    private readonly HashSet<string> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _participantsLock = new();

    // nickname -> candidate name. Nickname comparison is case-insensitive.
    private readonly Dictionary<string, string> _votesByNickname = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _votesLock = new();

    private readonly object _phaseLock = new();
    private SessionPhase _phase = SessionPhase.Lobby;
    private DateTimeOffset? _endsAtUtc;
    private string? _winner;

    public Session(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    public IReadOnlyCollection<string> Participants
    {
        get
        {
            lock (_participantsLock) return _participants.ToArray();
        }
    }

    public SessionPhase Phase
    {
        get
        {
            ResolveIfExpired();
            lock (_phaseLock) return _phase;
        }
    }

    public DateTimeOffset? EndsAtUtc
    {
        get
        {
            ResolveIfExpired();
            lock (_phaseLock) return _endsAtUtc;
        }
    }

    public string? Winner
    {
        get
        {
            ResolveIfExpired();
            lock (_phaseLock) return _winner;
        }
    }

    /// <summary>
    /// Whole seconds remaining on the timer (rounded up). Null when the
    /// session is not in <see cref="SessionPhase.Voting"/>.
    /// </summary>
    public int? SecondsRemaining
    {
        get
        {
            ResolveIfExpired();
            lock (_phaseLock)
            {
                if (_phase != SessionPhase.Voting || _endsAtUtc is null) return null;
                var remaining = (_endsAtUtc.Value - _timeProvider.GetUtcNow()).TotalSeconds;
                if (remaining <= 0) return 0;
                return (int)Math.Ceiling(remaining);
            }
        }
    }

    /// <summary>
    /// Returns a snapshot of per-candidate vote counts. Every candidate is
    /// included, even those with zero votes. Keyed by candidate name
    /// (case-sensitive, matching <see cref="Candidates"/>).
    /// </summary>
    public IReadOnlyDictionary<string, int> Tally
    {
        get
        {
            ResolveIfExpired();
            return ComputeTally();
        }
    }

    private Dictionary<string, int> ComputeTally()
    {
        var counts = Candidates.ToDictionary(c => c.Name, _ => 0, StringComparer.Ordinal);
        lock (_votesLock)
        {
            foreach (var candidateName in _votesByNickname.Values)
            {
                if (counts.ContainsKey(candidateName)) counts[candidateName]++;
            }
        }
        return counts;
    }

    internal void AddParticipant(string nickname)
    {
        lock (_participantsLock) _participants.Add(nickname);
    }

    internal StartTimerResult StartTimer(int durationSeconds)
    {
        if (durationSeconds <= 0) return StartTimerResult.InvalidDuration;
        lock (_phaseLock)
        {
            ResolveIfExpiredLocked();
            if (_phase != SessionPhase.Lobby) return StartTimerResult.NotInLobby;
            _phase = SessionPhase.Voting;
            _endsAtUtc = _timeProvider.GetUtcNow().AddSeconds(durationSeconds);
        }
        return StartTimerResult.Success;
    }

    internal ExtendTimerResult ExtendTimer()
    {
        lock (_phaseLock)
        {
            ResolveIfExpiredLocked();
            if (_phase != SessionPhase.Voting || _endsAtUtc is null)
                return ExtendTimerResult.NotRunning;
            _endsAtUtc = _endsAtUtc.Value.AddSeconds(60);
        }
        return ExtendTimerResult.Success;
    }

    internal CastVoteResult CastVote(string nickname, string candidateName)
    {
        ResolveIfExpired();
        lock (_phaseLock)
        {
            if (_phase != SessionPhase.Voting) return CastVoteResult.NotInVotingPhase;
        }

        if (!Candidates.Any(c => string.Equals(c.Name, candidateName, StringComparison.Ordinal)))
            return CastVoteResult.CandidateNotFound;

        lock (_votesLock)
        {
            if (_votesByNickname.ContainsKey(nickname))
                return CastVoteResult.AlreadyVoted;
            _votesByNickname[nickname] = candidateName;
        }
        return CastVoteResult.Success;
    }

    private void ResolveIfExpired()
    {
        lock (_phaseLock) ResolveIfExpiredLocked();
    }

    private void ResolveIfExpiredLocked()
    {
        if (_phase != SessionPhase.Voting) return;
        if (_endsAtUtc is null) return;
        if (_timeProvider.GetUtcNow() < _endsAtUtc.Value) return;

        var tally = ComputeTally();
        var max = tally.Values.Count == 0 ? 0 : tally.Values.Max();
        if (max == 0)
        {
            _phase = SessionPhase.TieBreaker;
            _winner = null;
            return;
        }

        var topCandidates = tally.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList();
        if (topCandidates.Count == 1)
        {
            _phase = SessionPhase.Finished;
            _winner = topCandidates[0];
        }
        else
        {
            _phase = SessionPhase.TieBreaker;
            _winner = null;
        }
    }
}

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly IPokemonCatalog _catalog;
    private readonly TimeProvider _timeProvider;

    public SessionManager(IPokemonCatalog catalog, TimeProvider timeProvider)
    {
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public async Task<Session> CreateAsync(char letter, CancellationToken ct = default)
    {
        var candidates = await _catalog.GetPokemonByLetterAsync(
            letter, Array.Empty<string>(), ct);

        var session = new Session(_timeProvider)
        {
            Id = Guid.NewGuid(),
            Letter = char.ToUpperInvariant(letter),
            Candidates = candidates
        };
        _sessions[session.Id] = session;
        return session;
    }

    public Session? Get(Guid id) => _sessions.TryGetValue(id, out var s) ? s : null;

    public bool AddParticipant(Guid sessionId, string nickname)
    {
        if (!_sessions.TryGetValue(sessionId, out var session)) return false;
        session.AddParticipant(nickname);
        return true;
    }

    public CastVoteResult CastVote(Guid sessionId, string nickname, string candidateName)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return CastVoteResult.SessionNotFound;
        return session.CastVote(nickname, candidateName);
    }

    public StartTimerResult StartTimer(Guid sessionId, int durationSeconds)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return StartTimerResult.SessionNotFound;
        return session.StartTimer(durationSeconds);
    }

    public ExtendTimerResult ExtendTimer(Guid sessionId)
    {
        if (!_sessions.TryGetValue(sessionId, out var session))
            return ExtendTimerResult.SessionNotFound;
        return session.ExtendTimer();
    }
}
