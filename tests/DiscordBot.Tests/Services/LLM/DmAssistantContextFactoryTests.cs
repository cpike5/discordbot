using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
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
        ILlmModelResolver modelResolver, DmAssistantOptions? options = null)
    {
        var mockConversationRepo = new Mock<IDmConversationMessageRepository>();
        mockConversationRepo
            .Setup(r => r.GetRecentByUserAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<DmConversationMessage>());

        return new DmAssistantContextFactory(
            Enumerable.Empty<IDmToolProvider>(),
            NullLoggerFactory.Instance,
            Mock.Of<IPromptTemplate>(),
            mockConversationRepo.Object,
            Mock.Of<IDmAssistantInteractionLogRepository>(),
            Mock.Of<IDmAssistantUsageMetricsRepository>(),
            new MemoryCache(new MemoryCacheOptions()),
            modelResolver,
            Options.Create(options ?? new DmAssistantOptions()));
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
