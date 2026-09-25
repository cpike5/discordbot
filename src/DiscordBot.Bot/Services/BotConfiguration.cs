using System.ComponentModel.DataAnnotations;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Strongly-typed configuration for Discord bot settings.
/// </summary>
public class BotConfiguration : IValidatableObject
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "Discord";

    /// <summary>
    /// Discord bot token for authentication.
    /// Should be stored in user secrets for security.
    /// Required unless <see cref="OfflineMode"/> is enabled.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Runs the process without connecting to the Discord gateway, for local testing of the
    /// web portal. The token (and Discord OAuth credentials) become optional, the bot never
    /// logs in, and every Discord-backed view sees an empty, disconnected client.
    /// Never enable this in production.
    /// </summary>
    public bool OfflineMode { get; set; }

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

    /// <inheritdoc />
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!OfflineMode && string.IsNullOrWhiteSpace(Token))
        {
            yield return new ValidationResult(
                "Discord:Token is required. Set it via environment variable Discord__Token or user secrets, " +
                "or set Discord:OfflineMode to true to run the web portal without connecting to Discord.",
                new[] { nameof(Token) });
        }
    }
}
