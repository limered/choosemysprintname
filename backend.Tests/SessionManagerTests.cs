using Backend.Pokemon;
using Backend.Sessions;
using Microsoft.Extensions.Time.Testing;

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
        => CreateManager(new FakeTimeProvider(), names);

    private static SessionManager CreateManager(FakeTimeProvider time, params GermanPokemonName[] names)
    {
        var sample = names.Length == 0
            ? new[]
              {
                  new GermanPokemonName(1, "Bisasam"),
                  new GermanPokemonName(2, "Bisaknosp"),
                  new GermanPokemonName(3, "Bisaflor"),
              }
            : names;
        return new SessionManager(new PokemonCatalog(new StubSource(sample)), time);
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
        manager.StartTimer(session.Id, 60);

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
        manager.StartTimer(session.Id, 60);
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
        manager.StartTimer(session.Id, 60);

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

    // ---------- Issue #5: timer + phase + winner ----------

    [Fact]
    public async Task StartTimer_transitions_lobby_session_to_voting_phase()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        Assert.Equal(SessionPhase.Lobby, session.Phase);

        var result = manager.StartTimer(session.Id, 60);

        Assert.Equal(StartTimerResult.Success, result);
        Assert.Equal(SessionPhase.Voting, session.Phase);
        Assert.Equal(60, session.SecondsRemaining);
    }

    [Fact]
    public async Task StartTimer_rejects_when_already_started()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 60);

        var second = manager.StartTimer(session.Id, 30);

        Assert.Equal(StartTimerResult.NotInLobby, second);
    }

    [Fact]
    public async Task StartTimer_rejects_negative_or_zero_duration()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');

        Assert.Equal(StartTimerResult.InvalidDuration, manager.StartTimer(session.Id, 0));
        Assert.Equal(StartTimerResult.InvalidDuration, manager.StartTimer(session.Id, -5));
        Assert.Equal(SessionPhase.Lobby, session.Phase);
    }

    [Fact]
    public async Task ExtendTimer_adds_60_seconds_to_running_timer()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);

        time.Advance(TimeSpan.FromSeconds(10));
        var result = manager.ExtendTimer(session.Id);

        Assert.Equal(ExtendTimerResult.Success, result);
        Assert.Equal(80, session.SecondsRemaining);
    }

    [Fact]
    public async Task ExtendTimer_rejects_when_not_running()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');

        var result = manager.ExtendTimer(session.Id);

        Assert.Equal(ExtendTimerResult.NotRunning, result);
    }

    [Fact]
    public async Task CastVote_rejects_when_phase_is_lobby()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");

        var result = manager.CastVote(session.Id, "alice", "Bisasam");

        Assert.Equal(CastVoteResult.NotInVotingPhase, result);
    }

    [Fact]
    public async Task Session_transitions_to_finished_when_timer_expires_with_unique_top_vote()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.Finished, session.Phase);
        Assert.Equal("Bisasam", session.Winner);
        Assert.Null(session.SecondsRemaining);
    }

    [Fact]
    public async Task Session_transitions_to_tiebreaker_when_timer_expires_with_tie()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.Null(session.Winner);
    }

    [Fact]
    public async Task Session_transitions_to_tiebreaker_when_timer_expires_with_no_votes()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.Null(session.Winner);
    }

    [Fact]
    public async Task CastVote_rejects_when_phase_is_finished()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(SessionPhase.Finished, session.Phase);

        var result = manager.CastVote(session.Id, "bob", "Bisaknosp");

        Assert.Equal(CastVoteResult.NotInVotingPhase, result);
    }
}
