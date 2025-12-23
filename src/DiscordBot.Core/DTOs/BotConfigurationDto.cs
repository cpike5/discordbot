namespace DiscordBot.Core.DTOs;

/// <summary>
/// Read-only bot configuration for display purposes.
/// Sensitive values are masked for security.
/// </summary>
public class BotConfigurationDto
{
    /// <summary>
    /// Gets or sets the bot token (masked, shows only last 4 characters).
    /// </summary>
    public string TokenMasked { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the test guild ID if configured.
    /// </summary>
    public ulong? TestGuildId { get; set; }

    /// <summary>
    /// Gets or sets whether a test guild is configured.
    /// </summary>
    public bool HasTestGuild { get; set; }

    /// <summary>
    /// Gets or sets the database provider name.
    /// </summary>
    public string DatabaseProvider { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord.NET version.
    /// </summary>
    public string DiscordNetVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public string AppVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the .NET runtime version.
    /// </summary>
    public string RuntimeVersion { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the default rate limit invokes.
    /// </summary>
    public int DefaultRateLimitInvokes { get; set; }

    /// <summary>
    /// Gets or sets the default rate limit period in seconds.
    /// </summary>
    public double DefaultRateLimitPeriodSeconds { get; set; }
}
