using System.Collections.Concurrent;
using Backend.Pokemon;

namespace Backend.Sessions;

public enum CastVoteResult
{
    Success,
    SessionNotFound,
    CandidateNotFound,
    AlreadyVoted,
}

public sealed class Session
{
    public required Guid Id { get; init; }
    public required char Letter { get; init; }
    public required IReadOnlyList<PokemonCandidate> Candidates { get; init; }

    private readonly HashSet<string> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _participantsLock = new();

    // nickname -> candidate name. Nickname comparison is case-insensitive.
    private readonly Dictionary<string, string> _votesByNickname = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _votesLock = new();

    public IReadOnlyCollection<string> Participants
    {
        get
        {
            lock (_participantsLock) return _participants.ToArray();
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
    }

    internal void AddParticipant(string nickname)
    {
        lock (_participantsLock) _participants.Add(nickname);
    }

    internal CastVoteResult CastVote(string nickname, string candidateName)
    {
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
}

public sealed class SessionManager
{
    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly IPokemonCatalog _catalog;

    public SessionManager(IPokemonCatalog catalog) => _catalog = catalog;

    public async Task<Session> CreateAsync(char letter, CancellationToken ct = default)
    {
        var candidates = await _catalog.GetPokemonByLetterAsync(
            letter, Array.Empty<string>(), ct);

        var session = new Session
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
}
