using DiscordBot.Agents;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Agents.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Agents.OpenRouter;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Providers;
using DiscordBot.Infrastructure.Services.LLM.Tools;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace DiscordBot.Evals;

/// <summary>
/// One eval run: a real agent loop, a real OpenRouter call, real tools over a real database.
/// </summary>
/// <remarks>
/// <para>
/// What is real here matters more than what is convenient. The loop is <see cref="AgentRunner"/>,
/// the client is <see cref="OpenRouterLlmClient"/>, the tools are the shipped
/// <see cref="IAgentTool"/>s behind the shipped <see cref="DmAgentToolProvider"/> — so the catalogue
/// routing, the mutation refusal, the skill composition and the result conventions are all the
/// production ones. What is faked is only what an eval cannot have: the database is throwaway SQLite
/// and there is no Discord client, which is why the tools exercised here are the ones that do not
/// need one.
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

    private readonly SqliteConnection _connection;
    private readonly BotDbContext _context;
    private readonly HttpClient _http;
    private readonly IAgentRunner _runner;
    private readonly IToolRegistry _registry;
    private readonly IMemoryCache _cache;

    /// <summary>Creates the harness.</summary>
    public EvalHarness()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _context = new BotDbContext(
            new DbContextOptionsBuilder<BotDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        Notes = new DmAssistantNoteRepository(
            _context,
            NullLogger<DmAssistantNoteRepository>.Instance,
            NullLogger<Repository<DmAssistantNote>>.Instance);

        _cache = new MemoryCache(new MemoryCacheOptions());

        var options = Options.Create(new OpenRouterOptions
        {
            ApiKey = EvalSettings.ApiKey ?? string.Empty,
            BaseUrl = EvalSettings.BaseUrl,
            DefaultModel = EvalSettings.Model
        });

        _http = new HttpClient
        {
            BaseAddress = new Uri(EvalSettings.BaseUrl.EndsWith('/') ? EvalSettings.BaseUrl : EvalSettings.BaseUrl + "/"),
            Timeout = TimeSpan.FromMinutes(2)
        };

        _runner = new AgentRunner(
            new OpenRouterLlmClient(
                _http,
                options,
                new OpenRouterParameterSupportCache(),
                NullLogger<OpenRouterLlmClient>.Instance),
            NullLogger<AgentRunner>.Instance);

        // The shipped adapter over the shipped tools, so the catalogue is what decides what the DM
        // surface advertises here exactly as it does in production.
        var provider = new DmAgentToolProvider(Tools(), NullLogger<DmAgentToolProvider>.Instance);

        _registry = new ToolRegistry(NullLogger<ToolRegistry>.Instance, new[] { (IToolProvider)provider });
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
        var skills = await SkillsAsync(preActivatedSkills, cancellationToken);

        var context = new AgentContext
        {
            SystemPrompt = SkillRoster.Append(SystemPrompt, skills),
            Model = EvalSettings.Model,
            ToolRegistry = _registry,
            Skills = skills,
            MaxToolCallIterations = 6,
            Temperature = 0,
            RunKind = "eval",
            ConversationHistory = new List<LlmMessage>(),
            ExecutionContext = new ToolContext { UserId = UserId, CanMutate = true }
        };

        var result = await _runner.RunAsync(question, context, cancellationToken);

        return new EvalRun(result, skills);
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
        _http.Dispose();
        _context.Dispose();
        _connection.Dispose();
        _cache.Dispose();
    }

    /// <summary>
    /// The tools this harness can run: the memory tools, which need only a repository, and the skill
    /// loader. The rest of the DM surface needs a Discord client or a populated guild database, which
    /// an eval has neither of.
    /// </summary>
    private IEnumerable<IAgentTool> Tools() =>
        new IAgentTool[]
        {
            new SaveNoteTool(Notes, NullLogger<SaveNoteTool>.Instance),
            new SearchNotesTool(Notes),
            new GetNoteTool(Notes),
            new ListNotesTool(Notes),
            new DeleteNoteTool(Notes, NullLogger<DeleteNoteTool>.Instance),
            new LoadSkillTool()
        };

    private async Task<SkillSession> SkillsAsync(
        IEnumerable<string>? preActivated,
        CancellationToken cancellationToken)
    {
        var library = new SkillLibrary(
            new PromptTemplate(NullLogger<PromptTemplate>.Instance, _cache),
            _cache,
            NullLogger<SkillLibrary>.Instance);

        var factory = new SkillSessionFactory(library, NullLogger<SkillSessionFactory>.Instance);

        return await factory.CreateAsync(
            EvalPaths.DmSkillsDirectory, _registry, preActivated, cancellationToken);
    }

    /// <summary>
    /// Deliberately thin. The production DM prompt is long, changes often, and would make every eval
    /// a test of the prompt rather than of the mechanism; what is being evaluated here is whether the
    /// model reaches for the right tool when nothing is telling it to.
    /// </summary>
    private const string SystemPrompt =
        "You are the assistant for a Discord bot, talking to its owner in a direct message. "
        + "Use your tools when a request needs them, and answer plainly when it does not.";
}

/// <summary>What one eval run produced.</summary>
/// <param name="Result">The loop's result.</param>
/// <param name="Skills">The run's skill session, after the run — its activations are the record of what loaded.</param>
public sealed record EvalRun(AgentRunResult Result, SkillSession Skills)
{
    /// <summary>The tools the model called, in order.</summary>
    public IReadOnlyList<string> ToolNames => Result.ToolNames;

    /// <summary>Whether <paramref name="toolName"/> was called at least once.</summary>
    /// <param name="toolName">The tool to look for.</param>
    public bool Called(string toolName) =>
        Result.ToolNames.Any(n => string.Equals(n, toolName, StringComparison.OrdinalIgnoreCase));

    /// <summary>The skills the run loaded, by key.</summary>
    public IReadOnlyList<string> ActivatedSkills => Skills.Activated.Select(s => s.Key).ToList();
}
