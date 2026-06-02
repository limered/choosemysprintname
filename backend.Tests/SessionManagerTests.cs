using Backend.Pokemon;
using Backend.Sessions;

namespace Backend.Tests;

public class SessionManagerTests
{
    private sealed class StubSource : IGermanPokemonNameSource
    {
        public IReadOnlyList<GermanPokemonName> GetAll() => new[]
        {
            new GermanPokemonName(25, "Pikachu"),
        };
    }

    [Fact]
    public async Task AddParticipant_records_nickname_on_session()
    {
        var manager = new SessionManager(new PokemonCatalog(new StubSource()));
        var session = await manager.CreateAsync('P');

        var added = manager.AddParticipant(session.Id, "alice");

        Assert.True(added);
        Assert.Contains("alice", session.Participants);
    }
}
