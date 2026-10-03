using System.Security.Claims;
using DiscordBot.Bot.Controllers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace DiscordBot.Tests.Controllers;

/// <summary>
/// "Delete all" with filters must delete only what the filters match; without any it deletes
/// everything, as it always did.
/// </summary>
public class NotificationsControllerDeleteAllTests
{
    private const string UserId = "user-1";

    private readonly Mock<INotificationService> _service = new();
    private readonly NotificationsController _controller;

    public NotificationsControllerDeleteAllTests()
    {
        _controller = new NotificationsController(_service.Object, NullLogger<NotificationsController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, UserId) }, "test"))
                }
            }
        };
    }

    [Fact]
    public async Task DeleteAll_NoFilters_DeletesEverything()
    {
        _service.Setup(s => s.DeleteAllAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(7);

        var result = await _controller.DeleteAll();

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(7);
        _service.Verify(s => s.DeleteMatchingAsync(It.IsAny<string>(), It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAll_WithFilters_DeletesOnlyMatchingAndEndsTheRangeAtTheEndOfTheDay()
    {
        NotificationQueryDto? captured = null;
        _service
            .Setup(s => s.DeleteMatchingAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<string, NotificationQueryDto, CancellationToken>((_, q, _) => captured = q)
            .ReturnsAsync(3);

        var result = await _controller.DeleteAll(
            isRead: true,
            severity: AlertSeverity.Warning,
            startDate: new DateTime(2026, 9, 26),
            endDate: new DateTime(2026, 10, 3),
            searchTerm: "memory");

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(3);
        captured.Should().NotBeNull();
        captured!.IsRead.Should().BeTrue();
        captured.Severity.Should().Be(AlertSeverity.Warning);
        captured.StartDate.Should().Be(new DateTime(2026, 9, 26));
        captured.EndDate.Should().Be(new DateTime(2026, 10, 4).AddTicks(-1));
        captured.SearchTerm.Should().Be("memory");
        _service.Verify(s => s.DeleteAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAll_WithoutAUser_IsUnauthorized()
    {
        _controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        var result = await _controller.DeleteAll(isRead: true);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
