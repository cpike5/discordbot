using System.ComponentModel.DataAnnotations;

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
    [Required(ErrorMessage = "Discord:OAuth:ClientId is required. Set it via environment variable Discord__OAuth__ClientId or user secrets.")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Discord OAuth client secret from the Discord Developer Portal.
    /// Should be stored in user secrets or environment variables. Default is empty string.
    /// </summary>
    [Required(ErrorMessage = "Discord:OAuth:ClientSecret is required. Set it via environment variable Discord__OAuth__ClientSecret or user secrets.")]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OAuth scopes requested during Discord authentication.
    /// Default scopes are "identify", "email", and "guilds".
    /// </summary>
    public List<string> Scopes { get; set; } = new() { "identify", "email", "guilds" };
}
