using System.Diagnostics;

namespace DiscordBot.Agents;

/// <summary>
/// The agent engine's own <see cref="System.Diagnostics.ActivitySource"/>.
/// </summary>
/// <remarks>
/// The engine is a leaf library and cannot reach the host's tracing types, so it owns its source
/// and the host subscribes to it by name. Registered in the bot's OpenTelemetry setup alongside
/// the other sources.
/// </remarks>
public static class AgentsActivitySource
{
    /// <summary>The source name the host subscribes to.</summary>
    public const string SourceName = "DiscordBot.Agents";

    /// <summary>The shared source instance for engine spans.</summary>
    public static readonly ActivitySource Instance = new(SourceName);
}
