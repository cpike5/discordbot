using System.Reflection;
using DiscordBot.Core.Interfaces;

namespace DiscordBot.Bot.Services;

/// <summary>
/// Service for retrieving application version information.
/// </summary>
public class VersionService : IVersionService
{
    private readonly string _version;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionService"/> class.
    /// </summary>
    public VersionService()
    {
        var assembly = Assembly.GetEntryAssembly();
        var version = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString()
            ?? "0.0.0";

        // Strip any metadata suffix (e.g., "+abc123" from semantic versioning)
        var plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
        {
            version = version[..plusIndex];
        }

        _version = $"v{version}";
    }

    /// <inheritdoc/>
    public string GetVersion() => _version;
}
