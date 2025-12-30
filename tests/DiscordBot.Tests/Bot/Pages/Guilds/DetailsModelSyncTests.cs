using DiscordBot.Bot.Pages.Guilds;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace DiscordBot.Tests.Bot.Pages.Guilds;

/// <summary>
/// Unit tests for <see cref="DetailsModel"/> sync functionality.
/// </summary>
public class DetailsModelSyncTests
{
    private readonly Mock<IGuildService> _mockGuildService;
    private readonly Mock<ICommandLogService> _mockCommandLogService;
    private readonly Mock<IWelcomeService> _mockWelcomeService;
    private readonly Mock<IScheduledMessageService> _mockScheduledMessageService;
    private readonly Mock<IRatWatchService> _mockRatWatchService;
    private readonly Mock<ILogger<DetailsModel>> _mockLogger;
    private readonly DetailsModel _detailsModel;

    public DetailsModelSyncTests()
    {
        _mockGuildService = new Mock<IGuildService>();
        _mockCommandLogService = new Mock<ICommandLogService>();
        _mockWelcomeService = new Mock<IWelcomeService>();
        _mockScheduledMessageService = new Mock<IScheduledMessageService>();
        _mockRatWatchService = new Mock<IRatWatchService>();
        _mockLogger = new Mock<ILogger<DetailsModel>>();

        // Setup default scheduled message service behavior
        _mockScheduledMessageService.Setup(s => s.GetByGuildIdAsync(
            It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<ScheduledMessageDto>(), 0));

        // Setup default Rat Watch service behavior
        _mockRatWatchService.Setup(s => s.GetGuildSettingsAsync(It.IsAny<ulong>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuildRatWatchSettings { IsEnabled = true, Timezone = "Eastern Standard Time" });
        _mockRatWatchService.Setup(s => s.GetByGuildAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<RatWatchDto>(), 0));
        _mockRatWatchService.Setup(s => s.GetLeaderboardAsync(It.IsAny<ulong>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<RatLeaderboardEntryDto>());

        _detailsModel = new DetailsModel(
            _mockGuildService.Object,
            _mockCommandLogService.Object,
            _mockWelcomeService.Object,
            _mockScheduledMessageService.Object,
            _mockRatWatchService.Object,
            _mockLogger.Object);

        SetupPageContext(isAjax: false);
    }

    private void SetupPageContext(bool isAjax)
    {
        var httpContext = new DefaultHttpContext();

        if (isAjax)
        {
            httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        }

        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);
        var pageContext = new PageContext(actionContext);

        _detailsModel.PageContext = pageContext;

        // Setup TempData
        _detailsModel.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        // Setup URL helper
        var urlHelper = new Mock<IUrlHelper>();
        urlHelper.Setup(u => u.Content(It.IsAny<string>()))
            .Returns<string>(path => path);
        _detailsModel.Url = urlHelper.Object;
    }

    [Fact]
    public async Task OnPostSyncAsync_WhenSuccessful_ReturnsJsonWithSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: true);

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>("AJAX request should return JsonResult");

        var jsonResult = (JsonResult)result;
        var value = jsonResult.Value;

        value.Should().NotBeNull();
        value.Should().BeEquivalentTo(new
        {
            success = true,
            message = "Guild synced successfully"
        }, "successful sync should return success=true with message");

        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnPostSyncAsync_WhenGuildNotFound_ReturnsJsonWithFailure()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        SetupPageContext(isAjax: true);

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>("AJAX request should return JsonResult");

        var jsonResult = (JsonResult)result;
        var value = jsonResult.Value;

        value.Should().NotBeNull();
        value.Should().BeEquivalentTo(new
        {
            success = false,
            message = "Guild not found in Discord client"
        }, "failed sync should return success=false with message");

        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnPostSyncAsync_NonAjax_RedirectsWithTempData()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: false);

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>("non-AJAX request should redirect");

        var redirectResult = (RedirectToPageResult)result;
        redirectResult.RouteValues.Should().ContainKey("id")
            .WhoseValue.Should().Be(guildId, "redirect should include guild ID");

        _detailsModel.SuccessMessage.Should().Be("Guild synced successfully",
            "TempData should contain success message");

        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnPostSyncAsync_NonAjax_WhenGuildNotFound_RedirectsWithoutSuccessMessage()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        SetupPageContext(isAjax: false);

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>("non-AJAX request should redirect");

        var redirectResult = (RedirectToPageResult)result;
        redirectResult.RouteValues.Should().ContainKey("id")
            .WhoseValue.Should().Be(guildId, "redirect should include guild ID");

        _detailsModel.SuccessMessage.Should().BeNull(
            "TempData should not contain success message for failed sync");

        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnPostSyncAsync_WithException_ReturnsJsonWithError()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: true);

        var expectedException = new InvalidOperationException("Test exception");
        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<JsonResult>("AJAX request should return JsonResult");

        var jsonResult = (JsonResult)result;
        var value = jsonResult.Value;

        value.Should().NotBeNull();
        value.Should().BeEquivalentTo(new
        {
            success = false,
            message = "An error occurred while syncing the guild"
        }, "exception should return success=false with error message");

        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnPostSyncAsync_NonAjax_WithException_RedirectsWithoutSuccessMessage()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: false);

        var expectedException = new InvalidOperationException("Test exception");
        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        var result = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        result.Should().BeOfType<RedirectToPageResult>("non-AJAX request should redirect");

        var redirectResult = (RedirectToPageResult)result;
        redirectResult.RouteValues.Should().ContainKey("id")
            .WhoseValue.Should().Be(guildId, "redirect should include guild ID");

        _detailsModel.SuccessMessage.Should().BeNull(
            "TempData should not contain success message when exception occurs");

        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()),
            Times.Once,
            "guild service should be called once");
    }

    [Fact]
    public async Task OnPostSyncAsync_LogsInformationOnSuccess()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: true);

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("User requesting sync") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information when sync is requested");

        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Successfully synced guild") &&
                    v.ToString()!.Contains(guildId.ToString())),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log information when sync succeeds");
    }

    [Fact]
    public async Task OnPostSyncAsync_LogsWarningOnFailure()
    {
        // Arrange
        const ulong guildId = 999999999UL;
        SetupPageContext(isAjax: true);

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Failed to sync guild") &&
                    v.ToString()!.Contains(guildId.ToString()) &&
                    v.ToString()!.Contains("not found in Discord")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log warning when guild is not found");
    }

    [Fact]
    public async Task OnPostSyncAsync_LogsErrorOnException()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: true);

        var expectedException = new InvalidOperationException("Test exception");
        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(expectedException);

        // Act
        await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);

        // Assert
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) =>
                    v.ToString()!.Contains("Error syncing guild") &&
                    v.ToString()!.Contains(guildId.ToString())),
                expectedException,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "should log error when exception occurs");
    }

    [Fact]
    public async Task OnPostSyncAsync_PassesCancellationTokenToService()
    {
        // Arrange
        const ulong guildId = 123456789UL;
        SetupPageContext(isAjax: true);

        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        await _detailsModel.OnPostSyncAsync(guildId, cancellationToken);

        // Assert
        _mockGuildService.Verify(
            s => s.SyncGuildAsync(guildId, cancellationToken),
            Times.Once,
            "cancellation token should be passed to guild service");
    }

    [Fact]
    public async Task OnPostSyncAsync_DetectsAjaxRequestCorrectly()
    {
        // Arrange
        const ulong guildId = 123456789UL;

        _mockGuildService
            .Setup(s => s.SyncGuildAsync(guildId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Test with AJAX request
        SetupPageContext(isAjax: true);
        var ajaxResult = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);
        ajaxResult.Should().BeOfType<JsonResult>("AJAX request should return JSON");

        // Test with non-AJAX request
        SetupPageContext(isAjax: false);
        var nonAjaxResult = await _detailsModel.OnPostSyncAsync(guildId, CancellationToken.None);
        nonAjaxResult.Should().BeOfType<RedirectToPageResult>("non-AJAX request should redirect");
    }
}
