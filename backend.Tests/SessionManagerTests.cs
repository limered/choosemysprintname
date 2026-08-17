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

    private sealed class FakeWinnerHistoryStore : IWinnerHistoryStore
    {
        public List<string> Saved { get; } = new();

        public FakeWinnerHistoryStore(params string[] preseed) => Saved.AddRange(preseed);

        public Task SaveWinnerAsync(string name, CancellationToken ct = default)
        {
            Saved.Add(name);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<string>> GetAllWinnerNamesAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Saved.ToArray());

        public Task ClearAllAsync(CancellationToken ct = default)
        {
            Saved.Clear();
            return Task.CompletedTask;
        }
    }

    private static SessionManager CreateManager(params GermanPokemonName[] names)
        => CreateManager(new FakeTimeProvider(), new FakeWinnerHistoryStore(), names);

    private static SessionManager CreateManager(FakeTimeProvider time, params GermanPokemonName[] names)
        => CreateManager(time, new FakeWinnerHistoryStore(), names);

    private static SessionManager CreateManager(
        FakeTimeProvider time,
        IWinnerHistoryStore winnerStore,
        params GermanPokemonName[] names)
    {
        var sample = names.Length == 0
            ? new[]
              {
                  new GermanPokemonName(1, "Bisasam"),
                  new GermanPokemonName(2, "Bisaknosp"),
                  new GermanPokemonName(3, "Bisaflor"),
              }
            : names;
        return new SessionManager(new PokemonCatalog(new StubSource(sample)), time, winnerStore);
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
    public async Task CastVote_allows_changing_vote_within_same_round()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");
        manager.StartTimer(session.Id, 60);
        manager.CastVote(session.Id, "alice", "Bisasam");

        var second = manager.CastVote(session.Id, "alice", "Bisaknosp");

        Assert.Equal(CastVoteResult.Success, second);
        Assert.Equal(0, session.Tally["Bisasam"]);
        Assert.Equal(1, session.Tally["Bisaknosp"]);
    }

    [Fact]
    public async Task VotersByCandidate_lists_each_voter_under_their_chosen_candidate()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 60);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisasam");
        manager.CastVote(session.Id, "carol", "Bisaknosp");

        var voters = session.VotersByCandidate;

        Assert.Equal(new[] { "alice", "bob" }, voters["Bisasam"].OrderBy(x => x).ToArray());
        Assert.Equal(new[] { "carol" }, voters["Bisaknosp"].ToArray());
        Assert.Empty(voters["Bisaflor"]);
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

    [Fact]
    public async Task GetActiveSessions_returns_sessions_not_yet_finished()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var lobby = await manager.CreateAsync('B');
        var voting = await manager.CreateAsync('B');
        manager.StartTimer(voting.Id, 30);
        var finished = await manager.CreateAsync('B');
        manager.StartTimer(finished.Id, 30);
        manager.CastVote(finished.Id, "alice", "Bisasam");
        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(SessionPhase.Finished, finished.Phase);

        var active = manager.GetActiveSessions().Select(s => s.Id).ToHashSet();

        Assert.Contains(lobby.Id, active);
        Assert.Contains(voting.Id, active);
        Assert.DoesNotContain(finished.Id, active);
    }

    [Fact]
    public async Task Delete_removes_the_session_so_it_cannot_be_found_or_listed()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');

        var removed = manager.Delete(session.Id);

        Assert.True(removed);
        Assert.Null(manager.Get(session.Id));
        Assert.DoesNotContain(session.Id, manager.GetActiveSessions().Select(s => s.Id));
    }

    [Fact]
    public void Delete_returns_false_for_unknown_session_id()
    {
        var manager = CreateManager();

        var removed = manager.Delete(Guid.NewGuid());

        Assert.False(removed);
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

        Assert.Equal(StartTimerResult.TimerAlreadyRunning, second);
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

    // ---------- Issue #6: Tie-breaker rounds ----------

    [Fact]
    public async Task TieBreaker_round_contains_only_previously_tied_candidates()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        // Bisaflor stays at 0 votes -> not part of the tie

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        var actual = session.CurrentRoundCandidates.OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "Bisaknosp", "Bisasam" }, actual);
    }

    [Fact]
    public async Task Votes_are_reset_when_entering_tiebreaker()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.All(session.Tally.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public async Task Nickname_can_vote_again_in_tiebreaker_round()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        manager.StartTimer(session.Id, 30);

        var result = manager.CastVote(session.Id, "alice", "Bisaknosp");

        Assert.Equal(CastVoteResult.Success, result);
        Assert.Equal(1, session.Tally["Bisaknosp"]);
    }

    [Fact]
    public async Task CastVote_in_tiebreaker_rejects_candidate_not_in_round()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        manager.StartTimer(session.Id, 30);

        // Bisaflor was eliminated (had 0 votes) and is not in this round
        var result = manager.CastVote(session.Id, "carol", "Bisaflor");

        Assert.Equal(CastVoteResult.CandidateNotFound, result);
    }

    [Fact]
    public async Task StartTimer_works_in_tiebreaker_after_expiry()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.Null(session.SecondsRemaining);

        var result = manager.StartTimer(session.Id, 45);

        Assert.Equal(StartTimerResult.Success, result);
        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.Equal(45, session.SecondsRemaining);
    }

    [Fact]
    public async Task Tiebreaker_resolves_to_finished_when_unique_top_vote_emerges()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisaknosp");

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.Finished, session.Phase);
        Assert.Equal("Bisaknosp", session.Winner);
    }

    [Fact]
    public async Task Tiebreaker_chains_into_another_tiebreaker_when_still_tied()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        var roundAfterFirstExpiry = session.CurrentRoundId;

        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.True(session.CurrentRoundId > roundAfterFirstExpiry);
        Assert.Equal(new[] { "Bisaknosp", "Bisasam" }, session.CurrentRoundCandidates.OrderBy(x => x).ToArray());
        Assert.All(session.Tally.Values, v => Assert.Equal(0, v));
    }

    [Fact]
    public async Task Tiebreaker_with_zero_votes_chains_with_same_candidate_set()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        var inPlayBefore = session.CurrentRoundCandidates.OrderBy(x => x).ToArray();
        var roundBefore = session.CurrentRoundId;
        manager.StartTimer(session.Id, 30);
        // no votes cast

        time.Advance(TimeSpan.FromSeconds(31));

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.True(session.CurrentRoundId > roundBefore);
        Assert.Equal(inPlayBefore, session.CurrentRoundCandidates.OrderBy(x => x).ToArray());
    }

    [Fact]
    public async Task StartTimer_rejects_when_timer_already_running_in_tiebreaker()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");
        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(StartTimerResult.Success, manager.StartTimer(session.Id, 30));

        var second = manager.StartTimer(session.Id, 15);

        Assert.Equal(StartTimerResult.TimerAlreadyRunning, second);
    }

    // ---------- Issue #7: Winner persistence ----------

    [Fact]
    public async Task Winner_is_saved_to_store_when_session_finishes()
    {
        var time = new FakeTimeProvider();
        var store = new FakeWinnerHistoryStore();
        var manager = CreateManager(time, store);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");

        time.Advance(TimeSpan.FromSeconds(31));
        _ = session.Phase; // observe expiry

        Assert.Equal(SessionPhase.Finished, session.Phase);
        Assert.Equal(new[] { "Bisasam" }, store.Saved.ToArray());
    }

    [Fact]
    public async Task Winner_is_not_saved_when_session_ends_in_tiebreaker()
    {
        var time = new FakeTimeProvider();
        var store = new FakeWinnerHistoryStore();
        var manager = CreateManager(time, store);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");

        time.Advance(TimeSpan.FromSeconds(31));
        _ = session.Phase;

        Assert.Equal(SessionPhase.TieBreaker, session.Phase);
        Assert.Empty(store.Saved);
    }

    [Fact]
    public async Task Winner_is_saved_only_once_even_if_state_is_read_many_times()
    {
        var time = new FakeTimeProvider();
        var store = new FakeWinnerHistoryStore();
        var manager = CreateManager(time, store);
        var session = await manager.CreateAsync('B');
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");

        time.Advance(TimeSpan.FromSeconds(31));

        // Multiple state reads each invoke ResolveIfExpiredLocked.
        _ = session.Phase;
        _ = session.Winner;
        _ = session.Tally;
        _ = session.SecondsRemaining;
        _ = session.CurrentRoundCandidates;

        Assert.Single(store.Saved);
        Assert.Equal("Bisasam", store.Saved[0]);
    }

    [Fact]
    public async Task CreateAsync_passes_existing_winner_names_as_exclusion_list_to_catalog()
    {
        var store = new FakeWinnerHistoryStore("Bisasam");
        var manager = CreateManager(new FakeTimeProvider(), store);

        var session = await manager.CreateAsync('B');

        var candidateNames = session.Candidates.Select(c => c.Name).ToArray();
        Assert.DoesNotContain("Bisasam", candidateNames);
        Assert.Contains("Bisaknosp", candidateNames);
        Assert.Contains("Bisaflor", candidateNames);
    }

    // ---------- Participant vote status (who voted / all votes in) ----------

    [Fact]
    public async Task ParticipantStatuses_marks_voted_and_unvoted_participants()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");
        manager.AddParticipant(session.Id, "bob");
        manager.AddParticipant(session.Id, "carol");
        manager.StartTimer(session.Id, 60);

        manager.CastVote(session.Id, "alice", "Bisasam");

        var statuses = session.ParticipantStatuses;
        Assert.Equal(3, statuses.Count);
        Assert.True(statuses.Single(p => p.Nickname == "alice").HasVoted);
        Assert.False(statuses.Single(p => p.Nickname == "bob").HasVoted);
        Assert.False(statuses.Single(p => p.Nickname == "carol").HasVoted);
    }

    [Fact]
    public async Task ParticipantStatuses_keeps_voter_marked_after_changing_vote()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");
        manager.StartTimer(session.Id, 60);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "alice", "Bisaknosp");

        var status = session.ParticipantStatuses.Single();

        Assert.True(status.HasVoted);
        Assert.Equal("alice", status.Nickname);
    }

    [Fact]
    public async Task ParticipantStatuses_reset_when_entering_tiebreaker_round()
    {
        var time = new FakeTimeProvider();
        var manager = CreateManager(time);
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "alice");
        manager.AddParticipant(session.Id, "bob");
        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisasam");
        manager.CastVote(session.Id, "bob", "Bisaknosp");

        time.Advance(TimeSpan.FromSeconds(31));
        Assert.Equal(SessionPhase.TieBreaker, session.Phase);

        // Fresh round -> nobody has voted yet
        Assert.All(session.ParticipantStatuses, p => Assert.False(p.HasVoted));

        manager.StartTimer(session.Id, 30);
        manager.CastVote(session.Id, "alice", "Bisaknosp");

        var statuses = session.ParticipantStatuses;
        Assert.True(statuses.Single(p => p.Nickname == "alice").HasVoted);
        Assert.False(statuses.Single(p => p.Nickname == "bob").HasVoted);
    }

    [Fact]
    public async Task ParticipantStatuses_are_sorted_by_nickname()
    {
        var manager = CreateManager();
        var session = await manager.CreateAsync('B');
        manager.AddParticipant(session.Id, "carol");
        manager.AddParticipant(session.Id, "alice");
        manager.AddParticipant(session.Id, "bob");

        var nicknames = session.ParticipantStatuses.Select(p => p.Nickname).ToArray();

        Assert.Equal(new[] { "alice", "bob", "carol" }, nicknames);
    }
}
