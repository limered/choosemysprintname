using Backend.Pokemon;
using Backend.Sessions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IGermanPokemonNameSource>(_ =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "Pokemon", "Data", "german-pokemon-names.json");
    return new JsonFileGermanPokemonNameSource(path);
});
builder.Services.AddSingleton<IPokemonCatalog, PokemonCatalog>();
builder.Services.AddSingleton<SessionManager>();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/api/sessions", async (CreateSessionRequest req, SessionManager mgr, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Letter) || req.Letter.Length != 1 || req.Letter[0] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
        return Results.BadRequest(new { error = "letter must be a single A-Z character" });

    var session = await mgr.CreateAsync(req.Letter[0], ct);
    return Results.Ok(new { id = session.Id });
});

app.MapGet("/api/sessions/{id:guid}", (Guid id, SessionManager mgr) =>
{
    var session = mgr.Get(id);
    if (session is null) return Results.NotFound();

    return Results.Ok(new
    {
        id = session.Id,
        letter = session.Letter.ToString(),
        candidates = session.Candidates.Select(c => new { name = c.Name, spriteUrl = c.SpriteUrl })
    });
});

// SPA fallback: any non-API, non-file route serves index.html
app.MapFallbackToFile("index.html");

app.Run();

public record CreateSessionRequest(string Letter);

public partial class Program;
