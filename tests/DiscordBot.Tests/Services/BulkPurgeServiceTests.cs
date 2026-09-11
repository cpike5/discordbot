using DiscordBot.Bot.Hubs;
using DiscordBot.Bot.Services;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="BulkPurgeService"/>'s dual-publish to <see cref="IDashboardEventBus"/>
/// alongside its existing SignalR progress broadcast.
/// </summary>
public class BulkPurgeServiceTests : IDisposable
{
    private readonly BotDbContext _dbContext;
    private readonly SqliteConnection _connection;
    private readonly Mock<IAuditLogService> _mockAuditLogService;
    private readonly Mock<IHubContext<DashboardHub>> _mockHubContext;
    private readonly IDashboardEventBus _eventBus;
    private readonly Mock<ILogger<BulkPurgeService>> _mockLogger;
    private readonly BulkPurgeService _service;

    public BulkPurgeServiceTests()
    {
        (_dbContext, _connection) = TestDbContextFactory.CreateContext();
        _mockAuditLogService = new Mock<IAuditLogService>();
        _mockHubContext = new Mock<IHubContext<DashboardHub>>();
        _eventBus = new DashboardEventBus(new Mock<ILogger<DashboardEventBus>>().Object);
        _mockLogger = new Mock<ILogger<BulkPurgeService>>();

        var mockClients = new Mock<IHubClients>();
        var mockClientProxy = new Mock<IClientProxy>();
        mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(mockClientProxy.Object);
        _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

        var mockBuilder = new Mock<IAuditLogBuilder>();
        mockBuilder.Setup(b => b.ForCategory(It.IsAny<AuditLogCategory>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.WithAction(It.IsAny<AuditLogAction>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.ByUser(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.OnTarget(It.IsAny<string>(), It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.InGuild(It.IsAny<ulong>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.WithDetails(It.IsAny<object>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.WithCorrelationId(It.IsAny<string>())).Returns(mockBuilder.Object);
        mockBuilder.Setup(b => b.Enqueue());
        _mockAuditLogService.Setup(a => a.CreateBuilder()).Returns(mockBuilder.Object);

        _service = new BulkPurgeService(
            _dbContext,
            _mockAuditLogService.Object,
            _mockHubContext.Object,
            _eventBus,
            _mockLogger.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private async Task SeedCommandLogAsync()
    {
        var guild = new Guild { Id = 1, Name = "Test Guild", JoinedAt = DateTime.UtcNow, IsActive = true };
        var user = new User { Id = 2, Username = "TestUser", Discriminator = "0001", FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow };
        _dbContext.Guilds.Add(guild);
        _dbContext.Users.Add(user);
        _dbContext.CommandLogs.Add(new CommandLog
        {
            Id = Guid.NewGuid(),
            GuildId = 1,
            UserId = 2,
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow.AddMinutes(-1),
            ResponseTimeMs = 10,
            Success = true
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ExecutePurgeAsync_ShouldDualPublishProgressToEventBus()
    {
        // Arrange
        await SeedCommandLogAsync();
        var criteria = new BulkPurgeCriteriaDto { EntityType = BulkPurgeEntityType.CommandLogs };

        var received = new List<BulkPurgeProgressEvent>();
        using var subscription = _eventBus.Subscribe<BulkPurgeProgressEvent>((evt, _) =>
        {
            received.Add(evt);
            return Task.CompletedTask;
        });

        // Act
        var result = await _service.ExecutePurgeAsync(criteria, "admin-user");

        // Assert
        result.Success.Should().BeTrue();
        received.Should().NotBeEmpty("every BulkPurgeProgress SignalR send should dual-publish to the event bus");
        received.Last().Progress.IsComplete.Should().BeTrue();
    }
}
