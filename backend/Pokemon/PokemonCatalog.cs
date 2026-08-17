using System.Text.Json;

namespace Backend.Pokemon;

public record PokemonCandidate(string Name, string SpriteUrl, bool IsPastWinner = false);

public record GermanPokemonName(int Id, string Name);

public interface IGermanPokemonNameSource
{
    IReadOnlyList<GermanPokemonName> GetAll();
}

public interface IPokemonCatalog
{
    Task<IReadOnlyList<PokemonCandidate>> GetPokemonByLetterAsync(
        char letter,
        IEnumerable<string> excludedNames,
        CancellationToken ct = default);
}

public sealed class PokemonCatalog : IPokemonCatalog
{
    private readonly IGermanPokemonNameSource _source;

    public PokemonCatalog(IGermanPokemonNameSource source) => _source = source;

    public Task<IReadOnlyList<PokemonCandidate>> GetPokemonByLetterAsync(
        char letter,
        IEnumerable<string> excludedNames,
        CancellationToken ct = default)
    {
        var prefix = char.ToLowerInvariant(letter);
        var excluded = new HashSet<string>(
            excludedNames ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var results = new List<PokemonCandidate>();
        foreach (var entry in _source.GetAll())
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (char.ToLowerInvariant(entry.Name[0]) != prefix) continue;

            // Past winners stay in the list so the UI can show why they are
            // not selectable; they are flagged and filtered out of voting
            // rounds by the SessionManager instead.
            var sprite = $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{entry.Id}.png";
            results.Add(new PokemonCandidate(entry.Name, sprite, excluded.Contains(entry.Name)));
        }

        return Task.FromResult<IReadOnlyList<PokemonCandidate>>(results);
    }
}

public sealed class JsonFileGermanPokemonNameSource : IGermanPokemonNameSource
{
    private readonly IReadOnlyList<GermanPokemonName> _names;

    public JsonFileGermanPokemonNameSource(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"German Pokemon name data file not found: {filePath}", filePath);

        using var stream = File.OpenRead(filePath);
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        var parsed = JsonSerializer.Deserialize<List<GermanPokemonName>>(stream, options)
            ?? new List<GermanPokemonName>();
        _names = parsed;
    }

    public IReadOnlyList<GermanPokemonName> GetAll() => _names;
}
