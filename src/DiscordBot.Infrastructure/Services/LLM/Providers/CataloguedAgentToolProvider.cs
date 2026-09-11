using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Models.Llm;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Infrastructure.Services.LLM.Providers;

/// <summary>
/// Base for this bot's <see cref="AgentToolProvider"/> surfaces: takes every scanned
/// <see cref="IAgentTool"/> and keeps the ones <see cref="ToolCatalog"/> puts on this surface.
/// </summary>
/// <remarks>
/// <para>
/// The catalogue is what decides where a tool is advertised. That makes an entry part of writing a
/// tool rather than a rule alongside it: a tool nobody catalogued has
/// <see cref="ToolScopes.None"/>, reaches no surface, and is advertised nowhere — logged as a
/// warning rather than left to be discovered by its absence.
/// </para>
/// <para>
/// The eleven hand-written <see cref="IToolProvider"/>s are unaffected; they declare their surface
/// by which interface they are registered under, as they always have. This applies only to tools
/// authored as <see cref="IAgentTool"/>.
/// </para>
/// </remarks>
public abstract class CataloguedAgentToolProvider : AgentToolProvider
{
    /// <summary>
    /// Filters <paramref name="tools"/> to <paramref name="scope"/> and refuses forbidden writes in
    /// this bot's voice.
    /// </summary>
    /// <param name="name">Provider name, as it appears in the registry and in tool spans.</param>
    /// <param name="description">Human-readable description of the group.</param>
    /// <param name="tools">Every scanned agent tool; the constructor keeps the ones in scope.</param>
    /// <param name="scope">The surface this provider serves.</param>
    /// <param name="logger">Used to report a tool that has no catalogue entry.</param>
    protected CataloguedAgentToolProvider(
        string name,
        string description,
        IEnumerable<IAgentTool> tools,
        ToolScopes scope,
        ILogger logger)
        : base(name, description, ForScope(tools, scope, name, logger), ToolPermissions.MutationForbidden)
    {
    }

    /// <summary>
    /// The tools the catalogue advertises on <paramref name="scope"/>, warning about any it does not
    /// know at all.
    /// </summary>
    private static IReadOnlyList<IAgentTool> ForScope(
        IEnumerable<IAgentTool> tools,
        ToolScopes scope,
        string providerName,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(tools);
        ArgumentNullException.ThrowIfNull(logger);

        var inScope = new List<IAgentTool>();

        foreach (var tool in tools)
        {
            var toolName = tool.Definition.Name;

            if (!ToolCatalog.IsCatalogued(toolName))
            {
                logger.LogWarning(
                    "Agent tool {ToolName} ({ToolType}) has no ToolCatalog entry, so it is advertised "
                    + "on no assistant surface. Add one in Core/Models/Llm/ToolCatalog.cs",
                    toolName,
                    tool.GetType().Name);
                continue;
            }

            if ((ToolCatalog.Describe(toolName).Scopes & scope) != 0)
            {
                inScope.Add(tool);
            }
        }

        logger.LogDebug(
            "{ProviderName} advertises {ToolCount} catalogued tools on {Scope}",
            providerName, inScope.Count, scope);

        return inScope;
    }
}
