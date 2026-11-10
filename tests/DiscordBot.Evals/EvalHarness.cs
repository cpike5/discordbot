using System.Runtime.CompilerServices;
using System.Text.Json;
using cpike.Agent;
using cpike.Agent.OpenRouter;
using cpike.Agent.Prompting;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using DiscordBot.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Evals;

/// <summary>
/// One eval run: a real agent loop, a real OpenRouter call, real tools over a real database.
/// </summary>
/// <remarks>
/// <para>
/// What is real here matters more than what is convenient. The loop is cpike.Agent's
/// <see cref="AgentService"/>, the client is the framework's OpenRouter client, the run goes through
/// the shipped <see cref="AssistantMessagePipeline"/> in the <c>dm</c> mode, and the prompts, skills
/// and tool list come from the shipped files through <see cref="CatalogPromptStore"/> — so the
/// catalogue routing, the edit refusal, the skill roster and the result conventions are all the
/// production ones. What is faked is only what an eval cannot have: the database is a throwaway
/// PostgreSQL database and there is no Discord client, so the tools that need one are not
/// registered and the dm mode simply runs without them.
/// </para>
/// <para>
/// The skill tools are the exception. The framework leaves a skill off the roster when none of its
/// tools is registered, and the analytics and moderation tools need services an eval has no data
/// for, so each is registered as a stand-in with the shipped definition — the schema the model sees
/// is the production one — that answers "no data". The skill still loads and its instructions still
/// arrive, which is the part being measured.
/// </para>
/// <para>
/// Nothing asserts on what the reply <em>says</em>. A model claiming it saved the note is the
/// failure mode these tests exist to catch, so the facts checked are the ones a model cannot talk
/// its way around: which tools were called, and what is in the database afterwards.
/// </para>
/// </remarks>
public sealed class EvalHarness : IDisposable
{
    /// <summary>The user every eval runs as.</summary>
    public const ulong UserId = 4242;

    /// <summary>The skill tools stood in for; see the type's remarks.</summary>
    private static readonly Type[] StandInToolTypes =
    [
        typeof(GetModerationCasesTool),
        typeof(GetUserModHistoryTool),
        typeof(GetServerActivitySummaryTool),
        typeof(GetCommandAnalyticsTool),
    ];

    private readonly TestDatabase _database;
    private readonly BotDbContext _context;
    private readonly ServiceProvider _services;

    /// <summary>Creates the harness.</summary>
    public EvalHarness()
    {
        _database = PostgresTestServer.CreateDatabase();
        _context = _database.CreateContext();

        Notes = new DmAssistantNoteRepository(
            _context,
            NullLogger<DmAssistantNoteRepository>.Instance,
            NullLogger<Repository<DmAssistantNote>>.Instance);

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OpenRouter:ApiKey"] = EvalSettings.ApiKey ?? string.Empty,
                ["OpenRouter:BaseUrl"] = EvalSettings.BaseUrl,
                ["OpenRouter:Model"] = EvalSettings.Model,
                ["OpenRouter:AppTitle"] = "DiscordBot evals",
            })
            .Build();

        var promptOptions = Options.Create(new PromptOptions
        {
            Directory = EvalPaths.PromptsDirectory,
            SkillsDirectory = EvalPaths.SkillsDirectory,
            DefaultAgent = "bot",
            WatchForChanges = false,
        });

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<IConfiguration>(configuration);
        services.AddOpenRouter(configuration);

        var skills = new MarkdownSkillStore(promptOptions, null, NullLogger<MarkdownSkillStore>.Instance);
        services.AddSingleton<IAgentSkillStore>(skills);
        services.AddSingleton<IAgentPromptStore>(new CatalogPromptStore(
            new MarkdownAgentPromptStore(promptOptions, null, NullLogger<MarkdownAgentPromptStore>.Instance),
            skills,
            baseUrl: null));

        services.AddAgentCore(configuration, agent => agent
            .AddBuiltInTools()
            .AddTool<SaveNoteTool>()
            .AddTool<SearchNotesTool>()
            .AddTool<GetNoteTool>()
            .AddTool<ListNotesTool>()
            .AddTool<DeleteNoteTool>());

        foreach (var type in StandInToolTypes)
        {
            var definition = ((IAgentTool)RuntimeHelpers.GetUninitializedObject(type)).Definition;
            services.AddScoped<IAgentTool>(_ => new StandInTool(definition));
        }

        services.AddSingleton(Notes);
        services.AddScoped<DiscordRunContext>();
        services.AddScoped<IAssistantMessagePipeline, AssistantMessagePipeline>();

        _services = services.BuildServiceProvider();
    }

    /// <summary>The note store the memory tools write to, for asserting on rows.</summary>
    public IDmAssistantNoteRepository Notes { get; }

    /// <summary>
    /// Runs one question and returns what happened.
    /// </summary>
    /// <param name="question">What the user asked.</param>
    /// <param name="preActivatedSkills">Skills to treat as already loaded, as a second turn would.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<EvalRun> AskAsync(
        string question,
        IEnumerable<string>? preActivatedSkills = null,
        CancellationToken cancellationToken = default)
    {
        using var scope = _services.CreateScope();
        var pipeline = scope.ServiceProvider.GetRequiredService<IAssistantMessagePipeline>();

        var result = await pipeline.RunAsync(
            question,
            new EvalContext(preActivatedSkills?.ToList() ?? []),
            cancellationToken);

        return new EvalRun(result);
    }

    /// <summary>Seeds a note so a lookup case has something to find.</summary>
    /// <param name="content">The note's content.</param>
    /// <param name="tag">Its tag, if any.</param>
    public async Task<DmAssistantNote> SeedNoteAsync(string content, string? tag = null)
    {
        var note = new DmAssistantNote
        {
            UserId = UserId,
            Content = content,
            Tag = tag,
            CreatedAt = DateTime.UtcNow
        };

        _context.Set<DmAssistantNote>().Add(note);
        await _context.SaveChangesAsync();

        return note;
    }

    /// <summary>Every note this user has, read straight from the database.</summary>
    public async Task<IReadOnlyList<DmAssistantNote>> AllNotesAsync() =>
        await _context.Set<DmAssistantNote>().AsNoTracking().Where(n => n.UserId == UserId).ToListAsync();

    /// <inheritdoc />
    public void Dispose()
    {
        _services.Dispose();
        _context.Dispose();
        _database.Dispose();
    }

    /// <summary>The DM surface as an eval runs it: the owner, no history, a small round budget.</summary>
    private sealed class EvalContext(IReadOnlyCollection<string> preloadedSkills) : IAssistantContext
    {
        public string RateLimitCacheKeyPrefix => "eval:";
        public string RateLimitScopeKey => UserId.ToString();
        public int? RateLimit => null;
        public int RateLimitWindowMinutes => 0;
        public string? Model => EvalSettings.Model;
        public LlmMode Mode => LlmMode.DmAssistant;
        public string ModeKey => DmAssistantContext.ModeName;
        public DiscordRunIdentity Identity { get; } = new(UserId, CanMutate: true);
        public int MaxTokens => 2048;
        public int MaxToolCallIterations => 6;
        public int MaxToolResultChars => 8000;
        public IReadOnlyCollection<string> DisabledTools => [];
        public IReadOnlyCollection<string> PreloadedSkills => preloadedSkills;
        public IReadOnlyList<ConversationTurn> History => [];
        public AssistantCostRates CostRates => default;
        public int MaxResponseLength => 50_000;
        public string TruncationSuffix => "…";

        public Task<IReadOnlyList<PromptPart>> BuildPartsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PromptPart>>([]);

        public Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken) =>
            Task.FromResult(rawMessage);

        public Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    /// <summary>A skill tool with its shipped definition and no data behind it.</summary>
    private sealed class StandInTool(AgentToolDefinition definition) : IAgentTool
    {
        public AgentToolDefinition Definition { get; } = definition;

        public Task<string> InvokeAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResults.Error(
                "No server data is available in this environment. Tell the user you could not look it up."));
    }
}

/// <summary>What one eval run produced.</summary>
/// <param name="Result">The pipeline's result.</param>
public sealed record EvalRun(AssistantPipelineResult Result)
{
    /// <summary>The tools the model called, in order.</summary>
    public IReadOnlyList<string> ToolNames => Result.ToolNames;

    /// <summary>Whether <paramref name="toolName"/> was called at least once.</summary>
    /// <param name="toolName">The tool to look for.</param>
    public bool Called(string toolName) =>
        Result.ToolNames.Any(n => string.Equals(n, toolName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The skills the run ended with loaded (preloaded ones included), by key.</summary>
    public IReadOnlyList<string> ActivatedSkills => Result.LoadedSkills;
}
