using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Tests for the shared assistant pipeline pieces (<see cref="AssistantRateLimiter"/> and
/// <see cref="AssistantMessagePipeline"/>) used by both <c>AssistantService</c> (guild) and
/// <c>DmAssistantService</c> (DM).
/// </summary>
public class AssistantMessagePipelineTests
{
    private const ulong TestUserId = 111222333UL;
    private const ulong TestGuildId = 123456789UL;

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
    public async Task RunAsync_GuildContext_ReturnsSuccessfulResult()
    {
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "Here's how to use the soundboard.",
                TotalToolCalls = 1,
                TotalUsage = new LlmUsage { InputTokens = 100, OutputTokens = 50, CachedTokens = 20 }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("system prompt template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("rendered system prompt");

        var result = await pipeline.RunAsync("How do I use the soundboard?", context);

        result.Success.Should().BeTrue();
        result.Response.Should().Be("Here's how to use the soundboard.");
        result.InputTokens.Should().Be(100);
        result.OutputTokens.Should().Be(50);
        result.CachedTokens.Should().Be(20);
        result.ToolCalls.Should().Be(1);
        result.EstimatedCostUsd.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task RunAsync_PassesContextMode_ThroughToAgentContext()
    {
        AgentContext? capturedContext = null;
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .Callback<string, AgentContext, CancellationToken>((_, ctx, _) => capturedContext = ctx)
            .ReturnsAsync(new AgentRunResult { Success = true, Response = "ok" });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("system prompt template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("rendered system prompt");

        await pipeline.RunAsync("question", context);

        capturedContext.Should().NotBeNull();
        capturedContext!.Mode.Should().Be(context.Mode);
    }

    [Fact]
    public async Task RunAsync_WithNoModelReportedAnywhere_UsesUnknownNotEmptyString()
    {
        // GuildAssistantContext/DmAssistantContext always carry a resolved model in production
        // (ILlmModelResolver has a last-resort default), so a null context.Model can only be
        // exercised through the IAssistantContext abstraction directly.
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult { Success = true, Response = "ok", Model = null });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var mockContext = new Mock<IAssistantContext>();
        mockContext.Setup(c => c.Model).Returns((string?)null);
        mockContext.Setup(c => c.Mode).Returns(LlmMode.GuildAssistant);
        mockContext.Setup(c => c.MaxTokens).Returns(512);
        mockContext.Setup(c => c.Temperature).Returns(0.7);
        mockContext.Setup(c => c.MaxToolCallIterations).Returns(5);
        mockContext.Setup(c => c.ExecutionContext).Returns(new ToolContext { UserId = TestUserId, GuildId = TestGuildId });
        mockContext.Setup(c => c.ConversationHistory).Returns(new List<LlmMessage>());
        mockContext.Setup(c => c.CostRates).Returns(new AssistantCostRates());
        mockContext.Setup(c => c.MaxResponseLength).Returns(1800);
        mockContext.Setup(c => c.TruncationSuffix).Returns("...");
        mockContext.Setup(c => c.BuildSystemPromptAsync(It.IsAny<CancellationToken>())).ReturnsAsync("system prompt");

        var result = await pipeline.RunAsync("question", mockContext.Object);

        result.Model.Should().Be("unknown");
        result.UsageRecord!.Model.Should().Be("unknown");
    }

    /// <summary>
    /// OpenRouter reports what it actually billed for a call. That figure is authoritative and must
    /// win over the configured per-million rates, which are only an estimate from a fixed price list
    /// and are wrong for any model priced differently from the configured one.
    /// </summary>
    [Fact]
    public async Task RunAsync_UsesBilledCost_WhenTheProviderReportsOne()
    {
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "Answer.",
                // Token counts that would price at 0.00105 via the configured rates, so a passing
                // assertion cannot be satisfied by the fallback path.
                TotalUsage = new LlmUsage
                {
                    InputTokens = 100,
                    OutputTokens = 50,
                    EstimatedCost = 0.00042m
                }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("system prompt template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("rendered system prompt");

        var result = await pipeline.RunAsync("question", context);

        result.EstimatedCostUsd.Should().Be(0.00042m);
    }

    /// <summary>
    /// A response that carries no cost means "not reported", not "free" — the configured rates are
    /// the fallback for that case.
    /// </summary>
    [Fact]
    public async Task RunAsync_FallsBackToConfiguredRates_WhenNoBilledCostIsReported()
    {
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "Answer.",
                TotalUsage = new LlmUsage { InputTokens = 100, OutputTokens = 50, EstimatedCost = null }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("system prompt template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("rendered system prompt");

        var result = await pipeline.RunAsync("question", context);

        // 100 input @ $3/M + 50 output @ $15/M.
        result.EstimatedCostUsd.Should().Be(0.00105m);
    }

    [Fact]
    public async Task RunAsync_BuildsUsageRecord_WithBilledCostSource_WhenProviderReportsCost()
    {
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "Answer.",
                LoopCount = 2,
                TotalToolCalls = 1,
                Model = "anthropic/claude-sonnet-4.6",
                TotalUsage = new LlmUsage { InputTokens = 100, OutputTokens = 50, EstimatedCost = 0.00042m }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("system prompt template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("rendered system prompt");

        var result = await pipeline.RunAsync("question", context);

        result.Model.Should().Be("anthropic/claude-sonnet-4.6");
        result.UsageRecord.Should().NotBeNull();
        result.UsageRecord!.Mode.Should().Be(LlmMode.GuildAssistant);
        result.UsageRecord.Model.Should().Be("anthropic/claude-sonnet-4.6");
        result.UsageRecord.UserId.Should().Be(TestUserId);
        result.UsageRecord.GuildId.Should().Be(TestGuildId);
        result.UsageRecord.LlmCalls.Should().Be(2);
        result.UsageRecord.ToolCalls.Should().Be(1);
        result.UsageRecord.CostUsd.Should().Be(0.00042m);
        result.UsageRecord.CostSource.Should().Be(LlmCostSource.Billed);
        result.UsageRecord.Success.Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_BuildsUsageRecord_WithEstimatedCostSource_WhenNoBilledCostReported()
    {
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "Answer.",
                Model = null,
                TotalUsage = new LlmUsage { InputTokens = 100, OutputTokens = 50, EstimatedCost = null }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("system prompt template");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("rendered system prompt");

        var result = await pipeline.RunAsync("question", context);

        // No response model reported - falls back to the requested slug (context.Model).
        result.Model.Should().Be("test/resolved-model");
        result.UsageRecord!.Model.Should().Be("test/resolved-model");
        result.UsageRecord.CostSource.Should().Be(LlmCostSource.Estimated);
    }

    [Fact]
    public async Task RunAsync_DmContext_ReturnsSuccessfulResult()
    {
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "Sure, here's the status.",
                TotalUsage = new LlmUsage { InputTokens = 200, OutputTokens = 75 }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildDmContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("owner system prompt");

        var result = await pipeline.RunAsync("What's the bot status?", context);

        result.Success.Should().BeTrue();
        result.Response.Should().Be("Sure, here's the status.");
        result.InputTokens.Should().Be(200);
        result.OutputTokens.Should().Be(75);
    }

    [Fact]
    public async Task RunAsync_TruncatesResponse_WhenLongerThanMaxResponseLength()
    {
        var longResponse = new string('a', 2000);
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult { Success = true, Response = longResponse, TotalUsage = new LlmUsage() });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var context = BuildGuildContext(out var mockPromptTemplate);
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("prompt");
        mockPromptTemplate
            .Setup(p => p.Render(It.IsAny<string>(), It.IsAny<Dictionary<string, string>>()))
            .Returns("prompt");

        var result = await pipeline.RunAsync("question", context);

        result.Response!.Length.Should().BeLessThanOrEqualTo(1800);
        result.Response.Should().EndWith("(response truncated)*");
    }

    [Fact]
    public async Task RunAsync_ThenRecordUsage_InvokesRecordUsage_OnTheSameContextInstance()
    {
        // Exercises the DM path end-to-end through the shared pipeline: RunAsync builds/executes
        // against the DmAssistantContext, and a subsequent RecordUsageAsync call (as
        // DmAssistantService performs) must be invoked against that same context instance —
        // never silently routed to a different context.
        var mockAgentRunner = new Mock<IAgentRunner>();
        mockAgentRunner
            .Setup(r => r.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "DM response",
                TotalUsage = new LlmUsage { InputTokens = 10, OutputTokens = 5 }
            });

        var pipeline = new AssistantMessagePipeline(mockAgentRunner.Object);
        var mockConversationRepo = new Mock<IDmConversationMessageRepository>();
        var mockInteractionLogRepo = new Mock<IDmAssistantInteractionLogRepository>();
        var mockMetricsRepo = new Mock<IDmAssistantUsageMetricsRepository>();

        var options = new DmAssistantOptions
        {
            Model = "anthropic/claude-sonnet-4",
            MaxTokens = 4096,
            Temperature = 0.7,
            MaxResponseLength = 50000,
            TruncationSuffix = "\n\n... *(response truncated)*",
            EnableCostTracking = true,
            LogInteractions = true
        };

        var mockPromptTemplate = new Mock<IPromptTemplate>();
        mockPromptTemplate
            .Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("owner system prompt");

        var context = new DmAssistantContext(
            TestUserId,
            activeGuildId: null,
            Mock.Of<IToolRegistry>(),
            new List<LlmMessage>(),
            mockPromptTemplate.Object,
            mockConversationRepo.Object,
            mockInteractionLogRepo.Object,
            mockMetricsRepo.Object,
            options,
            Mock.Of<ILogger>(),
            "test/resolved-model");

        var result = await pipeline.RunAsync("What's the bot status?", context);
        result.Success.Should().BeTrue();

        await context.RecordUsageAsync("What's the bot status?", result, CancellationToken.None);

        // Conversation turns were saved (proves RecordUsageAsync ran against this DmAssistantContext).
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

    private static GuildAssistantContext BuildGuildContext(out Mock<IPromptTemplate> mockPromptTemplate, string model = "test/resolved-model")
    {
        mockPromptTemplate = new Mock<IPromptTemplate>();
        var mockGuildService = new Mock<IGuildService>();

        var options = new AssistantOptions
        {
            Sampling = new()
            {
                Model = "anthropic/claude-sonnet-4",
                MaxTokens = 512,
                Temperature = 0.7
            },
            Tools = new()
            {
                MaxToolCallsPerQuestion = 5
            },
            Messages = new()
            {
                MaxResponseLength = 1800,
                TruncationSuffix = "\n\n... *(response truncated)*"
            },
            Cost = new()
            {
                EnableCostTracking = false
            },
            Privacy = new()
            {
                LogInteractions = false
            }
        };

        return new GuildAssistantContext(
            TestGuildId,
            channelId: 1,
            TestUserId,
            messageId: 1,
            rateLimit: 5,
            question: "question",
            toolRegistry: null,
            mockGuildService.Object,
            mockPromptTemplate.Object,
            Mock.Of<IAssistantUsageMetricsRepository>(),
            Mock.Of<IAssistantInteractionLogRepository>(),
            options,
            Mock.Of<ILogger>(),
            model);
    }

    private static DmAssistantContext BuildDmContext(out Mock<IPromptTemplate> mockPromptTemplate)
    {
        mockPromptTemplate = new Mock<IPromptTemplate>();

        var options = new DmAssistantOptions
        {
            Model = "anthropic/claude-sonnet-4",
            MaxTokens = 4096,
            Temperature = 0.7,
            MaxResponseLength = 50000,
            TruncationSuffix = "\n\n... *(response truncated)*",
            EnableCostTracking = false,
            LogInteractions = false
        };

        return new DmAssistantContext(
            TestUserId,
            activeGuildId: null,
            Mock.Of<IToolRegistry>(),
            new List<LlmMessage>(),
            mockPromptTemplate.Object,
            Mock.Of<IDmConversationMessageRepository>(),
            Mock.Of<IDmAssistantInteractionLogRepository>(),
            Mock.Of<IDmAssistantUsageMetricsRepository>(),
            options,
            Mock.Of<ILogger>(),
            "test/resolved-model");
    }

    #endregion

    #region Usage ledger wiring

    [Fact]
    public async Task GuildAssistantContext_RecordUsageAsync_SetsInteractionLogId_ThenRecords()
    {
        var mockGuildService = new Mock<IGuildService>();
        var mockPromptTemplate = new Mock<IPromptTemplate>();
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
            toolRegistry: null, mockGuildService.Object, mockPromptTemplate.Object,
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
        var mockPromptTemplate = new Mock<IPromptTemplate>();
        var mockInteractionLogRepo = new Mock<IAssistantInteractionLogRepository>();
        var mockRecorder = new Mock<ILlmUsageRecorder>();

        var options = new AssistantOptions
        {
            Privacy = new() { LogInteractions = false },
            Cost = new() { EnableCostTracking = false }
        };

        var context = new GuildAssistantContext(
            TestGuildId, channelId: 1, TestUserId, messageId: 1, rateLimit: 5, question: "q",
            toolRegistry: null, mockGuildService.Object, mockPromptTemplate.Object,
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
        var mockPromptTemplate = new Mock<IPromptTemplate>();
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
            TestUserId, activeGuildId: null, Mock.Of<IToolRegistry>(), new List<LlmMessage>(),
            mockPromptTemplate.Object, mockConversationRepo.Object, mockInteractionLogRepo.Object,
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
