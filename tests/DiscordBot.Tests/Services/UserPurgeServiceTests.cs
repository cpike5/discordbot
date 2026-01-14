using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace DiscordBot.Tests.Services;

/// <summary>
/// Unit tests for <see cref="UserPurgeService"/>.
/// Tests cover user data purge preview and DTO functionality.
/// Note: Tests that require full UserManager integration are tested via integration tests.
/// </summary>
public class UserPurgeServiceTests : IDisposable
{
    private readonly BotDbContext _context;
    private readonly UserPurgeService _service;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<IAuditLogBuilder> _auditLogBuilderMock;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<UserPurgeService>> _loggerMock;

    public UserPurgeServiceTests()
    {
        // Set up in-memory SQLite database
        var options = new DbContextOptionsBuilder<BotDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options;

        _context = new BotDbContext(options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();

        // Set up mocks
        _loggerMock = new Mock<ILogger<UserPurgeService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _auditLogServiceMock = new Mock<IAuditLogService>();
        _auditLogBuilderMock = new Mock<IAuditLogBuilder>();

        // Set up UserManager mock with empty Users queryable
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Set up Users property to return empty list (no linked ApplicationUser accounts)
        _userManagerMock.Setup(m => m.Users)
            .Returns(new List<ApplicationUser>().AsQueryable());

        // Set up GetRolesAsync to return empty list (no roles)
        _userManagerMock.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new List<string>());

        // Set up fluent builder chain for audit log
        _auditLogServiceMock.Setup(x => x.CreateBuilder())
            .Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.ForCategory(It.IsAny<AuditLogCategory>()))
            .Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.WithAction(It.IsAny<AuditLogAction>()))
            .Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.ByUser(It.IsAny<string>()))
            .Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.OnTarget(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.WithDetails(It.IsAny<object>()))
            .Returns(_auditLogBuilderMock.Object);
        _auditLogBuilderMock.Setup(x => x.WithCorrelationId(It.IsAny<string>()))
            .Returns(_auditLogBuilderMock.Object);

        _service = new UserPurgeService(
            _context,
            _userManagerMock.Object,
            _auditLogServiceMock.Object,
            _cache,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    #region UserPurgeResultDto Tests

    [Fact]
    public void UserPurgeResultDto_Succeeded_CreatesSuccessResult()
    {
        // Arrange
        var deletedCounts = new Dictionary<string, int>
        {
            { "MessageLogs", 10 },
            { "CommandLogs", 5 }
        };
        var correlationId = Guid.NewGuid().ToString();

        // Act
        var result = UserPurgeResultDto.Succeeded(deletedCounts, correlationId);

        // Assert
        result.Success.Should().BeTrue();
        result.ErrorMessage.Should().BeNull();
        result.ErrorCode.Should().BeNull();
        result.DeletedCounts.Should().BeEquivalentTo(deletedCounts);
        result.AuditLogCorrelationId.Should().Be(correlationId);
        result.PurgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UserPurgeResultDto_Failed_CreatesFailureResult()
    {
        // Arrange
        var errorCode = UserPurgeResultDto.UserNotFound;
        var errorMessage = "User not found in database";

        // Act
        var result = UserPurgeResultDto.Failed(errorCode, errorMessage);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be(errorCode);
        result.ErrorMessage.Should().Be(errorMessage);
        result.DeletedCounts.Should().BeEmpty();
        result.PurgedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void UserPurgeResultDto_ErrorCodes_AreCorrectlyDefined()
    {
        // Assert
        UserPurgeResultDto.UserNotFound.Should().Be("USER_NOT_FOUND");
        UserPurgeResultDto.UserHasAdminRole.Should().Be("USER_HAS_ADMIN_ROLE");
        UserPurgeResultDto.DatabaseError.Should().Be("DATABASE_ERROR");
        UserPurgeResultDto.TransactionFailed.Should().Be("TRANSACTION_FAILED");
    }

    #endregion

    #region PurgeInitiator Enum Tests

    [Fact]
    public void PurgeInitiator_HasCorrectValues()
    {
        // Assert
        PurgeInitiator.User.Should().Be((PurgeInitiator)1);
        PurgeInitiator.Admin.Should().Be((PurgeInitiator)2);
        PurgeInitiator.System.Should().Be((PurgeInitiator)3);
    }

    [Fact]
    public void PurgeInitiator_ToString_ReturnsCorrectNames()
    {
        // Assert
        PurgeInitiator.User.ToString().Should().Be("User");
        PurgeInitiator.Admin.ToString().Should().Be("Admin");
        PurgeInitiator.System.ToString().Should().Be("System");
    }

    #endregion

    #region AuditLogAction Tests

    [Fact]
    public void AuditLogAction_UserDataPurged_Exists()
    {
        // Assert
        var action = AuditLogAction.UserDataPurged;
        action.Should().Be((AuditLogAction)20);
        action.ToString().Should().Be("UserDataPurged");
    }

    #endregion

    #region Service Integration Tests

    [Fact]
    public async Task PreviewPurgeAsync_ReturnsZeroCounts_WhenUserHasNoDataInDatabase()
    {
        // Arrange
        var discordUserId = 999999999UL;

        // User exists but has no associated data
        var user = new User { Id = discordUserId };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("Users");
        result.DeletedCounts["Users"].Should().Be(1);

        // All other counts should be 0
        var otherCounts = result.DeletedCounts.Where(kvp => kvp.Key != "Users");
        otherCounts.Should().AllSatisfy(kvp => kvp.Value.Should().Be(0));
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsMessageLogs_Correctly()
    {
        // Arrange
        var discordUserId = 123456789UL;
        var guildId = 111111111UL;

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        // Add multiple message logs for the user
        for (int i = 0; i < 5; i++)
        {
            var messageLog = new MessageLog
            {
                AuthorId = discordUserId,
                GuildId = guildId,
                ChannelId = 222222222UL,
                Content = $"Test message {i}",
                Timestamp = DateTime.UtcNow,
                LoggedAt = DateTime.UtcNow
            };
            _context.MessageLogs.Add(messageLog);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("MessageLogs");
        result.DeletedCounts["MessageLogs"].Should().Be(5);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsCommandLogs_Correctly()
    {
        // Arrange
        var discordUserId = 234567890UL;
        var guildId = 111111111UL;

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        // Add multiple command logs for the user
        for (int i = 0; i < 3; i++)
        {
            var commandLog = new CommandLog
            {
                UserId = discordUserId,
                GuildId = guildId,
                CommandName = $"/command{i}",
                ExecutedAt = DateTime.UtcNow
            };
            _context.CommandLogs.Add(commandLog);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("CommandLogs");
        result.DeletedCounts["CommandLogs"].Should().Be(3);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsReminders_Correctly()
    {
        // Arrange
        var discordUserId = 345678901UL;
        var guildId = 111111111UL;

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        // Add reminders for the user
        for (int i = 0; i < 2; i++)
        {
            var reminder = new Reminder
            {
                UserId = discordUserId,
                GuildId = guildId,
                ChannelId = 222222222UL,
                Message = $"Reminder {i}",
                TriggerAt = DateTime.UtcNow.AddDays(1),
                CreatedAt = DateTime.UtcNow,
                Status = ReminderStatus.Pending
            };
            _context.Reminders.Add(reminder);
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("Reminders");
        result.DeletedCounts["Reminders"].Should().Be(2);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsUserConsents_Correctly()
    {
        // Arrange
        var discordUserId = 456789012UL;

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        // Add consent records for the user
        var consent = new UserConsent
        {
            DiscordUserId = discordUserId,
            ConsentType = ConsentType.MessageLogging,
            GrantedAt = DateTime.UtcNow,
            GrantedVia = "Test"
        };
        _context.UserConsents.Add(consent);

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("UserConsents");
        result.DeletedCounts["UserConsents"].Should().Be(1);
    }

    [Fact]
    public async Task PreviewPurgeAsync_DoesNotCountOtherUsersData()
    {
        // Arrange
        var targetUserId = 567890123UL;
        var otherUserId = 987654321UL;
        var guildId = 111111111UL;

        var targetUser = new User { Id = targetUserId };
        var otherUser = new User { Id = otherUserId };
        _context.Users.Add(targetUser);
        _context.Users.Add(otherUser);

        // Add message logs for both users
        _context.MessageLogs.Add(new MessageLog
        {
            AuthorId = targetUserId,
            GuildId = guildId,
            ChannelId = 222222222UL,
            Content = "Target user message",
            Timestamp = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow
        });

        _context.MessageLogs.Add(new MessageLog
        {
            AuthorId = otherUserId,
            GuildId = guildId,
            ChannelId = 222222222UL,
            Content = "Other user message",
            Timestamp = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(targetUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts["MessageLogs"].Should().Be(1, "should only count target user's messages");
    }

    #endregion
}
