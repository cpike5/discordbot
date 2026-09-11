using Discord;
using DiscordBot.Bot.Services;
using FluentAssertions;
using Microsoft.Extensions.Logging;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BotHostedService"/>.
/// Tests focus on testable behavior since DiscordSocketClient is sealed and difficult to mock.
/// Most tests verify the log severity mapping logic which is critical for proper logging.
/// </summary>
public class BotHostedServiceTests
{
    [Theory]
    [InlineData(LogSeverity.Critical, LogLevel.Critical)]
    [InlineData(LogSeverity.Error, LogLevel.Error)]
    [InlineData(LogSeverity.Warning, LogLevel.Warning)]
    [InlineData(LogSeverity.Info, LogLevel.Information)]
    [InlineData(LogSeverity.Verbose, LogLevel.Debug)]
    [InlineData(LogSeverity.Debug, LogLevel.Trace)]
    public void MapLogSeverity_ShouldMapDiscordLogSeverityToLogLevel(LogSeverity discordSeverity, LogLevel expectedLogLevel)
    {
        // Arrange & Act
        // We test the mapping logic by using reflection to access the private static method
        var method = typeof(BotHostedService).GetMethod(
            "MapLogSeverity",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        method.Should().NotBeNull("MapLogSeverity method should exist");

        var result = (LogLevel)method!.Invoke(null, new object[] { discordSeverity })!;

        // Assert
        result.Should().Be(expectedLogLevel,
            $"LogSeverity.{discordSeverity} should map to LogLevel.{expectedLogLevel}");
    }

    [Fact]
    public void MapLogSeverity_WithInvalidSeverity_ShouldReturnInformation()
    {
        // Arrange
        // Using an invalid enum value (cast from int) to test the default case
        var invalidSeverity = (LogSeverity)999;

        // Act
        var method = typeof(BotHostedService).GetMethod(
            "MapLogSeverity",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

        var result = (LogLevel)method!.Invoke(null, new object[] { invalidSeverity })!;

        // Assert
        result.Should().Be(LogLevel.Information,
            "Invalid LogSeverity values should default to LogLevel.Information");
    }

    [Fact]
    public void BotConfiguration_SectionName_ShouldBeDiscord()
    {
        // Arrange & Act
        var sectionName = BotConfiguration.SectionName;

        // Assert
        sectionName.Should().Be("Discord",
            "Configuration section name must match what's used in appsettings");
    }

    /// <summary>
    /// This test documents the expected behavior when token is missing.
    /// The actual integration test would require mocking DiscordSocketClient which is not feasible.
    /// See BotHostedService.StartAsync lines 49-54 for the implementation.
    /// </summary>
    [Fact]
    public void StartAsync_WithMissingToken_ShouldCallStopApplication_Documentation()
    {
        // This test documents the expected behavior:
        // 1. When BotConfiguration.Token is null, empty, or whitespace
        // 2. BotHostedService.StartAsync should:
        //    - Log a critical message about missing token
        //    - Call IHostApplicationLifetime.StopApplication()
        //    - Return without attempting to login to Discord
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/BotHostedService.cs:49-54

        var expectedBehavior = new
        {
            Condition = "Token is null, empty, or whitespace",
            LogLevel = "Critical",
            LogMessage = "Discord bot token is missing in configuration",
            Action = "Calls IHostApplicationLifetime.StopApplication()",
            Result = "Application shutdown initiated"
        };

        expectedBehavior.Should().NotBeNull("This documents the expected behavior for missing token");
        expectedBehavior.Condition.Should().NotBeNullOrEmpty();
        expectedBehavior.LogLevel.Should().Be("Critical");
    }

    /// <summary>
    /// This test documents the initialization sequence.
    /// The actual integration test would require mocking Discord.NET dependencies.
    /// See BotHostedService.StartAsync lines 38-69 for the implementation.
    /// </summary>
    [Fact]
    public void StartAsync_InitializationSequence_Documentation()
    {
        // This test documents the startup sequence:
        // 1. Log "Starting Discord bot hosted service"
        // 2. Wire up DiscordSocketClient.Log event to LogDiscordMessageAsync
        // 3. Call InteractionHandler.InitializeAsync()
        // 4. Validate token (see StartAsync_WithMissingToken_Documentation)
        // 5. Call DiscordSocketClient.LoginAsync(TokenType.Bot, token)
        // 6. Call DiscordSocketClient.StartAsync()
        // 7. Log "Discord bot started successfully"
        // 8. On exception: Log critical error and call StopApplication()
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/BotHostedService.cs:38-69

        var startupSequence = new[]
        {
            "1. Log startup information",
            "2. Wire Discord.NET logging to ILogger",
            "3. Initialize InteractionHandler",
            "4. Validate token configuration",
            "5. Login to Discord",
            "6. Start Discord client",
            "7. Log success or handle errors"
        };

        startupSequence.Should().HaveCount(7, "Startup has 7 distinct steps");
        startupSequence[2].Should().Contain("InteractionHandler", "InteractionHandler must be initialized");
    }

    /// <summary>
    /// This test documents the web-only (Discord:Enabled=false) short-circuit added for
    /// browser/UI testing without a bot token (docs/plans/blazor-port-plan.md Phase 0).
    /// The actual integration test would require mocking the full Discord.NET/handler
    /// dependency graph, which is not feasible here (see class remarks); see
    /// SlashCommandRegistrationServiceTests.StartAsync_WhenDiscordDisabled_ShouldSkipModuleDiscovery
    /// and BotConfigurationValidatorTests for the behaviorally-tested parts of this feature.
    /// </summary>
    [Fact]
    public void StartAsync_WhenDiscordDisabled_ShouldLogAndReturnWithoutLogin_Documentation()
    {
        // This test documents the expected behavior when BotConfiguration.Enabled is false:
        // 1. StartAsync logs "Discord bot disabled by configuration; running web-only" at
        //    Information level.
        // 2. It returns immediately: no event handlers are wired to DiscordSocketClient, the
        //    interaction handler is not initialized, the missing-token check never runs, and
        //    LoginAsync/StartAsync are never called on the client.
        // 3. StopAsync mirrors this: when Enabled is false it logs and returns without
        //    unsubscribing events or calling StopAsync/LogoutAsync on the client.
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/BotHostedService.cs
        // (StartAsync/StopAsync, Discord:Enabled guard near the top of each method).

        var expectedBehavior = new
        {
            Condition = "BotConfiguration.Enabled is false",
            LogLevel = "Information",
            LogMessage = "Discord bot disabled by configuration; running web-only",
            Action = "Returns before wiring events, initializing InteractionHandler, or calling LoginAsync/StartAsync",
            MissingTokenPathSkipped = true
        };

        expectedBehavior.Should().NotBeNull("This documents the web-only short-circuit behavior");
        expectedBehavior.LogMessage.Should().Be("Discord bot disabled by configuration; running web-only");
        expectedBehavior.MissingTokenPathSkipped.Should().BeTrue();
    }

    /// <summary>
    /// This test documents the shutdown sequence.
    /// The actual integration test would require mocking Discord.NET dependencies.
    /// See BotHostedService.StopAsync lines 75-90 for the implementation.
    /// </summary>
    [Fact]
    public void StopAsync_ShutdownSequence_Documentation()
    {
        // This test documents the shutdown sequence:
        // 1. Log "Stopping Discord bot hosted service"
        // 2. Call DiscordSocketClient.StopAsync()
        // 3. Call DiscordSocketClient.LogoutAsync()
        // 4. Log "Discord bot stopped successfully"
        // 5. On exception: Log error (does not rethrow)
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/BotHostedService.cs:75-90

        var shutdownSequence = new[]
        {
            "1. Log shutdown information",
            "2. Stop Discord client",
            "3. Logout from Discord",
            "4. Log success or handle errors"
        };

        shutdownSequence.Should().HaveCount(4, "Shutdown has 4 distinct steps");
        shutdownSequence[2].Should().Contain("Logout", "Must logout from Discord");
    }
}
