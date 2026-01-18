using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "Discord:Token is required. Set it via environment variable Discord__Token or user secrets.")]
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
