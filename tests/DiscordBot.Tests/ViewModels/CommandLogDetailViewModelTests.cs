using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="CommandLogDetailViewModel"/>.
/// </summary>
public class CommandLogDetailViewModelTests
{
    [Fact]
    public void FromDto_MapsAllPropertiesCorrectly()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789UL,
            GuildName = "Test Guild",
            UserId = 987654321UL,
            Username = "TestUser",
            CommandName = "ping",
            Parameters = "{\"arg1\": \"value1\"}",
            ExecutedAt = new DateTime(2023, 6, 15, 10, 30, 0, DateTimeKind.Utc),
            ResponseTimeMs = 150,
            Success = true,
            ErrorMessage = null
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.Id.Should().Be(dto.Id);
        viewModel.GuildId.Should().Be(dto.GuildId);
        viewModel.GuildName.Should().Be("Test Guild");
        viewModel.UserId.Should().Be(dto.UserId);
        viewModel.Username.Should().Be("TestUser");
        viewModel.CommandName.Should().Be("ping");
        viewModel.Parameters.Should().Be("{\"arg1\": \"value1\"}");
        viewModel.ExecutedAt.Should().Be(dto.ExecutedAt);
        viewModel.ResponseTimeMs.Should().Be(150);
        viewModel.Success.Should().BeTrue();
        viewModel.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void FromDto_WithNullGuildName_DefaultsToDirectMessage()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = null,
            GuildName = null,
            UserId = 123UL,
            Username = "User",
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.GuildName.Should().Be("Direct Message");
        viewModel.HasGuildLink.Should().BeFalse();
    }

    [Fact]
    public void FromDto_WithNullUsername_DefaultsToUnknown()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = null,
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.Username.Should().Be("Unknown");
    }

    [Fact]
    public void FromDto_WithValidJsonParameters_FormatsWithIndentation()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = "User",
            CommandName = "test",
            Parameters = "{\"name\":\"test\",\"value\":123}",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.FormattedParameters.Should().Contain("\"name\": \"test\"");
        viewModel.FormattedParameters.Should().Contain("\"value\": 123");
        viewModel.FormattedParameters.Should().Contain("\n"); // Should be pretty-printed
    }

    [Fact]
    public void FromDto_WithInvalidJsonParameters_UsesOriginalString()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = "User",
            CommandName = "test",
            Parameters = "not-valid-json",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.FormattedParameters.Should().Be("not-valid-json");
    }

    [Fact]
    public void FromDto_WithNullParameters_HasParametersIsFalse()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = "User",
            CommandName = "test",
            Parameters = null,
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.HasParameters.Should().BeFalse();
        viewModel.FormattedParameters.Should().BeEmpty();
    }

    [Fact]
    public void FromDto_WithEmptyParameters_HasParametersIsFalse()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = "User",
            CommandName = "test",
            Parameters = "",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.HasParameters.Should().BeFalse();
    }

    [Fact]
    public void FromDto_WithErrorMessage_HasErrorIsTrue()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = "User",
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            Success = false,
            ErrorMessage = "Something went wrong"
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.HasError.Should().BeTrue();
        viewModel.ErrorMessage.Should().Be("Something went wrong");
    }

    [Fact]
    public void FromDto_WithNullErrorMessage_HasErrorIsFalse()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            Username = "User",
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            Success = true,
            ErrorMessage = null
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.HasError.Should().BeFalse();
    }

    [Fact]
    public void StatusText_WhenSuccess_ReturnsSuccess()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.StatusText.Should().Be("Success");
    }

    [Fact]
    public void StatusText_WhenFailed_ReturnsFailed()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            UserId = 123UL,
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            Success = false
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.StatusText.Should().Be("Failed");
    }

    [Fact]
    public void HasGuildLink_WithGuildId_ReturnsTrue()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 123456789UL,
            GuildName = "Test Guild",
            UserId = 123UL,
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.HasGuildLink.Should().BeTrue();
    }

    [Fact]
    public void HasGuildLink_WithoutGuildId_ReturnsFalse()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = null,
            UserId = 123UL,
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            Success = true
        };

        // Act
        var viewModel = CommandLogDetailViewModel.FromDto(dto);

        // Assert
        viewModel.HasGuildLink.Should().BeFalse();
    }
}
