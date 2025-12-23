using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="GuildDetailViewModel"/>.
/// </summary>
public class GuildDetailViewModelTests
{
    [Fact]
    public void FromDto_WithValidGuildDto_MapsAllProperties()
    {
        // Arrange
        var joinedDate = DateTime.UtcNow.AddMonths(-6);
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = 150,
            IconUrl = "https://cdn.discord.com/icons/123456789/icon.png",
            IsActive = true,
            JoinedAt = joinedDate,
            Prefix = "!",
            Settings = "{\"WelcomeChannel\":\"welcome\",\"LogChannel\":\"logs\",\"AutoModEnabled\":true}"
        };

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(123456789UL, "guild ID should be mapped correctly");
        result.Name.Should().Be("Test Guild", "guild name should be mapped correctly");
        result.MemberCount.Should().Be(150, "member count should be mapped correctly");
        result.IconUrl.Should().Be("https://cdn.discord.com/icons/123456789/icon.png", "icon URL should be mapped correctly");
        result.IsActive.Should().BeTrue("IsActive should be mapped correctly");
        result.JoinedAt.Should().Be(joinedDate, "JoinedAt should be mapped correctly");
        result.Prefix.Should().Be("!", "Prefix should be mapped correctly");
        result.Settings.Should().NotBeNull();
        result.CanEdit.Should().BeTrue("CanEdit defaults to true");
    }

    [Fact]
    public void FromDto_WithNullIconUrl_HandlesGracefully()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto);

        // Assert
        result.Should().NotBeNull();
        result.IconUrl.Should().BeNull("null icon URL should be preserved");
        result.Prefix.Should().BeNull("null prefix should be preserved");
    }

    [Fact]
    public void FromDto_WithNullMemberCount_DefaultsToZero()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = null,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto);

        // Assert
        result.Should().NotBeNull();
        result.MemberCount.Should().Be(0, "null member count should default to zero");
    }

    [Fact]
    public void FromDto_WithEmptyRecentCommands_ReturnsEmptyList()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        // Act - explicitly pass empty list
        var result1 = GuildDetailViewModel.FromDto(guildDto, new List<CommandLogDto>());

        // Act - pass null (default parameter)
        var result2 = GuildDetailViewModel.FromDto(guildDto, null);

        // Assert
        result1.Should().NotBeNull();
        result1.RecentCommandLogs.Should().NotBeNull("recent commands should not be null");
        result1.RecentCommandLogs.Should().BeEmpty("empty list should result in empty collection");

        result2.Should().NotBeNull();
        result2.RecentCommandLogs.Should().NotBeNull("recent commands should not be null");
        result2.RecentCommandLogs.Should().BeEmpty("null should result in empty collection");
    }

    [Fact]
    public void FromDto_WithRecentCommands_MapsCommandsCorrectly()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var executedAt1 = DateTime.UtcNow.AddMinutes(-30);
        var executedAt2 = DateTime.UtcNow.AddMinutes(-15);
        var executedAt3 = DateTime.UtcNow.AddMinutes(-5);

        var commandLogs = new List<CommandLogDto>
        {
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = 123456789UL,
                UserId = 111UL,
                Username = "User1",
                CommandName = "ping",
                ExecutedAt = executedAt1,
                ResponseTimeMs = 50,
                Success = true,
                ErrorMessage = null
            },
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = 123456789UL,
                UserId = 222UL,
                Username = "User2",
                CommandName = "status",
                ExecutedAt = executedAt2,
                ResponseTimeMs = 120,
                Success = true,
                ErrorMessage = null
            },
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = 123456789UL,
                UserId = 333UL,
                Username = null,
                CommandName = "help",
                ExecutedAt = executedAt3,
                ResponseTimeMs = 200,
                Success = false,
                ErrorMessage = "Command timeout"
            }
        };

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto, commandLogs);

        // Assert
        result.Should().NotBeNull();
        result.RecentCommandLogs.Should().NotBeNull();
        result.RecentCommandLogs.Should().HaveCount(3, "all three command logs should be mapped");

        var firstCommand = result.RecentCommandLogs[0];
        firstCommand.Username.Should().Be("User1");
        firstCommand.CommandName.Should().Be("ping");
        firstCommand.ExecutedAt.Should().Be(executedAt1);
        firstCommand.ResponseTimeMs.Should().Be(50);
        firstCommand.Success.Should().BeTrue();
        firstCommand.ErrorMessage.Should().BeNull();

        var secondCommand = result.RecentCommandLogs[1];
        secondCommand.Username.Should().Be("User2");
        secondCommand.CommandName.Should().Be("status");
        secondCommand.ExecutedAt.Should().Be(executedAt2);
        secondCommand.ResponseTimeMs.Should().Be(120);
        secondCommand.Success.Should().BeTrue();

        var thirdCommand = result.RecentCommandLogs[2];
        thirdCommand.Username.Should().Be("Unknown", "null username should default to 'Unknown'");
        thirdCommand.CommandName.Should().Be("help");
        thirdCommand.ExecutedAt.Should().Be(executedAt3);
        thirdCommand.ResponseTimeMs.Should().Be(200);
        thirdCommand.Success.Should().BeFalse();
        thirdCommand.ErrorMessage.Should().Be("Command timeout");
    }

    [Fact]
    public void RecentCommandLogItem_FromDto_MapsAllProperties()
    {
        // Arrange
        var commandId = Guid.NewGuid();
        var executedAt = DateTime.UtcNow.AddHours(-1);

        var commandLogDto = new CommandLogDto
        {
            Id = commandId,
            GuildId = 123456789UL,
            UserId = 999UL,
            Username = "TestUser",
            CommandName = "test",
            ExecutedAt = executedAt,
            ResponseTimeMs = 75,
            Success = true,
            ErrorMessage = null
        };

        // Act
        var result = RecentCommandLogItem.FromDto(commandLogDto);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be(commandId, "ID should be mapped correctly");
        result.Username.Should().Be("TestUser", "username should be mapped correctly");
        result.CommandName.Should().Be("test", "command name should be mapped correctly");
        result.ExecutedAt.Should().Be(executedAt, "executed at should be mapped correctly");
        result.ResponseTimeMs.Should().Be(75, "response time should be mapped correctly");
        result.Success.Should().BeTrue("success should be mapped correctly");
        result.ErrorMessage.Should().BeNull("error message should be null for successful commands");
    }

    [Fact]
    public void RecentCommandLogItem_FromDto_WithNullUsername_DefaultsToUnknown()
    {
        // Arrange
        var commandLogDto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789UL,
            UserId = 999UL,
            Username = null,
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 50,
            Success = true,
            ErrorMessage = null
        };

        // Act
        var result = RecentCommandLogItem.FromDto(commandLogDto);

        // Assert
        result.Should().NotBeNull();
        result.Username.Should().Be("Unknown", "null username should default to 'Unknown'");
    }

    [Fact]
    public void RecentCommandLogItem_FromDto_WithFailedCommand_MapsErrorMessage()
    {
        // Arrange
        var commandLogDto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789UL,
            UserId = 999UL,
            Username = "TestUser",
            CommandName = "fail",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 500,
            Success = false,
            ErrorMessage = "Permission denied"
        };

        // Act
        var result = RecentCommandLogItem.FromDto(commandLogDto);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse("failed command should have Success = false");
        result.ErrorMessage.Should().Be("Permission denied", "error message should be mapped for failed commands");
    }

    [Fact]
    public void GuildDetailViewModel_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = "!",
            Settings = null
        };

        // Act
        var viewModel1 = GuildDetailViewModel.FromDto(guildDto);
        var viewModel2 = GuildDetailViewModel.FromDto(guildDto);

        // Assert
        viewModel1.Should().BeEquivalentTo(viewModel2, "view models with same values should be equivalent");
    }

    [Fact]
    public void RecentCommandLogItem_IsRecordType_SupportsValueEquality()
    {
        // Arrange
        var commandId = Guid.NewGuid();
        var executedAt = DateTime.UtcNow;

        var item1 = new RecentCommandLogItem
        {
            Id = commandId,
            Username = "User1",
            CommandName = "ping",
            ExecutedAt = executedAt,
            ResponseTimeMs = 50,
            Success = true,
            ErrorMessage = null
        };

        var item2 = new RecentCommandLogItem
        {
            Id = commandId,
            Username = "User1",
            CommandName = "ping",
            ExecutedAt = executedAt,
            ResponseTimeMs = 50,
            Success = true,
            ErrorMessage = null
        };

        var item3 = new RecentCommandLogItem
        {
            Id = Guid.NewGuid(),
            Username = "User1",
            CommandName = "ping",
            ExecutedAt = executedAt,
            ResponseTimeMs = 50,
            Success = true,
            ErrorMessage = null
        };

        // Assert
        item1.Should().Be(item2, "records with same values should be equal");
        item1.Should().NotBe(item3, "records with different IDs should not be equal");
    }

    [Fact]
    public void FromDto_WithLargeRecentCommandsList_MapsAllCommands()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Test Guild",
            MemberCount = 100,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        var commandLogs = Enumerable.Range(1, 15).Select(i => new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789UL,
            UserId = (ulong)i,
            Username = $"User{i}",
            CommandName = $"command{i}",
            ExecutedAt = DateTime.UtcNow.AddMinutes(-i),
            ResponseTimeMs = i * 10,
            Success = true,
            ErrorMessage = null
        }).ToList();

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto, commandLogs);

        // Assert
        result.Should().NotBeNull();
        result.RecentCommandLogs.Should().HaveCount(15, "all 15 command logs should be mapped (no limit in ViewModel)");
    }

    [Fact]
    public void FromDto_WithInactiveGuild_MapsIsActiveFalse()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Inactive Guild",
            MemberCount = 50,
            IconUrl = null,
            IsActive = false,
            JoinedAt = DateTime.UtcNow.AddYears(-1),
            Prefix = null,
            Settings = null
        };

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto);

        // Assert
        result.Should().NotBeNull();
        result.IsActive.Should().BeFalse("inactive guild should have IsActive = false");
    }

    [Fact]
    public void FromDto_WithZeroMemberCount_MapsToZero()
    {
        // Arrange
        var guildDto = new GuildDto
        {
            Id = 123456789UL,
            Name = "Empty Guild",
            MemberCount = 0,
            IconUrl = null,
            IsActive = true,
            JoinedAt = DateTime.UtcNow,
            Prefix = null,
            Settings = null
        };

        // Act
        var result = GuildDetailViewModel.FromDto(guildDto);

        // Assert
        result.Should().NotBeNull();
        result.MemberCount.Should().Be(0, "zero member count should be preserved");
    }
}
