using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Core.Models.Llm;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IGuildAssistantContextFactory" />
public class GuildAssistantContextFactory : IGuildAssistantContextFactory
{
    private readonly IGuildService _guildService;
    private readonly IAssistantUsageMetricsRepository _metricsRepository;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;
    private readonly ILlmModelResolver _modelResolver;
    private readonly IToolAccessResolver _toolAccess;
    private readonly ILogger<GuildAssistantContext> _logger;
    private readonly AssistantOptions _options;
    private readonly ILlmUsageRecorder _usageRecorder;

    public GuildAssistantContextFactory(
        IGuildService guildService,
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository,
        ILlmModelResolver modelResolver,
        IToolAccessResolver toolAccess,
        ILogger<GuildAssistantContext> logger,
        IOptions<AssistantOptions> options,
        ILlmUsageRecorder usageRecorder)
    {
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _interactionLogRepository = interactionLogRepository ?? throw new ArgumentNullException(nameof(interactionLogRepository));
        _modelResolver = modelResolver ?? throw new ArgumentNullException(nameof(modelResolver));
        _toolAccess = toolAccess ?? throw new ArgumentNullException(nameof(toolAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _usageRecorder = usageRecorder ?? throw new ArgumentNullException(nameof(usageRecorder));
    }

    /// <inheritdoc />
    public async Task<IAssistantContext> CreateAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question,
        bool callerCanMutate = false,
        CancellationToken cancellationToken = default)
    {
        var resolved = await _modelResolver.ResolveAsync(LlmMode.GuildAssistant, cancellationToken);
        var disabledTools = await DisabledToolsAsync(guildId, cancellationToken);

        return new GuildAssistantContext(
            guildId,
            channelId,
            userId,
            messageId,
            rateLimit,
            question,
            callerCanMutate,
            disabledTools,
            _guildService,
            _metricsRepository,
            _interactionLogRepository,
            _options,
            _logger,
            resolved.Slug,
            resolved.Pricing,
            _usageRecorder);
    }

    /// <summary>
    /// The guild tools this run may not use: those the guild's allow-list leaves out, or all of them
    /// when the tools are switched off. The allow-list is shared by every caller in the guild, so this
    /// still leaves one prompt-cache prefix per guild rather than one per user.
    /// </summary>
    private async Task<IReadOnlyCollection<string>> DisabledToolsAsync(ulong guildId, CancellationToken cancellationToken)
    {
        var guildTools = ToolCatalog.ForScope(ToolScopes.Guild).Select(entry => entry.Name);
        if (!_options.Tools.EnableDocumentationTools)
        {
            return guildTools.ToList();
        }

        var allowed = await _toolAccess.ResolveAsync(guildId, cancellationToken);
        return guildTools.Where(name => !allowed.Contains(name)).ToList();
    }
}
