using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Llm;
using DiscordBot.Core.Entities;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.Contracts.Enums;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DiscordBot.Infrastructure.Abstractions.LLM;

namespace DiscordBot.Infrastructure.Services.LLM;

/// <inheritdoc cref="IGuildAssistantContextFactory" />
public class GuildAssistantContextFactory : IGuildAssistantContextFactory
{
    private readonly IGuildService _guildService;
    private readonly IPromptTemplate _promptTemplate;
    private readonly IToolRegistry _toolRegistry;
    private readonly IAssistantUsageMetricsRepository _metricsRepository;
    private readonly IAssistantInteractionLogRepository _interactionLogRepository;
    private readonly ILlmModelResolver _modelResolver;
    private readonly IToolAccessResolver _toolAccess;
    private readonly ILogger<GuildAssistantContext> _logger;
    private readonly AssistantOptions _options;
    private readonly ILlmUsageRecorder _usageRecorder;
    private readonly ISkillSessionFactory _skillSessions;
    private readonly IAssistantThreadRepository? _threads;
    private readonly IAssistantThreadMessageRepository? _threadMessages;

    public GuildAssistantContextFactory(
        IGuildService guildService,
        IPromptTemplate promptTemplate,
        IToolRegistry toolRegistry,
        IAssistantUsageMetricsRepository metricsRepository,
        IAssistantInteractionLogRepository interactionLogRepository,
        ILlmModelResolver modelResolver,
        IToolAccessResolver toolAccess,
        ILogger<GuildAssistantContext> logger,
        IOptions<AssistantOptions> options,
        ILlmUsageRecorder usageRecorder,
        ISkillSessionFactory skillSessions,
        IAssistantThreadRepository? threads = null,
        IAssistantThreadMessageRepository? threadMessages = null)
    {
        _guildService = guildService ?? throw new ArgumentNullException(nameof(guildService));
        _promptTemplate = promptTemplate ?? throw new ArgumentNullException(nameof(promptTemplate));
        _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
        _metricsRepository = metricsRepository ?? throw new ArgumentNullException(nameof(metricsRepository));
        _interactionLogRepository = interactionLogRepository ?? throw new ArgumentNullException(nameof(interactionLogRepository));
        _modelResolver = modelResolver ?? throw new ArgumentNullException(nameof(modelResolver));
        _toolAccess = toolAccess ?? throw new ArgumentNullException(nameof(toolAccess));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _usageRecorder = usageRecorder ?? throw new ArgumentNullException(nameof(usageRecorder));
        _skillSessions = skillSessions ?? throw new ArgumentNullException(nameof(skillSessions));
        // Optional so a test or a host without thread support builds single-reply contexts only
        _threads = threads;
        _threadMessages = threadMessages;
    }

    /// <inheritdoc />
    public Task<IAssistantContext> CreateAsync(
        ulong guildId,
        ulong channelId,
        ulong userId,
        ulong messageId,
        int rateLimit,
        string question,
        bool callerCanMutate = false,
        CancellationToken cancellationToken = default)
        => CreateAsync(
            GuildAssistantRequest.SingleReply(guildId, channelId, userId, messageId, question, callerCanMutate),
            rateLimit,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IAssistantContext> CreateAsync(
        GuildAssistantRequest request,
        int rateLimit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var guildId = request.GuildId;
        var resolved = await _modelResolver.ResolveAsync(LlmMode.GuildAssistant, cancellationToken);

        // A thread turn: the thread row proves it is ours and carries the loaded skills; the recent
        // turns seed the run. A request naming a thread this host cannot see (no repositories, or
        // the row was retained away) is answered as a single reply rather than refused.
        AssistantThread? thread = null;
        var history = new List<LlmMessage>();
        if (request.ThreadId is { } threadId && _threads is not null && _threadMessages is not null)
        {
            thread = await _threads.GetByThreadIdAsync(threadId, cancellationToken);
            if (thread is not null)
            {
                var recent = await _threadMessages.GetRecentByThreadAsync(
                    threadId, _options.Threads.MaxConversationMessages, cancellationToken);
                history = recent
                    .Select(m => new LlmMessage
                    {
                        Role = m.Role == "assistant" ? LlmRole.Assistant : LlmRole.User,
                        Content = m.Content
                    })
                    .ToList();
            }
            else
            {
                _logger.LogDebug("Thread {ThreadId} has no assistant thread row; answering as a single reply", threadId);
            }
        }

        // Resolved once per run, then applied as a decorator: the guild's allow-list is shared by
        // every caller in the guild, so narrowing here still leaves one prompt-cache prefix per
        // guild rather than one per user.
        IToolRegistry? registry = null;
        if (_options.Tools.EnableDocumentationTools)
        {
            var allowed = await _toolAccess.ResolveAsync(guildId, cancellationToken);
            registry = new FilteredToolRegistry(_toolRegistry, allowed);
        }

        // Built from the narrowed registry, so a skill can only ever un-hide a tool this guild is
        // already allowed. A single reply pre-activates nothing: there is no previous turn to
        // replay, so a skill costs a round every time. A thread turn replays what the thread has
        // loaded, which is what makes a skill cost one round per conversation there.
        var skills = await _skillSessions.CreateAsync(
            _options.Tools.SkillsPath, registry, thread?.GetActiveSkillsList(), cancellationToken);

        return new GuildAssistantContext(
            guildId,
            request.ChannelId,
            request.UserId,
            request.MessageId,
            rateLimit,
            request.Question,
            request.CallerCanMutate,
            registry,
            _guildService,
            _promptTemplate,
            _metricsRepository,
            _interactionLogRepository,
            _options,
            _logger,
            resolved.Slug,
            resolved.Pricing,
            _usageRecorder,
            skills,
            thread,
            history,
            _threads,
            _threadMessages);
    }
}
