using DiscordBot.Bot.Pages.CommandLogs;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.CommandLogs;

/// <summary>
/// Unit tests for <see cref="DetailsModel"/>.
/// </summary>
public class DetailsModelTests
{
    private readonly Mock<ICommandLogService> _mockCommandLogService;
    private readonly Mock<ILogger<DetailsModel>> _mockLogger;
    private readonly DetailsModel _pageModel;

    public DetailsModelTests()
    {
        _mockCommandLogService = new Mock<ICommandLogService>();
        _mockLogger = new Mock<ILogger<DetailsModel>>();
        _pageModel = new DetailsModel(_mockCommandLogService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task OnGetAsync_WhenLogExists_ReturnsPageResult()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var dto = new CommandLogDto
        {
            Id = logId,
            GuildId = 123456789UL,
            GuildName = "Test Guild",
            UserId = 987654321UL,
            Username = "TestUser",
            CommandName = "ping",
            Parameters = "{\"test\": true}",
            ExecutedAt = DateTime.UtcNow,
            ResponseTimeMs = 100,
            Success = true
        };

        _mockCommandLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _pageModel.OnGetAsync(logId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.ViewModel.Should().NotBeNull();
        _pageModel.ViewModel.Id.Should().Be(logId);
        _pageModel.ViewModel.CommandName.Should().Be("ping");
        _pageModel.ViewModel.GuildName.Should().Be("Test Guild");
    }

    [Fact]
    public async Task OnGetAsync_WhenLogDoesNotExist_ReturnsNotFound()
    {
        // Arrange
        var logId = Guid.NewGuid();

        _mockCommandLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommandLogDto?)null);

        // Act
        var result = await _pageModel.OnGetAsync(logId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task OnGetAsync_CallsServiceWithCorrectId()
    {
        // Arrange
        var logId = Guid.NewGuid();

        _mockCommandLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CommandLogDto?)null);

        // Act
        await _pageModel.OnGetAsync(logId, CancellationToken.None);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_PassesCancellationToken()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();

        _mockCommandLogService
            .Setup(s => s.GetByIdAsync(logId, cancellationToken))
            .ReturnsAsync((CommandLogDto?)null);

        // Act
        await _pageModel.OnGetAsync(logId, cancellationToken);

        // Assert
        _mockCommandLogService.Verify(
            s => s.GetByIdAsync(logId, cancellationToken),
            Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_MapsViewModelCorrectly()
    {
        // Arrange
        var logId = Guid.NewGuid();
        var dto = new CommandLogDto
        {
            Id = logId,
            GuildId = null,
            GuildName = null,
            UserId = 123UL,
            Username = null,
            CommandName = "test",
            Parameters = null,
            ExecutedAt = new DateTime(2023, 1, 1, 12, 0, 0, DateTimeKind.Utc),
            ResponseTimeMs = 50,
            Success = false,
            ErrorMessage = "An error occurred"
        };

        _mockCommandLogService
            .Setup(s => s.GetByIdAsync(logId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        // Act
        var result = await _pageModel.OnGetAsync(logId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<PageResult>();
        _pageModel.ViewModel.GuildName.Should().Be("Direct Message");
        _pageModel.ViewModel.Username.Should().Be("Unknown");
        _pageModel.ViewModel.HasError.Should().BeTrue();
        _pageModel.ViewModel.ErrorMessage.Should().Be("An error occurred");
        _pageModel.ViewModel.Success.Should().BeFalse();
    }
}
