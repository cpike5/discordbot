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

    private FeatureRequestConversationService BuildService()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_mockAgentRunner.Object);
        services.AddSingleton(_mockModelResolver.Object);
        services.AddSingleton(_mockFeatureRequestService.Object);
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
}
