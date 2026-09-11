using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="GuildAssistantContextFactory"/>: verifies the model slug and pricing
/// resolved via <see cref="ILlmModelResolver"/> land on the created <see cref="IAssistantContext"/>,
/// and that <c>CostRates</c> falls back to the configured rate per-field when the catalog pricing
/// is only partially populated.
/// </summary>
public class GuildAssistantContextFactoryTests
{
    private static GuildAssistantContextFactory BuildFactory(
        ILlmModelResolver modelResolver, AssistantOptions? options = null)
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
            Mock.Of<ILogger<GuildAssistantContext>>(),
            Options.Create(options ?? new AssistantOptions()),
            Mock.Of<ILlmUsageRecorder>());
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
}
