using Backend.Pokemon;
using Backend.Sessions;

namespace Backend.Tests;

public class SessionManagerTests
{
    private sealed class StubSource : IGermanPokemonNameSource
    {
        private readonly IReadOnlyList<GermanPokemonName> _names;
        public StubSource(params GermanPokemonName[] names) => _names = names;
        public IReadOnlyList<GermanPokemonName> GetAll() => _names;
    }

    private static SessionManager CreateManager(params GermanPokemonName[] names)
    {
        var sample = names.Length == 0
            ? new[]
              {
                  new GermanPokemonName(1, "Bisasam"),
                  new GermanPokemonName(2, "Bisaknosp"),
                  new GermanPokemonName(3, "Bisaflor"),
              }
            : names;
        return new SessionManager(new PokemonCatalog(new StubSource(sample)));
    }

    [Fact]
    public async Task AddParticipant_records_nickname_on_session()
    {
        var manager = CreateManager(new GermanPokemonName(25, "Pikachu"));
        var session = await manager.CreateAsync('P');

        var added = manager.AddParticipant(session.Id, "alice");

        Assert.True(added);
        Assert.Contains("alice", session.Participants);
    }

    [Fact]
    public async Task CastVote_records_vote_when_nickname_has_not_voted()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");

        var result = manager.CastVote(session.Id, "alice", "Bisasam");

        Assert.Equal(CastVoteResult.Success, result);
        Assert.Equal(1, session.Tally["Bisasam"]);
    }

    [Fact]
    public async Task CastVote_rejects_second_vote_from_same_nickname()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");
        manager.CastVote(session.Id, "alice", "Bisasam");

        var second = manager.CastVote(session.Id, "alice", "Bisaknosp");

        Assert.Equal(CastVoteResult.AlreadyVoted, second);
        Assert.Equal(1, session.Tally["Bisasam"]);
        Assert.Equal(0, session.Tally["Bisaknosp"]);
    }

    [Fact]
    public async Task CastVote_rejects_when_candidate_not_in_session()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');

        var result = manager.CastVote(session.Id, "alice", "Pikachu");

        Assert.Equal(CastVoteResult.CandidateNotFound, result);
    }

    [Fact]
    public void CastVote_rejects_when_session_does_not_exist()
    {
        var manager = CreateManager();

        var result = manager.CastVote(Guid.NewGuid(), "alice", "Bisasam");

        Assert.Equal(CastVoteResult.SessionNotFound, result);
    }

    [Fact]
    public async Task Tally_includes_all_candidates_with_zero_when_unvoted()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');

        var tally = session.Tally;

        Assert.Equal(3, tally.Count);
        Assert.Equal(0, tally["Bisasam"]);
        Assert.Equal(0, tally["Bisaknosp"]);
        Assert.Equal(0, tally["Bisaflor"]);
    }
}
