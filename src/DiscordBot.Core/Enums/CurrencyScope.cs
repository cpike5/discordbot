namespace DiscordBot.Core.Enums;

/// <summary>
/// Whether a currency belongs to one guild or to the whole bot.
/// </summary>
public enum CurrencyScope
{
    /// <summary>Owned by the bot owner and usable in every guild.</summary>
    Global = 0,

    /// <summary>Owned by a single guild and usable only there.</summary>
    Guild = 1
}
