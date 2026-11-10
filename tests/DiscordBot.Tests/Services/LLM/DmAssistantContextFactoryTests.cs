using DiscordBot.Core.Configuration;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Infrastructure.Abstractions.LLM;
using cpike.Agent;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="DmAssistantContextFactory"/>: verifies the model slug and pricing
/// resolved via <see cref="ILlmModelResolver"/> land on the created <see cref="IAssistantContext"/>,
/// and that <c>CostRates</c> falls back to the configured rate per-field when the catalog pricing
/// is only partially populated.
/// </summary>
public class DmAssistantContextFactoryTests
{
    private static DmAssistantContextFactory BuildFactory(
        ILlmModelResolver modelResolver,
        DmAssistantOptions? options = null,
        IEnumerable<DmConversationMessage>? history = null,
        IMemoryCache? cache = null,
        IDmSkillActivationStore? skills = null)
    {
        var mockConversationRepo = new Mock<IDmConversationMessageRepository>();
        mockConversationRepo
            .Setup(r => r.GetRecentByUserAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(history ?? Enumerable.Empty<DmConversationMessage>());

        return new DmAssistantContextFactory(
            NullLoggerFactory.Instance,
            mockConversationRepo.Object,
            Mock.Of<IDmAssistantInteractionLogRepository>(),
            Mock.Of<IDmAssistantUsageMetricsRepository>(),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            modelResolver,
            Options.Create(options ?? new DmAssistantOptions()),
            Mock.Of<ILlmUsageRecorder>(),
            skills ?? new DmSkillActivationStore(new MemoryCache(new MemoryCacheOptions())));
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

    [Fact]
    public async Task CreateAsync_SeedsTheStoredHistoryAsConversationTurns_OldestFirst()
    {
        var history = new[]
        {
            new DmConversationMessage { UserId = 1, Role = "user", Content = "first" },
            new DmConversationMessage { UserId = 1, Role = "assistant", Content = "second" }
        };

        var context = await BuildFactory(StubModelResolver(), history: history)
            .CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);

        context.History.Should().Equal(new ConversationTurn(true, "first"), new ConversationTurn(false, "second"));
        context.ModeKey.Should().Be(DmAssistantContext.ModeName);
    }

    [Fact]
    public async Task CreateAsync_CarriesTheUsersStoredSkillsAsPreloaded()
    {
        var skills = new DmSkillActivationStore(new MemoryCache(new MemoryCacheOptions()));
        skills.Set(1, new[] { "moderation" });

        var context = await BuildFactory(StubModelResolver(), skills: skills)
            .CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);

        context.PreloadedSkills.Should().Equal("moderation");
    }

    [Fact]
    public async Task CreateAsync_FallsBackToTheCachedActiveGuild()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set<ulong?>(DmAssistantContextFactory.ActiveGuildCacheKeyPrefix + 1, 77UL);

        var context = await BuildFactory(StubModelResolver(), cache: cache)
            .CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);

        context.Identity.UserId.Should().Be(1UL);
        context.Identity.ActiveGuildId.Should().Be(77UL);
        context.Identity.CanMutate.Should().BeTrue("the DM assistant is owner-only");
    }

    [Fact]
    public async Task CreateAsync_AnExplicitActiveGuild_WinsOverTheCachedOne()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set<ulong?>(DmAssistantContextFactory.ActiveGuildCacheKeyPrefix + 1, 77UL);

        var context = await BuildFactory(StubModelResolver(), cache: cache)
            .CreateAsync(userId: 1, activeGuildId: 88UL, CancellationToken.None);

        context.Identity.ActiveGuildId.Should().Be(88UL);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task CreateAsync_DisablesExecutePython_OnlyWhenCodeExecutionIsOff(bool enabled, bool disabled)
    {
        var context = await BuildFactory(StubModelResolver(), new DmAssistantOptions { EnableCodeExecution = enabled })
            .CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);

        context.DisabledTools.Contains("execute_python").Should().Be(disabled);
    }

    [Fact]
    public async Task CreateAsync_PutsResolvedSlugAndPricing_OnCreatedContext()
    {
        var mockResolver = new Mock<ILlmModelResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(LlmMode.DmAssistant, It.IsAny<CancellationToken>()))
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

        var context = await factory.CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);

        context.Model.Should().Be("anthropic/claude-opus-4");
        context.CostRates.InputPerMillion.Should().Be(5m);
        context.CostRates.OutputPerMillion.Should().Be(25m);
        context.CostRates.CachedPerMillion.Should().Be(0.5m);
        context.CostRates.CacheWritePerMillion.Should().Be(6.25m);
    }

    [Fact]
    public async Task CostRates_FallsBackPerField_WhenCatalogPricingOnlyPartiallyPopulated()
    {
        var options = new DmAssistantOptions
        {
            CostPerMillionInputTokens = 3m,
            CostPerMillionOutputTokens = 15m,
            CostPerMillionCachedTokens = 0.3m,
            CostPerMillionCacheWriteTokens = 3.75m
        };

        var mockResolver = new Mock<ILlmModelResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(LlmMode.DmAssistant, It.IsAny<CancellationToken>()))
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

        var context = await factory.CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);

        context.CostRates.InputPerMillion.Should().Be(4m, "the catalog reported this price");
        context.CostRates.OutputPerMillion.Should().Be(15m, "the catalog did not report this price");
        context.CostRates.CachedPerMillion.Should().Be(0.3m, "the catalog did not report this price");
        context.CostRates.CacheWritePerMillion.Should().Be(3.75m, "the catalog did not report this price");
    }
}
