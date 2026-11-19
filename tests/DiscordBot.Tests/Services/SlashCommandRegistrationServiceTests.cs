using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Configuration;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using DiscordBot.Tests.TestHelpers;
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

    private SlashCommandRegistrationService CreateService(ulong? testGuildId, IServiceProvider? serviceProvider = null)
    {
        var config = Options.Create(new BotConfiguration
        {
            Token = "test-token",
            TestGuildId = testGuildId
        });

        // Discord.Interactions' ModuleBuilder creates a DI scope while building each module,
        // so the service provider passed to AddModuleAsync needs a real IServiceScopeFactory.
        serviceProvider ??= new ServiceCollection().BuildServiceProvider();

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

        var result = (bool)method!.Invoke(null, new object[] { typeof(DiscordBot.Bot.Commands.ReminderModule) })!;

        result.Should().BeTrue("ReminderModule inherits InteractionModuleBase<SocketInteractionContext>");
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
    public void IsNestedSubModule_WithGroupDeclaredInsideModule_ShouldReturnTrue()
    {
        // Nested [Group] sub-modules are built by Discord.NET as part of their parent; passing
        // them to AddModuleAsync on their own throws at startup (regression test).
        var method = typeof(SlashCommandRegistrationService).GetMethod(
            "IsNestedSubModule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("IsNestedSubModule should exist as a private static helper");

        var nested = (bool)method!.Invoke(null, new object[] { typeof(NestedGroupFixtureModule.ChildSubModule) })!;
        var parent = (bool)method.Invoke(null, new object[] { typeof(NestedGroupFixtureModule) })!;

        nested.Should().BeTrue("ChildSubModule is declared inside NestedGroupFixtureModule");
        parent.Should().BeFalse("NestedGroupFixtureModule is a top-level module");
    }

    [Fact]
    public async Task AddModuleAsync_WithNestedGroupModule_ShouldBuildNestedSubGroupsWithParent()
    {
        // Registering only the top-level module must produce the nested group; registering
        // the nested type on its own is what Discord.NET rejects.
        var services = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var module = await _interactionService.AddModuleAsync(typeof(NestedGroupFixtureModule), services);

        module.SlashGroupName.Should().Be("fixture");
        module.SubModules.Select(m => m.SlashGroupName).Should().BeEquivalentTo("child");

        var act = async () => await _interactionService.AddModuleAsync(
            typeof(NestedGroupFixtureModule.ChildSubModule), services);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not build the module*");
    }

    [Theory]
    [InlineData("RatWatchComponentModule", "RatWatchModule")]
    [InlineData("SoundboardComponentModule", "SoundboardModule")]
    public void ResolveCompanionParentModule_WithComponentModule_ShouldDeriveParentByName(
        string moduleName, string expectedParent)
    {
        var result = SlashCommandRegistrationService.ResolveCompanionParentModule(moduleName);

        result.Should().Be(expectedParent);
    }

    [Fact]
    public void ResolveCompanionParentModule_WithRatWatchContextMenuModule_ShouldMapToRatWatchModule()
    {
        // The Rat Watch context menu cannot live in the [Group("rat")] module, so it sits in its
        // own module that has no configuration row; the explicit map ties it to the Rat Watch toggle.
        SlashCommandRegistrationService
            .ResolveCompanionParentModule(nameof(DiscordBot.Bot.Commands.RatWatchContextMenuModule))
            .Should().Be(nameof(DiscordBot.Bot.Commands.RatWatchModule));
    }

    [Fact]
    public async Task DiscoverAndLoadModulesAsync_WithRatWatchModuleDisabled_ShouldSkipRatWatchContextMenuModule()
    {
        _mockCommandModuleConfigService
            .Setup(s => s.SyncModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockCommandModuleConfigService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllModulesDisabledExcept("RatWatchModule", ratWatchEnabled: false));

        var service = CreateService(testGuildId: null, new AutoMockServiceProvider(_client));

        await service.DiscoverAndLoadModulesAsync();

        var loadedModuleNames = _interactionService.Modules.Select(m => m.Name).ToList();
        loadedModuleNames.Should().NotContain("RatWatchModule");
        loadedModuleNames.Should().NotContain("RatWatchContextMenuModule",
            "the context menu module follows the Rat Watch toggle");
        loadedModuleNames.Should().NotContain("RatWatchComponentModule");
        _interactionService.ContextCommands.Select(c => c.Name).Should().NotContain("Rat Watch");
    }

    [Fact]
    public async Task DiscoverAndLoadModulesAsync_WithRatWatchModuleEnabled_ShouldLoadRatWatchContextMenuModule()
    {
        _mockCommandModuleConfigService
            .Setup(s => s.SyncModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        _mockCommandModuleConfigService
            .Setup(s => s.GetAllModulesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(AllModulesDisabledExcept("RatWatchModule", ratWatchEnabled: true));

        var service = CreateService(testGuildId: null, new AutoMockServiceProvider(_client));

        await service.DiscoverAndLoadModulesAsync();

        _interactionService.ContextCommands.Select(c => c.Name).Should().Contain("Rat Watch");
        _interactionService.Modules.Should().Contain(m => m.SlashGroupName == "rat");
    }

    /// <summary>
    /// Builds a configuration list that disables every top-level module in the Bot assembly that
    /// owns a toggle, except <paramref name="moduleName"/>, whose state is given. Disabled modules
    /// are never built, so the discovery run only builds the module under test and its companions.
    /// </summary>
    private static IReadOnlyList<CommandModuleConfigurationDto> AllModulesDisabledExcept(
        string moduleName, bool ratWatchEnabled)
    {
        return typeof(DiscordBot.Bot.Commands.RatWatchModule).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.DeclaringType == null
                && typeof(IInteractionModuleBase).IsAssignableFrom(t)
                && SlashCommandRegistrationService.ResolveCompanionParentModule(t.Name) is null)
            .Select(t => new CommandModuleConfigurationDto
            {
                ModuleName = t.Name,
                IsEnabled = t.Name == moduleName && ratWatchEnabled,
                Category = "Features"
            })
            .ToList();
    }

    [Fact]
    public void ResolveCompanionParentModule_WithModuleOwningItsToggle_ShouldReturnNull()
    {
        // A null result routes the module to the normal database lookups, so any module with
        // its own configuration row must not be mistaken for a companion.
        SlashCommandRegistrationService
            .ResolveCompanionParentModule(nameof(DiscordBot.Bot.Commands.ReminderModule))
            .Should().BeNull();
        SlashCommandRegistrationService
            .ResolveCompanionParentModule(nameof(DiscordBot.Bot.Commands.WalletModule))
            .Should().BeNull();
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

/// <summary>
/// A top-level <c>[Group]</c> module with a nested sub-group, used to exercise nested-module
/// discovery without depending on a production module having that shape.
/// </summary>
[Group("fixture", "Test fixture group")]
public class NestedGroupFixtureModule : InteractionModuleBase<SocketInteractionContext>
{
    [SlashCommand("ping", "Fixture command")]
    public Task PingAsync() => Task.CompletedTask;

    [Group("child", "Nested fixture group")]
    public class ChildSubModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("ping", "Nested fixture command")]
        public Task PingAsync() => Task.CompletedTask;
    }
}
