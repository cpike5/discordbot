using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.DTOs.LLM.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IDmAssistantContextFactory" />
public class DmAssistantContextFactory : IDmAssistantContextFactory
{
    /// <summary>Cache key prefix for the active-guild-per-user selection set by the set_active_guild tool.</summary>
    public const string ActiveGuildCacheKeyPrefix = "dm_active_guild:";

    private readonly IEnumerable<IDmToolProvider> _dmToolProviders;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IPromptTemplate _promptTemplate;
    private readonly IDmConversationMessageRepository _conversationRepo;
    private readonly IDmAssistantInteractionLogRepository _interactionLogRepo;
    private readonly IDmAssistantUsageMetricsRepository _metricsRepo;
    private readonly IMemoryCache _memoryCache;
    private readonly DmAssistantOptions _options;

    public DmAssistantContextFactory(
        IEnumerable<IDmToolProvider> dmToolProviders,
        ILoggerFactory loggerFactory,
        IPromptTemplate promptTemplate,
        IDmConversationMessageRepository conversationRepo,
        IDmAssistantInteractionLogRepository interactionLogRepo,
        IDmAssistantUsageMetricsRepository metricsRepo,
        IMemoryCache memoryCache,
        IOptions<DmAssistantOptions> options)
    {
        _dmToolProviders = dmToolProviders ?? throw new ArgumentNullException(nameof(dmToolProviders));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _conversationRepo = conversationRepo ?? throw new ArgumentNullException(nameof(conversationRepo));
        _interactionLogRepo = interactionLogRepo ?? throw new ArgumentNullException(nameof(interactionLogRepo));
        _metricsRepo = metricsRepo ?? throw new ArgumentNullException(nameof(metricsRepo));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public async Task<IAssistantContext> CreateAsync(ulong userId, ulong? activeGuildId, CancellationToken cancellationToken)
    {
        activeGuildId ??= _memoryCache.Get<ulong?>(ActiveGuildCacheKeyPrefix + userId);

        var toolRegistry = BuildDmToolRegistry();

        var history = await _conversationRepo.GetRecentByUserAsync(
            userId, _options.MaxConversationMessages, cancellationToken);

        var conversationHistory = history
            .Select(h => new LlmMessage
            {
                Role = h.Role == "assistant" ? LlmRole.Assistant : LlmRole.User,
                Content = h.Content
            })
            .ToList();

        return new DmAssistantContext(
            userId,
            activeGuildId,
            toolRegistry,
            conversationHistory,
            _promptTemplate,
            _conversationRepo,
            _interactionLogRepo,
            _metricsRepo,
            _options,
            _loggerFactory.CreateLogger<DmAssistantContext>());
    }

    /// <inheritdoc />
    public async Task LogPlaceholderInteractionAsync(
        ulong userId, string message, string placeholderResponse, int latencyMs, CancellationToken cancellationToken)
    {
        if (!_options.LogInteractions)
        {
            return;
        }

        try
        {
            var log = new Core.Entities.DmAssistantInteractionLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = userId,
                IsOwner = false,
                Message = message.Length > 2000 ? message[..2000] : message,
                Response = placeholderResponse.Length > 2000 ? placeholderResponse[..2000] : placeholderResponse,
                Success = true,
                LatencyMs = latencyMs
            };

            await _interactionLogRepo.AddAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            _loggerFactory.CreateLogger<DmAssistantContextFactory>()
                .LogError(ex, "Failed to log DM assistant placeholder interaction for user {UserId}", userId);
        }
    }

    private IToolRegistry BuildDmToolRegistry()
    {
        var registry = new ToolRegistry(
            _loggerFactory.CreateLogger<ToolRegistry>(),
            Enumerable.Empty<IToolProvider>());

        foreach (var provider in _dmToolProviders)
        {
            registry.RegisterProvider(provider);
        }

        return registry;
    }
}
