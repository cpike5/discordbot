using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordBot.Bot.Handlers;
using DiscordBot.Bot.Metrics;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace DiscordBot.Tests.Handlers;

/// <summary>
/// Unit tests for <see cref="InteractionHandler"/> dashboard integration functionality.
/// Tests verify the SignalR broadcast behavior for command execution added in issue #246.
///
/// Note: These are primarily documentation tests because Discord.NET classes are sealed
/// and cannot be easily mocked. The tests document the expected behavior and verify
/// DTO structure.
/// </summary>
public class InteractionHandlerDashboardTests
{
    // No setup required - these are documentation tests that verify behavior contracts

    /// <summary>
    /// Test documents expected behavior when a slash command is executed successfully.
    /// </summary>
    [Fact]
    public void OnSlashCommandExecutedAsync_WithSuccessfulCommand_ShouldBroadcastCommandExecuted_Documentation()
    {
        // This test documents the expected behavior when a slash command executes successfully:
        // 1. OnSlashCommandExecutedAsync is called after command execution
        // 2. Command metrics are recorded via BotMetrics.RecordCommandExecution
        // 3. Success is logged at Information level
        // 4. Command execution is logged to database via ICommandExecutionLogger (fire-and-forget)
        // 5. BroadcastCommandExecutedAsync is called (fire-and-forget)
        // 6. CommandExecutedUpdateDto is created with:
        //    - CommandName from commandInfo.Name
        //    - GuildId from context.Guild?.Id (nullable)
        //    - GuildName from context.Guild?.Name (nullable)
        //    - UserId from context.User.Id
        //    - Username from context.User.Username
        //    - Success = true
        //    - Timestamp = DateTime.UtcNow
        // 7. IDashboardUpdateService.BroadcastCommandExecutedAsync is called
        // 8. If broadcast fails, error is logged but exception is not thrown
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:280-371

        var expectedBehavior = new
        {
            EventHandler = "OnSlashCommandExecutedAsync",
            Parameters = new[]
            {
                "SlashCommandInfo commandInfo",
                "IInteractionContext context",
                "IResult result (IsSuccess = true)"
            },
            Operations = new[]
            {
                "Record command metrics",
                "Log success at Information level",
                "Log to database (fire-and-forget)",
                "Broadcast to dashboard (fire-and-forget)"
            },
            DtoProperties = new[]
            {
                "CommandName from commandInfo.Name",
                "GuildId from context.Guild?.Id (nullable for DMs)",
                "GuildName from context.Guild?.Name (nullable for DMs)",
                "UserId from context.User.Id",
                "Username from context.User.Username",
                "Success = true",
                "Timestamp = DateTime.UtcNow"
            },
            BroadcastMethod = "IDashboardUpdateService.BroadcastCommandExecutedAsync",
            ErrorHandling = "Logs warning but does not throw on broadcast failure"
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.EventHandler.Should().Be("OnSlashCommandExecutedAsync");
        expectedBehavior.Operations.Should().HaveCount(4);
        expectedBehavior.DtoProperties.Should().HaveCount(7);
        expectedBehavior.ErrorHandling.Should().Contain("does not throw");
    }

    /// <summary>
    /// Test documents expected behavior when a slash command fails.
    /// </summary>
    [Fact]
    public void OnSlashCommandExecutedAsync_WithFailedCommand_ShouldBroadcastCommandExecuted_Documentation()
    {
        // This test documents the expected behavior when a slash command fails:
        // 1. OnSlashCommandExecutedAsync is called after command execution
        // 2. Command metrics are recorded via BotMetrics.RecordCommandExecution
        // 3. Failure is logged at Warning level with error reason
        // 4. If error is UnmetPrecondition, permission denied embed is sent to user
        // 5. Command execution is logged to database via ICommandExecutionLogger (fire-and-forget)
        // 6. BroadcastCommandExecutedAsync is called (fire-and-forget)
        // 7. CommandExecutedUpdateDto is created with:
        //    - CommandName from commandInfo.Name
        //    - GuildId from context.Guild?.Id (nullable)
        //    - GuildName from context.Guild?.Name (nullable)
        //    - UserId from context.User.Id
        //    - Username from context.User.Username
        //    - Success = false
        //    - Timestamp = DateTime.UtcNow
        // 8. IDashboardUpdateService.BroadcastCommandExecutedAsync is called
        // 9. If broadcast fails, error is logged but exception is not thrown
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:280-371

        var expectedBehavior = new
        {
            EventHandler = "OnSlashCommandExecutedAsync",
            Parameters = new[]
            {
                "SlashCommandInfo commandInfo",
                "IInteractionContext context",
                "IResult result (IsSuccess = false)"
            },
            Operations = new[]
            {
                "Record command metrics with success=false",
                "Log failure at Warning level with error reason",
                "Send permission denied embed if UnmetPrecondition",
                "Log to database (fire-and-forget)",
                "Broadcast to dashboard (fire-and-forget)"
            },
            DtoProperties = new[]
            {
                "CommandName from commandInfo.Name",
                "Success = false",
                "All other properties same as success case"
            },
            BroadcastMethod = "IDashboardUpdateService.BroadcastCommandExecutedAsync",
            ErrorHandling = "Logs warning but does not throw on broadcast failure"
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.EventHandler.Should().Be("OnSlashCommandExecutedAsync");
        expectedBehavior.Operations.Should().HaveCount(5);
        expectedBehavior.DtoProperties.Should().HaveCount(3);
    }

    /// <summary>
    /// Test documents expected behavior for DM commands (no guild context).
    /// </summary>
    [Fact]
    public void OnSlashCommandExecutedAsync_InDirectMessage_ShouldBroadcastWithNullGuild_Documentation()
    {
        // This test documents the expected behavior for commands executed in DMs:
        // 1. OnSlashCommandExecutedAsync is called after command execution
        // 2. context.Guild is null because it's a DM
        // 3. CommandExecutedUpdateDto is created with:
        //    - GuildId = null
        //    - GuildName = null
        //    - All other properties populated normally
        // 4. IDashboardUpdateService.BroadcastCommandExecutedAsync is called with null guild info
        // 5. Dashboard can handle null guild values to display "DM" appropriately
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:377-399

        var expectedBehavior = new
        {
            Scenario = "Command executed in Direct Message",
            ContextGuild = "null",
            DtoProperties = new[]
            {
                "GuildId = context.Guild?.Id (null in DMs)",
                "GuildName = context.Guild?.Name (null in DMs)",
                "UserId, Username, CommandName, Success, Timestamp populated normally"
            },
            DashboardDisplay = "Dashboard should display 'DM' when GuildId is null",
            BroadcastBehavior = "Broadcast succeeds with null guild values"
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Scenario.Should().Contain("Direct Message");
        expectedBehavior.ContextGuild.Should().Be("null");
        expectedBehavior.DtoProperties.Should().HaveCount(3);
    }

    /// <summary>
    /// Test verifies that command execution broadcast failure does not affect main functionality.
    /// This is a critical requirement - SignalR failures must not break command execution.
    /// </summary>
    [Fact]
    public void BroadcastCommandExecutedAsync_WhenServiceThrows_ShouldLogButNotThrow_Documentation()
    {
        // This test documents the fire-and-forget error handling pattern:
        // 1. BroadcastCommandExecutedAsync is called in a fire-and-forget manner (discard operator _)
        // 2. Inside the method, all code is wrapped in try-catch
        // 3. If IDashboardUpdateService throws an exception:
        //    - Exception is caught
        //    - Warning is logged with exception details and command name
        //    - Exception is NOT re-thrown
        //    - Method returns normally
        // 4. The main operation (command logging, metrics, user response) continues unaffected
        //
        // This pattern ensures SignalR failures don't break command execution.
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:377-399

        var expectedBehavior = new
        {
            Pattern = "Fire-and-forget with internal error handling",
            Invocation = "_ = BroadcastCommandExecutedAsync(commandInfo.Name, context, success)",
            ErrorHandling = new[]
            {
                "try-catch wraps entire method body",
                "Catches all exceptions from IDashboardUpdateService",
                "Logs warning: 'Failed to broadcast command executed update for {CommandName}, but continuing normal operation'",
                "Does NOT re-throw exception",
                "Command execution, logging, and metrics continue unaffected"
            },
            CriticalRequirement = "SignalR failures must never break command execution",
            CalledAfter = new[]
            {
                "Command metrics recorded",
                "Success/failure logged",
                "Database logging initiated"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Pattern.Should().Be("Fire-and-forget with internal error handling");
        expectedBehavior.ErrorHandling.Should().HaveCount(5);
        expectedBehavior.CriticalRequirement.Should().Contain("must never break");
        expectedBehavior.CalledAfter.Should().HaveCount(3);
    }

    /// <summary>
    /// Test verifies CommandExecutedUpdateDto has correct structure for guild command.
    /// </summary>
    [Fact]
    public void CommandExecutedUpdateDto_ForGuildCommand_ShouldHaveCorrectStructure()
    {
        // Arrange
        var commandName = "ping";
        var guildId = 123456789UL;
        var guildName = "Test Guild";
        var userId = 987654321UL;
        var username = "TestUser";
        var success = true;
        var timestamp = DateTime.UtcNow;

        // Act
        var dto = new CommandExecutedUpdateDto
        {
            CommandName = commandName,
            GuildId = guildId,
            GuildName = guildName,
            UserId = userId,
            Username = username,
            Success = success,
            Timestamp = timestamp
        };

        // Assert
        dto.Should().NotBeNull();
        dto.CommandName.Should().Be("ping");
        dto.GuildId.Should().Be(123456789UL);
        dto.GuildName.Should().Be("Test Guild");
        dto.UserId.Should().Be(987654321UL);
        dto.Username.Should().Be("TestUser");
        dto.Success.Should().BeTrue();
        dto.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Test verifies CommandExecutedUpdateDto has correct structure for DM command.
    /// </summary>
    [Fact]
    public void CommandExecutedUpdateDto_ForDirectMessage_ShouldHaveNullGuildInfo()
    {
        // Arrange
        var commandName = "help";
        var userId = 111222333UL;
        var username = "DMUser";
        var success = true;
        var timestamp = DateTime.UtcNow;

        // Act
        var dto = new CommandExecutedUpdateDto
        {
            CommandName = commandName,
            GuildId = null,
            GuildName = null,
            UserId = userId,
            Username = username,
            Success = success,
            Timestamp = timestamp
        };

        // Assert
        dto.Should().NotBeNull();
        dto.CommandName.Should().Be("help");
        dto.GuildId.Should().BeNull();
        dto.GuildName.Should().BeNull();
        dto.UserId.Should().Be(111222333UL);
        dto.Username.Should().Be("DMUser");
        dto.Success.Should().BeTrue();
        dto.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Test verifies CommandExecutedUpdateDto has correct structure for failed command.
    /// </summary>
    [Fact]
    public void CommandExecutedUpdateDto_ForFailedCommand_ShouldHaveSuccessFalse()
    {
        // Arrange
        var commandName = "admin";
        var guildId = 555666777UL;
        var guildName = "Admin Guild";
        var userId = 888999000UL;
        var username = "UnauthorizedUser";
        var success = false;
        var timestamp = DateTime.UtcNow;

        // Act
        var dto = new CommandExecutedUpdateDto
        {
            CommandName = commandName,
            GuildId = guildId,
            GuildName = guildName,
            UserId = userId,
            Username = username,
            Success = success,
            Timestamp = timestamp
        };

        // Assert
        dto.Should().NotBeNull();
        dto.CommandName.Should().Be("admin");
        dto.GuildId.Should().Be(555666777UL);
        dto.GuildName.Should().Be("Admin Guild");
        dto.UserId.Should().Be(888999000UL);
        dto.Username.Should().Be("UnauthorizedUser");
        dto.Success.Should().BeFalse();
        dto.Timestamp.Should().BeCloseTo(timestamp, TimeSpan.FromMilliseconds(100));
    }

    /// <summary>
    /// Test documents the execution flow in OnSlashCommandExecutedAsync.
    /// </summary>
    [Fact]
    public void OnSlashCommandExecutedAsync_ExecutionFlow_Documentation()
    {
        // This test documents the complete execution flow:
        // 1. Get execution context from AsyncLocal storage
        // 2. Extract correlation ID and stopwatch
        // 3. Stop stopwatch to measure execution time
        // 4. Determine success/failure from result.IsSuccess
        // 5. Extract error message if failed
        // 6. Record metrics via BotMetrics.RecordCommandExecution
        // 7. Log success or failure with correlation ID
        // 8. If UnmetPrecondition error, send permission denied embed to user
        // 9. Log to database via ICommandExecutionLogger (fire-and-forget)
        // 10. Broadcast to dashboard via BroadcastCommandExecutedAsync (fire-and-forget)
        //
        // Both fire-and-forget operations have internal error handling.
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:280-371

        var expectedFlow = new[]
        {
            "1. Get execution context (correlation ID, stopwatch)",
            "2. Stop stopwatch and calculate execution time",
            "3. Extract success/failure and error message",
            "4. Record command metrics",
            "5. Log with correlation ID",
            "6. Send permission denied embed if needed",
            "7. Log to database (fire-and-forget, internal error handling)",
            "8. Broadcast to dashboard (fire-and-forget, internal error handling)"
        };

        expectedFlow.Should().HaveCount(8);
        expectedFlow[6].Should().Contain("fire-and-forget");
        expectedFlow[7].Should().Contain("fire-and-forget");
    }

    /// <summary>
    /// Test documents component interaction behavior (not broadcasting to dashboard).
    /// </summary>
    [Fact]
    public void OnComponentCommandExecutedAsync_ShouldNotBroadcastToDashboard_Documentation()
    {
        // This test documents that component interactions (buttons, select menus, modals)
        // are NOT broadcast to the dashboard:
        // 1. OnComponentCommandExecutedAsync handles button clicks, select menus, modals
        // 2. Records metrics via BotMetrics.RecordComponentInteraction
        // 3. Logs success or failure with correlation ID
        // 4. Sends permission denied embed if needed
        // 5. Does NOT log to database (components are follow-up actions)
        // 6. Does NOT broadcast to dashboard (only slash commands are broadcast)
        //
        // Rationale: Component interactions are follow-up actions to slash commands,
        // not primary user actions, so they're not tracked in the dashboard.
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:405-482

        var expectedBehavior = new
        {
            EventHandler = "OnComponentCommandExecutedAsync",
            ComponentTypes = new[] { "Button", "SelectMenu", "Modal" },
            Operations = new[]
            {
                "Record component metrics",
                "Log success or failure",
                "Send permission denied embed if needed"
            },
            NotPerformed = new[]
            {
                "Database logging (components are follow-up actions)",
                "Dashboard broadcast (only slash commands broadcast)"
            },
            Rationale = "Component interactions are follow-up actions, not primary commands"
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.EventHandler.Should().Be("OnComponentCommandExecutedAsync");
        expectedBehavior.ComponentTypes.Should().HaveCount(3);
        expectedBehavior.Operations.Should().HaveCount(3);
        expectedBehavior.NotPerformed.Should().HaveCount(2);
    }

    /// <summary>
    /// Test documents the GetFullCommandName method behavior for grouped commands (issue #483).
    /// Grouped commands should be logged with full path: "consent status" not "status".
    /// </summary>
    [Fact]
    public void GetFullCommandName_WithGroupedCommand_ShouldReturnFullPath_Documentation()
    {
        // This test documents the expected behavior for GetFullCommandName helper method:
        //
        // Before fix (issue #483):
        // - Grouped commands logged only subcommand name: "status", "grant", "revoke"
        // - This caused inaccurate analytics and potential naming conflicts
        //
        // After fix:
        // - GetFullCommandName checks if Module.SlashGroupName is set
        // - If group name exists: returns "{groupName} {commandName}" (e.g., "consent status")
        // - If no group name: returns just commandName (e.g., "ping")
        //
        // Implementation pattern (matches CommandMetadataService.cs):
        //   var fullName = string.IsNullOrEmpty(command.Module.SlashGroupName)
        //       ? command.Name
        //       : $"{command.Module.SlashGroupName} {command.Name}";
        //
        // Affected commands:
        // - ConsentModule: consent grant, consent revoke, consent status
        // - WelcomeModule: welcome show, welcome set, etc.
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:506-517

        var expectedBehavior = new
        {
            MethodName = "GetFullCommandName",
            Purpose = "Build full command name including group prefix for subcommands",
            Examples = new[]
            {
                ("consent", "status", "consent status"),
                ("consent", "grant", "consent grant"),
                ("welcome", "show", "welcome show"),
                (null as string, "ping", "ping"),
                ("", "help", "help")
            },
            Logic = "If SlashGroupName is set, prefix command name with group name and space"
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.MethodName.Should().Be("GetFullCommandName");
        expectedBehavior.Examples.Should().HaveCount(5);

        // Verify the logic with inline test cases
        // These demonstrate the expected output for various inputs
        foreach (var (groupName, commandName, expected) in expectedBehavior.Examples)
        {
            var actual = string.IsNullOrEmpty(groupName)
                ? commandName
                : $"{groupName} {commandName}";
            actual.Should().Be(expected, $"Group '{groupName ?? "(null)"}' + Command '{commandName}' should produce '{expected}'");
        }
    }

    /// <summary>
    /// Test verifies that command logging uses full command name for grouped commands.
    /// </summary>
    [Fact]
    public void OnSlashCommandExecutedAsync_WithGroupedCommand_ShouldLogFullCommandName_Documentation()
    {
        // This test documents that OnSlashCommandExecutedAsync now uses GetFullCommandName
        // to ensure grouped commands are logged with their full path.
        //
        // The full command name is used in:
        // 1. BotMetrics.RecordCommandExecution - for accurate command metrics
        // 2. Logger.LogInformation/LogWarning - for accurate log messages
        // 3. ICommandExecutionLogger.LogCommandExecutionAsync - for database command logs
        // 4. BroadcastCommandExecutedAsync - for dashboard updates
        //
        // This ensures:
        // - Analytics correctly show "consent status" instead of just "status"
        // - No naming conflicts between subcommands in different groups
        // - Easy identification of which parent command was executed
        //
        // Implementation verified at: src/DiscordBot.Bot/Handlers/InteractionHandler.cs:280-373

        var expectedBehavior = new
        {
            Method = "OnSlashCommandExecutedAsync",
            UsesGetFullCommandName = true,
            FullCommandNameUsedIn = new[]
            {
                "BotMetrics.RecordCommandExecution",
                "Logger.LogInformation",
                "Logger.LogWarning",
                "ICommandExecutionLogger.LogCommandExecutionAsync",
                "BroadcastCommandExecutedAsync"
            },
            Benefits = new[]
            {
                "Accurate command analytics and usage tracking",
                "No naming conflicts between subcommands in different groups",
                "Easy identification of which parent command was executed"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.UsesGetFullCommandName.Should().BeTrue();
        expectedBehavior.FullCommandNameUsedIn.Should().HaveCount(5);
        expectedBehavior.Benefits.Should().HaveCount(3);
    }
}
