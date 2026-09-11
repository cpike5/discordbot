namespace DiscordBot.Core.Configuration;

/// <summary>
/// Configuration options for the not-X feature (X/Twitter link preview).
/// Bound from the "NotX" appsettings section.
/// </summary>
public class NotXOptions
{
    /// <summary>
    /// The configuration section name for binding.
    /// </summary>
    public const string SectionName = "NotX";

    /// <summary>
    /// Gets or sets whether the not-X feature is enabled globally.
    /// When false the slash-command and context-menu modules are left out of command
    /// registration (which deregisters them from Discord on the next startup), the
    /// message handler ignores every message, and <c>NotXService</c> refuses to post.
    /// Per-guild settings are unaffected and take effect again once re-enabled.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the HTTP request timeout in seconds for fxtwitter API calls.
    /// Default is 5 seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum number of bytes to read from the fxtwitter API response.
    /// Default is 262144 (256 KB).
    /// </summary>
    public int MaxResponseBytes { get; set; } = 262144;

    /// <summary>
    /// Gets or sets the User-Agent header value sent to the fxtwitter API.
    /// Default is "DiscordBot/1.0 (+not-x)".
    /// </summary>
    public string UserAgent { get; set; } = "DiscordBot/1.0 (+not-x)";
}
