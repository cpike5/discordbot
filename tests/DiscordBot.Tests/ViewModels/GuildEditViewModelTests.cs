using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="GuildEditViewModel"/>.
/// </summary>
public class GuildEditViewModelTests
{
    #region FromDto Tests

    [Fact]
    public void FromDto_WithValidDto_PopulatesAllProperties()
    {
        // Arrange
        var sampleDto = new GuildDto
        {
            Id = 123456789012345678,
            Name = "Test Guild",
            IconUrl = "https://cdn.discordapp.com/icons/123/abc.png",
            IsActive = true,
            Prefix = "!",
            Settings = null
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(sampleDto);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Id.Should().Be(123456789012345678, "Id should be copied from DTO");
        viewModel.Name.Should().Be("Test Guild", "Name should be copied from DTO");
        viewModel.IconUrl.Should().Be("https://cdn.discordapp.com/icons/123/abc.png", "IconUrl should be copied from DTO");
        viewModel.IsActive.Should().BeTrue("IsActive should be copied from DTO");
    }

    [Fact]
    public void FromDto_WithNullIconUrl_HandlesNullCorrectly()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 987654321098765432,
            Name = "No Icon Guild",
            IconUrl = null,
            IsActive = true,
            Prefix = null,
            Settings = null
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Id.Should().Be(987654321098765432, "Id should be copied from DTO");
        viewModel.Name.Should().Be("No Icon Guild", "Name should be copied from DTO");
        viewModel.IconUrl.Should().BeNull("IconUrl should be null when DTO has null");
        viewModel.IsActive.Should().BeTrue("IsActive should be copied from DTO");
    }

    [Fact]
    public void FromDto_WithIsActiveFalse_CopiesCorrectly()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 111222333444555666,
            Name = "Inactive Guild",
            IconUrl = "https://cdn.discordapp.com/icons/111/xyz.png",
            IsActive = false,
            Prefix = "?",
            Settings = null
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Id.Should().Be(111222333444555666, "Id should be copied from DTO");
        viewModel.Name.Should().Be("Inactive Guild", "Name should be copied from DTO");
        viewModel.IconUrl.Should().Be("https://cdn.discordapp.com/icons/111/xyz.png", "IconUrl should be copied from DTO");
        viewModel.IsActive.Should().BeFalse("IsActive should be copied from DTO");
    }

    [Fact]
    public void FromDto_CopiesGuildIdentityProperties()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 555666777888999000,
            Name = "Identity Test Guild",
            IconUrl = "https://cdn.discordapp.com/icons/555/def.png",
            IsActive = true,
            Prefix = ">>",
            Settings = "{}"
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Id.Should().Be(555666777888999000, "Id should be copied correctly");
        viewModel.Name.Should().Be("Identity Test Guild", "Name should be copied correctly");
        viewModel.IconUrl.Should().Be("https://cdn.discordapp.com/icons/555/def.png", "IconUrl should be copied correctly");
        viewModel.IsActive.Should().BeTrue("IsActive should be copied correctly");
    }

    [Fact]
    public void FromDto_WithEmptyName_CopiesEmptyString()
    {
        // Arrange
        var dto = new GuildDto
        {
            Id = 777888999000111222,
            Name = "",
            IconUrl = null,
            IsActive = true,
            Prefix = "!",
            Settings = string.Empty
        };

        // Act
        var viewModel = GuildEditViewModel.FromDto(dto);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Name.Should().BeEmpty("empty name should be preserved");
    }

    #endregion
}
