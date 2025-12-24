namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for Discord OAuth 2.0 authentication.
/// </summary>
public class DiscordOAuthOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "Discord:OAuth";

    /// <summary>
    /// Gets or sets the Discord OAuth client ID from the Discord Developer Portal.
    /// Required for Discord authentication. Default is empty string.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord OAuth client secret from the Discord Developer Portal.
    /// Should be stored in user secrets or environment variables. Default is empty string.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OAuth scopes requested during Discord authentication.
    /// Default scopes are "identify", "email", and "guilds".
    /// </summary>
    public List<string> Scopes { get; set; } = new() { "identify", "email", "guilds" };
}
