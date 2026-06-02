using Backend.Pokemon;

namespace Backend.Nicknames;

public interface INicknameGenerator
{
    string Generate();
}

public sealed class NicknameGenerator(IGermanPokemonNameSource pokemonNames) : INicknameGenerator
{
    private static readonly string[] Adjectives =
    [
        "glowing", "sneaky", "brave", "fierce", "mighty", "clever", "swift", "wild",
        "sleepy", "hungry", "lucky", "shiny", "fluffy", "grumpy", "jolly", "bold",
        "calm", "quirky", "dizzy", "fancy", "gentle", "mystic", "lazy", "eager",
        "cosmic", "electric", "frozen", "blazing", "stormy", "golden", "silver",
        "tiny", "giant", "dreamy", "fearless", "noble", "rowdy", "silly", "spicy",
        "sparkly", "thunder", "crystal", "shadow", "rusty", "zesty"
    ];

    public string Generate()
    {
        var adjective = Adjectives[Random.Shared.Next(Adjectives.Length)];
        var pokemonList = pokemonNames.GetAll();
        var pokemon = pokemonList[Random.Shared.Next(pokemonList.Count)].Name.ToLowerInvariant();
        return $"{adjective}-{pokemon}";
    }
}
