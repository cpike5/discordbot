using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Infrastructure.Data.Repositories;
using DiscordBot.Infrastructure.Services.LLM;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Infrastructure.LLM;

/// <summary>
/// Unit tests for LlmModelCatalogService against the real SQLite-in-memory repository: refresh
/// upsert/unavailable behaviour, the first-refresh-only bootstrap, and the SetEnabledAsync guard
/// against disabling a mode's current default.
/// </summary>
public class LlmModelCatalogServiceTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly SqliteConnection _connection;
    private readonly LlmModelRepository _repository;
    private readonly Mock<IOpenRouterModelCatalogClient> _mockCatalogClient;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly IOptions<AssistantOptions> _assistantOptions;
    private readonly IOptions<DmAssistantOptions> _dmAssistantOptions;
    private readonly IOptions<FeatureRequestsOptions> _featureRequestsOptions;
    private readonly LlmModelCatalogService _service;

    public LlmModelCatalogServiceTests()
    {
        (_context, _connection) = TestDbContextFactory.CreateContext();
        _repository = new LlmModelRepository(
            _context, NullLogger<LlmModelRepository>.Instance, NullLogger<Repository<LlmModel>>.Instance);

        _mockCatalogClient = new Mock<IOpenRouterModelCatalogClient>();

        _mockAuditLogService = new Mock<IAuditLogService>();
        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.BySystem()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);

        _mockSettingsService = new Mock<ISettingsService>();
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        _assistantOptions = Options.Create(new AssistantOptions
        {
            Sampling = { Model = "anthropic/claude-sonnet-4" },
        });
        _dmAssistantOptions = Options.Create(new DmAssistantOptions { Model = "anthropic/claude-haiku-4" });
        _featureRequestsOptions = Options.Create(
            new FeatureRequestsOptions { RequirementsGatheringModel = "openai/gpt-4o" });

        _service = new LlmModelCatalogService(
            _repository,
            _mockCatalogClient.Object,
            _mockAuditLogService.Object,
            _mockSettingsService.Object,
            _assistantOptions,
            _dmAssistantOptions,
            _featureRequestsOptions,
            NullLogger<LlmModelCatalogService>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private static LlmCatalogModel Catalog(
        string id, bool tools = true, decimal? promptPrice = 1.0m) => new()
    {
        Id = id,
        Name = id,
        Vendor = id.Split('/')[0],
        ContextLength = 128000,
        SupportsTools = tools,
        PromptPricePerMillion = promptPrice,
    };

    [Fact]
    public async Task RefreshAsync_UpsertsNewAndExistingModels()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4"), Catalog("openai/gpt-4o") });

        var result = await _service.RefreshAsync(userId: null);

        result.Added.Should().Be(2);
        result.Updated.Should().Be(0);
        result.Removed.Should().Be(0);

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(2);
        all.Should().OnlyContain(m => m.IsAvailable);

        // A second refresh with the same set upserts (updates), not re-adds.
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4"), Catalog("openai/gpt-4o") });

        var result2 = await _service.RefreshAsync(userId: null);
        result2.Added.Should().Be(0);
        result2.Updated.Should().Be(2);
    }

    [Fact]
    public async Task RefreshAsync_MarksMissingModelsUnavailableWithoutDeleting()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4"), Catalog("openai/gpt-4o") });
        await _service.RefreshAsync(userId: null);

        // Second refresh no longer returns gpt-4o.
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4") });

        var result = await _service.RefreshAsync(userId: null);

        result.Removed.Should().Be(1);
        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(2, "rows are marked unavailable, never deleted");
        all.Single(m => m.Id == "openai/gpt-4o").IsAvailable.Should().BeFalse();
        all.Single(m => m.Id == "anthropic/claude-sonnet-4").IsAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_NeverChangesIsEnabledOnAlreadyKnownModels()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4") });
        await _service.RefreshAsync(userId: null);

        // Admin enables it explicitly, independent of refresh.
        await _service.SetEnabledAsync("anthropic/claude-sonnet-4", enabled: true, userId: "admin-1");

        // Further refreshes must not touch IsEnabled either way.
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4") });
        await _service.RefreshAsync(userId: null);

        var model = await _repository.GetByIdAsync("anthropic/claude-sonnet-4");
        model!.IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task RefreshAsync_BootstrapsOnlyOnFirstEverRefresh()
    {
        // First refresh: table is empty, so the three configured slugs get enabled.
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Catalog("anthropic/claude-sonnet-4"),
                Catalog("anthropic/claude-haiku-4"),
                Catalog("openai/gpt-4o"),
                Catalog("some/other-model"),
            });

        await _service.RefreshAsync(userId: null);

        var all = (await _repository.GetAllAsync()).ToDictionary(m => m.Id);
        all["anthropic/claude-sonnet-4"].IsEnabled.Should().BeTrue();
        all["anthropic/claude-haiku-4"].IsEnabled.Should().BeTrue();
        all["openai/gpt-4o"].IsEnabled.Should().BeTrue();
        all["some/other-model"].IsEnabled.Should().BeFalse("only the configured mode defaults are bootstrap-enabled");

        // Manually enable, then disable, a non-default model - state an admin controls directly,
        // not something bootstrap should ever touch again.
        await _service.SetEnabledAsync("some/other-model", enabled: true, userId: "admin-1");
        await _service.SetEnabledAsync("some/other-model", enabled: false, userId: "admin-1");

        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                Catalog("anthropic/claude-sonnet-4"),
                Catalog("anthropic/claude-haiku-4"),
                Catalog("openai/gpt-4o"),
                Catalog("some/other-model"),
                Catalog("brand-new/vendor-model"),
            });
        await _service.RefreshAsync(userId: null);

        var afterSecondRefresh = (await _repository.GetAllAsync()).ToDictionary(m => m.Id);
        afterSecondRefresh["some/other-model"].IsEnabled.Should().BeFalse("bootstrap only ever runs once");
        afterSecondRefresh["brand-new/vendor-model"].IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabledAsync_RefusesToDisableAModeDefault()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4") });
        await _service.RefreshAsync(userId: null); // bootstrap-enables it as the guild assistant default

        var result = await _service.SetEnabledAsync("anthropic/claude-sonnet-4", enabled: false, userId: "admin-1");

        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrWhiteSpace();

        var model = await _repository.GetByIdAsync("anthropic/claude-sonnet-4");
        model!.IsEnabled.Should().BeTrue("the refused disable must not have taken effect");
    }

    [Fact]
    public async Task SetEnabledAsync_RefusesUsingDbSettingOverBoundOptions()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("some/db-configured-model"), Catalog("anthropic/claude-sonnet-4") });
        await _service.RefreshAsync(userId: null);
        await _service.SetEnabledAsync("some/db-configured-model", enabled: true, userId: "admin-1");

        // The DB setting for the guild assistant mode overrides the bound options value.
        _mockSettingsService
            .Setup(x => x.GetSettingValueAsync<string>("Assistant:Sampling:Model", It.IsAny<CancellationToken>()))
            .ReturnsAsync("some/db-configured-model");

        var result = await _service.SetEnabledAsync("some/db-configured-model", enabled: false, userId: "admin-1");

        result.Success.Should().BeFalse();
    }

    [Fact]
    public async Task SetEnabledAsync_EnablingSetsAuditedFieldsAndCallsAuditLog()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("some/unbootstrapped-model") });
        await _service.RefreshAsync(userId: null); // not a configured mode default - stays disabled

        var result = await _service.SetEnabledAsync("some/unbootstrapped-model", enabled: true, userId: "admin-1");

        result.Success.Should().BeTrue();
        var model = await _repository.GetByIdAsync("some/unbootstrapped-model");
        model!.IsEnabled.Should().BeTrue();
        model.EnabledBy.Should().Be("admin-1");
        model.EnabledAt.Should().NotBeNull();

        _mockAuditLogService.Verify(x => x.CreateBuilder(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RefreshAsync_CallsAuditLog()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4") });

        await _service.RefreshAsync(userId: "admin-1");

        _mockAuditLogService.Verify(x => x.CreateBuilder(), Times.AtLeastOnce);
    }

    [Fact]
    public async Task RefreshAsync_WithEmptyFetchedCatalog_ReturnsZeroResultAndLeavesCatalogUntouched()
    {
        // Seed an existing, available catalog first.
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4"), Catalog("openai/gpt-4o") });
        await _service.RefreshAsync(userId: null);

        // Simulate OpenRouter returning an empty {"data":[]} catalog.
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<LlmCatalogModel>());

        var result = await _service.RefreshAsync(userId: null);

        result.Added.Should().Be(0);
        result.Updated.Should().Be(0);
        result.Removed.Should().Be(0, "an empty fetched catalog must never mark the whole catalog unavailable");

        var all = await _repository.GetAllAsync();
        all.Should().HaveCount(2);
        all.Should().OnlyContain(m => m.IsAvailable, "the existing catalog must be untouched by an empty fetch");
    }

    [Fact]
    public async Task GetEnabledAsync_ReturnsOnlyEnabledModels()
    {
        _mockCatalogClient.Setup(x => x.GetModelsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { Catalog("anthropic/claude-sonnet-4"), Catalog("some/never-enabled") });
        await _service.RefreshAsync(userId: null); // bootstrap-enables only claude-sonnet-4

        var enabled = await _service.GetEnabledAsync();

        enabled.Should().ContainSingle(m => m.Id == "anthropic/claude-sonnet-4");
    }
}
