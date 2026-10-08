using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts.Enums;
using DiscordBot.Core.Entities;
using DiscordBot.Core.DTOs.Llm;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Agents;
using DiscordBot.Tests.TestHelpers;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="GuildAssistantContextFactory"/>: verifies the model slug and pricing
/// resolved via <see cref="ILlmModelResolver"/> land on the created <see cref="IAssistantContext"/>,
/// and that <c>CostRates</c> falls back to the configured rate per-field when the catalog pricing
/// is only partially populated. Also covers the per-guild tool allow-list being applied as a
/// <see cref="FilteredToolRegistry"/> decorator.
/// </summary>
public class GuildAssistantContextFactoryTests
{
    private static IToolAccessResolver StubToolAccess(params string[] allowed)
    {
        var mock = new Mock<IToolAccessResolver>();
        mock.Setup(r => r.ResolveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)allowed.ToHashSet(StringComparer.OrdinalIgnoreCase));
        return mock.Object;
    }

    private static GuildAssistantContextFactory BuildFactory(
        ILlmModelResolver modelResolver,
        AssistantOptions? options = null,
        IToolAccessResolver? toolAccess = null,
        StubSkillSessionFactory? skillSessions = null,
        IAssistantThreadRepository? threads = null,
        IAssistantThreadMessageRepository? threadMessages = null)
    {
        var mockGuildService = new Mock<IGuildService>();
        var mockPromptTemplate = new Mock<IPromptTemplate>();
        var mockToolRegistry = new Mock<IToolRegistry>();

        return new GuildAssistantContextFactory(
            mockGuildService.Object,
            mockPromptTemplate.Object,
            mockToolRegistry.Object,
            Mock.Of<IAssistantUsageMetricsRepository>(),
            Mock.Of<IAssistantInteractionLogRepository>(),
            modelResolver,
            toolAccess ?? StubToolAccess(),
            Mock.Of<ILogger<GuildAssistantContext>>(),
            Options.Create(options ?? new AssistantOptions()),
            Mock.Of<ILlmUsageRecorder>(),
            skillSessions ?? new StubSkillSessionFactory(),
            threads,
            threadMessages);
    }

    #region Threads

    private const ulong ThreadId = 777UL;

    private static GuildAssistantRequest ThreadRequest(ulong? threadId = ThreadId) =>
        new(GuildId: 42, ChannelId: threadId ?? 2, ParentChannelId: threadId is null ? null : 2, ThreadId: threadId,
            UserId: 3, MessageId: 4, Question: "and then?");

    private static (Mock<IAssistantThreadRepository> Threads, Mock<IAssistantThreadMessageRepository> Messages) ThreadRepos(
        AssistantThread? thread, params AssistantThreadMessage[] turns)
    {
        var threads = new Mock<IAssistantThreadRepository>();
        threads.Setup(r => r.GetByThreadIdAsync(ThreadId, It.IsAny<CancellationToken>())).ReturnsAsync(thread);
        var messages = new Mock<IAssistantThreadMessageRepository>();
        messages.Setup(r => r.GetRecentByThreadAsync(ThreadId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<AssistantThreadMessage>)turns.ToList());
        return (threads, messages);
    }

    [Fact]
    public async Task CreateAsync_SingleReply_SeedsNoHistory_AndPreActivatesNothing()
    {
        var skills = new StubSkillSessionFactory();
        var (threads, messages) = ThreadRepos(null);
        var factory = BuildFactory(StubModelResolver(), skillSessions: skills, threads: threads.Object, threadMessages: messages.Object);

        var context = await factory.CreateAsync(ThreadRequest(threadId: null), rateLimit: 5);

        context.ConversationHistory.Should().BeEmpty();
        skills.LastPreActivatedKeys.Should().BeEmpty();
        threads.Verify(r => r.GetByThreadIdAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ThreadTurn_SeedsTheRecentTurnsInOrder_WithinTheWindow()
    {
        var thread = new AssistantThread { ThreadId = ThreadId, GuildId = 42 };
        var (threads, messages) = ThreadRepos(thread,
            new AssistantThreadMessage { Role = "user", Content = "first" },
            new AssistantThreadMessage { Role = "assistant", Content = "reply" },
            new AssistantThreadMessage { Role = "user", Content = "second" });
        var options = new AssistantOptions { Threads = new() { MaxConversationMessages = 3 } };
        var factory = BuildFactory(StubModelResolver(), options, threads: threads.Object, threadMessages: messages.Object);

        var context = await factory.CreateAsync(ThreadRequest(), rateLimit: 5);

        context.ConversationHistory.Select(m => (m.Role, m.Content)).Should().Equal(
            (LlmRole.User, "first"), (LlmRole.Assistant, "reply"), (LlmRole.User, "second"));
        messages.Verify(r => r.GetRecentByThreadAsync(ThreadId, 3, It.IsAny<CancellationToken>()), Times.Once,
            "the window is the configured size");
        context.ExecutionContext.ChannelId.Should().Be(ThreadId, "a tool posting to the current channel posts in the thread");
    }

    [Fact]
    public async Task CreateAsync_ThreadTurn_ReplaysTheThreadsLoadedSkills()
    {
        var thread = new AssistantThread { ThreadId = ThreadId, GuildId = 42 };
        thread.SetActiveSkillsList(new[] { "moderation" });
        var skills = new StubSkillSessionFactory();
        var (threads, messages) = ThreadRepos(thread);
        var factory = BuildFactory(StubModelResolver(), skillSessions: skills, threads: threads.Object, threadMessages: messages.Object);

        await factory.CreateAsync(ThreadRequest(), rateLimit: 5);

        skills.LastPreActivatedKeys.Should().Equal("moderation");
    }

    [Fact]
    public async Task CreateAsync_ThreadTurn_WithNoThreadRow_FallsBackToASingleReply()
    {
        // The row was retained away, or the bot restarted mid-create: answer, do not refuse.
        var (threads, messages) = ThreadRepos(null);
        var factory = BuildFactory(StubModelResolver(), threads: threads.Object, threadMessages: messages.Object);

        var context = await factory.CreateAsync(ThreadRequest(), rateLimit: 5);

        context.ConversationHistory.Should().BeEmpty();
        messages.Verify(r => r.GetRecentByThreadAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ThreadTurn_WithoutThreadRepositories_IsASingleReply()
    {
        var factory = BuildFactory(StubModelResolver());

        var context = await factory.CreateAsync(ThreadRequest(), rateLimit: 5);

        context.ConversationHistory.Should().BeEmpty();
    }

    #endregion

    [Fact]
    public async Task CreateAsync_PutsResolvedSlugAndPricing_OnCreatedContext()
    {
        var mockResolver = new Mock<ILlmModelResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(LlmMode.GuildAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = "anthropic/claude-opus-4",
                Source = LlmModelResolutionSource.Database,
                ConfiguredSlug = "anthropic/claude-opus-4",
                IsEnabled = true,
                IsAvailable = true,
                Pricing = new LlmCatalogPricing
                {
                    PromptPricePerMillion = 5m,
                    CompletionPricePerMillion = 25m,
                    CacheReadPricePerMillion = 0.5m,
                    CacheWritePricePerMillion = 6.25m
                }
            });

        var factory = BuildFactory(mockResolver.Object);

        var context = await factory.CreateAsync(
            guildId: 1, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi");

        context.Model.Should().Be("anthropic/claude-opus-4");
        context.CostRates.InputPerMillion.Should().Be(5m);
        context.CostRates.OutputPerMillion.Should().Be(25m);
        context.CostRates.CachedPerMillion.Should().Be(0.5m);
        context.CostRates.CacheWritePerMillion.Should().Be(6.25m);
    }

    [Fact]
    public async Task CostRates_FallsBackPerField_WhenCatalogPricingOnlyPartiallyPopulated()
    {
        var options = new AssistantOptions
        {
            Cost = new()
            {
                CostPerMillionInputTokens = 3m,
                CostPerMillionOutputTokens = 15m,
                CostPerMillionCachedTokens = 0.3m,
                CostPerMillionCacheWriteTokens = 3.75m
            }
        };

        var mockResolver = new Mock<ILlmModelResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(LlmMode.GuildAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = "anthropic/claude-sonnet-4",
                Source = LlmModelResolutionSource.Configuration,
                ConfiguredSlug = "anthropic/claude-sonnet-4",
                Pricing = new LlmCatalogPricing
                {
                    // Only the prompt price is known to the catalog; every other field is null and
                    // must fall back to the configured rate.
                    PromptPricePerMillion = 4m
                }
            });

        var factory = BuildFactory(mockResolver.Object, options);

        var context = await factory.CreateAsync(
            guildId: 1, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi");

        context.CostRates.InputPerMillion.Should().Be(4m, "the catalog reported this price");
        context.CostRates.OutputPerMillion.Should().Be(15m, "the catalog did not report this price");
        context.CostRates.CachedPerMillion.Should().Be(0.3m, "the catalog did not report this price");
        context.CostRates.CacheWritePerMillion.Should().Be(3.75m, "the catalog did not report this price");
    }

    [Fact]
    public async Task CreateAsync_WrapsTheRegistryInTheGuildAllowList()
    {
        var factory = BuildFactory(
            StubModelResolver(),
            new AssistantOptions { Tools = new() { EnableDocumentationTools = true } },
            StubToolAccess("list_features"));

        var context = await factory.CreateAsync(
            guildId: 42, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi");

        context.ToolRegistry.Should().BeOfType<FilteredToolRegistry>()
            .Which.AllowedTools.Should().BeEquivalentTo(new[] { "list_features" });
    }

    [Fact]
    public async Task CreateAsync_ResolvesTheAllowListForTheAskingGuild()
    {
        var toolAccess = new Mock<IToolAccessResolver>();
        toolAccess
            .Setup(r => r.ResolveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlySet<string>)new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        var factory = BuildFactory(
            StubModelResolver(),
            new AssistantOptions { Tools = new() { EnableDocumentationTools = true } },
            toolAccess.Object);

        await factory.CreateAsync(
            guildId: 42, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi");

        toolAccess.Verify(r => r.ResolveAsync(42UL, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_LeavesTheRegistryNull_WhenToolsAreTurnedOffEntirely()
    {
        var toolAccess = new Mock<IToolAccessResolver>();

        var factory = BuildFactory(
            StubModelResolver(),
            new AssistantOptions { Tools = new() { EnableDocumentationTools = false } },
            toolAccess.Object);

        var context = await factory.CreateAsync(
            guildId: 42, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi");

        context.ToolRegistry.Should().BeNull();
        toolAccess.Verify(
            r => r.ResolveAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateAsync_PutsTheCallersWriteAccessOnTheToolContext(bool callerCanMutate)
    {
        var factory = BuildFactory(StubModelResolver());

        var context = await factory.CreateAsync(
            guildId: 42, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi",
            callerCanMutate: callerCanMutate);

        context.ExecutionContext.CanMutate.Should().Be(callerCanMutate);
    }

    [Fact]
    public async Task CreateAsync_DefaultsToReadOnly_WhenTheCallerWasNeverAssessed()
    {
        // The safe default has to be the one you get by forgetting the argument.
        var factory = BuildFactory(StubModelResolver());

        var context = await factory.CreateAsync(
            guildId: 42, channelId: 2, userId: 3, messageId: 4, rateLimit: 5, question: "hi");

        context.ExecutionContext.CanMutate.Should().BeFalse();
    }

    private static ILlmModelResolver StubModelResolver()
    {
        var mock = new Mock<ILlmModelResolver>();
        mock.Setup(r => r.ResolveAsync(It.IsAny<LlmMode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = "anthropic/claude-sonnet-4",
                Source = LlmModelResolutionSource.Configuration,
                ConfiguredSlug = "anthropic/claude-sonnet-4"
            });
        return mock.Object;
    }
}
