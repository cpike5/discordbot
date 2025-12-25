using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Preconditions;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandMetadataService"/>.
/// These tests use a real InteractionService with test command modules to verify metadata extraction.
/// </summary>
public class CommandMetadataServiceTests : IAsyncLifetime
{
    private DiscordSocketClient _client = null!;
    private InteractionService _interactionService = null!;
    private IServiceProvider _serviceProvider = null!;
    private Mock<ILogger<CommandMetadataService>> _mockLogger = null!;
    private CommandMetadataService _service = null!;

    public async Task InitializeAsync()
    {
        // Create Discord client and InteractionService
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.None // Minimal intents for testing
        });

        _interactionService = new InteractionService(_client);

        // Setup service provider for dependency injection in test modules
        var services = new ServiceCollection();
        services.AddLogging();
        _serviceProvider = services.BuildServiceProvider();

        // Setup logger mock
        _mockLogger = new Mock<ILogger<CommandMetadataService>>();

        // Create service
        _service = new CommandMetadataService(_interactionService, _mockLogger.Object);

        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_interactionService != null)
        {
            _interactionService.Dispose();
        }

        if (_client != null)
        {
            await _client.DisposeAsync();
        }

        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    [Fact]
    public async Task GetAllModulesAsync_WithNoModules_ReturnsEmptyList()
    {
        // Arrange
        // InteractionService has no modules registered

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("no registered modules")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when no modules are registered");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithSimpleModule_MapsCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestSimpleModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        result.Should().HaveCount(1);

        var module = result[0];
        module.Name.Should().Be("TestSimpleModule");
        module.DisplayName.Should().Be("TestSimple"); // "Module" suffix removed
        module.IsSlashGroup.Should().BeFalse();
        module.GroupName.Should().BeNull();
        module.Commands.Should().HaveCount(1);
        module.CommandCount.Should().Be(1);

        var command = module.Commands[0];
        command.Name.Should().Be("ping");
        command.FullName.Should().Be("ping"); // No group prefix
        command.Description.Should().Be("Responds with pong");
        command.ModuleName.Should().Be("TestSimpleModule");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithGroupedModule_MapsGroupCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestGroupedModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        result.Should().HaveCount(1);

        var module = result[0];
        module.Name.Should().Be("TestGroupedModule");
        module.DisplayName.Should().Be("TestGrouped");
        module.IsSlashGroup.Should().BeTrue();
        module.GroupName.Should().Be("test");
        module.Description.Should().Be("Test group commands");
        module.Commands.Should().HaveCount(2);

        // Commands should have group prefix in FullName
        var cmdOne = module.Commands.FirstOrDefault(c => c.Name == "one");
        cmdOne.Should().NotBeNull();
        cmdOne!.FullName.Should().Be("test one");

        var cmdTwo = module.Commands.FirstOrDefault(c => c.Name == "two");
        cmdTwo.Should().NotBeNull();
        cmdTwo!.FullName.Should().Be("test two");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithParameters_MapsParametersCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestParameterModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "echo");
        command.Should().NotBeNull();
        command!.Parameters.Should().HaveCount(2);

        // Required string parameter
        var messageParam = command.Parameters.FirstOrDefault(p => p.Name == "message");
        messageParam.Should().NotBeNull();
        messageParam!.Type.Should().Be("String");
        messageParam.IsRequired.Should().BeTrue();
        messageParam.Description.Should().Be("Message to echo");
        // Discord.NET sets default value for string parameters to empty string
        (messageParam.DefaultValue == null || messageParam.DefaultValue == "").Should().BeTrue();
        messageParam.Choices.Should().BeNull();

        // Optional int parameter with default
        var timesParam = command.Parameters.FirstOrDefault(p => p.Name == "times");
        timesParam.Should().NotBeNull();
        timesParam!.Type.Should().Be("Integer");
        timesParam.IsRequired.Should().BeFalse();
        timesParam.Description.Should().Be("Number of times to repeat");
        timesParam.DefaultValue.Should().Be("1");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithEnumParameter_ExtractsChoices()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestParameterModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "set-consent");
        command.Should().NotBeNull();
        command!.Parameters.Should().HaveCount(1);

        var typeParam = command.Parameters[0];
        typeParam.Name.Should().Be("type");
        typeParam.Type.Should().Be("ConsentType"); // Enum name
        typeParam.Choices.Should().NotBeNull();
        typeParam.Choices.Should().Contain("MessageLogging");
        // ConsentType currently only has one value
        typeParam.Choices.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllModulesAsync_WithDiscordTypeParameters_MapsFriendlyTypes()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestDiscordTypeModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];

        var kickCmd = module.Commands.FirstOrDefault(c => c.Name == "kick");
        kickCmd.Should().NotBeNull();
        kickCmd!.Parameters[0].Type.Should().Be("User");

        var channelCmd = module.Commands.FirstOrDefault(c => c.Name == "announce");
        channelCmd.Should().NotBeNull();
        channelCmd!.Parameters[0].Type.Should().Be("Channel");

        var roleCmd = module.Commands.FirstOrDefault(c => c.Name == "assign");
        roleCmd.Should().NotBeNull();
        roleCmd!.Parameters[0].Type.Should().Be("Role");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithRequireAdminPrecondition_MapsCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestPreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "admin-only");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(1);

        var precondition = command.Preconditions[0];
        precondition.Name.Should().Be("RequireAdmin");
        precondition.Type.Should().Be(PreconditionType.Admin);
        precondition.Configuration.Should().BeNull();
    }

    [Fact]
    public async Task GetAllModulesAsync_WithRequireOwnerPrecondition_MapsCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestPreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "owner-only");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(1);

        var precondition = command.Preconditions[0];
        precondition.Name.Should().Be("RequireOwner");
        precondition.Type.Should().Be(PreconditionType.Owner);
        precondition.Configuration.Should().BeNull();
    }

    [Fact]
    public async Task GetAllModulesAsync_WithRateLimitPrecondition_MapsConfigurationCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestPreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "rate-limited");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(1);

        var precondition = command.Preconditions[0];
        precondition.Name.Should().Be("RateLimit");
        precondition.Type.Should().Be(PreconditionType.RateLimit);
        precondition.Configuration.Should().Be("5 per 60s (User)");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithBotPermissionPrecondition_MapsCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestPreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "bot-permission");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(1);

        var precondition = command.Preconditions[0];
        precondition.Name.Should().Be("RequireBotPermission");
        precondition.Type.Should().Be(PreconditionType.BotPermission);
        precondition.Configuration.Should().Contain("ManageMessages");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithUserPermissionPrecondition_MapsCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestPreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "user-permission");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(1);

        var precondition = command.Preconditions[0];
        precondition.Name.Should().Be("RequireUserPermission");
        precondition.Type.Should().Be(PreconditionType.UserPermission);
        precondition.Configuration.Should().Contain("Administrator");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithContextPrecondition_MapsCorrectly()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestPreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "guild-only");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(1);

        var precondition = command.Preconditions[0];
        precondition.Name.Should().Be("RequireContext");
        precondition.Type.Should().Be(PreconditionType.Context);
        precondition.Configuration.Should().Contain("Guild");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithModuleLevelPrecondition_InheritsToAllCommands()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestModulePreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        module.Commands.Should().HaveCount(2);

        // Both commands should inherit module-level RequireAdmin
        foreach (var command in module.Commands)
        {
            command.Preconditions.Should().Contain(p =>
                p.Type == PreconditionType.Admin && p.Name == "RequireAdmin");
        }
    }

    [Fact]
    public async Task GetAllModulesAsync_WithCombinedPreconditions_IncludesBothModuleAndCommand()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestModulePreconditionModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        var module = result[0];
        var command = module.Commands.FirstOrDefault(c => c.Name == "with-extra");
        command.Should().NotBeNull();
        command!.Preconditions.Should().HaveCount(2);

        // Should have both module-level RequireAdmin and command-level RateLimit
        command.Preconditions.Should().Contain(p => p.Type == PreconditionType.Admin);
        command.Preconditions.Should().Contain(p => p.Type == PreconditionType.RateLimit);
    }

    [Fact]
    public async Task GetAllModulesAsync_WithMultipleModules_ReturnsAll()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestSimpleModule>(_serviceProvider);
        await _interactionService.AddModuleAsync<TestGroupedModule>(_serviceProvider);

        // Act
        var result = await _service.GetAllModulesAsync();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Name == "TestSimpleModule");
        result.Should().Contain(m => m.Name == "TestGroupedModule");
    }

    [Fact]
    public async Task GetAllModulesAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestSimpleModule>(_serviceProvider);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _service.GetAllModulesAsync(cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllModulesAsync_LogsModuleCount()
    {
        // Arrange
        await _interactionService.AddModuleAsync<TestSimpleModule>(_serviceProvider);
        await _interactionService.AddModuleAsync<TestGroupedModule>(_serviceProvider);

        // Act
        await _service.GetAllModulesAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Extracted metadata")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information about extracted modules");
    }

    #region Test Command Modules

    // Simple module without group
    public class TestSimpleModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("ping", "Responds with pong")]
        public Task PingAsync() => Task.CompletedTask;
    }

    // Module with group
    [Group("test", "Test group commands")]
    public class TestGroupedModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("one", "First test command")]
        public Task OneAsync() => Task.CompletedTask;

        [SlashCommand("two", "Second test command")]
        public Task TwoAsync() => Task.CompletedTask;
    }

    // Module with various parameter types
    public class TestParameterModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("echo", "Echoes a message")]
        public Task EchoAsync(
            [Summary("message", "Message to echo")] string message,
            [Summary("times", "Number of times to repeat")] int times = 1)
            => Task.CompletedTask;

        [SlashCommand("set-consent", "Set consent type")]
        public Task SetConsentAsync(
            [Summary("type", "The consent type")] ConsentType type)
            => Task.CompletedTask;
    }

    // Module with Discord type parameters
    public class TestDiscordTypeModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("kick", "Kicks a user")]
        public Task KickAsync(
            [Summary("user", "User to kick")] IUser user)
            => Task.CompletedTask;

        [SlashCommand("announce", "Announce in channel")]
        public Task AnnounceAsync(
            [Summary("channel", "Target channel")] IChannel channel)
            => Task.CompletedTask;

        [SlashCommand("assign", "Assign role")]
        public Task AssignAsync(
            [Summary("role", "Role to assign")] IRole role)
            => Task.CompletedTask;
    }

    // Module with various preconditions
    public class TestPreconditionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("admin-only", "Requires admin")]
        [RequireAdmin]
        public Task AdminOnlyAsync() => Task.CompletedTask;

        [SlashCommand("owner-only", "Requires owner")]
        [DiscordBot.Bot.Preconditions.RequireOwner]
        public Task OwnerOnlyAsync() => Task.CompletedTask;

        [SlashCommand("rate-limited", "Rate limited command")]
        [RateLimit(5, 60, RateLimitTarget.User)]
        public Task RateLimitedAsync() => Task.CompletedTask;

        [SlashCommand("bot-permission", "Requires bot permission")]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public Task BotPermissionAsync() => Task.CompletedTask;

        [SlashCommand("user-permission", "Requires user permission")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public Task UserPermissionAsync() => Task.CompletedTask;

        [SlashCommand("guild-only", "Guild context only")]
        [RequireContext(ContextType.Guild)]
        public Task GuildOnlyAsync() => Task.CompletedTask;
    }

    // Module with module-level precondition
    [RequireAdmin]
    public class TestModulePreconditionModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("simple", "Simple command")]
        public Task SimpleAsync() => Task.CompletedTask;

        [SlashCommand("with-extra", "Command with extra precondition")]
        [RateLimit(3, 30, RateLimitTarget.Guild)]
        public Task WithExtraAsync() => Task.CompletedTask;
    }

    #endregion
}
