using DiscordBot.Core.Configuration;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IGuildAssistantContextFactory" />
public class GuildAssistantContextFactory : IGuildAssistantContextFactory
{
    private readonly IGuildService _guildService;
    private readonly IPromptTemplate _promptTemplate;
    private readonly IToolRegistry _toolRegistry;
    private readonly IAssistantUsageMetricsRepository _metricsRepository;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;
    private readonly ILogger<GuildAssistantContext> _logger;
    private readonly AssistantOptions _options;

    public GuildAssistantContextFactory(
        IGuildService guildService,
        IPromptTemplate promptTemplate,
        IToolRegistry toolRegistry,
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository,
        ILogger<GuildAssistantContext> logger,
        IOptions<AssistantOptions> options)
    {
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _interactionLogRepository = interactionLogRepository ?? throw new ArgumentNullException(nameof(interactionLogRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public IAssistantContext Create(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question)
    {
        return new GuildAssistantContext(
            guildId,
            channelId,
            userId,
            messageId,
            rateLimit,
            question,
            _options.Tools.EnableDocumentationTools ? _toolRegistry : null,
            _guildService,
            _promptTemplate,
            _metricsRepository,
            _interactionLogRepository,
            _options,
            _logger);
    }
}
