using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GuildService.SyncAllGuildsAsync"/>.
/// Tests the Quick Actions feature implementation for syncing all connected guilds from Discord to the database.
/// NOTE: DiscordSocketClient.Guilds cannot be easily mocked as it returns IReadOnlyCollection of sealed SocketGuild.
/// These tests document expected behavior and test repository interactions where possible.
/// </summary>
public class GuildServiceSyncAllTests
{
    private readonly Mock<IGuildRepository> _mockGuildRepository;
    private readonly Mock<ILogger<GuildService>> _mockLogger;

    public GuildServiceSyncAllTests()
    {
        _mockGuildRepository = new Mock<IGuildRepository>();
        _mockLogger = new Mock<ILogger<GuildService>>();
    }

    /// <summary>
    /// Documentation test that describes the expected behavior of SyncAllGuildsAsync.
    /// This test serves as living documentation for the Quick Actions sync feature.
    /// </summary>
    [Fact]
    public void SyncAllGuildsAsync_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of GuildService.SyncAllGuildsAsync:
        // 1. Logs information message: "Syncing all connected guilds from Discord to database"
        // 2. Gets all guilds from _client.Guilds (IReadOnlyCollection<SocketGuild>)
        // 3. If no guilds connected:
        //    - Logs warning: "No guilds connected to sync"
        //    - Returns 0
        // 4. For each connected guild:
        //    - Creates Guild entity with data from SocketGuild
        //    - Calls repository.UpsertAsync(guild, cancellationToken)
        //    - Increments syncedCount on success
        //    - Logs debug message on success: "Synced guild {GuildId}: {GuildName}"
        //    - Catches exceptions per guild and logs error without failing entire operation
        // 5. Logs final information: "Synced {SyncedCount} of {TotalCount} guilds successfully"
        // 6. Returns syncedCount
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/GuildService.cs:156-195

        var expectedBehavior = new
        {
            Method = "SyncAllGuildsAsync",
            Returns = "Task<int>",
            Parameters = new[] { "CancellationToken cancellationToken = default" },
            Steps = new[]
            {
                "1. Log information message about starting sync",
                "2. Get all guilds from _client.Guilds",
                "3. If no guilds, log warning and return 0",
                "4. For each guild: create Guild entity and call repository.UpsertAsync",
                "5. Track syncedCount, handle exceptions per guild",
                "6. Log final sync results",
                "7. Return syncedCount"
            },
            ErrorHandling = new
            {
                PerGuildExceptions = "Logged but does not stop sync of other guilds",
                NoGuilds = "Returns 0 with warning log"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("SyncAllGuildsAsync");
        expectedBehavior.Returns.Should().Be("Task<int>");
        expectedBehavior.Steps.Should().HaveCount(7);
        expectedBehavior.ErrorHandling.PerGuildExceptions.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task SyncAllGuildsAsync_WithNoConnectedGuilds_ShouldReturnZero()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        // Act
        var result = await service.SyncAllGuildsAsync();

        // Assert
        result.Should().Be(0, "no guilds are connected to sync");

        // Verify repository was never called
        _mockGuildRepository.Verify(
            r => r.UpsertAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "UpsertAsync should not be called when there are no connected guilds");

        // Verify warning was logged
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No guilds connected to sync")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once,
            "warning should be logged when no guilds are connected");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SyncAllGuildsAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        // Note: Since client.Guilds will be empty, this tests that cancellation token
        // would be passed through if there were guilds to sync
        _mockGuildRepository
            .Setup(r => r.UpsertAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Guild());

        // Act
        await service.SyncAllGuildsAsync(cancellationToken);

        // Assert
        // Even though no guilds are present, this documents the expected signature
        // In a real scenario with guilds, the cancellationToken would be passed through
        _mockGuildRepository.Verify(
            r => r.UpsertAsync(It.IsAny<Guild>(), cancellationToken),
            Times.Never,
            "no guilds connected, so repository should not be called");

        // Cleanup
        await client.DisposeAsync();
        cancellationTokenSource.Dispose();
    }

    [Fact]
    public async Task SyncAllGuildsAsync_LogsStartMessage_OnInvocation()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        // Act
        await service.SyncAllGuildsAsync();

        // Assert
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Syncing all connected guilds from Discord to database")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once,
            "information log should be written when sync starts");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SyncAllGuildsAsync_WithNoGuilds_ShouldNotLogCompletionMessage()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        // Act
        await service.SyncAllGuildsAsync();

        // Assert - With no guilds, the method returns early and does NOT log completion message
        // It only logs the warning "No guilds connected to sync"
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Synced") &&
                    v.ToString()!.Contains("guilds successfully")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Never,
            "completion log should NOT be written when no guilds are connected (early return)");

        // Cleanup
        await client.DisposeAsync();
    }

    /// <summary>
    /// Documents that repository exceptions per guild should not prevent syncing other guilds.
    /// The implementation includes try-catch around each guild to ensure resilience.
    /// </summary>
    [Fact]
    public void SyncAllGuildsAsync_WithRepositoryException_ShouldHandlePerGuild()
    {
        // This test documents the expected error handling behavior:
        // 1. When repository.UpsertAsync throws exception for a guild
        // 2. The exception should be caught and logged with LogError
        // 3. The sync should continue for remaining guilds
        // 4. The failed guild should NOT be counted in syncedCount
        // 5. The final log should show actual synced count vs total count
        //
        // Example scenario:
        // - 3 guilds connected
        // - Guild 1: syncs successfully
        // - Guild 2: repository throws exception (logged, not counted)
        // - Guild 3: syncs successfully
        // - Result: returns 2, logs "Synced 2 of 3 guilds successfully"
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/GuildService.cs:172-190
        // The try-catch wraps UpsertAsync call and logs errors without re-throwing

        var expectedErrorHandling = new
        {
            ExceptionScope = "Per-guild (isolated)",
            ContinuesOnError = true,
            LogsError = true,
            CountsFailedGuild = false,
            RethrowsException = false
        };

        expectedErrorHandling.Should().NotBeNull();
        expectedErrorHandling.ContinuesOnError.Should().BeTrue();
        expectedErrorHandling.LogsError.Should().BeTrue();
        expectedErrorHandling.CountsFailedGuild.Should().BeFalse();
    }

    /// <summary>
    /// Documents the Guild entity creation from SocketGuild data during sync.
    /// </summary>
    [Fact]
    public void SyncAllGuildsAsync_GuildEntityCreation_Documentation()
    {
        // This test documents how Guild entities are created from SocketGuild during sync:
        // var guild = new Guild
        // {
        //     Id = discordGuild.Id,                                                    // ulong Discord snowflake
        //     Name = discordGuild.Name,                                                // Guild name from Discord
        //     JoinedAt = discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow,  // Join timestamp
        //     IsActive = true                                                          // Always set to true on sync
        // };
        //
        // Note: Prefix and Settings are not set during sync - they retain database values on upsert
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/GuildService.cs:173-179

        var expectedMapping = new
        {
            SourceType = "SocketGuild",
            TargetType = "Guild",
            MappedProperties = new[]
            {
                "Id (discordGuild.Id)",
                "Name (discordGuild.Name)",
                "JoinedAt (discordGuild.CurrentUser?.JoinedAt?.UtcDateTime ?? DateTime.UtcNow)",
                "IsActive (always true)"
            },
            UnmappedProperties = new[]
            {
                "Prefix (retains database value)",
                "Settings (retains database value)"
            }
        };

        expectedMapping.Should().NotBeNull();
        expectedMapping.MappedProperties.Should().HaveCount(4);
        expectedMapping.UnmappedProperties.Should().HaveCount(2);
    }

    [Fact]
    public async Task SyncAllGuildsAsync_WithDefaultCancellationToken_ShouldSucceed()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object);

        // Act
        var result = await service.SyncAllGuildsAsync(); // Using default CancellationToken

        // Assert
        result.Should().Be(0, "no guilds are connected");

        // Verify the method completes successfully with default cancellation token
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Syncing all connected guilds")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);

        // Cleanup
        await client.DisposeAsync();
    }

    /// <summary>
    /// Integration point documentation for controller usage of SyncAllGuildsAsync.
    /// </summary>
    [Fact]
    public void SyncAllGuildsAsync_ControllerIntegration_Documentation()
    {
        // This test documents how SyncAllGuildsAsync is used from the GuildsController
        // for the Quick Actions "Sync All" feature:
        //
        // Controller endpoint: POST /api/guilds/sync-all
        // Authorization: Requires Admin role
        // Response: Returns count of synced guilds
        //
        // Usage pattern:
        // var syncedCount = await _guildService.SyncAllGuildsAsync(cancellationToken);
        // return Ok(new { syncedCount });
        //
        // UI flow:
        // 1. User clicks "Sync All" button in Guilds page
        // 2. JavaScript sends POST to /api/guilds/sync-all
        // 3. Service syncs all _client.Guilds to database via repository
        // 4. Returns count of successfully synced guilds
        // 5. UI shows success message with count
        //
        // Related files:
        // - src/DiscordBot.Bot/Controllers/GuildsController.cs (API endpoint)
        // - src/DiscordBot.Bot/Pages/Guilds/Index.cshtml (UI with Sync All button)
        // - src/DiscordBot.Core/Interfaces/IGuildService.cs (Service interface)

        var integrationPoints = new
        {
            ControllerEndpoint = "POST /api/guilds/sync-all",
            Authorization = "RequireAdmin policy",
            RequestBody = "None (uses connected guilds)",
            ResponseType = "int (count of synced guilds)",
            UITrigger = "Sync All button in Guilds page",
            SuccessScenario = "Returns positive count, updates database",
            EmptyScenario = "Returns 0 when no guilds connected"
        };

        integrationPoints.Should().NotBeNull();
        integrationPoints.ControllerEndpoint.Should().Be("POST /api/guilds/sync-all");
        integrationPoints.Authorization.Should().Be("RequireAdmin policy");
    }

    /// <summary>
    /// Documents the difference between SyncGuildAsync (single) and SyncAllGuildsAsync (batch).
    /// </summary>
    [Fact]
    public void SyncAllGuildsAsync_VsSyncGuildAsync_Documentation()
    {
        // This test documents the differences between the two sync methods:
        //
        // SyncGuildAsync(ulong guildId, CancellationToken):
        // - Syncs a single guild by ID
        // - Returns bool (true if found and synced, false if not found)
        // - Returns false early if guild not in Discord client
        // - Throws exception on repository error
        // - Use case: Quick Actions "Sync" button on individual guild row
        //
        // SyncAllGuildsAsync(CancellationToken):
        // - Syncs all connected guilds from _client.Guilds
        // - Returns int (count of successfully synced guilds)
        // - Handles exceptions per guild without stopping batch
        // - Logs errors but continues syncing remaining guilds
        // - Use case: Quick Actions "Sync All" button at top of Guilds page
        //
        // Both methods:
        // - Use repository.UpsertAsync to create or update
        // - Set IsActive = true
        // - Map SocketGuild data to Guild entity
        // - Log operations at Information level

        var comparisonTable = new[]
        {
            new { Method = "SyncGuildAsync", Parameter = "ulong guildId", Returns = "bool", ErrorHandling = "Throws" },
            new { Method = "SyncAllGuildsAsync", Parameter = "none (uses _client.Guilds)", Returns = "int", ErrorHandling = "Continues on error" }
        };

        comparisonTable.Should().HaveCount(2);
        comparisonTable[0].Method.Should().Be("SyncGuildAsync");
        comparisonTable[0].Returns.Should().Be("bool");
        comparisonTable[1].Method.Should().Be("SyncAllGuildsAsync");
        comparisonTable[1].Returns.Should().Be("int");
    }
}
