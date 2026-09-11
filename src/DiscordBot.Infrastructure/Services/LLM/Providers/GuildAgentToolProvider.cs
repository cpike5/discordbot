using DiscordBot.Agents.Abstractions;
using DiscordBot.Core.Enums;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// The guild assistant's individually authored tools, as one provider.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IToolProvider"/>, so the scoped <c>ToolRegistry</c> picks it up with
/// the hand-written providers. Per-guild narrowing still happens above it, at the
/// <c>FilteredToolRegistry</c> boundary.
/// </remarks>
public sealed class GuildAgentToolProvider : CataloguedAgentToolProvider
{
    /// <summary>Creates the provider over every scanned tool the catalogue puts on the guild surface.</summary>
    /// <param name="tools">Every scanned agent tool.</param>
    /// <param name="logger">Used to report a tool that has no catalogue entry.</param>
    public GuildAgentToolProvider(IEnumerable<IAgentTool> tools, ILogger<GuildAgentToolProvider> logger)
        : base(
            "AgentTools",
            "Individually registered agent tools for the guild assistant",
            tools,
            ToolScopes.Guild,
            logger)
    {
    }
}
