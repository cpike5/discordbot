namespace DiscordBot.Core.Constants;

/// <summary>
/// Builders for the feature keys a <c>PriceEntry</c> is stored against. Keys are
/// <c>{area}:{identifier}</c>, and the price lookup is an exact string match, so the key a priced
/// feature asks for and the key an admin saves a price under have to be built the same way.
/// </summary>
public static class CurrencyFeatureKeys
{
    /// <summary>Area prefix for soundboard sounds.</summary>
    public const string SoundboardArea = "soundboard";

    /// <summary>
    /// The feature key for one soundboard sound, e.g.
    /// <c>soundboard:3fa85f64-5717-4562-b3fc-2c963f66afa6</c>.
    /// </summary>
    /// <param name="soundId">The sound's identifier.</param>
    public static string Soundboard(Guid soundId) => $"{SoundboardArea}:{soundId}";
}
