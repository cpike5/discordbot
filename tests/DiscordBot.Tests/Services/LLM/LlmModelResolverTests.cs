using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Services.LLM;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Services.LLM;

/// <summary>
/// Unit tests for <see cref="LlmModelResolver"/>: DB -> config -> fallback precedence, per-mode
/// caching, cache invalidation on <see cref="ISettingsService.SettingsChanged"/>, and the
/// not-enabled warning path.
/// </summary>
public class LlmModelResolverTests
{
    private readonly Mock<ISettingsService> _mockSettingsService = new();
    private readonly Mock<ILlmModelRepository> _mockRepository = new();
    private readonly IOptions<AssistantOptions> _assistantOptions =
        Options.Create(new AssistantOptions { Sampling = new() { Model = "config/guild-model" } });
    private readonly IOptions<DmAssistantOptions> _dmAssistantOptions =
        Options.Create(new DmAssistantOptions { Model = "config/dm-model" });
    private readonly IOptions<FeatureRequestsOptions> _featureRequestsOptions =
        Options.Create(new FeatureRequestsOptions { RequirementsGatheringModel = "config/fr-model" });
    private readonly IOptions<OpenRouterOptions> _openRouterOptions =
        Options.Create(new OpenRouterOptions { DefaultModel = "fallback/model" });

    private LlmModelResolver BuildResolver()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        var provider = services.BuildServiceProvider();

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        return new LlmModelResolver(
            mockScopeFactory.Object,
            _mockSettingsService.Object,
            _assistantOptions,
            _dmAssistantOptions,
            _featureRequestsOptions,
            _openRouterOptions,
            NullLogger<LlmModelResolver>.Instance);
    }

    [Fact]
    public async Task ResolveAsync_UsesDatabaseValue_WhenStoredValuePresent()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync("Assistant:Sampling:Model", It.IsAny<CancellationToken>()))
            .ReturnsAsync("db/guild-model");
        _mockRepository
            .Setup(r => r.GetByIdAsync("db/guild-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();
        var result = await resolver.ResolveAsync(LlmMode.GuildAssistant);

        result.Slug.Should().Be("db/guild-model");
        result.Source.Should().Be(LlmModelResolutionSource.Database);
    }

    [Fact]
    public async Task ResolveAsync_UsesConfiguredValue_WhenNoDbOverride()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();
        var result = await resolver.ResolveAsync(LlmMode.DmAssistant);

        result.Slug.Should().Be("config/dm-model");
        result.Source.Should().Be(LlmModelResolutionSource.Configuration);
    }

    [Fact]
    public async Task ResolveAsync_UsesFallback_WhenNeitherDbNorConfigPresent()
    {
        var options = Options.Create(new FeatureRequestsOptions { RequirementsGatheringModel = "" });

        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        var provider = services.BuildServiceProvider();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        var resolver = new LlmModelResolver(
            mockScopeFactory.Object,
            _mockSettingsService.Object,
            _assistantOptions,
            _dmAssistantOptions,
            options,
            _openRouterOptions,
            NullLogger<LlmModelResolver>.Instance);

        var result = await resolver.ResolveAsync(LlmMode.FeatureRequests);

        result.Slug.Should().Be("fallback/model");
        result.Source.Should().Be(LlmModelResolutionSource.Fallback);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsCatalogEnabledAndPricing_WhenCatalogRowExists()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync("config/guild-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmModel
            {
                Id = "config/guild-model",
                IsEnabled = true,
                IsAvailable = true,
                PromptPricePerMillion = 1.5m,
                CompletionPricePerMillion = 6m
            });

        var resolver = BuildResolver();
        var result = await resolver.ResolveAsync(LlmMode.GuildAssistant);

        result.IsEnabled.Should().BeTrue();
        result.IsAvailable.Should().BeTrue();
        result.Pricing.Should().NotBeNull();
        result.Pricing!.PromptPricePerMillion.Should().Be(1.5m);
        result.Pricing.CompletionPricePerMillion.Should().Be(6m);
        result.Pricing.CacheReadPricePerMillion.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_ReturnsNullCatalogState_WhenSlugNeverSeenByCatalog()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();
        var result = await resolver.ResolveAsync(LlmMode.GuildAssistant);

        result.IsEnabled.Should().BeNull();
        result.IsAvailable.Should().BeNull();
        result.Pricing.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_CachesResult_AndDoesNotReQueryUntilInvalidated()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();

        await resolver.ResolveAsync(LlmMode.GuildAssistant);
        await resolver.ResolveAsync(LlmMode.GuildAssistant);

        _mockSettingsService.Verify(
            s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ReResolves_AfterSettingsChangedReportsTheModeKey()
    {
        var callCount = 0;
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync("Assistant:Sampling:Model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ == 0 ? "db/first" : "db/second");
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();

        var first = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        first.Slug.Should().Be("db/first");

        _mockSettingsService.Raise(s => s.SettingsChanged += null, new SettingsChangedEventArgs
        {
            UpdatedKeys = new List<string> { "Assistant:Sampling:Model" },
            UserId = "admin-1"
        });

        var second = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        second.Slug.Should().Be("db/second");
    }

    [Fact]
    public async Task ResolveAsync_DoesNotInvalidate_WhenSettingsChangedReportsUnrelatedKey()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync("Assistant:Sampling:Model", It.IsAny<CancellationToken>()))
            .ReturnsAsync("db/guild-model");
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();
        await resolver.ResolveAsync(LlmMode.GuildAssistant);

        _mockSettingsService.Raise(s => s.SettingsChanged += null, new SettingsChangedEventArgs
        {
            UpdatedKeys = new List<string> { "General:StatusMessage" },
            UserId = "admin-1"
        });

        await resolver.ResolveAsync(LlmMode.GuildAssistant);

        _mockSettingsService.Verify(
            s => s.GetStoredValueAsync("Assistant:Sampling:Model", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ReResolves_AfterResetCategoryRaisesSettingsChanged()
    {
        // A category reset (ResetCategoryAsync/ResetAllAsync) raises SettingsChanged the same way
        // UpdateSettingsAsync does; the resolver must invalidate on it the same way.
        var callCount = 0;
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync("Assistant:Sampling:Model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ == 0 ? "db/first" : null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var resolver = BuildResolver();

        var first = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        first.Slug.Should().Be("db/first");

        // Simulate a reset: the setting row is gone, and the reset raised SettingsChanged with the
        // reset keys (mirroring SettingsService.ResetCategoryAsync/ResetAllAsync).
        _mockSettingsService.Raise(s => s.SettingsChanged += null, new SettingsChangedEventArgs
        {
            UpdatedKeys = new List<string> { "Assistant:Sampling:Model" },
            UserId = "admin-1"
        });

        var second = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        second.Slug.Should().Be("config/guild-model", "the reset fell back to configuration after DB row cleared");
    }

    [Fact]
    public async Task ResolveAsync_ReturnsDecidedSlugWithNullCatalogState_WhenRepositoryThrows()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("DB unavailable"));

        var resolver = BuildResolver();

        var result = await resolver.ResolveAsync(LlmMode.DmAssistant);

        result.Slug.Should().Be("config/dm-model", "the slug decision does not depend on the catalog lookup");
        result.IsEnabled.Should().BeNull();
        result.IsAvailable.Should().BeNull();
        result.Pricing.Should().BeNull();
    }

    [Fact]
    public async Task ResolveAsync_DoesNotCacheStaleResult_WhenSettingsChangedFiresMidResolve()
    {
        // Reproduces the invalidation race directly: the repository lookup (awaited partway
        // through ResolveUncachedAsync) is where we deterministically inject a concurrent settings
        // change, so the resolve that was already in flight - having already decided its slug from
        // data that is stale the instant the change lands - must not re-populate the cache
        // afterwards and silently resurrect the value the invalidation was meant to evict.
        var callCount = 0;
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync("Assistant:Sampling:Model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => callCount++ == 0 ? "db/stale" : "db/fresh");

        var mockRepository = new Mock<ILlmModelRepository>();
        mockRepository
            .Setup(r => r.GetByIdAsync("db/stale", It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                // Fires while the first (soon-to-be-stale) resolve is still in flight, after it
                // already decided on "db/stale" but before it returns and tries to cache.
                _mockSettingsService.Raise(s => s.SettingsChanged += null, new SettingsChangedEventArgs
                {
                    UpdatedKeys = new List<string> { "Assistant:Sampling:Model" },
                    UserId = "admin-1"
                });
                return null;
            });
        mockRepository
            .Setup(r => r.GetByIdAsync("db/fresh", It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var services = new ServiceCollection();
        services.AddSingleton(mockRepository.Object);
        var provider = services.BuildServiceProvider();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        var resolver = new LlmModelResolver(
            mockScopeFactory.Object,
            _mockSettingsService.Object,
            _assistantOptions,
            _dmAssistantOptions,
            _featureRequestsOptions,
            _openRouterOptions,
            NullLogger<LlmModelResolver>.Instance);

        var staleResult = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        staleResult.Slug.Should().Be("db/stale", "the in-flight resolve itself still returns what it read");

        // The invalidation fired mid-resolve must have won: the cache must not hold "db/stale".
        var afterward = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        afterward.Slug.Should().Be("db/fresh", "the stale resolve must not have overwritten the cache");
    }

    [Fact]
    public async Task ResolveAsync_LogsWarningOnce_WhenResolvedSlugIsUnknownToCatalog()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync(It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LlmModel?)null);

        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<LlmModelResolver>>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        var provider = services.BuildServiceProvider();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        var resolver = new LlmModelResolver(
            mockScopeFactory.Object,
            _mockSettingsService.Object,
            _assistantOptions,
            _dmAssistantOptions,
            _featureRequestsOptions,
            _openRouterOptions,
            mockLogger.Object);

        var result = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        result.IsEnabled.Should().BeNull();

        mockLogger.Verify(
            l => l.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_LogsWarningOnce_WhenResolvedSlugIsNotEnabled()
    {
        _mockSettingsService
            .Setup(s => s.GetStoredValueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);
        _mockRepository
            .Setup(r => r.GetByIdAsync("config/guild-model", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmModel { Id = "config/guild-model", IsEnabled = false, IsAvailable = true });

        var mockLogger = new Mock<Microsoft.Extensions.Logging.ILogger<LlmModelResolver>>();

        var services = new ServiceCollection();
        services.AddSingleton(_mockRepository.Object);
        var provider = services.BuildServiceProvider();
        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        var resolver = new LlmModelResolver(
            mockScopeFactory.Object,
            _mockSettingsService.Object,
            _assistantOptions,
            _dmAssistantOptions,
            _featureRequestsOptions,
            _openRouterOptions,
            mockLogger.Object);

        var result = await resolver.ResolveAsync(LlmMode.GuildAssistant);
        result.IsEnabled.Should().BeFalse();

        mockLogger.Verify(
            l => l.Log(
                Microsoft.Extensions.Logging.LogLevel.Warning,
                It.IsAny<Microsoft.Extensions.Logging.EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
