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
/// "Delete all" must delete only what the filters match and only what existed when the list was
/// rendered (the required <c>before</c> bound).
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
    public async Task DeleteAll_NoFilters_DeletesEverythingUpToTheRenderTime()
    {
        NotificationQueryDto? captured = null;
        _service
            .Setup(s => s.DeleteMatchingAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<string, NotificationQueryDto, CancellationToken>((_, q, _) => captured = q)
            .ReturnsAsync(7);
        var rendered = new DateTimeOffset(DateTime.UtcNow.AddMinutes(-5), TimeSpan.Zero);

        var result = await _controller.DeleteAll(before: rendered);

        result.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().Be(7);
        captured!.Before.Should().Be(rendered.UtcDateTime);
        captured.Before!.Value.Kind.Should().Be(DateTimeKind.Utc);
        captured.Type.Should().BeNull();
        captured.IsRead.Should().BeNull();
        _service.Verify(s => s.DeleteAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAll_WithoutBefore_IsABadRequestAndDeletesNothing()
    {
        var result = await _controller.DeleteAll(isRead: true);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        _service.Verify(s => s.DeleteMatchingAsync(It.IsAny<string>(), It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()), Times.Never);
        _service.Verify(s => s.DeleteAllAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAll_WithABeforeInTheFuture_ClampsItToNow()
    {
        NotificationQueryDto? captured = null;
        _service
            .Setup(s => s.DeleteMatchingAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .Callback<string, NotificationQueryDto, CancellationToken>((_, q, _) => captured = q)
            .ReturnsAsync(0);
        var lower = DateTime.UtcNow;

        await _controller.DeleteAll(before: DateTimeOffset.UtcNow.AddDays(1));

        captured!.Before.Should().BeOnOrAfter(lower).And.BeOnOrBefore(DateTime.UtcNow);
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
            before: DateTimeOffset.UtcNow,
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

        var result = await _controller.DeleteAll(before: DateTimeOffset.UtcNow, isRead: true);

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }
}
