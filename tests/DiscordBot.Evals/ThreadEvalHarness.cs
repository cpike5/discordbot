using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Configuration;
using DiscordBot.Agents.OpenRouter;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.DTOs.Llm;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Evals;

/// <summary>
/// A guild assistant thread, turn by turn, through the real context factory, the real pipeline and
/// the real repositories over a throwaway database. What is faked is only what an eval cannot have:
/// the Discord client (the guild name comes from a stub) and the tool set (none, so the run is about
/// the conversation, not the tools).
/// </summary>
public sealed class ThreadEvalHarness : IDisposable
{
    public const ulong GuildId = 7100;
    public const ulong ThreadId = 7777;
    public const ulong UserId = 4242;

    private readonly TestDatabase _database;
    private readonly BotDbContext _context;
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly GuildAssistantContextFactory _factory;
    private readonly AssistantMessagePipeline _pipeline;

    /// <summary>Creates the harness with an open thread already seeded.</summary>
    public ThreadEvalHarness()
    {
        _database = PostgresTestServer.CreateDatabase();
        _context = _database.CreateContext();
        _cache = new MemoryCache(new MemoryCacheOptions());

        _context.Guilds.Add(new Guild { Id = GuildId, Name = "Eval Guild", JoinedAt = DateTime.UtcNow });
        _context.Users.Add(new User { Id = UserId });
        _context.AssistantThreads.Add(new AssistantThread
        {
            ThreadId = ThreadId,
            GuildId = GuildId,
            ParentChannelId = 55,
            StarterUserId = UserId,
            CreatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        });
        _context.SaveChanges();

        var options = new AssistantOptions
        {
            GloballyEnabled = true,
            Tools = new() { EnableDocumentationTools = false, AgentPromptPath = EvalPaths.GuildAgentPrompt, SkillsPath = EvalPaths.GuildSkillsDirectory },
            Sampling = new() { MaxTokens = 400, Temperature = 0 },
            Cost = new() { EnableCostTracking = false },
            Privacy = new() { LogInteractions = true },
            Threads = new() { MaxConversationMessages = 20 }
        };

        var guildService = new Mock<IGuildService>();
        guildService.Setup(g => g.GetGuildByIdAsync(GuildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildDto { Id = GuildId, Name = "Eval Guild" });

        var resolver = new Mock<ILlmModelResolver>();
        resolver.Setup(r => r.ResolveAsync(It.IsAny<LlmMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = EvalSettings.Model, ConfiguredSlug = EvalSettings.Model, Source = LlmModelResolutionSource.Configuration });

        var toolAccess = new Mock<IToolAccessResolver>();
        toolAccess.Setup(r => r.ResolveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>());

        var promptTemplate = new PromptTemplate(NullLogger<PromptTemplate>.Instance, _cache);
        var skillLibrary = new SkillLibrary(promptTemplate, _cache, NullLogger<SkillLibrary>.Instance);

        _factory = new GuildAssistantContextFactory(
            guildService.Object,
            promptTemplate,
            new ToolRegistry(NullLogger<ToolRegistry>.Instance, Array.Empty<IToolProvider>()),
            new AssistantUsageMetricsRepository(_context, NullLogger<AssistantUsageMetricsRepository>.Instance, NullLogger<Repository<AssistantUsageMetrics>>.Instance),
            new AssistantInteractionLogRepository(_context, NullLogger<AssistantInteractionLogRepository>.Instance, NullLogger<Repository<AssistantInteractionLog>>.Instance),
            resolver.Object,
            toolAccess.Object,
            NullLogger<GuildAssistantContext>.Instance,
            Options.Create(options),
            Mock.Of<ILlmUsageRecorder>(),
            new SkillSessionFactory(skillLibrary, NullLogger<SkillSessionFactory>.Instance),
            new AssistantThreadRepository(_context, NullLogger<AssistantThreadRepository>.Instance, NullLogger<Repository<AssistantThread>>.Instance),
            new AssistantThreadMessageRepository(_context, NullLogger<AssistantThreadMessageRepository>.Instance, NullLogger<Repository<AssistantThreadMessage>>.Instance));

        _http = new HttpClient
        {
            BaseAddress = new Uri(EvalSettings.BaseUrl.EndsWith('/') ? EvalSettings.BaseUrl : EvalSettings.BaseUrl + "/"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        var runner = new AgentRunner(
            new OpenRouterLlmClient(
                _http,
                Options.Create(new OpenRouterOptions { ApiKey = EvalSettings.ApiKey ?? string.Empty, BaseUrl = EvalSettings.BaseUrl, DefaultModel = EvalSettings.Model }),
                new OpenRouterParameterSupportCache(),
                NullLogger<OpenRouterLlmClient>.Instance),
            NullLogger<AgentRunner>.Instance);

        _pipeline = new AssistantMessagePipeline(runner);
    }

    /// <summary>
    /// One turn of the thread, exactly as <c>AssistantService</c> runs it: build the context, format
    /// the message, run the pipeline, record the turn.
    /// </summary>
    public async Task<ThreadTurn> TurnAsync(string question, CancellationToken cancellationToken = default)
    {
        var request = new GuildAssistantRequest(GuildId, ThreadId, ParentChannelId: 55, ThreadId, UserId, MessageId: 1, question);
        var context = await _factory.CreateAsync(request, rateLimit: 100, cancellationToken);
        var seeded = context.ConversationHistory.Count;

        var formatted = await context.FormatUserMessageAsync(question, cancellationToken);
        var result = await _pipeline.RunAsync(formatted, context, cancellationToken);
        await context.RecordUsageAsync(question, result, cancellationToken);

        return new ThreadTurn(result, seeded);
    }

    /// <summary>The thread's saved turns, oldest first, straight from the database.</summary>
    public async Task<IReadOnlyList<AssistantThreadMessage>> SavedTurnsAsync() =>
        await _context.AssistantThreadMessages.AsNoTracking().Where(m => m.ThreadId == ThreadId).OrderBy(m => m.Id).ToListAsync();

    /// <summary>The thread row, re-read.</summary>
    public async Task<AssistantThread> ThreadAsync() =>
        await _context.AssistantThreads.AsNoTracking().SingleAsync(t => t.ThreadId == ThreadId);

    /// <summary>The interaction log rows this thread produced.</summary>
    public async Task<IReadOnlyList<AssistantInteractionLog>> LogsAsync() =>
        await _context.AssistantInteractionLogs.AsNoTracking().Where(l => l.ThreadId == ThreadId).OrderBy(l => l.Id).ToListAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        _http.Dispose();
        _context.Dispose();
        _database.Dispose();
        _cache.Dispose();
    }
}

/// <summary>What one turn produced.</summary>
/// <param name="Result">The pipeline's result.</param>
/// <param name="HistoryMessagesSeeded">How many earlier turns the run was seeded with.</param>
public sealed record ThreadTurn(AssistantPipelineResult Result, int HistoryMessagesSeeded);
