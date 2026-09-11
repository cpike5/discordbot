using System.Security.Claims;
using Bunit;
using DiscordBot.Bot.Interfaces;
using DiscordBot.Bot.Services.Realtime;
using DiscordBot.Bot.Services.Realtime.Events;
using DiscordBot.Bot.Blazor.Shared;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Enums;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Shared.Widgets;

public class NotificationBellTests : BlazorComponentTestContext
{
    private const string UserId = "user-123";

    private readonly Mock<IDashboardNotificationQueryService> _queryService = new();

    public NotificationBellTests()
    {
        _queryService
            .Setup(s => s.GetNotificationSummaryAsync(UserId, null))
            .ReturnsAsync(new NotificationSummaryDto { TotalUnread = 2 });
        _queryService
            .Setup(s => s.GetNotificationsAsync(UserId, null, 15))
            .ReturnsAsync(new List<UserNotificationDto>
            {
                new() { Id = Guid.NewGuid(), Title = "First", Message = "First message", Type = NotificationType.BotStatus, TypeDisplay = "Bot Status", IsRead = false, CreatedAt = DateTime.UtcNow },
                new() { Id = Guid.NewGuid(), Title = "Second", Message = "Second message", Type = NotificationType.GuildEvent, TypeDisplay = "Guild Event", IsRead = true, CreatedAt = DateTime.UtcNow }
            });

        Services.AddSingleton(_queryService.Object);

        AddAuthorization().SetAuthorized("tester").SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));
    }

    [Fact]
    public void OnInitialized_ShowsUnreadBadge_FromSummary()
    {
        var cut = Render<NotificationBell>();
        cut.Find(".notification-badge").TextContent.Should().Be("2");
    }

    [Fact]
    public void Toggle_OpensDropdown_AndLoadsNotifications()
    {
        var cut = Render<NotificationBell>();

        cut.Find("button[aria-label='Notifications']").Click();

        cut.Find(".notification-dropdown").ClassList.Should().Contain("active");
        cut.Markup.Should().Contain("First message");
        cut.Markup.Should().Contain("Second message");
        _queryService.Verify(s => s.GetNotificationsAsync(UserId, null, 15), Times.Once);
    }

    [Fact]
    public void MarkAllAsRead_CallsService_AndClearsBadge()
    {
        var cut = Render<NotificationBell>();
        cut.Find("button[aria-label='Notifications']").Click();

        cut.Find(".notification-mark-all-read").Click();

        _queryService.Verify(s => s.MarkAllNotificationsReadAsync(UserId, null), Times.Once);
        cut.Find(".notification-badge").ClassList.Should().Contain("hidden");
    }

    [Fact]
    public void Dismiss_RemovesItem_FromList()
    {
        var cut = Render<NotificationBell>();
        cut.Find("button[aria-label='Notifications']").Click();
        cut.Markup.Should().Contain("First message");

        cut.FindAll("button[aria-label='Dismiss notification']")[0].Click();

        _queryService.Verify(s => s.DismissNotificationAsync(UserId, null, It.IsAny<Guid>()), Times.Once);
        cut.Markup.Should().NotContain("First message");
    }

    [Fact]
    public async Task NotificationReceivedEvent_UserScoped_PrependsItem_AndAnnounces()
    {
        var cut = Render<NotificationBell>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        await bus.PublishAsync(new NotificationReceivedEvent
        {
            UserId = UserId,
            Notification = new UserNotificationDto { Id = Guid.NewGuid(), Title = "Live", Message = "Live message", Type = NotificationType.CommandError, TypeDisplay = "Command Error", CreatedAt = DateTime.UtcNow }
        });

        cut.WaitForAssertion(() => cut.Markup.Should().Contain("New notification: Live"), TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task NotificationReceivedEvent_ForDifferentUser_IsIgnored()
    {
        var cut = Render<NotificationBell>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        // The user-scoped Subscribe overload filters synchronously inside PublishAsync, so by
        // the time this await returns the event has already been dispatched (or, as expected
        // here, not dispatched at all) - no wait needed to know that.
        await bus.PublishAsync(new NotificationReceivedEvent
        {
            UserId = "someone-else",
            Notification = new UserNotificationDto { Id = Guid.NewGuid(), Title = "NotForUs", Message = "x", Type = NotificationType.BotStatus, TypeDisplay = "Bot Status", CreatedAt = DateTime.UtcNow }
        });

        cut.Markup.Should().NotContain("NotForUs");
    }

    [Fact]
    public async Task Dispose_UnsubscribesFromBus()
    {
        var cut = Render<NotificationBell>();
        var bus = Services.GetRequiredService<IDashboardEventBus>();

        // First prove the subscription is live before disposal (initial unread count is 2 from
        // the constructor's summary mock, so use a distinct value), so the "does not re-render"
        // check below is actually meaningful.
        await bus.PublishAsync(new NotificationCountChangedEvent { UserId = UserId, Summary = new NotificationSummaryDto { TotalUnread = 42 } });
        cut.WaitForAssertion(() => cut.Find(".notification-badge").TextContent.Should().Be("42"), TimeSpan.FromSeconds(3));

        await DisposeComponentsAsync();
        var renderCountAfterDispose = cut.RenderCount;

        await bus.PublishAsync(new NotificationCountChangedEvent { UserId = UserId, Summary = new NotificationSummaryDto { TotalUnread = 99 } });
        // Short window, not a full debounce-window sleep: Dispose() unsubscribes synchronously,
        // so there is nothing left to debounce - this only guards against a latent regression.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        cut.RenderCount.Should().Be(renderCountAfterDispose);
    }
}
