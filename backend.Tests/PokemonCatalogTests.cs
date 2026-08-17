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
    public async Task GetPokemonByLetterAsync_flags_excluded_names_as_past_winners()
    {
        var catalog = CreateCatalog(DefaultSample);

        var result = await catalog.GetPokemonByLetterAsync(
            'b', new[] { "BISASAM", "Baldorfish" });

        var byName = result.ToDictionary(c => c.Name);
        Assert.Equal(3, result.Count);
        Assert.True(byName["Bisasam"].IsPastWinner);
        Assert.True(byName["Baldorfish"].IsPastWinner);
        Assert.False(byName["Brutalanda"].IsPastWinner);
    }

    [Fact]
    public async Task GetPokemonByLetterAsync_returns_all_matching_pokemon_even_when_excluded()
    {
        var catalog = CreateCatalog(DefaultSample);

        var result = await catalog.GetPokemonByLetterAsync('b', new[] { "Bisasam" });

        // Excluded names are kept in the list (flagged) so the UI can show them.
        Assert.Equal(
            new[] { "Bisasam", "Baldorfish", "Brutalanda" }.OrderBy(n => n),
            result.Select(c => c.Name).OrderBy(n => n));
        Assert.All(
            result.Where(c => c.Name != "Bisasam"),
            c => Assert.False(c.IsPastWinner));
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
