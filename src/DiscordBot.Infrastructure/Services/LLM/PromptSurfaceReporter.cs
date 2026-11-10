using System.Text.Json;
using cpike.Agent;
using cpike.Agent.Prompting;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.Llm;
using DiscordBot.Infrastructure.Abstractions.LLM;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IPromptSurfaceReporter" />
/// <remarks>
/// <para>
/// This measures a surface as a run would see it: the mode's tool list (<see cref="CatalogPromptStore"/>
/// builds it from <see cref="ToolCatalog"/>, without the tools that only a skill brings), minus a
/// guild's disabled tools, each measured as the wire definition the framework sends. A tool behind a
/// skill is registered and callable once the skill is loaded, and is <em>not</em> in the per-request
/// prefix, so it is reported as withheld.
/// </para>
/// <para>
/// The framework services are resolved from the container rather than injected, because they are
/// registered only when an OpenRouter API key is configured, while this service is registered
/// unconditionally so the metrics page can render a plain "not configured" panel instead of failing
/// to resolve — absence is one of its answers.
/// </para>
/// </remarks>
public sealed class PromptSurfaceReporter : IPromptSurfaceReporter
{
    private static readonly JsonSerializerOptions WireJson = new() { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    private readonly IServiceProvider _services;
    private readonly IToolAccessResolver _toolAccess;
    private readonly AssistantOptions _assistantOptions;
    private readonly DmAssistantOptions _dmOptions;
    private readonly ILogger<PromptSurfaceReporter> _logger;

    /// <summary>Creates the reporter.</summary>
    /// <param name="services">Resolves the framework's stores and tools on demand; see the type's remarks.</param>
    /// <param name="toolAccess">Resolves a guild's tool allow-list.</param>
    /// <param name="assistantOptions">Guild assistant options: the tool gate.</param>
    /// <param name="dmOptions">DM assistant options: the code-execution switch.</param>
    /// <param name="logger">This service's own logger.</param>
    public PromptSurfaceReporter(
        IServiceProvider services,
        IToolAccessResolver toolAccess,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<DmAssistantOptions> dmOptions,
        ILogger<PromptSurfaceReporter> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _toolAccess = toolAccess ?? throw new ArgumentNullException(nameof(toolAccess));
        _assistantOptions = assistantOptions?.Value ?? throw new ArgumentNullException(nameof(assistantOptions));
        _dmOptions = dmOptions?.Value ?? throw new ArgumentNullException(nameof(dmOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<PromptSurfaceReport?> ReportAsync(
        ToolScopes scope,
        ulong? guildId = null,
        CancellationToken cancellationToken = default)
    {
        // The prompt store is registered in the same API-key-gated block as every other assistant
        // service, so its absence is the cheapest honest test for "there is no assistant".
        var prompts = _services.GetService<IAgentPromptStore>();
        if (prompts is null)
        {
            return null;
        }

        var tools = _services.GetServices<IAgentTool>().ToList();
        var skills = _services.GetService<IAgentSkillStore>();

        switch (scope)
        {
            case ToolScopes.Guild:
                var disabled = await GuildDisabledToolsAsync(guildId, cancellationToken);
                return Build(scope, "Guild assistant", guildId, GuildAssistantContext.ModeName, disabled, prompts, skills, tools);
            case ToolScopes.Dm:
                return Build(scope, "DM assistant", guildId: null, DmAssistantContext.ModeName, DmAssistantContext.DisabledToolsFor(_dmOptions), prompts, skills, tools);
            default:
                return null;
        }
    }

    /// <summary>The same decision <see cref="GuildAssistantContextFactory"/> makes for a run.</summary>
    private async Task<IReadOnlySet<string>> GuildDisabledToolsAsync(ulong? guildId, CancellationToken cancellationToken)
    {
        var guildTools = ToolCatalog.ForScope(ToolScopes.Guild).Select(entry => entry.Name);
        if (!_assistantOptions.Tools.EnableDocumentationTools)
        {
            return guildTools.ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (guildId is null)
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var allowed = await _toolAccess.ResolveAsync(guildId.Value, cancellationToken);
        return guildTools.Where(name => !allowed.Contains(name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private PromptSurfaceReport? Build(
        ToolScopes scope,
        string surfaceName,
        ulong? guildId,
        string modeKey,
        IReadOnlyCollection<string> disabled,
        IAgentPromptStore prompts,
        IAgentSkillStore? skillStore,
        IReadOnlyList<IAgentTool> tools)
    {
        if (!prompts.TryGet(modeKey, out var mode))
        {
            return null;
        }

        var disabledSet = new HashSet<string>(disabled, StringComparer.OrdinalIgnoreCase);
        var modeTools = new HashSet<string>(mode.Tools, StringComparer.OrdinalIgnoreCase);

        var skillsByTool = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var key in mode.Skills)
        {
            if (skillStore is null || !skillStore.TryGet(key, out var skill))
            {
                continue;
            }

            foreach (var tool in skill.Tools)
            {
                if (!skillsByTool.TryGetValue(tool, out var keys))
                {
                    keys = new List<string>();
                    skillsByTool[tool] = keys;
                }

                keys.Add(skill.Key);
            }
        }

        // Everything this surface holds: the mode's tools plus what its skills can bring.
        var surfaceTools = tools
            .Where(t => modeTools.Contains(t.Definition.Name!) || skillsByTool.ContainsKey(t.Definition.Name!))
            .OrderBy(t => t.Definition.Name, StringComparer.Ordinal)
            .ToList();

        // What a request actually carries: the mode's list, minus what this guild disabled.
        var advertisedTools = surfaceTools
            .Where(t => modeTools.Contains(t.Definition.Name!) && !disabledSet.Contains(t.Definition.Name!))
            .ToList();
        var advertised = Measure(advertisedTools);
        var advertisedByName = advertised.Tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var rows = new List<PromptSurfaceToolRow>();
        var registeredChars = 0;
        foreach (var tool in surfaceTools)
        {
            var name = tool.Definition.Name!;
            var chars = MeasureOne(tool);
            registeredChars += chars;

            var entry = ToolCatalog.Describe(name);
            var namedBy = skillsByTool.GetValueOrDefault(name) ?? new List<string>();

            rows.Add(new PromptSurfaceToolRow(
                name,
                entry.DisplayName,
                entry.Category,
                chars,
                advertisedByName.TryGetValue(name, out var measured) ? measured.Share : 0,
                advertisedByName.ContainsKey(name),
                namedBy.Count > 0,
                namedBy));
        }

        _logger.LogDebug(
            "{SurfaceName} advertises {AdvertisedCount} of {RegisteredCount} tools, {Chars} chars",
            surfaceName, advertised.ToolCount, rows.Count, advertised.TotalChars);

        return new PromptSurfaceReport
        {
            Scope = scope,
            SurfaceName = surfaceName,
            GuildId = guildId,
            Advertised = advertised,
            Tools = rows
                .OrderByDescending(r => r.SchemaChars)
                .ThenBy(r => r.Name, StringComparer.Ordinal)
                .ToList(),
            RegisteredChars = registeredChars
        };
    }

    /// <summary>Characters one tool's wire definition serializes to.</summary>
    private static int MeasureOne(IAgentTool tool) =>
        JsonSerializer.Serialize(cpike.Agent.Tools.ToolWire.ToDefinition(tool.Definition), WireJson).Length;

    /// <summary>The cost of sending <paramref name="tools"/> as one tool array.</summary>
    private static PromptSurfaceMeasurement Measure(IReadOnlyList<IAgentTool> tools)
    {
        if (tools.Count == 0)
        {
            return PromptSurfaceMeasurement.Empty;
        }

        var sizes = tools.Select(t => (Name: t.Definition.Name!, Chars: MeasureOne(t))).ToList();

        // The array's brackets and separating commas belong to no tool but are part of the total.
        var total = sizes.Sum(s => s.Chars) + 2 + (sizes.Count - 1);
        var measured = sizes
            .Select(s => new PromptSurfaceTool(s.Name, s.Chars, (double)s.Chars / total))
            .ToList();

        return new PromptSurfaceMeasurement(measured, total, total / 4);
    }
}
