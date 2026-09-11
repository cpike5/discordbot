using Discord;
using DiscordBot.Bot.Services.FeatureRequests;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs.LLM;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Core.Interfaces.LLM;
using DiscordBot.Infrastructure.Services.FeatureRequests;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using DiscordBot.Core.DTOs.Llm.Reporting;

namespace DiscordBot.Tests.Bot.Services.FeatureRequests;

/// <summary>
/// Unit tests for <see cref="FeatureRequestConversationService"/>'s use of
/// <see cref="ILlmModelResolver"/> for the requirements-gathering agent's model.
/// </summary>
public class FeatureRequestConversationServiceTests
{
    private readonly Mock<IInteractionStateService> _mockStateService = new();
    private readonly Mock<IInputValidationService> _mockValidationService = new();
    private readonly Mock<IAgentRunner> _mockAgentRunner = new();
    private readonly Mock<ILlmModelResolver> _mockModelResolver = new();
    private readonly Mock<IFeatureRequestService> _mockFeatureRequestService = new();
    private readonly Mock<ILlmUsageRecorder> _mockUsageRecorder = new();

    private FeatureRequestConversationService BuildService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockAgentRunner.Object);
        services.AddSingleton(_mockModelResolver.Object);
        services.AddSingleton(_mockFeatureRequestService.Object);
        services.AddSingleton(_mockUsageRecorder.Object);
        services.AddSingleton(Options.Create(new AssistantOptions()));
        services.AddSingleton<FeatureRequestToolProvider>();
        services.AddSingleton<Microsoft.Extensions.Logging.ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddLogging();
        var provider = services.BuildServiceProvider();

        var mockScopeFactory = new Mock<IServiceScopeFactory>();
        mockScopeFactory.Setup(f => f.CreateScope()).Returns(() => provider.CreateScope());

        var options = Options.Create(new FeatureRequestsOptions
        {
            RequirementsGatheringModel = "config/fr-model",
            ConversationTimeoutMinutes = 30
        });

        return new FeatureRequestConversationService(
            _mockStateService.Object,
            _mockValidationService.Object,
            mockScopeFactory.Object,
            options,
            NullLogger<FeatureRequestConversationService>.Instance);
    }

    [Fact]
    public async Task StartConversationAsync_UsesResolverSlug_NotConfiguredOptionsValue()
    {
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "db/fr-model", Source = LlmModelResolutionSource.Database, ConfiguredSlug = "db/fr-model" });

        _mockAgentRunner
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult { Success = true, Response = "What problem does this solve?" });

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(555UL);
        var mockDmChannel = new Mock<IDMChannel>();
        mockUser.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(mockDmChannel.Object);

        var service = BuildService();

        await service.StartConversationAsync(mockUser.Object, guildId: 1, initialDescription: "It would be nice to have dark mode.");

        _mockAgentRunner.Verify(
            a => a.RunAsync(
                It.IsAny<string>(),
                It.Is<AgentContext>(c => c.Model == "db/fr-model"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StartConversationAsync_RecordsUsageLedgerRow_WithFeatureRequestsMode()
    {
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "db/fr-model", Source = LlmModelResolutionSource.Database, ConfiguredSlug = "db/fr-model" });

        _mockAgentRunner
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentRunResult
            {
                Success = true,
                Response = "What problem does this solve?",
                Model = "anthropic/claude-sonnet-4.6",
                LoopCount = 1,
                TotalUsage = new LlmUsage { InputTokens = 40, OutputTokens = 10, EstimatedCost = 0.0001m }
            });

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(555UL);
        var mockDmChannel = new Mock<IDMChannel>();
        mockUser.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(mockDmChannel.Object);

        var service = BuildService();

        await service.StartConversationAsync(mockUser.Object, guildId: 1, initialDescription: "It would be nice to have dark mode.");

        _mockUsageRecorder.Verify(r => r.Record(It.Is<LlmUsageRecord>(u =>
            u.Mode == LlmMode.FeatureRequests &&
            u.UserId == 555UL &&
            u.GuildId == 1UL &&
            u.Model == "anthropic/claude-sonnet-4.6" &&
            u.CostSource == LlmCostSource.Billed &&
            u.CostUsd == 0.0001m &&
            u.InteractionLogId == null)), Times.Once);
    }

    [Fact]
    public async Task StartConversationAsync_WhenAgentRunnerThrows_StillRecordsFailedUsageLedgerRow()
    {
        // agentRunner.RunAsync throwing (rather than coming back as a normal Success=false result)
        // must not leave this turn with no ledger row at all.
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmResolvedModel { Slug = "db/fr-model", Source = LlmModelResolutionSource.Database, ConfiguredSlug = "db/fr-model" });

        _mockAgentRunner
            .Setup(a => a.RunAsync(It.IsAny<string>(), It.IsAny<AgentContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("provider unavailable"));

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(555UL);
        var mockDmChannel = new Mock<IDMChannel>();
        mockUser.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(mockDmChannel.Object);

        var service = BuildService();

        // StartConversationAsync's own outer catch swallows the exception and sends a fallback
        // message - it must not propagate out of this call.
        await service.StartConversationAsync(mockUser.Object, guildId: 1, initialDescription: "It would be nice to have dark mode.");

        _mockUsageRecorder.Verify(r => r.Record(It.Is<LlmUsageRecord>(u =>
            u.Mode == LlmMode.FeatureRequests &&
            u.UserId == 555UL &&
            u.GuildId == 1UL &&
            u.Model == "db/fr-model" &&
            u.Success == false &&
            u.CostSource == LlmCostSource.Estimated &&
            u.CostUsd == 0m)), Times.Once);
    }

    [Fact]
    public async Task StartConversationAsync_WhenModelResolverThrows_RecordsFailedUsageLedgerRow_WithUnknownModel()
    {
        // The failure happens before a model is even resolved, so "unknown" is the only honest value.
        _mockModelResolver
            .Setup(r => r.ResolveAsync(LlmMode.FeatureRequests, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("resolver failed"));

        var mockUser = new Mock<IUser>();
        mockUser.Setup(u => u.Id).Returns(555UL);
        var mockDmChannel = new Mock<IDMChannel>();
        mockUser.Setup(u => u.CreateDMChannelAsync(It.IsAny<RequestOptions>())).ReturnsAsync(mockDmChannel.Object);

        var service = BuildService();

        await service.StartConversationAsync(mockUser.Object, guildId: 1, initialDescription: "It would be nice to have dark mode.");

        _mockUsageRecorder.Verify(r => r.Record(It.Is<LlmUsageRecord>(u =>
            u.Mode == LlmMode.FeatureRequests &&
            u.UserId == 555UL &&
            u.GuildId == 1UL &&
            u.Model == "unknown" &&
            u.Success == false)), Times.Once);
    }
}
