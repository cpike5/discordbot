using DiscordBot.Bot.ViewModels.Components;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels.Components;

/// <summary>
/// Unit tests for <see cref="AuditLogCardViewModel"/>.
/// Tests verify that audit log DTOs are correctly transformed into display-ready view models.
/// </summary>
public class AuditLogCardViewModelTests
{
    [Fact]
    public void FromLogs_WithEmptyList_ReturnsEmptyViewModel()
    {
        // Arrange
        var emptyLogs = new List<AuditLogDto>();

        // Act
        var result = AuditLogCardViewModel.FromLogs(emptyLogs);

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().NotBeNull("Logs collection should not be null");
        result.Logs.Should().BeEmpty("Logs collection should be empty when no logs provided");
    }

    [Fact]
    public void FromLogs_WithSingleLog_MapsAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow.AddMinutes(-5);
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = timestamp,
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login",
                ActorDisplayName = "TestUser",
                TargetType = "User",
                TargetId = "123",
                GuildName = "Test Guild"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().HaveCount(1);

        var log = result.Logs[0];
        log.Id.Should().Be(1, "ID should be mapped correctly");
        log.Timestamp.Should().Be(timestamp, "Timestamp should be mapped correctly");
        log.Category.Should().Be(AuditLogCategory.User, "Category should be mapped correctly");
        log.CategoryName.Should().Be("User", "CategoryName should be mapped correctly");
        log.Action.Should().Be(AuditLogAction.Login, "Action should be mapped correctly");
        log.ActionName.Should().Be("Login", "ActionName should be mapped correctly");
        log.ActorDisplayName.Should().Be("TestUser", "ActorDisplayName should be mapped correctly");
        log.TargetType.Should().Be("User", "TargetType should be mapped correctly");
        log.TargetId.Should().Be("123", "TargetId should be mapped correctly");
        log.GuildName.Should().Be("Test Guild", "GuildName should be mapped correctly");
        log.RelativeTime.Should().NotBeNullOrEmpty("RelativeTime should be formatted");
        log.CategoryIcon.Should().NotBeNullOrEmpty("CategoryIcon should be mapped");
        log.Description.Should().NotBeNullOrEmpty("Description should be formatted");
    }

    [Fact]
    public void FromLogs_WithMultipleLogs_MapsAllLogs()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddMinutes(-10),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login",
                ActorDisplayName = "User1"
            },
            new AuditLogDto
            {
                Id = 2,
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Category = AuditLogCategory.Guild,
                CategoryName = "Guild",
                Action = AuditLogAction.Updated,
                ActionName = "Updated",
                ActorDisplayName = "User2"
            },
            new AuditLogDto
            {
                Id = 3,
                Timestamp = DateTime.UtcNow.AddMinutes(-1),
                Category = AuditLogCategory.Configuration,
                CategoryName = "Configuration",
                Action = AuditLogAction.SettingChanged,
                ActionName = "SettingChanged",
                ActorDisplayName = "Admin"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().HaveCount(3, "all logs should be mapped");
        result.Logs.Select(l => l.Id).Should().BeEquivalentTo(new[] { 1L, 2L, 3L });
    }

    [Fact]
    public void FromLogs_WithNullActorDisplayName_DefaultsToSystem()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.System,
                CategoryName = "System",
                Action = AuditLogAction.Created,
                ActionName = "Created",
                ActorDisplayName = null
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Should().NotBeNull();
        result.Logs.Should().HaveCount(1);
        result.Logs[0].ActorDisplayName.Should().Be("System", "null actor should default to 'System'");
    }

    [Fact]
    public void CategoryIcon_ForUserCategory_ReturnsUserIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M16 7a4 4 0 11-8 0", "should return user icon SVG path");
    }

    [Fact]
    public void CategoryIcon_ForGuildCategory_ReturnsGuildIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Guild,
                CategoryName = "Guild",
                Action = AuditLogAction.Updated,
                ActionName = "Updated"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M19 21V5a2 2 0 00-2-2H7", "should return guild icon SVG path");
    }

    [Fact]
    public void CategoryIcon_ForConfigurationCategory_ReturnsConfigurationIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Configuration,
                CategoryName = "Configuration",
                Action = AuditLogAction.SettingChanged,
                ActionName = "SettingChanged"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M10.325 4.317c.426-1.756", "should return configuration icon SVG path");
    }

    [Fact]
    public void CategoryIcon_ForSecurityCategory_ReturnsSecurityIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Security,
                CategoryName = "Security",
                Action = AuditLogAction.PermissionChanged,
                ActionName = "PermissionChanged"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M12 15v2m-6 4h12", "should return security icon SVG path");
    }

    [Fact]
    public void CategoryIcon_ForCommandCategory_ReturnsCommandIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Command,
                CategoryName = "Command",
                Action = AuditLogAction.CommandExecuted,
                ActionName = "CommandExecuted"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M8 9l3 3-3 3m5 0h3", "should return command icon SVG path");
    }

    [Fact]
    public void CategoryIcon_ForMessageCategory_ReturnsMessageIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Message,
                CategoryName = "Message",
                Action = AuditLogAction.MessageDeleted,
                ActionName = "MessageDeleted"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M8 10h.01M12 10h.01M16 10h.01", "should return message icon SVG path");
    }

    [Fact]
    public void CategoryIcon_ForSystemCategory_ReturnsSystemIcon()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.System,
                CategoryName = "System",
                Action = AuditLogAction.Created,
                ActionName = "Created"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].CategoryIcon.Should().Contain("M9 3v2m6-2v2M9 19v2m6-2v2", "should return system icon SVG path");
    }

    [Fact]
    public void Description_ForCreatedAction_ReturnsCorrectFormat()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Guild,
                CategoryName = "Guild",
                Action = AuditLogAction.Created,
                ActionName = "Created",
                ActorDisplayName = "Admin",
                TargetType = "Channel",
                TargetId = "123"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].Description.Should().Be("Admin created Channel", "description should follow 'actor action target' format");
    }

    [Fact]
    public void Description_ForUpdatedAction_ReturnsCorrectFormat()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Guild,
                CategoryName = "Guild",
                Action = AuditLogAction.Updated,
                ActionName = "Updated",
                ActorDisplayName = "Moderator",
                TargetType = "Settings",
                TargetId = "456"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].Description.Should().Be("Moderator updated Settings", "description should follow 'actor action target' format");
    }

    [Fact]
    public void Description_ForDeletedAction_ReturnsCorrectFormat()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.Message,
                CategoryName = "Message",
                Action = AuditLogAction.Deleted,
                ActionName = "Deleted",
                ActorDisplayName = "Moderator",
                TargetType = "Message",
                TargetId = "789"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].Description.Should().Be("Moderator deleted Message", "description should follow 'actor action target' format");
    }

    [Fact]
    public void Description_ForLoginAction_ReturnsCorrectFormat()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login",
                ActorDisplayName = "TestUser"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].Description.Should().Be("TestUser logged in", "login action should have specific description");
    }

    [Fact]
    public void Description_ForLogoutAction_ReturnsCorrectFormat()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow,
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Logout,
                ActionName = "Logout",
                ActorDisplayName = "TestUser"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].Description.Should().Be("TestUser logged out", "logout action should have specific description");
    }

    [Fact]
    public void RelativeTime_ForRecentTimestamp_ReturnsJustNow()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddSeconds(-30),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].RelativeTime.Should().Be("Just now", "timestamps under 1 minute should show 'Just now'");
    }

    [Fact]
    public void RelativeTime_ForMinutesAgo_ReturnsMinutes()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddMinutes(-5),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].RelativeTime.Should().Be("5 min ago", "timestamps in minutes should show 'X min ago'");
    }

    [Fact]
    public void RelativeTime_ForOneHourAgo_ReturnsSingularHour()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddMinutes(-60),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].RelativeTime.Should().Be("1 hour ago", "exactly 1 hour should show '1 hour ago'");
    }

    [Fact]
    public void RelativeTime_ForHoursAgo_ReturnsHours()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddHours(-5),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].RelativeTime.Should().Be("5 hours ago", "timestamps in hours should show 'X hours ago'");
    }

    [Fact]
    public void RelativeTime_ForOneDayAgo_ReturnsSingularDay()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddDays(-1),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].RelativeTime.Should().Be("1 day ago", "exactly 1 day should show '1 day ago'");
    }

    [Fact]
    public void RelativeTime_ForDaysAgo_ReturnsDays()
    {
        // Arrange
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = DateTime.UtcNow.AddDays(-3),
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert
        result.Logs[0].RelativeTime.Should().Be("3 days ago", "timestamps in days should show 'X days ago'");
    }

    [Fact]
    public void RelativeTime_ForOldTimestamp_ReturnsFormattedDate()
    {
        // Arrange - Use a date that's definitely more than 7 days ago
        var oldDate = DateTime.UtcNow.AddDays(-30);
        var logs = new List<AuditLogDto>
        {
            new AuditLogDto
            {
                Id = 1,
                Timestamp = oldDate,
                Category = AuditLogCategory.User,
                CategoryName = "User",
                Action = AuditLogAction.Login,
                ActionName = "Login"
            }
        };

        // Act
        var result = AuditLogCardViewModel.FromLogs(logs);

        // Assert - Check format matches "MMM d" pattern
        var expectedFormat = oldDate.ToString("MMM d");
        result.Logs[0].RelativeTime.Should().Be(expectedFormat, "timestamps over 7 days should show formatted date");
    }

    [Fact]
    public void AuditLogItem_IsRecord_SupportsValueEquality()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var item1 = new AuditLogItem(
            Id: 1,
            Timestamp: timestamp,
            RelativeTime: "Just now",
            Category: AuditLogCategory.User,
            CategoryName: "User",
            CategoryIcon: "icon-path",
            Action: AuditLogAction.Login,
            ActionName: "Login",
            ActorDisplayName: "TestUser",
            TargetType: "User",
            TargetId: "123",
            GuildName: "Test Guild",
            Description: "TestUser logged in"
        );

        var item2 = new AuditLogItem(
            Id: 1,
            Timestamp: timestamp,
            RelativeTime: "Just now",
            Category: AuditLogCategory.User,
            CategoryName: "User",
            CategoryIcon: "icon-path",
            Action: AuditLogAction.Login,
            ActionName: "Login",
            ActorDisplayName: "TestUser",
            TargetType: "User",
            TargetId: "123",
            GuildName: "Test Guild",
            Description: "TestUser logged in"
        );

        // Act & Assert
        item1.Should().Be(item2, "records with same values should be equal");
    }

    [Fact]
    public void FromLogs_WithAllAuditLogActions_GeneratesCorrectDescriptions()
    {
        // Arrange
        var actions = new[]
        {
            (AuditLogAction.Created, "Admin created User"),
            (AuditLogAction.Updated, "Admin updated User"),
            (AuditLogAction.Deleted, "Admin deleted User"),
            (AuditLogAction.Login, "Admin logged in"),
            (AuditLogAction.Logout, "Admin logged out"),
            (AuditLogAction.PermissionChanged, "Admin changed permissions"),
            (AuditLogAction.SettingChanged, "Admin changed settings"),
            (AuditLogAction.CommandExecuted, "Admin executed command"),
            (AuditLogAction.MessageDeleted, "Admin deleted message"),
            (AuditLogAction.MessageEdited, "Admin edited message"),
            (AuditLogAction.UserBanned, "Admin banned user"),
            (AuditLogAction.UserUnbanned, "Admin unbanned user"),
            (AuditLogAction.UserKicked, "Admin kicked user"),
            (AuditLogAction.RoleAssigned, "Admin assigned role"),
            (AuditLogAction.RoleRemoved, "Admin removed role")
        };

        foreach (var (action, expectedDescription) in actions)
        {
            var logs = new List<AuditLogDto>
            {
                new AuditLogDto
                {
                    Id = 1,
                    Timestamp = DateTime.UtcNow,
                    Category = AuditLogCategory.User,
                    CategoryName = "User",
                    Action = action,
                    ActionName = action.ToString(),
                    ActorDisplayName = "Admin",
                    TargetType = "User",
                    TargetId = "123"
                }
            };

            // Act
            var result = AuditLogCardViewModel.FromLogs(logs);

            // Assert
            result.Logs[0].Description.Should().Be(expectedDescription, $"action {action} should have correct description");
        }
    }
}
