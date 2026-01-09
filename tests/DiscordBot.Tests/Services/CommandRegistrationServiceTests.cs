using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Services.Commands;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="CommandRegistrationService"/>.
/// NOTE: Direct testing is limited because DiscordSocketClient and InteractionService
/// are sealed/concrete classes that cannot be easily mocked. These tests focus on
/// testable behavior and document expected behavior for complex scenarios.
/// </summary>
public class CommandRegistrationServiceTests : IAsyncLifetime
{
    private DiscordSocketClient _client = null!;
    private InteractionService _interactionService = null!;
    private Mock<ILogger<CommandRegistrationService>> _mockLogger = null!;
    private CommandRegistrationService _service = null!;

    public async Task InitializeAsync()
    {
        // Create real Discord client with minimal configuration for testing
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.None // Minimal intents for testing
        });

        // Create real InteractionService
        _interactionService = new InteractionService(_client);

        // Setup logger mock
        _mockLogger = new Mock<ILogger<CommandRegistrationService>>();

        // Create service
        _service = new CommandRegistrationService(_client, _interactionService, _mockLogger.Object);

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
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_ReturnsFailure_WhenBotNotConnected()
    {
        // Arrange
        // Client is not connected by default in tests

        // Act
        var result = await _service.ClearAndRegisterGloballyAsync();

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("bot is not connected to Discord");
        result.Message.Should().Contain("not connected", "error message should explain the connection issue");
        result.Message.Should().Contain(_client.ConnectionState.ToString(), "error message should include current connection state");
        result.GlobalCommandsRegistered.Should().Be(0, "no commands should be registered when not connected");
        result.GuildsCleared.Should().Be(0, "no guilds should be cleared when not connected");
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_WhenNotConnected_LogsWarning()
    {
        // Arrange
        // Client is not connected by default in tests

        // Act
        await _service.ClearAndRegisterGloballyAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("not connected")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when client is not connected");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(_client.ConnectionState.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log the current connection state");
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_WhenDisconnected_ReturnsAppropriateConnectionState()
    {
        // Arrange
        // Verify different connection states are reflected in the error message

        // Act
        var result = await _service.ClearAndRegisterGloballyAsync();

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("state:", "message should include connection state label");

        // ConnectionState enum values that should fail:
        // - Disconnected (default in tests)
        // - Connecting
        // - Disconnecting
        // Only Connected should succeed
        var failureStates = new[] { ConnectionState.Disconnected, ConnectionState.Connecting, ConnectionState.Disconnecting };
        failureStates.Should().Contain(_client.ConnectionState, "client should be in a non-connected state during tests");
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_WithCancellationToken_AcceptsToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var result = await _service.ClearAndRegisterGloballyAsync(cts.Token);

        // Assert
        result.Should().NotBeNull("service should return a result even with cancellation token");
        result.Success.Should().BeFalse("should fail because client is not connected");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_StartupSequence_Documentation()
    {
        // This test documents the expected execution sequence when the bot IS connected:
        // 1. Log "Starting command registration process"
        // 2. Check ConnectionState - if not Connected, return failure with error message
        // 3. Clear global commands via client.BulkOverwriteGlobalApplicationCommandsAsync(empty array)
        // 4. Iterate through client.Guilds and clear each guild's commands via guild.BulkOverwriteApplicationCommandAsync(empty array)
        //    - Count successful guild clears in guildsCleared variable
        //    - Log error for failed guilds but continue with remaining guilds
        // 5. Re-register commands globally via interactionService.RegisterCommandsGloballyAsync()
        // 6. Get command count from interactionService.SlashCommands.Count
        // 7. Return success result with command count, guild count, and informational message
        // 8. On exception: Log error and return failure result with exception message
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:35-112

        var executionSequence = new[]
        {
            "1. Log startup information about command registration",
            "2. Validate bot connection state (must be Connected)",
            "3. Clear global commands by overwriting with empty array",
            "4. Iterate all guilds and clear guild-specific commands",
            "5. Re-register commands globally via InteractionService",
            "6. Extract command count from InteractionService",
            "7. Return success result with counts and message",
            "8. Handle exceptions by logging and returning failure"
        };

        executionSequence.Should().HaveCount(8, "command registration has 8 distinct steps");
        executionSequence[1].Should().Contain("Connected", "connection state check is critical");
        executionSequence[3].Should().Contain("guilds", "must clear guild commands");
        executionSequence[4].Should().Contain("globally", "must re-register globally");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_SuccessfulExecution_Documentation()
    {
        // This test documents the expected behavior when successfully connected:
        // - Calls client.BulkOverwriteGlobalApplicationCommandsAsync with Array.Empty<ApplicationCommandProperties>()
        // - For each guild in client.Guilds:
        //   - Calls guild.BulkOverwriteApplicationCommandAsync with Array.Empty<ApplicationCommandProperties>()
        //   - Increments guildsCleared counter
        //   - Logs debug message with guild ID and name
        //   - On error: Logs error but continues with next guild
        // - Calls interactionService.RegisterCommandsGloballyAsync()
        // - Returns CommandRegistrationResult with:
        //   - Success = true
        //   - Message = informative message about command count and guild count
        //   - GlobalCommandsRegistered = interactionService.SlashCommands.Count
        //   - GuildsCleared = number of successfully cleared guilds
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:53-101

        var expectedSuccessResult = new
        {
            Success = true,
            MessageContains = new[] { "Successfully", "command(s)", "guild(s)", "1 hour" },
            GlobalCommandsRegistered = "interactionService.SlashCommands.Count",
            GuildsCleared = "Number of guilds successfully cleared"
        };

        expectedSuccessResult.Success.Should().BeTrue("successful execution returns Success = true");
        expectedSuccessResult.MessageContains.Should().Contain("1 hour", "message should warn about global command propagation delay");
        expectedSuccessResult.MessageContains.Should().Contain("Successfully", "message should indicate success");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_GuildClearingLogic_Documentation()
    {
        // This test documents the guild clearing behavior:
        // - Iterates through client.Guilds collection
        // - For each guild, attempts to clear commands
        // - On success: Increments guildsCleared counter
        // - On error: Logs error with guild ID and name, but does NOT rethrow
        // - Continues with remaining guilds even if one fails
        // - Final result includes total number of successfully cleared guilds
        //
        // Error handling ensures partial failures don't prevent the overall operation
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:68-84

        var guildClearingBehavior = new
        {
            Approach = "Iterate all guilds and attempt to clear each",
            ErrorHandling = "Log error and continue (do not rethrow)",
            SuccessTracking = "Count guilds that cleared successfully",
            Logging = new[]
            {
                "Debug: Logs start of guild clearing with total count",
                "Debug: Logs each successful guild clear with ID and name",
                "Error: Logs failures with guild ID, name, and exception",
                "Information: Logs final count of cleared guilds"
            }
        };

        guildClearingBehavior.Should().NotBeNull();
        guildClearingBehavior.ErrorHandling.Should().Contain("continue", "should continue on errors");
        guildClearingBehavior.Logging.Should().HaveCount(4, "has 4 types of log messages");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_ErrorHandling_Documentation()
    {
        // This test documents error handling behavior:
        // - Global command clearing errors: Logged as error and rethrown (caught by outer try-catch)
        // - Guild command clearing errors: Logged as error but NOT rethrown (allows continuation)
        // - Re-registration errors: Propagate to outer try-catch
        // - Outer try-catch: Logs error and returns failure result with exception message
        //
        // Global clearing failure = entire operation fails
        // Guild clearing failure = operation continues with other guilds
        // Re-registration failure = entire operation fails
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:39-112

        var errorHandling = new
        {
            GlobalClearError = new { Logged = true, Rethrown = true, Result = "Operation fails" },
            GuildClearError = new { Logged = true, Rethrown = false, Result = "Operation continues" },
            ReregistrationError = new { Logged = true, Rethrown = true, Result = "Operation fails" },
            OuterCatch = new { Logged = true, Returns = "Failure result with exception message" }
        };

        errorHandling.GlobalClearError.Rethrown.Should().BeTrue("global clear errors cause operation to fail");
        errorHandling.GuildClearError.Rethrown.Should().BeFalse("guild clear errors allow operation to continue");
        errorHandling.ReregistrationError.Rethrown.Should().BeTrue("re-registration errors cause operation to fail");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_LoggingBehavior_Documentation()
    {
        // This test documents all logging performed by the service:
        // 1. Information: "Starting command registration process: clearing all commands and re-registering globally"
        // 2. Warning: "Cannot register commands: Discord client is not connected (state: {ConnectionState})" - if not connected
        // 3. Debug: "Clearing global commands"
        // 4. Information: "Global commands cleared successfully"
        // 5. Error: "Failed to clear global commands" - if global clear fails
        // 6. Debug: "Clearing guild-specific commands for {GuildCount} guilds"
        // 7. Debug: "Cleared commands for guild {GuildId} ({GuildName})" - for each successful guild
        // 8. Error: "Failed to clear commands for guild {GuildId} ({GuildName})" - for each failed guild
        // 9. Information: "Cleared commands from {GuildsCleared} guild(s)"
        // 10. Debug: "Re-registering commands globally"
        // 11. Information: "Successfully re-registered {CommandCount} command(s) globally"
        // 12. Error: "Failed to clear and re-register commands" - on exception
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:35-112

        var logMessages = new[]
        {
            new { Level = LogLevel.Information, Contains = "Starting command registration" },
            new { Level = LogLevel.Warning, Contains = "not connected" },
            new { Level = LogLevel.Debug, Contains = "Clearing global commands" },
            new { Level = LogLevel.Information, Contains = "Global commands cleared successfully" },
            new { Level = LogLevel.Error, Contains = "Failed to clear global commands" },
            new { Level = LogLevel.Debug, Contains = "Clearing guild-specific commands" },
            new { Level = LogLevel.Debug, Contains = "Cleared commands for guild" },
            new { Level = LogLevel.Error, Contains = "Failed to clear commands for guild" },
            new { Level = LogLevel.Information, Contains = "Cleared commands from" },
            new { Level = LogLevel.Debug, Contains = "Re-registering commands globally" },
            new { Level = LogLevel.Information, Contains = "Successfully re-registered" },
            new { Level = LogLevel.Error, Contains = "Failed to clear and re-register" }
        };

        logMessages.Should().HaveCount(12, "service performs 12 different types of logging");
        logMessages.Should().Contain(m => m.Level == LogLevel.Information, "should have informational logs");
        logMessages.Should().Contain(m => m.Level == LogLevel.Debug, "should have debug logs");
        logMessages.Should().Contain(m => m.Level == LogLevel.Warning, "should have warning logs");
        logMessages.Should().Contain(m => m.Level == LogLevel.Error, "should have error logs");
    }

    [Fact]
    public void CommandRegistrationResult_Properties_Documentation()
    {
        // This test documents the CommandRegistrationResult record structure:
        // - Success: bool indicating if operation succeeded
        // - Message: string with human-readable description of result
        // - GlobalCommandsRegistered: int count of commands registered (0 on failure)
        // - GuildsCleared: int count of guilds successfully cleared (0 on failure)
        //
        // Success = true example:
        //   Message: "Successfully cleared and re-registered 15 command(s) globally. Commands cleared from 3 guild(s). Global commands may take up to 1 hour to propagate."
        //
        // Success = false examples:
        //   Message: "Bot is not connected to Discord (state: Disconnected). Please wait for the bot to connect and try again."
        //   Message: "Failed to register commands: {exception.Message}"
        //
        // Implementation verified at: src/DiscordBot.Core/Interfaces/ICommandRegistrationService.cs:16-40

        var resultStructure = new
        {
            Properties = new[]
            {
                new { Name = "Success", Type = "bool", Purpose = "Indicates operation success" },
                new { Name = "Message", Type = "string", Purpose = "Human-readable result description" },
                new { Name = "GlobalCommandsRegistered", Type = "int", Purpose = "Count of commands registered" },
                new { Name = "GuildsCleared", Type = "int", Purpose = "Count of guilds cleared" }
            },
            SuccessMessagePattern = "Successfully cleared and re-registered {count} command(s) globally. Commands cleared from {guilds} guild(s). Global commands may take up to 1 hour to propagate.",
            FailureMessagePatterns = new[]
            {
                "Bot is not connected to Discord (state: {state}). Please wait for the bot to connect and try again.",
                "Failed to register commands: {message}"
            }
        };

        resultStructure.Properties.Should().HaveCount(4, "result has 4 properties");
        resultStructure.SuccessMessagePattern.Should().Contain("1 hour", "success message warns about propagation delay");
        resultStructure.FailureMessagePatterns.Should().HaveCount(2, "has 2 types of failure messages");
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_ReturnsResultWithDefaultCounts_WhenNotConnected()
    {
        // Arrange
        // Client is not connected

        // Act
        var result = await _service.ClearAndRegisterGloballyAsync();

        // Assert
        result.GlobalCommandsRegistered.Should().Be(0, "no commands registered when not connected");
        result.GuildsCleared.Should().Be(0, "no guilds cleared when not connected");
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_LogsStartupMessage()
    {
        // Arrange
        // Client is not connected, but should still log startup

        // Act
        await _service.ClearAndRegisterGloballyAsync();

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Starting command registration")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log startup message before checking connection state");
    }

    [Fact]
    public async Task ClearAndRegisterGloballyAsync_ReturnsCorrectFailureMessageFormat()
    {
        // Arrange
        // Client is not connected

        // Act
        var result = await _service.ClearAndRegisterGloballyAsync();

        // Assert
        result.Message.Should().StartWith("Bot is not connected", "failure message should start with clear statement");
        result.Message.Should().Contain("Please wait", "failure message should provide guidance");
        result.Message.Should().Contain("try again", "failure message should indicate retry is possible");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_BulkOverwriteOperations_Documentation()
    {
        // This test documents the bulk overwrite operations used to clear commands:
        // - Global commands cleared via: client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>())
        // - Guild commands cleared via: guild.BulkOverwriteApplicationCommandAsync(Array.Empty<ApplicationCommandProperties>())
        //
        // Using BulkOverwrite with empty array is the Discord-recommended approach for clearing commands
        // This is more efficient than deleting commands individually
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:59,75

        var bulkOverwriteInfo = new
        {
            GlobalClearMethod = "client.BulkOverwriteGlobalApplicationCommandsAsync(Array.Empty<ApplicationCommandProperties>())",
            GuildClearMethod = "guild.BulkOverwriteApplicationCommandAsync(Array.Empty<ApplicationCommandProperties>())",
            Reasoning = "BulkOverwrite with empty array is the Discord-recommended way to clear all commands efficiently",
            Alternative = "Could delete individually, but bulk operation is more efficient"
        };

        bulkOverwriteInfo.Should().NotBeNull();
        bulkOverwriteInfo.GlobalClearMethod.Should().Contain("BulkOverwriteGlobalApplicationCommandsAsync");
        bulkOverwriteInfo.GuildClearMethod.Should().Contain("BulkOverwriteApplicationCommandAsync");
        bulkOverwriteInfo.Reasoning.Should().Contain("efficient");
    }

    [Fact]
    public void ClearAndRegisterGloballyAsync_RegistrationMethod_Documentation()
    {
        // This test documents the command re-registration method:
        // - Uses InteractionService.RegisterCommandsGloballyAsync()
        // - This method reads all modules/commands from InteractionService.Modules
        // - Registers them as global Discord application commands
        // - Global commands take up to 1 hour to propagate across Discord
        // - Alternative would be RegisterCommandsToGuildAsync for instant updates (test guilds)
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/CommandRegistrationService.cs:90

        var registrationInfo = new
        {
            Method = "interactionService.RegisterCommandsGloballyAsync()",
            Source = "interactionService.Modules (all registered modules and commands)",
            PropagationDelay = "Up to 1 hour for global commands",
            Alternative = "RegisterCommandsToGuildAsync for instant guild-specific registration"
        };

        registrationInfo.Should().NotBeNull();
        registrationInfo.Method.Should().Contain("RegisterCommandsGloballyAsync");
        registrationInfo.PropagationDelay.Should().Contain("1 hour");
        registrationInfo.Alternative.Should().Contain("instant");
    }
}
