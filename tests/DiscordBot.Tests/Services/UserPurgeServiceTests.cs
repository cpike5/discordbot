using DiscordBot.Bot.Services;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using DiscordBot.Infrastructure.Data;
using DiscordBot.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly TestDatabase _database;
    private readonly UserPurgeService _service;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<IAuditLogService> _auditLogServiceMock;
    private readonly Mock<IAuditLogBuilder> _auditLogBuilderMock;
    private readonly IMemoryCache _cache;
    private readonly Mock<ILogger<UserPurgeService>> _loggerMock;
    private readonly Mock<IUserDataExportService> _exportServiceMock = new();

    public UserPurgeServiceTests()
    {
        (_context, _database) = TestDbContextFactory.CreateContext();

        // Set up mocks
        _loggerMock = new Mock<ILogger<UserPurgeService>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _auditLogServiceMock = new Mock<IAuditLogService>();
        _auditLogBuilderMock = new Mock<IAuditLogBuilder>();

        // Set up UserManager mock with empty Users queryable
        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // Set up Users property against the real (empty) DbContext set, not an in-memory
        // List<T>.AsQueryable(): PurgeUserDataAsync calls FirstOrDefaultAsync on it, which needs a
        // provider implementing IAsyncQueryProvider. The EF Core queryable satisfies that while
        // still returning no rows, i.e. no linked ApplicationUser account.
        _userManagerMock.Setup(m => m.Users)
            .Returns(_context.Set<ApplicationUser>());

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
            _exportServiceMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _context.Dispose();
        _database.Dispose();
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

        var guild = new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _context.Guilds.Add(guild);

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        // Add multiple message logs for the user
        for (int i = 0; i < 5; i++)
        {
            var messageLog = new MessageLog
            {
                DiscordMessageId = (ulong)(100000000 + i),
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

        var guild = new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _context.Guilds.Add(guild);

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

        var guild = new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _context.Guilds.Add(guild);

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

        var guild = new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _context.Guilds.Add(guild);

        var targetUser = new User { Id = targetUserId };
        var otherUser = new User { Id = otherUserId };
        _context.Users.Add(targetUser);
        _context.Users.Add(otherUser);

        // Add message logs for both users
        _context.MessageLogs.Add(new MessageLog
        {
            DiscordMessageId = 200000001UL,
            AuthorId = targetUserId,
            GuildId = guildId,
            ChannelId = 222222222UL,
            Content = "Target user message",
            Timestamp = DateTime.UtcNow,
            LoggedAt = DateTime.UtcNow
        });

        _context.MessageLogs.Add(new MessageLog
        {
            DiscordMessageId = 200000002UL,
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

    [Fact]
    public async Task PreviewPurgeAsync_CountsLlmUsageRecords_Correctly()
    {
        // Arrange
        var discordUserId = 678901234UL;
        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        for (int i = 0; i < 4; i++)
        {
            _context.LlmUsageRecords.Add(new LlmUsageRecord
            {
                Timestamp = DateTime.UtcNow,
                Mode = LlmMode.GuildAssistant,
                UserId = discordUserId,
                Model = "anthropic/claude-sonnet-4",
                CostUsd = 0.01m,
                CostSource = LlmCostSource.Billed
            });
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("LlmUsageRecords");
        result.DeletedCounts["LlmUsageRecords"].Should().Be(4);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsAssistantInteractionLogs_Correctly()
    {
        // Arrange
        var discordUserId = 789012345UL;
        var guildId = 111111111UL;
        var guild = new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _context.Guilds.Add(guild);

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        for (int i = 0; i < 3; i++)
        {
            _context.AssistantInteractionLogs.Add(new AssistantInteractionLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = discordUserId,
                GuildId = guildId,
                ChannelId = 222222222UL,
                MessageId = (ulong)(300000000 + i),
                Question = $"Question {i}",
                Response = $"Response {i}"
            });
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("AssistantInteractionLogs");
        result.DeletedCounts["AssistantInteractionLogs"].Should().Be(3);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsDmAssistantInteractionLogs_Correctly()
    {
        // Arrange
        var discordUserId = 890123456UL;
        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        for (int i = 0; i < 2; i++)
        {
            _context.DmAssistantInteractionLogs.Add(new DmAssistantInteractionLog
            {
                Timestamp = DateTime.UtcNow,
                UserId = discordUserId,
                Message = $"Message {i}",
                Response = $"Response {i}"
            });
        }

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("DmAssistantInteractionLogs");
        result.DeletedCounts["DmAssistantInteractionLogs"].Should().Be(2);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsDmAssistantUsageMetrics_Correctly()
    {
        // Arrange
        var discordUserId = 901234567UL;
        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        _context.DmAssistantUsageMetrics.Add(new DmAssistantUsageMetrics
        {
            UserId = discordUserId,
            Date = DateTime.UtcNow.Date
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PreviewPurgeAsync(discordUserId);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts.Should().ContainKey("DmAssistantUsageMetrics");
        result.DeletedCounts["DmAssistantUsageMetrics"].Should().Be(1);
    }

    [Fact]
    public async Task PurgeUserDataAsync_DeletesAssistantData_Correctly()
    {
        // Arrange
        var discordUserId = 912345678UL;
        var guildId = 111111111UL;
        var guild = new Guild { Id = guildId, Name = "Test Guild", JoinedAt = DateTime.UtcNow };
        _context.Guilds.Add(guild);

        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        _context.LlmUsageRecords.Add(new LlmUsageRecord
        {
            Timestamp = DateTime.UtcNow,
            Mode = LlmMode.GuildAssistant,
            UserId = discordUserId,
            Model = "anthropic/claude-sonnet-4",
            CostUsd = 0.01m,
            CostSource = LlmCostSource.Billed
        });

        _context.AssistantInteractionLogs.Add(new AssistantInteractionLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = discordUserId,
            GuildId = guildId,
            ChannelId = 222222222UL,
            MessageId = 300000000UL,
            Question = "Question",
            Response = "Response"
        });

        _context.DmAssistantInteractionLogs.Add(new DmAssistantInteractionLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = discordUserId,
            Message = "Message",
            Response = "Response"
        });

        _context.DmAssistantUsageMetrics.Add(new DmAssistantUsageMetrics
        {
            UserId = discordUserId,
            Date = DateTime.UtcNow.Date
        });

        await _context.SaveChangesAsync();

        // Act
        var result = await _service.PurgeUserDataAsync(discordUserId, PurgeInitiator.User);

        // Assert
        result.Success.Should().BeTrue();
        result.DeletedCounts["LlmUsageRecords"].Should().Be(1);
        result.DeletedCounts["AssistantInteractionLogs"].Should().Be(1);
        result.DeletedCounts["DmAssistantInteractionLogs"].Should().Be(1);
        result.DeletedCounts["DmAssistantUsageMetrics"].Should().Be(1);

        (await _context.LlmUsageRecords.CountAsync(r => r.UserId == discordUserId)).Should().Be(0);
        (await _context.AssistantInteractionLogs.CountAsync(l => l.UserId == discordUserId)).Should().Be(0);
        (await _context.DmAssistantInteractionLogs.CountAsync(l => l.UserId == discordUserId)).Should().Be(0);
        (await _context.DmAssistantUsageMetrics.CountAsync(m => m.UserId == discordUserId)).Should().Be(0);
    }

    [Fact]
    public async Task PurgeUserDataAsync_DeletesTheUsersExportDirectory_ButNotOtherUsers()
    {
        // Arrange: a real export service over a temp content root, so the files really exist
        var contentRoot = Path.Combine(Path.GetTempPath(), $"purge_export_{Guid.NewGuid():N}");
        Directory.CreateDirectory(contentRoot);
        try
        {
            var env = new Mock<IWebHostEnvironment>();
            env.Setup(e => e.ContentRootPath).Returns(contentRoot);
            env.Setup(e => e.WebRootPath).Returns(Path.Combine(contentRoot, "wwwroot"));
            var exportService = new UserDataExportService(
                _context, _auditLogServiceMock.Object, env.Object,
                Options.Create(new DiscordBot.Core.Configuration.ApplicationOptions { BaseUrl = "https://localhost" }),
                Mock.Of<ILogger<UserDataExportService>>());
            var purge = new UserPurgeService(
                _context, _userManagerMock.Object, _auditLogServiceMock.Object, _cache, exportService, _loggerMock.Object);

            var userId = 931000001UL;
            var otherId = 931000002UL;
            _context.Users.AddRange(new User { Id = userId }, new User { Id = otherId });
            await _context.SaveChangesAsync();
            var mine = await exportService.ExportUserDataAsync(userId);
            var theirs = await exportService.ExportUserDataAsync(otherId);
            var userDir = Path.Combine(contentRoot, "data", "exports", userId.ToString());
            Directory.Exists(userDir).Should().BeTrue();

            // Act
            var result = await purge.PurgeUserDataAsync(userId, PurgeInitiator.User);

            // Assert
            result.Success.Should().BeTrue();
            Directory.Exists(userDir).Should().BeFalse("the archive holds the user's data and must not outlive the purge");
            exportService.GetExportFilePath(otherId, theirs.ExportId!.Value).Should().NotBeNull();
            mine.Success.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(contentRoot, recursive: true);
        }
    }

    #endregion

    #region Personal tables added for H10 (see docs/articles/user-data-inventory.md)

    private static readonly string[] NewlyDeletedKeys =
    {
        "UserPreferences", "UserSoundFavorites", "UserTtsPresets", "TtsMessageHistory", "VoxMessageHistory",
        "AudioPlaybackLogs", "DmConversationMessages", "DmAssistantNotes", "UserActivityEvents",
        "MemberActivitySnapshots", "FeatureRequestRejections"
    };

    /// <summary>Makes the mocked UserManager really delete the row, so the database FKs are exercised.</summary>
    private void DeleteApplicationUsersForReal()
    {
        _userManagerMock.Setup(m => m.DeleteAsync(It.IsAny<ApplicationUser>()))
            .Returns(async (ApplicationUser user) =>
            {
                _context.Set<ApplicationUser>().Remove(user);
                await _context.SaveChangesAsync();
                return IdentityResult.Success;
            });
    }

    [Fact]
    public async Task PurgeUserDataAsync_PurgesThePersonalTables_AndLeavesOtherUsersRows()
    {
        const ulong guildId = 940000000UL, userId = 940000001UL, otherId = 940000002UL;
        var (_, sound, currency) = await PersonalDataSeeder.SeedSharedAsync(_context, guildId);
        await PersonalDataSeeder.SeedUserAsync(_context, userId, guildId, sound.Id, currency.Id);
        await PersonalDataSeeder.SeedUserAsync(_context, otherId, guildId, sound.Id, currency.Id);

        var result = await _service.PurgeUserDataAsync(userId, PurgeInitiator.User);

        result.Success.Should().BeTrue(result.ErrorMessage);
        foreach (var key in NewlyDeletedKeys)
        {
            result.DeletedCounts.Should().ContainKey(key).WhoseValue.Should().Be(1, $"{key} holds one row for the user");
        }
        result.DeletedCounts.Should().ContainKey("FeatureRequests_Anonymized").WhoseValue.Should().Be(1);

        _context.ChangeTracker.Clear();
        (await _context.UserPreferences.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.UserSoundFavorites.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.UserTtsPresets.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.TtsMessageHistory.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.VoxMessageHistory.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.AudioPlaybackLogs.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.DmConversationMessages.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.DmAssistantNotes.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.UserActivityEvents.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.MemberActivitySnapshots.CountAsync(x => x.UserId == userId)).Should().Be(0);
        (await _context.FeatureRequestRejections.CountAsync(x => x.UserId == userId)).Should().Be(0);

        // The feature request stays for the guild, without its author
        (await _context.FeatureRequests.CountAsync(x => x.SubmittedByUserId == userId)).Should().Be(0);
        (await _context.FeatureRequests.CountAsync(x => x.SubmittedByUserId == 0UL && x.Title == $"request {userId}")).Should().Be(1);

        // Retained, awaiting the owner's decision: the wallet and its append-only ledger
        (await _context.Wallets.CountAsync(x => x.UserId == userId)).Should().Be(1);
        (await _context.LedgerTransactions.CountAsync(x => x.Wallet!.UserId == userId)).Should().Be(1);

        // Every other user's rows survive
        (await _context.UserPreferences.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.UserSoundFavorites.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.UserTtsPresets.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.TtsMessageHistory.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.VoxMessageHistory.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.AudioPlaybackLogs.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.DmConversationMessages.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.DmAssistantNotes.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.UserActivityEvents.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.MemberActivitySnapshots.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.FeatureRequestRejections.CountAsync(x => x.UserId == otherId)).Should().Be(1);
        (await _context.FeatureRequests.CountAsync(x => x.SubmittedByUserId == otherId)).Should().Be(1);
    }

    [Fact]
    public async Task PurgeUserDataAsync_WithLinkedAccount_PurgesNotificationsAndActivityLog()
    {
        // The account acted in the portal (actor rows have a Restrict FK) and an admin acted on it (target rows)
        const ulong guildId = 941000000UL, userId = 941000001UL, otherId = 941000002UL;
        var appUserId = Guid.NewGuid().ToString();
        var otherAppUserId = Guid.NewGuid().ToString();
        var (_, sound, currency) = await PersonalDataSeeder.SeedSharedAsync(_context, guildId);
        await PersonalDataSeeder.SeedUserAsync(_context, userId, guildId, sound.Id, currency.Id, appUserId);
        await PersonalDataSeeder.SeedUserAsync(_context, otherId, guildId, sound.Id, currency.Id, otherAppUserId);
        _context.UserActivityLogs.Add(new UserActivityLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = otherAppUserId,
            TargetUserId = appUserId,
            Action = UserActivityAction.UserCreated,
            Details = "{\"Email\":\"target@example.com\"}",
            Timestamp = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        // As in a real request scope: nothing seeded is tracked, so the delete is the database's alone
        _context.ChangeTracker.Clear();
        DeleteApplicationUsersForReal();

        var result = await _service.PurgeUserDataAsync(userId, PurgeInitiator.Admin, "admin");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.DeletedCounts["ApplicationUser"].Should().Be(1);
        result.DeletedCounts["UserNotifications"].Should().Be(1);
        result.DeletedCounts["UserActivityLogs"].Should().Be(1);
        result.DeletedCounts["UserActivityLogs_Anonymized"].Should().Be(1);

        _context.ChangeTracker.Clear();
        (await _context.Set<ApplicationUser>().AnyAsync(u => u.Id == appUserId)).Should().BeFalse();
        (await _context.UserNotifications.CountAsync(n => n.UserId == appUserId)).Should().Be(0);
        (await _context.UserActivityLogs.CountAsync(l => l.ActorUserId == appUserId || l.TargetUserId == appUserId)).Should().Be(0);

        // The other admin's record of acting stays, without the purged user's id or details
        var adminTrail = await _context.UserActivityLogs.SingleAsync(l => l.Action == UserActivityAction.UserCreated);
        adminTrail.ActorUserId.Should().Be(otherAppUserId);
        adminTrail.TargetUserId.Should().BeNull();
        adminTrail.Details.Should().BeNull();

        (await _context.UserNotifications.CountAsync(n => n.UserId == otherAppUserId)).Should().Be(1);
        (await _context.UserActivityLogs.CountAsync(l => l.ActorUserId == otherAppUserId && l.Action == UserActivityAction.UserUpdated)).Should().Be(1);
    }

    [Fact]
    public async Task PurgeUserDataAsync_RetainsModerationRecordsAboutTheUser()
    {
        const ulong guildId = 942000000UL, userId = 942000001UL, moderatorId = 942000002UL;
        _context.Guilds.Add(new Guild { Id = guildId, Name = "Mod Guild", JoinedAt = DateTime.UtcNow });
        _context.Users.Add(new User { Id = userId });
        var flagged = new FlaggedEvent { Id = Guid.NewGuid(), GuildId = guildId, UserId = userId, Description = "spam", Evidence = "{}", CreatedAt = DateTime.UtcNow };
        _context.FlaggedEvents.Add(flagged);
        _context.ModerationCases.Add(new ModerationCase { Id = Guid.NewGuid(), CaseNumber = 1, GuildId = guildId, TargetUserId = userId, ModeratorUserId = moderatorId, CreatedAt = DateTime.UtcNow, RelatedFlaggedEventId = flagged.Id });
        _context.ModNotes.Add(new ModNote { Id = Guid.NewGuid(), GuildId = guildId, TargetUserId = userId, AuthorUserId = moderatorId, Content = "watch", CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync();

        var result = await _service.PurgeUserDataAsync(userId, PurgeInitiator.User);

        result.Success.Should().BeTrue(result.ErrorMessage);
        _context.ChangeTracker.Clear();
        (await _context.ModerationCases.CountAsync(c => c.TargetUserId == userId)).Should().Be(1);
        (await _context.FlaggedEvents.CountAsync(f => f.UserId == userId)).Should().Be(1);
        (await _context.ModNotes.CountAsync(n => n.TargetUserId == userId)).Should().Be(1);
    }

    [Fact]
    public async Task PreviewPurgeAsync_CountsThePersonalTables()
    {
        const ulong guildId = 943000000UL, userId = 943000001UL;
        var (_, sound, currency) = await PersonalDataSeeder.SeedSharedAsync(_context, guildId);
        await PersonalDataSeeder.SeedUserAsync(_context, userId, guildId, sound.Id, currency.Id, Guid.NewGuid().ToString());

        var result = await _service.PreviewPurgeAsync(userId);

        result.Success.Should().BeTrue(result.ErrorMessage);
        foreach (var key in NewlyDeletedKeys.Append("FeatureRequests_Anonymized").Append("UserNotifications").Append("UserActivityLogs"))
        {
            result.DeletedCounts.Should().ContainKey(key).WhoseValue.Should().Be(1, key);
        }
        result.DeletedCounts.Should().ContainKey("UserActivityLogs_Anonymized").WhoseValue.Should().Be(0);
    }

    #endregion

    #region CanPurgeUserAsync Tests

    [Fact]
    public async Task CanPurgeUserAsync_ReturnsTrue_WhenNoLinkedApplicationUser()
    {
        // Arrange
        var discordUserId = 678901234UL;

        // Add a Discord User without a linked ApplicationUser
        var user = new User { Id = discordUserId };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        // Act
        var (canPurge, blockingReason) = await _service.CanPurgeUserAsync(discordUserId);

        // Assert
        canPurge.Should().BeTrue();
        blockingReason.Should().BeNull();
    }

    [Fact]
    public async Task CanPurgeUserAsync_ReturnsTrue_WhenLinkedApplicationUserHasNoAdminRole()
    {
        // Arrange
        var discordUserId = 789012345UL;

        // Add a Discord User
        var user = new User { Id = discordUserId };
        _context.Users.Add(user);

        // Add a linked ApplicationUser
        var applicationUser = new ApplicationUser
        {
            Id = Guid.NewGuid().ToString(),
            UserName = "testuser",
            Email = "test@example.com",
            DiscordUserId = discordUserId
        };
        _context.Set<ApplicationUser>().Add(applicationUser);
        await _context.SaveChangesAsync();

        // UserManager mock returns empty roles (no admin)
        _userManagerMock.Setup(m => m.GetRolesAsync(It.Is<ApplicationUser>(u => u.DiscordUserId == discordUserId)))
            .ReturnsAsync(new List<string> { "Viewer" });

        // Act
        var (canPurge, blockingReason) = await _service.CanPurgeUserAsync(discordUserId);

        // Assert
        canPurge.Should().BeTrue();
        blockingReason.Should().BeNull();
    }

    #endregion
}
