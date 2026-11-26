using DiscordBot.Core.Configuration;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;
using DiscordBot.Infrastructure.Abstractions.LLM;
using DiscordBot.Tests.TestHelpers;

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
        ILlmModelResolver modelResolver, DmAssistantOptions? options = null,
        ISettingsService? settingsService = null, IPromptTemplate? promptTemplate = null)
    {
        var mockConversationRepo = new Mock<IDmConversationMessageRepository>();
        mockConversationRepo
            .Setup(r => r.GetRecentByUserAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<DmConversationMessage>());

        return new DmAssistantContextFactory(
            Enumerable.Empty<IDmToolProvider>(),
            NullLoggerFactory.Instance,
            promptTemplate ?? Mock.Of<IPromptTemplate>(),
            mockConversationRepo.Object,
            Mock.Of<IDmAssistantInteractionLogRepository>(),
            Mock.Of<IDmAssistantUsageMetricsRepository>(),
            new MemoryCache(new MemoryCacheOptions()),
            modelResolver,
            Options.Create(options ?? new DmAssistantOptions()),
            Mock.Of<ILlmUsageRecorder>(),
            new StubSkillSessionFactory(),
            new DmSkillActivationStore(new MemoryCache(new MemoryCacheOptions())),
            settingsService ?? Mock.Of<ISettingsService>());
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

    private static Mock<ILlmModelResolver> DefaultResolver()
    {
        var mockResolver = new Mock<ILlmModelResolver>();
        mockResolver
            .Setup(r => r.ResolveAsync(LlmMode.DmAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = "test/model",
                Source = LlmModelResolutionSource.Database,
                ConfiguredSlug = "test/model",
                IsEnabled = true,
                IsAvailable = true
            });
        return mockResolver;
    }

    private static IPromptTemplate BasePrompt(string text)
    {
        var mock = new Mock<IPromptTemplate>();
        mock.Setup(p => p.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(text);
        return mock.Object;
    }

    [Fact]
    public async Task CreateAsync_WithCustomInstructionsSetting_AppendsThemToSystemPrompt()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetSettingValueAsync<string>(
                DiscordBot.Infrastructure.Services.SettingDefinitions.DmAssistantCustomInstructionsKey,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("  Answer in bullet points.  ");

        var factory = BuildFactory(DefaultResolver().Object, settingsService: settings.Object,
            promptTemplate: BasePrompt("Base prompt."));

        var context = await factory.CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);
        var prompt = await context.BuildSystemPromptAsync(CancellationToken.None);

        prompt.Should().StartWith("Base prompt.");
        prompt.Should().Contain(DmAssistantContext.CustomInstructionsHeading);
        prompt.Should().EndWith("Answer in bullet points.");
    }

    [Fact]
    public async Task CreateAsync_WithBlankCustomInstructions_LeavesSystemPromptUnchanged()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetSettingValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");

        var factory = BuildFactory(DefaultResolver().Object, settingsService: settings.Object,
            promptTemplate: BasePrompt("Base prompt."));

        var context = await factory.CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);
        var prompt = await context.BuildSystemPromptAsync(CancellationToken.None);

        prompt.Should().Be("Base prompt.");
    }

    [Fact]
    public async Task CreateAsync_WhenSettingsReadFails_StillBuildsPromptWithoutInstructions()
    {
        var settings = new Mock<ISettingsService>();
        settings
            .Setup(s => s.GetSettingValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db down"));

        var factory = BuildFactory(DefaultResolver().Object, settingsService: settings.Object,
            promptTemplate: BasePrompt("Base prompt."));

        var context = await factory.CreateAsync(userId: 1, activeGuildId: null, CancellationToken.None);
        var prompt = await context.BuildSystemPromptAsync(CancellationToken.None);

        prompt.Should().Be("Base prompt.");
    }
}
