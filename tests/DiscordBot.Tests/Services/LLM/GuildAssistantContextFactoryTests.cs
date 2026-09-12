using DiscordBot.Core.Configuration;
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
        IToolAccessResolver? toolAccess = null)
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
            new StubSkillSessionFactory());
    }

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
