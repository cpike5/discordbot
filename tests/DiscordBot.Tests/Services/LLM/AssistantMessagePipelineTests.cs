using System.Text.Json;
using cpike.Agent;
using cpike.Agent.OpenRouter;
using cpike.Agent.Prompting;
using cpike.Agent.Testing;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Infrastructure.Services.LLM.Tooling;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Tests for the shared assistant pipeline pieces (<see cref="AssistantRateLimiter"/> and
/// <see cref="AssistantMessagePipeline"/>) used by the guild, DM and feature-request assistants.
/// </summary>
/// <remarks>
/// The pipeline runs over the real cpike.Agent loop (<c>AddAgentCore</c>) with a
/// <see cref="ScriptedOpenRouterClient"/> in place of the network, so what is asserted is what the
/// loop actually sent and what the pipeline made of what came back.
/// </remarks>
public class AssistantMessagePipelineTests
{
    private const ulong TestUserId = 111222333UL;
    private const ulong TestGuildId = 123456789UL;
    private const string DefaultModel = "test/default-model";

    #region AssistantRateLimiter

    [Fact]
    public async Task CheckAsync_ReturnsAllowed_WhenUnderLimit()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var limiter = new AssistantRateLimiter(cache);

        var result = await limiter.CheckAsync("assistant_ratelimit:", "guild:1:user:2", limit: 5, windowMinutes: 5);

        result.IsAllowed.Should().BeTrue();
        result.RemainingQuestions.Should().Be(5);
    }

    [Fact]
    public async Task CheckAsync_ReturnsRateLimited_AfterLimitReached()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var limiter = new AssistantRateLimiter(cache);
        const string prefix = "assistant_ratelimit:";
        const string scopeKey = "guild:1:user:2";

        for (var i = 0; i < 3; i++)
        {
            limiter.RecordUsage(prefix, scopeKey, windowMinutes: 5);
        }

        var result = await limiter.CheckAsync(prefix, scopeKey, limit: 3, windowMinutes: 5);

        result.IsAllowed.Should().BeFalse();
        result.Message.Should().Contain("question limit");
        result.RetryAfter.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckAsync_CacheKeysAreIsolated_BetweenGuildAndDmPrefixes()
    {
        // A guild context and a DM context tracking the *same* scope key (e.g. the same user id)
        // must never share rate-limit state — proven here by exhausting the guild prefix's quota
        // and confirming the DM prefix is unaffected.
        var cache = new MemoryCache(new MemoryCacheOptions());
        var limiter = new AssistantRateLimiter(cache);
        var scopeKey = TestUserId.ToString();

        for (var i = 0; i < 5; i++)
        {
            limiter.RecordUsage(GuildAssistantContext.RateLimitPrefix, scopeKey, windowMinutes: 5);
        }

        var guildResult = await limiter.CheckAsync(GuildAssistantContext.RateLimitPrefix, scopeKey, limit: 5, windowMinutes: 5);
        var dmResult = await limiter.CheckAsync(DmAssistantContext.RateLimitPrefix, scopeKey, limit: 5, windowMinutes: 5);

        guildResult.IsAllowed.Should().BeFalse("the guild scope's quota was exhausted");
        dmResult.IsAllowed.Should().BeTrue("the DM prefix tracks a separate cache entry even for the same scope key");
        dmResult.RemainingQuestions.Should().Be(5);
    }

    #endregion

    #region AssistantMessagePipeline

    [Fact]
    public async Task RunAsync_ReturnsTheReplyAndItsUsage()
    {
        using var host = Host.Create(ScriptedResponses.Text("Here's how to use the soundboard.", inputTokens: 100, outputTokens: 50));

        var result = await host.Pipeline.RunAsync("How do I use the soundboard?", new StubContext());

        result.Success.Should().BeTrue();
        result.Response.Should().Be("Here's how to use the soundboard.");
        result.InputTokens.Should().Be(100);
        result.OutputTokens.Should().Be(50);
        result.LoopCount.Should().Be(1);
        result.ToolCalls.Should().Be(0);
        result.ToolNames.Should().BeEmpty();
        result.Model.Should().Be("test/context-model");
    }

    [Fact]
    public async Task RunAsync_SendsTheContextsModelBudgetsAndNoWebSearch()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));
        var context = new StubContext { Model = "vendor/some-model", MaxTokens = 777, MaxToolCallIterations = 3 };

        await host.Pipeline.RunAsync("hello", context);

        var request = host.Client.Requests.Should().ContainSingle().Subject;
        request.Model.Should().Be("vendor/some-model");
        request.MaxTokens.Should().Be(777);
        (request.Tools ?? []).Should().NotContain(t => t.Type != "function",
            "the assistants have no web search; the DM assistant fetches pages with fetch_url");
        (request.Plugins ?? []).Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_WithNoContextModel_RunsOnTheConfiguredDefault()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));

        var result = await host.Pipeline.RunAsync("hello", new StubContext { Model = null });

        host.Client.Requests.Single().Model.Should().Be(DefaultModel);
        result.Model.Should().Be(DefaultModel);
        result.UsageRecord!.Model.Should().Be(DefaultModel);
    }

    [Fact]
    public async Task RunAsync_StopsAtTheContextsToolRoundBudget()
    {
        // Every response asks for another tool call; MaxToolCallIterations is what ends the run.
        using var host = Host.Create(ScriptedResponses.ToolCall(ProbeTool.ToolName, new { n = 1 }));

        var result = await host.Pipeline.RunAsync("loop", new StubContext { MaxToolCallIterations = 2 });

        result.Success.Should().BeTrue();
        result.StoppedOnMaxIterations.Should().BeTrue();
        result.ToolCalls.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task RunAsync_ReportsTheToolsCalledAndTheRoundsTaken()
    {
        using var host = Host.Create(
            ScriptedResponses.ToolCall(ProbeTool.ToolName),
            ScriptedResponses.Text("done"));

        var result = await host.Pipeline.RunAsync("use the probe", new StubContext());

        result.ToolNames.Should().Equal(ProbeTool.ToolName);
        result.ToolCalls.Should().Be(1);
        result.LoopCount.Should().Be(2);
        result.Response.Should().Be("done");
    }

    [Fact]
    public async Task RunAsync_SeedsTheCallerIntoTheRunForTheTools()
    {
        using var host = Host.Create(
            ScriptedResponses.ToolCall(ProbeTool.ToolName),
            ScriptedResponses.Text("done"));

        await host.Pipeline.RunAsync("who am I", new StubContext
        {
            Identity = new DiscordRunIdentity(TestUserId, TestGuildId, ChannelId: 5, MessageId: 6)
        });

        host.Probe.SeenUserId.Should().Be(TestUserId);
        host.Probe.SeenGuildId.Should().Be(TestGuildId);
    }

    [Fact]
    public async Task RunAsync_DisabledTool_IsNotAdvertised()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));

        await host.Pipeline.RunAsync("hi", new StubContext { DisabledTools = [ClearingTool.ToolName] });

        var advertised = AdvertisedNames(host.Client.Requests.Single());
        advertised.Should().Contain(ProbeTool.ToolName);
        advertised.Should().NotContain(ClearingTool.ToolName);
    }

    [Fact]
    public async Task RunAsync_ConversationClearedByATool_IsReportedOnTheResult()
    {
        using var host = Host.Create(
            ScriptedResponses.ToolCall(ClearingTool.ToolName),
            ScriptedResponses.Text("Cleared."));

        var result = await host.Pipeline.RunAsync("start over", new StubContext());

        result.Success.Should().BeTrue();
        result.ConversationCleared.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithoutAClearingTool_DoesNotReportAClearedConversation()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));

        var result = await host.Pipeline.RunAsync("hi", new StubContext());

        result.ConversationCleared.Should().BeFalse();
    }

    [Fact]
    public async Task RunAsync_SkillLoadedDuringTheRun_IsReportedAsLoaded()
    {
        using var host = Host.Create(
            ScriptedResponses.ToolCall("load_skill", new { skill = Host.SkillKey }),
            ScriptedResponses.Text("loaded"));

        var result = await host.Pipeline.RunAsync("needs the skill", new StubContext { ModeKey = Host.SkilledMode });

        result.LoadedSkills.Should().Equal(Host.SkillKey);
    }

    [Fact]
    public async Task RunAsync_PreloadedSkill_ItsToolIsAdvertisedFromTheFirstCall()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));

        var result = await host.Pipeline.RunAsync("again", new StubContext
        {
            ModeKey = Host.SkilledMode,
            PreloadedSkills = [Host.SkillKey]
        });

        AdvertisedNames(host.Client.Requests.Single()).Should().Contain(SkillTool.ToolName);
        result.LoadedSkills.Should().Contain(Host.SkillKey);
    }

    [Fact]
    public async Task RunAsync_WithoutThePreload_TheSkillsToolIsWithheld()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));

        await host.Pipeline.RunAsync("first", new StubContext { ModeKey = Host.SkilledMode });

        var advertised = AdvertisedNames(host.Client.Requests.Single());
        advertised.Should().NotContain(SkillTool.ToolName);
        advertised.Should().Contain("load_skill");
    }

    /// <summary>
    /// OpenRouter reports what it actually billed for a call. That figure is authoritative and must
    /// win over the configured per-million rates, which are only an estimate from a fixed price list.
    /// </summary>
    [Fact]
    public async Task RunAsync_UsesBilledCost_WhenTheProviderReportsOne()
    {
        // Token counts that would price at 0.00105 via the configured rates, so a passing assertion
        // cannot be satisfied by the fallback path.
        using var host = Host.Create(ScriptedResponses.Text("answer", inputTokens: 100, outputTokens: 50, cost: 0.0123m));

        var result = await host.Pipeline.RunAsync("q", new StubContext { CostRates = new AssistantCostRates(3m, 15m, 0.3m, 3.75m) });

        result.EstimatedCostUsd.Should().Be(0.0123m);
        result.UsageRecord!.CostUsd.Should().Be(0.0123m);
        result.UsageRecord.CostSource.Should().Be(LlmCostSource.Billed);
    }

    /// <summary>
    /// A response that carries no cost means "not reported", not "free" — the configured rates are the
    /// fallback for that case.
    /// </summary>
    [Fact]
    public async Task RunAsync_FallsBackToConfiguredRates_WhenNoBilledCostIsReported()
    {
        using var host = Host.Create(ScriptedResponses.Text("answer", inputTokens: 100, outputTokens: 50));

        var result = await host.Pipeline.RunAsync("q", new StubContext { CostRates = new AssistantCostRates(3m, 15m, 0.3m, 3.75m) });

        // 100 input @ $3/M + 50 output @ $15/M.
        result.EstimatedCostUsd.Should().Be(0.00105m);
        result.UsageRecord!.CostSource.Should().Be(LlmCostSource.Estimated);
    }

    [Fact]
    public async Task RunAsync_BuildsTheUsageRecordFromTheContextAndTheRun()
    {
        using var host = Host.Create(
            ScriptedResponses.ToolCall(ProbeTool.ToolName),
            ScriptedResponses.Text("done", inputTokens: 30, outputTokens: 7));

        var result = await host.Pipeline.RunAsync("q", new StubContext
        {
            Mode = LlmMode.DmAssistant,
            Identity = new DiscordRunIdentity(TestUserId, TestGuildId)
        });

        var record = result.UsageRecord!;
        record.Mode.Should().Be(LlmMode.DmAssistant);
        record.UserId.Should().Be(TestUserId);
        record.GuildId.Should().Be(TestGuildId);
        record.Model.Should().Be("test/context-model");
        record.InputTokens.Should().Be(result.InputTokens);
        record.OutputTokens.Should().Be(result.OutputTokens);
        record.LlmCalls.Should().Be(2);
        record.ToolCalls.Should().Be(1);
        record.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_TruncatesResponse_WhenLongerThanMaxResponseLength()
    {
        using var host = Host.Create(ScriptedResponses.Text(new string('x', 5000)));

        var result = await host.Pipeline.RunAsync("question", new StubContext
        {
            MaxResponseLength = 1800,
            TruncationSuffix = "\n\n... *(response truncated)*"
        });

        result.Response!.Length.Should().Be(1800);
        result.Response.Should().EndWith("(response truncated)*");
    }

    [Fact]
    public async Task RunAsync_ShortResponse_IsLeftAlone()
    {
        using var host = Host.Create(ScriptedResponses.Text("short"));

        var result = await host.Pipeline.RunAsync("question", new StubContext { MaxResponseLength = 1800 });

        result.Response.Should().Be("short");
    }

    [Fact]
    public async Task RunAsync_WhenTheClientThrows_ReturnsAFailedResultWithAFailedUsageRecord()
    {
        using var host = Host.Create(_ => throw new InvalidOperationException("provider down"));

        var result = await host.Pipeline.RunAsync("q", new StubContext { Mode = LlmMode.GuildAssistant });

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        result.UsageRecord.Should().NotBeNull();
        result.UsageRecord!.Success.Should().BeFalse();
        result.UsageRecord.CostUsd.Should().Be(0m);
        result.UsageRecord.Mode.Should().Be(LlmMode.GuildAssistant);
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_Throws()
    {
        // A cancelled run is the caller's decision, not a failed run: it propagates rather than
        // becoming a Success=false result with a ledger row.
        using var cts = new CancellationTokenSource();
        using var host = Host.Create(_ =>
        {
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
            return ScriptedResponses.Text("never");
        });

        var act = () => host.Pipeline.RunAsync("q", new StubContext(), cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_GuildContext_PutsThePortalLinksInTheTurnContextNotTheSystemPrompt()
    {
        // The links name the guild; in the system prompt they would make the cached prefix per guild.
        using var host = Host.Create(ScriptedResponses.Text("ok"));
        var context = BuildGuildContext(options =>
        {
            options.IncludeGuildContext = true;
            options.BaseUrl = "https://bot.example.com/";
        });

        await host.Pipeline.RunAsync("where is the soundboard?", context);

        var request = host.Client.Requests.Single();
        var system = Text(request.Messages.First(m => m.Role == "system").Content);
        var user = Text(request.Messages.Last(m => m.Role == "user").Content);

        var link = $"https://bot.example.com/Portal/Soundboard/{TestGuildId}";
        user.Should().Contain(link);
        system.Should().NotContain(link);
        system.Should().NotContain(TestGuildId.ToString());
    }

    [Fact]
    public async Task RunAsync_GuildContext_WithGuildContextOff_SendsNoPortalLinks()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));
        var context = BuildGuildContext(options =>
        {
            options.IncludeGuildContext = false;
            options.BaseUrl = "https://bot.example.com";
        });

        await host.Pipeline.RunAsync("hi", context);

        var request = host.Client.Requests.Single();
        request.Messages.Select(m => Text(m.Content)).Should().NotContain(t => t.Contains("/Portal/Soundboard/"));
    }

    [Fact]
    public async Task RunAsync_GuildContext_PassesItsDisabledToolsAndBudgets()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));
        var context = BuildGuildContext(disabledTools: [ProbeTool.ToolName]);

        await host.Pipeline.RunAsync("hi", context);

        var request = host.Client.Requests.Single();
        AdvertisedNames(request).Should().NotContain(ProbeTool.ToolName);
        request.MaxTokens.Should().Be(512);
        request.Model.Should().Be("test/resolved-model");
    }

    [Fact]
    public async Task RunAsync_ThenRecordUsage_InvokesRecordUsage_OnTheSameContextInstance()
    {
        // Exercises the DM path end-to-end through the shared pipeline: RunAsync executes against the
        // DmAssistantContext, and a subsequent RecordUsageAsync call (as DmAssistantService performs)
        // must land on that same context instance.
        using var host = Host.Create(ScriptedResponses.Text("DM response"));
        var mockConversationRepo = new Mock<IDmConversationMessageRepository>();
        var mockInteractionLogRepo = new Mock<IDmAssistantInteractionLogRepository>();
        var mockMetricsRepo = new Mock<IDmAssistantUsageMetricsRepository>();

        var options = new DmAssistantOptions
        {
            Model = "anthropic/claude-sonnet-4",
            MaxTokens = 4096,
            MaxResponseLength = 50000,
            TruncationSuffix = "\n\n... *(response truncated)*",
            EnableCostTracking = true,
            LogInteractions = true
        };

        var context = new DmAssistantContext(
            TestUserId,
            activeGuildId: null,
            new List<ConversationTurn>(),
            Array.Empty<string>(),
            mockConversationRepo.Object,
            mockInteractionLogRepo.Object,
            mockMetricsRepo.Object,
            options,
            Mock.Of<ILogger>(),
            "test/resolved-model");

        var result = await host.Pipeline.RunAsync("What's the bot status?", context);
        result.Success.Should().BeTrue();

        await context.RecordUsageAsync("What's the bot status?", result, CancellationToken.None);

        mockConversationRepo.Verify(
            r => r.AddAsync(It.Is<DmConversationMessage>(m => m.UserId == TestUserId && m.Role == "user"), It.IsAny<CancellationToken>()),
            Times.Once);
        mockConversationRepo.Verify(
            r => r.AddAsync(It.Is<DmConversationMessage>(m => m.UserId == TestUserId && m.Role == "assistant" && m.Content == "DM response"), It.IsAny<CancellationToken>()),
            Times.Once);
        mockInteractionLogRepo.Verify(
            r => r.AddAsync(It.Is<DmAssistantInteractionLog>(l => l.UserId == TestUserId && l.Success), It.IsAny<CancellationToken>()),
            Times.Once);
        mockMetricsRepo.Verify(
            r => r.GetByUserAndDateAsync(TestUserId, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunAsync_DmContext_ReplaysItsHistoryBeforeThePrompt()
    {
        using var host = Host.Create(ScriptedResponses.Text("ok"));
        var context = new StubContext
        {
            History = [new ConversationTurn(true, "earlier question"), new ConversationTurn(false, "earlier answer")]
        };

        await host.Pipeline.RunAsync("now", context);

        var texts = host.Client.Requests.Single().Messages.Select(m => Text(m.Content)).ToList();
        var earlier = texts.FindIndex(t => t.Contains("earlier question"));
        var now = texts.FindLastIndex(t => t.Contains("now"));
        earlier.Should().BeGreaterThan(0);
        now.Should().BeGreaterThan(earlier);
    }

    private static List<string> AdvertisedNames(ChatCompletionRequest request) =>
        (request.Tools ?? []).Where(t => t.Function is not null).Select(t => t.Function!.Name).ToList();

    private static string Text(object? content) => content switch
    {
        null => string.Empty,
        string text => text,
        IEnumerable<ContentPart> parts => string.Join("\n", parts.Select(p => p.Text)),
        _ => JsonSerializer.Serialize(content)
    };

    private static GuildAssistantContext BuildGuildContext(
        Action<AssistantOptions>? configure = null,
        IReadOnlyCollection<string>? disabledTools = null)
    {
        var options = new AssistantOptions
        {
            Sampling = new() { Model = "anthropic/claude-sonnet-4", MaxTokens = 512 },
            Tools = new() { MaxToolRounds = 5 },
            Messages = new() { MaxResponseLength = 1800, TruncationSuffix = "\n\n... *(response truncated)*" },
            Cost = new() { EnableCostTracking = false },
            Privacy = new() { LogInteractions = false }
        };
        configure?.Invoke(options);

        return new GuildAssistantContext(
            TestGuildId,
            channelId: 1,
            TestUserId,
            messageId: 1,
            rateLimit: 5,
            question: "question",
            callerCanMutate: false,
            disabledTools ?? Array.Empty<string>(),
            Mock.Of<IGuildService>(),
            Mock.Of<IAssistantUsageMetricsRepository>(),
            Mock.Of<IAssistantInteractionLogRepository>(),
            options,
            Mock.Of<ILogger>(),
            "test/resolved-model");
    }

    /// <summary>The real pipeline over the real loop, with a scripted model and in-memory prompts.</summary>
    private sealed class Host : IDisposable
    {
        public const string SkilledMode = "skilled";
        public const string SkillKey = "probing";

        private readonly ServiceProvider _provider;

        private Host(ServiceProvider provider, ScriptedOpenRouterClient client)
        {
            _provider = provider;
            Client = client;
        }

        public ScriptedOpenRouterClient Client { get; }

        public IAssistantMessagePipeline Pipeline => _provider.GetRequiredService<IAssistantMessagePipeline>();

        public ProbeRecorder Probe => _provider.GetRequiredService<ProbeRecorder>();

        public static Host Create(params ChatCompletionResponse[] responses) => Create(new ScriptedOpenRouterClient(responses));

        public static Host Create(Func<ScriptedCall, ChatCompletionResponse> respond) => Create(new ScriptedOpenRouterClient(respond));

        private static Host Create(ScriptedOpenRouterClient client)
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["OpenRouter:Model"] = DefaultModel })
                .Build();

            var services = new ServiceCollection();
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
            services.AddSingleton<IConfiguration>(configuration);
            services.Configure<OpenRouterOptions>(configuration.GetSection(OpenRouterOptions.SectionName));
            services.AddSingleton<IOpenRouterClient>(client);
            services.AddSingleton<IOpenRouterStreamingClient>(client);

            var tools = new[] { ProbeTool.ToolName, ClearingTool.ToolName };
            services.AddSingleton<IAgentPromptStore>(new InMemoryAgentPromptStore("bot",
            [
                new AgentDefinition("bot", "Bot", "You are a test bot."),
                new AgentDefinition(StubContext.DefaultMode, "Test", "Test mode prompt.", tools),
                new AgentDefinition(GuildAssistantContext.ModeName, "Guild", "Guild mode prompt.", tools),
                new AgentDefinition(DmAssistantContext.ModeName, "DM", "DM mode prompt.", tools),
                new AgentDefinition(SkilledMode, "Skilled", "Skilled mode prompt.", [.. tools, "load_skill"])
                {
                    Skills = [SkillKey]
                }
            ]));
            services.AddSingleton<IAgentSkillStore>(new InMemorySkillStore(
            [
                new SkillDefinition(SkillKey, "Probe something with the skill tool.", "Use skill_probe.", [SkillTool.ToolName])
            ]));

            services.AddAgentCore(configuration, agent => agent
                .AddBuiltInTools()
                .AddTool<ProbeTool>()
                .AddTool<ClearingTool>()
                .AddTool<SkillTool>());

            services.AddSingleton<ProbeRecorder>();
            services.AddScoped<DiscordRunContext>();
            services.AddSingleton<IAssistantMessagePipeline>(sp => new AssistantMessagePipeline(
                sp.GetRequiredService<IServiceScopeFactory>(), NullLogger<AssistantMessagePipeline>.Instance));

            return new Host(services.BuildServiceProvider(validateScopes: true), client);
        }

        public void Dispose() => _provider.Dispose();
    }

    private sealed class ProbeRecorder
    {
        public ulong SeenUserId { get; set; }
        public ulong SeenGuildId { get; set; }
    }

    /// <summary>A tool that records the caller it was run for.</summary>
    private sealed class ProbeTool(DiscordRunContext run, ProbeRecorder recorder) : IAgentTool
    {
        public const string ToolName = "probe";

        public AgentToolDefinition Definition { get; } = AgentTools.Define(
            ToolName, "Probes the run.", ToolInput.ObjectSchema(new { n = ToolInput.Schema("integer", "Any number.") }));

        public Task<string> InvokeAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
        {
            recorder.SeenUserId = run.UserId;
            recorder.SeenGuildId = run.GuildId;
            return Task.FromResult(ToolResults.Json(new { ok = true }));
        }
    }

    /// <summary>A tool that clears the conversation the way <c>clear_conversation</c> does.</summary>
    private sealed class ClearingTool(DiscordRunContext run) : IAgentTool
    {
        public const string ToolName = "clear_probe";

        public AgentToolDefinition Definition { get; } = AgentTools.Define(
            ToolName, "Clears the conversation.", ToolInput.ObjectSchema(new { }));

        public Task<string> InvokeAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken)
        {
            run.ConversationCleared = true;
            return Task.FromResult(ToolResults.Json(new { success = true }));
        }
    }

    /// <summary>A tool only the test skill brings.</summary>
    private sealed class SkillTool : IAgentTool
    {
        public const string ToolName = "skill_probe";

        public AgentToolDefinition Definition { get; } = AgentTools.Define(
            ToolName, "Probes with the skill.", ToolInput.ObjectSchema(new { }));

        public Task<string> InvokeAsync(IReadOnlyDictionary<string, JsonElement> input, CancellationToken cancellationToken) =>
            Task.FromResult(ToolResults.Json(new { ok = true }));
    }

    /// <summary>An <see cref="IAssistantContext"/> whose every member a test can set.</summary>
    private sealed class StubContext : IAssistantContext
    {
        public const string DefaultMode = "test";

        public string RateLimitCacheKeyPrefix { get; init; } = "test:";
        public string RateLimitScopeKey { get; init; } = "scope";
        public int? RateLimit { get; init; }
        public int RateLimitWindowMinutes { get; init; }
        public string? Model { get; init; } = "test/context-model";
        public LlmMode Mode { get; init; } = LlmMode.GuildAssistant;
        public string ModeKey { get; init; } = DefaultMode;
        public DiscordRunIdentity Identity { get; init; } = new(TestUserId, CanMutate: true);
        public int MaxTokens { get; init; } = 1024;
        public int MaxToolCallIterations { get; init; } = 5;
        public int MaxToolResultChars { get; init; } = 8000;
        public IReadOnlyCollection<string> DisabledTools { get; init; } = [];
        public IReadOnlyCollection<string> PreloadedSkills { get; init; } = [];
        public IReadOnlyList<ConversationTurn> History { get; init; } = [];
        public AssistantCostRates CostRates { get; init; } = new(3m, 15m, 0.3m, 3.75m);
        public int MaxResponseLength { get; init; } = 2000;
        public string TruncationSuffix { get; init; } = "…";

        public Task<IReadOnlyList<PromptPart>> BuildPartsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PromptPart>>([]);

        public Task<string> FormatUserMessageAsync(string rawMessage, CancellationToken cancellationToken) =>
            Task.FromResult(rawMessage);

        public Task RecordUsageAsync(string inputMessage, AssistantPipelineResult result, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    #endregion

    #region Usage ledger wiring

    [Fact]
    public async Task GuildAssistantContext_RecordUsageAsync_SetsInteractionLogId_ThenRecords()
    {
        var mockGuildService = new Mock<IGuildService>();
        var mockInteractionLogRepo = new Mock<IAssistantInteractionLogRepository>();
        mockInteractionLogRepo
            .Setup(r => r.AddAsync(It.IsAny<AssistantInteractionLog>(), It.IsAny<CancellationToken>()))
            .Callback<AssistantInteractionLog, CancellationToken>((log, _) => log.Id = 42)
            .ReturnsAsync((AssistantInteractionLog log, CancellationToken _) => log);
        var mockRecorder = new Mock<ILlmUsageRecorder>();

        var options = new AssistantOptions
        {
            Privacy = new() { LogInteractions = true },
            Cost = new() { EnableCostTracking = false }
        };

        var context = new GuildAssistantContext(
            TestGuildId, channelId: 1, TestUserId, messageId: 1, rateLimit: 5, question: "q",
            callerCanMutate: false, Array.Empty<string>(), mockGuildService.Object,
            Mock.Of<IAssistantUsageMetricsRepository>(), mockInteractionLogRepo.Object,
            options, Mock.Of<ILogger>(), "test/model", resolvedPricing: null, usageRecorder: mockRecorder.Object);

        var pipelineResult = new AssistantPipelineResult
        {
            Success = true,
            Response = "answer",
            LatencyMs = 123,
            UsageRecord = new LlmUsageRecord { Mode = LlmMode.GuildAssistant, Model = "test/model" }
        };

        await context.RecordUsageAsync("q", pipelineResult, CancellationToken.None);

        pipelineResult.UsageRecord.InteractionLogId.Should().Be(42);
        pipelineResult.UsageRecord.LatencyMs.Should().Be(123);
        mockRecorder.Verify(r => r.Record(It.Is<LlmUsageRecord>(u => u.InteractionLogId == 42)), Times.Once);
    }

    [Fact]
    public async Task GuildAssistantContext_RecordUsageAsync_WithLoggingOff_RecordsWithNullInteractionLogId()
    {
        var mockGuildService = new Mock<IGuildService>();
        var mockInteractionLogRepo = new Mock<IAssistantInteractionLogRepository>();
        var mockRecorder = new Mock<ILlmUsageRecorder>();

        var options = new AssistantOptions
        {
            Privacy = new() { LogInteractions = false },
            Cost = new() { EnableCostTracking = false }
        };

        var context = new GuildAssistantContext(
            TestGuildId, channelId: 1, TestUserId, messageId: 1, rateLimit: 5, question: "q",
            callerCanMutate: false, Array.Empty<string>(), mockGuildService.Object,
            Mock.Of<IAssistantUsageMetricsRepository>(), mockInteractionLogRepo.Object,
            options, Mock.Of<ILogger>(), "test/model", resolvedPricing: null, usageRecorder: mockRecorder.Object);

        var pipelineResult = new AssistantPipelineResult
        {
            Success = true,
            Response = "answer",
            LatencyMs = 99,
            UsageRecord = new LlmUsageRecord { Mode = LlmMode.GuildAssistant, Model = "test/model" }
        };

        await context.RecordUsageAsync("q", pipelineResult, CancellationToken.None);

        mockInteractionLogRepo.Verify(r => r.AddAsync(It.IsAny<AssistantInteractionLog>(), It.IsAny<CancellationToken>()), Times.Never);
        pipelineResult.UsageRecord.InteractionLogId.Should().BeNull();
        mockRecorder.Verify(r => r.Record(It.Is<LlmUsageRecord>(u => u.InteractionLogId == null)), Times.Once);
    }

    [Fact]
    public async Task DmAssistantContext_RecordUsageAsync_SetsInteractionLogId_ThenRecords()
    {
        var mockConversationRepo = new Mock<IDmConversationMessageRepository>();
        var mockInteractionLogRepo = new Mock<IDmAssistantInteractionLogRepository>();
        mockInteractionLogRepo
            .Setup(r => r.AddAsync(It.IsAny<DmAssistantInteractionLog>(), It.IsAny<CancellationToken>()))
            .Callback<DmAssistantInteractionLog, CancellationToken>((log, _) => log.Id = 7)
            .ReturnsAsync((DmAssistantInteractionLog log, CancellationToken _) => log);
        var mockMetricsRepo = new Mock<IDmAssistantUsageMetricsRepository>();
        var mockRecorder = new Mock<ILlmUsageRecorder>();

        var options = new DmAssistantOptions { LogInteractions = true, EnableCostTracking = false };

        var context = new DmAssistantContext(
            TestUserId, activeGuildId: null, new List<ConversationTurn>(), Array.Empty<string>(),
            mockConversationRepo.Object, mockInteractionLogRepo.Object,
            mockMetricsRepo.Object, options, Mock.Of<ILogger>(), "test/model", resolvedPricing: null,
            usageRecorder: mockRecorder.Object);

        var pipelineResult = new AssistantPipelineResult
        {
            Success = true,
            Response = "answer",
            LatencyMs = 55,
            UsageRecord = new LlmUsageRecord { Mode = LlmMode.DmAssistant, Model = "test/model" }
        };

        await context.RecordUsageAsync("q", pipelineResult, CancellationToken.None);

        pipelineResult.UsageRecord.InteractionLogId.Should().Be(7);
        mockRecorder.Verify(r => r.Record(It.Is<LlmUsageRecord>(u => u.InteractionLogId == 7)), Times.Once);
    }

    #endregion
}
