using System.Collections.Concurrent;
using Backend.Pokemon;

namespace Backend.Sessions;

public sealed class Session
{
    public required Guid Id { get; init; }
    public required char Letter { get; init; }
    public required IReadOnlyList<PokemonCandidate> Candidates { get; init; }

    private readonly HashSet<string> _participants = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _participantsLock = new();

    public IReadOnlyCollection<string> Participants
    {
        get
        {
            lock (_participantsLock) return _participants.ToArray();
        }
    }

    internal void AddParticipant(string nickname)
    {
        lock (_participantsLock) _participants.Add(nickname);
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
}
