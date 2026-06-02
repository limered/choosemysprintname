using System.Net;
using System.Text;
using Backend.Pokemon;

namespace Backend.Tests;

public class PokemonCatalogTests
{
    private const string SamplePokeApiJson = """
    {
      "results": [
        { "name": "bulbasaur",  "url": "https://pokeapi.co/api/v2/pokemon/1/" },
        { "name": "quagsire",   "url": "https://pokeapi.co/api/v2/pokemon/195/" },
        { "name": "qwilfish",   "url": "https://pokeapi.co/api/v2/pokemon/211/" },
        { "name": "quaxly",     "url": "https://pokeapi.co/api/v2/pokemon/912/" },
        { "name": "pikachu",    "url": "https://pokeapi.co/api/v2/pokemon/25/" }
      ]
    }
    """;

    private static IPokemonCatalog CreateCatalog(string json = SamplePokeApiJson)
    {
        var handler = new StubHandler(json);
        var client = new HttpClient(handler) { BaseAddress = new Uri("https://pokeapi.co/") };
        return new PokemonCatalog(client);
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_only_pokemon_starting_with_letter_case_insensitive()
    {
        var catalog = CreateCatalog();

        var result = await catalog.GetPokemonByLetterAsync('Q', Array.Empty<string>());

        Assert.Equal(
            new[] { "quagsire", "qwilfish", "quaxly" }.OrderBy(n => n),
            result.Select(c => c.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_excludes_names_in_excluded_list_case_insensitive()
    {
        var catalog = CreateCatalog();

        var result = await catalog.GetPokemonByLetterAsync(
            'q', new[] { "QWILFISH", "Quagsire" });

        Assert.Equal(new[] { "quaxly" }, result.Select(c => c.Name));
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_candidates_with_non_empty_sprite_url()
    {
        var catalog = CreateCatalog();

        var result = await catalog.GetPokemonByLetterAsync('q', Array.Empty<string>());

        Assert.NotEmpty(result);
        Assert.All(result, c => Assert.False(string.IsNullOrWhiteSpace(c.SpriteUrl)));
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_empty_when_no_pokemon_match_letter()
    {
        var catalog = CreateCatalog();

        var result = await catalog.GetPokemonByLetterAsync('z', Array.Empty<string>());

        Assert.Empty(result);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _json;
        public int CallCount { get; private set; }
        public StubHandler(string json) => _json = json;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
