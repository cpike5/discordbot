using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.Contracts.Enums;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Agents;

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
    private readonly ILlmModelResolver _modelResolver;
    private readonly DmAssistantOptions _options;
    private readonly ILlmUsageRecorder _usageRecorder;
    private readonly ISkillSessionFactory _skillSessions;
    private readonly IDmSkillActivationStore _skillActivations;
    private readonly ISettingsService _settingsService;

    public DmAssistantContextFactory(
        IEnumerable<IDmToolProvider> dmToolProviders,
        ILoggerFactory loggerFactory,
        IPromptTemplate promptTemplate,
        IDmConversationMessageRepository conversationRepo,
        IDmAssistantInteractionLogRepository interactionLogRepo,
        IDmAssistantUsageMetricsRepository metricsRepo,
        IMemoryCache memoryCache,
        ILlmModelResolver modelResolver,
        IOptions<DmAssistantOptions> options,
        ILlmUsageRecorder usageRecorder,
        ISkillSessionFactory skillSessions,
        IDmSkillActivationStore skillActivations,
        ISettingsService settingsService)
    {
        _dmToolProviders = dmToolProviders ?? throw new ArgumentNullException(nameof(dmToolProviders));
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _conversationRepo = conversationRepo ?? throw new ArgumentNullException(nameof(conversationRepo));
        _interactionLogRepo = interactionLogRepo ?? throw new ArgumentNullException(nameof(interactionLogRepo));
        _metricsRepo = metricsRepo ?? throw new ArgumentNullException(nameof(metricsRepo));
        _memoryCache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        _modelResolver = modelResolver ?? throw new ArgumentNullException(nameof(modelResolver));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _usageRecorder = usageRecorder ?? throw new ArgumentNullException(nameof(usageRecorder));
        _skillSessions = skillSessions ?? throw new ArgumentNullException(nameof(skillSessions));
        _skillActivations = skillActivations ?? throw new ArgumentNullException(nameof(skillActivations));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
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

        var resolved = await _modelResolver.ResolveAsync(LlmMode.DmAssistant, cancellationToken);

        // Replaying the previous turn's activations is what makes a skill nearly free from turn 2:
        // its tools are advertised on the first call and its instructions are already in the prompt,
        // so the round that loaded it is never paid for twice.
        var skills = await _skillSessions.CreateAsync(
            _options.SkillsPath, toolRegistry, _skillActivations.Get(userId), cancellationToken);

        var customInstructions = await LoadCustomInstructionsAsync(cancellationToken);

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
            _loggerFactory.CreateLogger<DmAssistantContext>(),
            resolved.Slug,
            resolved.Pricing,
            _usageRecorder,
            skills,
            _skillActivations,
            customInstructions);
    }

    /// <summary>
    /// The owner's custom instructions from the admin Settings page (Features tab), or null when
    /// none are set. A failed read costs the turn its custom style, not the reply.
    /// </summary>
    private async Task<string?> LoadCustomInstructionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await _settingsService.GetSettingValueAsync<string>(
                SettingDefinitions.DmAssistantCustomInstructionsKey, cancellationToken);
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _loggerFactory.CreateLogger<DmAssistantContextFactory>()
                .LogWarning(ex, "Failed to load DM assistant custom instructions; continuing without them");
            return null;
        }
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
