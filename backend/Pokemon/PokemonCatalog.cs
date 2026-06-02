using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Backend.Pokemon;

public record PokemonCandidate(string Name, string SpriteUrl);

public interface IPokemonCatalog
{
    Task<IReadOnlyList<PokemonCandidate>> GetPokemonByLetterAsync(
        char letter,
        IEnumerable<string> excludedNames,
        CancellationToken ct = default);
}

public sealed class PokemonCatalog : IPokemonCatalog
{
    private readonly HttpClient _http;

    public PokemonCatalog(HttpClient http) => _http = http;

    public async Task<IReadOnlyList<PokemonCandidate>> GetPokemonByLetterAsync(
        char letter,
        IEnumerable<string> excludedNames,
        CancellationToken ct = default)
    {
        var list = await _http.GetFromJsonAsync<PokeApiList>(
            "api/v2/pokemon?limit=10000", ct) ?? new PokeApiList();

        var prefix = char.ToLowerInvariant(letter);
        var excluded = new HashSet<string>(
            excludedNames ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var results = new List<PokemonCandidate>();
        foreach (var entry in list.Results ?? Array.Empty<PokeApiEntry>())
        {
            if (string.IsNullOrEmpty(entry.Name)) continue;
            if (char.ToLowerInvariant(entry.Name[0]) != prefix) continue;
            if (excluded.Contains(entry.Name)) continue;

            var id = ExtractId(entry.Url);
            var sprite = id is null
                ? string.Empty
                : $"https://raw.githubusercontent.com/PokeAPI/sprites/master/sprites/pokemon/{id}.png";

            results.Add(new PokemonCandidate(entry.Name, sprite));
        }

        return results;
    }

    private static int? ExtractId(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        var trimmed = url.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        if (lastSlash < 0 || lastSlash == trimmed.Length - 1) return null;
        return int.TryParse(trimmed.AsSpan(lastSlash + 1), out var id) ? id : null;
    }

    private sealed class PokeApiList
    {
        [JsonPropertyName("results")]
        public PokeApiEntry[]? Results { get; set; }
    }

    private sealed class PokeApiEntry
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("url")]
        public string? Url { get; set; }
    }
}
