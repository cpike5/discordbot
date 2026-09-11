using DiscordBot.Bot.Controllers;
using DiscordBot.Bot.Middleware;
using DiscordBot.Core.DTOs;
using DiscordBot.Agents.Contracts;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Agents.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// Unit tests for <see cref="LlmModelsController"/>: catalog listing, refresh, enable/disable, and
/// per-mode default resolution (delegated to <see cref="ILlmModelResolver"/>).
/// </summary>
[Trait("Category", "Unit")]
public class LlmModelsControllerTests
{
    private readonly Mock<ILlmModelCatalogService> _mockCatalogService;
    private readonly Mock<ILlmModelResolver> _mockModelResolver;
    private readonly LlmModelsController _controller;

    public LlmModelsControllerTests()
    {
        _mockCatalogService = new Mock<ILlmModelCatalogService>();
        _mockModelResolver = new Mock<ILlmModelResolver>();

        _controller = new LlmModelsController(
            _mockCatalogService.Object,
            _mockModelResolver.Object,
            Mock.Of<ILogger<LlmModelsController>>());

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        _controller.HttpContext.Items[CorrelationIdMiddleware.ItemKey] = "test-correlation-id";
    }

    private static LlmModel CreateModel(
        string id = "anthropic/claude-sonnet-4.6",
        string vendor = "anthropic",
        bool enabled = false,
        bool available = true,
        bool supportsTools = true)
    {
        return new LlmModel
        {
            Id = id,
            Name = "Claude Sonnet 4.6",
            Description = "A model",
            Vendor = vendor,
            ContextLength = 200000,
            PromptPricePerMillion = 3.00m,
            CompletionPricePerMillion = 15.00m,
            SupportsTools = supportsTools,
            SupportsImages = true,
            IsAvailable = available,
            IsEnabled = enabled,
            FirstSeenAt = DateTime.UtcNow,
            LastSeenAt = DateTime.UtcNow
        };
    }

    #region GetCatalog

    [Fact]
    public async Task GetCatalog_ShouldReturnOkWithMappedModelsAndVendors()
    {
        var models = new List<LlmModel>
        {
            CreateModel(id: "anthropic/claude-sonnet-4.6", vendor: "anthropic", enabled: true),
            CreateModel(id: "openai/gpt-5", vendor: "openai")
        };

        _mockCatalogService
            .Setup(s => s.GetCatalogAsync(It.IsAny<LlmModelCatalogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(models);
        _mockCatalogService
            .Setup(s => s.GetVendorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "anthropic", "openai" });
        _mockCatalogService
            .Setup(s => s.GetLastRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await _controller.GetCatalog(
            search: null, vendor: null, enabledOnly: false, availableOnly: false, toolsOnly: false,
            sortBy: LlmModelSortBy.Name, descending: false, cancellationToken: CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmModelListResponseDto;

        response.Should().NotBeNull();
        response!.Models.Should().HaveCount(2);
        response.Models.Should().Contain(m => m.Slug == "anthropic/claude-sonnet-4.6" && m.IsEnabled);
        response.Vendors.Should().BeEquivalentTo(new[] { "anthropic", "openai" });
        response.LastRefreshAt.Should().Be(new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task GetCatalog_ShouldReturnEmptyModels_WhenCatalogIsEmpty()
    {
        _mockCatalogService
            .Setup(s => s.GetCatalogAsync(It.IsAny<LlmModelCatalogFilter>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<LlmModel>());
        _mockCatalogService
            .Setup(s => s.GetVendorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());
        _mockCatalogService
            .Setup(s => s.GetLastRefreshAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((DateTime?)null);

        var result = await _controller.GetCatalog(
            search: null, vendor: null, enabledOnly: false, availableOnly: false, toolsOnly: false,
            sortBy: LlmModelSortBy.Name, descending: false, cancellationToken: CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmModelListResponseDto;

        response!.Models.Should().BeEmpty();
        response.Vendors.Should().BeEmpty();
        response.LastRefreshAt.Should().BeNull();
    }

    #endregion

    #region Refresh

    [Fact]
    public async Task Refresh_ShouldReturnOkWithCounts_WhenServiceSucceeds()
    {
        var expected = new LlmCatalogRefreshResult { Added = 5, Updated = 2, Removed = 1, FetchedAt = DateTime.UtcNow };

        _mockCatalogService
            .Setup(s => s.RefreshAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _controller.Refresh(CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = result.Result as OkObjectResult;
        ok!.Value.Should().BeEquivalentTo(expected);
    }

    #endregion

    #region SetEnabled

    [Fact]
    public async Task SetEnabled_ShouldReturnOk_WhenServiceSucceeds()
    {
        _mockCatalogService
            .Setup(s => s.SetEnabledAsync("anthropic/claude-sonnet-4.6", true, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmModelEnableResult.Ok());

        var result = await _controller.SetEnabled(
            new LlmModelSetSlugEnabledDto { Slug = "anthropic/claude-sonnet-4.6", Enabled = true },
            CancellationToken.None);

        result.Result.Should().BeOfType<OkObjectResult>();
        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmModelEnableResult;
        response!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SetEnabled_ShouldReturnBadRequest_WhenServiceFails()
    {
        _mockCatalogService
            .Setup(s => s.SetEnabledAsync(It.IsAny<string>(), false, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(LlmModelEnableResult.Fail("This model is the current default for Guild Assistant."));

        var result = await _controller.SetEnabled(
            new LlmModelSetSlugEnabledDto { Slug = "anthropic/claude-sonnet-4.6", Enabled = false },
            CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        var bad = result.Result as BadRequestObjectResult;
        var error = bad!.Value as ApiErrorDto;
        error!.Message.Should().Contain("default for Guild Assistant");
    }

    [Fact]
    public async Task SetEnabled_ShouldReturnBadRequest_WhenSlugIsMissing()
    {
        var result = await _controller.SetEnabled(
            new LlmModelSetSlugEnabledDto { Slug = "", Enabled = true },
            CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _mockCatalogService.Verify(
            s => s.SetEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetEnabled_ShouldReturnBadRequest_WhenSlugExceedsMaxLength()
    {
        var overlongSlug = new string('a', 201);

        var result = await _controller.SetEnabled(
            new LlmModelSetSlugEnabledDto { Slug = overlongSlug, Enabled = true },
            CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _mockCatalogService.Verify(
            s => s.SetEnabledAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    #endregion

    #region GetDefaults

    [Fact]
    public async Task GetDefaults_ShouldUseConfigValue_WhenResolverReportsConfiguration()
    {
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.GuildAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = "anthropic/claude-sonnet-4",
                Source = LlmModelResolutionSource.Configuration,
                ConfiguredSlug = "anthropic/claude-sonnet-4"
            });
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.DmAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "anthropic/claude-sonnet-4", Source = LlmModelResolutionSource.Configuration, ConfiguredSlug = "anthropic/claude-sonnet-4" });
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "anthropic/claude-sonnet-4", Source = LlmModelResolutionSource.Configuration, ConfiguredSlug = "anthropic/claude-sonnet-4" });

        var result = await _controller.GetDefaults(CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmModelDefaultsResponseDto;

        response!.Modes.Should().HaveCount(3);
        var guildMode = response.Modes.Single(m => m.Mode == "GuildAssistant");
        guildMode.Source.Should().Be(LlmModelDefaultSource.Config);
        guildMode.Slug.Should().Be("anthropic/claude-sonnet-4");
        guildMode.SettingKey.Should().Be("Assistant:Sampling:Model");
        guildMode.IsKnown.Should().BeFalse();
    }

    [Fact]
    public async Task GetDefaults_ShouldMapDatabaseSourceAndCatalogState_WhenResolverReportsDbOverride()
    {
        var dbSlug = "openai/gpt-5";

        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.GuildAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = dbSlug,
                Source = LlmModelResolutionSource.Database,
                ConfiguredSlug = "anthropic/claude-sonnet-4",
                IsEnabled = true,
                IsAvailable = true
            });
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.DmAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "anthropic/claude-sonnet-4", Source = LlmModelResolutionSource.Configuration, ConfiguredSlug = "anthropic/claude-sonnet-4" });
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "anthropic/claude-sonnet-4", Source = LlmModelResolutionSource.Configuration, ConfiguredSlug = "anthropic/claude-sonnet-4" });

        var result = await _controller.GetDefaults(CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmModelDefaultsResponseDto;

        var guildMode = response!.Modes.Single(m => m.Mode == "GuildAssistant");
        guildMode.Source.Should().Be(LlmModelDefaultSource.Db);
        guildMode.Slug.Should().Be(dbSlug);
        guildMode.IsKnown.Should().BeTrue();
        guildMode.IsEnabled.Should().BeTrue();
        guildMode.IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task GetDefaults_ShouldMapFallbackSource_WhenResolverReportsFallback()
    {
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.GuildAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel
            {
                Slug = "openrouter/default",
                Source = LlmModelResolutionSource.Fallback,
                ConfiguredSlug = "openrouter/default"
            });
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.DmAssistant, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "anthropic/claude-sonnet-4", Source = LlmModelResolutionSource.Configuration, ConfiguredSlug = "anthropic/claude-sonnet-4" });
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "anthropic/claude-sonnet-4", Source = LlmModelResolutionSource.Configuration, ConfiguredSlug = "anthropic/claude-sonnet-4" });

        var result = await _controller.GetDefaults(CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        var response = ok!.Value as LlmModelDefaultsResponseDto;

        var guildMode = response!.Modes.Single(m => m.Mode == "GuildAssistant");
        guildMode.Source.Should().Be(LlmModelDefaultSource.Fallback);
        guildMode.Slug.Should().Be("openrouter/default");
    }

    #endregion
}
