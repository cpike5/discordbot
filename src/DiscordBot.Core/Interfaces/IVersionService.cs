namespace DiscordBot.Core.Interfaces;

/// <summary>
/// Service interface for retrieving application version information.
/// </summary>
public interface IVersionService
{
    /// <summary>
    /// Gets the current application version string.
    /// </summary>
    /// <returns>The version string (e.g., "v1.0.0"), or a fallback value if unavailable.</returns>
    string GetVersion();
}
