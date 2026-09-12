using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Contracts;
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
/// This rebuilds a surface exactly as a run would see it — the same registry, the same allow-list
/// decorator, the same skill session, the same <see cref="SkillToolSet.Compose"/> — because a
/// measurement of anything else would answer a question nobody asked. In particular it does not
/// measure <see cref="IToolRegistry.GetEnabledTools"/>: a tool behind a skill is registered, is
/// callable once the skill is loaded, and is <em>not</em> in the per-request prefix.
/// </para>
/// <para>
/// The tool registries are resolved from the container rather than injected, which is otherwise not
/// how anything here is written, for two reasons. <c>IToolRegistry</c> and
/// <c>ISkillSessionFactory</c> are registered only when an OpenRouter API key is configured, while
/// this service is registered unconditionally so the metrics page can render a plain "not
/// configured" panel instead of failing to resolve — absence is one of its answers. And each surface
/// is built only when that surface is asked about: a guild's metrics page should not construct every
/// DM tool provider to draw a guild's panel.
/// </para>
/// </remarks>
public sealed class PromptSurfaceReporter : IPromptSurfaceReporter
{
    private readonly IServiceProvider _services;
    private readonly IToolAccessResolver _toolAccess;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AssistantOptions _assistantOptions;
    private readonly DmAssistantOptions _dmOptions;
    private readonly ILogger<PromptSurfaceReporter> _logger;

    /// <summary>Creates the reporter.</summary>
    /// <param name="services">Resolves each surface's registry on demand; see the type's remarks.</param>
    /// <param name="toolAccess">Resolves a guild's tool allow-list.</param>
    /// <param name="loggerFactory">Builds the logger the composed DM registry needs.</param>
    /// <param name="assistantOptions">Guild assistant options: skills path, tool gate.</param>
    /// <param name="dmOptions">DM assistant options: skills path.</param>
    /// <param name="logger">This service's own logger.</param>
    public PromptSurfaceReporter(
        IServiceProvider services,
        IToolAccessResolver toolAccess,
        ILoggerFactory loggerFactory,
        IOptions<AssistantOptions> assistantOptions,
        IOptions<DmAssistantOptions> dmOptions,
        ILogger<PromptSurfaceReporter> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _toolAccess = toolAccess ?? throw new ArgumentNullException(nameof(toolAccess));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
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
        // The skill session factory is registered in the same API-key-gated block as every other
        // assistant service, so its absence is the cheapest honest test for "there is no assistant".
        var skillSessions = _services.GetService<ISkillSessionFactory>();

        if (skillSessions is null)
        {
            return null;
        }

        return scope switch
        {
            ToolScopes.Guild => await GuildReportAsync(skillSessions, guildId, cancellationToken),
            ToolScopes.Dm => await DmReportAsync(skillSessions, cancellationToken),
            _ => null
        };
    }

    private async Task<PromptSurfaceReport?> GuildReportAsync(
        ISkillSessionFactory skillSessions,
        ulong? guildId,
        CancellationToken cancellationToken)
    {
        var registry = _services.GetService<IToolRegistry>();

        if (registry is null)
        {
            return null;
        }

        // The same two decisions GuildAssistantContextFactory makes, in the same order: the guild's
        // allow-list first, then the skills over what it left.
        IToolRegistry effective = registry;

        if (guildId is not null && _assistantOptions.Tools.EnableDocumentationTools)
        {
            var allowed = await _toolAccess.ResolveAsync(guildId.Value, cancellationToken);
            effective = new FilteredToolRegistry(registry, allowed);
        }

        var skills = await skillSessions.CreateAsync(
            _assistantOptions.Tools.SkillsPath, effective, preActivatedKeys: null, cancellationToken);

        return Build(ToolScopes.Guild, "Guild assistant", guildId, registry, effective, skills);
    }

    private async Task<PromptSurfaceReport?> DmReportAsync(
        ISkillSessionFactory skillSessions,
        CancellationToken cancellationToken)
    {
        var providers = _services.GetServices<IDmToolProvider>().ToList();

        if (providers.Count == 0)
        {
            return null;
        }

        // Built the way DmAssistantContextFactory builds it, from the DM providers rather than the
        // guild registry - the two surfaces do not share one.
        var registry = new ToolRegistry(_loggerFactory.CreateLogger<ToolRegistry>(), providers);

        // Nothing pre-activated: this is the cost of a DM user's *first* turn, which is the one that
        // pays for skills. A user carrying activations into turn 2 pays more, and that is the
        // mechanism working rather than something a surface-level report should average away.
        var skills = await skillSessions.CreateAsync(
            _dmOptions.SkillsPath, registry, preActivatedKeys: null, cancellationToken);

        return Build(ToolScopes.Dm, "DM assistant", guildId: null, registry, registry, skills);
    }

    /// <summary>
    /// Turns a registry and its skills into the report.
    /// </summary>
    /// <param name="scope">The surface.</param>
    /// <param name="surfaceName">Its human-readable name.</param>
    /// <param name="guildId">The guild whose allow-list was applied, if any.</param>
    /// <param name="registered">Everything the surface holds, before narrowing.</param>
    /// <param name="effective">What a run would be handed - the narrowed registry.</param>
    /// <param name="skills">The run's skill session.</param>
    private PromptSurfaceReport Build(
        ToolScopes scope,
        string surfaceName,
        ulong? guildId,
        IToolRegistry registered,
        IToolRegistry effective,
        ISkillActivationState skills)
    {
        // What a request actually carries. Not GetEnabledTools(): a tool behind an unloaded skill is
        // registered and callable later, and is not in the prefix now.
        var advertised = PromptSurface.Measure(SkillToolSet.Compose(effective, skills));

        var advertisedByName = advertised.Tools.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);

        var skillsByTool = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var skill in skills.Available)
        {
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

        var rows = new List<PromptSurfaceToolRow>();
        var registeredChars = 0;

        foreach (var definition in registered.GetEnabledTools())
        {
            var chars = PromptSurface.MeasureOne(definition);
            registeredChars += chars;

            var entry = ToolCatalog.Describe(definition.Name);
            var namedBy = skillsByTool.GetValueOrDefault(definition.Name) ?? new List<string>();

            rows.Add(new PromptSurfaceToolRow(
                definition.Name,
                entry.DisplayName,
                entry.Category,
                chars,
                advertisedByName.TryGetValue(definition.Name, out var measured) ? measured.Share : 0,
                advertisedByName.ContainsKey(definition.Name),
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
}
