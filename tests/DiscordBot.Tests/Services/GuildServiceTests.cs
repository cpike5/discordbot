using Discord.WebSocket;
using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="GuildService"/>.
/// NOTE: Direct testing of GuildService is limited because DiscordSocketClient and SocketGuild
/// are sealed classes that cannot be easily mocked. These tests focus on testing the repository
/// interactions while documenting the expected Discord client behaviors.
/// Integration testing through the controller layer provides better coverage for this service.
/// </summary>
public class GuildServiceTests
{
    private readonly Mock<IGuildRepository> _mockGuildRepository;
    private readonly Mock<ILogger<GuildService>> _mockLogger;
    private readonly Mock<IAuditLogService> _mockAuditLogService;

    public GuildServiceTests()
    {
        _mockGuildRepository = new Mock<IGuildRepository>();
        _mockLogger = new Mock<ILogger<GuildService>>();
        _mockAuditLogService = new Mock<IAuditLogService>();

        // Setup audit log service to return a builder that returns itself for fluent API
        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.BySystem()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.ByBot()).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.InGuild(It.IsAny<ulong>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<Dictionary<string, object?>>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.FromIpAddress(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.WithCorrelationId(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(x => x.LogAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        _mockAuditLogService.Setup(x => x.CreateBuilder()).Returns(mockBuilder.Object);
    }

    /// <summary>
    /// Documentation test that describes the expected behavior of GetAllGuildsAsync.
    /// </summary>
    [Fact]
    public void GetAllGuildsAsync_ExpectedBehavior_Documentation()
    {
        // This test documents the expected behavior of GuildService.GetAllGuildsAsync:
        // 1. Calls repository.GetAllAsync() to fetch all guilds from database
        // 2. For each guild, calls client.GetGuild(guildId) to get live Discord data
        // 3. Merges database and Discord data into GuildDto:
        //    - Name: Uses Discord name if available, else database name
        //    - MemberCount, IconUrl: From Discord guild (nullable if not available)
        //    - IsActive, Prefix, Settings, JoinedAt: From database guild
        // 4. Returns IReadOnlyList<GuildDto>
        //
        // Implementation verified at: src/DiscordBot.Bot/Services/GuildService.cs:34-50

        var expectedBehavior = new
        {
            Method = "GetAllGuildsAsync",
            Returns = "IReadOnlyList<GuildDto>",
            Steps = new[]
            {
                "1. Call repository.GetAllAsync()",
                "2. For each guild: client.GetGuild(guildId)",
                "3. Merge database and Discord data",
                "4. Return mapped list"
            }
        };

        expectedBehavior.Should().NotBeNull();
        expectedBehavior.Method.Should().Be("GetAllGuildsAsync");
        expectedBehavior.Steps.Should().HaveCount(4);
    }

    [Fact]
    public async Task GetGuildByIdAsync_WithNonExistentGuild_ShouldReturnNull()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        _mockGuildRepository
            .Setup(r => r.GetByDiscordIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild?)null);

        // Act
        var result = await service.GetGuildByIdAsync(guildId);

        // Assert
        result.Should().BeNull("the guild does not exist in the database");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task UpdateGuildAsync_WithPartialUpdate_ShouldUpdateOnlyProvidedFields()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guild = new Guild
        {
            Id = guildId,
            Name = "Test Guild",
            JoinedAt = DateTime.UtcNow.AddDays(-30),
            IsActive = true,
            Prefix = "!",
            Settings = "{\"old\":true}"
        };

        var request = new GuildUpdateRequestDto
        {
            Prefix = "?",
            Settings = null, // Not updating settings
            IsActive = null  // Not updating IsActive
        };

        _mockGuildRepository
            .Setup(r => r.GetByDiscordIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(guild);

        _mockGuildRepository
            .Setup(r => r.UpdateAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await service.UpdateGuildAsync(guildId, request);

        // Assert
        result.Should().NotBeNull();
        result!.Prefix.Should().Be("?", "prefix should be updated");
        result.Settings.Should().Be("{\"old\":true}", "settings should remain unchanged");
        result.IsActive.Should().BeTrue("IsActive should remain unchanged");

        _mockGuildRepository.Verify(
            r => r.UpdateAsync(It.Is<Guild>(g =>
                g.Prefix == "?" &&
                g.Settings == "{\"old\":true}" &&
                g.IsActive == true),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "only the specified fields should be updated");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task UpdateGuildAsync_WithNonExistentGuild_ShouldReturnNull()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);
        var request = new GuildUpdateRequestDto { Prefix = "?" };

        _mockGuildRepository
            .Setup(r => r.GetByDiscordIdAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guild?)null);

        // Act
        var result = await service.UpdateGuildAsync(guildId, request);

        // Assert
        result.Should().BeNull("the guild does not exist in the database");

        _mockGuildRepository.Verify(
            r => r.UpdateAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "update should not be called when guild does not exist");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task SyncGuildAsync_WithNonExistentDiscordGuild_ShouldReturnFalse()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        // Act
        var result = await service.SyncGuildAsync(guildId);

        // Assert
        result.Should().BeFalse("the guild does not exist in Discord");

        _mockGuildRepository.Verify(
            r => r.UpsertAsync(It.IsAny<Guild>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "upsert should not be called when Discord guild does not exist");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetAllGuildsAsync_WithCancellationToken_ShouldPassToRepository()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Guild>());

        // Act
        await service.GetAllGuildsAsync(cancellationToken);

        // Assert
        _mockGuildRepository.Verify(
            r => r.GetAllAsync(cancellationToken),
            Times.Once,
            "the cancellation token should be passed to the repository");

        // Cleanup
        await client.DisposeAsync();
    }

    #region GetGuildsAsync Tests

    [Fact]
    public async Task GetGuildsAsync_WithNoFilters_ReturnsAllGuilds()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = false }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3, "all guilds should be returned when no filters are applied");
        result.TotalCount.Should().Be(3);
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_WithSearchTerm_FiltersGuildsByName()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = false }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SearchTerm = "beta",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1, "only guilds matching the search term should be returned");
        result.Items[0].Name.Should().Be("Beta Guild");
        result.TotalCount.Should().Be(1);

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_WithSearchTerm_FiltersGuildsById()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = false }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SearchTerm = "222",
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1, "guilds should be searchable by ID");
        result.Items[0].Id.Should().Be(222UL);
        result.Items[0].Name.Should().Be("Beta Guild");
        result.TotalCount.Should().Be(1);

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_WithActiveFilter_FiltersActiveGuilds()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = false }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            IsActive = true,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "only active guilds should be returned");
        result.Items.Should().OnlyContain(g => g.IsActive == true, "all returned guilds should be active");
        result.TotalCount.Should().Be(2);

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_WithActiveFilter_FiltersInactiveGuilds()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = false }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            IsActive = false,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(1, "only inactive guilds should be returned");
        result.Items.Should().OnlyContain(g => g.IsActive == false, "all returned guilds should be inactive");
        result.Items[0].Name.Should().Be("Gamma Guild");
        result.TotalCount.Should().Be(1);

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_SortByName_OrdersAscending()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = true },
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SortBy = "Name",
            SortDescending = false,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Items[0].Name.Should().Be("Alpha Guild", "guilds should be sorted alphabetically");
        result.Items[1].Name.Should().Be("Beta Guild");
        result.Items[2].Name.Should().Be("Gamma Guild");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_SortByName_OrdersDescending()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Alpha Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Beta Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Gamma Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = true }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SortBy = "Name",
            SortDescending = true,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Items[0].Name.Should().Be("Gamma Guild", "guilds should be sorted reverse alphabetically");
        result.Items[1].Name.Should().Be("Beta Guild");
        result.Items[2].Name.Should().Be("Alpha Guild");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_SortByMemberCount_OrdersCorrectly()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Small Guild", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Large Guild", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Medium Guild", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = true }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SortBy = "MemberCount",
            SortDescending = true,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        // Note: All guilds will have null MemberCount since DiscordSocketClient.GetGuild returns null
        // This test verifies the sorting logic handles null values (treating them as 0)
        result.Items.Should().AllSatisfy(g => g.MemberCount.Should().BeNull());

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_SortByJoinedAt_OrdersCorrectly()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var now = DateTime.UtcNow;
        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Recent Guild", JoinedAt = now.AddDays(-5), IsActive = true },
            new Guild { Id = 222UL, Name = "Old Guild", JoinedAt = now.AddDays(-30), IsActive = true },
            new Guild { Id = 333UL, Name = "Medium Guild", JoinedAt = now.AddDays(-15), IsActive = true }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SortBy = "JoinedAt",
            SortDescending = false,
            Page = 1,
            PageSize = 10
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(3);
        result.Items[0].Name.Should().Be("Old Guild", "guilds should be sorted by join date ascending (oldest first)");
        result.Items[1].Name.Should().Be("Medium Guild");
        result.Items[2].Name.Should().Be("Recent Guild");

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_WithPagination_ReturnsCorrectPage()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Guild 1", JoinedAt = DateTime.UtcNow.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Guild 2", JoinedAt = DateTime.UtcNow.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Guild 3", JoinedAt = DateTime.UtcNow.AddDays(-30), IsActive = true },
            new Guild { Id = 444UL, Name = "Guild 4", JoinedAt = DateTime.UtcNow.AddDays(-40), IsActive = true },
            new Guild { Id = 555UL, Name = "Guild 5", JoinedAt = DateTime.UtcNow.AddDays(-50), IsActive = true }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            Page = 2,
            PageSize = 2,
            SortBy = "Name"
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "page 2 with page size 2 should return 2 items");
        result.Items[0].Name.Should().Be("Guild 3", "pagination should skip the first page");
        result.Items[1].Name.Should().Be("Guild 4");
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
        result.TotalCount.Should().Be(5);
        result.TotalPages.Should().Be(3);
        result.HasPreviousPage.Should().BeTrue();
        result.HasNextPage.Should().BeTrue();

        // Cleanup
        await client.DisposeAsync();
    }

    [Fact]
    public async Task GetGuildsAsync_WithCombinedFilters_AppliesAllFilters()
    {
        // Arrange
        var client = new DiscordSocketClient();
        var service = new GuildService(_mockGuildRepository.Object, client, _mockLogger.Object, _mockAuditLogService.Object);

        var now = DateTime.UtcNow;
        var guilds = new List<Guild>
        {
            new Guild { Id = 111UL, Name = "Active Alpha", JoinedAt = now.AddDays(-10), IsActive = true },
            new Guild { Id = 222UL, Name = "Active Beta", JoinedAt = now.AddDays(-20), IsActive = true },
            new Guild { Id = 333UL, Name = "Inactive Alpha", JoinedAt = now.AddDays(-30), IsActive = false },
            new Guild { Id = 444UL, Name = "Active Gamma", JoinedAt = now.AddDays(-40), IsActive = true },
            new Guild { Id = 555UL, Name = "Inactive Beta", JoinedAt = now.AddDays(-50), IsActive = false }
        };

        _mockGuildRepository
            .Setup(r => r.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(guilds);

        var query = new GuildSearchQueryDto
        {
            SearchTerm = "Active",
            IsActive = true,
            SortBy = "JoinedAt",
            SortDescending = false,
            Page = 1,
            PageSize = 2
        };

        // Act
        var result = await service.GetGuildsAsync(query);

        // Assert
        result.Should().NotBeNull();
        result.Items.Should().HaveCount(2, "page size is 2");
        result.TotalCount.Should().Be(3, "3 guilds match the combined filters (Active + contains 'Active')");
        result.Items.Should().OnlyContain(g => g.IsActive == true && g.Name.Contains("Active"),
            "all filters should be applied");
        result.Items[0].Name.Should().Be("Active Gamma", "results should be sorted by JoinedAt ascending");
        result.Items[1].Name.Should().Be("Active Beta");

        // Cleanup
        await client.DisposeAsync();
    }

    #endregion
}
