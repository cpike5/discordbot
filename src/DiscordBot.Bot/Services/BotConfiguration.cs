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
    /// Whether the Discord bot gateway connection is enabled. Defaults to true.
    /// Set to false to run the web portal without logging in to Discord (no token required,
    /// no slash-command registration, no interaction handlers) — used for browser/UI testing
    /// and for running the admin portal without a bot. See <see cref="BotConfigurationValidator"/>
    /// for how this makes <see cref="Token"/> conditionally required.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Discord bot token for authentication. Required when <see cref="Enabled"/> is true.
    /// Should be stored in user secrets for security.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Optional test guild ID for faster command registration during development.
    /// When set, commands are registered to this specific guild instead of globally.
    /// </summary>
    public ulong? TestGuildId { get; set; }

    /// <summary>
    /// Default number of invocations allowed within the rate limit period.
    /// </summary>
    public int DefaultRateLimitInvokes { get; set; } = 3;

    /// <summary>
    /// Default rate limit period in seconds.
    /// </summary>
    public double DefaultRateLimitPeriodSeconds { get; set; } = 60.0;

    /// <summary>
    /// Additional user IDs that should be treated as bot owners (beyond the application owner).
    /// </summary>
    public List<ulong> AdditionalOwnerIds { get; set; } = new();
}
