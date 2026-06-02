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
    /// <summary>
    /// The session is not in a state that accepts a new timer. Returned when the
    /// session is <see cref="SessionPhase.Finished"/>, or when a timer is already
    /// running (in <see cref="SessionPhase.Voting"/> or a <see cref="SessionPhase.TieBreaker"/>
    /// round whose timer has not yet expired).
    /// </summary>
    TimerAlreadyRunning,
    InvalidDuration,
}

public enum ExtendTimerResult
{
    Success,
    SessionNotFound,
    NotRunning,
}

/// <summary>
/// State for a single voting round - either the initial voting round or any
/// subsequent tie-breaker round. Each round has its own in-play candidate
/// subset and its own fresh vote tally (one vote per nickname per round).
/// </summary>
internal sealed class Round
{
    public int Id { get; }
    public IReadOnlyList<string> CandidatesInPlay { get; }
    public DateTimeOffset? EndsAtUtc;
    public readonly Dictionary<string, string> VotesByNickname = new(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> _inPlaySet;

    public Round(int id, IEnumerable<string> candidatesInPlay)
    {
        Id = id;
        var list = candidatesInPlay.ToArray();
        CandidatesInPlay = list;
        _inPlaySet = new HashSet<string>(list, StringComparer.Ordinal);
    }

    public bool IsCandidateInPlay(string name) => _inPlaySet.Contains(name);

    public Dictionary<string, int> Tally()
    {
        var counts = CandidatesInPlay.ToDictionary(n => n, _ => 0, StringComparer.Ordinal);
        foreach (var candidateName in VotesByNickname.Values)
        {
            if (counts.ContainsKey(candidateName)) counts[candidateName]++;
        }
        return counts;
    }
}

public sealed class Session
{
    public required Guid Id { get; init; }
    public required char Letter { get; init; }
    public required IReadOnlyList<PokemonCandidate> Candidates { get; init; }

    private readonly TimeProvider _timeProvider;

    private readonly HashSet<string> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _participantsLock = new();

    private readonly object _stateLock = new();
    private SessionPhase _phase = SessionPhase.Lobby;
    private string? _winner;
    private Round _currentRound = null!; // initialized in ctor

    public Session(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Initializer hook used by <see cref="SessionManager"/> right after construction so
    /// the first round can be created using the resolved candidate list.
    /// </summary>
    internal void InitializeFirstRound()
    {
        _currentRound = new Round(1, Candidates.Select(c => c.Name));
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
            lock (_stateLock) { ResolveIfExpiredLocked(); return _phase; }
        }
    }

    public int CurrentRoundId
    {
        get
        {
            lock (_stateLock) { ResolveIfExpiredLocked(); return _currentRound.Id; }
        }
    }

    public IReadOnlyList<string> CurrentRoundCandidates
    {
        get
        {
            lock (_stateLock) { ResolveIfExpiredLocked(); return _currentRound.CandidatesInPlay; }
        }
    }

    public DateTimeOffset? EndsAtUtc
    {
        get
        {
            lock (_stateLock) { ResolveIfExpiredLocked(); return _currentRound.EndsAtUtc; }
        }
    }

    public string? Winner
    {
        get
        {
            lock (_stateLock) { ResolveIfExpiredLocked(); return _winner; }
        }
    }

    /// <summary>
    /// Whole seconds remaining on the current round's timer (rounded up). Null
    /// when no timer is currently running on the session.
    /// </summary>
    public int? SecondsRemaining
    {
        get
        {
            lock (_stateLock)
            {
                ResolveIfExpiredLocked();
                if (_currentRound.EndsAtUtc is null) return null;
                if (_phase is not (SessionPhase.Voting or SessionPhase.TieBreaker)) return null;
                var remaining = (_currentRound.EndsAtUtc.Value - _timeProvider.GetUtcNow()).TotalSeconds;
                if (remaining <= 0) return 0;
                return (int)Math.Ceiling(remaining);
            }
        }
    }

    /// <summary>
    /// Per-candidate vote counts for the CURRENT round. Includes only the
    /// candidates that are in play for this round. Each is present even when
    /// they have zero votes.
    /// </summary>
    public IReadOnlyDictionary<string, int> Tally
    {
        get
        {
            lock (_stateLock) { ResolveIfExpiredLocked(); return _currentRound.Tally(); }
        }
    }

    internal void AddParticipant(string nickname)
    {
        lock (_participantsLock) _participants.Add(nickname);
    }

    internal StartTimerResult StartTimer(int durationSeconds)
    {
        if (durationSeconds <= 0) return StartTimerResult.InvalidDuration;
        lock (_stateLock)
        {
            ResolveIfExpiredLocked();
            // Allowed from Lobby (start initial round) or from TieBreaker when
            // no timer is currently running on the tie-breaker round.
            if (_phase == SessionPhase.Lobby)
            {
                _currentRound.EndsAtUtc = _timeProvider.GetUtcNow().AddSeconds(durationSeconds);
                _phase = SessionPhase.Voting;
                return StartTimerResult.Success;
            }
            if (_phase == SessionPhase.TieBreaker && _currentRound.EndsAtUtc is null)
            {
                _currentRound.EndsAtUtc = _timeProvider.GetUtcNow().AddSeconds(durationSeconds);
                return StartTimerResult.Success;
            }
            return StartTimerResult.TimerAlreadyRunning;
        }
    }

    internal ExtendTimerResult ExtendTimer()
    {
        lock (_stateLock)
        {
            ResolveIfExpiredLocked();
            if (_currentRound.EndsAtUtc is null) return ExtendTimerResult.NotRunning;
            if (_phase is not (SessionPhase.Voting or SessionPhase.TieBreaker))
                return ExtendTimerResult.NotRunning;
            _currentRound.EndsAtUtc = _currentRound.EndsAtUtc.Value.AddSeconds(60);
            return ExtendTimerResult.Success;
        }
    }

    internal CastVoteResult CastVote(string nickname, string candidateName)
    {
        lock (_stateLock)
        {
            ResolveIfExpiredLocked();
            if (_phase is not (SessionPhase.Voting or SessionPhase.TieBreaker))
                return CastVoteResult.NotInVotingPhase;

            if (!_currentRound.IsCandidateInPlay(candidateName))
                return CastVoteResult.CandidateNotFound;

            if (_currentRound.VotesByNickname.ContainsKey(nickname))
                return CastVoteResult.AlreadyVoted;
            _currentRound.VotesByNickname[nickname] = candidateName;
            return CastVoteResult.Success;
        }
    }

    private void ResolveIfExpiredLocked()
    {
        if (_phase is not (SessionPhase.Voting or SessionPhase.TieBreaker)) return;
        if (_currentRound.EndsAtUtc is null) return;
        if (_timeProvider.GetUtcNow() < _currentRound.EndsAtUtc.Value) return;

        var tally = _currentRound.Tally();
        var max = tally.Values.Count == 0 ? 0 : tally.Values.Max();

        if (max == 0)
        {
            // No votes were cast in this round. Treat as a multi-way tie of the
            // entire in-play set: start a new tie-breaker round with the SAME
            // candidates carried over. (PRD: tie-breaker rounds repeat until
            // they resolve; an all-zero round cannot resolve, so we re-run it.)
            _currentRound = new Round(_currentRound.Id + 1, _currentRound.CandidatesInPlay);
            _phase = SessionPhase.TieBreaker;
            _winner = null;
            return;
        }

        var topCandidates = tally.Where(kv => kv.Value == max).Select(kv => kv.Key).ToList();
        if (topCandidates.Count == 1)
        {
            _phase = SessionPhase.Finished;
            _winner = topCandidates[0];
            _currentRound.EndsAtUtc = null;
        }
        else
        {
            _currentRound = new Round(_currentRound.Id + 1, topCandidates);
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
        session.InitializeFirstRound();
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
