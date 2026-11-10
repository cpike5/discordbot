using cpike.Agent;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IDmAssistantContextFactory" />
public class DmAssistantContextFactory : IDmAssistantContextFactory
{
    /// <summary>Cache key prefix for the active-guild-per-user selection set by the set_active_guild tool.</summary>
    public const string ActiveGuildCacheKeyPrefix = "dm_active_guild:";

    private readonly ILoggerFactory _loggerFactory;
    private readonly IDmConversationMessageRepository _conversationRepo;
    private readonly IDmAssistantInteractionLogRepository _interactionLogRepo;
    private readonly IDmAssistantUsageMetricsRepository _metricsRepo;
    private readonly IMemoryCache _memoryCache;
    private readonly ILlmModelResolver _modelResolver;
    private readonly DmAssistantOptions _options;
    private readonly ILlmUsageRecorder _usageRecorder;
    private readonly IDmSkillActivationStore _skillActivations;

    public DmAssistantContextFactory(
        ILoggerFactory loggerFactory,
        IDmConversationMessageRepository conversationRepo,
        IDmAssistantInteractionLogRepository interactionLogRepo,
        IDmAssistantUsageMetricsRepository metricsRepo,
        IMemoryCache memoryCache,
        ILlmModelResolver modelResolver,
        IOptions<DmAssistantOptions> options,
        ILlmUsageRecorder usageRecorder,
        IDmSkillActivationStore skillActivations)
    {
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _conversationRepo = conversationRepo ?? throw new ArgumentNullException(nameof(conversationRepo));
        _interactionLogRepo = interactionLogRepo ?? throw new ArgumentNullException(nameof(interactionLogRepo));
        _metricsRepo = metricsRepo ?? throw new ArgumentNullException(nameof(metricsRepo));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _modelResolver = modelResolver ?? throw new ArgumentNullException(nameof(modelResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _usageRecorder = usageRecorder ?? throw new ArgumentNullException(nameof(usageRecorder));
        _skillActivations = skillActivations ?? throw new ArgumentNullException(nameof(skillActivations));
    }

    /// <inheritdoc />
    public async Task<IAssistantContext> CreateAsync(ulong userId, ulong? activeGuildId, CancellationToken cancellationToken)
    {
        activeGuildId ??= _memoryCache.Get<ulong?>(ActiveGuildCacheKeyPrefix + userId);

        var history = await _conversationRepo.GetRecentByUserAsync(
            userId, _options.MaxConversationMessages, cancellationToken);

        var conversationHistory = history
            .Select(h => new ConversationTurn(IsUser: h.Role != "assistant", h.Content))
            .ToList();

        var resolved = await _modelResolver.ResolveAsync(LlmMode.DmAssistant, cancellationToken);

        return new DmAssistantContext(
            userId,
            activeGuildId,
            conversationHistory,
            _skillActivations.Get(userId),
            _conversationRepo,
            _interactionLogRepo,
            _metricsRepo,
            _options,
            _loggerFactory.CreateLogger<DmAssistantContext>(),
            resolved.Slug,
            resolved.Pricing,
            _usageRecorder,
            _skillActivations);
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
}
