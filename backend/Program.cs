using Backend.Nicknames;
using Backend.Pokemon;
using Backend.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGermanPokemonNameSource>(_ =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "Pokemon", "Data", "german-pokemon-names.json");
    return new JsonFileGermanPokemonNameSource(path);
});
builder.Services.AddSingleton<IPokemonCatalog, PokemonCatalog>();
builder.Services.AddSingleton<INicknameGenerator, NicknameGenerator>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IWinnerHistoryStore>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var path = config["Sqlite:WinnersDbPath"];
    if (string.IsNullOrWhiteSpace(path))
        path = Path.Combine(AppContext.BaseDirectory, "winners.db");
    return new SqliteWinnerHistoryStore(path);
});
builder.Services.AddSingleton<SessionManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/api/nickname", (INicknameGenerator gen) => Results.Ok(new { nickname = gen.Generate() }));

app.MapPost("/api/sessions", async (CreateSessionRequest req, SessionManager mgr, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Letter) || req.Letter.Length != 1 || req.Letter[0] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
        return Results.BadRequest(new { error = "letter must be a single A-Z character" });

    var session = await mgr.CreateAsync(req.Letter[0], ct);
    return Results.Ok(new { id = session.Id });
});

app.MapGet("/api/sessions", (SessionManager mgr) =>
{
    var active = mgr.GetActiveSessions().Select(s => new
    {
        id = s.Id,
        letter = s.Letter.ToString(),
        phase = s.Phase.ToString(),
        participantCount = s.Participants.Count,
    });
    return Results.Ok(active);
});

app.MapGet("/api/sessions/{id:guid}", (Guid id, SessionManager mgr) =>
{
    var session = mgr.Get(id);
    if (session is null) return Results.NotFound();

    var tally = session.Tally;
    var voters = session.VotersByCandidate;
    var roundCandidates = session.CurrentRoundCandidates;
    return Results.Ok(new
    {
        id = session.Id,
        letter = session.Letter.ToString(),
        phase = session.Phase.ToString(),
        secondsRemaining = session.SecondsRemaining,
        winner = session.Winner,
        roundId = session.CurrentRoundId,
        candidates = session.Candidates.Select(c => new { name = c.Name, spriteUrl = c.SpriteUrl }),
        roundCandidates = roundCandidates,
        votes = roundCandidates.Select(name => new
        {
            name,
            count = tally.TryGetValue(name, out var n) ? n : 0,
            voters = voters.TryGetValue(name, out var vs) ? vs : (IReadOnlyList<string>)Array.Empty<string>()
        })
    });
});

app.MapPost("/api/sessions/{id:guid}/participants", (Guid id, JoinSessionRequest req, SessionManager mgr) =>
{
    if (string.IsNullOrWhiteSpace(req?.Nickname))
        return Results.BadRequest(new { error = "nickname is required" });

    var nickname = req.Nickname.Trim();
    if (!mgr.AddParticipant(id, nickname))
        return Results.NotFound();

    return Results.Ok(new { ok = true });
});

app.MapPost("/api/sessions/{id:guid}/votes", (Guid id, CastVoteRequest req, SessionManager mgr) =>
{
    if (string.IsNullOrWhiteSpace(req?.Nickname) || string.IsNullOrWhiteSpace(req?.CandidateName))
        return Results.BadRequest(new { error = "nickname and candidateName are required" });

    var result = mgr.CastVote(id, req.Nickname.Trim(), req.CandidateName.Trim());
    return result switch
    {
        CastVoteResult.Success => Results.Ok(new { ok = true }),
        CastVoteResult.SessionNotFound => Results.NotFound(new { error = "session not found" }),
        CastVoteResult.CandidateNotFound => Results.NotFound(new { error = "candidate not found in session" }),
        CastVoteResult.NotInVotingPhase => Results.Conflict(new { error = "voting is not active in this session phase" }),
        _ => Results.StatusCode(500),
    };
});

app.MapPost("/api/sessions/{id:guid}/timer/start", (Guid id, StartTimerRequest req, SessionManager mgr) =>
{
    var duration = req?.DurationSeconds ?? 0;
    var result = mgr.StartTimer(id, duration);
    return result switch
    {
        StartTimerResult.Success => Results.Ok(new { ok = true }),
        StartTimerResult.SessionNotFound => Results.NotFound(new { error = "session not found" }),
        StartTimerResult.TimerAlreadyRunning => Results.Conflict(new { error = "a timer is already running" }),
        StartTimerResult.InvalidDuration => Results.BadRequest(new { error = "durationSeconds must be > 0" }),
        _ => Results.StatusCode(500),
    };
});

app.MapPost("/api/sessions/{id:guid}/timer/extend", (Guid id, SessionManager mgr) =>
{
    var result = mgr.ExtendTimer(id);
    return result switch
    {
        ExtendTimerResult.Success => Results.Ok(new { ok = true }),
        ExtendTimerResult.SessionNotFound => Results.NotFound(new { error = "session not found" }),
        ExtendTimerResult.NotRunning => Results.Conflict(new { error = "timer is not running" }),
        _ => Results.StatusCode(500),
    };
});

// SPA fallback: any non-API, non-file route serves index.html
app.MapFallbackToFile("index.html");

app.Run();

public record CreateSessionRequest(string Letter);
public record JoinSessionRequest(string Nickname);
public record CastVoteRequest(string Nickname, string CandidateName);
public record StartTimerRequest(int DurationSeconds);

public partial class Program;
