namespace DiscordBot.Bot.Services;

/// <summary>
/// Strongly-typed configuration for Discord bot settings.
/// </summary>
public class BotConfiguration
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Discord";

    /// <summary>
    /// Discord bot token for authentication.
    /// Should be stored in user secrets for security.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Optional test guild ID for faster command registration during development.
    /// When set, commands are registered to this specific guild instead of globally.
    /// </summary>
    public ulong? TestGuildId { get; set; }
}
