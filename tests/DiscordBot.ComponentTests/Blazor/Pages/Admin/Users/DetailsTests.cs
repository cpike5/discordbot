using System.Security.Claims;
using Bunit;
using Bunit.TestDoubles;
using DiscordBot.Bot.Blazor.Pages.Admin.Users;
using DiscordBot.ComponentTests.TestHelpers;
using DiscordBot.Core.DTOs;
using DiscordBot.Core.Entities;
using DiscordBot.Core.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace DiscordBot.ComponentTests.Blazor.Pages.Admin.Users;

/// <summary>
/// Component tests for <see cref="Details"/>, the routable replacement for
/// <c>Pages/Admin/Users/Details.cshtml</c> + <c>DetailsModel</c> (docs/plans/blazor-port-plan.md §5
/// Phase 4, cluster 4a).
/// </summary>
public class DetailsTests : BlazorComponentTestContext
{
    private const string CurrentUserId = "admin-1";
    private const string TargetUserId = "user-2";

    private readonly Mock<IUserManagementService> _service = new();

    public DetailsTests()
    {
        Services.AddSingleton(_service.Object);
        AddAuthorization().SetAuthorized("admin").SetClaims(new Claim(ClaimTypes.NameIdentifier, CurrentUserId));
    }

    private static UserDto BuildUser(string id, string role = "Admin", bool discordLinked = false) => new()
    {
        Id = id,
        Email = "target@example.test",
        DisplayName = "Target User",
        IsActive = true,
        EmailConfirmed = true,
        CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        Roles = new[] { role },
        IsDiscordLinked = discordLinked,
        DiscordUsername = discordLinked ? "targetTag" : null,
        DiscordUserId = discordLinked ? 123456789012345678UL : null
    };

    private static PaginatedResponseDto<UserActivityLogDto> ActivityPage(params UserActivityLogDto[] items) => new()
    {
        Items = items,
        Page = 1,
        PageSize = 20,
        TotalCount = items.Length
    };

    private IRenderedComponent<Details> RenderWithId(string id)
    {
        var navMan = (BunitNavigationManager)Services.GetRequiredService<NavigationManager>();
        navMan.NavigateTo(navMan.GetUriWithQueryParameter("id", id));
        return Render<Details>();
    }

    [Fact]
    public void MissingId_ShowsNotFoundEmptyState()
    {
        var cut = Render<Details>();

        cut.Markup.Should().Contain("User Not Found");
    }

    [Fact]
    public void UnknownId_ShowsNotFoundEmptyState()
    {
        _service.Setup(s => s.GetUserByIdAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync((UserDto?)null);

        var cut = RenderWithId("missing");

        cut.Markup.Should().Contain("User Not Found");
    }

    [Fact]
    public void KnownUser_RendersProfileCardAndDiscordCard()
    {
        _service.Setup(s => s.GetUserByIdAsync(TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(TargetUserId, discordLinked: true));
        _service.Setup(s => s.GetActivityLogAsync(TargetUserId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(ActivityPage());
        _service.Setup(s => s.CanManageUserAsync(CurrentUserId, TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = RenderWithId(TargetUserId);

        cut.Markup.Should().Contain("Target User");
        cut.Markup.Should().Contain("target@example.test");
        cut.Markup.Should().Contain("targetTag");
        cut.FindAll("a[href='/Admin/Users/Edit?id=user-2']").Should().HaveCount(1);
        // "Member Since" renders through <LocalTime> (Blazor/Shared/Primitives/LocalTime.razor),
        // not a raw <time data-utc> the review flagged this page for still emitting.
        cut.Find("time[data-utc][data-format='date']").TextContent.Should().Contain("Jan");
    }

    [Fact]
    public void NoDiscordLink_ShowsNotLinkedCard()
    {
        _service.Setup(s => s.GetUserByIdAsync(TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(TargetUserId));
        _service.Setup(s => s.GetActivityLogAsync(TargetUserId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(ActivityPage());
        _service.Setup(s => s.CanManageUserAsync(CurrentUserId, TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var cut = RenderWithId(TargetUserId);

        cut.Markup.Should().Contain("No Discord account linked");
    }

    [Fact]
    public void CannotManage_HidesEditLink()
    {
        _service.Setup(s => s.GetUserByIdAsync(TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(TargetUserId));
        _service.Setup(s => s.GetActivityLogAsync(TargetUserId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(ActivityPage());
        _service.Setup(s => s.CanManageUserAsync(CurrentUserId, TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var cut = RenderWithId(TargetUserId);

        cut.FindAll("a[href='/Admin/Users/Edit?id=user-2']").Should().BeEmpty();
    }

    [Fact]
    public void ActivityLog_RendersRows_WithBadgesPerAction()
    {
        _service.Setup(s => s.GetUserByIdAsync(TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(TargetUserId));
        _service.Setup(s => s.CanManageUserAsync(CurrentUserId, TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _service.Setup(s => s.GetActivityLogAsync(TargetUserId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(ActivityPage(
            new UserActivityLogDto { Id = Guid.NewGuid(), ActorEmail = "admin@example.test", Action = UserActivityAction.PasswordReset, Timestamp = DateTime.UtcNow }));

        var cut = RenderWithId(TargetUserId);

        cut.Markup.Should().Contain("Password Reset");
        cut.Markup.Should().Contain("admin@example.test");
    }

    [Fact]
    public void NoActivity_ShowsEmptyState()
    {
        _service.Setup(s => s.GetUserByIdAsync(TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(BuildUser(TargetUserId));
        _service.Setup(s => s.CanManageUserAsync(CurrentUserId, TargetUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _service.Setup(s => s.GetActivityLogAsync(TargetUserId, 1, 20, It.IsAny<CancellationToken>())).ReturnsAsync(ActivityPage());

        var cut = RenderWithId(TargetUserId);

        cut.Markup.Should().Contain("No activity yet");
    }
}
