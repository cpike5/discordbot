namespace DiscordBot.Core.Utilities;

/// <summary>
/// Configuration options for log sanitization.
/// </summary>
public class LogSanitizationOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "LogSanitization";

    /// <summary>
    /// Gets or sets whether log sanitization is enabled.
    /// Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets additional custom regex patterns for sanitization.
    /// Key is the pattern name, value is the regex pattern.
    /// </summary>
    public Dictionary<string, CustomPattern> CustomPatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets additional sensitive key names to redact.
    /// Values in dictionary keys matching these names will be fully redacted.
    /// </summary>
    public List<string> AdditionalSensitiveKeys { get; set; } = new();
}

/// <summary>
/// Represents a custom sanitization pattern.
/// </summary>
public class CustomPattern
{
    /// <summary>
    /// Gets or sets the regex pattern to match.
    /// </summary>
    public string Pattern { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the replacement marker.
    /// </summary>
    public string Replacement { get; set; } = "[REDACTED]";
}
