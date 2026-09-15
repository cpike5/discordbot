using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Services;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using IndexPage = DiscordBot.Bot.Blazor.Pages.Admin.Notifications.Index;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Notifications;

/// <summary>
/// Component tests for <see cref="IndexPage"/>, the routable replacement for
/// <c>Pages/Admin/Notifications/Index.cshtml</c> + <c>IndexModel</c>
/// (docs/plans/blazor-port-plan.md §5 Phase 4, cluster 4d).
/// </summary>
public class IndexTests : BlazorComponentTestContext
{
    private const string UserId = "user-1";

    private readonly Mock<INotificationService> _service = new();
    private readonly Mock<IGuildService> _guildService = new();

    public IndexTests()
    {
        Services.AddSingleton(_service.Object);
        Services.AddSingleton(_guildService.Object);
        _guildService.Setup(g => g.GetAllGuildsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        AddAuthorization().SetAuthorized("user").SetClaims(new Claim(ClaimTypes.NameIdentifier, UserId));
        SetInteractiveRendererInfo();
    }

    private static UserNotificationDto BuildNotification(Guid id, bool isRead = false, string title = "Hello") => new()
    {
        Id = id,
        Type = DiscordBot.Core.Enums.NotificationType.GuildEvent,
        TypeDisplay = "Guild Event",
        Title = title,
        Message = "msg",
        IsRead = isRead,
        CreatedAt = DateTime.UtcNow,
        TimeAgo = "just now"
    };

    private static PaginatedResponseDto<UserNotificationDto> Page(params UserNotificationDto[] items) => new()
    {
        Items = items,
        Page = 1,
        PageSize = 25,
        TotalCount = items.Length
    };

    [Fact]
    public void BulkMarkRead_CallsService_WithSelectedIds()
    {
        var n1 = BuildNotification(Guid.NewGuid());
        var n2 = BuildNotification(Guid.NewGuid());
        _service.Setup(s => s.GetUserNotificationsPagedAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(n1, n2));
        _service.Setup(s => s.MarkMultipleAsReadAsync(UserId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cut = Render<IndexPage>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='notification-row']").Should().HaveCount(2));

        cut.FindAll("input[type='checkbox']")[0].Change(true);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Mark Read").Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.MarkMultipleAsReadAsync(
            UserId, It.Is<IEnumerable<Guid>>(ids => ids.Single() == n1.Id), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void BulkDelete_CallsService_WithSelectedIds()
    {
        var n1 = BuildNotification(Guid.NewGuid());
        _service.Setup(s => s.GetUserNotificationsPagedAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page(n1));
        _service.Setup(s => s.DeleteMultipleAsync(UserId, It.IsAny<IEnumerable<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var cut = Render<IndexPage>();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='notification-row']").Should().HaveCount(1));

        cut.Find("input[type='checkbox']").Change(true);
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Delete Selected").Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.DeleteMultipleAsync(
            UserId, It.Is<IEnumerable<Guid>>(ids => ids.Single() == n1.Id), It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void MarkAllRead_CallsService()
    {
        _service.Setup(s => s.GetUserNotificationsPagedAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());
        _service.Setup(s => s.MarkAllAsReadAsync(UserId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var cut = Render<IndexPage>();
        cut.WaitForAssertion(() => cut.Markup.Should().Contain("Mark All Read"));

        cut.FindAll("button").Single(b => b.TextContent.Trim().Contains("Mark All Read")).Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.MarkAllAsReadAsync(UserId, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void DeleteAll_RequiresTypedConfirmation_ThenCallsService()
    {
        _service.Setup(s => s.GetUserNotificationsPagedAsync(UserId, It.IsAny<NotificationQueryDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());
        _service.Setup(s => s.DeleteAllAsync(UserId, It.IsAny<CancellationToken>())).ReturnsAsync(5);

        var cut = Render<IndexPage>();
        cut.FindAll("button").Single(b => b.TextContent.Trim() == "Delete All").Click();

        var confirmButton = cut.FindAll("#deleteAllModal button").Single(b => b.TextContent.Trim() == "Delete All");
        confirmButton.HasAttribute("disabled").Should().BeTrue();

        cut.Find("#deleteAllModalInput").Input("DELETE");
        confirmButton = cut.FindAll("#deleteAllModal button").Single(b => b.TextContent.Trim() == "Delete All");
        confirmButton.HasAttribute("disabled").Should().BeFalse();
        confirmButton.Click();

        cut.WaitForAssertion(() => _service.Verify(s => s.DeleteAllAsync(UserId, It.IsAny<CancellationToken>()), Times.Once));
    }

    [Fact]
    public void SupplyParameterFromQuery_PassesFiltersToTheService()
    {
        // Type/Severity are query-bound as int? (see Index.razor.cs's TypeQuery note) - GuildEvent=3.
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo("/Admin/Notifications?Type=3&IsRead=false&SearchTerm=hi");
        _service.Setup(s => s.GetUserNotificationsPagedAsync(UserId, It.Is<NotificationQueryDto>(q =>
                q.Type == DiscordBot.Core.Enums.NotificationType.GuildEvent && q.IsRead == false && q.SearchTerm == "hi"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Page());

        Render<IndexPage>();

        _service.Verify(s => s.GetUserNotificationsPagedAsync(UserId, It.Is<NotificationQueryDto>(q =>
            q.Type == DiscordBot.Core.Enums.NotificationType.GuildEvent && q.IsRead == false && q.SearchTerm == "hi"),
            It.IsAny<CancellationToken>()), Times.Once);
    }
}
