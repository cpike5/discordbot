using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="SlashCommandRegistrationService"/>.
/// DiscordSocketClient/InteractionService are sealed/concrete Discord.Net types, so tests
/// use real (unconnected) instances for construction and focus on the testable registration
/// decision and module discovery/filtering behavior, per the existing pattern used for
/// <see cref="InteractionHandler"/>/<see cref="BotHostedService"/> tests in this project.
/// </summary>
public class SlashCommandRegistrationServiceTests : IAsyncLifetime
{
    private DiscordSocketClient _client = null!;
    private InteractionService _interactionService = null!;
    private Mock<ICommandModuleConfigurationService> _mockCommandModuleConfigService = null!;
    private Mock<ILogger<SlashCommandRegistrationService>> _mockLogger = null!;

    public Task InitializeAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.None
        });
        _interactionService = new InteractionService(_client);
        _mockCommandModuleConfigService = new Mock<ICommandModuleConfigurationService>();
        _mockLogger = new Mock<ILogger<SlashCommandRegistrationService>>();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    private SlashCommandRegistrationService CreateService(ulong? testGuildId)
    {
        var config = Options.Create(new BotConfiguration
        {
            Token = "test-token",
            TestGuildId = testGuildId
        });

        // Discord.Interactions' ModuleBuilder creates a DI scope while building each module,
        // so the service provider passed to AddModuleAsync needs a real IServiceScopeFactory.
        var serviceProvider = new ServiceCollection().BuildServiceProvider();

        return new SlashCommandRegistrationService(
            _client,
            _interactionService,
            serviceProvider,
            config,
            _mockLogger.Object,
            _mockCommandModuleConfigService.Object);
    }

    [Fact]
    public void Constructor_ShouldCreateInstance_ImplementingIHostedService()
    {
        var service = CreateService(testGuildId: null);

        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IHostedService>();
    }

    [Theory]
    [InlineData(123456789UL, true)]
    [InlineData(0UL, true)]
    public void ShouldRegisterToTestGuild_WithTestGuildIdSet_ShouldReturnTrue(ulong guildId, bool expected)
    {
        var result = SlashCommandRegistrationService.ShouldRegisterToTestGuild(guildId);

        result.Should().Be(expected);
    }

    [Fact]
    public void ShouldRegisterToTestGuild_WithNullTestGuildId_ShouldReturnFalse()
    {
        var result = SlashCommandRegistrationService.ShouldRegisterToTestGuild(null);

        result.Should().BeFalse("commands should register globally when no test guild is configured");
    }

    [Fact]
    public async Task DiscoverAndLoadModulesAsync_ShouldSyncModuleConfigurationsBeforeDiscoveringModules()
    {
        // Modules discovered from the real assembly have their own constructor dependencies
        // (DI-heavy Discord.Interactions modules), which aren't in scope here — this test
        // only asserts the module-configuration sync/read happens, tolerating a downstream
        // module-build failure the same way a caller with an incomplete container would.
        _mockCommandModuleConfigService
            .Setup(s => s.SyncModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockCommandModuleConfigService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<CommandModuleConfigurationDto>());

        var service = CreateService(testGuildId: null);

        try
        {
            await service.DiscoverAndLoadModulesAsync();
        }
        catch (InvalidOperationException)
        {
            // Expected: real interaction modules require the full application DI container.
        }

        _mockCommandModuleConfigService.Verify(s => s.SyncModulesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _mockCommandModuleConfigService.Verify(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void IsInteractionModule_WithRealSlashCommandModule_ShouldReturnTrue()
    {
        // Mirrors the reflection-based testing pattern used for InteractionHandler's
        // private static helpers elsewhere in this test project (sealed/concrete Discord.Net
        // types make full end-to-end module registration impractical to unit test).
        var method = typeof(SlashCommandRegistrationService).GetMethod(
            "IsInteractionModule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("IsInteractionModule should exist as a private static helper");

        var result = (bool)method!.Invoke(null, new object[] { typeof(DiscordBot.Bot.Commands.AdminModule) })!;

        result.Should().BeTrue("AdminModule inherits InteractionModuleBase<SocketInteractionContext>");
    }

    [Fact]
    public void IsInteractionModule_WithNonModuleType_ShouldReturnFalse()
    {
        var method = typeof(SlashCommandRegistrationService).GetMethod(
            "IsInteractionModule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (bool)method!.Invoke(null, new object[] { typeof(string) })!;

        result.Should().BeFalse("System.String does not inherit InteractionModuleBase<>");
    }

    [Fact]
    public async Task RegisterCommandsAsync_WithUnstartedClient_ShouldLogErrorRatherThanThrow()
    {
        // The client isn't logged in, so RegisterCommandsGloballyAsync/ToGuildAsync will fail;
        // RegisterCommandsAsync must catch and log rather than propagate (fire-and-forget from Ready).
        var service = CreateService(testGuildId: null);

        var act = async () => await service.RegisterCommandsAsync();

        await act.Should().NotThrowAsync();
    }
}
