using DiscordBot.Bot.ViewModels.Pages;
using DiscordBot.Core.DTOs;
using FluentAssertions;

namespace DiscordBot.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="CommandLogListViewModel"/> and <see cref="CommandLogFilterOptions"/>.
/// </summary>
public class CommandLogListViewModelTests
{
    [Fact]
    public void FromPaginatedDto_WithValidData_CreatesViewModelCorrectly()
    {
        // Arrange
        var commandLogs = new List<CommandLogDto>
        {
            new CommandLogDto
            {
                Id = Guid.NewGuid(),
                GuildId = 111UL,
                GuildName = "Test Guild",
                UserId = 222UL,
                Username = "TestUser",
                CommandName = "ping",
                ExecutedAt = DateTime.UtcNow,
                ResponseTimeMs = 100,
                Success = true,
                ErrorMessage = null
            }
        };

        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = commandLogs.AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 1
        };

        // Act
        var viewModel = CommandLogListViewModel.FromPaginatedDto(paginatedResponse);

        // Assert
        viewModel.Should().NotBeNull();
        viewModel.Logs.Should().HaveCount(1);
        viewModel.CurrentPage.Should().Be(1);
        viewModel.PageSize.Should().Be(25);
        viewModel.TotalCount.Should().Be(1);
        viewModel.TotalPages.Should().Be(1);
    }

    [Fact]
    public void FromPaginatedDto_WithFilters_IncludesFiltersInViewModel()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        var filters = new CommandLogFilterOptions
        {
            SearchTerm = "test",
            GuildId = 123UL,
            CommandName = "ping"
        };

        // Act
        var viewModel = CommandLogListViewModel.FromPaginatedDto(paginatedResponse, filters);

        // Assert
        viewModel.Filters.Should().NotBeNull();
        viewModel.Filters.SearchTerm.Should().Be("test");
        viewModel.Filters.GuildId.Should().Be(123UL);
        viewModel.Filters.CommandName.Should().Be("ping");
    }

    [Fact]
    public void FromPaginatedDto_WithoutFilters_UsesEmptyFilters()
    {
        // Arrange
        var paginatedResponse = new PaginatedResponseDto<CommandLogDto>
        {
            Items = new List<CommandLogDto>().AsReadOnly(),
            Page = 1,
            PageSize = 25,
            TotalCount = 0
        };

        // Act
        var viewModel = CommandLogListViewModel.FromPaginatedDto(paginatedResponse);

        // Assert
        viewModel.Filters.Should().NotBeNull();
        viewModel.Filters.HasActiveFilters.Should().BeFalse();
    }

    [Fact]
    public void CommandLogListItem_FromDto_MapsPropertiesCorrectly()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 111UL,
            GuildName = "Test Guild",
            UserId = 222UL,
            Username = "TestUser",
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 150,
            Success = true,
            ErrorMessage = null
        };

        // Act
        var item = CommandLogListItem.FromDto(dto);

        // Assert
        item.Should().NotBeNull();
        item.Id.Should().Be(dto.Id);
        item.GuildName.Should().Be("Test Guild");
        item.Username.Should().Be("TestUser");
        item.CommandName.Should().Be("ping");
        item.ExecutedAt.Should().Be(dto.ExecutedAt);
        item.ResponseTimeMs.Should().Be(150);
        item.Success.Should().BeTrue();
        item.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void CommandLogListItem_FromDto_WithNullGuildName_ShowsDirectMessage()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = null,
            GuildName = null,
            UserId = 222UL,
            Username = "TestUser",
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = true
        };

        // Act
        var item = CommandLogListItem.FromDto(dto);

        // Assert
        item.GuildName.Should().Be("Direct Message");
    }

    [Fact]
    public void CommandLogListItem_FromDto_WithNullUsername_ShowsUnknown()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 111UL,
            GuildName = "Test Guild",
            UserId = 222UL,
            Username = null,
            CommandName = "ping",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = true
        };

        // Act
        var item = CommandLogListItem.FromDto(dto);

        // Assert
        item.Username.Should().Be("Unknown");
    }

    [Fact]
    public void CommandLogListItem_FromDto_WithFailedCommand_IncludesErrorMessage()
    {
        // Arrange
        var dto = new CommandLogDto
        {
            Id = Guid.NewGuid(),
            GuildId = 111UL,
            GuildName = "Test Guild",
            UserId = 222UL,
            Username = "TestUser",
            CommandName = "test",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 200,
            Success = false,
            ErrorMessage = "Command execution failed"
        };

        // Act
        var item = CommandLogListItem.FromDto(dto);

        // Assert
        item.Success.Should().BeFalse();
        item.ErrorMessage.Should().Be("Command execution failed");
    }

    [Fact]
    public void HasNextPage_WhenCurrentPageIsLessThanTotalPages_ReturnsTrue()
    {
        // Arrange
        var viewModel = new CommandLogListViewModel
        {
            CurrentPage = 1,
            TotalPages = 5
        };

        // Act & Assert
        viewModel.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public void HasNextPage_WhenCurrentPageEqualsTotalPages_ReturnsFalse()
    {
        // Arrange
        var viewModel = new CommandLogListViewModel
        {
            CurrentPage = 5,
            TotalPages = 5
        };

        // Act & Assert
        viewModel.HasNextPage.Should().BeFalse();
    }

    [Fact]
    public void HasPreviousPage_WhenCurrentPageIsGreaterThanOne_ReturnsTrue()
    {
        // Arrange
        var viewModel = new CommandLogListViewModel
        {
            CurrentPage = 2,
            TotalPages = 5
        };

        // Act & Assert
        viewModel.HasPreviousPage.Should().BeTrue();
    }

    [Fact]
    public void HasPreviousPage_WhenCurrentPageIsOne_ReturnsFalse()
    {
        // Arrange
        var viewModel = new CommandLogListViewModel
        {
            CurrentPage = 1,
            TotalPages = 5
        };

        // Act & Assert
        viewModel.HasPreviousPage.Should().BeFalse();
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithSearchTerm_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            SearchTerm = "test"
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("search term is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithGuildId_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            GuildId = 123UL
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("guild ID is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithUserId_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            UserId = 456UL
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("user ID is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithCommandName_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            CommandName = "ping"
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("command name is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithStartDate_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            StartDate = DateTime.UtcNow
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("start date is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithEndDate_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            EndDate = DateTime.UtcNow
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("end date is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithSuccessOnly_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            SuccessOnly = true
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("success filter is set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithNoFilters_ReturnsFalse()
    {
        // Arrange
        var filters = new CommandLogFilterOptions();

        // Act & Assert
        filters.HasActiveFilters.Should().BeFalse("no filters are set");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithEmptySearchTerm_ReturnsFalse()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            SearchTerm = ""
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeFalse("empty search term should not count as active filter");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithWhitespaceSearchTerm_ReturnsFalse()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            SearchTerm = "   "
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeFalse("whitespace search term should not count as active filter");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithEmptyCommandName_ReturnsFalse()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            CommandName = ""
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeFalse("empty command name should not count as active filter");
    }

    [Fact]
    public void CommandLogFilterOptions_HasActiveFilters_WithMultipleFilters_ReturnsTrue()
    {
        // Arrange
        var filters = new CommandLogFilterOptions
        {
            SearchTerm = "test",
            GuildId = 123UL,
            StartDate = DateTime.UtcNow,
            SuccessOnly = true
        };

        // Act & Assert
        filters.HasActiveFilters.Should().BeTrue("multiple filters are set");
    }
}
