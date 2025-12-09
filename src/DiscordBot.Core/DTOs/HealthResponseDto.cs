namespace DiscordBot.Core.DTOs;

/// <summary>
/// Data transfer object for health check responses.
/// </summary>
public class HealthResponseDto
{
    /// <summary>
    /// Gets or sets the overall health status.
    /// </summary>
    public string Status { get; set; } = "Healthy";

    /// <summary>
    /// Gets or sets the timestamp of the health check.
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets the application version.
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets individual health check results.
    /// </summary>
    public Dictionary<string, string> Checks { get; set; } = new();
}
