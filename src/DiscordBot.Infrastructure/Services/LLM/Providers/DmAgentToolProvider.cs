using DiscordBot.Agents.Abstractions;
using DiscordBot.Core.Enums;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// The DM assistant's individually authored tools, as one provider.
/// </summary>
/// <remarks>
/// Registered as an <see cref="IDmToolProvider"/>, which is what keeps DM-only tools out of the
/// guild assistant's registry — the same separation the hand-written DM providers use.
/// </remarks>
public sealed class DmAgentToolProvider : CataloguedAgentToolProvider, IDmToolProvider
{
    /// <summary>Creates the provider over every scanned tool the catalogue puts on the DM surface.</summary>
    /// <param name="tools">Every scanned agent tool.</param>
    /// <param name="logger">Used to report a tool that has no catalogue entry.</param>
    public DmAgentToolProvider(IEnumerable<IAgentTool> tools, ILogger<DmAgentToolProvider> logger)
        : base(
            "DmAgentTools",
            "Individually registered agent tools for the DM assistant",
            tools,
            ToolScopes.Dm,
            logger)
    {
    }
}
