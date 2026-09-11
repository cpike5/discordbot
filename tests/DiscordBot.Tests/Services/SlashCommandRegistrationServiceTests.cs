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

    private SlashCommandRegistrationService CreateService(ulong? testGuildId, bool notXEnabled = true)
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
            _mockCommandModuleConfigService.Object,
            Options.Create(new NotXOptions { Enabled = notXEnabled }));
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
    public void IsNestedSubModule_WithGroupDeclaredInsideModule_ShouldReturnTrue()
    {
        // Nested [Group] sub-modules are built by Discord.NET as part of their parent; passing
        // them to AddModuleAsync on their own throws at startup (regression test).
        var method = typeof(SlashCommandRegistrationService).GetMethod(
            "IsNestedSubModule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("IsNestedSubModule should exist as a private static helper");

        var channel = (bool)method!.Invoke(null, new object[] { typeof(DiscordBot.Bot.Commands.NotXCommandModule.ChannelSubModule) })!;
        var monitor = (bool)method.Invoke(null, new object[] { typeof(DiscordBot.Bot.Commands.NotXCommandModule.MonitorSubModule) })!;
        var parent = (bool)method.Invoke(null, new object[] { typeof(DiscordBot.Bot.Commands.NotXCommandModule) })!;

        channel.Should().BeTrue("ChannelSubModule is declared inside NotXCommandModule");
        monitor.Should().BeTrue("MonitorSubModule is declared inside NotXCommandModule");
        parent.Should().BeFalse("NotXCommandModule is a top-level module");
    }

    [Fact]
    public async Task AddModuleAsync_WithNotXCommandModule_ShouldBuildNestedSubGroupsWithParent()
    {
        // Registering only the top-level module must produce the nested channel/monitor groups;
        // registering the nested type on its own is what Discord.NET rejects.
        var services = new ServiceCollection()
            .AddSingleton(Mock.Of<INotXService>())
            .AddLogging()
            .BuildServiceProvider();

        var module = await _interactionService.AddModuleAsync(typeof(DiscordBot.Bot.Commands.NotXCommandModule), services);

        module.SlashGroupName.Should().Be("notx");
        module.SubModules.Select(m => m.SlashGroupName).Should().BeEquivalentTo("channel", "monitor");

        var act = async () => await _interactionService.AddModuleAsync(
            typeof(DiscordBot.Bot.Commands.NotXCommandModule.ChannelSubModule), services);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Could not build the module*");
    }

    [Fact]
    public void GetConfigurationDisabledModules_WithNotXEnabled_ShouldReturnEmptySet()
    {
        var result = SlashCommandRegistrationService.GetConfigurationDisabledModules(
            new NotXOptions { Enabled = true });

        result.Should().BeEmpty("an enabled feature must not have its modules filtered out");
    }

    [Fact]
    public void GetConfigurationDisabledModules_WithNotXDisabled_ShouldContainBothNotXModules()
    {
        // Both modules must be named: the context menu command lives in its own top-level
        // module (it cannot sit inside a [Group]), so the component-module parent lookup
        // in DiscoverAndLoadModulesAsync would never catch it.
        var result = SlashCommandRegistrationService.GetConfigurationDisabledModules(
            new NotXOptions { Enabled = false });

        // nameof keeps this honest: renaming either module breaks the build here rather
        // than silently leaving the kill switch matching a name that no longer exists.
        result.Should().BeEquivalentTo(new[]
        {
            nameof(DiscordBot.Bot.Commands.NotXCommandModule),
            nameof(DiscordBot.Bot.Commands.NotXContextMenuModule)
        });
    }

    [Fact]
    public void GetConfigurationDisabledModules_ShouldMatchModuleNamesCaseInsensitively()
    {
        // Module names are compared against the database configuration case-insensitively
        // elsewhere in the service; this set must behave the same way.
        var result = SlashCommandRegistrationService.GetConfigurationDisabledModules(
            new NotXOptions { Enabled = false });

        result.Contains("notxcommandmodule").Should().BeTrue();
        result.Contains("NOTXCONTEXTMENUMODULE").Should().BeTrue();
    }

    [Fact]
    public void GetConfigurationDisabledModules_ShouldNameOnlyTopLevelInteractionModules()
    {
        // The skip in DiscoverAndLoadModulesAsync matches on Type.Name of the top-level
        // modules it discovers, so every name here must belong to a discoverable module —
        // a nested sub-group or a non-module name would never match and the commands would
        // register anyway.
        var disabled = SlashCommandRegistrationService.GetConfigurationDisabledModules(
            new NotXOptions { Enabled = false });

        var isInteractionModule = typeof(SlashCommandRegistrationService).GetMethod(
            "IsInteractionModule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var isNestedSubModule = typeof(SlashCommandRegistrationService).GetMethod(
            "IsNestedSubModule",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;

        var discoverableModuleNames = typeof(SlashCommandRegistrationService).Assembly
            .GetTypes()
            .Where(t => t.IsClass
                && !t.IsAbstract
                && (bool)isInteractionModule.Invoke(null, new object[] { t })!
                && !(bool)isNestedSubModule.Invoke(null, new object[] { t })!)
            .Select(t => t.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        disabled.Should().OnlyContain(name => discoverableModuleNames.Contains(name));
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
    public void ResolveCompanionParentModule_WithNotXContextMenuModule_ShouldReturnNotXCommandModule()
    {
        // The Commands tab presents not-X as one feature with a single toggle, but the
        // context menu command needs its own top-level module (it cannot sit inside a
        // [Group]). Without this mapping, disabling not-X would leave "Fetch Tweet"
        // registered and working.
        var result = SlashCommandRegistrationService.ResolveCompanionParentModule(
            nameof(DiscordBot.Bot.Commands.NotXContextMenuModule));

        result.Should().Be(nameof(DiscordBot.Bot.Commands.NotXCommandModule));
    }

    [Fact]
    public void ResolveCompanionParentModule_WithModuleOwningItsToggle_ShouldReturnNull()
    {
        // A null result routes the module to the normal database lookups, so any module with
        // its own configuration row must not be mistaken for a companion.
        SlashCommandRegistrationService
            .ResolveCompanionParentModule(nameof(DiscordBot.Bot.Commands.NotXCommandModule))
            .Should().BeNull();
        SlashCommandRegistrationService
            .ResolveCompanionParentModule(nameof(DiscordBot.Bot.Commands.AdminModule))
            .Should().BeNull();
    }

    [Fact]
    public void ResolveCompanionParentModule_ShouldNameParentsThatHaveADatabaseToggle()
    {
        // Every companion parent must be a module the Commands tab actually lists, otherwise
        // the companion follows a toggle that can never be disabled. NotXCommandModule was
        // missing from DefaultModules before this feature gained a UI switch, which is
        // exactly the regression this guards.
        var defaultModules = typeof(DiscordBot.Infrastructure.Services.CommandModuleConfigurationService)
            .GetField("DefaultModules",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null);

        var seededNames = ((System.Collections.IEnumerable)defaultModules!)
            .Cast<object>()
            .Select(d => (string)d.GetType().GetProperty("ModuleName")!.GetValue(d)!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parent = SlashCommandRegistrationService.ResolveCompanionParentModule(
            nameof(DiscordBot.Bot.Commands.NotXContextMenuModule))!;

        seededNames.Should().Contain(parent);
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
