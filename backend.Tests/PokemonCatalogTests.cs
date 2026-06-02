using Backend.Pokemon;

namespace Backend.Tests;

public class PokemonCatalogTests
{
    private sealed class InMemorySource : IGermanPokemonNameSource
    {
        private readonly IReadOnlyList<GermanPokemonName> _names;
        public InMemorySource(params GermanPokemonName[] names) => _names = names;
        public IReadOnlyList<GermanPokemonName> GetAll() => _names;
    }

    private static IPokemonCatalog CreateCatalog(params GermanPokemonName[] names)
        => new PokemonCatalog(new InMemorySource(names));

    private static readonly GermanPokemonName[] DefaultSample =
    [
        new(1,   "Bisasam"),
        new(4,   "Glumanda"),
        new(7,   "Schiggy"),
        new(25,  "Pikachu"),
        new(195, "Morlord"),
        new(211, "Baldorfish"),
        new(373, "Brutalanda"),
        new(912, "Kwaks"),
    ];

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_only_pokemon_starting_with_letter_case_insensitive()
    {
        var catalog = CreateCatalog(DefaultSample);

        var result = await catalog.GetPokemonByLetterAsync('B', Array.Empty<string>());

        Assert.Equal(
            new[] { "Bisasam", "Baldorfish", "Brutalanda" }.OrderBy(n => n),
            result.Select(c => c.Name).OrderBy(n => n));
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_excludes_names_in_excluded_list_case_insensitive()
    {
        var catalog = CreateCatalog(DefaultSample);

        var result = await catalog.GetPokemonByLetterAsync(
            'b', new[] { "BISASAM", "Baldorfish" });

        Assert.Equal(new[] { "Brutalanda" }, result.Select(c => c.Name));
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_candidates_with_non_empty_sprite_url()
    {
        var catalog = CreateCatalog(DefaultSample);

        var result = await catalog.GetPokemonByLetterAsync('k', Array.Empty<string>());

        Assert.NotEmpty(result);
        Assert.All(result, c => Assert.False(string.IsNullOrWhiteSpace(c.SpriteUrl)));
        Assert.Contains(result, c => c.SpriteUrl.Contains("/sprites/pokemon/912.png"));
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_empty_when_no_pokemon_match_letter()
    {
        var catalog = CreateCatalog(DefaultSample);

        var result = await catalog.GetPokemonByLetterAsync('z', Array.Empty<string>());

        Assert.Empty(result);
    }
}
